using SharpClaw.Abstractions;
using SharpClaw.Runs;

namespace SharpClaw.Runs.UnitTests;

public class RuntimePipelineUnitTests
{
    [Fact]
    public async Task Pipeline_ExecutesMiddlewaresInOrder()
    {
        var steps = new List<string>();
        var pipeline = new AgentRuntimePipeline()
            .Use(async (request, ct, next) =>
            {
                steps.Add("mw1-before");
                var result = await next(request, ct);
                steps.Add("mw1-after");
                return result;
            })
            .Use(async (request, ct, next) =>
            {
                steps.Add("mw2-before");
                var result = await next(request, ct);
                steps.Add("mw2-after");
                return result;
            });

        _ = await pipeline.ExecuteAsync(
            new RunRequest("run-1", "hi"),
            static (request, _) => Task.FromResult(new RunResult(request.RunId, OperationResult.Success())));

        Assert.Equal(["mw1-before", "mw2-before", "mw2-after", "mw1-after"], steps);
    }

    [Fact]
    public async Task Pipeline_WithoutMiddleware_InvokesTerminalDelegate()
    {
        var pipeline = new AgentRuntimePipeline();
        var called = false;

        var result = await pipeline.ExecuteAsync(
            new RunRequest("run-3", "payload"),
            static (request, _) =>
            {
                return Task.FromResult(new RunResult(request.RunId, OperationResult.Success()));
            });

        called = true;

        Assert.True(called);
        Assert.Equal("run-3", result.RunId);
        Assert.True(result.Result.Succeeded);
    }

    [Fact]
    public async Task Pipeline_PropagatesCancellation()
    {
        var pipeline = new AgentRuntimePipeline();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            pipeline.ExecuteAsync(
                new RunRequest("run-2", "input"),
                static (_, ct) => Task.FromCanceled<RunResult>(ct),
                cts.Token));
    }
}
