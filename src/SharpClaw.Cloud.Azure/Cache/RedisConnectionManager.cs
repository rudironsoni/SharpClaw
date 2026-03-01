using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SharpClaw.Cloud.Azure.Cache;

/// <summary>
/// Manages Redis connection multiplexing for optimal performance.
/// </summary>
public sealed class RedisConnectionManager : IDisposable
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisConnectionManager>? _logger;
    private readonly string _instanceName;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisConnectionManager"/> class.
    /// </summary>
    /// <param name="connectionMultiplexer">The Redis connection multiplexer.</param>
    /// <param name="instanceName">Optional instance name prefix.</param>
    /// <param name="logger">Optional logger.</param>
    public RedisConnectionManager(
        IConnectionMultiplexer connectionMultiplexer,
        string? instanceName = null,
        ILogger<RedisConnectionManager>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        _connectionMultiplexer = connectionMultiplexer;
        _instanceName = instanceName ?? string.Empty;
        _logger = logger;

        if (!_connectionMultiplexer.IsConnected)
        {
            throw new InvalidOperationException("Redis connection is not established.");
        }

        _logger?.LogInformation("Redis connection manager initialized.");
    }

    /// <summary>
    /// Gets the Redis database instance.
    /// </summary>
    /// <param name="db">The database number (default is 0).</param>
    /// <returns>The Redis database.</returns>
    public IDatabase GetDatabase(int db = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _connectionMultiplexer.GetDatabase(db);
    }

    /// <summary>
    /// Gets the Redis server instance.
    /// </summary>
    /// <param name="endPoint">The endpoint of the server.</param>
    /// <returns>The Redis server.</returns>
    public IServer GetServer(System.Net.EndPoint endPoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _connectionMultiplexer.GetServer(endPoint);
    }

    /// <summary>
    /// Gets all Redis server endpoints.
    /// </summary>
    /// <returns>The collection of endpoints.</returns>
    public System.Net.EndPoint[] GetEndPoints()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _connectionMultiplexer.GetEndPoints();
    }

    /// <summary>
    /// Prefixes a key with the instance name if configured.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>The prefixed key.</returns>
    public string GetPrefixedKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (string.IsNullOrEmpty(_instanceName))
        {
            return key;
        }

        return $"{_instanceName}:{key}";
    }

    /// <summary>
    /// Gets the connection status.
    /// </summary>
    public bool IsConnected => _connectionMultiplexer.IsConnected;

    /// <summary>
    /// Gets the connection multiplexer for advanced operations.
    /// </summary>
    public IConnectionMultiplexer ConnectionMultiplexer => _connectionMultiplexer;

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _connectionMultiplexer.Dispose();
            _disposed = true;
            _logger?.LogInformation("Redis connection manager disposed.");
        }
    }
}
