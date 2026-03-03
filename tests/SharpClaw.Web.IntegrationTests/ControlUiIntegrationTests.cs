using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Web;

namespace SharpClaw.Web.IntegrationTests;

[Trait("Category", "ExternalInfrastructure")]
public class ControlUiIntegrationTests(SharpClawWebApplicationFactory factory)
    : IClassFixture<SharpClawWebApplicationFactory>
{
    private readonly SharpClawWebApplicationFactory _factory = factory;

    private async Task SeedTestDeviceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SharpClaw.Persistence.Core.SharpClawDbContext>();
        
        // Ensure device exists with proper scopes
        var device = dbContext.DeviceIdentities.FirstOrDefault(d => d.DeviceId == "device-1" && d.TenantId == "default");
        if (device == null)
        {
            device = new SharpClaw.Persistence.Contracts.Entities.DeviceIdentityEntity
            {
                DeviceId = "device-1",
                TenantId = "default",
                IsPaired = true,
                PublicKey = "test-key-1",
                Scopes = "operator:write,operator:read",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.DeviceIdentities.Add(device);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"Seeded device with scopes: {device.Scopes}");
        }
        else
        {
            // Update existing device scopes
            device.Scopes = "operator:write,operator:read";
            device.IsPaired = true;
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"Updated device with scopes: {device.Scopes}");
        }
    }

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
        await SeedTestDeviceAsync();
        
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "default");
        
        var request = new ControlUiSendRequest(
            DeviceId: "device-1",
            Message: "hello",
            IdempotencyKey: "ui-idem-1");

        using var response = await client.PostAsJsonAsync("/control-ui/chat/send", request);
        
        // Debug: capture error details
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Request failed with {response.StatusCode}: {errorContent}");
        }

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
