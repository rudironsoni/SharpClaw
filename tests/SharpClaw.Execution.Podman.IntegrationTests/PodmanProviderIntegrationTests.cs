using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Podman;
using SharpClaw.Execution.SandboxManager;

namespace SharpClaw.Execution.Podman.IntegrationTests;

public class PodmanProviderIntegrationTests
{
    [Fact]
    public async Task StartDefaultAsync_UsesPodmanWhenConfiguredAsDefault()
    {
        var manager = new SandboxManagerService([new PodmanSandboxProvider()], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance, defaultProvider: "podman");

        var runId = Guid.NewGuid().ToString("N");
        var handle = await manager.StartDefaultAsync(runId);

        Assert.Equal("podman", handle.Provider);
        Assert.True(manager.IsActive(runId));
    }

    [Fact]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        var manager = new SandboxManagerService([new PodmanSandboxProvider()], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance, defaultProvider: "podman");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartDefaultAsync(Guid.NewGuid().ToString("N"), ["/var/run/docker.sock:/var/run/docker.sock"]));
    }
}
