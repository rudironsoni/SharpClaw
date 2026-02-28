using SharpClaw.Execution.Kubernetes;

namespace SharpClaw.Execution.Kubernetes.UnitTests;

public class KubernetesSandboxProviderUnitTests
{
    [Fact]
    public void Name_IsKubernetes()
    {
        var provider = new KubernetesSandboxProvider();

        Assert.Equal("kubernetes", provider.Name);
    }

    [Fact]
    public async Task StartAsync_ReturnsK8sPrefixedHandle()
    {
        var provider = new KubernetesSandboxProvider();

        var handle = await provider.StartAsync();

        Assert.Equal("kubernetes", handle.Provider);
        Assert.StartsWith("k8s-runc-", handle.SandboxId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_RespectsCancellation()
    {
        var provider = new KubernetesSandboxProvider();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.StartAsync(cts.Token));
    }

    [Fact]
    public async Task StartAsync_UsesKataRuntimeClass_ForSensitiveWorkloads_WhenEnabled()
    {
        var provider = new KubernetesSandboxProvider(
            policy: new KubernetesRuntimeClassPolicy(EnableKataForSensitive: true),
            sensitivityResolver: () => WorkloadSensitivity.Sensitive);

        var handle = await provider.StartAsync();

        Assert.StartsWith("k8s-kata-", handle.SandboxId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_UsesDefaultRuntimeClass_ForSensitiveWorkloads_WhenKataDisabled()
    {
        var provider = new KubernetesSandboxProvider(
            policy: new KubernetesRuntimeClassPolicy(
                EnableKataForSensitive: false,
                DefaultRuntimeClass: "runc",
                KataRuntimeClass: "kata"),
            sensitivityResolver: () => WorkloadSensitivity.Sensitive);

        var handle = await provider.StartAsync();

        Assert.StartsWith("k8s-runc-", handle.SandboxId, StringComparison.Ordinal);
    }
}
