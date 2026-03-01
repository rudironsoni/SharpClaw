using Azure.Core;
using SharpClaw.Abstractions.Cloud;
using SharpClaw.Cloud.Azure.Auth;
using SharpClaw.Cloud.Azure.Cache;
using SharpClaw.Cloud.Azure.Secrets;
using SharpClaw.Cloud.Azure.Storage;
using StackExchange.Redis;

namespace SharpClaw.Cloud.Azure;

/// <summary>
/// Azure implementation of <see cref="ICloudProvider"/>.
/// </summary>
public sealed class AzureCloudProvider : ICloudProvider, IDisposable
{
    private readonly AzureCloudProviderOptions _options;
    private readonly TokenCredential _credential;
    private AzureBlobStorage? _blobStorage;
    private AzureKeyVaultSecretManager? _secretManager;
    private AzureRedisCache? _redisCache;
    private RedisConnectionManager? _redisConnectionManager;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureCloudProvider"/> class.
    /// </summary>
    /// <param name="options">The Azure cloud provider options.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public AzureCloudProvider(AzureCloudProviderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _credential = AzureCredentialFactory.CreateCredential(options);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureCloudProvider"/> class with a pre-configured credential.
    /// </summary>
    /// <param name="options">The Azure cloud provider options.</param>
    /// <param name="credential">The Azure token credential.</param>
    public AzureCloudProvider(AzureCloudProviderOptions options, TokenCredential credential)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
    }

    /// <inheritdoc />
    public ICloudStorage CreateStorage()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_blobStorage is not null)
        {
            return _blobStorage;
        }

        if (_options.BlobStorage is null)
        {
            throw new InvalidOperationException(
                "Blob storage is not configured. Set AzureCloudProviderOptions.BlobStorage.");
        }

        _blobStorage = new AzureBlobStorage(_credential, _options.BlobStorage);
        return _blobStorage;
    }

    /// <inheritdoc />
    public ISecretManager CreateSecretManager()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_secretManager is not null)
        {
            return _secretManager;
        }

        if (_options.KeyVault is null)
        {
            throw new InvalidOperationException(
                "Key Vault is not configured. Set AzureCloudProviderOptions.KeyVault.");
        }

        _secretManager = new AzureKeyVaultSecretManager(_credential, _options.KeyVault);
        return _secretManager;
    }

    /// <inheritdoc />
    public ICache CreateCache()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_redisCache is not null)
        {
            return _redisCache;
        }

        if (_options.RedisCache is null)
        {
            throw new InvalidOperationException(
                "Redis cache is not configured. Set AzureCloudProviderOptions.RedisCache.");
        }

        // Create connection multiplexer with configuration options
        var configurationOptions = CreateRedisConfigurationOptions(_options.RedisCache);
        var connectionMultiplexer = ConnectionMultiplexer.Connect(configurationOptions);

        _redisConnectionManager = new RedisConnectionManager(
            connectionMultiplexer,
            _options.RedisCache.InstanceName);

        _redisCache = new AzureRedisCache(
            _redisConnectionManager,
            _options.RedisCache.DefaultExpiration);

        return _redisCache;
    }

    /// <summary>
    /// Creates Redis configuration options from provider options.
    /// </summary>
    private static ConfigurationOptions CreateRedisConfigurationOptions(AzureRedisCacheOptions options)
    {
        var config = ConfigurationOptions.Parse(options.ConnectionString);

        config.AbortOnConnectFail = options.AbortOnConnectFail;
        config.ConnectTimeout = options.ConnectTimeout;
        config.SyncTimeout = options.SyncTimeout;
        config.ConnectRetry = options.ConnectRetryCount;
        config.Ssl = options.UseSsl;

        if (options.MaxPoolSize > 0)
        {
            // StackExchange.Redis manages connections automatically
            // The MaxPoolSize concept is handled differently
        }

        return config;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _blobStorage?.DisposeAsync().AsTask().Wait();
            _secretManager?.DisposeAsync().AsTask().Wait();
            _redisCache?.Dispose();
            _redisConnectionManager?.Dispose();
            _disposed = true;
        }
    }
}
