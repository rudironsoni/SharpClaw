using System;
using System.Threading;
using System.Threading.Tasks;
using SharpClaw.Execution.Abstractions;

namespace SharpClaw.TestCommon.TestUtils;

/// <summary>
/// Minimal, test-only fake implementation of ISandboxProvider used to make unit
/// tests hermetic (no external Docker/Podman dependencies).
/// </summary>
public sealed class FakeSandboxProvider : ISandboxProvider
{
    private readonly string _name;

    public FakeSandboxProvider(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name => _name;

    public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
    {
        // Fail fast on cancellation to mimic real providers' behavior
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SandboxHandle(_name, $"{_name}-{Guid.NewGuid():N}"));
    }

    public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
