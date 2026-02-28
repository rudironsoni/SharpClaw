using SharpClaw.Execution.Kubernetes;
using SharpClaw.Execution.SandboxManager;

namespace SharpClaw.Execution.Kubernetes.IntegrationTests;

public class KubernetesProviderIntegrationTests
{
    [Fact]
    public async Task StartDefaultAsync_UsesKubernetesWhenConfiguredAsDefault()
    {
        var manager = new SandboxManagerService([new KubernetesSandboxProvider()], defaultProvider: "kubernetes");

        var handle = await manager.StartDefaultAsync();

        Assert.Equal("kubernetes", handle.Provider);
        Assert.StartsWith("k8s-runc-", handle.SandboxId, StringComparison.Ordinal);
        Assert.True(manager.IsActive(handle.SandboxId));
    }

    [Fact]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        var manager = new SandboxManagerService([new KubernetesSandboxProvider()], defaultProvider: "kubernetes");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartDefaultAsync(["/var/run/docker.sock:/var/run/docker.sock"]));
    }

    [Fact]
    public async Task StartDefaultAsync_UsesKataRuntimeClass_ForSensitivePolicy()
    {
        var provider = new KubernetesSandboxProvider(
            policy: new KubernetesRuntimeClassPolicy(EnableKataForSensitive: true),
            sensitivityResolver: () => WorkloadSensitivity.Sensitive);
        var manager = new SandboxManagerService([provider], defaultProvider: "kubernetes");

        var handle = await manager.StartDefaultAsync();

        Assert.StartsWith("k8s-kata-", handle.SandboxId, StringComparison.Ordinal);
    }
}
