using System.Text.Json;
using SharpClaw.Gateway;
using SharpClaw.Protocol.Contracts;
using SharpClaw.Runs;

namespace SharpClaw.Gateway.IntegrationTests;

public class ConformanceSecurityLoadIntegrationTests
{
    [Fact]
    public async Task Conformance_ReplayCoreTranscript_ProducesExpectedOutcomes()
    {
        var dispatcher = new GatewayDispatcher();
        var runs = new RunCoordinator();
        GatewayMethodRegistration.RegisterCoreMethods(dispatcher, runs);

        var steps = new[]
        {
            new RequestFrame("1", "ping"),
            new RequestFrame("2", "chat.send", new { message = "hello" }, "idem-100"),
            new RequestFrame("3", "unknown.method")
        };

        var responses = new List<ResponseFrame>();
        foreach (var step in steps)
        {
            responses.Add(await dispatcher.DispatchAsync(step));
        }

        Assert.True(responses[0].Ok);
        Assert.True(responses[1].Ok);
        Assert.False(responses[2].Ok);
        Assert.Equal(ErrorCodes.InvalidRequest, responses[2].Error?.Code);
        Assert.Equal("3", responses[2].Id);
    }

    [Fact]
    public async Task Security_AbortWithoutRunId_ReturnsInvalidRequestError()
    {
        var dispatcher = new GatewayDispatcher();
        var runs = new RunCoordinator();
        GatewayMethodRegistration.RegisterCoreMethods(dispatcher, runs);

        var response = await dispatcher.DispatchAsync(
            new RequestFrame("abort-1", "chat.abort", new { bad = "payload" }));

        Assert.False(response.Ok);
        Assert.Equal(ErrorCodes.InvalidRequest, response.Error?.Code);
    }

    [Fact]
    public async Task Load_ConcurrentChatSend_StaysSuccessful()
    {
        var dispatcher = new GatewayDispatcher();
        var runs = new RunCoordinator();
        GatewayMethodRegistration.RegisterCoreMethods(dispatcher, runs);

        const int count = 200;
        var tasks = Enumerable.Range(1, count)
            .Select(index => dispatcher.DispatchAsync(
                new RequestFrame(
                    Id: $"load-{index}",
                    Method: "chat.send",
                    Payload: new { message = "load" },
                    IdempotencyKey: $"idem-load-{index}")))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        Assert.Equal(count, responses.Length);
        Assert.All(responses, static response => Assert.True(response.Ok));

        var runIds = responses
            .Select(static response => ExtractString(response.Payload, "runId"))
            .Where(static runId => !string.IsNullOrWhiteSpace(runId))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(count, runIds.Count);
    }

    private static string? ExtractString(object? payload, string property)
    {
        if (payload is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}
