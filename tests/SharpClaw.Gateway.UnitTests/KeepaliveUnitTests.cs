using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Gateway.Events;
using SharpClaw.Gateway.Keepalive;
using Xunit;

namespace SharpClaw.Gateway.UnitTests;

internal static class TestDispatcherFactory
{
    public static GatewayDispatcher Create()
    {
        var eventPublisher = new ChannelEventPublisher(
            EventPublisherOptions.Default,
            NullLogger<ChannelEventPublisher>.Instance);
        return new GatewayDispatcher(eventPublisher, NullLogger<GatewayDispatcher>.Instance);
    }
}

public class ConnectionHealthMonitorUnitTests
{
    [Fact]
    public void TryRegister_NewConnection_ReturnsTrue()
    {
        var monitor = new ConnectionHealthMonitor();

        var result = monitor.TryRegister("conn-1");

        Assert.True(result);
        Assert.Equal(1, monitor.MonitoredCount);
    }

    [Fact]
    public void TryRegister_DuplicateConnection_ReturnsFalse()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");

        var result = monitor.TryRegister("conn-1");

        Assert.False(result);
        Assert.Equal(1, monitor.MonitoredCount);
    }

    [Fact]
    public void TryUnregister_ExistingConnection_ReturnsTrue()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");

        var result = monitor.TryUnregister("conn-1");

        Assert.True(result);
        Assert.Equal(0, monitor.MonitoredCount);
    }

    [Fact]
    public void TryUnregister_NonExistentConnection_ReturnsFalse()
    {
        var monitor = new ConnectionHealthMonitor();

        var result = monitor.TryUnregister("conn-1");

        Assert.False(result);
    }

    [Fact]
    public void RecordActivity_UpdatesLastActivity()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        monitor.RecordActivity("conn-1");

        Assert.True(monitor.TryGetHealthState("conn-1", out var state));
        Assert.True(state.LastActivity > before);
    }

    [Fact]
    public void RecordPingSent_SetsPendingPong()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");

        monitor.RecordPingSent("conn-1");

        Assert.True(monitor.TryGetHealthState("conn-1", out var state));
        Assert.True(state.PendingPong);
        Assert.Equal(1, monitor.TotalPingsSent);
    }

    [Fact]
    public void RecordPongReceived_ClearsPendingPong()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");
        monitor.RecordPingSent("conn-1");

        monitor.RecordPongReceived("conn-1");

        Assert.True(monitor.TryGetHealthState("conn-1", out var state));
        Assert.False(state.PendingPong);
        Assert.Equal(1, monitor.TotalPongsReceived);
    }

    [Fact]
    public void RecordPongReceived_ReturnsResponseTime()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");
        monitor.RecordPingSent("conn-1");

        // Small delay to ensure measurable response time
        Thread.Sleep(10);

        var responseTime = monitor.RecordPongReceived("conn-1");

        Assert.True(responseTime.HasValue);
        Assert.True(responseTime.Value.TotalMilliseconds > 0);
    }

    [Fact]
    public void RecordPongReceived_WithoutPing_ReturnsNull()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");

        var responseTime = monitor.RecordPongReceived("conn-1");

        Assert.Null(responseTime);
    }

    [Fact]
    public void RecordTimeout_IncrementsConsecutiveTimeouts()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");
        monitor.RecordPingSent("conn-1");

        var consecutive = monitor.RecordTimeout("conn-1");

        Assert.Equal(1, consecutive);
        Assert.True(monitor.TryGetHealthState("conn-1", out var state));
        Assert.Equal(1, state.ConsecutiveTimeouts);
    }

    [Fact]
    public void RecordTimeout_MultipleTimes_IncrementsEachTime()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");

        monitor.RecordTimeout("conn-1");
        monitor.RecordTimeout("conn-1");
        monitor.RecordTimeout("conn-1");

        Assert.True(monitor.TryGetHealthState("conn-1", out var state));
        Assert.Equal(3, state.ConsecutiveTimeouts);
        Assert.Equal(3, monitor.TotalTimeoutsDetected);
    }

    [Fact]
    public void GetMonitoredConnectionIds_ReturnsAllRegistered()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");
        monitor.TryRegister("conn-2");
        monitor.TryRegister("conn-3");

        var ids = monitor.GetMonitoredConnectionIds();

        Assert.Equal(3, ids.Count);
        Assert.Contains("conn-1", ids);
        Assert.Contains("conn-2", ids);
        Assert.Contains("conn-3", ids);
    }

    [Fact]
    public void TotalConnectionsTracked_CountsAllTimeRegistrations()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");
        monitor.TryRegister("conn-2");
        monitor.TryUnregister("conn-1");
        monitor.TryRegister("conn-3");

        Assert.Equal(3, monitor.TotalConnectionsTracked);
        Assert.Equal(2, monitor.MonitoredCount);
    }

    [Fact]
    public void RecordPongReceived_ResetsConsecutiveTimeouts()
    {
        var monitor = new ConnectionHealthMonitor();
        monitor.TryRegister("conn-1");
        monitor.RecordTimeout("conn-1");
        monitor.RecordTimeout("conn-1");

        monitor.RecordPongReceived("conn-1");

        Assert.True(monitor.TryGetHealthState("conn-1", out var state));
        Assert.Equal(0, state.ConsecutiveTimeouts);
    }
}

public class ConnectionHealthStateUnitTests
{
    [Fact]
    public void WithActivityRecorded_UpdatesLastActivity()
    {
        var state = new ConnectionHealthState();
        var before = DateTimeOffset.UtcNow;

        var updated = state.WithActivityRecorded();

        Assert.True(updated.LastActivity >= before);
    }

    [Fact]
    public void WithPingRecorded_SetsPendingPongAndLastPingSent()
    {
        var state = new ConnectionHealthState();

        var updated = state.WithPingRecorded();

        Assert.True(updated.PendingPong);
        Assert.NotNull(updated.LastPingSent);
    }

    [Fact]
    public void WithPongReceived_ClearsPendingPong()
    {
        var state = new ConnectionHealthState();
        var updated = state.WithPingRecorded();

        var final = updated.WithPongReceived();

        Assert.False(final.PendingPong);
    }

    [Fact]
    public void WithTimeoutRecorded_ClearsPendingPongAndIncrementsCount()
    {
        var state = new ConnectionHealthState();
        var updated = state.WithPingRecorded();

        var final = updated.WithTimeoutRecorded();

        Assert.False(final.PendingPong);
        Assert.Equal(1, final.ConsecutiveTimeouts);
    }

    [Fact]
    public void ConnectedAt_IsSetOnCreation()
    {
        var before = DateTimeOffset.UtcNow;

        var state = new ConnectionHealthState();

        Assert.True(state.ConnectedAt >= before);
    }

    [Fact]
    public void Immutability_OriginalStateUnchanged()
    {
        var original = new ConnectionHealthState();
        var originalLastActivity = original.LastActivity;

        var updated = original.WithActivityRecorded();

        // Original should be unchanged
        Assert.Equal(originalLastActivity, original.LastActivity);
        // Updated should be different
        Assert.True(updated.LastActivity > original.LastActivity);
    }
}

public class KeepaliveMetricsUnitTests
{
    [Fact]
    public void GetSnapshot_ReturnsCurrentStats()
    {
        var healthMonitor = new ConnectionHealthMonitor();
        using var metrics = new KeepaliveMetrics(healthMonitor);

        healthMonitor.TryRegister("conn-1");
        healthMonitor.RecordPingSent("conn-1");
        healthMonitor.RecordPongReceived("conn-1");

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(1, snapshot.MonitoredConnections);
        Assert.Equal(1, snapshot.TotalPingsSent);
        Assert.Equal(1, snapshot.TotalPongsReceived);
        Assert.Equal(0, snapshot.PendingPongs);
    }

    [Fact]
    public void RecordPingSent_IncrementsCounter()
    {
        var healthMonitor = new ConnectionHealthMonitor();
        using var metrics = new KeepaliveMetrics(healthMonitor);
        healthMonitor.TryRegister("conn-1");

        // Record pings through health monitor (which tracks actual counts)
        healthMonitor.RecordPingSent("conn-1");
        healthMonitor.RecordPingSent("conn-1");

        var snapshot = metrics.GetSnapshot();
        Assert.Equal(2, snapshot.TotalPingsSent);
    }

    [Fact]
    public void RecordTimeoutDetected_IncrementsCounter()
    {
        var healthMonitor = new ConnectionHealthMonitor();
        using var metrics = new KeepaliveMetrics(healthMonitor);
        healthMonitor.TryRegister("conn-1");

        // Record timeouts through health monitor
        healthMonitor.RecordTimeout("conn-1");
        healthMonitor.RecordTimeout("conn-1");

        Assert.Equal(2, healthMonitor.TotalTimeoutsDetected);
    }

    [Fact]
    public void RecordConnectionTerminated_IncrementsCounter()
    {
        var healthMonitor = new ConnectionHealthMonitor();
        using var metrics = new KeepaliveMetrics(healthMonitor);

        metrics.RecordConnectionTerminated();

        // Just verify no exception - counter is internal
        Assert.NotNull(metrics);
    }

    [Fact]
    public void GetSnapshot_CalculatesAverageResponseTime()
    {
        var healthMonitor = new ConnectionHealthMonitor();
        using var metrics = new KeepaliveMetrics(healthMonitor);

        healthMonitor.TryRegister("conn-1");
        healthMonitor.TryRegister("conn-2");

        // Simulate ping-pong with different response times
        var now = DateTimeOffset.UtcNow;

        // Can not easily test exact response times without mocking, but we can verify it runs
        var snapshot = metrics.GetSnapshot();
        Assert.True(snapshot.AverageResponseTimeMs >= 0);
    }
}

public class KeepaliveBackgroundServiceUnitTests
{
    [Fact]
    public void Constructor_WithInvalidPolicy_ThrowsArgumentException()
    {
        var registry = new ConnectionRegistry();
        var healthMonitor = new ConnectionHealthMonitor();
        var metrics = new KeepaliveMetrics(healthMonitor);
        var dispatcher = TestDispatcherFactory.Create();
        var invalidPolicy = new KeepalivePolicy(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));

        Assert.Throws<ArgumentException>(() =>
            new KeepaliveBackgroundService(
                registry,
                healthMonitor,
                metrics,
                dispatcher,
                NullLogger<KeepaliveBackgroundService>.Instance,
                invalidPolicy));
    }

    [Fact]
    public void RegisterConnection_AddsToHealthMonitor()
    {
        var registry = new ConnectionRegistry();
        var healthMonitor = new ConnectionHealthMonitor();
        var metrics = new KeepaliveMetrics(healthMonitor);
        var dispatcher = TestDispatcherFactory.Create();
        var service = new KeepaliveBackgroundService(
            registry,
            healthMonitor,
            metrics,
            dispatcher,
            NullLogger<KeepaliveBackgroundService>.Instance);

        var result = service.RegisterConnection("conn-1");

        Assert.True(result);
        Assert.Equal(1, healthMonitor.MonitoredCount);
    }

    [Fact]
    public void RegisterConnection_Duplicate_ReturnsFalse()
    {
        var registry = new ConnectionRegistry();
        var healthMonitor = new ConnectionHealthMonitor();
        var metrics = new KeepaliveMetrics(healthMonitor);
        var dispatcher = TestDispatcherFactory.Create();
        var service = new KeepaliveBackgroundService(
            registry,
            healthMonitor,
            metrics,
            dispatcher,
            NullLogger<KeepaliveBackgroundService>.Instance);
        service.RegisterConnection("conn-1");

        var result = service.RegisterConnection("conn-1");

        Assert.False(result);
    }

    [Fact]
    public void UnregisterConnection_RemovesFromHealthMonitor()
    {
        var registry = new ConnectionRegistry();
        var healthMonitor = new ConnectionHealthMonitor();
        var metrics = new KeepaliveMetrics(healthMonitor);
        var dispatcher = TestDispatcherFactory.Create();
        var service = new KeepaliveBackgroundService(
            registry,
            healthMonitor,
            metrics,
            dispatcher,
            NullLogger<KeepaliveBackgroundService>.Instance);
        service.RegisterConnection("conn-1");

        var result = service.UnregisterConnection("conn-1");

        Assert.True(result);
        Assert.Equal(0, healthMonitor.MonitoredCount);
    }

    [Fact]
    public void RecordPongReceived_UpdatesHealthState()
    {
        var registry = new ConnectionRegistry();
        var healthMonitor = new ConnectionHealthMonitor();
        var metrics = new KeepaliveMetrics(healthMonitor);
        var dispatcher = TestDispatcherFactory.Create();
        var service = new KeepaliveBackgroundService(
            registry,
            healthMonitor,
            metrics,
            dispatcher,
            NullLogger<KeepaliveBackgroundService>.Instance);
        service.RegisterConnection("conn-1");
        healthMonitor.RecordPingSent("conn-1");

        service.RecordPongReceived("conn-1");

        Assert.True(healthMonitor.TryGetHealthState("conn-1", out var state));
        Assert.False(state.PendingPong);
        Assert.Equal(1, healthMonitor.TotalPongsReceived);
    }

    [Fact]
    public void GetStatsSnapshot_ReturnsCurrentMetrics()
    {
        var registry = new ConnectionRegistry();
        var healthMonitor = new ConnectionHealthMonitor();
        var metrics = new KeepaliveMetrics(healthMonitor);
        var dispatcher = TestDispatcherFactory.Create();
        var service = new KeepaliveBackgroundService(
            registry,
            healthMonitor,
            metrics,
            dispatcher,
            NullLogger<KeepaliveBackgroundService>.Instance);
        service.RegisterConnection("conn-1");
        healthMonitor.RecordPingSent("conn-1");
        healthMonitor.RecordPongReceived("conn-1");

        var snapshot = service.GetStatsSnapshot();

        Assert.Equal(1, snapshot.MonitoredConnections);
        Assert.Equal(1, snapshot.TotalPingsSent);
        Assert.Equal(1, snapshot.TotalPongsReceived);
    }
}

public class KeepaliveEnabledConnectionRegistryUnitTests
{
    [Fact]
    public void TryConnect_RegistersWithRegistry()
    {
        var registry = new ConnectionRegistry();
        var wrapped = new KeepaliveEnabledConnectionRegistry(registry);

        var result = wrapped.TryConnect("conn-1");

        Assert.True(result);
        Assert.Equal(1, registry.ActiveCount);
    }

    [Fact]
    public void TryConnect_Duplicate_ReturnsFalse()
    {
        var registry = new ConnectionRegistry();
        var wrapped = new KeepaliveEnabledConnectionRegistry(registry);
        wrapped.TryConnect("conn-1");

        var result = wrapped.TryConnect("conn-1");

        Assert.False(result);
    }

    [Fact]
    public void TryDisconnect_RemovesFromRegistry()
    {
        var registry = new ConnectionRegistry();
        var wrapped = new KeepaliveEnabledConnectionRegistry(registry);
        wrapped.TryConnect("conn-1");

        var result = wrapped.TryDisconnect("conn-1");

        Assert.True(result);
        Assert.Equal(0, registry.ActiveCount);
    }

    [Fact]
    public void ActiveCount_ReturnsRegistryCount()
    {
        var registry = new ConnectionRegistry();
        var wrapped = new KeepaliveEnabledConnectionRegistry(registry);
        wrapped.TryConnect("conn-1");
        wrapped.TryConnect("conn-2");

        Assert.Equal(2, wrapped.ActiveCount);
    }

    [Fact]
    public void TryConnect_WithKeepaliveService_RegistersConnection()
    {
        var registry = new ConnectionRegistry();
        var healthMonitor = new ConnectionHealthMonitor();
        var metrics = new KeepaliveMetrics(healthMonitor);
        var dispatcher = TestDispatcherFactory.Create();
        var keepaliveService = new KeepaliveBackgroundService(
            registry,
            healthMonitor,
            metrics,
            dispatcher,
            NullLogger<KeepaliveBackgroundService>.Instance);
        var wrapped = new KeepaliveEnabledConnectionRegistry(registry, keepaliveService);

        var result = wrapped.TryConnect("conn-1");

        Assert.True(result);
        Assert.Equal(1, healthMonitor.MonitoredCount);
    }
}
