using SharpClaw.Abstractions;
using SharpClaw.Runs;

namespace SharpClaw.Runs.UnitTests;

public class RunExecutionServiceUnitTests
{
    [Fact]
    public async Task ExecuteAsync_UsesPipelineAndAdapter()
    {
        var pipeline = new AgentRuntimePipeline()
            .Use(async (request, ct, next) =>
            {
                var result = await next(request, ct);
                return result with { Result = OperationResult.Success() };
            });

        var adapter = new StubAdapter();
        var service = new RunExecutionService(pipeline, adapter);

        var result = await service.ExecutePipelineAsync(new RunRequest("run-100", "hello"));

        Assert.Equal("run-100", result.RunId);
        Assert.True(result.Result.Succeeded);
        Assert.Equal(1, adapter.InvocationCount);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesAdapterFailure()
    {
        var pipeline = new AgentRuntimePipeline();
        var adapter = new StubAdapter(shouldFail: true);
        var service = new RunExecutionService(pipeline, adapter);

        var result = await service.ExecutePipelineAsync(new RunRequest("run-200", "hello"));

        Assert.False(result.Result.Succeeded);
        Assert.Equal("adapter-failure", result.Result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_IRunExecutionService_ReturnsSuccessResult()
    {
        var pipeline = new AgentRuntimePipeline();
        var adapter = new StubAdapter();
        IRunExecutionService service = new RunExecutionService(pipeline, adapter);

        var result = await service.ExecuteAsync(new RunRequest("run-300", "hello"));

        Assert.True(result.Result.Succeeded);
        Assert.Null(result.Result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_IRunExecutionService_ReturnsFailureResult()
    {
        var pipeline = new AgentRuntimePipeline();
        var adapter = new StubAdapter(shouldFail: true);
        IRunExecutionService service = new RunExecutionService(pipeline, adapter);

        var result = await service.ExecuteAsync(new RunRequest("run-400", "hello"));

        Assert.False(result.Result.Succeeded);
        Assert.Equal("adapter-failure", result.Result.Error);
    }

    private sealed class StubAdapter(bool shouldFail = false) : IAgentRuntimeAdapter
    {
        public int InvocationCount { get; private set; }

        public Task<RunResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            var result = shouldFail
                ? new RunResult(request.RunId, OperationResult.Failure("adapter-failure"))
                : new RunResult(request.RunId, OperationResult.Success());
            return Task.FromResult(result);
        }
    }
}
