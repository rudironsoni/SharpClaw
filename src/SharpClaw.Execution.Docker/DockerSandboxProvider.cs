using SharpClaw.Execution.Abstractions;

namespace SharpClaw.Execution.Docker;

public sealed class DockerSandboxProvider : ISandboxProvider
{
    public string Name => "dind";

    public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SandboxHandle(Name, $"dind-{Guid.NewGuid():N}"));
    }

    public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
