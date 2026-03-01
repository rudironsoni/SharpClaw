using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SharpClaw.Execution.Abstractions;

namespace SharpClaw.Execution.SandboxManager;

public sealed record SandboxStartRequest(
    string RunId,
    string Image = "alpine:latest",
    string? Provider = null,
    IReadOnlyList<string>? Mounts = null,
    bool AllowFallback = true);

public sealed class SandboxManagerService
{
    private readonly Dictionary<string, ISandboxProvider> _providers;
    private readonly ConcurrentDictionary<string, SandboxHandle> _active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, KeyedLock> _locks = new(StringComparer.Ordinal);
    private readonly ExecutionProviderPolicy _policy;
    private readonly ILogger<SandboxManagerService> _logger;

    private sealed class KeyedLock
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount = 1;
    }

    private async Task<IDisposable> AcquireLockAsync(string runId, CancellationToken cancellationToken)
    {
        KeyedLock keyedLock;
        lock (_locks)
        {
            if (!_locks.TryGetValue(runId, out keyedLock!))
            {
                keyedLock = new KeyedLock();
                _locks.Add(runId, keyedLock);
            }
            else
            {
                keyedLock.RefCount++;
            }
        }

        try
        {
            await keyedLock.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new LockReleaser(this, runId, keyedLock);
        }
        catch
        {
            lock (_locks)
            {
                keyedLock.RefCount--;
                if (keyedLock.RefCount == 0)
                {
                    _locks.Remove(runId);
                    keyedLock.Semaphore.Dispose();
                }
            }

            throw;
        }
    }

    private void ReleaseLock(string runId, KeyedLock keyedLock)
    {
        keyedLock.Semaphore.Release();

        lock (_locks)
        {
            keyedLock.RefCount--;
            if (keyedLock.RefCount == 0)
            {
                _locks.Remove(runId);
                keyedLock.Semaphore.Dispose();
            }
        }
    }

    private readonly struct LockReleaser : IDisposable
    {
        private readonly SandboxManagerService _service;
        private readonly string _runId;
        private readonly KeyedLock _keyedLock;

        public LockReleaser(SandboxManagerService service, string runId, KeyedLock keyedLock)
        {
            _service = service;
            _runId = runId;
            _keyedLock = keyedLock;
        }

        public void Dispose()
        {
            _service.ReleaseLock(_runId, _keyedLock);
        }
    }

    public SandboxManagerService(
        IEnumerable<ISandboxProvider> providers,
        ILogger<SandboxManagerService> logger,
        ExecutionProviderPolicy? policy = null)
    {
        _providers = providers.ToDictionary(static provider => provider.Name, StringComparer.OrdinalIgnoreCase);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _policy = policy ?? new ExecutionProviderPolicy();

        if (!_providers.ContainsKey(_policy.DefaultProvider))
        {
            throw new InvalidOperationException($"Default sandbox provider '{_policy.DefaultProvider}' is not registered.");
        }
    }

    public SandboxManagerService(
        IEnumerable<ISandboxProvider> providers,
        ILogger<SandboxManagerService> logger,
        string defaultProvider)
        : this(providers, logger, new ExecutionProviderPolicy(DefaultProvider: defaultProvider))
    {
    }

    public Task<SandboxHandle> StartDefaultAsync(string runId, IReadOnlyList<string>? mounts = null, CancellationToken cancellationToken = default)
    {
        return StartSandboxAsync(new SandboxStartRequest(RunId: runId, Mounts: mounts), cancellationToken);
    }

    public async Task<SandboxHandle> StartSandboxAsync(SandboxStartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMountPolicy(request.Mounts);

        using var sync = await AcquireLockAsync(request.RunId, cancellationToken).ConfigureAwait(false);

        if (_active.TryGetValue(request.RunId, out var existingHandle))
        {
            _logger.LogInformation("Sandbox for RunId {RunId} is already active.", request.RunId);
            return existingHandle;
        }

        var candidates = _policy.ResolveCandidates(request.Provider, request.AllowFallback);
        List<Exception>? failures = null;

        foreach (var candidate in candidates)
        {
            if (!_policy.IsEnabled(candidate))
            {
                continue;
            }

            if (!_providers.TryGetValue(candidate, out var provider))
            {
                continue;
            }

            try
            {
                var handle = await provider.StartAsync(cancellationToken).ConfigureAwait(false);
                _active.TryAdd(request.RunId, handle);
                _logger.LogInformation("Successfully started sandbox for RunId {RunId} using provider {Provider}.", request.RunId, candidate);
                return handle;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} failed to start sandbox for RunId {RunId}.", candidate, request.RunId);
                failures ??= [];
                failures.Add(ex);

                if (!request.AllowFallback)
                {
                    throw;
                }
            }
        }

        if (failures is { Count: > 0 })
        {
            throw new InvalidOperationException(
                $"Failed to start sandbox using candidates: {string.Join(", ", candidates)}",
                new AggregateException(failures));
        }

        throw new InvalidOperationException($"No available sandbox provider matched candidates: {string.Join(", ", candidates)}");
    }

    public async Task StopSandboxAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        using var sync = await AcquireLockAsync(runId, cancellationToken).ConfigureAwait(false);

        if (!_active.TryRemove(runId, out var handle))
        {
            _logger.LogInformation("No active sandbox found for RunId {RunId}.", runId);
            return;
        }

        if (_providers.TryGetValue(handle.Provider, out var provider))
        {
            _logger.LogInformation("Stopping sandbox for RunId {RunId} using provider {Provider}.", runId, handle.Provider);
            await provider.StopAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    public bool IsActive(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return _active.ContainsKey(runId);
    }

    private static void ValidateMountPolicy(IReadOnlyList<string>? mounts)
    {
        if (mounts is null)
        {
            return;
        }

        if (mounts.Any(static mount => mount.Contains("/var/run/docker.sock", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Mounting host /var/run/docker.sock is forbidden.");
        }
    }
}
