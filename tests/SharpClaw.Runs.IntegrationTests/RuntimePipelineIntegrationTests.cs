using SharpClaw.Abstractions;
using SharpClaw.Runs;

namespace SharpClaw.Runs.IntegrationTests;

public class RuntimePipelineIntegrationTests
{
    [Fact]
    public async Task RuntimePipeline_ProducesStableRunResult()
    {
        var pipeline = new AgentRuntimePipeline()
            .Use(async (request, ct, next) => await next(request, ct));

        var result = await pipeline.ExecuteAsync(
            new RunRequest("run-42", "hello"),
            static (request, _) => Task.FromResult(new RunResult(request.RunId, OperationResult.Success())));

        Assert.StartsWith("run-", result.RunId);
        Assert.True(result.Result.Succeeded);
    }

    [Fact]
    public async Task ExecutionService_ComposesPipelineWithAdapter()
    {
        var pipeline = new AgentRuntimePipeline()
            .Use(async (request, ct, next) =>
            {
                var result = await next(request, ct);
                return result with { Result = OperationResult.Success() };
            });

        var service = new RunExecutionService(pipeline, new IntegrationAdapter());

        var result = await service.ExecuteAsync(new RunRequest("run-77", "task"));

        Assert.Equal("run-77", result.RunId);
        Assert.True(result.Result.Succeeded);
    }

    [Fact]
    public async Task RunCoordinator_CompletesRunAndPublishesTerminalState()
    {
        var coordinator = new RunCoordinator();
        var started = await coordinator.StartAsync("integration", "idem-77");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var evt in coordinator.ReadEventsAsync(started.RunId, timeout.Token))
        {
            if (evt.Event == "run.completed")
            {
                break;
            }
        }

        var snapshot = coordinator.GetSnapshot(started.RunId);
        Assert.Equal("completed", snapshot.Status);
    }

    private sealed class IntegrationAdapter : IAgentRuntimeAdapter
    {
        public Task<RunResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RunResult(request.RunId, OperationResult.Success()));
        }
    }
}
