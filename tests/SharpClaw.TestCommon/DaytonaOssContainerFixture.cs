using System.Net;
using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Xunit;

namespace SharpClaw.TestCommon;

/// <summary>
/// Starts Daytona OSS full stack and dependencies in Docker for integration tests (requires Docker + Testcontainers).
/// Daytona integration tests are currently deferred and not run by default.
/// </summary>
public sealed class DaytonaOssContainerFixture : Xunit.IAsyncLifetime, IAsyncDisposable
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
    private const string DefaultApiImage = "daytonaio/daytona-api:latest";
    private const string DefaultProxyImage = "daytonaio/daytona-proxy:latest";
    private const string DefaultRunnerImage = "daytonaio/daytona-runner:latest";
    private const string DefaultSshGatewayImage = "daytonaio/daytona-ssh-gateway:latest";
    private const string DefaultPostgresImage = "postgres:18";
    private const string DefaultRedisImage = "redis:latest";
    private const string DefaultMinioImage = "minio/minio:latest";
    private const string DefaultRegistryImage = "registry:2.8.2";
    private const string DefaultDexImage = "dexidp/dex:v2.42.0";

    private readonly INetwork _network;
    private readonly IContainer _postgres;
    private readonly IContainer _redis;
    private readonly IContainer _minio;
    private readonly IContainer _registry;
    private readonly IContainer _dex;
    private readonly IContainer _proxy;
    private readonly IContainer _runner;
    private readonly IContainer _sshGateway;
    private readonly IContainer _daytonaApi;
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
        _dbName = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_DB_NAME") ?? "daytona";
        _dbUser = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_DB_USER") ?? "daytona";
        _dbPassword = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_DB_PASSWORD") ?? "daytona";
        _s3AccessKey = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_S3_ACCESS_KEY") ?? "daytona";
        _s3SecretKey = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_S3_SECRET_KEY") ?? "daytona-secret";
        _s3Bucket = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_S3_BUCKET") ?? "daytona";
        _s3Region = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_S3_REGION") ?? "us-east-1";
        _dexConfigPath = Path.Combine(Path.GetTempPath(), $"sharpclaw-dex-{Guid.NewGuid():N}.yaml");

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
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-postgres")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("database system is ready to accept connections"))
            .Build();

        var redisImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_REDIS_IMAGE") ?? DefaultRedisImage;
        _redis = new ContainerBuilder(redisImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-redis")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();

        var minioImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_MINIO_IMAGE") ?? DefaultMinioImage;
        _minio = new ContainerBuilder(minioImage)
            .WithEnvironment("MINIO_ROOT_USER", _s3AccessKey)
            .WithEnvironment("MINIO_ROOT_PASSWORD", _s3SecretKey)
            .WithCommand("server", "/data", "--console-address", ":9001")
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
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-dex")
            .WithBindMount(_dexConfigPath, "/etc/dex/config.yaml")
            .WithCommand("serve", "/etc/dex/config.yaml")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort((ushort)DexPort)
                    .ForPath("/dex/.well-known/openid-configuration")
                    .WithMethod(HttpMethod.Get)
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        var apiInternalUrl = $"http://daytona-api:{DefaultApiPort}";
        var proxyUrl = $"http://daytona-proxy:{DefaultProxyPort}";
        var runnerUrl = $"http://daytona-runner:{DefaultRunnerPort}";
        var sshGatewayUrl = $"ssh://daytona-ssh-gateway:{DefaultSshGatewayPort}";

        var proxyImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_PROXY_IMAGE") ?? DefaultProxyImage;
        _proxy = new ContainerBuilder(proxyImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-proxy")
            .WithEnvironment("ENCRYPTION_KEY", _encryptionKey)
            .WithEnvironment("ENCRYPTION_SALT", _encryptionSalt)
            .WithEnvironment("PROXY_PORT", DefaultProxyPort.ToString())
            .WithEnvironment("PROXY_URL", proxyUrl)
            .WithEnvironment("PROXY_API_URL", apiInternalUrl)
            .WithEnvironment("PROXY_API_KEY", ApiKey)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("proxy"))
            .Build();

        var runnerImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_RUNNER_IMAGE") ?? DefaultRunnerImage;
        _runner = new ContainerBuilder(runnerImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-runner")
            .WithEnvironment("ENCRYPTION_KEY", _encryptionKey)
            .WithEnvironment("ENCRYPTION_SALT", _encryptionSalt)
            .WithEnvironment("DEFAULT_RUNNER_PORT", DefaultRunnerPort.ToString())
            .WithEnvironment("DEFAULT_RUNNER_URL", runnerUrl)
            .WithEnvironment("DEFAULT_RUNNER_API_URL", apiInternalUrl)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("runner"))
            .Build();

        var sshGatewayImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_SSH_GATEWAY_IMAGE") ?? DefaultSshGatewayImage;
        _sshGateway = new ContainerBuilder(sshGatewayImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-ssh-gateway")
            .WithEnvironment("ENCRYPTION_KEY", _encryptionKey)
            .WithEnvironment("ENCRYPTION_SALT", _encryptionSalt)
            .WithEnvironment("SSH_GATEWAY_PORT", DefaultSshGatewayPort.ToString())
            .WithEnvironment("SSH_GATEWAY_URL", sshGatewayUrl)
            .WithEnvironment("SSH_GATEWAY_API_URL", apiInternalUrl)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("ssh"))
            .Build();

        var apiImage = Environment.GetEnvironmentVariable("SHARPCLAW_DAYTONA_API_IMAGE") ?? DefaultApiImage;
        _daytonaApi = new ContainerBuilder(apiImage)
            .WithNetwork(_network)
            .WithNetworkAliases("daytona-api")
            .WithPortBinding(_apiPort, true)
            .WithEnvironment("DAYTONA_API_KEY", ApiKey)
            .WithEnvironment("DAYTONA_SERVER_API_KEY", ApiKey)
            .WithEnvironment("ENCRYPTION_KEY", _encryptionKey)
            .WithEnvironment("ENCRYPTION_SALT", _encryptionSalt)
            .WithEnvironment("DB_HOST", "daytona-postgres")
            .WithEnvironment("DB_PORT", PostgresPort.ToString())
            .WithEnvironment("DB_NAME", _dbName)
            .WithEnvironment("DB_USER", _dbUser)
            .WithEnvironment("DB_PASSWORD", _dbPassword)
            .WithEnvironment("REDIS_HOST", "daytona-redis")
            .WithEnvironment("REDIS_PORT", RedisPort.ToString())
            .WithEnvironment("S3_ENDPOINT", $"http://daytona-minio:{MinioPort}")
            .WithEnvironment("S3_ACCESS_KEY", _s3AccessKey)
            .WithEnvironment("S3_SECRET_KEY", _s3SecretKey)
            .WithEnvironment("S3_BUCKET", _s3Bucket)
            .WithEnvironment("S3_REGION", _s3Region)
            .WithEnvironment("S3_USE_SSL", "false")
            .WithEnvironment("S3_FORCE_PATH_STYLE", "true")
            .WithEnvironment("PROXY_HOST", "daytona-proxy")
            .WithEnvironment("PROXY_PORT", DefaultProxyPort.ToString())
            .WithEnvironment("PROXY_URL", proxyUrl)
            .WithEnvironment("DEFAULT_RUNNER_HOST", "daytona-runner")
            .WithEnvironment("DEFAULT_RUNNER_PORT", DefaultRunnerPort.ToString())
            .WithEnvironment("DEFAULT_RUNNER_URL", runnerUrl)
            .WithEnvironment("SSH_GATEWAY_HOST", "daytona-ssh-gateway")
            .WithEnvironment("SSH_GATEWAY_PORT", DefaultSshGatewayPort.ToString())
            .WithEnvironment("SSH_GATEWAY_URL", sshGatewayUrl)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort((ushort)_apiPort)
                    .ForPath(_healthPath)
                    .WithMethod(HttpMethod.Get)
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();
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

            await _postgres.StartAsync();
            await _redis.StartAsync();
            await _minio.StartAsync();
            await _registry.StartAsync();
            await _dex.StartAsync();
            await _proxy.StartAsync();
            await _runner.StartAsync();
            await _sshGateway.StartAsync();
            await _daytonaApi.StartAsync();

            ServerUrl = $"http://127.0.0.1:{_daytonaApi.GetMappedPublicPort(_apiPort)}";
            _started = true;
        }
        catch
        {
            try
            {
                await DisposeAsync();
            }
            catch
            {
                // Ignore cleanup failures during startup.
            }

            throw;
        }
    }

    Task Xunit.IAsyncLifetime.InitializeAsync() => StartAsync();

    async Task Xunit.IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var errors = new List<Exception>();

        await StopAndDisposeAsync(_daytonaApi, "Daytona API", errors);
        await StopAndDisposeAsync(_sshGateway, "Daytona SSH gateway", errors);
        await StopAndDisposeAsync(_runner, "Daytona runner", errors);
        await StopAndDisposeAsync(_proxy, "Daytona proxy", errors);
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

    private static async Task StopAndDisposeAsync(IContainer container, string name, List<Exception> errors)
    {
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
}
