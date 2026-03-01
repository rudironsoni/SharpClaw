using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SharpClaw.Abstractions.Cloud;
using StackExchange.Redis;

namespace SharpClaw.Cloud.Azure.Cache;

/// <summary>
/// Azure Redis Cache implementation of <see cref="ICache"/>.
/// </summary>
public sealed class AzureRedisCache : ICache, IDisposable
{
    private readonly RedisConnectionManager _connectionManager;
    private readonly ILogger<AzureRedisCache>? _logger;
    private readonly TimeSpan? _defaultExpiration;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureRedisCache"/> class.
    /// </summary>
    /// <param name="connectionManager">The Redis connection manager.</param>
    /// <param name="defaultExpiration">Optional default expiration.</param>
    /// <param name="logger">Optional logger.</param>
    public AzureRedisCache(
        RedisConnectionManager connectionManager,
        TimeSpan? defaultExpiration = null,
        ILogger<AzureRedisCache>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(connectionManager);
        _connectionManager = connectionManager;
        _defaultExpiration = defaultExpiration;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateKey(key);

        var prefixedKey = _connectionManager.GetPrefixedKey(key);
        var database = _connectionManager.GetDatabase();

        try
        {
            var value = await database.StringGetAsync(prefixedKey).ConfigureAwait(false);

            if (value.IsNullOrEmpty)
            {
                return default;
            }

            var stringValue = (string?)value;
            if (string.IsNullOrEmpty(stringValue))
            {
                return default;
            }

            var deserializedValue = JsonSerializer.Deserialize<T>(stringValue, _jsonOptions);
            return deserializedValue;
        }
        catch (RedisException ex)
        {
            _logger?.LogError(ex, "Failed to get value from cache for key '{Key}'.", key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(
        string key,
        T value,
        CacheExpiration expiration,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);

        var prefixedKey = _connectionManager.GetPrefixedKey(key);
        var database = _connectionManager.GetDatabase();

        var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);

        // Calculate expiration
        var expiry = CalculateExpiration(expiration);

        try
        {
            await database.StringSetAsync(prefixedKey, serializedValue, expiry).ConfigureAwait(false);
            _logger?.LogDebug("Set value in cache for key '{Key}' with expiration {Expiration}.", key, expiry);
        }
        catch (RedisException ex)
        {
            _logger?.LogError(ex, "Failed to set value in cache for key '{Key}'.", key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateKey(key);

        var prefixedKey = _connectionManager.GetPrefixedKey(key);
        var database = _connectionManager.GetDatabase();

        try
        {
            await database.KeyDeleteAsync(prefixedKey).ConfigureAwait(false);
            _logger?.LogDebug("Removed value from cache for key '{Key}'.", key);
        }
        catch (RedisException ex)
        {
            _logger?.LogError(ex, "Failed to remove value from cache for key '{Key}'.", key);
            throw;
        }
    }

    /// <summary>
    /// Calculates the Redis expiration time from CacheExpiration.
    /// </summary>
    private TimeSpan? CalculateExpiration(CacheExpiration expiration)
    {
        // Prefer absolute expiration
        if (expiration.AbsoluteExpiration.HasValue)
        {
            return expiration.AbsoluteExpiration.Value;
        }

        // Fall back to default if no sliding expiration
        if (!expiration.SlidingExpiration.HasValue)
        {
            return _defaultExpiration;
        }

        // For sliding expiration, use it as the absolute expiration
        // Note: Redis doesn't natively support sliding expiration,
        // this would need custom implementation with GETSET pattern
        return expiration.SlidingExpiration.Value;
    }

    /// <summary>
    /// Validates the cache key.
    /// </summary>
    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _connectionManager.Dispose();
            _disposed = true;
        }
    }
}
