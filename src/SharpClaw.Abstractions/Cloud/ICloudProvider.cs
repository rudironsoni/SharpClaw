using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClaw.Abstractions.Cloud;

/// <summary>
/// Cloud provider abstraction.
/// </summary>
public interface ICloudProvider
{
    /// <summary>
    /// Creates a cloud storage instance.
    /// </summary>
    ICloudStorage CreateStorage();
    
    /// <summary>
    /// Creates a secret manager instance.
    /// </summary>
    ISecretManager CreateSecretManager();
    
    /// <summary>
    /// Creates a cache instance.
    /// </summary>
    ICache CreateCache();
}

/// <summary>
/// Cloud storage abstraction.
/// </summary>
public interface ICloudStorage
{
    /// <summary>
    /// Gets an object from storage.
    /// </summary>
    Task<Stream> GetObjectAsync(string bucket, string key, CancellationToken ct = default);
    
    /// <summary>
    /// Puts an object into storage.
    /// </summary>
    Task PutObjectAsync(string bucket, string key, Stream data, CancellationToken ct = default);
    
    /// <summary>
    /// Deletes an object from storage.
    /// </summary>
    Task DeleteObjectAsync(string bucket, string key, CancellationToken ct = default);
    
    /// <summary>
    /// Checks if an object exists.
    /// </summary>
    Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct = default);
}

/// <summary>
/// Secret manager abstraction.
/// </summary>
public interface ISecretManager
{
    /// <summary>
    /// Gets a secret value.
    /// </summary>
    Task<string> GetSecretAsync(string name, CancellationToken ct = default);
    
    /// <summary>
    /// Sets a secret value.
    /// </summary>
    Task SetSecretAsync(string name, string value, CancellationToken ct = default);
    
    /// <summary>
    /// Rotates a secret.
    /// </summary>
    Task RotateSecretAsync(string name, CancellationToken ct = default);
}

/// <summary>
/// Cache abstraction.
/// </summary>
public interface ICache
{
    /// <summary>
    /// Gets a value from cache.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    
    /// <summary>
    /// Sets a value in cache.
    /// </summary>
    Task SetAsync<T>(string key, T value, CacheExpiration expiration, CancellationToken ct = default);
    
    /// <summary>
    /// Removes a value from cache.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Cache expiration options.
/// </summary>
public sealed record CacheExpiration
{
    public TimeSpan? AbsoluteExpiration { get; init; }
    public TimeSpan? SlidingExpiration { get; init; }
}
