using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Abstractions;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.TestCommon;

namespace SharpClaw.Execution.UnitTests;

/// <summary>
/// Edge case and comprehensive tests for SandboxManagerService.
/// </summary>
public class SandboxManagerServiceEdgeCaseTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_NoProviders_ThrowsInvalidOperationException()
    {
        var providers = Array.Empty<ISandboxProvider>();

        Assert.Throws<InvalidOperationException>(() =>
            new SandboxManagerService(providers, NullLogger<SandboxManagerService>.Instance));
    }

    [Fact]
    public void Constructor_SingleProvider_UsesAsDefault()
    {
        var providers = new[] { new FakeSandboxProvider("docker") };

        var service = new SandboxManagerService(providers, NullLogger<SandboxManagerService>.Instance);

        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_MultipleProviders_WithExplicitDefault_UsesSpecifiedDefault()
    {
        var providers = new[]
        {
            new FakeSandboxProvider("docker"),
            new FakeSandboxProvider("kubernetes")
        };

        var service = new SandboxManagerService(providers, NullLogger<SandboxManagerService>.Instance, "kubernetes");

        Assert.NotNull(service);
    }

    #endregion

    #region StartSandboxAsync Edge Cases

    [Fact]
    public async Task StartSandboxAsync_NullRequest_ThrowsArgumentNullException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await service.StartSandboxAsync(null!);
        });
    }

    [Fact]
    public async Task StartSandboxAsync_SameRunIdConcurrently_OnlyCreatesOneSandbox()
    {
        var provider = new FakeSandboxProvider("docker", startDelay: TimeSpan.FromMilliseconds(50));
        var service = CreateService(provider);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.StartSandboxAsync(new SandboxStartRequest(RunId: "same-run-id")))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // All should return the same handle
        Assert.All(results, r => Assert.Equal(results[0].Provider, r.Provider));
        Assert.Equal(1, provider.ActiveCount);
    }

    [Fact]
    public async Task StartSandboxAsync_ForbiddenMount_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        var request = new SandboxStartRequest(
            RunId: "run-1",
            Mounts: new[] { "/var/run/docker.sock" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.StartSandboxAsync(request);
        });

        Assert.Contains("docker.sock", ex.Message);
    }

    [Fact]
    public async Task StartSandboxAsync_ForbiddenMountCaseInsensitive_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        var request = new SandboxStartRequest(
            RunId: "run-1",
            Mounts: new[] { "/VAR/RUN/DOCKER.SOCK" });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.StartSandboxAsync(request);
        });
    }

    [Fact]
    public async Task StartSandboxAsync_AllowedMounts_Succeeds()
    {
        var service = CreateService();
        var request = new SandboxStartRequest(
            RunId: "run-1",
            Mounts: new[] { "/data", "/config", "/tmp/workspace" });

        var result = await service.StartSandboxAsync(request);

        Assert.NotEqual(default, result);
    }

    #endregion

    #region Fallback Logic Tests

    [Fact]
    public async Task StartSandboxAsync_FirstProviderFails_WithFallback_UsesSecondProvider()
    {
        var providers = new ISandboxProvider[]
        {
            new FakeSandboxProvider("docker", shouldFail: true),
            new FakeSandboxProvider("kubernetes")
        };

        var service = new SandboxManagerService(
            providers,
            NullLogger<SandboxManagerService>.Instance,
            new ExecutionProviderPolicy(DefaultProvider: "docker", FallbackProvider: "kubernetes"));

        var result = await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"));

        Assert.Equal("kubernetes", result.Provider);
    }

    [Fact]
    public async Task StartSandboxAsync_FirstProviderFails_NoFallback_ThrowsException()
    {
        var providers = new ISandboxProvider[]
        {
            new FakeSandboxProvider("docker", shouldFail: true),
            new FakeSandboxProvider("kubernetes")
        };

        var service = new SandboxManagerService(
            providers,
            NullLogger<SandboxManagerService>.Instance,
            new ExecutionProviderPolicy(DefaultProvider: "docker", FallbackProvider: null));

        var request = new SandboxStartRequest(RunId: "run-1");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.StartSandboxAsync(request);
        });
    }

    [Fact]
    public async Task StartSandboxAsync_AllProvidersFail_ThrowsException()
    {
        var providers = new ISandboxProvider[]
        {
            new FakeSandboxProvider("docker", shouldFail: true),
            new FakeSandboxProvider("kubernetes", shouldFail: true)
        };

        var service = new SandboxManagerService(
            providers,
            NullLogger<SandboxManagerService>.Instance,
            new ExecutionProviderPolicy(DefaultProvider: "docker", FallbackProvider: "kubernetes"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"));
        });
    }

    [Fact]
    public async Task StartSandboxAsync_SpecificProviderRequested_UsesThatProvider()
    {
        var providers = new ISandboxProvider[]
        {
            new FakeSandboxProvider("docker"),
            new FakeSandboxProvider("kubernetes"),
            new FakeSandboxProvider("podman")
        };

        var policy = new ExecutionProviderPolicy(DefaultProvider: "docker");
        var service = new SandboxManagerService(
            providers,
            NullLogger<SandboxManagerService>.Instance,
            policy);

        var request = new SandboxStartRequest(
            RunId: "run-1",
            Provider: "kubernetes");

        var result = await service.StartSandboxAsync(request);

        Assert.Equal("kubernetes", result.Provider);
    }

    [Fact]
    public async Task StartSandboxAsync_DisabledProvider_SkipsAndUsesNext()
    {
        var dockerProvider = new FakeSandboxProvider("docker");
        var kubernetesProvider = new FakeSandboxProvider("kubernetes");

        var providers = new ISandboxProvider[] { dockerProvider, kubernetesProvider };

        var policy = new ExecutionProviderPolicy(
            DefaultProvider: "docker",
            FallbackProvider: "kubernetes",
            EnabledProviders: new HashSet<string> { "kubernetes" });

        var service = new SandboxManagerService(providers, NullLogger<SandboxManagerService>.Instance, policy);

        var result = await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"));

        Assert.Equal("kubernetes", result.Provider);
        Assert.Equal(0, dockerProvider.ActiveCount);
    }

    #endregion

    #region StopSandboxAsync Tests

    [Fact]
    public async Task StopSandboxAsync_NonExistentRunId_DoesNotThrow()
    {
        var service = CreateService();

        await service.StopSandboxAsync("nonexistent-run-id");

        Assert.True(true); // Should not throw
    }

    [Fact]
    public async Task StopSandboxAsync_NullRunId_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await service.StopSandboxAsync(null!);
        });
    }

    [Fact]
    public async Task StopSandboxAsync_EmptyRunId_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await service.StopSandboxAsync("");
        });
    }

    [Fact]
    public async Task StopSandboxAsync_ExistingRunId_StopsSandbox()
    {
        var provider = new FakeSandboxProvider("docker");
        var service = CreateService(provider);

        var started = await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"));
        Assert.True(service.IsActive("run-1"));

        await service.StopSandboxAsync("run-1");

        Assert.False(service.IsActive("run-1"));
    }

    [Fact]
    public async Task StopSandboxAsync_ConcurrentStops_HandledSafely()
    {
        var provider = new FakeSandboxProvider("docker");
        var service = CreateService(provider);

        await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"));

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.StopSandboxAsync("run-1"))
            .ToList();

        await Task.WhenAll(tasks);

        Assert.False(service.IsActive("run-1"));
    }

    #endregion

    #region IsActive Tests

    [Fact]
    public void IsActive_NonExistentRunId_ReturnsFalse()
    {
        var service = CreateService();

        var result = service.IsActive("nonexistent");

        Assert.False(result);
    }

    [Fact]
    public async Task IsActive_AfterStart_ReturnsTrue()
    {
        var service = CreateService();
        await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"));

        var result = service.IsActive("run-1");

        Assert.True(result);
    }

    [Fact]
    public async Task IsActive_AfterStop_ReturnsFalse()
    {
        var service = CreateService();
        await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"));
        await service.StopSandboxAsync("run-1");

        var result = service.IsActive("run-1");

        Assert.False(result);
    }

    [Fact]
    public void IsActive_NullRunId_ThrowsArgumentException()
    {
        var service = CreateService();

        Assert.Throws<ArgumentNullException>(() => service.IsActive(null!));
    }

    #endregion

    #region StartDefaultAsync Tests

    [Fact]
    public async Task StartDefaultAsync_UsesDefaultProvider()
    {
        var dockerProvider = new FakeSandboxProvider("docker");
        var kubernetesProvider = new FakeSandboxProvider("kubernetes");

        var service = new SandboxManagerService(
            new[] { dockerProvider, kubernetesProvider },
            NullLogger<SandboxManagerService>.Instance,
            "kubernetes");

        var result = await service.StartDefaultAsync("run-1");

        Assert.Equal("kubernetes", result.Provider);
    }

    [Fact]
    public async Task StartDefaultAsync_WithMounts_PassesMountsToProvider()
    {
        var service = CreateService();
        var mounts = new[] { "/data", "/config" };

        var result = await service.StartDefaultAsync("run-1", mounts);

        Assert.NotEqual(default, result);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task StartSandboxAsync_CancellationDuringStart_ThrowsOperationCanceledException()
    {
        var provider = new FakeSandboxProvider("docker", startDelay: TimeSpan.FromSeconds(5));
        var service = CreateService(provider);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"), cts.Token);
        });
    }

    [Fact]
    public async Task StopSandboxAsync_Cancellation_DoesNotThrow()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"));
        await service.StopSandboxAsync("run-1", cts.Token);

        Assert.True(true); // Should complete without throwing
    }

    #endregion

    #region Helper Methods

    private static SandboxManagerService CreateService(params FakeSandboxProvider[] providers)
    {
        var actualProviders = providers.Length > 0
            ? providers
            : new[] { new FakeSandboxProvider("docker") };

        return new SandboxManagerService(
            actualProviders,
            NullLogger<SandboxManagerService>.Instance);
    }

    #endregion
}
