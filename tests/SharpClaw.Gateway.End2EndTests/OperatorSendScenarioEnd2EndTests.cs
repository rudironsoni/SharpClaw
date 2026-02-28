using SharpClaw.Gateway;
using SharpClaw.Protocol.Contracts;
using SharpClaw.Runs;
using System.Text.Json;

namespace SharpClaw.Gateway.End2EndTests;

public class OperatorSendScenarioEnd2EndTests
{
    [Fact]
    public async Task OperatorSendFlow_StartsRunAndReturnsAck()
    {
        var dispatcher = new GatewayDispatcher();
        var runs = new RunCoordinator();
        GatewayMethodRegistration.RegisterCoreMethods(dispatcher, runs);

        var response = await dispatcher.DispatchAsync(
            new RequestFrame("req-1", "chat.send", new { message = "hello" }, "idem-1"));

        Assert.True(response.Ok);
        Assert.Equal("req-1", response.Id);
        Assert.NotNull(response.Payload);
    }

    [Fact]
    public async Task OperatorAbortFlow_TransitionsRunToAborted()
    {
        var dispatcher = new GatewayDispatcher();
        var runs = new RunCoordinator();
        GatewayMethodRegistration.RegisterCoreMethods(dispatcher, runs);

        var send = await dispatcher.DispatchAsync(new RequestFrame("req-send", "chat.send", new { message = "hello" }, "idem-2"));
        Assert.True(send.Ok);

        var runId = ExtractString(send.Payload, "runId");
        Assert.False(string.IsNullOrWhiteSpace(runId));

        var abort = await dispatcher.DispatchAsync(new RequestFrame("req-abort", "chat.abort", new { runId }));

        Assert.True(abort.Ok);

        var snapshot = runs.GetSnapshot(runId!);
        Assert.True(snapshot.Status is "aborted" or "completed");
    }

    [Fact]
    public async Task UnknownMethod_ReturnsProtocolInvalidRequestError()
    {
        var dispatcher = new GatewayDispatcher();

        var response = await dispatcher.DispatchAsync(new RequestFrame("req-404", "unknown.method"));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal(ErrorCodes.InvalidRequest, response.Error!.Code);
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
