namespace SharpClaw.Execution.Abstractions;

public readonly record struct SandboxHandle(string Provider, string SandboxId);

public sealed record ExecutionProviderPolicy(
    string DefaultProvider = "dind",
    string? FallbackProvider = "podman",
    IReadOnlySet<string>? EnabledProviders = null)
{
    public bool IsEnabled(string provider)
    {
        if (EnabledProviders is null)
        {
            return true;
        }

        return EnabledProviders.Contains(provider);
    }

    public IReadOnlyList<string> ResolveCandidates(string? requestedProvider, bool allowFallback)
    {
        var primary = string.IsNullOrWhiteSpace(requestedProvider)
            ? DefaultProvider
            : requestedProvider;

        var candidates = new List<string> { primary };

        if (allowFallback
            && !string.IsNullOrWhiteSpace(FallbackProvider)
            && string.Equals(primary, DefaultProvider, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(primary, FallbackProvider, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(FallbackProvider);
        }

        return candidates;
    }
}

public interface ISandboxProvider
{
    string Name { get; }

    Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default);
}
