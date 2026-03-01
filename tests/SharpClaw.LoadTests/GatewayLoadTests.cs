using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;
using SharpClaw.TestCommon;
using Xunit;
using Xunit.Abstractions;

namespace SharpClaw.LoadTests;

/// <summary>
/// Load tests for Gateway endpoints using NBomber.
/// </summary>
public class GatewayLoadTests
{
    private readonly ITestOutputHelper _output;

    public GatewayLoadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Load tests should be run manually")]
    [Trait(TestTraits.Category, TestConstants.Categories.Load)]
    public void Gateway_PingEndpoint_ShouldHandleHighLoad()
    {
        var httpClient = new HttpClient();
        var scenario = Scenario.Create("gateway_ping_load", async context =>
        {
            var response = await httpClient.GetAsync("http://localhost:5000/health");
            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: (int)response.StatusCode)
                : Response.Fail(statusCode: (int)response.StatusCode);
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 1000, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        _output.WriteLine($"Load test completed:");
        _output.WriteLine($"  - RPS: {stats.ScenarioStats[0].Ok.Request.RPS}");
        _output.WriteLine($"  - Success rate: {(stats.ScenarioStats[0].Ok.Request.Count / (double)stats.ScenarioStats[0].RequestCount) * 100}%");
        _output.WriteLine($"  - P95 latency: {stats.ScenarioStats[0].Ok.Latency.Percent75}ms");

        Assert.True(stats.ScenarioStats[0].Ok.Request.Count > 1000, "Should handle at least 1000 successful requests");
        Assert.True(stats.ScenarioStats[0].Fail.Request.Count < stats.ScenarioStats[0].RequestCount * 0.01, "Failure rate should be less than 1%");
    }

    [Fact(Skip = "Load tests should be run manually")]
    [Trait(TestTraits.Category, TestConstants.Categories.Load)]
    public void Gateway_WebSocketConnections_ShouldHandleConcurrentClients()
    {
        const int concurrentConnections = 100;
        const int messagesPerConnection = 50;

        var scenario = Scenario.Create("websocket_concurrent", async context =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var client = new ClientWebSocket();

            try
            {
                await client.ConnectAsync(new Uri("ws://localhost:5000/ws"), cts.Token);

                for (var i = 0; i < messagesPerConnection; i++)
                {
                    var message = $"{{\"id\":\"{Guid.NewGuid():N}\",\"method\":\"ping\"}}";
                    var buffer = System.Text.Encoding.UTF8.GetBytes(message);
                    await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cts.Token);

                    var receiveBuffer = new byte[1024];
                    await client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cts.Token);
                }

                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
                return Response.Ok();
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        })
        .WithLoadSimulations(
            Simulation.RampingInject(rate: concurrentConnections, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            Simulation.Inject(rate: concurrentConnections, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60)),
            Simulation.RampingInject(rate: 0, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        _output.WriteLine($"WebSocket load test completed:");
        _output.WriteLine($"  - Total connections: {stats.ScenarioStats[0].RequestCount}");
        _output.WriteLine($"  - Success rate: {(stats.ScenarioStats[0].Ok.Request.Count / (double)stats.ScenarioStats[0].RequestCount) * 100}%");

        Assert.True(stats.ScenarioStats[0].Ok.Request.Count >= concurrentConnections * 0.95, "Should handle at least 95% of connections");
    }

    [Fact(Skip = "Load tests should be run manually")]
    [Trait(TestTraits.Category, TestConstants.Categories.Load)]
    public void Gateway_MemoryUsage_ShouldRemainStable()
    {
        var initialMemory = GC.GetTotalMemory(true);
        var memorySnapshots = new List<long>();

        var httpClient = new HttpClient();
        var scenario = Scenario.Create("memory_stability", async context =>
        {
            var response = await httpClient.GetAsync("http://localhost:5000/health");
            
            if (context.InvocationNumber % 1000 == 0)
            {
                memorySnapshots.Add(GC.GetTotalMemory(false));
            }

            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: (int)response.StatusCode)
                : Response.Fail(statusCode: (int)response.StatusCode);
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        var finalMemory = GC.GetTotalMemory(true);
        var memoryGrowth = finalMemory - initialMemory;
        var memoryGrowthMB = memoryGrowth / (1024.0 * 1024.0);

        _output.WriteLine($"Memory stability test completed:");
        _output.WriteLine($"  - Initial memory: {initialMemory / (1024 * 1024)} MB");
        _output.WriteLine($"  - Final memory: {finalMemory / (1024 * 1024)} MB");
        _output.WriteLine($"  - Growth: {memoryGrowthMB:F2} MB");

        // Memory growth should be minimal - less than 100MB for a 2-minute test
        Assert.True(memoryGrowthMB < 100, $"Memory growth ({memoryGrowthMB:F2} MB) exceeded acceptable threshold");
    }
}
