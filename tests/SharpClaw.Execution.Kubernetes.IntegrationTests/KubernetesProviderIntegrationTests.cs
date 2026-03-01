using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Kubernetes;
using SharpClaw.Execution.SandboxManager;

namespace SharpClaw.Execution.Kubernetes.IntegrationTests;

public class KubernetesProviderIntegrationTests
{
    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_UsesKubernetesWhenConfiguredAsDefault()
    {
        var manager = new SandboxManagerService([new KubernetesSandboxProvider(NullLogger<KubernetesSandboxProvider>.Instance)], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance, defaultProvider: "kubernetes");

        var runId = Guid.NewGuid().ToString("N");
        var handle = await manager.StartDefaultAsync(runId);

        Assert.Equal("kubernetes", handle.Provider);
        Assert.StartsWith("k8s-runc-", handle.SandboxId, StringComparison.Ordinal);
        Assert.True(manager.IsActive(runId));
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        var manager = new SandboxManagerService([new KubernetesSandboxProvider(NullLogger<KubernetesSandboxProvider>.Instance)], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance, defaultProvider: "kubernetes");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartDefaultAsync(Guid.NewGuid().ToString("N"), ["/var/run/docker.sock:/var/run/docker.sock"]));
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_UsesKataRuntimeClass_ForSensitivePolicy()
    {
        var provider = new KubernetesSandboxProvider(
            NullLogger<KubernetesSandboxProvider>.Instance,
            policy: new KubernetesRuntimeClassPolicy(EnableKataForSensitive: true));
        var manager = new SandboxManagerService([provider], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance, defaultProvider: "kubernetes");

        var handle = await manager.StartDefaultAsync(Guid.NewGuid().ToString("N"));

        Assert.StartsWith("k8s-kata-", handle.SandboxId, StringComparison.Ordinal);
    }
}
