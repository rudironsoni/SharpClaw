using System.Collections.Frozen;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SharpClaw.Gateway;
using SharpClaw.Gateway.Events;
using SharpClaw.Protocol.Contracts;

using SharpClaw.Abstractions.Identity;

namespace SharpClaw.Gateway.IntegrationTests;

public class GatewayInteractionIntegrationTests
{
    private static GatewayDispatcher CreateDispatcher()
    {
        var eventPublisher = new ChannelEventPublisher(
            EventPublisherOptions.Default,
            NullLogger<ChannelEventPublisher>.Instance);
        return new GatewayDispatcher(eventPublisher, NullLogger<GatewayDispatcher>.Instance);
    }

    [Fact]
    public async Task RegistryHealthAndDispatch_WorkTogether()
    {
        var registry = new ConnectionRegistry();
        var dispatcher = CreateDispatcher();
        var health = new GatewayHealthService(registry);

        dispatcher.Register(
            "chat.send",
            FrozenSet<string>.Empty,
            static (request, _, _) => Task.FromResult(new ResponseFrame(request.Id, Ok: true, Payload: new { status = "started" })));

        Assert.True(registry.TryConnect("operator-1"));

        var deviceContext = new DeviceContext("test-device", FrozenSet<string>.Empty, true);
        var response = await dispatcher.DispatchAsync(new RequestFrame("req-1", "chat.send"), deviceContext);
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
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IIdentityService, StubIdentityService>();

            // Remove real sandbox providers and add a stub
            var sandboxProviders = services.Where(d => d.ServiceType == typeof(SharpClaw.Execution.Abstractions.ISandboxProvider)).ToList();
            foreach (var descriptor in sandboxProviders)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<SharpClaw.Execution.Abstractions.ISandboxProvider, StubSandboxProvider>();
        });
    });

    private class StubSandboxProvider : SharpClaw.Execution.Abstractions.ISandboxProvider
    {
        public string Name => "dind";

        public Task<SharpClaw.Execution.Abstractions.SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SharpClaw.Execution.Abstractions.SandboxHandle("dind", "stub-1"));
        }

        public Task StopAsync(SharpClaw.Execution.Abstractions.SandboxHandle handle, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class StubIdentityService : IIdentityService
    {
        public Task<DeviceAuthResult> AuthorizeAsync(string deviceId, string tenantId, IReadOnlySet<string> requiredScopes, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<DeviceIdentity?> GetDeviceAsync(string deviceId, string tenantId, CancellationToken ct = default)
        {
            if (deviceId == "test-token")
            {
                return Task.FromResult<DeviceIdentity?>(new DeviceIdentity
                {
                    DeviceId = "test-device",
                    TenantId = tenantId,
                    IsPaired = true,
                    Scopes = new HashSet<string> { "operator:write" }
                });
            }

            return Task.FromResult<DeviceIdentity?>(null);
        }

        public Task<DeviceAuthResult> PairDeviceAsync(string deviceId, string tenantId, string publicKey, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<DeviceIdentity> UpsertDeviceAsync(string deviceId, string tenantId, DeviceIdentity identity, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

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
        using var socket = await client.ConnectAsync(new Uri("ws://localhost/ws?token=test-token"), CancellationToken.None);

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
