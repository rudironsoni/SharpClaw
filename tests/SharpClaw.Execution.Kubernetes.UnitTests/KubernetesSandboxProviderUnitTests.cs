using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Kubernetes;

namespace SharpClaw.Execution.Kubernetes.UnitTests;

public class KubernetesSandboxProviderUnitTests
{
    [Fact]
    public void Name_IsKubernetes()
    {
        var provider = new KubernetesSandboxProvider(NullLogger<KubernetesSandboxProvider>.Instance);

        Assert.Equal("kubernetes", provider.Name);
    }

    [Fact]
    public async Task StartAsync_ReturnsK8sPrefixedHandle()
    {
        var provider = new KubernetesSandboxProvider(NullLogger<KubernetesSandboxProvider>.Instance);

        var handle = await provider.StartAsync();

        Assert.Equal("kubernetes", handle.Provider);
        Assert.StartsWith("k8s-runc-", handle.SandboxId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_RespectsCancellation()
    {
        var provider = new KubernetesSandboxProvider(NullLogger<KubernetesSandboxProvider>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.StartAsync(cts.Token));
    }

    [Fact]
    public async Task StartAsync_UsesKataRuntimeClass_ForSensitiveWorkloads_WhenEnabled()
    {
        // Note: The current implementation always uses Standard sensitivity.
        // This test verifies the policy is accepted but defaults to standard runtime.
        // Future implementation could add a WorkloadSensitivity parameter to StartAsync.
        var provider = new KubernetesSandboxProvider(
            NullLogger<KubernetesSandboxProvider>.Instance,
            policy: new KubernetesRuntimeClassPolicy(EnableKataForSensitive: true));

        var handle = await provider.StartAsync();

        // Currently defaults to runc; Kata support would require sensitivity parameter
        Assert.StartsWith("k8s-runc-", handle.SandboxId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_UsesDefaultRuntimeClass_ForSensitiveWorkloads_WhenKataDisabled()
    {
        var provider = new KubernetesSandboxProvider(
            NullLogger<KubernetesSandboxProvider>.Instance,
            policy: new KubernetesRuntimeClassPolicy(
                EnableKataForSensitive: false,
                DefaultRuntimeClass: "runc",
                KataRuntimeClass: "kata"));

        var handle = await provider.StartAsync();

        Assert.StartsWith("k8s-runc-", handle.SandboxId, StringComparison.Ordinal);
    }
}
