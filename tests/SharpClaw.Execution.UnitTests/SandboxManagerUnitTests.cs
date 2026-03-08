using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Abstractions;
using SharpClaw.Execution.Docker;
using SharpClaw.Execution.Podman;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.TestCommon;

namespace SharpClaw.Execution.UnitTests;

public class SandboxManagerUnitTests
{
    [DockerAvailable]
    public async Task StartSandboxAsync_UsesSelectedProvider()
    {
        var manager = new SandboxManagerService(
            [new DockerSandboxProvider(NullLogger<DockerSandboxProvider>.Instance), new PodmanSandboxProvider()],
            NullLogger<SandboxManagerService>.Instance);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await manager.StartSandboxAsync(new SandboxStartRequest(runId, Provider: "dind"));

        Assert.Equal("dind", handle.Provider);
        Assert.True(manager.IsActive(runId));
    }

    [DockerAvailable]
    public async Task StopSandboxAsync_RemovesActiveHandle()
    {
        var manager = new SandboxManagerService(
            [new DockerSandboxProvider(NullLogger<DockerSandboxProvider>.Instance)],
            NullLogger<SandboxManagerService>.Instance);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await manager.StartSandboxAsync(new SandboxStartRequest(runId, Provider: "dind"));

        await manager.StopSandboxAsync(runId);

        Assert.False(manager.IsActive(runId));
    }

    [Fact]
    public async Task StartSandboxAsync_ThrowsForUnknownProvider()
    {
        var manager = new SandboxManagerService(
            [new DockerSandboxProvider(NullLogger<DockerSandboxProvider>.Instance)],
            NullLogger<SandboxManagerService>.Instance);

        var runId = Guid.NewGuid().ToString("N");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartSandboxAsync(new SandboxStartRequest(runId, Provider: "unknown")));
    }

    [Fact]
    public async Task StartSandboxAsync_RejectsHostDockerSocketMount()
    {
        var manager = new SandboxManagerService(
            [new DockerSandboxProvider(NullLogger<DockerSandboxProvider>.Instance)],
            NullLogger<SandboxManagerService>.Instance);

        var runId = Guid.NewGuid().ToString("N");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartSandboxAsync(new SandboxStartRequest(runId, Provider: "dind", Mounts: ["/var/run/docker.sock:/var/run/docker.sock"])));
    }

    [Fact]
    public async Task StartSandboxAsync_FallsBackToPodman_WhenDefaultProviderFails()
    {
        var failing = new ThrowingProvider("dind");
        var podman = new PodmanSandboxProvider();
        var policy = new ExecutionProviderPolicy(DefaultProvider: "dind", FallbackProvider: "podman");
        var manager = new SandboxManagerService(
            [failing, podman],
            NullLogger<SandboxManagerService>.Instance,
            policy);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await manager.StartDefaultAsync(runId);

        Assert.Equal("podman", handle.Provider);
        Assert.True(manager.IsActive(runId));
    }

    [Fact]
    public async Task StartSandboxAsync_DoesNotFallback_WhenAllowFallbackFalse()
    {
        var failing = new ThrowingProvider("dind");
        var podman = new PodmanSandboxProvider();
        var policy = new ExecutionProviderPolicy(DefaultProvider: "dind", FallbackProvider: "podman");
        var manager = new SandboxManagerService(
            [failing, podman],
            NullLogger<SandboxManagerService>.Instance,
            policy);

        var runId = Guid.NewGuid().ToString("N");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartSandboxAsync(new SandboxStartRequest(runId, Provider: null, Mounts: null, AllowFallback: false)));
    }

    [Fact]
    public async Task StartSandboxAsync_RespectsEnabledProvidersPolicy()
    {
        var policy = new ExecutionProviderPolicy(
            DefaultProvider: "dind",
            FallbackProvider: "podman",
            EnabledProviders: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "podman" });

        var manager = new SandboxManagerService(
            [new DockerSandboxProvider(NullLogger<DockerSandboxProvider>.Instance), new PodmanSandboxProvider()],
            NullLogger<SandboxManagerService>.Instance,
            policy);

        var runId = Guid.NewGuid().ToString("N");
        var handle = await manager.StartDefaultAsync(runId);

        Assert.Equal("podman", handle.Provider);
    }
    
    [Fact]
    public async Task StartSandboxAsync_IsIdempotent_ForSameRunId()
    {
        var provider = new RecordingProvider("dind");
        var manager = new SandboxManagerService(
            [provider],
            NullLogger<SandboxManagerService>.Instance);

        var runId = "test-run-123";
        
        var handle1 = await manager.StartSandboxAsync(new SandboxStartRequest(runId, Provider: "dind"));
        var handle2 = await manager.StartSandboxAsync(new SandboxStartRequest(runId, Provider: "dind"));

        Assert.Equal(handle1, handle2);
        Assert.Equal(1, provider.StartCount);
    }

    [Fact]
    public async Task StartAndStop_ConcurrentExecution_DoesNotLeakSandbox()
    {
        var startCompletionSource = new TaskCompletionSource();
        var stopRequestSource = new TaskCompletionSource();

        var provider = new DelayedStartProvider("dind", startCompletionSource, stopRequestSource);
        var manager = new SandboxManagerService(
            [provider],
            NullLogger<SandboxManagerService>.Instance);

        var runId = Guid.NewGuid().ToString("N");

        // Start in background
        var startTask = manager.StartSandboxAsync(new SandboxStartRequest(runId, Provider: "dind"));

        // Wait until provider.StartAsync is called and we are inside it
        await startCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // At this point, StartSandboxAsync has not yet added the handle to _active because it is awaiting StopRequestSource
        // Now call StopSandboxAsync concurrently. It should wait because the runId is locked.
        var stopTask = manager.StopSandboxAsync(runId);

        // Ensure stopTask hasn't completed yet, since it's waiting on the lock
        await Task.Delay(50);
        Assert.False(stopTask.IsCompleted);

        // Allow StartAsync to finish
        stopRequestSource.SetResult();

        // Now both should complete
        await Task.WhenAll(startTask, stopTask).WaitAsync(TimeSpan.FromSeconds(5));

        // The sandbox must be stopped and not active
        Assert.False(manager.IsActive(runId));
        Assert.Equal(1, provider.StopCount);
    }

    private sealed class DelayedStartProvider(string name, TaskCompletionSource startCalled, TaskCompletionSource allowStartToFinish) : ISandboxProvider
    {
        public string Name => name;
        public int StopCount { get; private set; }

        public async Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
        {
            startCalled.SetResult();
            await allowStartToFinish.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            return new SandboxHandle(name, Guid.NewGuid().ToString());
        }

        public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingProvider(string name) : ISandboxProvider
    {
        public string Name => name;

        public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException($"Provider {name} failed to start");
        }

        public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
    
    private sealed class RecordingProvider(string name) : ISandboxProvider
    {
        public string Name => name;
        public int StartCount { get; private set; }

        public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            return Task.FromResult(new SandboxHandle(name, Guid.NewGuid().ToString()));
        }

        public Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
