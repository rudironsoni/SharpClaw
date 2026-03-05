using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Xunit;

namespace SharpClaw.TestCommon;

/// <summary>
/// Starts Daytona OSS full stack and dependencies in Docker for integration tests.
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
    private const string DefaultApiHealthPath = "/api/health";
    private static readonly TimeSpan DefaultReadyTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DefaultReadyPollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultReadyRequestTimeout = TimeSpan.FromSeconds(15);

    private const string DefaultApiImage = "daytonaio/daytona-api:v0.148.0";
    private const string DefaultProxyImage = "daytonaio/daytona-proxy:v0.148.0";
    private const string DefaultRunnerImage = "daytonaio/daytona-runner:v0.148.0";
    private const string DefaultPostgresImage = "postgres:16";
    private const string DefaultRedisImage = "redis:latest";
    private const string DefaultMinioImage = "minio/minio:latest";
    private const string DefaultRegistryImage = "registry:2.8.2";
    private const string DefaultDexImage = "dexidp/dex:v2.42.0";
    private const string DefaultDindImage = "docker:27-dind";
    private static readonly TimeSpan ContainerOperationTimeout = TimeSpan.FromMinutes(5);

    private readonly INetwork _network;
    private readonly IContainer _postgres;
    private readonly IContainer _redis;
    private readonly IContainer _minio;
    private readonly IContainer _registry;
    private readonly IContainer _dex;
    private readonly IContainer _daytonaApi;
    private readonly IContainer _dind;
    private IContainer _daytonaRunner;
    private IContainer _daytonaProxy;
    private DotNet.Testcontainers.Images.IFutureDockerImage? _runnerCustomImage;
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
    private readonly string _runnerEnvDir;
    private readonly TimeSpan _readyTimeout;
    private readonly TimeSpan _readyPollInterval;
    private readonly TimeSpan _readyRequestTimeout;
    private bool _networkCreated;
    private bool _started;
    private bool _disposed;

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
        _dbName = "daytona";
        _dbUser = "daytona";
        _dbPassword = "daytona";
        _s3AccessKey = "daytona";
        _s3SecretKey = "daytona-secret";
        _s3Bucket = "daytona";
        _s3Region = "us-east-1";

        _readyTimeout = GetDurationFromEnvironment("SHARPCLAW_DAYTONA_READY_TIMEOUT", DefaultReadyTimeout);
        _readyPollInterval = GetDurationFromEnvironment("SHARPCLAW_DAYTONA_READY_POLL_INTERVAL", DefaultReadyPollInterval);
        _readyRequestTimeout = GetDurationFromEnvironment("SHARPCLAW_DAYTONA_READY_REQUEST_TIMEOUT", DefaultReadyRequestTimeout);

        _dexConfigPath = Path.Combine(Path.GetTempPath(), $"sharpclaw-dex-{Guid.NewGuid():N}.yaml");
        _runnerEnvDir = Path.Combine(Path.GetTempPath(), $"sharpclaw-daytona-env-{Guid.NewGuid():N}");

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

        Directory.CreateDirectory(_runnerEnvDir);

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
        var runnerUrl = $"http://daytona-runner:{DefaultRunnerPort}";
        var sshGatewayUrl = $"ssh://daytona-ssh-gateway:{DefaultSshGatewayPort}";

        var apiImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_API_IMAGE") ?? DefaultApiImage;
        _daytonaApi = new ContainerBuilder(apiImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-api")
            .WithPortBinding(_apiPort, true)
            .WithEnvironment("PORT", _apiPort.ToString())
            .WithEnvironment("DAYTONA_API_KEY", ApiKey)
            .WithEnvironment("DAYTONA_SERVER_API_KEY", ApiKey)
            .WithEnvironment("SKIP_CONNECTIONS", "false")
            .WithEnvironment("RUN_MIGRATIONS", "true")
            .WithEnvironment("NODE_ENV", "development")
            .WithEnvironment("ENCRYPTION_KEY", _encryptionKey)
            .WithEnvironment("ENCRYPTION_SALT", _encryptionSalt)
            .WithEnvironment("JWT_SECRET", _encryptionKey)
            .WithEnvironment("API_TOKEN_SECRET", _encryptionKey)
            .WithEnvironment("SESSION_SECRET", _encryptionSalt)
            .WithEnvironment("DB_HOST", "daytona-postgres")
            .WithEnvironment("DB_PORT", PostgresPort.ToString())
            .WithEnvironment("DB_USERNAME", _dbUser)
            .WithEnvironment("DB_PASSWORD", _dbPassword)
            .WithEnvironment("DB_DATABASE", _dbName)
            .WithEnvironment("ADMIN_API_KEY", ApiKey)
            .WithEnvironment("SKIP_USER_EMAIL_VERIFICATION", "true")
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
            .WithEnvironment("PROXY_HOST", "daytona-proxy")
            .WithEnvironment("PROXY_PORT", DefaultProxyPort.ToString())
            .WithEnvironment("PROXY_URL", proxyUrl)
            .WithEnvironment("DEFAULT_RUNNER_HOST", "daytona-runner")
            .WithEnvironment("DEFAULT_RUNNER_PORT", DefaultRunnerPort.ToString())
            .WithEnvironment("DEFAULT_RUNNER_URL", runnerUrl)
            .WithEnvironment("SSH_GATEWAY_HOST", "daytona-ssh-gateway")
            .WithEnvironment("SSH_GATEWAY_PORT", DefaultSshGatewayPort.ToString())
            .WithEnvironment("SSH_GATEWAY_URL", sshGatewayUrl)
            .WithEnvironment("OIDC_ISSUER_BASE_URL", $"http://daytona-dex:{DexPort}/dex")
            .WithEnvironment("OIDC_CLIENT_ID", "daytona")
            .WithEnvironment("OIDC_CLIENT_SECRET", "daytona-secret")
            .WithEnvironment("OIDC_AUDIENCE", "daytona")
            .WithEnvironment("OIDC_REDIRECT_URI", $"http://daytona-proxy:{DefaultProxyPort}/callback")
            .WithEnvironment("APP_URL", proxyUrl)
            .WithEnvironment("DASHBOARD_BASE_API_URL", proxyUrl)
            .WithEnvironment("DASHBOARD_URL", proxyUrl)
            .WithEnvironment("DEFAULT_SNAPSHOT", "ubuntu:22.04")
            .WithEnvironment("TRANSIENT_REGISTRY_URL", $"http://daytona-registry:{RegistryPort}")
            .WithEnvironment("TRANSIENT_REGISTRY_ADMIN", "admin")
            .WithEnvironment("TRANSIENT_REGISTRY_PASSWORD", "password")
            .WithEnvironment("TRANSIENT_REGISTRY_PROJECT_ID", "daytona")
            .WithEnvironment("INTERNAL_REGISTRY_URL", $"http://daytona-registry:{RegistryPort}")
            .WithEnvironment("INTERNAL_REGISTRY_ADMIN", "admin")
            .WithEnvironment("INTERNAL_REGISTRY_PASSWORD", "password")
            .WithEnvironment("INTERNAL_REGISTRY_PROJECT_ID", "daytona")
            .WithEnvironment("SSH_GATEWAY_API_KEY", ApiKey)
            .WithEnvironment("SSH_GATEWAY_COMMAND", $"ssh -p {DefaultSshGatewayPort} {{{{TOKEN}}}}@localhost")
            .WithEnvironment("SSH_GATEWAY_URL", $"localhost:{DefaultSshGatewayPort}")
            .WithEnvironment("PROXY_API_KEY", ApiKey)
            .WithEnvironment("PROXY_PROTOCOL", "http")
            .WithEnvironment("PROXY_DOMAIN", $"proxy.localhost:{DefaultProxyPort}")
            .WithEnvironment("PROXY_TEMPLATE_URL", $"http://{{{{PORT}}}}-{{{{sandboxId}}}}.proxy.localhost:{DefaultProxyPort}")
            .WithEnvironment("PROXY_TOOLBOX_BASE_URL", $"http://proxy.localhost:{DefaultProxyPort}")
            .WithEnvironment("DEFAULT_RUNNER_DOMAIN", $"localhost:{DefaultRunnerPort}")
            .WithEnvironment("DEFAULT_RUNNER_API_URL", runnerUrl)
            .WithEnvironment("DEFAULT_RUNNER_PROXY_URL", runnerUrl)
            .WithEnvironment("DEFAULT_RUNNER_API_KEY", ApiKey)
            .WithEnvironment("DEFAULT_RUNNER_CPU", "4")
            .WithEnvironment("DEFAULT_RUNNER_MEMORY", "8")
            .WithEnvironment("DEFAULT_RUNNER_DISK", "50")
            .WithEnvironment("DEFAULT_RUNNER_NAME", "default")
            .WithEnvironment("HEALTH_CHECK_API_KEY", ApiKey)
            .WithEnvironment("API_KEY_VALIDATION_CACHE_TTL_SECONDS", "10")
            .WithEnvironment("API_KEY_USER_CACHE_TTL_SECONDS", "60")
            .WithEnvironment("MAINTENANCE_MODE", "false")
            .WithEnvironment("OTEL_ENABLED", "false")
            .WithEnvironment("LOG_LEVEL", "debug")
            .WithEnvironment("DEBUG", "true")
            .WithEnvironment("DISABLE_TELEMETRY", "true")
            .WithEnvironment("WEBHOOK_SECRET", ApiKey)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort((ushort)_apiPort)
                    .ForPath(_healthPath)
                    .WithMethod(HttpMethod.Get)
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        _dind = BuildDindContainer();
        _daytonaRunner = null!;
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
            Console.WriteLine("[Daytona] Creating network...");
            using (var networkCts = new CancellationTokenSource(ContainerOperationTimeout))
            {
                await _network.CreateAsync(networkCts.Token);
            }

            _networkCreated = true;
            Console.WriteLine("[Daytona] Network created successfully");

            await StartContainerWithTimeoutAsync(_postgres, "Postgres");
            await StartContainerWithTimeoutAsync(_redis, "Redis");
            await StartContainerWithTimeoutAsync(_minio, "MinIO");
            await StartContainerWithTimeoutAsync(_registry, "Registry");
            await StartContainerWithTimeoutAsync(_dex, "Dex");

            await StartContainerWithTimeoutAsync(_dind, "Docker-in-Docker");
            await StartContainerWithTimeoutAsync(_daytonaApi, "Daytona API");

            ServerUrl = $"http://127.0.0.1:{_daytonaApi.GetMappedPublicPort(_apiPort)}";

            _daytonaRunner = await BuildRunnerContainerWithTimeoutAsync();
            await StartContainerWithTimeoutAsync(_daytonaRunner, "Daytona Runner");
            await EnsureRunnerReadyWithTimeoutAsync();

            _daytonaProxy = BuildProxyContainer();
            await StartContainerWithTimeoutAsync(_daytonaProxy, "Daytona Proxy");
            await EnsureProxyReadyWithTimeoutAsync();

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
            }

            throw;
        }
    }

    private static async Task StartContainerWithTimeoutAsync(IContainer container, string name)
    {
        Console.WriteLine($"[Daytona] Starting {name} container...");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var cts = new CancellationTokenSource(ContainerOperationTimeout);
            await container.StartAsync(cts.Token);
            Console.WriteLine($"[Daytona] {name} container started successfully in {stopwatch.Elapsed.TotalSeconds:F1}s");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[Daytona] ERROR: {name} container startup timed out after {stopwatch.Elapsed.TotalSeconds:F1}s");
            throw new TimeoutException($"{name} container startup timed out after {ContainerOperationTimeout.TotalMinutes} minutes");
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine($"[Daytona] ERROR: {name} container readiness check timed out after {stopwatch.Elapsed.TotalSeconds:F1}s");
            Console.WriteLine($"[Daytona] ERROR: {ex.Message}");
            throw new TimeoutException($"{name} container readiness check timed out. The container started but did not become ready within {ContainerOperationTimeout.TotalMinutes} minutes. Check that the wait strategy is correct for this container.", ex);
        }
    }

    private async Task<IContainer> BuildRunnerContainerWithTimeoutAsync()
    {
        Console.WriteLine("[Daytona] Building custom runner image...");
        try
        {
            using var cts = new CancellationTokenSource(ContainerOperationTimeout);
            var runnerContainer = await BuildRunnerContainerWithTokenAsync(cts.Token);
            Console.WriteLine("[Daytona] Custom runner image built successfully");
            return runnerContainer;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[Daytona] ERROR: Custom runner image build timed out after {ContainerOperationTimeout.TotalMinutes} minutes");
            throw new TimeoutException($"Custom runner image build timed out after {ContainerOperationTimeout.TotalMinutes} minutes");
        }
    }

    private async Task EnsureRunnerReadyWithTimeoutAsync()
    {
        Console.WriteLine("[Daytona] Waiting for runner to be ready...");
        var deadline = DateTimeOffset.UtcNow + _readyTimeout;
        Exception? lastError = null;

        using var cts = new CancellationTokenSource(ContainerOperationTimeout);
        var host = _daytonaRunner.Hostname;
        var mappedPort = _daytonaRunner.GetMappedPublicPort(DefaultRunnerPort);

        while (DateTimeOffset.UtcNow < deadline && !cts.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, mappedPort).WaitAsync(_readyRequestTimeout, cts.Token);
                if (client.Connected)
                {
                    Console.WriteLine("[Daytona] Runner is ready");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[Daytona] ERROR: Runner readiness check timed out after {ContainerOperationTimeout.TotalMinutes} minutes");
                throw new TimeoutException($"Runner readiness check timed out after {ContainerOperationTimeout.TotalMinutes} minutes");
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(_readyPollInterval, cts.Token);
        }

        throw new TimeoutException($"Daytona runner not ready at {host}:{mappedPort} within {_readyTimeout}.", lastError);
    }

    private async Task EnsureProxyReadyWithTimeoutAsync()
    {
        Console.WriteLine("[Daytona] Waiting for proxy to be ready...");
        var host = _daytonaProxy.Hostname;
        var mappedPort = _daytonaProxy.GetMappedPublicPort(DefaultProxyPort);
        var uri = new UriBuilder("http", host, mappedPort, "/health").Uri;
        using var client = new HttpClient { Timeout = _readyRequestTimeout };
        var deadline = DateTimeOffset.UtcNow + _readyTimeout;
        Exception? lastError = null;

        using var cts = new CancellationTokenSource(ContainerOperationTimeout);

        while (DateTimeOffset.UtcNow < deadline && !cts.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync(uri, cts.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine("[Daytona] Proxy is ready");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[Daytona] ERROR: Proxy readiness check timed out after {ContainerOperationTimeout.TotalMinutes} minutes");
                throw new TimeoutException($"Proxy readiness check timed out after {ContainerOperationTimeout.TotalMinutes} minutes");
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(_readyPollInterval, cts.Token);
        }

        throw new TimeoutException($"Daytona proxy not ready at {uri} within {_readyTimeout}.", lastError);
    }

    public Task InitializeAsync()
    {
        return StartAsync();
    }

    public Task DisposeAsync()
    {
        return DisposeInternalAsync();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisposeInternalAsync();
    }

    private async Task DisposeInternalAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var errors = new List<Exception>();

        await StopAndDisposeAsync(_daytonaProxy, "Daytona Proxy", errors);
        await StopAndDisposeAsync(_daytonaApi, "Daytona API", errors);
        await StopAndDisposeAsync(_daytonaRunner, "Daytona Runner", errors);
        await StopAndDisposeAsync(_dind, "Docker-in-Docker", errors);
        await StopAndDisposeAsync(_dex, "Dex", errors);
        await StopAndDisposeAsync(_registry, "Registry", errors);
        await StopAndDisposeAsync(_minio, "MinIO", errors);
        await StopAndDisposeAsync(_redis, "Redis", errors);
        await StopAndDisposeAsync(_postgres, "Postgres", errors);

        // Dispose custom runner image
        if (_runnerCustomImage is IAsyncDisposable asyncDisposableImage)
        {
            try
            {
                await asyncDisposableImage.DisposeAsync();
            }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException("Failed to dispose custom runner image.", ex));
            }
        }

        if (_networkCreated)
        {
            try
            {
                await _network.DeleteAsync();
            }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException("Failed to delete network.", ex));
            }
        }

        try
        {
            if (File.Exists(_dexConfigPath))
            {
                File.Delete(_dexConfigPath);
            }
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException("Failed to delete Dex config.", ex));
        }

        try
        {
            if (Directory.Exists(_runnerEnvDir))
            {
                Directory.Delete(_runnerEnvDir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException("Failed to delete runner env dir.", ex));
        }

        if (errors.Count > 0)
        {
            throw new AggregateException("Failed to dispose Daytona containers.", errors);
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
            errors.Add(new InvalidOperationException($"Failed to stop {name}.", ex));
        }

        try
        {
            await container.DisposeAsync();
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException($"Failed to dispose {name}.", ex));
        }
    }

    private IContainer BuildDindContainer()
    {
        var dindImage = Environment.GetEnvironmentVariable("SHARPCLAW_DIND_IMAGE") ?? DefaultDindImage;

        return new ContainerBuilder(dindImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-dind")
            .WithEnvironment("DOCKER_TLS_CERTDIR", string.Empty)
            .WithPrivileged(true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("docker info"))
            .Build();
    }

    private async Task<IContainer> BuildRunnerContainerWithTokenAsync(CancellationToken cancellationToken)
    {
        var runnerImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_RUNNER_IMAGE") ?? DefaultRunnerImage;
        var apiUrl = $"http://daytona-api:{_apiPort}/api";

        var envContent = $"API_URL={apiUrl}{Environment.NewLine}" +
            $"DAYTONA_API_URL={apiUrl}{Environment.NewLine}" +
            $"SERVER_URL={apiUrl}{Environment.NewLine}" +
            $"DEFAULT_RUNNER_API_URL={apiUrl}{Environment.NewLine}" +
            $"API_KEY={ApiKey}{Environment.NewLine}" +
            $"API_TOKEN={ApiKey}{Environment.NewLine}" +
            $"DAYTONA_RUNNER_TOKEN={ApiKey}{Environment.NewLine}" +
            $"RUNNER_NAME=default{Environment.NewLine}" +
            $"RUNNER_ID=default-runner{Environment.NewLine}" +
            $"RUNNER_PORT={DefaultRunnerPort}{Environment.NewLine}" +
            $"RUNNER_URL=http://daytona-runner:{DefaultRunnerPort}{Environment.NewLine}" +
            "DOCKER_HOST=tcp://daytona-dind:2375";

        File.WriteAllText(Path.Combine(_runnerEnvDir, ".env"), envContent);

        // Create custom Dockerfile with baked-in .env file
        var dockerfileContent = $@"FROM {runnerImage}
WORKDIR /config
COPY .env /config/.env
";

        var dockerfilePath = Path.Combine(_runnerEnvDir, "Dockerfile");
        File.WriteAllText(dockerfilePath, dockerfileContent);

        // Build custom image with .env baked in
        var imageBuilder = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(_runnerEnvDir)
            .WithBuildArgument("DOCKER_BUILDKIT", "1");

        _runnerCustomImage = imageBuilder.Build();
        await _runnerCustomImage.CreateAsync(cancellationToken);

        return new ContainerBuilder(_runnerCustomImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-runner")
            .WithWorkingDirectory("/config")
            .WithPortBinding(DefaultRunnerPort, true)
            .WithEnvironment("DOCKER_HOST", "tcp://daytona-dind:2375")
            .WithEnvironment("API_URL", apiUrl)
            .WithEnvironment("DAYTONA_API_URL", apiUrl)
            .WithEnvironment("SERVER_URL", apiUrl)
            .WithEnvironment("DEFAULT_RUNNER_API_URL", apiUrl)
            .WithEnvironment("API_KEY", ApiKey)
            .WithEnvironment("API_TOKEN", ApiKey)
            .WithEnvironment("DAYTONA_RUNNER_TOKEN", ApiKey)
            .WithEnvironment("RUNNER_NAME", "default")
            .WithEnvironment("RUNNER_ID", "default-runner")
            .WithEnvironment("RUNNER_PORT", DefaultRunnerPort.ToString())
            .WithEnvironment("RUNNER_URL", $"http://daytona-runner:{DefaultRunnerPort}")
            .Build();
    }

    private IContainer BuildProxyContainer()
    {
        var proxyImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_PROXY_IMAGE") ?? DefaultProxyImage;
        var apiInternalUrl = $"http://daytona-api:{_apiPort}/api";

        return new ContainerBuilder(proxyImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-proxy")
            .WithPortBinding(DefaultProxyPort, true)
            .WithEnvironment("ENCRYPTION_KEY", _encryptionKey)
            .WithEnvironment("ENCRYPTION_SALT", _encryptionSalt)
            .WithEnvironment("PROXY_PORT", DefaultProxyPort.ToString())
            .WithEnvironment("PROXY_URL", $"http://daytona-proxy:{DefaultProxyPort}")
            .WithEnvironment("PROXY_API_URL", apiInternalUrl)
            .WithEnvironment("PROXY_API_KEY", ApiKey)
            .WithEnvironment("PROXY_PROTOCOL", "http")
            .WithEnvironment("DAYTONA_API_URL", apiInternalUrl)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort((ushort)DefaultProxyPort)
                    .ForPath("/health")
                    .WithMethod(HttpMethod.Get)
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();
    }

    private async Task EnsureRunnerReadyAsync()
    {
        var host = _daytonaRunner.Hostname;
        var mappedPort = _daytonaRunner.GetMappedPublicPort(DefaultRunnerPort);
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

        throw new TimeoutException($"Daytona runner not ready at {host}:{mappedPort} within {_readyTimeout}.", lastError);
    }

    private async Task EnsureProxyReadyAsync()
    {
        var host = _daytonaProxy.Hostname;
        var mappedPort = _daytonaProxy.GetMappedPublicPort(DefaultProxyPort);
        var uri = new UriBuilder("http", host, mappedPort, "/health").Uri;
        using var client = new HttpClient { Timeout = _readyRequestTimeout };
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

        throw new TimeoutException($"Daytona proxy not ready at {uri} within {_readyTimeout}.", lastError);
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
