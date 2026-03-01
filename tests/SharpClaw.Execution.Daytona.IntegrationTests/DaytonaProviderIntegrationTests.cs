using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Daytona;
using SharpClaw.Execution.SandboxManager;

namespace SharpClaw.Execution.Daytona.IntegrationTests;

public class DaytonaProviderIntegrationTests
{
    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_UsesDaytonaWhenConfiguredAsDefault()
    {
        var manager = new SandboxManagerService(
            [new DaytonaSandboxProvider(NullLogger<DaytonaSandboxProvider>.Instance)], 
            NullLogger<SandboxManagerService>.Instance,
            defaultProvider: "daytona");

        var runId = Guid.NewGuid().ToString("N");
        var handle = await manager.StartDefaultAsync(runId);

        Assert.Equal("daytona", handle.Provider);
        Assert.True(manager.IsActive(runId));
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        var manager = new SandboxManagerService(
            [new DaytonaSandboxProvider(NullLogger<DaytonaSandboxProvider>.Instance)], 
            NullLogger<SandboxManagerService>.Instance,
            defaultProvider: "daytona");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartDefaultAsync(Guid.NewGuid().ToString("N"), ["/var/run/docker.sock:/var/run/docker.sock"]));
    }
}
