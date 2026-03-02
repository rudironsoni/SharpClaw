using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Daytona;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.TestCommon;
using Xunit;

namespace SharpClaw.Execution.Daytona.IntegrationTests;

public class DaytonaProviderIntegrationTests : IAsyncLifetime, IClassFixture<DaytonaOssContainerFixture>
{
    private readonly DaytonaOssContainerFixture _fixture;
    private SandboxManagerService? _manager;

    public DaytonaProviderIntegrationTests(DaytonaOssContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task EnsureManagerAsync()
    {
        if (_manager is not null)
        {
            return;
        }

        try
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
        catch
        {
            await _fixture.DisposeAsync();
            throw;
        }
    }

    public async Task InitializeAsync()
    {
        await EnsureManagerAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_UsesDaytonaWhenConfiguredAsDefault()
    {
        await EnsureManagerAsync();
        ArgumentNullException.ThrowIfNull(_manager);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await _manager.StartDefaultAsync(runId);

        Assert.Equal("daytona", handle.Provider);
        Assert.True(_manager.IsActive(runId));
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        await EnsureManagerAsync();
        ArgumentNullException.ThrowIfNull(_manager);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.StartDefaultAsync(Guid.NewGuid().ToString("N"), ["/var/run/docker.sock:/var/run/docker.sock"]));
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StopAsync_CallsDaytonaApisToStopAndRemove()
    {
        await EnsureManagerAsync();
        ArgumentNullException.ThrowIfNull(_manager);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await _manager.StartDefaultAsync(runId);

        await _manager.StopSandboxAsync(runId);

        Assert.False(_manager.IsActive(runId));
    }
}
