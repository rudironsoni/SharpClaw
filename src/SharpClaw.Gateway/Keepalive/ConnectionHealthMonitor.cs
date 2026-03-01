using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SharpClaw.Gateway.Keepalive;

/// <summary>
/// Immutable health state for a single connection. Thread-safe by design.
/// </summary>
public sealed record ConnectionHealthState
{
    /// <summary>
    /// UTC timestamp of the last activity on this connection.
    /// </summary>
    public DateTimeOffset LastActivity { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// UTC timestamp when the last ping was sent.
    /// </summary>
    public DateTimeOffset? LastPingSent { get; init; }

    /// <summary>
    /// Whether a pong response is currently pending for a sent ping.
    /// </summary>
    public bool PendingPong { get; init; }

    /// <summary>
    /// Number of consecutive timeouts detected.
    /// </summary>
    public int ConsecutiveTimeouts { get; init; }

    /// <summary>
    /// UTC timestamp when this connection was established.
    /// </summary>
    public DateTimeOffset ConnectedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a new state with activity recorded.
    /// </summary>
    public ConnectionHealthState WithActivityRecorded() =>
        this with { LastActivity = DateTimeOffset.UtcNow };

    /// <summary>
    /// Creates a new state with ping recorded.
    /// </summary>
    public ConnectionHealthState WithPingRecorded() =>
        this with
        {
            LastPingSent = DateTimeOffset.UtcNow,
            PendingPong = true,
            LastActivity = DateTimeOffset.UtcNow
        };

    /// <summary>
    /// Creates a new state with pong received.
    /// </summary>
    public ConnectionHealthState WithPongReceived() =>
        this with
        {
            PendingPong = false,
            ConsecutiveTimeouts = 0,
            LastActivity = DateTimeOffset.UtcNow
        };

    /// <summary>
    /// Creates a new state with timeout recorded.
    /// </summary>
    public ConnectionHealthState WithTimeoutRecorded() =>
        this with
        {
            ConsecutiveTimeouts = ConsecutiveTimeouts + 1,
            PendingPong = false
        };
}

/// <summary>
/// Thread-safe monitor for tracking connection health across all active connections.
/// </summary>
public sealed class ConnectionHealthMonitor
{
    private readonly ConcurrentDictionary<string, ConnectionHealthState> _healthStates = new(StringComparer.Ordinal);
    private long _totalConnectionsTracked;
    private long _totalPingsSent;
    private long _totalPongsReceived;
    private long _totalTimeoutsDetected;

    /// <summary>
    /// Registers a new connection for health monitoring.
    /// </summary>
    public bool TryRegister(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        if (!_healthStates.TryAdd(connectionId, new ConnectionHealthState()))
        {
            return false;
        }

        Interlocked.Increment(ref _totalConnectionsTracked);
        return true;
    }

    /// <summary>
    /// Unregisters a connection from health monitoring.
    /// </summary>
    public bool TryUnregister(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        return _healthStates.TryRemove(connectionId, out _);
    }

    /// <summary>
    /// Attempts to get a snapshot of the health state for a specific connection.
    /// Returns a copy to prevent external mutation.
    /// </summary>
    public bool TryGetHealthState(string connectionId, [NotNullWhen(true)] out ConnectionHealthState? state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        if (!_healthStates.TryGetValue(connectionId, out var existingState))
        {
            state = null;
            return false;
        }

        // Return a copy to prevent external mutation
        state = existingState;
        return true;
    }

    /// <summary>
    /// Records activity on a connection atomically.
    /// </summary>
    public void RecordActivity(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        _healthStates.AddOrUpdate(
            connectionId,
            _ => new ConnectionHealthState(),
            (_, existing) => existing.WithActivityRecorded());
    }

    /// <summary>
    /// Records that a ping was sent to a connection atomically.
    /// </summary>
    public void RecordPingSent(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        _healthStates.AddOrUpdate(
            connectionId,
            _ => new ConnectionHealthState { LastPingSent = DateTimeOffset.UtcNow, PendingPong = true },
            (_, existing) => existing.WithPingRecorded());

        Interlocked.Increment(ref _totalPingsSent);
    }

    /// <summary>
    /// Records that a pong was received from a connection atomically.
    /// Returns the response time if the ping timestamp was recorded.
    /// </summary>
    public TimeSpan? RecordPongReceived(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        TimeSpan? responseTime = null;
        var now = DateTimeOffset.UtcNow;

        _healthStates.AddOrUpdate(
            connectionId,
            _ => new ConnectionHealthState(),
            (_, existing) =>
            {
                if (existing.LastPingSent.HasValue)
                {
                    responseTime = now - existing.LastPingSent.Value;
                }

                return existing.WithPongReceived();
            });

        Interlocked.Increment(ref _totalPongsReceived);
        return responseTime;
    }

    /// <summary>
    /// Records a timeout detection atomically.
    /// Returns the new consecutive timeout count.
    /// </summary>
    public int RecordTimeout(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        Interlocked.Increment(ref _totalTimeoutsDetected);

        int newTimeoutCount = 0;
        _healthStates.AddOrUpdate(
            connectionId,
            _ => new ConnectionHealthState { ConsecutiveTimeouts = 1 },
            (_, existing) =>
            {
                var updated = existing.WithTimeoutRecorded();
                newTimeoutCount = updated.ConsecutiveTimeouts;
                return updated;
            });

        return newTimeoutCount;
    }

    /// <summary>
    /// Gets all connection IDs that are currently being monitored.
    /// </summary>
    public IReadOnlyCollection<string> GetMonitoredConnectionIds()
    {
        return _healthStates.Keys.ToArray();
    }

    /// <summary>
    /// Gets the total number of connections currently being monitored.
    /// </summary>
    public int MonitoredCount => _healthStates.Count;

    /// <summary>
    /// Gets the total number of connections ever tracked.
    /// </summary>
    public long TotalConnectionsTracked => Interlocked.Read(ref _totalConnectionsTracked);

    /// <summary>
    /// Gets the total number of pings sent across all connections.
    /// </summary>
    public long TotalPingsSent => Interlocked.Read(ref _totalPingsSent);

    /// <summary>
    /// Gets the total number of pongs received across all connections.
    /// </summary>
    public long TotalPongsReceived => Interlocked.Read(ref _totalPongsReceived);

    /// <summary>
    /// Gets the total number of timeouts detected across all connections.
    /// </summary>
    public long TotalTimeoutsDetected => Interlocked.Read(ref _totalTimeoutsDetected);

    /// <summary>
    /// Gets all health states for snapshot purposes.
    /// Returns a copy to prevent external mutation.
    /// </summary>
    public IReadOnlyDictionary<string, ConnectionHealthState> GetAllHealthStates()
    {
        return _healthStates.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value,
            StringComparer.Ordinal);
    }
}
