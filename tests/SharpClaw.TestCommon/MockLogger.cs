using Microsoft.Extensions.Logging;

namespace SharpClaw.TestCommon;

/// <summary>
/// A mock logger that captures log entries for verification in tests.
/// </summary>
public class MockLogger<T> : ILogger<T>, IDisposable
{
    private readonly List<LogEntry> _logs = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<LogEntry> Logs
    {
        get
        {
            lock (_lock)
            {
                return _logs.ToList();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => this;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var entry = new LogEntry
        {
            LogLevel = logLevel,
            EventId = eventId,
            Message = formatter(state, exception),
            Exception = exception
        };

        lock (_lock)
        {
            _logs.Add(entry);
        }
    }

    public void Dispose() { }

    public bool ContainsLog(LogLevel level, string partialMessage)
    {
        lock (_lock)
        {
            return _logs.Any(l => l.LogLevel == level && l.Message.Contains(partialMessage, StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool ContainsLog(string partialMessage)
    {
        lock (_lock)
        {
            return _logs.Any(l => l.Message.Contains(partialMessage, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _logs.Clear();
        }
    }
}

public sealed class LogEntry
{
    public LogLevel LogLevel { get; set; }
    public EventId EventId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}
