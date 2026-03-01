using System.Collections.Concurrent;
using SharpClaw.RateLimiting.Abstractions;

namespace SharpClaw.RateLimiting.Stores;

/// <summary>
/// In-memory rate limit store using ConcurrentDictionary.
/// </summary>
public sealed class MemoryRateLimitStore : IRateLimitStore, IDisposable
{
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly Timer? _cleanupTimer;
    private readonly TimeSpan _bucketExpiration;
    
    public MemoryRateLimitStore(TimeSpan? bucketExpiration = null)
    {
        _bucketExpiration = bucketExpiration ?? TimeSpan.FromHours(1);
        _cleanupTimer = new Timer(CleanupExpiredBuckets, null, _bucketExpiration, _bucketExpiration);
    }
    
    public RateLimitBucket GetOrCreateBucket(string key, TokenBucketOptions options)
    {
        return _buckets.GetOrAdd(key, _ => new RateLimitBucket
        {
            Tokens = options.TokenLimit,
            LastReplenishment = DateTimeOffset.UtcNow,
            Options = options
        });
    }
    
    public bool TryRemoveBucket(string key)
    {
        return _buckets.TryRemove(key, out _);
    }
    
    public IEnumerable<string> GetBucketKeys()
    {
        return _buckets.Keys;
    }
    
    private void CleanupExpiredBuckets(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _buckets
            .Where(kvp => now - kvp.Value.LastReplenishment > _bucketExpiration)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in expiredKeys)
        {
            _buckets.TryRemove(key, out _);
        }
    }
    
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _buckets.Clear();
    }
}
