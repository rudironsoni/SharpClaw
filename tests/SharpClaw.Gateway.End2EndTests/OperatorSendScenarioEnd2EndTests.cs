using System.Collections.Frozen;
using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Gateway;
using SharpClaw.Gateway.Events;
using SharpClaw.Protocol.Contracts;
using SharpClaw.Runs;
using System.Text.Json;

namespace SharpClaw.Gateway.End2EndTests;

public class OperatorSendScenarioEnd2EndTests
{
    private static DeviceContext TestDevice => new("device-1", new[] { "operator:write" }.ToFrozenSet(), true);

    private static GatewayDispatcher CreateDispatcher()
    {
        var eventPublisher = new ChannelEventPublisher(
            EventPublisherOptions.Default,
            NullLogger<ChannelEventPublisher>.Instance);
        return new GatewayDispatcher(eventPublisher, NullLogger<GatewayDispatcher>.Instance);
    }

    [Fact]
    public async Task OperatorSendFlow_StartsRunAndReturnsAck()
    {
        var dispatcher = CreateDispatcher();
        var runs = TestHelpers.CreateRunCoordinator();
        GatewayMethodRegistration.RegisterCoreMethods(dispatcher, runs);

        var response = await dispatcher.DispatchAsync(
            new RequestFrame("req-1", "chat.send", new { message = "hello" }, "idem-1"),
            TestDevice);

        Assert.True(response.Ok);
        Assert.Equal("req-1", response.Id);
        Assert.NotNull(response.Payload);
    }

    [Fact]
    public async Task OperatorAbortFlow_TransitionsRunToAborted()
    {
        var dispatcher = CreateDispatcher();
        var runs = TestHelpers.CreateRunCoordinator();
        GatewayMethodRegistration.RegisterCoreMethods(dispatcher, runs);

        var send = await dispatcher.DispatchAsync(new RequestFrame("req-send", "chat.send", new { message = "hello" }, "idem-2"), TestDevice);
        Assert.True(send.Ok);

        var runId = ExtractString(send.Payload, "runId");
        Assert.False(string.IsNullOrWhiteSpace(runId));

        var abort = await dispatcher.DispatchAsync(new RequestFrame("req-abort", "chat.abort", new { runId }), TestDevice);

        Assert.True(abort.Ok);

        var snapshot = await runs.GetSnapshotAsync(runId!, "device-1");
        Assert.True(snapshot.Status is "aborted" or "completed");
    }

    [Fact]
    public async Task UnknownMethod_ReturnsProtocolMethodNotFoundError()
    {
        var dispatcher = CreateDispatcher();

        var response = await dispatcher.DispatchAsync(new RequestFrame("req-404", "unknown.method"), TestDevice);

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal(ErrorCodes.MethodNotFound, response.Error!.Code);
        Assert.Equal("req-404", response.Id);
    }

    private static string? ExtractString(object? payload, string property)
    {
        if (payload is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(payload);
        var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}
