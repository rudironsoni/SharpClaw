using SharpClaw.Execution.Abstractions;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.TestCommon;
using Microsoft.Extensions.Logging.Abstractions;

namespace SharpClaw.IntegrationTests;

/// <summary>
/// Integration tests for provider orchestration and fallback logic.
/// </summary>
public class ProviderOrchestrationTests
{
    [Fact]
    public async Task Fallback_WhenPrimaryFails_UsesSecondaryProvider()
    {
        // Arrange
        var primaryProvider = new FakeSandboxProvider("docker", shouldFail: true);
        var fallbackProvider = new FakeSandboxProvider("kubernetes");
        
        var service = new SandboxManagerService(
            new[] { primaryProvider, fallbackProvider },
            NullLogger<SandboxManagerService>.Instance,
            new ExecutionProviderPolicy(DefaultProvider: "docker", FallbackProviders: new[] { "kubernetes" }));

        // Act
        var result = await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"));

        // Assert
        Assert.Equal("kubernetes", result.Provider);
        Assert.Equal(0, primaryProvider.ActiveCount);
        Assert.Equal(1, fallbackProvider.ActiveCount);
    }

    [Fact]
    public async Task Fallback_Chain_WhenFirstTwoFail_UsesThirdProvider()
    {
        // Arrange
        var provider1 = new FakeSandboxProvider("docker", shouldFail: true);
        var provider2 = new FakeSandboxProvider("kubernetes", shouldFail: true);
        var provider3 = new FakeSandboxProvider("podman");
        
        var service = new SandboxManagerService(
            new[] { provider1, provider2, provider3 },
            NullLogger<SandboxManagerService>.Instance,
            new ExecutionProviderPolicy(
                DefaultProvider: "docker",
                FallbackProviders: new[] { "kubernetes", "podman" }));

        // Act
        var result = await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"));

        // Assert
        Assert.Equal("podman", result.Provider);
        Assert.Equal(1, provider3.ActiveCount);
    }

    [Fact]
    public async Task ProviderSelection_ExplicitProviderRequested_UsesThatProvider()
    {
        // Arrange
        var dockerProvider = new FakeSandboxProvider("docker");
        var k8sProvider = new FakeSandboxProvider("kubernetes");
        
        var service = new SandboxManagerService(
            new[] { dockerProvider, k8sProvider },
            NullLogger<SandboxManagerService>.Instance,
            "docker");

        // Act - request kubernetes explicitly
        var result = await service.StartSandboxAsync(
            new SandboxStartRequest(RunId: "run-1", Provider: "kubernetes"));

        // Assert
        Assert.Equal("kubernetes", result.Provider);
    }

    [Fact]
    public async Task ProviderHealth_DisabledProvider_IsSkipped()
    {
        // Arrange
        var dockerProvider = new FakeSandboxProvider("docker");
        var k8sProvider = new FakeSandboxProvider("kubernetes");
        
        var policy = new ExecutionProviderPolicy(
            DefaultProvider: "docker",
            EnabledProviders: new[] { "kubernetes" }); // Only kubernetes is enabled
        
        var service = new SandboxManagerService(
            new[] { dockerProvider, k8sProvider },
            NullLogger<SandboxManagerService>.Instance,
            policy);

        // Act
        var result = await service.StartSandboxAsync(new SandboxStartRequest(RunId: "run-1"));

        // Assert
        Assert.Equal("kubernetes", result.Provider);
        Assert.Equal(0, dockerProvider.ActiveCount);
    }

    [Fact]
    public async Task ConcurrentRuns_MultipleRuns_UseDifferentSandboxes()
    {
        // Arrange
        var provider = new FakeSandboxProvider("docker");
        var service = new SandboxManagerService(
            new[] { provider },
            NullLogger<SandboxManagerService>.Instance);

        // Act
        var tasks = Enumerable.Range(0, 5)
            .Select(i => service.StartSandboxAsync(new SandboxStartRequest(RunId: $"run-{i}")))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, provider.ActiveCount);
        Assert.Equal(5, results.Select(r => r.Provider).Distinct().Count());
    }

    [Fact]
    public async Task SandboxLifecycle_StartThenStop_CleanupSuccessful()
    {
        // Arrange
        var provider = new FakeSandboxProvider("docker");
        var service = new SandboxManagerService(
            new[] { provider },
            NullLogger<SandboxManagerService>.Instance);

        // Act
        var runId = "run-lifecycle-test";
        var handle = await service.StartSandboxAsync(new SandboxStartRequest(RunId: runId));
        Assert.True(service.IsActive(runId));
        Assert.Equal(1, provider.ActiveCount);

        await service.StopSandboxAsync(runId);

        // Assert
        Assert.False(service.IsActive(runId));
        Assert.Equal(0, provider.ActiveCount);
    }

    [Fact]
    public async Task FallbackDisabled_WhenPrimaryFails_ThrowsException()
    {
        // Arrange
        var primaryProvider = new FakeSandboxProvider("docker", shouldFail: true);
        var fallbackProvider = new FakeSandboxProvider("kubernetes");
        
        var service = new SandboxManagerService(
            new[] { primaryProvider, fallbackProvider },
            NullLogger<SandboxManagerService>.Instance,
            new ExecutionProviderPolicy(
                DefaultProvider: "docker",
                AllowFallback: false));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.StartSandboxAsync(
                new SandboxStartRequest(RunId: "run-1", AllowFallback: false));
        });
    }
}
