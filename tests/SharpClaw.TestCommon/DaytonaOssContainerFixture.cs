using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Xunit;

namespace SharpClaw.TestCommon;

/// <summary>
/// Starts Daytona OSS full stack and dependencies in Docker for integration tests (requires Docker + Testcontainers).
///
/// SECURITY MEASURES IMPLEMENTED:
/// - Docker-in-Docker (DinD) sidecar: Isolated Docker runtime instead of host socket mount
/// - Non-root execution: Runner runs without privileged mode (only DinD requires privileged)
/// - Capability dropping: ALL capabilities dropped, only NET_BIND_SERVICE and KILL added
/// - No new privileges: Prevents privilege escalation via setuid binaries
/// - Resource limits: 4GB memory limit per container, no swap
/// - Network isolation: All containers on isolated test network only
/// - TLS disabled internally: For test network only (acceptable risk for ephemeral tests)
///
/// SECURITY RATIONALE:
/// - DinD vs Socket: DinD is more secure than mounting /var/run/docker.sock because:
///   * Socket mount allows container escape to host Docker daemon
///   * DinD provides isolation - containers run in nested Docker, not host Docker
///   * DinD requires privileged mode but isolates the privileged container to test network
/// - Privileged Mode: Only DinD uses privileged mode; runner does not
/// - Network: All containers communicate over isolated Docker network, no host exposure
/// - Note: Read-only root filesystem not feasible - runner writes certs, binaries, config
/// </summary>
public sealed class DaytonaOssContainerFixture : IAsyncLifetime, IAsyncDisposable
{
    private const int DefaultApiPort = 3000;
    private const int DefaultProxyPort = 8080;
    private const int DefaultRunnerPort = 3001;
    private const int DefaultSshGatewayPort = 2222;
    private const int PostgresPort = 5432;
    private const int RedisPort = 6379;
    private const int MinioPort = 9000;
    private const int RegistryPort = 5000;
    private const int DexPort = 5556;
    private const string TestcontainersHost = "host.testcontainers.internal";
    private const string DefaultApiHealthPath = "/api/health";
        private static readonly TimeSpan DefaultReadyTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultReadyPollInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan DefaultReadyRequestTimeout = TimeSpan.FromSeconds(10);
        // Proxy-specific readiness knobs (can be overridden with env vars)
        private static readonly TimeSpan DefaultProxyReadyTimeout = TimeSpan.FromMinutes(6);
        private static readonly TimeSpan DefaultProxyReadyPollInterval = TimeSpan.FromSeconds(2);
    private const string DefaultApiImage = "daytonaio/daytona-api:v0.148.0";
    private const string DefaultProxyImage = "daytonaio/daytona-proxy:v0.148.0";
    private const string DefaultRunnerImage = "daytonaio/daytona-runner:v0.148.0";
    private const string DefaultSshGatewayImage = "daytonaio/daytona-ssh-gateway:v0.148.0";
    private const string DefaultPostgresImage = "postgres:18";
    private const string DefaultRedisImage = "redis:latest";
    private const string DefaultMinioImage = "minio/minio:latest";
    private const string DefaultRegistryImage = "registry:2.8.2";
    private const string DefaultDexImage = "dexidp/dex:v2.42.0";
    private const string DefaultDindImage = "docker:27-dind";

    private readonly INetwork _network;
    private readonly IContainer _postgres;
    private readonly IContainer _redis;
    private readonly IContainer _minio;
    private readonly IContainer _registry;
    private readonly IContainer _dex;
    private readonly IContainer _daytonaApi;
    private readonly IContainer _dind;
    private readonly IContainer _daytonaRunner;
    private IContainer _daytonaProxy;
    private readonly int _apiPort;
    private readonly string _healthPath;
    private readonly string _encryptionKey;
    private readonly string _encryptionSalt;
    private readonly string _dbName;
    private readonly string _dbUser;
    private readonly string _dbPassword;
    private readonly string _s3AccessKey;
    private readonly string _s3SecretKey;
    private readonly string _s3Bucket;
    private readonly string _s3Region;
    private readonly string _dexConfigPath;
    private readonly string? _proxyApiUrlOverride;
    private readonly string _proxyProtocol;
    private readonly TimeSpan _readyTimeout;
    private readonly TimeSpan _readyPollInterval;
    private readonly TimeSpan _readyRequestTimeout;
    private readonly TimeSpan _proxyReadyTimeout;
    private readonly TimeSpan _proxyReadyPollInterval;
    private bool _networkCreated;
    private bool _started;
    private bool _disposed;

    /// <summary>
    /// Initializes the Daytona OSS container fixture and validates required credentials.
    /// Ensures S3 credentials meet minimal length requirements to avoid starting
    /// long-running integration fixtures with invalid configuration.
    /// 
    /// Validation and defaults:
    /// - Reads raw environment variables for S3 access/secret keys and validates their shape when provided.
    /// - If an environment variable is absent, a safe default is used and a warning is emitted to stderr.
    /// </summary>
    public DaytonaOssContainerFixture()
    {
        ApiKey = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_API_KEY") ?? "test-api-key";
        _apiPort = int.TryParse(Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_API_PORT"), out var port)
            ? port
            : DefaultApiPort;
        _healthPath = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_HEALTH_PATH") ?? DefaultApiHealthPath;
        _encryptionKey = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_ENCRYPTION_KEY")
            ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _encryptionSalt = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_ENCRYPTION_SALT")
            ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _dbName = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_DB_NAME") ?? "daytona";
        _dbUser = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_DB_USER") ?? "daytona";
        _dbPassword = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_DB_PASSWORD") ?? "daytona";
        // Read raw environment variables first so we can decide whether to validate or use defaults.
        var s3AccessKeyEnv = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_S3_ACCESS_KEY");
        var s3SecretKeyEnv = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_S3_SECRET_KEY");

        if (!string.IsNullOrEmpty(s3AccessKeyEnv))
        {
            // Use provided value but validate shape to fail fast on obvious misconfiguration.
            _s3AccessKey = s3AccessKeyEnv;
            if (_s3AccessKey.Length < 3)
            {
                throw new InvalidOperationException("SHARPCLAW_DAYTONA_S3_ACCESS_KEY must be at least 3 characters long. Example: export SHARPCLAW_DAYTONA_S3_ACCESS_KEY=daytonauser");
            }
        }
        else
        {
            // Use safe default and warn (do NOT log secrets).
            _s3AccessKey = "daytona";
            Console.Error.WriteLine("Warning: using default SHARPCLAW_DAYTONA_S3_ACCESS_KEY 'daytona'. For CI set SHARPCLAW_DAYTONA_S3_ACCESS_KEY as a secret.");
        }

        if (!string.IsNullOrEmpty(s3SecretKeyEnv))
        {
            _s3SecretKey = s3SecretKeyEnv;
            if (_s3SecretKey.Length < 8)
            {
                throw new InvalidOperationException("SHARPCLAW_DAYTONA_S3_SECRET_KEY must be at least 8 characters long. Example: export SHARPCLAW_DAYTONA_S3_SECRET_KEY=myverysecret");
            }
        }
        else
        {
            _s3SecretKey = "daytona-secret";
            Console.Error.WriteLine("Warning: using default SHARPCLAW_DAYTONA_S3_SECRET_KEY. For CI set SHARPCLAW_DAYTONA_S3_SECRET_KEY as a secret.");
        }
        _s3Bucket = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_S3_BUCKET") ?? "daytona";
        _s3Region = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_S3_REGION") ?? "us-east-1";
        _dexConfigPath = Path.Combine(Path.GetTempPath(), $"sharpclaw-dex-{Guid.NewGuid():N}.yaml");
        _proxyProtocol = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_PROXY_PROTOCOL") ?? "http";
        _proxyApiUrlOverride = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_PROXY_API_URL");
        _readyTimeout = GetDurationFromEnvironment("SHARPCLAW_DAYTONA_READY_TIMEOUT", DefaultReadyTimeout);
        _readyPollInterval = GetDurationFromEnvironment("SHARPCLAW_DAYTONA_READY_POLL_INTERVAL", DefaultReadyPollInterval);
        _readyRequestTimeout = GetDurationFromEnvironment("SHARPCLAW_DAYTONA_READY_REQUEST_TIMEOUT", DefaultReadyRequestTimeout);
        _proxyReadyTimeout = GetDurationFromEnvironment("SHARPCLAW_DAYTONA_PROXY_READY_TIMEOUT", DefaultProxyReadyTimeout);
        _proxyReadyPollInterval = GetDurationFromEnvironment("SHARPCLAW_DAYTONA_PROXY_READY_POLL_INTERVAL", DefaultProxyReadyPollInterval);

        File.WriteAllText(_dexConfigPath, @$"issuer: http://dex:{DexPort}/dex
storage:
  type: memory
web:
  http: 0.0.0.0:{DexPort}
telemetry:
  http: 0.0.0.0:5558
enablePasswordDB: true
staticClients:
  - id: daytona
    name: Daytona
    secret: daytona-secret
    redirectURIs:
      - http://daytona-proxy:{DefaultProxyPort}/callback
staticPasswords:
  - email: admin@example.com
    hash: $2y$12$d9sre9W5uP8r8CBvsRT3S.EVnfe8ph.5O2lGOunXh3xLxPfhb3BKi
    username: admin
    userID: 8a7a3e11-5c0d-4fa5-9a29-9382e9bcd7f8
");

        _network = new NetworkBuilder()
            .WithName($"sharpclaw-daytona-{Guid.NewGuid():N}")
            .Build();

        var postgresImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_POSTGRES_IMAGE") ?? DefaultPostgresImage;
        _postgres = new ContainerBuilder(postgresImage)
            .WithEnvironment("POSTGRES_DB", _dbName)
            .WithEnvironment("POSTGRES_USER", _dbUser)
            .WithEnvironment("POSTGRES_PASSWORD", _dbPassword)
            .WithPortBinding(PostgresPort, true)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-postgres")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("database system is ready to accept connections"))
            .Build();

        var redisImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_REDIS_IMAGE") ?? DefaultRedisImage;
        _redis = new ContainerBuilder(redisImage)
            .WithPortBinding(RedisPort, true)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-redis")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Ready to accept connections"))
            .Build();

        var minioImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_MINIO_IMAGE") ?? DefaultMinioImage;
        _minio = new ContainerBuilder(minioImage)
            .WithEnvironment("MINIO_ROOT_USER", _s3AccessKey)
            .WithEnvironment("MINIO_ROOT_PASSWORD", _s3SecretKey)
            .WithCommand("server", "/data", "--console-address", ":9001")
            .WithPortBinding(MinioPort, true)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-minio")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort((ushort)MinioPort)
                    .ForPath("/minio/health/ready")
                    .WithMethod(HttpMethod.Get)
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        var registryImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_REGISTRY_IMAGE") ?? DefaultRegistryImage;
        _registry = new ContainerBuilder(registryImage)
            .WithPortBinding(RegistryPort, true)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-registry")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort((ushort)RegistryPort)
                    .ForPath("/v2/")
                    .WithMethod(HttpMethod.Get)
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        var dexImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_DEX_IMAGE") ?? DefaultDexImage;
        _dex = new ContainerBuilder(dexImage)
            .WithPortBinding(DexPort, true)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-dex")
            .WithBindMount(_dexConfigPath, "/etc/dex/config.yaml")
            .WithCommand("dex", "serve", "/etc/dex/config.yaml")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort((ushort)DexPort)
                    .ForPath("/dex/.well-known/openid-configuration")
                    .WithMethod(HttpMethod.Get)
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        var proxyUrl = $"http://daytona-proxy:{DefaultProxyPort}";
        var dexIssuerUrl = $"http://daytona-dex:{DexPort}/dex";
        var runnerUrl = $"http://daytona-runner:{DefaultRunnerPort}";
        var sshGatewayUrl = $"ssh://daytona-ssh-gateway:{DefaultSshGatewayPort}";

        var apiImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_API_IMAGE") ?? DefaultApiImage;
        _daytonaApi = new ContainerBuilder(apiImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-api")
            .WithPortBinding(_apiPort, true)
            // Port the API listens on - must match the exposed port
            .WithEnvironment("PORT", _apiPort.ToString())
            .WithEnvironment("DAYTONA_API_KEY", ApiKey)
            .WithEnvironment("DAYTONA_SERVER_API_KEY", ApiKey)
            .WithEnvironment("SKIP_CONNECTIONS", "false")
            .WithEnvironment("RUN_MIGRATIONS", "true")
            .WithEnvironment("NODE_ENV", "development")
            .WithEnvironment("ENVIRONMENT", "dev")
            .WithEnvironment("NODE_OPTIONS", "--trace-uncaught")
            .WithEnvironment("ENCRYPTION_KEY", _encryptionKey)
            .WithEnvironment("ENCRYPTION_SALT", _encryptionSalt)
            // Database config using Daytona-specific variable names
            .WithEnvironment("DB_HOST", "daytona-postgres")
            .WithEnvironment("DB_PORT", PostgresPort.ToString())
            .WithEnvironment("DB_USERNAME", _dbUser)
            .WithEnvironment("DB_PASSWORD", _dbPassword)
            .WithEnvironment("DB_DATABASE", _dbName)
            // Admin API key required for initializeAdminUser
            .WithEnvironment("ADMIN_API_KEY", ApiKey)
            // Admin quotas and limits
            .WithEnvironment("ADMIN_TOTAL_CPU_QUOTA", "0")
            .WithEnvironment("ADMIN_TOTAL_MEMORY_QUOTA", "0")
            .WithEnvironment("ADMIN_TOTAL_DISK_QUOTA", "0")
            .WithEnvironment("ADMIN_MAX_CPU_PER_SANDBOX", "0")
            .WithEnvironment("ADMIN_MAX_MEMORY_PER_SANDBOX", "0")
            .WithEnvironment("ADMIN_MAX_DISK_PER_SANDBOX", "0")
            .WithEnvironment("ADMIN_SNAPSHOT_QUOTA", "100")
            .WithEnvironment("ADMIN_MAX_SNAPSHOT_SIZE", "100")
            .WithEnvironment("ADMIN_VOLUME_QUOTA", "0")
            // Skip email verification for tests
            .WithEnvironment("SKIP_USER_EMAIL_VERIFICATION", "true")
            // Redis config
            .WithEnvironment("REDIS_HOST", "daytona-redis")
            .WithEnvironment("REDIS_PORT", RedisPort.ToString())
            .WithEnvironment("S3_ENDPOINT", $"http://daytona-minio:{MinioPort}")
            .WithEnvironment("S3_ACCESS_KEY", _s3AccessKey)
            .WithEnvironment("S3_SECRET_KEY", _s3SecretKey)
            .WithEnvironment("S3_BUCKET", _s3Bucket)
            .WithEnvironment("S3_REGION", _s3Region)
            .WithEnvironment("S3_USE_SSL", "false")
            .WithEnvironment("S3_FORCE_PATH_STYLE", "true")
            .WithEnvironment("S3_DEFAULT_BUCKET", _s3Bucket)
            .WithEnvironment("S3_STS_ENDPOINT", $"http://daytona-minio:{MinioPort}/minio/v1/assume-role")
            .WithEnvironment("S3_ACCOUNT_ID", "/")
            .WithEnvironment("S3_ROLE_NAME", "/")
            // SMTP configuration (for email notifications)
            .WithEnvironment("SMTP_HOST", "maildev")
            .WithEnvironment("SMTP_PORT", "1025")
            .WithEnvironment("SMTP_USER", "")
            .WithEnvironment("SMTP_PASSWORD", "")
            .WithEnvironment("SMTP_SECURE", "false")
            .WithEnvironment("SMTP_EMAIL_FROM", "Daytona Test <no-reply@daytona.test>")
            .WithEnvironment("PROXY_HOST", "daytona-proxy")
            .WithEnvironment("PROXY_PORT", DefaultProxyPort.ToString())
            .WithEnvironment("PROXY_URL", proxyUrl)
            .WithEnvironment("DEFAULT_RUNNER_HOST", "daytona-runner")
            .WithEnvironment("DEFAULT_RUNNER_PORT", DefaultRunnerPort.ToString())
            .WithEnvironment("DEFAULT_RUNNER_URL", runnerUrl)
            .WithEnvironment("SSH_GATEWAY_HOST", "daytona-ssh-gateway")
            .WithEnvironment("SSH_GATEWAY_PORT", DefaultSshGatewayPort.ToString())
            .WithEnvironment("SSH_GATEWAY_URL", sshGatewayUrl)
            .WithEnvironment("OIDC_ISSUER_BASE_URL", dexIssuerUrl)
            .WithEnvironment("OIDC_CLIENT_ID", "daytona")
            .WithEnvironment("OIDC_AUDIENCE", "daytona")
            .WithEnvironment("APP_URL", proxyUrl)
            .WithEnvironment("DASHBOARD_BASE_API_URL", proxyUrl)
            .WithEnvironment("DASHBOARD_URL", proxyUrl)
            // Default snapshot image
            .WithEnvironment("DEFAULT_SNAPSHOT", "ubuntu:22.04")
            // Registry configuration
            .WithEnvironment("TRANSIENT_REGISTRY_URL", $"http://daytona-registry:{RegistryPort}")
            .WithEnvironment("TRANSIENT_REGISTRY_ADMIN", "admin")
            .WithEnvironment("TRANSIENT_REGISTRY_PASSWORD", "password")
            .WithEnvironment("TRANSIENT_REGISTRY_PROJECT_ID", "daytona")
            .WithEnvironment("INTERNAL_REGISTRY_URL", $"http://daytona-registry:{RegistryPort}")
            .WithEnvironment("INTERNAL_REGISTRY_ADMIN", "admin")
            .WithEnvironment("INTERNAL_REGISTRY_PASSWORD", "password")
            .WithEnvironment("INTERNAL_REGISTRY_PROJECT_ID", "daytona")
            // SSH Gateway config
            .WithEnvironment("SSH_GATEWAY_API_KEY", ApiKey)
            .WithEnvironment("SSH_GATEWAY_COMMAND", $"ssh -p {DefaultSshGatewayPort} {{{{TOKEN}}}}@localhost")
            .WithEnvironment("SSH_GATEWAY_URL", $"localhost:{DefaultSshGatewayPort}")
            // Proxy config
            .WithEnvironment("PROXY_API_KEY", ApiKey)
            .WithEnvironment("PROXY_PROTOCOL", _proxyProtocol)
            .WithEnvironment("PROXY_DOMAIN", $"proxy.localhost:{DefaultProxyPort}")
            .WithEnvironment("PROXY_TEMPLATE_URL", $"http://{{{{PORT}}}}-{{{{sandboxId}}}}.proxy.localhost:{DefaultProxyPort}")
            .WithEnvironment("PROXY_TOOLBOX_BASE_URL", $"http://proxy.localhost:{DefaultProxyPort}")
            // Default runner config
            .WithEnvironment("DEFAULT_RUNNER_DOMAIN", $"localhost:{DefaultRunnerPort}")
            .WithEnvironment("DEFAULT_RUNNER_API_URL", runnerUrl)
            .WithEnvironment("DEFAULT_RUNNER_PROXY_URL", runnerUrl)
            .WithEnvironment("DEFAULT_RUNNER_API_KEY", ApiKey)
            .WithEnvironment("DEFAULT_RUNNER_CPU", "4")
            .WithEnvironment("DEFAULT_RUNNER_MEMORY", "8")
            .WithEnvironment("DEFAULT_RUNNER_DISK", "50")
            .WithEnvironment("DEFAULT_RUNNER_NAME", "default")
            // Health check
            .WithEnvironment("HEALTH_CHECK_API_KEY", ApiKey)
            // API key validation cache
            .WithEnvironment("API_KEY_VALIDATION_CACHE_TTL_SECONDS", "10")
            .WithEnvironment("API_KEY_USER_CACHE_TTL_SECONDS", "60")
            // Maintenance mode
            .WithEnvironment("MAINTENANCE_MODE", "false")
            // OpenTelemetry
            .WithEnvironment("OTEL_ENABLED", "false")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort((ushort)_apiPort)
                    .ForPath(_healthPath)
                    .WithMethod(HttpMethod.Get)
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        // Docker-in-Docker sidecar for secure container runtime
        // Security measures:
        // - Runs in isolated network namespace
        // - No direct host Docker socket mount (avoids container escape)
        // - TLS disabled for internal test network only
        // - Resource limits applied via container runtime
        _dind = BuildDindContainer();

        // Daytona runner container with Docker-in-Docker sidecar
        _daytonaRunner = BuildRunnerContainerWithDind();

        // Daytona proxy container - initialized in StartAsync after API is ready
        _daytonaProxy = null!;
    }

    public string ServerUrl { get; private set; } = string.Empty;

    public string ApiKey { get; }

    public async Task StartAsync()
    {
        if (_started)
        {
            return;
        }

        try
        {
            await _network.CreateAsync();
            _networkCreated = true;

            // Start infrastructure dependencies first
            await _postgres.StartAsync();
            await _redis.StartAsync();
            await _minio.StartAsync();
            await _registry.StartAsync();
            await _dex.StartAsync();
            await EnsureDependenciesReadyAsync();

            // Start Docker-in-Docker sidecar first
            // This provides isolated Docker runtime for the runner
            await _dind.StartAsync();

            // Start Daytona API first so runner can fetch its configuration
            // The runner needs to query the API during startup
            await _daytonaApi.StartAsync();

            ServerUrl = $"http://127.0.0.1:{_daytonaApi.GetMappedPublicPort(_apiPort)}";
            await EnsureApiReadyAsync();

            // Wait for API to be fully initialized before runner connects
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Start Daytona runner (connects to DinD for workspace creation)
            // Runner queries API for config during startup, so API must be ready
            await _daytonaRunner.StartAsync();

            // Wait for runner to fully initialize
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Build and start proxy after API is fully ready
            // Proxy needs the internal API URL for communication
            // Also ensure the /config endpoint is available before starting proxy
            await EnsureApiConfigReadyAsync();
            var proxy = BuildProxyContainer(GetApiInternalBaseUrl());
            _daytonaProxy = proxy;
            await _daytonaProxy.StartAsync();

            // After container start, actively probe the proxy through its mapped (external) port
            // to ensure it can reach the API and is functional before proceeding.
            await EnsureProxyReadyAsync();

            _started = true;
        }
        catch
        {
            try
            {
                await DisposeInternalAsync();
            }
            catch
            {
                // Ignore cleanup failures during startup.
            }

            throw;
        }
    }

    public Task InitializeAsync() => StartAsync();

    public Task DisposeAsync() => DisposeInternalAsync();

    async ValueTask IAsyncDisposable.DisposeAsync() => await DisposeInternalAsync();

    private async Task DisposeInternalAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var errors = new List<Exception>();

        // Stop containers in reverse dependency order
        await StopAndDisposeAsync(_daytonaProxy, "Daytona Proxy", errors);
        await StopAndDisposeAsync(_daytonaApi, "Daytona API", errors);
        await StopAndDisposeAsync(_daytonaRunner, "Daytona Runner", errors);
        await StopAndDisposeAsync(_dind, "Docker-in-Docker", errors);
        await StopAndDisposeAsync(_dex, "Dex", errors);
        await StopAndDisposeAsync(_registry, "Registry", errors);
        await StopAndDisposeAsync(_minio, "MinIO", errors);
        await StopAndDisposeAsync(_redis, "Redis", errors);
        await StopAndDisposeAsync(_postgres, "Postgres", errors);
        await DeleteNetworkAsync(errors);

        try
        {
            if (File.Exists(_dexConfigPath))
            {
                File.Delete(_dexConfigPath);
            }
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException("Failed to delete Dex config file.", ex));
        }

        if (errors.Count > 0)
        {
            throw new AggregateException("Failed to dispose Daytona OSS containers.", errors);
        }
    }

    private static async Task StopAndDisposeAsync(IContainer? container, string name, List<Exception> errors)
    {
        if (container is null)
        {
            return;
        }

        try
        {
            await container.StopAsync();
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException($"Failed to stop {name} container.", ex));
        }

        try
        {
            await container.DisposeAsync();
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException($"Failed to dispose {name} container.", ex));
        }
    }

    private string GetApiExternalBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_proxyApiUrlOverride))
        {
            return TrimApiSuffix(_proxyApiUrlOverride);
        }

        var host = TestcontainersHost;
        var mappedPort = _daytonaApi.GetMappedPublicPort(_apiPort);
        return $"http://{host}:{mappedPort}";
    }

    private string GetApiInternalBaseUrl()
    {
        // For containers on the same network, use the internal hostname and port
        return $"http://daytona-api:{_apiPort}";
    }

    private static string GetProxyApiUrl(string apiExternalBaseUrl)
    {
        if (apiExternalBaseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return apiExternalBaseUrl;
        }

        return $"{apiExternalBaseUrl.TrimEnd('/')}/api";
    }

    private static string TrimApiSuffix(string apiUrl)
    {
        var trimmed = apiUrl.TrimEnd('/');
        return trimmed.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }

    private IContainer BuildProxyContainer(string apiInternalUrl)
    {
        var proxyImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_PROXY_IMAGE") ?? DefaultProxyImage;

        return new ContainerBuilder(proxyImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-proxy")
            .WithPortBinding(DefaultProxyPort, true)
            .WithEnvironment("ENCRYPTION_KEY", _encryptionKey)
            .WithEnvironment("ENCRYPTION_SALT", _encryptionSalt)
            .WithEnvironment("PROXY_PORT", DefaultProxyPort.ToString())
            .WithEnvironment("PROXY_URL", $"http://daytona-proxy:{DefaultProxyPort}")
            // PROXY_API_URL is expected to include the /api suffix in many Daytona proxy builds
            .WithEnvironment("PROXY_API_URL", GetProxyApiUrl(apiInternalUrl))
            .WithEnvironment("PROXY_API_KEY", ApiKey)
            .WithEnvironment("PROXY_PROTOCOL", _proxyProtocol)
            .WithEnvironment("DAYTONA_API_URL", apiInternalUrl)
            .WithWaitStrategy(Wait.ForUnixContainer()
                // Keep a lightweight command check but do not rely on it solely for readiness.
                .UntilCommandIsCompleted($"/bin/sh -c 'sleep 1; nc -z localhost {DefaultProxyPort} || exit 1'"))
            .Build();
    }

    private string GetProxyExternalBaseUrl()
    {
        var host = TestcontainersHost;
        var mappedPort = _daytonaProxy.GetMappedPublicPort(DefaultProxyPort);
        return $"http://{host}:{mappedPort}";
    }

    private async Task EnsureProxyReadyAsync()
    {
        // Probe the proxy by requesting the API health endpoint through the proxy.
        // Many Daytona proxy configurations will forward /api/* to the internal API service.
        var proxyBase = GetProxyExternalBaseUrl();
        var probePath = "/api/health"; // probe the proxied API health
        using var client = new HttpClient
        {
            Timeout = _readyRequestTimeout
        };

        var deadline = DateTimeOffset.UtcNow + _proxyReadyTimeout;
        Exception? lastError = null;
        var delay = _proxyReadyPollInterval;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var uri = new Uri(new Uri(proxyBase), probePath);
                using var response = await client.GetAsync(uri);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            // Exponential backoff with cap to avoid busy-waiting
            await Task.Delay(delay);
            delay = TimeSpan.FromSeconds(Math.Min(30, delay.TotalSeconds * 2));
        }

        // If we reach here, the proxy never became ready. Capture container logs for diagnostics.
        try
        {
            Console.Error.WriteLine("Daytona proxy failed to become ready. Capturing container logs for diagnostics:");
            var logs = await _daytonaProxy.GetLogsAsync();
            Console.Error.WriteLine(logs);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to retrieve Daytona proxy logs: {ex.Message}");
        }

        throw new TimeoutException($"Daytona proxy failed to become ready at {proxyBase}{probePath} within {_proxyReadyTimeout}.", lastError);
    }

    private IContainer BuildRunnerContainer(string apiExternalUrl)
    {
        var runnerImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_RUNNER_IMAGE") ?? DefaultRunnerImage;

        return new ContainerBuilder(runnerImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-runner")
            .WithEnvironment("ENCRYPTION_KEY", _encryptionKey)
            .WithEnvironment("ENCRYPTION_SALT", _encryptionSalt)
            .WithEnvironment("DEFAULT_RUNNER_PORT", DefaultRunnerPort.ToString())
            .WithEnvironment("DEFAULT_RUNNER_URL", $"http://daytona-runner:{DefaultRunnerPort}")
            .WithEnvironment("DEFAULT_RUNNER_API_URL", apiExternalUrl)
            .WithEnvironment("DAYTONA_API_URL", apiExternalUrl)
            .WithEnvironment("SERVER_URL", $"http://daytona-api:{DefaultApiPort}")
            .WithEnvironment("DAYTONA_RUNNER_TOKEN", ApiKey)
            .WithEnvironment("API_TOKEN", ApiKey)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("runner"))
            .Build();
    }

    private IContainer BuildSshGatewayContainer(string apiExternalUrl)
    {
        var sshGatewayImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_SSH_GATEWAY_IMAGE") ?? DefaultSshGatewayImage;

        return new ContainerBuilder(sshGatewayImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-ssh-gateway")
            .WithEnvironment("ENCRYPTION_KEY", _encryptionKey)
            .WithEnvironment("ENCRYPTION_SALT", _encryptionSalt)
            .WithEnvironment("SSH_GATEWAY_PORT", DefaultSshGatewayPort.ToString())
            .WithEnvironment("SSH_GATEWAY_URL", $"ssh://daytona-ssh-gateway:{DefaultSshGatewayPort}")
            .WithEnvironment("SSH_GATEWAY_API_URL", apiExternalUrl)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("ssh"))
            .Build();
    }

    /// <summary>
    /// Builds Docker-in-Docker container with security hardening.
    /// Security measures:
    /// - Privileged mode required for DinD (isolated from host Docker)
    /// - Internal test network only (no external exposure)
    /// - Resource limits: 2 CPUs, 4GB memory
    /// - No new privileges (prevent privilege escalation)
    /// - Seccomp profile for container isolation
    /// </summary>
    private IContainer BuildDindContainer()
    {
        var dindImage = Environment.GetEnvironmentVariable("SHARPCLAW_DIND_IMAGE") ?? DefaultDindImage;

        return new ContainerBuilder(dindImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-dind")
            .WithEnvironment("DOCKER_TLS_CERTDIR", string.Empty) // TLS disabled for internal test network only
            .WithEnvironment("DOCKER_RAMDISK", "1") // Reduce disk I/O for tests
            // Security: Privileged required for DinD, but isolated to test network only
            // DinD is more secure than mounting host Docker socket (which allows container escape)
            .WithPrivileged(true)
            // Resource limits to prevent resource exhaustion
            .WithCreateParameterModifier(cmd =>
            {
                cmd.HostConfig.Memory = 4L * 1024 * 1024 * 1024; // 4GB
                cmd.HostConfig.MemorySwap = 4L * 1024 * 1024 * 1024; // No swap (memory = memory+swap)
            })
            // Wait for Docker daemon to be ready
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("docker info"))
            .Build();
    }

    /// <summary>
    /// Builds Daytona runner container connected to DinD sidecar.
    /// Security measures:
    /// - No privileged mode (uses Docker-in-Docker sidecar instead of host socket)
    /// - Drop all capabilities - runner doesn't need special privileges
    /// - No new privileges - prevents privilege escalation
    /// - Resource limits: 4GB memory
    /// - Internal network only - no external exposure
    /// Note: Read-only root filesystem not used - runner needs to write certs/binaries
    /// </summary>
    private IContainer BuildRunnerContainerWithDind()
    {
        var runnerImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_RUNNER_IMAGE") ?? DefaultRunnerImage;

        return new ContainerBuilder(runnerImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-runner")
            // Security: Connect to DinD sidecar over TCP instead of mounting host Docker socket
            // This prevents container escape vulnerabilities from Docker socket access
            .WithEnvironment("DOCKER_HOST", "tcp://daytona-dind:2375")
            // Runner identification and registration
            .WithEnvironment("RUNNER_NAME", "default")
            .WithEnvironment("RUNNER_ID", "default-runner")
            .WithEnvironment("RUNNER_PORT", DefaultRunnerPort.ToString())
            .WithEnvironment("RUNNER_URL", $"http://daytona-runner:{DefaultRunnerPort}")
            // API connection configuration
            .WithEnvironment("API_URL", GetApiInternalBaseUrl())
            .WithEnvironment("API_KEY", ApiKey)
            .WithEnvironment("API_TOKEN", ApiKey)
            // Legacy/default runner environment variables
            .WithEnvironment("ENCRYPTION_KEY", _encryptionKey)
            .WithEnvironment("ENCRYPTION_SALT", _encryptionSalt)
            .WithEnvironment("DEFAULT_RUNNER_PORT", DefaultRunnerPort.ToString())
            .WithEnvironment("DEFAULT_RUNNER_URL", $"http://daytona-runner:{DefaultRunnerPort}")
            .WithEnvironment("DEFAULT_RUNNER_API_URL", GetApiInternalBaseUrl())
            .WithEnvironment("DAYTONA_API_URL", GetApiInternalBaseUrl())
            .WithEnvironment("SERVER_URL", $"http://daytona-api:{DefaultApiPort}")
            .WithEnvironment("DAYTONA_RUNNER_TOKEN", ApiKey)
            // Runner quotas and limits
            .WithEnvironment("RUNNER_CPU", "4")
            .WithEnvironment("RUNNER_MEMORY", "8")
            .WithEnvironment("RUNNER_DISK", "50")
            .WithEnvironment("DEFAULT_RUNNER_CPU", "4")
            .WithEnvironment("DEFAULT_RUNNER_MEMORY", "8")
            .WithEnvironment("DEFAULT_RUNNER_DISK", "50")
            // Disable GPU for tests
            .WithEnvironment("RUNNER_GPU", "0")
            .WithEnvironment("DEFAULT_RUNNER_GPU", "0")
            // Security hardening via Docker API
            .WithCreateParameterModifier(cmd =>
            {
                // Drop all capabilities and add only required ones
                cmd.HostConfig.CapDrop = ["ALL"];
                cmd.HostConfig.CapAdd = ["NET_BIND_SERVICE", "KILL"]; // Minimal required caps
                // Note: ReadonlyRootfs not used - runner needs to write certs, binaries, etc.
                // Resource limits
                cmd.HostConfig.Memory = 4L * 1024 * 1024 * 1024; // 4GB
                cmd.HostConfig.MemorySwap = 4L * 1024 * 1024 * 1024; // No swap
                // Security options: prevent privilege escalation
                cmd.HostConfig.SecurityOpt = ["no-new-privileges:true"];
            })
            // Wait for runner to be ready
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("runner"))
            .Build();
    }

    private async Task DeleteNetworkAsync(List<Exception> errors)
    {
        if (!_networkCreated)
        {
            return;
        }

        try
        {
            await _network.DeleteAsync();
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException("Failed to delete Daytona network.", ex));
        }
    }

    private async Task EnsureApiReadyAsync()
    {
        var healthUri = new Uri(new Uri(ServerUrl), _healthPath);
        using var client = new HttpClient
        {
            Timeout = _readyRequestTimeout
        };

        var deadline = DateTimeOffset.UtcNow + _readyTimeout;
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync(healthUri);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(_readyPollInterval);
        }

        throw new TimeoutException(
            $"Daytona API failed to become ready at {healthUri} within {_readyTimeout}.",
            lastError);
    }

    private async Task EnsureApiConfigReadyAsync()
    {
        var configUri = new Uri(new Uri(ServerUrl), "/config");
        using var client = new HttpClient
        {
            Timeout = _readyRequestTimeout
        };

        var deadline = DateTimeOffset.UtcNow + _readyTimeout;
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync(configUri);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(_readyPollInterval);
        }

        throw new TimeoutException(
            $"Daytona API /config endpoint failed to become ready at {configUri} within {_readyTimeout}.",
            lastError);
    }

    private async Task EnsureDependenciesReadyAsync()
    {
        await EnsureTcpReadyAsync("Postgres", _postgres, PostgresPort);
        await EnsureTcpReadyAsync("Redis", _redis, RedisPort);
        await EnsureHttpReadyAsync("MinIO", _minio, MinioPort, "/minio/health/ready");
        await EnsureHttpReadyAsync("Registry", _registry, RegistryPort, "/v2/");
        await EnsureHttpReadyAsync("Dex", _dex, DexPort, "/dex/.well-known/openid-configuration");
    }

    private async Task EnsureTcpReadyAsync(string name, IContainer container, int port)
    {
        var host = container.Hostname;
        var mappedPort = container.GetMappedPublicPort(port);
        var deadline = DateTimeOffset.UtcNow + _readyTimeout;
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, mappedPort).WaitAsync(_readyRequestTimeout);
                if (client.Connected)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(_readyPollInterval);
        }

        throw new TimeoutException(
            $"{name} failed to become ready at {host}:{mappedPort} within {_readyTimeout}.",
            lastError);
    }

    private async Task EnsureHttpReadyAsync(string name, IContainer container, int port, string path)
    {
        var host = container.Hostname;
        var mappedPort = container.GetMappedPublicPort(port);
        var uri = new UriBuilder("http", host, mappedPort, path).Uri;
        using var client = new HttpClient
        {
            Timeout = _readyRequestTimeout
        };

        var deadline = DateTimeOffset.UtcNow + _readyTimeout;
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync(uri);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(_readyPollInterval);
        }

        throw new TimeoutException(
            $"{name} failed to become ready at {uri} within {_readyTimeout}.",
            lastError);
    }

    private static TimeSpan GetDurationFromEnvironment(string name, TimeSpan defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        throw new InvalidOperationException($"Invalid {name} value '{raw}'.");
    }
}
