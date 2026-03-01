using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SharpClaw.Abstractions.Identity;
using SharpClaw.Gateway;
using SharpClaw.Protocol.Contracts;
using SharpClaw.Tenancy;

namespace SharpClaw.HttpApi;

/// <summary>
/// WebSocket gateway endpoint with Protocol v3 handshake, authentication, and keepalive.
/// </summary>
public static class WebSocketGatewayEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task HandleAsync(
        HttpContext context, 
        GatewayDispatcher dispatcher,
        IIdentityService identityService,
        ConnectionRegistry connectionRegistry,
        ILogger logger)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Expected WebSocket request." });
            return;
        }

        // Extract tenant context
        var tenantId = AsyncLocalTenantContext.Current.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant context required." });
            return;
        }

        // Perform Protocol v3 handshake
        var handshakeResult = await PerformHandshakeAsync(context, identityService, tenantId, logger);
        if (!handshakeResult.IsSuccess)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = handshakeResult.ErrorMessage });
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString("N");
        
        // Register connection
        connectionRegistry.TryConnect(connectionId);
        logger.LogInformation("WebSocket connection {ConnectionId} established for tenant {TenantId}", 
            connectionId, tenantId);

        try
        {
            if (handshakeResult.Device == null)
            {
                await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Device not available", CancellationToken.None);
                return;
            }

            await HandleConnectionAsync(socket, dispatcher, handshakeResult.Device, tenantId, connectionId, logger);
        }
        finally
        {
            connectionRegistry.TryDisconnect(connectionId);
            logger.LogInformation("WebSocket connection {ConnectionId} closed", connectionId);
        }
    }

    private static async Task<HandshakeResult> PerformHandshakeAsync(
        HttpContext context,
        IIdentityService identityService,
        string tenantId,
        ILogger logger)
    {
        // Extract device token from query string or header
        var deviceToken = context.Request.Query["token"].FirstOrDefault() 
            ?? context.Request.Headers["X-Device-Token"].FirstOrDefault();

        if (string.IsNullOrEmpty(deviceToken))
        {
            logger.LogWarning("WebSocket handshake failed: No device token provided");
            return HandshakeResult.Failed("Device token required");
        }

        // Get device and verify pairing
        var device = await identityService.GetDeviceAsync(deviceToken, tenantId);
        if (device == null)
        {
            logger.LogWarning("WebSocket handshake failed: Device not found");
            return HandshakeResult.Failed("Device not registered");
        }

        if (!device.IsPaired)
        {
            logger.LogWarning("WebSocket handshake failed: Device not paired");
            return HandshakeResult.Failed("Device pairing required");
        }

        return HandshakeResult.Success(device);
    }

    private static async Task HandleConnectionAsync(
        WebSocket socket,
        GatewayDispatcher dispatcher,
#pragma warning disable IDE0060 // Parameters reserved for future use
        DeviceIdentity device,
        string tenantId,
#pragma warning restore IDE0060
        string connectionId,
        ILogger logger)
    {
        var buffer = new byte[16 * 1024];
        var lastPingTime = DateTimeOffset.UtcNow;
        var keepalivePolicy = KeepalivePolicy.Default;

        while (socket.State == WebSocketState.Open)
        {
            // Check keepalive timeout
            if (DateTimeOffset.UtcNow - lastPingTime > keepalivePolicy.Timeout)
            {
                logger.LogWarning("Connection {ConnectionId} timed out", connectionId);
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Keepalive timeout", CancellationToken.None);
                break;
            }

            // Send ping if needed
            if (DateTimeOffset.UtcNow - lastPingTime > keepalivePolicy.PingInterval)
            {
                var pingFrame = new RequestFrame("ping", "ping", null, null);
                var pingJson = JsonSerializer.Serialize(pingFrame, JsonOptions);
                var pingBytes = Encoding.UTF8.GetBytes(pingJson);
                await socket.SendAsync(pingBytes, WebSocketMessageType.Text, true, CancellationToken.None);
                lastPingTime = DateTimeOffset.UtcNow;
            }

            // Receive with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            WebSocketReceiveResult? receive = null;
            
            try
            {
                receive = await socket.ReceiveAsync(buffer, cts.Token);
            }
            catch (OperationCanceledException)
            {
                continue; // Timeout, check keepalive
            }

            if (receive.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                break;
            }

            if (receive.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            lastPingTime = DateTimeOffset.UtcNow;
            var requestJson = Encoding.UTF8.GetString(buffer, 0, receive.Count);
            
            try
            {
                var request = JsonSerializer.Deserialize<RequestFrame>(requestJson, JsonOptions);
                if (request == null)
                {
                    await SendErrorAsync(socket, "invalid", ErrorCodes.InvalidRequest, "Request payload is required.");
                    continue;
                }

                // Handle ping
                if (request.Method == "ping")
                {
                    var pongFrame = new ResponseFrame(request.Id, true, new { pong = true });
                    await SendResponseAsync(socket, pongFrame);
                    continue;
                }

                var deviceContext = new SharpClaw.Gateway.DeviceContext(device.DeviceId, device.Scopes, true);
                var response = await dispatcher.DispatchAsync(request, deviceContext, CancellationToken.None);
                await SendResponseAsync(socket, response);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Invalid JSON received on connection {ConnectionId}", connectionId);
                await SendErrorAsync(socket, "invalid", ErrorCodes.InvalidRequest, "Invalid JSON payload.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing request on connection {ConnectionId}", connectionId);
                await SendErrorAsync(socket, "error", ErrorCodes.InternalError, "Internal server error.");
            }
        }
    }

    private static async Task SendResponseAsync(WebSocket socket, ResponseFrame response)
    {
        var payload = JsonSerializer.Serialize(response, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(payload);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task SendErrorAsync(WebSocket socket, string id, string code, string message)
    {
        var response = new ResponseFrame(id, false, Error: new ErrorShape(code, message));
        await SendResponseAsync(socket, response);
    }
}

public sealed record HandshakeResult(bool IsSuccess, DeviceIdentity? Device, string? ErrorMessage)
{
    public static HandshakeResult Success(DeviceIdentity device) => new(true, device, null);
    public static HandshakeResult Failed(string error) => new(false, null, error);
}
