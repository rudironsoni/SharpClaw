using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpClaw.OpenResponses.HttpApi;

namespace SharpClaw.OpenResponses.IntegrationTests;

[Trait("Category", "ExternalInfrastructure")]
public class OpenResponsesIntegrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task PostResponses_ReturnsOkForValidPayload()
    {
        var client = _factory.CreateClient();
        var request = new OpenResponsesRequest(
            Model: "gpt-4o-mini",
            Input: new { text = "hello" },
            Stream: false,
            User: "integration");

        var response = await client.PostAsJsonAsync("/v1/responses", request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OpenResponsesResponse>();

        Assert.NotNull(payload);
        Assert.StartsWith("run-", payload!.Id, StringComparison.Ordinal);
        Assert.Equal("response", payload.Object);
        Assert.Equal("completed", payload.Status);
    }

    [Fact]
    public async Task PostResponses_ReturnsBadRequestForInvalidPayload()
    {
        var client = _factory.CreateClient();
        var request = new OpenResponsesRequest(
            Model: null,
            Input: null,
            Stream: false,
            User: "integration");

        var response = await client.PostAsJsonAsync("/v1/responses", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
