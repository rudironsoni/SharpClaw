using System.Collections.Concurrent;
using SharpClaw.Execution.Abstractions;

namespace SharpClaw.Execution.SandboxManager;

public sealed record SandboxStartRequest(
    string? Provider = null,
    IReadOnlyList<string>? Mounts = null,
    bool AllowFallback = true);

public sealed class SandboxManagerService
{
    private readonly Dictionary<string, ISandboxProvider> _providers;
    private readonly ConcurrentDictionary<string, SandboxHandle> _active = new(StringComparer.Ordinal);
    private readonly ExecutionProviderPolicy _policy;

    public SandboxManagerService(IEnumerable<ISandboxProvider> providers, ExecutionProviderPolicy? policy = null)
    {
        _providers = providers.ToDictionary(static provider => provider.Name, StringComparer.OrdinalIgnoreCase);
        _policy = policy ?? new ExecutionProviderPolicy();

        if (!_providers.ContainsKey(_policy.DefaultProvider))
        {
            throw new InvalidOperationException($"Default sandbox provider '{_policy.DefaultProvider}' is not registered.");
        }
    }

    public SandboxManagerService(IEnumerable<ISandboxProvider> providers, string defaultProvider)
        : this(providers, new ExecutionProviderPolicy(DefaultProvider: defaultProvider))
    {
    }

    public Task<SandboxHandle> StartDefaultAsync(IReadOnlyList<string>? mounts = null, CancellationToken cancellationToken = default)
    {
        return StartAsync(new SandboxStartRequest(Mounts: mounts), cancellationToken);
    }

    public async Task<SandboxHandle> StartAsync(SandboxStartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMountPolicy(request.Mounts);

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
                _active[handle.SandboxId] = handle;
                return handle;
            }
            catch (Exception ex)
            {
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

    public async Task StopAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxId);

        if (!_active.TryRemove(sandboxId, out var handle))
        {
            return;
        }

        if (_providers.TryGetValue(handle.Provider, out var provider))
        {
            await provider.StopAsync(handle, cancellationToken).ConfigureAwait(false);
        }
    }

    public bool IsActive(string sandboxId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxId);
        return _active.ContainsKey(sandboxId);
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
