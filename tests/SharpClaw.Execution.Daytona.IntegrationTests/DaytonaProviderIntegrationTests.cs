using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Daytona;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.TestCommon;
using Xunit;

namespace SharpClaw.Execution.Daytona.IntegrationTests;

public class DaytonaProviderIntegrationTests : IAsyncLifetime
{
    private readonly DaytonaOssContainerFixture _fixture = new();
    private SandboxManagerService? _manager;

    public async Task InitializeAsync()
    {
        await _fixture.StartAsync();

        var provider = new DaytonaSandboxProvider(
            NullLogger<DaytonaSandboxProvider>.Instance,
            serverUrl: _fixture.ServerUrl,
            apiKey: _fixture.ApiKey);

        _manager = new SandboxManagerService(
            [provider],
            NullLogger<SandboxManagerService>.Instance,
            defaultProvider: "daytona");
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_UsesDaytonaWhenConfiguredAsDefault()
    {
        ArgumentNullException.ThrowIfNull(_manager);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await _manager.StartDefaultAsync(runId);

        Assert.Equal("daytona", handle.Provider);
        Assert.Equal("mock-workspace-123", handle.SandboxId);
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
    public async Task StopAsync_CallsDaytonaApisToStopAndRemove()
    {
        ArgumentNullException.ThrowIfNull(_manager);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await _manager.StartDefaultAsync(runId);

        await _manager.StopSandboxAsync(runId);

        Assert.False(_manager.IsActive(runId));
        Assert.True(await _fixture.HasRequestAsync("POST", $"/api/workspaces/{handle.SandboxId}/stop"));
        Assert.True(await _fixture.HasRequestAsync("DELETE", $"/api/workspaces/{handle.SandboxId}"));
    }
}
