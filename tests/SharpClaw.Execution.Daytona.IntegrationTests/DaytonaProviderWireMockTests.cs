using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Daytona;
using SharpClaw.Execution.SandboxManager;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SharpClaw.Execution.Daytona.IntegrationTests;

public class DaytonaProviderWireMockTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly SandboxManagerService _manager;

    public DaytonaProviderWireMockTests()
    {
        _server = WireMockServer.Start();

        var provider = new DaytonaSandboxProvider(
            NullLogger<DaytonaSandboxProvider>.Instance,
            serverUrl: _server.Urls[0],
            apiKey: "test-api-key");

        _manager = new SandboxManagerService([provider], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance, defaultProvider: "daytona");
    }

    [Fact]
    public async Task StartDefaultAsync_UsesDaytonaWithMockServer()
    {
        // Arrange
        _server.Given(
            Request.Create().WithPath("/api/workspaces").UsingPost()
        ).RespondWith(
            Response.Create().WithStatusCode(200).WithBodyAsJson(new { id = "mock-workspace-123", state = "pending" })
        );

        _server.Given(
            Request.Create().WithPath("/api/workspaces/mock-workspace-123").UsingGet()
        ).RespondWith(
            Response.Create().WithStatusCode(200).WithBodyAsJson(new { id = "mock-workspace-123", state = "running" })
        );

        // Act
        var handle = await _manager.StartDefaultAsync(Guid.NewGuid().ToString("N"));

        // Assert
        Assert.Equal("daytona", handle.Provider);
        Assert.Equal("mock-workspace-123", handle.SandboxId);
        Assert.True(_manager.IsActive(handle.SandboxId));
    }

    [Fact]
    public async Task StopAsync_CallsDaytonaApisToStopAndRemove()
    {
        // Arrange
        _server.Given(
            Request.Create().WithPath("/api/workspaces/mock-workspace-123").UsingGet()
        ).RespondWith(
            Response.Create().WithStatusCode(200).WithBodyAsJson(new { id = "mock-workspace-123", state = "running" })
        );
        
        _server.Given(
            Request.Create().WithPath("/api/workspaces/mock-workspace-123/stop").UsingPost()
        ).RespondWith(
            Response.Create().WithStatusCode(200)
        );

        _server.Given(
            Request.Create().WithPath("/api/workspaces/mock-workspace-123").UsingDelete()
        ).RespondWith(
            Response.Create().WithStatusCode(200)
        );

        // We can't directly call StopAsync via manager without having it in active sandboxes,
        // so we start it first (which adds it to active list).
        _server.Given(
            Request.Create().WithPath("/api/workspaces").UsingPost()
        ).RespondWith(
            Response.Create().WithStatusCode(200).WithBodyAsJson(new { id = "mock-workspace-123", state = "running" })
        );

        var handle = await _manager.StartDefaultAsync(Guid.NewGuid().ToString("N"));

        // Act
        await _manager.StopSandboxAsync(handle.SandboxId);

        // Assert
        Assert.False(_manager.IsActive(handle.SandboxId));
        
        var logs = _server.LogEntries;
        Assert.Contains(logs, l => l.RequestMessage.Path == "/api/workspaces/mock-workspace-123/stop" && l.RequestMessage.Method == "POST");
        Assert.Contains(logs, l => l.RequestMessage.Path == "/api/workspaces/mock-workspace-123" && l.RequestMessage.Method == "DELETE");
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }
}
