using SharpClaw.Execution.Daytona;
using SharpClaw.Execution.SandboxManager;

namespace SharpClaw.Execution.Daytona.IntegrationTests;

public class DaytonaProviderIntegrationTests
{
    [Fact]
    public async Task StartDefaultAsync_UsesDaytonaWhenConfiguredAsDefault()
    {
        var manager = new SandboxManagerService([new DaytonaSandboxProvider()], defaultProvider: "daytona");

        var handle = await manager.StartDefaultAsync();

        Assert.Equal("daytona", handle.Provider);
        Assert.True(manager.IsActive(handle.SandboxId));
    }

    [Fact]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        var manager = new SandboxManagerService([new DaytonaSandboxProvider()], defaultProvider: "daytona");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartDefaultAsync(["/var/run/docker.sock:/var/run/docker.sock"]));
    }
}
