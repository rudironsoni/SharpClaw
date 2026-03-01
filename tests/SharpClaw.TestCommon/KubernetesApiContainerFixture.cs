using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace SharpClaw.TestCommon;

public sealed class KubernetesApiContainerFixture : IAsyncDisposable
{
    private const int WireMockPort = 8080;
    private readonly IContainer _container;
    private readonly HttpClient _httpClient = new();

    public KubernetesApiContainerFixture()
    {
        var image = Environment.GetEnvironmentVariable("SHARPCLAW_WIREMOCK_IMAGE")
            ?? "wiremock/wiremock:3.9.1";

        _container = new ContainerBuilder(image)
            .WithPortBinding(WireMockPort, true)
            .Build();
    }

    public string ServerUrl { get; private set; } = string.Empty;

    public string KubeConfigPath { get; private set; } = string.Empty;

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

        var kubeConfigYaml = $@"
apiVersion: v1
clusters:
- cluster:
    server: {ServerUrl}
    insecure-skip-tls-verify: true
  name: test-cluster
contexts:
- context:
    cluster: test-cluster
    user: test-user
  name: test-context
current-context: test-context
kind: Config
preferences: {{}}
users:
- name: test-user
  user:
    token: fake-token
";
        KubeConfigPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(KubeConfigPath, kubeConfigYaml);

        await ConfigureKubernetesMappingsAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();

        if (!string.IsNullOrWhiteSpace(KubeConfigPath) && File.Exists(KubeConfigPath))
        {
            File.Delete(KubeConfigPath);
        }

        await _container.DisposeAsync();
    }

    private async Task ConfigureKubernetesMappingsAsync()
    {
        var createPodMapping = new
        {
            request = new
            {
                method = "POST",
                urlPath = "/api/v1/namespaces/default/pods"
            },
            response = new
            {
                status = 201,
                headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
                jsonBody = new { metadata = new { name = "mock-pod-123" }, status = new { phase = "Pending" } }
            }
        };

        var getPodMapping = new
        {
            request = new
            {
                method = "GET",
                urlPathPattern = "/api/v1/namespaces/default/pods/.*"
            },
            response = new
            {
                status = 200,
                headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
                jsonBody = new { metadata = new { name = "mock-pod-123" }, status = new { phase = "Running" } }
            }
        };

        var deletePodMapping = new
        {
            request = new
            {
                method = "DELETE",
                urlPathPattern = "/api/v1/namespaces/default/pods/.*"
            },
            response = new
            {
                status = 200,
                headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
                jsonBody = new { metadata = new { name = "mock-pod-123" }, status = new { phase = "Running" } }
            }
        };

        await Task.WhenAll(
            CreateMappingAsync(createPodMapping),
            CreateMappingAsync(getPodMapping),
            CreateMappingAsync(deletePodMapping));
    }

    private async Task CreateMappingAsync(object mapping)
    {
        var response = await _httpClient.PostAsJsonAsync($"{ServerUrl}/__admin/mappings", mapping);
        response.EnsureSuccessStatusCode();
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
}
