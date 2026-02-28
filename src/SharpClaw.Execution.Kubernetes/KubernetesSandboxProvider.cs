using SharpClaw.Execution.Abstractions;

namespace SharpClaw.Execution.Kubernetes;

public enum WorkloadSensitivity
{
    Standard,
    Sensitive
}

public sealed record KubernetesRuntimeClassPolicy(
    bool EnableKataForSensitive = true,
    string DefaultRuntimeClass = "runc",
    string KataRuntimeClass = "kata")
{
    public string ResolveRuntimeClass(WorkloadSensitivity sensitivity)
    {
        return sensitivity == WorkloadSensitivity.Sensitive && EnableKataForSensitive
            ? KataRuntimeClass
            : DefaultRuntimeClass;
    }
}

public sealed class KubernetesSandboxProvider : ISandboxProvider
{
    private readonly KubernetesRuntimeClassPolicy _policy;
    private readonly Func<WorkloadSensitivity> _sensitivityResolver;

    public KubernetesSandboxProvider(
        KubernetesRuntimeClassPolicy? policy = null,
        Func<WorkloadSensitivity>? sensitivityResolver = null)
    {
        _policy = policy ?? new KubernetesRuntimeClassPolicy();
        _sensitivityResolver = sensitivityResolver ?? (() => WorkloadSensitivity.Standard);
    }

    public string Name => "kubernetes";

    public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sensitivity = _sensitivityResolver();
        var runtimeClass = _policy.ResolveRuntimeClass(sensitivity);

        return Task.FromResult(new SandboxHandle(Name, $"k8s-{runtimeClass}-{Guid.NewGuid():N}"));
    }

    public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
