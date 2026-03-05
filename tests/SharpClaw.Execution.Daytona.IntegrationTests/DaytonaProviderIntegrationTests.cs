using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Daytona;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.TestCommon;
using Xunit;

namespace SharpClaw.Execution.Daytona.IntegrationTests;

[Trait("Category", "ExternalInfrastructure")]
public class DaytonaProviderIntegrationTests : IClassFixture<DaytonaOssContainerFixture>
{
    private readonly DaytonaOssContainerFixture _fixture;
    private SandboxManagerService? _manager;
    private static readonly TimeSpan ContainerStartTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SandboxOperationTimeout = TimeSpan.FromMinutes(5);

    public DaytonaProviderIntegrationTests(DaytonaOssContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task EnsureManagerAsync()
    {
        Console.WriteLine("[Daytona Test] EnsureManagerAsync called...");

        if (_manager is not null)
        {
            Console.WriteLine("[Daytona Test] Manager already initialized, returning immediately");
            return;
        }

        Console.WriteLine("[Daytona Test] Manager is null, will initialize...");
        var startTime = DateTime.UtcNow;

        try
        {
            Console.WriteLine("[Daytona Test] Calling _fixture.StartAsync() with timeout...");
            using var startCts = new CancellationTokenSource(ContainerStartTimeout);
            await _fixture.StartAsync().WaitAsync(startCts.Token);
            Console.WriteLine($"[Daytona Test] _fixture.StartAsync() completed in {DateTime.UtcNow - startTime}");

            Console.WriteLine("[Daytona Test] Creating DaytonaSandboxProvider...");
            Console.WriteLine($"[Daytona Test] ServerUrl: {_fixture.ServerUrl}");
            Console.WriteLine($"[Daytona Test] ApiKey configured: {!string.IsNullOrEmpty(_fixture.ApiKey)}");

            var provider = new DaytonaSandboxProvider(
                NullLogger<DaytonaSandboxProvider>.Instance,
                serverUrl: _fixture.ServerUrl,
                apiKey: _fixture.ApiKey);
            Console.WriteLine("[Daytona Test] DaytonaSandboxProvider created successfully");

            Console.WriteLine("[Daytona Test] Creating SandboxManagerService...");
            _manager = new SandboxManagerService(
                [provider],
                NullLogger<SandboxManagerService>.Instance,
                defaultProvider: "daytona");
            Console.WriteLine("[Daytona Test] SandboxManagerService created successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Daytona Test] ERROR in EnsureManagerAsync: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[Daytona Test] Stack trace: {ex.StackTrace}");
            await _fixture.DisposeAsync();
            throw;
        }

        Console.WriteLine($"[Daytona Test] EnsureManagerAsync completed in {DateTime.UtcNow - startTime}");
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_UsesDaytonaWhenConfiguredAsDefault()
    {
        Console.WriteLine("[Daytona Test] ============================================");
        Console.WriteLine("[Daytona Test] Starting test: StartDefaultAsync_UsesDaytonaWhenConfiguredAsDefault");
        Console.WriteLine("[Daytona Test] ============================================");
        var testStartTime = DateTime.UtcNow;

        await EnsureManagerAsync();
        ArgumentNullException.ThrowIfNull(_manager);

        var runId = Guid.NewGuid().ToString("N");
        Console.WriteLine($"[Daytona Test] Generated runId: {runId}");

        Console.WriteLine("[Daytona Test] Calling _manager.StartDefaultAsync() with timeout...");
        using var cts = new CancellationTokenSource(SandboxOperationTimeout);
        var handle = await _manager.StartDefaultAsync(runId, cancellationToken: cts.Token).WaitAsync(cts.Token);
        Console.WriteLine("[Daytona Test] _manager.StartDefaultAsync() completed successfully");

        Console.WriteLine($"[Daytona Test] Verifying handle.Provider = 'daytona', actual: '{handle.Provider}'");
        Assert.Equal("daytona", handle.Provider);

        Console.WriteLine("[Daytona Test] Checking if run is active in manager...");
        var isActive = _manager.IsActive(runId);
        Console.WriteLine($"[Daytona Test] IsActive returned: {isActive}");
        Assert.True(isActive);

        Console.WriteLine($"[Daytona Test] Test completed in {DateTime.UtcNow - testStartTime}");
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        Console.WriteLine("[Daytona Test] ============================================");
        Console.WriteLine("[Daytona Test] Starting test: StartDefaultAsync_RejectsDockerSocketMount");
        Console.WriteLine("[Daytona Test] ============================================");
        var testStartTime = DateTime.UtcNow;

        await EnsureManagerAsync();
        ArgumentNullException.ThrowIfNull(_manager);

        var runId = Guid.NewGuid().ToString("N");
        Console.WriteLine($"[Daytona Test] Generated runId: {runId}");
        Console.WriteLine("[Daytona Test] Expecting InvalidOperationException for docker socket mount...");

        using var cts = new CancellationTokenSource(SandboxOperationTimeout);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.StartDefaultAsync(runId, ["/var/run/docker.sock:/var/run/docker.sock"], cancellationToken: cts.Token).WaitAsync(cts.Token));

        Console.WriteLine($"[Daytona Test] Got expected exception: {exception.GetType().Name}: {exception.Message}");
        Console.WriteLine($"[Daytona Test] Test completed in {DateTime.UtcNow - testStartTime}");
    }

    [Fact]
    [Trait("Category", "ExternalInfrastructure")]
    public async Task StopAsync_CallsDaytonaApisToStopAndRemove()
    {
        Console.WriteLine("[Daytona Test] ============================================");
        Console.WriteLine("[Daytona Test] Starting test: StopAsync_CallsDaytonaApisToStopAndRemove");
        Console.WriteLine("[Daytona Test] ============================================");
        var testStartTime = DateTime.UtcNow;

        await EnsureManagerAsync();
        ArgumentNullException.ThrowIfNull(_manager);

        var runId = Guid.NewGuid().ToString("N");
        Console.WriteLine($"[Daytona Test] Generated runId: {runId}");

        Console.WriteLine("[Daytona Test] Creating sandbox with timeout...");
        using var createCts = new CancellationTokenSource(SandboxOperationTimeout);
        var handle = await _manager.StartDefaultAsync(runId, cancellationToken: createCts.Token).WaitAsync(createCts.Token);
        Console.WriteLine("[Daytona Test] Sandbox created successfully");

        Console.WriteLine("[Daytona Test] Stopping sandbox with timeout...");
        using var stopCts = new CancellationTokenSource(SandboxOperationTimeout);
        await _manager.StopSandboxAsync(runId).WaitAsync(stopCts.Token);
        Console.WriteLine("[Daytona Test] Sandbox stopped successfully");

        Console.WriteLine("[Daytona Test] Verifying run is no longer active...");
        var isActive = _manager.IsActive(runId);
        Console.WriteLine($"[Daytona Test] IsActive returned: {isActive}");
        Assert.False(isActive);

        Console.WriteLine($"[Daytona Test] Test completed in {DateTime.UtcNow - testStartTime}");
    }
}
