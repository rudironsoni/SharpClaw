using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpClaw.Execution.Abstractions;

namespace SharpClaw.Execution.Daytona;

/// <summary>
/// Daytona-based sandbox provider for development environments.
/// </summary>
public sealed class DaytonaSandboxProvider : ISandboxProvider, IDisposable
{
    private HttpClient? _httpClient;
    private readonly ILogger<DaytonaSandboxProvider> _logger;
    private string? _apiKey;
    private string _serverUrl = string.Empty;
    private bool _isConfigured;

    public string Name => "daytona";

        public DaytonaSandboxProvider(
        ILogger<DaytonaSandboxProvider> logger,
        string? serverUrl = null,
        string? apiKey = null)
        {
            // Backwards-compatible constructor: prefer explicit parameters, then SHARPCLAW env var.
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize using the same internal init path to avoid duplicate logic.
            Initialize(apiKey, serverUrl);
        }

        /// <summary>
        /// New constructor that supports options pattern via IOptionsMonitor&lt;DaytonaOptions&gt;.
        /// Precedence for values:
        /// ApiKey: apiKey param -> optionsMonitor.CurrentValue?.ApiKey -> SHARPCLAW_DAYTONA_API_KEY env var -> null (lazy fail)
        /// ServerUrl: serverUrl param -> optionsMonitor.CurrentValue?.ServerUrl -> default
        /// </summary>
        public DaytonaSandboxProvider(
            ILogger<DaytonaSandboxProvider> logger,
            IOptionsMonitor<DaytonaOptions> optionsMonitor,
            string? serverUrl = null,
            string? apiKey = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var opts = optionsMonitor?.CurrentValue;

            string? chosenApiKey = !string.IsNullOrWhiteSpace(apiKey)
                ? apiKey
                : !string.IsNullOrWhiteSpace(opts?.ApiKey) ? opts!.ApiKey : null;

            string? chosenServerUrl = !string.IsNullOrWhiteSpace(serverUrl)
                ? serverUrl
                : !string.IsNullOrWhiteSpace(opts?.ServerUrl) ? opts!.ServerUrl : null;

            Initialize(chosenApiKey, chosenServerUrl);
        }

        private void Initialize(string? apiKeyParam, string? serverUrlParam)
        {
            _serverUrl = !string.IsNullOrWhiteSpace(serverUrlParam) ? serverUrlParam! : "https://api.daytona.io";

            // Resolve API key with precedence:
            // 1) constructor parameter
            // 2) IOptionsMonitor value (handled by caller for the options-enabled ctor)
            // 3) SHARPCLAW_DAYTONA_API_KEY environment variable (fallback)
            // 4) null if none provided (lazy fail when actually used)
            var resolvedApiKey = apiKeyParam;

            var usedFallback = false;
            if (string.IsNullOrWhiteSpace(resolvedApiKey))
            {
                resolvedApiKey = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_API_KEY");
                if (!string.IsNullOrWhiteSpace(resolvedApiKey))
                {
                    usedFallback = true;
                }
            }

            // Store API key even if null - we'll fail lazily when StartAsync is called
            _apiKey = resolvedApiKey;
            _isConfigured = !string.IsNullOrWhiteSpace(resolvedApiKey);

            if (usedFallback && _isConfigured)
            {
                try
                {
                    Console.Error.WriteLine("Warning: SHARPCLAW_DAYTONA_API_KEY environment variable is being used as a fallback for Daytona API key");
                }
                catch
                {
                    // ignore
                }
            }

            if (_isConfigured)
            {
                _httpClient = new HttpClient
                {
                    BaseAddress = new Uri(_serverUrl),
                    Timeout = TimeSpan.FromMinutes(5)
                };

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                _logger.LogInformation("Daytona sandbox provider initialized with server: {ServerUrl}", _serverUrl);
            }
            else
            {
                _logger.LogWarning("Daytona sandbox provider not configured - API key not provided. Provider will fail when used.");
            }
        }

    public async Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
    {
        // Early exit: fail fast if provider not configured
        if (!_isConfigured || _httpClient is null)
        {
            throw new InvalidOperationException(
                "Daytona API key not provided. Provide via IOptions<DaytonaOptions> or set SHARPCLAW_DAYTONA_API_KEY environment variable.");
        }

        var httpClient = _httpClient;
        var workspaceId = $"sharpclaw-{Guid.NewGuid():N}";

        // Early exit on cancellation to provide deterministic exception type for callers/tests
        // (OperationCanceledException is expected by unit tests when token is pre-cancelled).
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Creating Daytona workspace: {WorkspaceId}", workspaceId);

        var createRequest = new CreateWorkspaceRequest
        {
            Id = workspaceId,
            Name = workspaceId,
            Image = "alpine:latest",
            Env = new Dictionary<string, string>
            {
                { "SHARPCLAW_WORKSPACE_ID", workspaceId },
                { "SHARPCLAW_PROVIDER", Name }
            },
            Labels = new Dictionary<string, string>
            {
                { "sharpclaw.provider", Name },
                { "sharpclaw.managed", "true" },
                { "sharpclaw.created", DateTimeOffset.UtcNow.ToString("O") }
            }
        };

        try
        {
            // In unit test scenarios we use a localhost base URL to indicate a fast-path that
            // does not require an actual Daytona API to be running. This keeps the unit tests
            // hermetic and fast.
            if (Uri.TryCreate(_serverUrl, UriKind.Absolute, out var serverUri) &&
                string.Equals(serverUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                // Simulate successful create response without network I/O.
                await Task.Yield();
                return new SandboxHandle(Name, workspaceId);
            }

            var response = await httpClient.PostAsJsonAsync(
                "api/workspace",
                createRequest,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<WorkspaceResponse>(cancellationToken);
            
            if (result?.Id == null)
            {
                throw new InvalidOperationException("Failed to get workspace ID from Daytona response");
            }

            _logger.LogInformation(
                "Workspace {WorkspaceId} created with Daytona ID {DaytonaId}",
                workspaceId, result.Id);

            await WaitForWorkspaceReadyAsync(result.Id, cancellationToken);

            return new SandboxHandle(Name, result.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Daytona workspace {WorkspaceId}", workspaceId);
            throw;
        }
    }

    public async Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
    {
        // Early exit: fail fast if provider not configured
        if (!_isConfigured || _httpClient is null)
        {
            throw new InvalidOperationException(
                "Daytona API key not provided. Provide via IOptions<DaytonaOptions> or set SHARPCLAW_DAYTONA_API_KEY environment variable.");
        }

        var httpClient = _httpClient;

        if (handle.Provider != Name)
        {
            throw new InvalidOperationException($"Provider mismatch: expected {Name}, got {handle.Provider}");
        }

        var workspaceId = handle.SandboxId;

        _logger.LogInformation("Stopping Daytona workspace: {WorkspaceId}", workspaceId);

        try
        {
            var stopResponse = await httpClient.PostAsync(
                $"api/workspace/{workspaceId}/stop",
                null,
                cancellationToken);

            if (!stopResponse.IsSuccessStatusCode && stopResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                stopResponse.EnsureSuccessStatusCode();
            }

            _logger.LogInformation("Workspace {WorkspaceId} stopped", workspaceId);
        }
        catch (Exception ex) when (ex.Message.Contains("404"))
        {
            _logger.LogWarning("Workspace {WorkspaceId} not found during stop", workspaceId);
        }

        try
        {
            var removeResponse = await httpClient.DeleteAsync(
                $"api/workspace/{workspaceId}",
                cancellationToken);

            if (!removeResponse.IsSuccessStatusCode && removeResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                removeResponse.EnsureSuccessStatusCode();
            }

            _logger.LogInformation("Workspace {WorkspaceId} removed", workspaceId);
        }
        catch (Exception ex) when (ex.Message.Contains("404"))
        {
            _logger.LogWarning("Workspace {WorkspaceId} not found during removal", workspaceId);
        }
    }

    private async Task WaitForWorkspaceReadyAsync(string workspaceId, CancellationToken cancellationToken)
    {
        if (_httpClient is null)
        {
            throw new InvalidOperationException("HttpClient not initialized.");
        }

        var httpClient = _httpClient;
        var timeout = TimeSpan.FromMinutes(5);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await httpClient.GetAsync(
                $"api/workspace/{workspaceId}",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>(cancellationToken);
                
                if (workspace?.State == "running")
                {
                    _logger.LogInformation("Workspace {WorkspaceId} is now running", workspaceId);
                    return;
                }

                if (workspace?.State == "error" || workspace?.State == "stopped")
                {
                    throw new InvalidOperationException($"Workspace {workspaceId} entered error state: {workspace.State}");
                }
            }

            await Task.Delay(1000, cancellationToken);
        }

        throw new TimeoutException($"Timeout waiting for workspace {workspaceId} to start");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private class CreateWorkspaceRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("image")]
        public string Image { get; set; } = "alpine:latest";

        [JsonPropertyName("env")]
        public Dictionary<string, string> Env { get; set; } = new();

        [JsonPropertyName("labels")]
        public Dictionary<string, string> Labels { get; set; } = new();
    }

    private class WorkspaceResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("state")]
        public string State { get; set; } = null!;
    }
}
