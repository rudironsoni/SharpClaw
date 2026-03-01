using Microsoft.Extensions.Logging;
using NSubstitute;
using SharpClaw.Abstractions.Cloud;
using SharpClaw.Cloud.Azure.Cache;
using StackExchange.Redis;
using Xunit;

namespace SharpClaw.Cloud.Azure.UnitTests.Cache;

public class AzureRedisCacheTests
{
    private readonly IConnectionMultiplexer _mockConnectionMultiplexer;
    private readonly IDatabase _mockDatabase;
    private readonly ILogger<AzureRedisCache> _mockLogger;

    public AzureRedisCacheTests()
    {
        _mockConnectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
        _mockDatabase = Substitute.For<IDatabase>();
        _mockLogger = Substitute.For<ILogger<AzureRedisCache>>();

        _mockConnectionMultiplexer.IsConnected.Returns(true);
        _mockConnectionMultiplexer.GetDatabase(Arg.Any<int>()).Returns(_mockDatabase);
    }

    private RedisConnectionManager CreateConnectionManager()
    {
        return new RedisConnectionManager(_mockConnectionMultiplexer, null, null);
    }

    [Fact]
    public void Constructor_WithNullConnectionManager_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AzureRedisCache(null!));
    }

    [Fact]
    public async Task GetAsync_WithNullKey_ThrowsArgumentNullException()
    {
        var cache = new AzureRedisCache(CreateConnectionManager(), logger: _mockLogger);

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.GetAsync<string>(null!));
    }

    [Fact]
    public async Task SetAsync_WithNullValue_ThrowsArgumentNullException()
    {
        var cache = new AzureRedisCache(CreateConnectionManager(), logger: _mockLogger);
        var expiration = new CacheExpiration();

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.SetAsync<string>("key", null!, expiration));
    }

    [Fact]
    public async Task RemoveAsync_WithNullKey_ThrowsArgumentNullException()
    {
        var cache = new AzureRedisCache(CreateConnectionManager(), logger: _mockLogger);

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.RemoveAsync(null!));
    }

    [Fact]
    public async Task RemoveAsync_WithEmptyKey_ThrowsArgumentException()
    {
        var cache = new AzureRedisCache(CreateConnectionManager(), logger: _mockLogger);

        await Assert.ThrowsAsync<ArgumentException>(() => cache.RemoveAsync(""));
    }
}
