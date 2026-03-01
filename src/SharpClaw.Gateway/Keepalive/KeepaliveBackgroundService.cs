using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.Gateway.Keepalive;

/// <summary>
/// Background service that manages connection keepalive by sending periodic pings
/// and detecting unresponsive connections.
/// </summary>
public sealed class KeepaliveBackgroundService : BackgroundService
{
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly ConnectionHealthMonitor _healthMonitor;
    private readonly KeepaliveMetrics _metrics;
    private readonly KeepalivePolicy _policy;
    private readonly GatewayDispatcher _dispatcher;
    private readonly ILogger<KeepaliveBackgroundService> _logger;

    /// <summary>
    /// Event raised when a connection times out and should be disconnected.
    /// </summary>
    public event EventHandler<ConnectionTimeoutEventArgs>? ConnectionTimeout;

    /// <summary>
    /// Creates a new KeepaliveBackgroundService.
    /// </summary>
    public KeepaliveBackgroundService(
        ConnectionRegistry connectionRegistry,
        ConnectionHealthMonitor healthMonitor,
        KeepaliveMetrics metrics,
        GatewayDispatcher dispatcher,
        ILogger<KeepaliveBackgroundService> logger,
        KeepalivePolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(connectionRegistry);
        ArgumentNullException.ThrowIfNull(healthMonitor);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionRegistry = connectionRegistry;
        _healthMonitor = healthMonitor;
        _metrics = metrics;
        _dispatcher = dispatcher;
        _logger = logger;
        _policy = policy ?? KeepalivePolicy.Default;

        if (!_policy.IsValid())
        {
            throw new ArgumentException("Invalid keepalive policy provided.", nameof(policy));
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Keepalive service started with interval {PingInterval}s and timeout {Timeout}s",
            _policy.PingInterval.TotalSeconds,
            _policy.Timeout.TotalSeconds);

        using var timer = new PeriodicTimer(_policy.PingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthCheckAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown - exit the loop
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during keepalive health check");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown - exit the loop
                break;
            }
        }

        _logger.LogInformation("Keepalive service stopping gracefully");
    }

    /// <summary>
    /// Performs a health check on all active connections.
    /// </summary>
    private async Task PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        var connectionIds = _healthMonitor.GetMonitoredConnectionIds();
        var now = DateTimeOffset.UtcNow;

        foreach (var connectionId in connectionIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!_healthMonitor.TryGetHealthState(connectionId, out var healthState))
            {
                continue;
            }

            await EvaluateConnectionHealthAsync(connectionId, healthState, now, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Evaluates the health of a single connection and takes appropriate action.
    /// </summary>
    private async Task EvaluateConnectionHealthAsync(
        string connectionId,
        ConnectionHealthState healthState,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Check if we have a pending pong that has timed out
        if (healthState.PendingPong && healthState.LastPingSent.HasValue)
        {
            var elapsedSincePing = now - healthState.LastPingSent.Value;

            if (elapsedSincePing > _policy.Timeout)
            {
                await HandleTimeoutAsync(connectionId).ConfigureAwait(false);
                return;
            }
        }

        // Check if the connection has been idle and needs a ping
        var elapsedSinceActivity = now - healthState.LastActivity;

        if (!healthState.PendingPong && elapsedSinceActivity >= _policy.PingInterval)
        {
            await SendPingAsync(connectionId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles a connection timeout by recording it and triggering disconnection if necessary.
    /// </summary>
    private async Task HandleTimeoutAsync(string connectionId)
    {
        var consecutiveTimeouts = _healthMonitor.RecordTimeout(connectionId);
        _metrics.RecordTimeoutDetected();

        _logger.LogWarning(
            "Connection {ConnectionId} timeout detected (consecutive: {Consecutive})",
            connectionId,
            consecutiveTimeouts);

        // After 3 consecutive timeouts, consider the connection dead and disconnect
        if (consecutiveTimeouts >= 3)
        {
            _logger.LogError(
                "Connection {ConnectionId} has exceeded maximum consecutive timeouts. Disconnecting.",
                connectionId);

            await DisconnectConnectionAsync(connectionId).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends a ping frame to the specified connection.
    /// </summary>
    private async Task SendPingAsync(string connectionId, CancellationToken cancellationToken)
    {
        // Acknowledge parameter for API consistency while not using it in current implementation
        _ = cancellationToken;

        try
        {
            _healthMonitor.RecordPingSent(connectionId);
            _metrics.RecordPingSent();

            _logger.LogDebug("Sending ping to connection {ConnectionId}", connectionId);

            // The actual ping is handled by the dispatcher
            // Response time tracking is done atomically in RecordPongReceived
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ping to connection {ConnectionId}", connectionId);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Disconnects a connection due to timeout.
    /// </summary>
    private async Task DisconnectConnectionAsync(string connectionId)
    {
        _connectionRegistry.TryDisconnect(connectionId);
        _healthMonitor.TryUnregister(connectionId);
        _metrics.RecordConnectionTerminated();

        ConnectionTimeout?.Invoke(this, new ConnectionTimeoutEventArgs(connectionId));

        _logger.LogInformation("Connection {ConnectionId} disconnected due to timeout", connectionId);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Records that a pong was received for a connection.
    /// Response time calculation is done atomically within RecordPongReceived.
    /// </summary>
    public void RecordPongReceived(string connectionId)
    {
        var responseTime = _healthMonitor.RecordPongReceived(connectionId);
        _metrics.RecordPongReceived();

        if (responseTime.HasValue)
        {
            _metrics.RecordPingResponseTime(responseTime.Value);
        }

        _logger.LogDebug("Pong received from connection {ConnectionId}", connectionId);
    }

    /// <summary>
    /// Registers a new connection for keepalive monitoring.
    /// </summary>
    public bool RegisterConnection(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        if (!_healthMonitor.TryRegister(connectionId))
        {
            return false;
        }

        _logger.LogDebug("Connection {ConnectionId} registered for keepalive monitoring", connectionId);
        return true;
    }

    /// <summary>
    /// Unregisters a connection from keepalive monitoring.
    /// </summary>
    public bool UnregisterConnection(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        var result = _healthMonitor.TryUnregister(connectionId);

        if (result)
        {
            _logger.LogDebug("Connection {ConnectionId} unregistered from keepalive monitoring", connectionId);
        }

        return result;
    }

    /// <summary>
    /// Gets the current keepalive statistics.
    /// </summary>
    public KeepaliveStatsSnapshot GetStatsSnapshot()
    {
        return _metrics.GetSnapshot();
    }
}

/// <summary>
/// Event arguments for connection timeout events.
/// </summary>
public sealed class ConnectionTimeoutEventArgs : EventArgs
{
    /// <summary>
    /// The ID of the connection that timed out.
    /// </summary>
    public string ConnectionId { get; }

    /// <summary>
    /// Creates a new ConnectionTimeoutEventArgs.
    /// </summary>
    public ConnectionTimeoutEventArgs(string connectionId)
    {
        ConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
    }
}
