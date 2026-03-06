using Xunit;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using System.Diagnostics;

namespace SharpClaw.Execution.Daytona.IntegrationTests;

/// <summary>
/// Integration test fixture for Daytona with dual-mode support.
/// Mode 1 (Local): Uses Testcontainers with Docker
/// Mode 2 (CI): Uses Daytona Cloud API
/// </summary>
public class DaytonaIntegrationTestFixture : IAsyncLifetime
{
    private readonly bool _useCloudMode;
    private readonly string _apiKey;
    private readonly string _apiUrl;
    
    // Testcontainers mode
    private INetwork? _network;
    private IContainer? _daytonaApi;
    private IContainer? _daytonaRunner;
    private bool _disposed;
    
    // Cloud mode
    private HttpClient? _httpClient;

    public DaytonaIntegrationTestFixture()
    {
        _apiKey = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_API_KEY") ?? "";
        _apiUrl = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_API_URL") ?? "https://api.daytona.io";
        _useCloudMode = !string.IsNullOrEmpty(_apiKey);
        
        Console.WriteLine($"[Daytona] Fixture initialized - Mode: {(_useCloudMode ? "Cloud" : "Local/Testcontainers")}");
    }

    public async Task InitializeAsync()
    {
        if (_useCloudMode)
        {
            await InitializeCloudModeAsync();
        }
        else
        {
            await InitializeLocalModeAsync();
        }
    }

    private async Task InitializeCloudModeAsync()
    {
        Console.WriteLine("[Daytona] Using Cloud mode - no containers needed");
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        
        // Health check
        var response = await _httpClient.GetAsync($"{_apiUrl}/api/health");
        response.EnsureSuccessStatusCode();
        Console.WriteLine("[Daytona] Cloud API health check passed");
    }

    private async Task InitializeLocalModeAsync()
    {
        Console.WriteLine("[Daytona] Using Local/Testcontainers mode");
        
        // Create network
        _network = new NetworkBuilder()
            .Build();

        await _network.CreateAsync();
        await _network.CreateAsync();
        
        // Start Daytona API container
        _daytonaApi = new ContainerBuilder("daytonaio/daytona:latest")
            .WithNetwork(_network)
            .WithPortBinding(3000, true)
            .WithEnvironment("API_KEY", "test-key-123")
            .Build();
            
        await _daytonaApi.StartAsync();
        Console.WriteLine("[Daytona] API container started");
        
        // Start Daytona Runner container
        _daytonaRunner = new ContainerBuilder("daytonaio/daytona:latest")
            .WithNetwork(_network)
            .WithPortBinding(3001, true)
            .WithEnvironment("API_URL", "http://daytona-api:3000/api")
            .WithEnvironment("API_KEY", "test-key-123")
            .Build();
            
        await _daytonaRunner.StartAsync();
        Console.WriteLine("[Daytona] Runner container started");
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        
        if (_useCloudMode)
        {
            _httpClient?.Dispose();
        }
        else
        {
            if (_daytonaRunner != null)
            {
                await _daytonaRunner.StopAsync();
            }

            if (_daytonaApi != null)
            {
                await _daytonaApi.StopAsync();
            }

            if (_network != null)
            {
                await _network.DeleteAsync();
            }
        }
    }

    /// <summary>
    /// Gets the API base URL for making requests
    /// </summary>
    public string GetApiBaseUrl()
    {
        return _useCloudMode ? _apiUrl : $"http://{_daytonaApi!.GetMappedPublicPort(3000)}";
    }

    /// <summary>
    /// Gets HTTP client configured for API requests
    /// </summary>
    public HttpClient GetHttpClient()
    {
        if (_useCloudMode)
        {
            return _httpClient!;
        }

        throw new InvalidOperationException("HTTP client only available in cloud mode");
    }
}
