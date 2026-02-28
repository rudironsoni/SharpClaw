using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpClaw.Web;

namespace SharpClaw.Web.End2EndTests;

public class ControlUiEnd2EndTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task SendThenAbort_RunLifecycleWorksThroughControlUiEndpoints()
    {
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
        var client = _factory.CreateClient();

        var abort = new ControlUiAbortRequest(
            DeviceId: "device-e2e",
            RunId: "");

        using var response = await client.PostAsJsonAsync("/control-ui/chat/abort", abort);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
