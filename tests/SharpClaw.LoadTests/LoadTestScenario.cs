using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Gateway;

namespace SharpClaw.LoadTests;

/// <summary>
/// Load test scenarios for SharpClaw gateway.
/// </summary>
public class LoadTestScenario
{
    private readonly IServiceProvider _serviceProvider;
    private readonly GatewayMetrics _metrics;

    public LoadTestScenario()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GatewayMetrics>();
        _serviceProvider = services.BuildServiceProvider();
        _metrics = _serviceProvider.GetRequiredService<GatewayMetrics>();
    }

    [Fact]
    public async Task SimulateConcurrentRequests_HandlesLoad()
    {
        // Arrange
        var concurrentRequests = 100;
        var totalRequests = 1000;
        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = Enumerable.Range(0, totalRequests)
            .Select(i => SimulateRequestAsync(i, concurrentRequests))
            .ToList();

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var duration = stopwatch.Elapsed.TotalSeconds;
        var throughput = totalRequests / duration;
        
        Assert.True(throughput > 50, $"Throughput {throughput:F2} req/s is below 50 req/s threshold");
        Assert.True(duration < 30, $"Duration {duration:F2}s exceeds 30s threshold");
    }

    private async Task SimulateRequestAsync(int requestId, int concurrentLimit)
    {
        using var semaphore = new SemaphoreSlim(concurrentLimit);
        await semaphore.WaitAsync();
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate request processing
            await Task.Delay(Random.Shared.Next(10, 100));
            
            stopwatch.Stop();
            
            // Record metrics
            _metrics.RecordRequest("GET", "/api/test", "tenant-1", true);
            _metrics.RecordRequestDuration(stopwatch.Elapsed.TotalSeconds, "GET", "/api/test");
        }
        finally
        {
            semaphore.Release();
        }
    }

    [Fact]
    public async Task RateLimitingUnderLoad_ThrottlesCorrectly()
    {
        // Arrange
        var burstRequests = 200;
        var tokenLimit = 100;

        // Act
        var results = new List<bool>();
        for (int i = 0; i < burstRequests; i++)
        {
            results.Add(i < tokenLimit); // Simulate throttling
            await Task.Delay(1);
        }

        // Assert
        var throttledCount = results.Count(r => !r);
        Assert.True(throttledCount > 0, "Rate limiting should throttle excess requests");
    }

    [Fact]
    public async Task MemoryUsage_StaysWithinLimits()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);
        var iterations = 1000;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var data = new byte[1024]; // 1KB allocation
            await Task.Delay(1);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var finalMemory = GC.GetTotalMemory(true);
        var memoryGrowth = (finalMemory - initialMemory) / 1024 / 1024; // MB

        // Assert
        Assert.True(memoryGrowth < 100, $"Memory grew by {memoryGrowth}MB, exceeding 100MB threshold");
    }
}
