using SharpClaw.Execution.Abstractions;

namespace SharpClaw.Execution.Podman;

public sealed class PodmanSandboxProvider : ISandboxProvider
{
    public string Name => "podman";

    public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SandboxHandle(Name, $"podman-{Guid.NewGuid():N}"));
    }

    public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
