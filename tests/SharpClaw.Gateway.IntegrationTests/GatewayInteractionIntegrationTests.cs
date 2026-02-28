using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.WebSockets;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SharpClaw.Gateway;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.Gateway.IntegrationTests;

public class GatewayInteractionIntegrationTests
{
    [Fact]
    public async Task RegistryHealthAndDispatch_WorkTogether()
    {
        var registry = new ConnectionRegistry();
        var dispatcher = new GatewayDispatcher();
        var health = new GatewayHealthService(registry);

        dispatcher.Register(
            "chat.send",
            static (request, _) => Task.FromResult(new ResponseFrame(request.Id, Ok: true, Payload: new { status = "started" })));

        Assert.True(registry.TryConnect("operator-1"));

        var response = await dispatcher.DispatchAsync(new RequestFrame("req-1", "chat.send"));
        var snapshot = health.Snapshot();

        Assert.True(response.Ok);
        Assert.Equal("req-1", response.Id);
        Assert.True(snapshot.IsHealthy);
        Assert.Equal(1, snapshot.ActiveConnections);
    }

    [Fact]
    public void ConnectThenDisconnect_UpdatesActiveConnectionCount()
    {
        var registry = new ConnectionRegistry();
        Assert.True(registry.TryConnect("conn-1"));
        Assert.Equal(1, registry.ActiveCount);

        Assert.True(registry.TryDisconnect("conn-1"));
        Assert.Equal(0, registry.ActiveCount);
    }
}

public class GatewayHttpIntegrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthySnapshot()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        var snapshot = await response.Content.ReadFromJsonAsync<GatewayHealthSnapshot>();

        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsHealthy);
    }

    [Fact]
    public async Task WebSocketEndpoint_DispatchesPingRequest()
    {
        var client = _factory.Server.CreateWebSocketClient();
        using var socket = await client.ConnectAsync(new Uri("ws://localhost/ws"), CancellationToken.None);

        var request = JsonSerializer.Serialize(new RequestFrame("req-1", "ping"));
        var requestBytes = Encoding.UTF8.GetBytes(request);
        await socket.SendAsync(requestBytes, WebSocketMessageType.Text, true, CancellationToken.None);

        var buffer = new byte[4096];
        var receive = await socket.ReceiveAsync(buffer, CancellationToken.None);
        var responseJson = Encoding.UTF8.GetString(buffer, 0, receive.Count);
        var response = JsonSerializer.Deserialize<ResponseFrame>(responseJson, JsonOptions);

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal("req-1", response.Id);
    }
}
