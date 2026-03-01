using SharpClaw.Gateway;
using Xunit.Abstractions;

namespace SharpClaw.Gateway.UnitTests;

/// <summary>
/// Unit tests for ConnectionRegistry and KeepaliveEnabledConnectionRegistry.
/// </summary>
public class ConnectionRegistryTests
{
    private readonly ITestOutputHelper _output;

    public ConnectionRegistryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region ConnectionRegistry Tests

    [Fact]
    public void TryConnect_NewConnection_ReturnsTrue()
    {
        var registry = new ConnectionRegistry();

        var result = registry.TryConnect("conn-1");

        Assert.True(result);
        Assert.Equal(1, registry.ActiveCount);
    }

    [Fact]
    public void TryConnect_DuplicateConnection_ReturnsFalse()
    {
        var registry = new ConnectionRegistry();
        registry.TryConnect("conn-1");

        var result = registry.TryConnect("conn-1");

        Assert.False(result);
        Assert.Equal(1, registry.ActiveCount);
    }

    [Fact]
    public void TryConnect_CaseSensitiveConnections_AreSeparate()
    {
        var registry = new ConnectionRegistry();

        var lower = registry.TryConnect("conn-1");
        var upper = registry.TryConnect("CONN-1");

        Assert.True(lower);
        Assert.True(upper);
        Assert.Equal(2, registry.ActiveCount);
    }

    [Fact]
    public void TryDisconnect_ExistingConnection_ReturnsTrue()
    {
        var registry = new ConnectionRegistry();
        registry.TryConnect("conn-1");

        var result = registry.TryDisconnect("conn-1");

        Assert.True(result);
        Assert.Equal(0, registry.ActiveCount);
    }

    [Fact]
    public void TryDisconnect_NonExistingConnection_ReturnsFalse()
    {
        var registry = new ConnectionRegistry();

        var result = registry.TryDisconnect("conn-nonexistent");

        Assert.False(result);
        Assert.Equal(0, registry.ActiveCount);
    }

    [Fact]
    public void TryDisconnect_WrongCase_ReturnsFalse()
    {
        var registry = new ConnectionRegistry();
        registry.TryConnect("conn-1");

        var result = registry.TryDisconnect("CONN-1");

        Assert.False(result);
        Assert.Equal(1, registry.ActiveCount);
    }

    [Fact]
    public void ActiveCount_MultipleConnections_ReturnsCorrectCount()
    {
        var registry = new ConnectionRegistry();

        for (var i = 0; i < 100; i++)
        {
            registry.TryConnect($"conn-{i}");
        }

        Assert.Equal(100, registry.ActiveCount);

        for (var i = 0; i < 50; i++)
        {
            registry.TryDisconnect($"conn-{i}");
        }

        Assert.Equal(50, registry.ActiveCount);
    }

    [Fact]
    public void TryConnect_NullOrEmpty_ThrowsArgumentException()
    {
        var registry = new ConnectionRegistry();

        Assert.Throws<ArgumentException>(() => registry.TryConnect(null!));
        Assert.Throws<ArgumentException>(() => registry.TryConnect(""));
        Assert.Throws<ArgumentException>(() => registry.TryConnect("   "));
    }

    [Fact]
    public void TryDisconnect_NullOrEmpty_ThrowsArgumentException()
    {
        var registry = new ConnectionRegistry();

        Assert.Throws<ArgumentException>(() => registry.TryDisconnect(null!));
        Assert.Throws<ArgumentException>(() => registry.TryDisconnect(""));
        Assert.Throws<ArgumentException>(() => registry.TryDisconnect("   "));
    }

    [Fact]
    public void TryConnect_ConcurrentConnections_HandledSafely()
    {
        var registry = new ConnectionRegistry();
        var connectionIds = Enumerable.Range(0, 1000).Select(i => $"conn-{i}").ToList();

        Parallel.ForEach(connectionIds, connId =>
        {
            registry.TryConnect(connId);
        });

        Assert.Equal(1000, registry.ActiveCount);
    }

    [Fact]
    public void TryDisconnect_ConcurrentDisconnections_HandledSafely()
    {
        var registry = new ConnectionRegistry();
        var connectionIds = Enumerable.Range(0, 1000).Select(i => $"conn-{i}").ToList();

        Parallel.ForEach(connectionIds, connId =>
        {
            registry.TryConnect(connId);
        });

        Parallel.ForEach(connectionIds, connId =>
        {
            registry.TryDisconnect(connId);
        });

        Assert.Equal(0, registry.ActiveCount);
    }

    #endregion

    #region GatewayHealthService Tests

    [Fact]
    public void Snapshot_WithActiveConnections_ReturnsCorrectCount()
    {
        var registry = new ConnectionRegistry();
        var healthService = new GatewayHealthService(registry);

        registry.TryConnect("conn-1");
        registry.TryConnect("conn-2");
        registry.TryConnect("conn-3");

        var snapshot = healthService.Snapshot();

        Assert.Equal(3, snapshot.ActiveConnections);
        Assert.True(snapshot.IsHealthy);
        Assert.True(snapshot.ObservedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Snapshot_NoConnections_ReturnsZeroCount()
    {
        var registry = new ConnectionRegistry();
        var healthService = new GatewayHealthService(registry);

        var snapshot = healthService.Snapshot();

        Assert.Equal(0, snapshot.ActiveConnections);
        Assert.True(snapshot.IsHealthy);
    }

    #endregion

    #region KeepaliveEnabledConnectionRegistry Tests

    [Fact]
    public void TryConnect_WithKeepaliveService_RegistersConnection()
    {
        var baseRegistry = new ConnectionRegistry();
        var keepaliveRegistry = new KeepaliveEnabledConnectionRegistry(baseRegistry, null);

        var result = keepaliveRegistry.TryConnect("conn-1");

        Assert.True(result);
        Assert.Equal(1, keepaliveRegistry.ActiveCount);
    }

    [Fact]
    public void TryDisconnect_WithKeepaliveService_UnregistersConnection()
    {
        var baseRegistry = new ConnectionRegistry();
        var keepaliveRegistry = new KeepaliveEnabledConnectionRegistry(baseRegistry, null);
        keepaliveRegistry.TryConnect("conn-1");

        var result = keepaliveRegistry.TryDisconnect("conn-1");

        Assert.True(result);
        Assert.Equal(0, keepaliveRegistry.ActiveCount);
    }

    [Fact]
    public void Keepalive_TryConnect_DuplicateConnection_ReturnsFalse()
    {
        var baseRegistry = new ConnectionRegistry();
        var keepaliveRegistry = new KeepaliveEnabledConnectionRegistry(baseRegistry, null);
        keepaliveRegistry.TryConnect("conn-1");

        var result = keepaliveRegistry.TryConnect("conn-1");

        Assert.False(result);
    }

    [Fact]
    public void TryConnect_NullConnectionId_ThrowsArgumentException()
    {
        var baseRegistry = new ConnectionRegistry();
        var keepaliveRegistry = new KeepaliveEnabledConnectionRegistry(baseRegistry, null);

        Assert.Throws<ArgumentException>(() => keepaliveRegistry.TryConnect(null!));
        Assert.Throws<ArgumentException>(() => keepaliveRegistry.TryConnect(""));
    }

    [Fact]
    public void TryDisconnect_NullConnectionId_ThrowsArgumentException()
    {
        var baseRegistry = new ConnectionRegistry();
        var keepaliveRegistry = new KeepaliveEnabledConnectionRegistry(baseRegistry, null);

        Assert.Throws<ArgumentException>(() => keepaliveRegistry.TryDisconnect(null!));
        Assert.Throws<ArgumentException>(() => keepaliveRegistry.TryDisconnect(""));
    }

    #endregion

    #region KeepalivePolicy Tests

    [Fact]
    public void Default_IsValid_ReturnsTrue()
    {
        var policy = KeepalivePolicy.Default;

        Assert.True(policy.IsValid());
    }

    [Theory]
    [InlineData(0, 10)] // Zero ping interval
    [InlineData(10, 0)] // Zero timeout
    [InlineData(10, 5)] // Timeout less than ping interval
    [InlineData(-1, 10)] // Negative ping interval
    [InlineData(10, -1)] // Negative timeout
    public void IsValid_InvalidScenarios_ReturnsFalse(int pingSeconds, int timeoutSeconds)
    {
        var policy = new KeepalivePolicy(
            TimeSpan.FromSeconds(pingSeconds),
            TimeSpan.FromSeconds(timeoutSeconds));

        Assert.False(policy.IsValid());
    }

    [Theory]
    [InlineData(5, 5)]  // Timeout equals ping interval
    [InlineData(5, 10)] // Timeout greater than ping interval
    [InlineData(1, 60)] // Short ping, long timeout
    public void IsValid_ValidScenarios_ReturnsTrue(int pingSeconds, int timeoutSeconds)
    {
        var policy = new KeepalivePolicy(
            TimeSpan.FromSeconds(pingSeconds),
            TimeSpan.FromSeconds(timeoutSeconds));

        Assert.True(policy.IsValid());
    }

    #endregion
}
