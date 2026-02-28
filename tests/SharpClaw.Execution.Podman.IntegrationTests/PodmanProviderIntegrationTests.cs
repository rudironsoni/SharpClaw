using SharpClaw.Execution.Podman;
using SharpClaw.Execution.SandboxManager;

namespace SharpClaw.Execution.Podman.IntegrationTests;

public class PodmanProviderIntegrationTests
{
    [Fact]
    public async Task StartDefaultAsync_UsesPodmanWhenConfiguredAsDefault()
    {
        var manager = new SandboxManagerService([new PodmanSandboxProvider()], defaultProvider: "podman");

        var handle = await manager.StartDefaultAsync();

        Assert.Equal("podman", handle.Provider);
        Assert.True(manager.IsActive(handle.SandboxId));
    }

    [Fact]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        var manager = new SandboxManagerService([new PodmanSandboxProvider()], defaultProvider: "podman");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartDefaultAsync(["/var/run/docker.sock:/var/run/docker.sock"]));
    }
}
