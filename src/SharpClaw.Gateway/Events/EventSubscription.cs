using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.Gateway.Events;

/// <summary>
/// Represents a subscription to an event topic with lifecycle management.
/// </summary>
public sealed class EventSubscription : IAsyncDisposable
{
    private readonly string _subscriptionId;
    private readonly string _topic;
    private readonly Channel<EventFrame> _buffer;
    private readonly CancellationTokenSource _cts;
    private readonly ILogger<EventSubscription> _logger;
    private long _eventsReceived;
    private long _eventsDropped;
    private bool _disposed;

    /// <summary>
    /// Unique identifier for this subscription.
    /// </summary>
    public string SubscriptionId => _subscriptionId;

    /// <summary>
    /// The topic this subscription is listening to.
    /// </summary>
    public string Topic => _topic;

    /// <summary>
    /// Whether the subscription is still active.
    /// </summary>
    public bool IsActive => !_disposed && !_cts.IsCancellationRequested;

    /// <summary>
    /// Total number of events received by this subscription.
    /// </summary>
    public long EventsReceived => Interlocked.Read(ref _eventsReceived);

    /// <summary>
    /// Total number of events dropped due to buffer overflow.
    /// </summary>
    public long EventsDropped => Interlocked.Read(ref _eventsDropped);

    /// <summary>
    /// Current number of buffered events waiting to be read.
    /// </summary>
    public int BufferedEventCount => _buffer.Reader.Count;

    /// <summary>
    /// Creates a new event subscription.
    /// </summary>
    public EventSubscription(
        string topic,
        int bufferCapacity = 100,
        ILogger<EventSubscription>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

        if (bufferCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferCapacity), "Buffer capacity must be positive.");

        _subscriptionId = Guid.NewGuid().ToString("N")[..8];
        _topic = topic;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EventSubscription>.Instance;
        _cts = new CancellationTokenSource();

        var options = new BoundedChannelOptions(bufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };

        _buffer = Channel.CreateBounded<EventFrame>(options);

        _logger.LogDebug("Created subscription {SubscriptionId} for topic '{Topic}' with buffer capacity {Capacity}",
            _subscriptionId, topic, bufferCapacity);
    }

    /// <summary>
    /// Gets the cancellation token for this subscription.
    /// </summary>
    public CancellationToken CancellationToken => _cts.Token;

    /// <summary>
    /// Attempts to write an event to the subscription buffer.
    /// </summary>
    /// <param name="eventFrame">The event to buffer.</param>
    /// <returns>True if written successfully, false if buffer is full.</returns>
    public bool TryWriteEvent(EventFrame eventFrame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_cts.IsCancellationRequested)
            return false;

        if (_buffer.Writer.TryWrite(eventFrame))
        {
            Interlocked.Increment(ref _eventsReceived);
            return true;
        }

        Interlocked.Increment(ref _eventsDropped);
        _logger.LogWarning("Dropped event {EventType} for subscription {SubscriptionId}: buffer full",
            eventFrame.Event, _subscriptionId);

        return false;
    }

    /// <summary>
    /// Asynchronously writes an event to the subscription buffer.
    /// </summary>
    public async ValueTask<bool> WriteEventAsync(EventFrame eventFrame, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_cts.IsCancellationRequested)
            return false;

        try
        {
            await _buffer.Writer.WriteAsync(eventFrame, ct).ConfigureAwait(false);
            Interlocked.Increment(ref _eventsReceived);
            return true;
        }
        catch (ChannelClosedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads events from the subscription buffer.
    /// </summary>
    public IAsyncEnumerable<EventFrame> ReadAllAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
        return _buffer.Reader.ReadAllAsync(linkedCts.Token);
    }

    /// <summary>
    /// Attempts to read an event from the buffer without blocking.
    /// </summary>
    public bool TryReadEvent([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out EventFrame? eventFrame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_buffer.Reader.TryRead(out var evt))
        {
            eventFrame = evt;
            return true;
        }

        eventFrame = null;
        return false;
    }

    /// <summary>
    /// Signals cancellation for this subscription.
    /// </summary>
    public void Cancel()
    {
        if (_disposed)
        {
            return;
        }

        _cts.Cancel();
        _buffer.Writer.TryComplete();

        _logger.LogDebug("Cancelled subscription {SubscriptionId} for topic '{Topic}'", _subscriptionId, _topic);
    }

    /// <summary>
    /// Disposes the subscription and releases all resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Cancel();
        _cts.Dispose();

        // Drain any remaining events to prevent memory leaks
        while (_buffer.Reader.TryRead(out _))
        {
        }

        _logger.LogDebug("Disposed subscription {SubscriptionId} for topic '{Topic}' (received: {Received}, dropped: {Dropped})",
            _subscriptionId, _topic, _eventsReceived, _eventsDropped);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a snapshot of the subscription's current statistics.
    /// </summary>
    public EventSubscriptionStats GetStats()
    {
        return new EventSubscriptionStats(
            _subscriptionId,
            _topic,
            IsActive,
            EventsReceived,
            EventsDropped,
            _buffer.Reader.Count);
    }
}

/// <summary>
/// Immutable snapshot of subscription statistics.
/// </summary>
public sealed record EventSubscriptionStats(
    string SubscriptionId,
    string Topic,
    bool IsActive,
    long EventsReceived,
    long EventsDropped,
    int BufferedEvents);

/// <summary>
/// Manages multiple event subscriptions with automatic cleanup.
/// </summary>
public sealed class EventSubscriptionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, EventSubscription> _subscriptions = new(StringComparer.Ordinal);
    private readonly ILogger<EventSubscriptionManager> _logger;
    private bool _disposed;

    public EventSubscriptionManager(ILogger<EventSubscriptionManager>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EventSubscriptionManager>.Instance;
    }

    /// <summary>
    /// Creates a new subscription for the specified topic.
    /// </summary>
    public EventSubscription CreateSubscription(string topic, int bufferCapacity = 100)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var subscription = new EventSubscription(topic, bufferCapacity);

        if (!_subscriptions.TryAdd(subscription.SubscriptionId, subscription))
        {
            throw new InvalidOperationException($"Failed to add subscription {subscription.SubscriptionId}.");
        }

        _logger.LogDebug("Added subscription {SubscriptionId} for topic '{Topic}' to manager",
            subscription.SubscriptionId, topic);

        return subscription;
    }

    /// <summary>
    /// Attempts to get a subscription by ID.
    /// </summary>
    public bool TryGetSubscription(string subscriptionId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out EventSubscription? subscription)
    {
        return _subscriptions.TryGetValue(subscriptionId, out subscription);
    }

    /// <summary>
    /// Removes and disposes a subscription.
    /// </summary>
    public async ValueTask<bool> RemoveSubscriptionAsync(string subscriptionId)
    {
        if (!_subscriptions.TryRemove(subscriptionId, out var subscription))
            return false;

        await subscription.DisposeAsync().ConfigureAwait(false);

        _logger.LogDebug("Removed subscription {SubscriptionId} from manager", subscriptionId);
        return true;
    }

    /// <summary>
    /// Gets all active subscription IDs.
    /// </summary>
    public IReadOnlyList<string> GetActiveSubscriptionIds()
    {
        return _subscriptions.Keys.ToList();
    }

    /// <summary>
    /// Gets the count of active subscriptions.
    /// </summary>
    public int ActiveSubscriptionCount => _subscriptions.Count;

    /// <summary>
    /// Gets statistics for all subscriptions.
    /// </summary>
    public IReadOnlyList<EventSubscriptionStats> GetAllStats()
    {
        return _subscriptions.Values.Select(s => s.GetStats()).ToList();
    }

    /// <summary>
    /// Cleans up completed or cancelled subscriptions.
    /// </summary>
    public async Task<int> CleanupInactiveSubscriptionsAsync()
    {
        var cleaned = 0;

        foreach (var (id, subscription) in _subscriptions)
        {
            if (subscription.IsActive)
                continue;

            if (_subscriptions.TryRemove(id, out var removed))
            {
                await removed.DisposeAsync().ConfigureAwait(false);
                cleaned++;
            }
        }

        if (cleaned > 0)
        {
            _logger.LogInformation("Cleaned up {Count} inactive subscriptions", cleaned);
        }

        return cleaned;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var (_, subscription) in _subscriptions)
        {
            subscription.Cancel();
        }

        _subscriptions.Clear();

        _logger.LogInformation("Disposed subscription manager with {Count} subscriptions", _subscriptions.Count);
    }
}
