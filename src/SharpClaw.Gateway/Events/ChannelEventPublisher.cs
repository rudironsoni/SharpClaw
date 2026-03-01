using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.Gateway.Events;

/// <summary>
/// Thread-safe event publisher using System.Threading.Channels for pub/sub.
/// Implements bounded channels with backpressure and automatic cleanup.
/// </summary>
public sealed class ChannelEventPublisher : IEventPublisher, IDisposable
{
    private readonly ConcurrentDictionary<string, TopicState> _topics = new(StringComparer.Ordinal);
    private long _globalSequence;
    private readonly EventPublisherOptions _options;
    private readonly ILogger<ChannelEventPublisher> _logger;
    private readonly TimeProvider _timeProvider;
    private bool _disposed;

    /// <summary>
    /// Creates a new ChannelEventPublisher with the specified options.
    /// </summary>
    public ChannelEventPublisher(
        EventPublisherOptions? options = null,
        ILogger<ChannelEventPublisher>? logger = null,
        TimeProvider? timeProvider = null)
    {
        _options = options ?? EventPublisherOptions.Default;
        _logger = logger ?? NullLogger<ChannelEventPublisher>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task PublishAsync(string topic, EventFrame eventFrame, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

        ArgumentNullException.ThrowIfNull(eventFrame);

        var topicState = GetOrCreateTopic(topic);
        var sequencedEvent = eventFrame with
        {
            Seq = Interlocked.Increment(ref _globalSequence)
        };

        try
        {
            await topicState.Channel.Writer.WriteAsync(sequencedEvent, ct).ConfigureAwait(false);
            topicState.UpdateLastActivity(_timeProvider.GetUtcNow());

            _logger.LogDebug("Published event {EventType} to topic '{Topic}' with seq {Sequence}",
                sequencedEvent.Event, topic, sequencedEvent.Seq);
        }
        catch (ChannelClosedException ex)
        {
            _logger.LogWarning(ex, "Failed to publish to topic '{Topic}': channel was closed", topic);
            CleanupTopicIfIdle(topic);
            throw new InvalidOperationException($"Topic '{topic}' channel is closed.", ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EventFrame> SubscribeAsync(
        string topic,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

        var topicState = GetOrCreateTopic(topic);
        var subscriptionId = Guid.NewGuid().ToString("N")[..8];

        topicState.IncrementSubscriberCount();
        _logger.LogDebug("Subscription {SubscriptionId} started for topic '{Topic}'", subscriptionId, topic);

        try
        {
            await foreach (var evt in topicState.Channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return evt;
            }
        }
        finally
        {
            topicState.DecrementSubscriberCount();
            _logger.LogDebug("Subscription {SubscriptionId} ended for topic '{Topic}'", subscriptionId, topic);

            if (topicState.CanCleanup())
            {
                CleanupTopicIfIdle(topic);
            }
        }
    }

    /// <inheritdoc />
    public int GetSubscriberCount(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return 0;

        return _topics.TryGetValue(topic, out var state) ? state.SubscriberCount : 0;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetActiveTopics()
    {
        return _topics.Keys.ToList();
    }

    /// <inheritdoc />
    public int CleanupIdleTopics()
    {
        var now = _timeProvider.GetUtcNow();
        var idleThreshold = TimeSpan.FromMinutes(5);
        var cleaned = 0;

        foreach (var (topic, state) in _topics)
        {
            if (!state.CanCleanup(now, idleThreshold))
                continue;

            if (_topics.TryRemove(topic, out var removedState))
            {
                removedState.Channel.Writer.TryComplete();
                cleaned++;
                _logger.LogDebug("Cleaned up idle topic '{Topic}'", topic);
            }
        }

        if (cleaned > 0)
        {
            _logger.LogInformation("Cleaned up {Count} idle topics", cleaned);
        }

        return cleaned;
    }

    /// <summary>
    /// Gets the current global sequence number (for testing/monitoring).
    /// </summary>
    public long GetCurrentSequence() => Interlocked.Read(ref _globalSequence);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var (topic, state) in _topics)
        {
            state.Channel.Writer.TryComplete();
            _logger.LogDebug("Closed channel for topic '{Topic}' during disposal", topic);
        }

        _topics.Clear();
    }

    private TopicState GetOrCreateTopic(string topic)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_topics.TryGetValue(topic, out var existing))
            return existing;

        var newState = new TopicState(CreateChannel(), _timeProvider.GetUtcNow());
        var actual = _topics.GetOrAdd(topic, newState);

        if (ReferenceEquals(actual, newState))
        {
            _logger.LogDebug("Created new topic channel for '{Topic}'", topic);
        }

        return actual;
    }

    private Channel<EventFrame> CreateChannel()
    {
        var options = new BoundedChannelOptions(_options.BoundedCapacity)
        {
            FullMode = _options.FullMode,
            SingleReader = _options.SingleReader,
            SingleWriter = _options.SingleWriter
        };

        return Channel.CreateBounded<EventFrame>(options);
    }

    private void CleanupTopicIfIdle(string topic)
    {
        if (!_topics.TryGetValue(topic, out var state))
            return;

        if (!state.CanCleanup())
            return;

        if (_topics.TryRemove(topic, out var removed))
        {
            removed.Channel.Writer.TryComplete();
            _logger.LogDebug("Cleaned up idle topic '{Topic}' after subscriber disconnect", topic);
        }
    }

    /// <summary>
    /// Internal state for a topic channel.
    /// </summary>
    private sealed class TopicState
    {
        private int _subscriberCount;
        private long _lastActivityTicks;

        public Channel<EventFrame> Channel { get; }

        public int SubscriberCount => Interlocked.CompareExchange(ref _subscriberCount, 0, 0);

        public TopicState(Channel<EventFrame> channel, DateTimeOffset createdAt)
        {
            Channel = channel;
            _lastActivityTicks = createdAt.Ticks;
        }

        public void IncrementSubscriberCount()
        {
            Interlocked.Increment(ref _subscriberCount);
        }

        public void DecrementSubscriberCount()
        {
            Interlocked.Decrement(ref _subscriberCount);
        }

        public void UpdateLastActivity(DateTimeOffset timestamp)
        {
            Interlocked.Exchange(ref _lastActivityTicks, timestamp.Ticks);
        }

        public bool CanCleanup(DateTimeOffset? now = null, TimeSpan? idleThreshold = null)
        {
            if (SubscriberCount > 0)
                return false;

            var threshold = idleThreshold ?? TimeSpan.FromMinutes(5);
            var currentTime = now ?? DateTimeOffset.UtcNow;
            var lastActivity = new DateTimeOffset(Interlocked.Read(ref _lastActivityTicks), TimeSpan.Zero);
            var inactive = currentTime - lastActivity > threshold;

            return inactive;
        }
    }
}
