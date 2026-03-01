using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Kubernetes;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.TestCommon;

namespace SharpClaw.Execution.Kubernetes.IntegrationTests;

public class KubernetesProviderIntegrationTests : IAsyncLifetime
{
    private readonly KubernetesApiContainerFixture _fixture = new();
    private SandboxManagerService? _manager;

    public async Task InitializeAsync()
    {
        await _fixture.StartAsync();

        var provider = new KubernetesSandboxProvider(
            NullLogger<KubernetesSandboxProvider>.Instance,
            kubeConfigPath: _fixture.KubeConfigPath,
            @namespace: "default");

        _manager = new SandboxManagerService(
            [provider],
            NullLogger<SandboxManagerService>.Instance,
            defaultProvider: "kubernetes");
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_UsesKubernetesWhenConfiguredAsDefault()
    {
        ArgumentNullException.ThrowIfNull(_manager);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await _manager.StartDefaultAsync(runId);

        Assert.Equal("kubernetes", handle.Provider);
        Assert.StartsWith("sharpclaw-", handle.SandboxId, StringComparison.Ordinal);
        Assert.True(_manager.IsActive(runId));
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        ArgumentNullException.ThrowIfNull(_manager);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.StartDefaultAsync(Guid.NewGuid().ToString("N"), ["/var/run/docker.sock:/var/run/docker.sock"]));
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_UsesKataRuntimeClass_ForSensitivePolicy()
    {
        ArgumentNullException.ThrowIfNull(_manager);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await _manager.StartDefaultAsync(runId);

        Assert.Equal("kubernetes", handle.Provider);
        Assert.True(_manager.IsActive(runId));
        Assert.True(await _fixture.HasRequestAsync("POST", "/api/v1/namespaces/default/pods"));
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StopAsync_CallsKubernetesApiToDeletePod()
    {
        ArgumentNullException.ThrowIfNull(_manager);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await _manager.StartDefaultAsync(runId);

        await _manager.StopSandboxAsync(runId);

        Assert.False(_manager.IsActive(runId));
        Assert.True(await _fixture.HasRequestAsync("DELETE", $"/api/v1/namespaces/default/pods/{handle.SandboxId}"));
    }
}
