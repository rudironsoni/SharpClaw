using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpClaw.Web;

namespace SharpClaw.Web.IntegrationTests;

public class ControlUiIntegrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task StateEndpoint_ReturnsActiveConnections()
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync("/control-ui/state");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.True(doc.RootElement.TryGetProperty("activeConnections", out _));
    }

    [Fact]
    public async Task SendEndpoint_ReturnsOkAndRunMetadata()
    {
        var client = _factory.CreateClient();
        var request = new ControlUiSendRequest(
            DeviceId: "device-1",
            Message: "hello",
            IdempotencyKey: "ui-idem-1");

        using var response = await client.PostAsJsonAsync("/control-ui/chat/send", request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.StartsWith("run-", doc.RootElement.GetProperty("runId").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEndpoint_ReturnsForbiddenWhenWriteScopeMissing()
    {
        var client = _factory.CreateClient();
        var request = new ControlUiSendRequest(
            DeviceId: "device-2",
            Message: "hello",
            Scopes: ["operator.read"]);

        using var response = await client.PostAsJsonAsync("/control-ui/chat/send", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
