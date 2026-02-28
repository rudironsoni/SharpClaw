using System.Collections.Concurrent;
using System.Text.Json;
using SharpClaw.Protocol.Contracts;
using SharpClaw.Runs;

namespace SharpClaw.Gateway;

public delegate Task<ResponseFrame> GatewayRequestHandler(RequestFrame request, CancellationToken cancellationToken);

public sealed class GatewayDispatcher
{
    private readonly ConcurrentDictionary<string, GatewayRequestHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(string method, GatewayRequestHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(handler);

        if (!_handlers.TryAdd(method, handler))
        {
            throw new InvalidOperationException($"Handler already registered for method '{method}'.");
        }
    }

    public async Task<ResponseFrame> DispatchAsync(RequestFrame request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_handlers.TryGetValue(request.Method, out var handler))
        {
            return new ResponseFrame(
                request.Id,
                Ok: false,
                Error: new ErrorShape(ErrorCodes.InvalidRequest, $"Unknown method: {request.Method}"));
        }

        var response = await handler(request, cancellationToken).ConfigureAwait(false);

        return response with { Id = request.Id };
    }
}

public sealed class ConnectionRegistry
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _connections = new(StringComparer.Ordinal);

    public bool TryConnect(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        return _connections.TryAdd(connectionId, DateTimeOffset.UtcNow);
    }

    public bool TryDisconnect(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        return _connections.TryRemove(connectionId, out _);
    }

    public int ActiveCount => _connections.Count;
}

public sealed record GatewayHealthSnapshot(DateTimeOffset ObservedAt, int ActiveConnections, bool IsHealthy);

public sealed class GatewayHealthService(ConnectionRegistry connectionRegistry)
{
    private readonly ConnectionRegistry _connectionRegistry = connectionRegistry;

    public GatewayHealthSnapshot Snapshot()
    {
        var active = _connectionRegistry.ActiveCount;
        return new GatewayHealthSnapshot(DateTimeOffset.UtcNow, active, IsHealthy: true);
    }
}

public sealed record KeepalivePolicy(TimeSpan PingInterval, TimeSpan Timeout)
{
    public static KeepalivePolicy Default => new(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45));

    public bool IsValid()
    {
        return PingInterval > TimeSpan.Zero
            && Timeout > TimeSpan.Zero
            && Timeout >= PingInterval;
    }
}

public static class GatewayMethodRegistration
{
    public static void RegisterCoreMethods(GatewayDispatcher dispatcher, RunCoordinator runCoordinator)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(runCoordinator);

        dispatcher.Register(
            "ping",
            static (request, _) => Task.FromResult(new ResponseFrame(request.Id, Ok: true, Payload: new { pong = true })));

        dispatcher.Register(
            "chat.send",
            async (request, cancellationToken) =>
            {
                var input = TryReadString(request.Payload, "message") ?? string.Empty;
                var result = await runCoordinator.StartAsync(input, request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
                return new ResponseFrame(request.Id, Ok: true, Payload: new { runId = result.RunId, status = result.Status });
            });

        dispatcher.Register(
            "chat.abort",
            async (request, cancellationToken) =>
            {
                var runId = TryReadString(request.Payload, "runId");
                if (string.IsNullOrWhiteSpace(runId))
                {
                    return new ResponseFrame(
                        request.Id,
                        Ok: false,
                        Error: new ErrorShape(ErrorCodes.InvalidRequest, "runId is required for chat.abort."));
                }

                var snapshot = await runCoordinator.AbortAsync(runId, cancellationToken).ConfigureAwait(false);
                if (snapshot.Status == "not_found")
                {
                    return new ResponseFrame(
                        request.Id,
                        Ok: false,
                        Error: new ErrorShape(ErrorCodes.InvalidRequest, $"Unknown runId: {runId}"));
                }

                return new ResponseFrame(request.Id, Ok: true, Payload: new { runId = snapshot.RunId, status = snapshot.Status });
            });

        dispatcher.Register(
            "chat.status",
            (request, _) =>
            {
                var runId = TryReadString(request.Payload, "runId");
                if (string.IsNullOrWhiteSpace(runId))
                {
                    return Task.FromResult(new ResponseFrame(
                        request.Id,
                        Ok: false,
                        Error: new ErrorShape(ErrorCodes.InvalidRequest, "runId is required for chat.status.")));
                }

                var snapshot = runCoordinator.GetSnapshot(runId);
                if (snapshot.Status == "not_found")
                {
                    return Task.FromResult(new ResponseFrame(
                        request.Id,
                        Ok: false,
                        Error: new ErrorShape(ErrorCodes.InvalidRequest, $"Unknown runId: {runId}")));
                }

                return Task.FromResult(new ResponseFrame(request.Id, Ok: true, Payload: new { runId = snapshot.RunId, status = snapshot.Status }));
            });
    }

    private static string? TryReadString(object? payload, string propertyName)
    {
        if (payload is null)
        {
            return null;
        }

        if (payload is JsonElement json && json.ValueKind == JsonValueKind.Object)
        {
            if (json.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }

            return null;
        }

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}
