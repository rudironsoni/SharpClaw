using SharpClaw.Execution.Abstractions;
using SharpClaw.Execution.SandboxManager;

namespace SharpClaw.Execution.IntegrationTests;

public class SandboxLifecycleIntegrationTests
{
    [Fact]
    public async Task StartAndStop_WorksAcrossMultipleRequests()
    {
        var provider = new LocalRecordingProvider();
        var manager = new SandboxManagerService([provider], defaultProvider: "fake");

        var first = await manager.StartAsync(new SandboxStartRequest("fake"));
        var second = await manager.StartAsync(new SandboxStartRequest("fake"));

        Assert.True(manager.IsActive(first.SandboxId));
        Assert.True(manager.IsActive(second.SandboxId));

        await manager.StopAsync(first.SandboxId);
        Assert.False(manager.IsActive(first.SandboxId));
        Assert.True(manager.IsActive(second.SandboxId));

        await manager.StopAsync(second.SandboxId);
        Assert.False(manager.IsActive(second.SandboxId));

        Assert.Equal(2, provider.Started);
        Assert.Equal(2, provider.Stopped);
    }

    private sealed class LocalRecordingProvider : ISandboxProvider
    {
        private int _seed;
        public string Name => "fake";
        public int Started { get; private set; }
        public int Stopped { get; private set; }

        public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Started++;
            var id = Interlocked.Increment(ref _seed);
            return Task.FromResult(new SandboxHandle(Name, $"fake-{id}"));
        }

        public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stopped++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DefaultProviderFailure_UsesFallbackProvider()
    {
        var failingDefault = new FailingProvider("dind");
        var fallback = new RecordingProvider("podman");
        var policy = new ExecutionProviderPolicy(DefaultProvider: "dind", FallbackProvider: "podman");
        var manager = new SandboxManagerService([failingDefault, fallback], policy);

        var handle = await manager.StartDefaultAsync();

        Assert.Equal("podman", handle.Provider);
        Assert.True(manager.IsActive(handle.SandboxId));
        Assert.Equal(1, fallback.Started);
    }

    private sealed class FailingProvider(string name) : ISandboxProvider
    {
        public string Name => name;

        public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("failed");
        }

        public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProvider(string name) : ISandboxProvider
    {
        private int _seed;
        public string Name => name;
        public int Started { get; private set; }
        public int Stopped { get; private set; }

        public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Started++;
            var id = Interlocked.Increment(ref _seed);
            return Task.FromResult(new SandboxHandle(Name, $"{name}-{id}"));
        }

        public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stopped++;
            return Task.CompletedTask;
        }
    }
}
