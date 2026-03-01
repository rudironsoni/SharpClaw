using SharpClaw.Protocol.Contracts;

namespace SharpClaw.Gateway.Events;

/// <summary>
/// Represents a stored event with metadata for replay.
/// </summary>
public sealed record StoredEvent(
    string Topic,
    EventFrame EventFrame,
    DateTimeOffset StoredAt,
    DateTimeOffset? ExpiresAt = null);

/// <summary>
/// Query options for retrieving stored events.
/// </summary>
public sealed record EventQueryOptions
{
    /// <summary>
    /// Starting sequence number (inclusive).
    /// </summary>
    public long? FromSequence { get; init; }

    /// <summary>
    /// Ending sequence number (inclusive).
    /// </summary>
    public long? ToSequence { get; init; }

    /// <summary>
    /// Starting timestamp (inclusive).
    /// </summary>
    public DateTimeOffset? FromTimestamp { get; init; }

    /// <summary>
    /// Ending timestamp (inclusive).
    /// </summary>
    public DateTimeOffset? ToTimestamp { get; init; }

    /// <summary>
    /// Maximum number of events to return.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Whether to return events in descending order (newest first).
    /// </summary>
    public bool Descending { get; init; } = false;

    /// <summary>
    /// Validates that at least one filter criteria is specified.
    /// </summary>
    public bool HasValidFilters()
    {
        return FromSequence.HasValue
            || ToSequence.HasValue
            || FromTimestamp.HasValue
            || ToTimestamp.HasValue;
    }
}

/// <summary>
/// Abstraction for persistent event storage supporting replay capabilities.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Stores an event for later replay.
    /// </summary>
    /// <param name="storedEvent">The event to store.</param>
    /// <param name="ct">Cancellation token.</param>
    Task StoreAsync(StoredEvent storedEvent, CancellationToken ct = default);

    /// <summary>
    /// Retrieves events matching the query options.
    /// </summary>
    /// <param name="topic">The topic to query.</param>
    /// <param name="options">Query filtering options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Matching events in the specified order.</returns>
    IAsyncEnumerable<StoredEvent> QueryAsync(
        string topic,
        EventQueryOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the latest sequence number for a topic.
    /// </summary>
    /// <param name="topic">The topic to query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The latest sequence number, or null if no events exist.</returns>
    Task<long?> GetLatestSequenceAsync(string topic, CancellationToken ct = default);

    /// <summary>
    /// Prunes expired events from storage.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of events pruned.</returns>
    Task<int> PruneExpiredAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes all events for a specific topic.
    /// </summary>
    /// <param name="topic">The topic to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of events deleted.</returns>
    Task<int> DeleteTopicAsync(string topic, CancellationToken ct = default);
}

/// <summary>
/// In-memory implementation of IEventStore for development and testing.
/// Not suitable for production use due to memory constraints.
/// </summary>
public sealed class InMemoryEventStore : IEventStore, IDisposable
{
    private readonly Dictionary<string, List<StoredEvent>> _topics = new(StringComparer.Ordinal);
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly int _maxEventsPerTopic;
    private bool _disposed;

    /// <summary>
    /// Creates a new InMemoryEventStore.
    /// </summary>
    /// <param name="maxEventsPerTopic">Maximum events to retain per topic before dropping oldest.</param>
    public InMemoryEventStore(int maxEventsPerTopic = 10000)
    {
        if (maxEventsPerTopic <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEventsPerTopic), "Must be positive.");

        _maxEventsPerTopic = maxEventsPerTopic;
    }

    /// <inheritdoc />
    public Task StoreAsync(StoredEvent storedEvent, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(storedEvent.Topic))
            throw new ArgumentException("Topic cannot be empty.", nameof(storedEvent));

        _lock.EnterWriteLock();
        try
        {
            if (!_topics.TryGetValue(storedEvent.Topic, out var events))
            {
                events = [];
                _topics[storedEvent.Topic] = events;
            }

            events.Add(storedEvent);

            // Enforce retention limit
            if (events.Count > _maxEventsPerTopic)
            {
                events.RemoveRange(0, events.Count - _maxEventsPerTopic);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StoredEvent> QueryAsync(
        string topic,
        EventQueryOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic cannot be empty.", nameof(topic));

        if (!options.HasValidFilters())
            throw new ArgumentException("At least one filter criteria must be specified.", nameof(options));

        List<StoredEvent> results;

        _lock.EnterReadLock();
        try
        {
            if (!_topics.TryGetValue(topic, out var events))
            {
                yield break;
            }

            var query = events.AsEnumerable();

            if (options.FromSequence.HasValue)
                query = query.Where(e => e.EventFrame.Seq >= options.FromSequence.Value);

            if (options.ToSequence.HasValue)
                query = query.Where(e => e.EventFrame.Seq <= options.ToSequence.Value);

            if (options.FromTimestamp.HasValue)
                query = query.Where(e => e.StoredAt >= options.FromTimestamp.Value);

            if (options.ToTimestamp.HasValue)
                query = query.Where(e => e.StoredAt <= options.ToTimestamp.Value);

            query = options.Descending
                ? query.OrderByDescending(e => e.EventFrame.Seq)
                : query.OrderBy(e => e.EventFrame.Seq);

            if (options.Limit.HasValue)
                query = query.Take(options.Limit.Value);

            results = query.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        foreach (var evt in results)
        {
            if (ct.IsCancellationRequested)
                yield break;

            yield return evt;

            // Yield to prevent blocking
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public Task<long?> GetLatestSequenceAsync(string topic, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(topic))
            return Task.FromResult<long?>(null);

        _lock.EnterReadLock();
        try
        {
            if (!_topics.TryGetValue(topic, out var events) || events.Count == 0)
                return Task.FromResult<long?>(null);

            var latest = events
                .Select(e => e.EventFrame.Seq)
                .Where(s => s.HasValue)
                .Max();

            return Task.FromResult(latest);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public Task<int> PruneExpiredAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var now = DateTimeOffset.UtcNow;
        var pruned = 0;

        _lock.EnterWriteLock();
        try
        {
            foreach (var (_, events) in _topics)
            {
                var expiredCount = events.RemoveAll(e => e.ExpiresAt.HasValue && e.ExpiresAt.Value <= now);
                pruned += expiredCount;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.FromResult(pruned);
    }

    /// <inheritdoc />
    public Task<int> DeleteTopicAsync(string topic, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(topic))
            return Task.FromResult(0);

        _lock.EnterWriteLock();
        try
        {
            if (_topics.Remove(topic, out var events))
                return Task.FromResult(events.Count);

            return Task.FromResult(0);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets all topics currently stored.
    /// </summary>
    public IReadOnlyList<string> GetTopics()
    {
        _lock.EnterReadLock();
        try
        {
            return _topics.Keys.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets the total event count across all topics.
    /// </summary>
    public int GetTotalEventCount()
    {
        _lock.EnterReadLock();
        try
        {
            return _topics.Values.Sum(e => e.Count);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _lock.EnterWriteLock();
        try
        {
            _topics.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        _lock.Dispose();
    }
}

/// <summary>
/// Extensions for integrating event storage with the publisher.
/// </summary>
public static class EventStoreExtensions
{
    /// <summary>
    /// Replays events from storage into the publisher.
    /// </summary>
    public static async Task ReplayAsync(
        this IEventStore store,
        IEventPublisher publisher,
        string topic,
        EventQueryOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(publisher);

        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic cannot be empty.", nameof(topic));

        await foreach (var stored in store.QueryAsync(topic, options, ct).ConfigureAwait(false))
        {
            await publisher.PublishAsync(topic, stored.EventFrame, ct).ConfigureAwait(false);
        }
    }
}
