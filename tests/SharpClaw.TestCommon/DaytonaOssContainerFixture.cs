using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace SharpClaw.TestCommon;

public sealed class DaytonaOssContainerFixture : IAsyncDisposable
{
    private const int WireMockPort = 8080;
    private readonly IContainer _container;
    private readonly HttpClient _httpClient = new();

    public DaytonaOssContainerFixture()
    {
        var image = Environment.GetEnvironmentVariable("SHARPCLAW_WIREMOCK_IMAGE")
            ?? "wiremock/wiremock:3.9.1";

        _container = new ContainerBuilder(image)
            .WithPortBinding(WireMockPort, true)
            .Build();
    }

    public string ServerUrl { get; private set; } = string.Empty;

    public string ApiKey { get; } = "test-api-key";

    public async Task<bool> HasRequestAsync(string method, string urlPathPrefix)
    {
        using var response = await _httpClient.GetAsync($"{ServerUrl}/__admin/requests");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        if (!document.RootElement.TryGetProperty("requests", out var requests))
        {
            return false;
        }

        foreach (var request in requests.EnumerateArray())
        {
            var requestInfo = request.GetProperty("request");
            var requestMethod = requestInfo.GetProperty("method").GetString();
            var urlPath = requestInfo.GetProperty("url").GetString();

            if (string.Equals(requestMethod, method, StringComparison.OrdinalIgnoreCase)
                && urlPath is not null
                && urlPath.StartsWith(urlPathPrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public async Task StartAsync()
    {
        await _container.StartAsync();

        var port = _container.GetMappedPublicPort(WireMockPort);
        ServerUrl = $"http://127.0.0.1:{port}";

        await WaitForAdminReadyAsync();
        await ConfigureDaytonaMappingsAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        await _container.DisposeAsync();
    }

    private async Task ConfigureDaytonaMappingsAsync()
    {
        var createWorkspaceMapping = new
        {
            request = new
            {
                method = "POST",
                urlPath = "/api/workspaces"
            },
            response = new
            {
                status = 200,
                jsonBody = new { id = "mock-workspace-123", state = "pending" }
            }
        };

        var getWorkspaceMapping = new
        {
            request = new
            {
                method = "GET",
                urlPathPattern = "/api/workspaces/.*"
            },
            response = new
            {
                status = 200,
                jsonBody = new { id = "mock-workspace-123", state = "running" }
            }
        };

        var stopWorkspaceMapping = new
        {
            request = new
            {
                method = "POST",
                urlPathPattern = "/api/workspaces/.*/stop"
            },
            response = new
            {
                status = 200
            }
        };

        var deleteWorkspaceMapping = new
        {
            request = new
            {
                method = "DELETE",
                urlPathPattern = "/api/workspaces/.*"
            },
            response = new
            {
                status = 200
            }
        };

        await Task.WhenAll(
            CreateMappingAsync(createWorkspaceMapping),
            CreateMappingAsync(getWorkspaceMapping),
            CreateMappingAsync(stopWorkspaceMapping),
            CreateMappingAsync(deleteWorkspaceMapping));
    }

    private async Task WaitForAdminReadyAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await _httpClient.GetAsync($"{ServerUrl}/__admin/mappings");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("WireMock admin endpoint did not become ready in time.");
    }

    private async Task CreateMappingAsync(object mapping)
    {
        var response = await _httpClient.PostAsJsonAsync($"{ServerUrl}/__admin/mappings", mapping);
        response.EnsureSuccessStatusCode();
    }
}
