using System.Collections.Concurrent;
using System.Threading.Channels;
using SharpClaw.Gateway.Events;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.TestCommon;

/// <summary>
/// A fake event publisher for testing that captures and replays events.
/// </summary>
public class FakeEventPublisher : IEventPublisher, IDisposable
{
    private readonly ConcurrentDictionary<string, Channel<EventFrame>> _topics = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<PublishedEvent> _publishedEvents = [];
    private readonly Lock _lock = new();
    private bool _disposed;

    public Task PublishAsync(string topic, EventFrame eventFrame, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = _topics.GetOrAdd(topic, _ => Channel.CreateUnbounded<EventFrame>());
        _publishedEvents.Add(new PublishedEvent(topic, eventFrame, DateTimeOffset.UtcNow));

        return channel.Writer.WriteAsync(eventFrame, cancellationToken).AsTask();
    }

    public IAsyncEnumerable<EventFrame> SubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = _topics.GetOrAdd(topic, _ => Channel.CreateUnbounded<EventFrame>());
        return channel.Reader.ReadAllAsync(cancellationToken);
    }

    public int GetSubscriberCount(string topic)
    {
        return _topics.TryGetValue(topic, out var channel) && channel.Reader.Count > 0 ? 1 : 0;
    }

    public IReadOnlyList<string> GetActiveTopics()
    {
        return _topics.Keys.ToList();
    }

    public int CleanupIdleTopics()
    {
        var cleaned = 0;

        foreach (var (topic, channel) in _topics)
        {
            if (channel.Reader.Count == 0)
            {
                _topics.TryRemove(topic, out _);
                cleaned++;
            }
        }

        return cleaned;
    }

    public IReadOnlyList<PublishedEvent> GetPublishedEvents(string? topic = null)
    {
        var events = _publishedEvents.ToList();
        return topic is null ? events : events.Where(e => e.Topic == topic).ToList();
    }

    public bool HasTopic(string topic) => _topics.ContainsKey(topic);

    public void Clear()
    {
        _publishedEvents.Clear();

        foreach (var (_, channel) in _topics)
        {
            channel.Writer.Complete();
        }

        _topics.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();
    }
}

public sealed record PublishedEvent(string Topic, EventFrame EventFrame, DateTimeOffset PublishedAt);
