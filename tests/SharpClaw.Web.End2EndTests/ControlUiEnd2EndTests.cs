using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Persistence.Core;
using SharpClaw.Web.IntegrationTests;

namespace SharpClaw.Web.End2EndTests;

[Trait("Category", "ExternalInfrastructure")]
public class ControlUiEnd2EndTests(SharpClawWebApplicationFactory factory)
    : IClassFixture<SharpClawWebApplicationFactory>
{
    private readonly SharpClawWebApplicationFactory _factory = factory;

    private async Task SeedTestDeviceAsync(string deviceId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SharpClawDbContext>();
        
        if (!dbContext.DeviceIdentities.Any(d => d.DeviceId == deviceId && d.TenantId == "default"))
        {
            dbContext.DeviceIdentities.Add(new SharpClaw.Persistence.Contracts.Entities.DeviceIdentityEntity
            {
                DeviceId = deviceId,
                TenantId = "default",
                IsPaired = true,
                PublicKey = $"test-key-{deviceId}",
                Scopes = "operator:write",
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SendThenAbort_RunLifecycleWorksThroughControlUiEndpoints()
    {
        await SeedTestDeviceAsync("device-e2e");
        var client = _factory.CreateClient();

        var send = new ControlUiSendRequest(
            DeviceId: "device-e2e",
            Message: "run this",
            IdempotencyKey: "e2e-idem-1");

        using var sendResponse = await client.PostAsJsonAsync("/control-ui/chat/send", send);
        sendResponse.EnsureSuccessStatusCode();

        var sendBody = await sendResponse.Content.ReadAsStringAsync();
        using var sendDoc = JsonDocument.Parse(sendBody);
        var runId = sendDoc.RootElement.GetProperty("runId").GetString();

        Assert.False(string.IsNullOrWhiteSpace(runId));

        var abort = new ControlUiAbortRequest(
            DeviceId: "device-e2e",
            RunId: runId!);

        using var abortResponse = await client.PostAsJsonAsync("/control-ui/chat/abort", abort);
        abortResponse.EnsureSuccessStatusCode();

        var abortBody = await abortResponse.Content.ReadAsStringAsync();
        using var abortDoc = JsonDocument.Parse(abortBody);
        var status = abortDoc.RootElement.GetProperty("status").GetString();

        Assert.True(status is "aborted" or "completed");
    }

    [Fact]
    public async Task AbortEndpoint_ReturnsBadRequestWhenRunIdMissing()
    {
        await SeedTestDeviceAsync("device-e2e");
        var client = _factory.CreateClient();

        var abort = new ControlUiAbortRequest(
            DeviceId: "device-e2e",
            RunId: "");

        using var response = await client.PostAsJsonAsync("/control-ui/chat/abort", abort);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
