using System.Threading.Channels;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.Gateway.Events;

/// <summary>
/// Abstraction for topic-based event publishing and subscription.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event to the specified topic.
    /// </summary>
    /// <param name="topic">The topic to publish to. Must be non-empty.</param>
    /// <param name="eventFrame">The event to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when topic is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when topic channel is closed.</exception>
    Task PublishAsync(string topic, EventFrame eventFrame, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to events on the specified topic.
    /// </summary>
    /// <param name="topic">The topic to subscribe to. Must be non-empty.</param>
    /// <param name="ct">Cancellation token for unsubscribing.</param>
    /// <returns>An async enumerable of event frames.</returns>
    /// <exception cref="ArgumentException">Thrown when topic is null or empty.</exception>
    IAsyncEnumerable<EventFrame> SubscribeAsync(string topic, CancellationToken ct = default);

    /// <summary>
    /// Gets the number of active subscribers for a topic.
    /// </summary>
    /// <param name="topic">The topic to query.</param>
    /// <returns>Subscriber count, or 0 if topic doesn't exist.</returns>
    int GetSubscriberCount(string topic);

    /// <summary>
    /// Gets a snapshot of all currently active topics.
    /// </summary>
    /// <returns>Read-only list of active topic names.</returns>
    IReadOnlyList<string> GetActiveTopics();

    /// <summary>
    /// Attempts to clean up idle topics with no subscribers.
    /// </summary>
    /// <returns>Number of topics cleaned up.</returns>
    int CleanupIdleTopics();
}

/// <summary>
/// Configuration options for the event publisher.
/// </summary>
public sealed record EventPublisherOptions
{
    /// <summary>
    /// Maximum number of events to buffer per topic before applying backpressure.
    /// </summary>
    public int BoundedCapacity { get; init; } = 1000;

    /// <summary>
    /// Mode to use when the channel reaches capacity.
    /// </summary>
    public BoundedChannelFullMode FullMode { get; init; } = BoundedChannelFullMode.DropOldest;

    /// <summary>
    /// Whether to enable single-reader optimization.
    /// </summary>
    public bool SingleReader { get; init; } = false;

    /// <summary>
    /// Whether to enable single-writer optimization.
    /// </summary>
    public bool SingleWriter { get; init; } = false;

    /// <summary>
    /// Default options with bounded capacity of 1000 and DropOldest backpressure.
    /// </summary>
    public static EventPublisherOptions Default => new();
}
