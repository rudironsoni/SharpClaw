using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SharpClaw.Gateway;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.HttpApi;

public static class WebSocketGatewayEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task HandleAsync(HttpContext context, GatewayDispatcher dispatcher)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Expected WebSocket request." });
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var buffer = new byte[16 * 1024];

        while (socket.State == WebSocketState.Open)
        {
            var receive = await socket.ReceiveAsync(buffer, context.RequestAborted);

            if (receive.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", context.RequestAborted);
                break;
            }

            if (receive.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            var requestJson = Encoding.UTF8.GetString(buffer, 0, receive.Count);
            ResponseFrame response;

            try
            {
                var request = JsonSerializer.Deserialize<RequestFrame>(requestJson, JsonOptions);
                if (request is null)
                {
                    response = new ResponseFrame(
                        Id: "invalid",
                        Ok: false,
                        Error: new ErrorShape(ErrorCodes.InvalidRequest, "Request payload is required."));
                }
                else
                {
                    response = await dispatcher.DispatchAsync(request, context.RequestAborted);
                }
            }
            catch (JsonException)
            {
                response = new ResponseFrame(
                    Id: "invalid",
                    Ok: false,
                    Error: new ErrorShape(ErrorCodes.InvalidRequest, "Invalid JSON payload."));
            }

            var payload = JsonSerializer.Serialize(response, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(payload);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, context.RequestAborted);
        }
    }
}
