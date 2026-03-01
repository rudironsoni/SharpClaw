using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Abstractions;
using SharpClaw.Execution.SandboxManager;

namespace SharpClaw.Runs.UnitTests;

internal static class TestHelpers
{
    public static RunCoordinator CreateRunCoordinator()
    {
        var dummyProvider = new DummyProvider();
        var sandboxManager = new SandboxManagerService(new[] { dummyProvider }, NullLogger<SandboxManagerService>.Instance, new ExecutionProviderPolicy("dummy"));
        
        var pipeline = new AgentRuntimePipeline();
        var executionService = new RunExecutionService(pipeline, new DummyAdapter());
        
        return new RunCoordinator(sandboxManager, executionService);
    }

    private sealed class DummyProvider : ISandboxProvider
    {
        public string Name => "dummy";
        public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SandboxHandle("dummy", "dummy-id"));
        public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DummyAdapter : IAgentRuntimeAdapter
    {
        public async Task<RunResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10000, cancellationToken);
            return new RunResult(request.RunId, SharpClaw.Abstractions.OperationResult.Success());
        }
    }
}
