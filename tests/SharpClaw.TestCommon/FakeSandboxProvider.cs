using System.Collections.Concurrent;
using SharpClaw.Execution.Abstractions;

namespace SharpClaw.TestCommon;

/// <summary>
/// A fake sandbox provider for testing that simulates sandbox lifecycle.
/// </summary>
public class FakeSandboxProvider : ISandboxProvider
{
    private readonly ConcurrentDictionary<string, SandboxHandle> _sandboxes = new(StringComparer.Ordinal);
    private readonly bool _shouldFail;
    private readonly TimeSpan _startDelay;
    private int _sandboxCounter;

    public string Name { get; }

    public FakeSandboxProvider(string name, bool shouldFail = false, TimeSpan? startDelay = null)
    {
        Name = name;
        _shouldFail = shouldFail;
        _startDelay = startDelay ?? TimeSpan.Zero;
    }

    public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_shouldFail)
        {
            throw new InvalidOperationException($"Sandbox provider '{Name}' is configured to fail.");
        }

        if (_startDelay > TimeSpan.Zero)
        {
            Thread.Sleep(_startDelay);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var sandboxId = $"{Name}-{Interlocked.Increment(ref _sandboxCounter)}";
        var handle = new SandboxHandle(Name, sandboxId);
        _sandboxes[sandboxId] = handle;

        return Task.FromResult(handle);
    }

    public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
    {
        _sandboxes.TryRemove(handle.SandboxId, out _);
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<string> ActiveSandboxes => _sandboxes.Keys.ToList();

    public int ActiveCount => _sandboxes.Count;

    public void Clear() => _sandboxes.Clear();
}
