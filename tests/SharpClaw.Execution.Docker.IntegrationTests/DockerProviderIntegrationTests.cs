using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Docker;
using SharpClaw.Execution.SandboxManager;

namespace SharpClaw.Execution.Docker.IntegrationTests;

public class DockerProviderIntegrationTests
{
    [Fact]
    public async Task StartDefaultAsync_UsesDindProvider()
    {
        var manager = new SandboxManagerService([new DockerSandboxProvider(NullLogger<DockerSandboxProvider>.Instance)], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await manager.StartDefaultAsync(runId);

        Assert.Equal("dind", handle.Provider);
        Assert.True(manager.IsActive(runId));
    }

    [Fact]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        var manager = new SandboxManagerService([new DockerSandboxProvider(NullLogger<DockerSandboxProvider>.Instance)], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartDefaultAsync(Guid.NewGuid().ToString("N"), ["/var/run/docker.sock:/var/run/docker.sock"]));
    }
}
