namespace SharpClaw.Cloud.Azure.Cache;

/// <summary>
/// Configuration options for Azure Redis Cache.
/// </summary>
public sealed class AzureRedisCacheOptions
{
    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the instance name (prefix) for cache keys.
    /// </summary>
    public string? InstanceName { get; set; }

    /// <summary>
    /// Gets or sets the default expiration time for cache entries.
    /// </summary>
    public TimeSpan? DefaultExpiration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use SSL/TLS for connections.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// Gets or sets the connection timeout in milliseconds.
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the sync timeout in milliseconds.
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the abort on connect fail flag.
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of connection retry attempts.
    /// </summary>
    public int ConnectRetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of concurrent connections in the pool.
    /// </summary>
    public int MaxPoolSize { get; set; } = 50;
}
