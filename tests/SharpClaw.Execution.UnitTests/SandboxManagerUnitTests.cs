using SharpClaw.Execution.Abstractions;
using SharpClaw.Execution.Docker;
using SharpClaw.Execution.Podman;
using SharpClaw.Execution.SandboxManager;

namespace SharpClaw.Execution.UnitTests;

public class SandboxManagerUnitTests
{
    [Fact]
    public async Task StartAsync_UsesSelectedProvider()
    {
        var manager = new SandboxManagerService([
            new DockerSandboxProvider(),
            new PodmanSandboxProvider()
        ]);

        var handle = await manager.StartAsync(new SandboxStartRequest("dind"));

        Assert.Equal("dind", handle.Provider);
        Assert.True(manager.IsActive(handle.SandboxId));
    }

    [Fact]
    public async Task StopAsync_RemovesActiveHandle()
    {
        var manager = new SandboxManagerService([new DockerSandboxProvider()]);
        var handle = await manager.StartAsync(new SandboxStartRequest("dind"));

        await manager.StopAsync(handle.SandboxId);

        Assert.False(manager.IsActive(handle.SandboxId));
    }

    [Fact]
    public async Task StartAsync_ThrowsForUnknownProvider()
    {
        var manager = new SandboxManagerService([new DockerSandboxProvider()]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartAsync(new SandboxStartRequest("unknown")));
    }

    [Fact]
    public async Task StartAsync_RejectsHostDockerSocketMount()
    {
        var manager = new SandboxManagerService([new DockerSandboxProvider()]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartAsync(new SandboxStartRequest("dind", ["/var/run/docker.sock:/var/run/docker.sock"])));
    }

    [Fact]
    public async Task StartAsync_FallsBackToPodman_WhenDefaultProviderFails()
    {
        var failing = new ThrowingProvider("dind");
        var podman = new PodmanSandboxProvider();
        var policy = new ExecutionProviderPolicy(DefaultProvider: "dind", FallbackProvider: "podman");
        var manager = new SandboxManagerService([failing, podman], policy);

        var handle = await manager.StartDefaultAsync();

        Assert.Equal("podman", handle.Provider);
        Assert.True(manager.IsActive(handle.SandboxId));
    }

    [Fact]
    public async Task StartAsync_DoesNotFallback_WhenAllowFallbackFalse()
    {
        var failing = new ThrowingProvider("dind");
        var podman = new PodmanSandboxProvider();
        var policy = new ExecutionProviderPolicy(DefaultProvider: "dind", FallbackProvider: "podman");
        var manager = new SandboxManagerService([failing, podman], policy);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartAsync(new SandboxStartRequest(Provider: null, Mounts: null, AllowFallback: false)));
    }

    [Fact]
    public async Task StartAsync_RespectsEnabledProvidersPolicy()
    {
        var policy = new ExecutionProviderPolicy(
            DefaultProvider: "dind",
            FallbackProvider: "podman",
            EnabledProviders: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "podman" });

        var manager = new SandboxManagerService([new DockerSandboxProvider(), new PodmanSandboxProvider()], policy);

        var handle = await manager.StartDefaultAsync();

        Assert.Equal("podman", handle.Provider);
    }

    private sealed class ThrowingProvider(string name) : ISandboxProvider
    {
        public string Name => name;

        public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException($"Provider {name} failed to start");
        }

        public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
