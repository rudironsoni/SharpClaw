using SharpClaw.Execution.Docker;
using SharpClaw.Execution.SandboxManager;

namespace SharpClaw.Execution.Docker.IntegrationTests;

public class DockerProviderIntegrationTests
{
    [Fact]
    public async Task StartDefaultAsync_UsesDindProvider()
    {
        var manager = new SandboxManagerService([new DockerSandboxProvider()]);

        var handle = await manager.StartDefaultAsync();

        Assert.Equal("dind", handle.Provider);
        Assert.True(manager.IsActive(handle.SandboxId));
    }

    [Fact]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        var manager = new SandboxManagerService([new DockerSandboxProvider()]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartDefaultAsync(["/var/run/docker.sock:/var/run/docker.sock"]));
    }
}
