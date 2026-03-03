using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpClaw.OpenResponses.HttpApi;

namespace SharpClaw.OpenResponses.End2EndTests;

[Trait("Category", "ExternalInfrastructure")]
public class OpenResponsesEnd2EndTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task StreamResponses_EmitsExpectedSseEvents()
    {
        var client = _factory.CreateClient();
        var request = new OpenResponsesRequest(
            Model: "gpt-4o-mini",
            Input: new { text = "stream" },
            Stream: true,
            User: "e2e");

        using var response = await client.PostAsJsonAsync("/v1/responses", request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("event: response.created", body, StringComparison.Ordinal);
        Assert.Contains("event: response.in_progress", body, StringComparison.Ordinal);
        Assert.Contains("event: response.output_text.delta", body, StringComparison.Ordinal);
        Assert.Contains("event: response.completed", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonStreamResponses_ReturnsStructuredResponsePayload()
    {
        var client = _factory.CreateClient();
        var request = new OpenResponsesRequest(
            Model: "gpt-4o-mini",
            Input: new { text = "non-stream" },
            Stream: false,
            User: "e2e");

        using var response = await client.PostAsJsonAsync("/v1/responses", request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.Equal("response", doc.RootElement.GetProperty("object").GetString());
        Assert.Equal("completed", doc.RootElement.GetProperty("status").GetString());
        Assert.StartsWith("run-", doc.RootElement.GetProperty("id").GetString(), StringComparison.Ordinal);
    }
}
