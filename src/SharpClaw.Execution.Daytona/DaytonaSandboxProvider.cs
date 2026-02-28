using SharpClaw.Execution.Abstractions;

namespace SharpClaw.Execution.Daytona;

public sealed class DaytonaSandboxProvider : ISandboxProvider
{
    public string Name => "daytona";

    public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SandboxHandle(Name, $"daytona-{Guid.NewGuid():N}"));
    }

    public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
