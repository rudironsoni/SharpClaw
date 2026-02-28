using SharpClaw.Abstractions;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace SharpClaw.Runs;

public sealed record RunRequest(string RunId, string Input);

public sealed record RunResult(string RunId, OperationResult Result);

public delegate Task<RunResult> RunMiddleware(RunRequest request, CancellationToken cancellationToken, RunExecutionDelegate next);

public delegate Task<RunResult> RunExecutionDelegate(RunRequest request, CancellationToken cancellationToken);

public sealed class AgentRuntimePipeline
{
    private readonly List<RunMiddleware> _middlewares = [];

    public AgentRuntimePipeline Use(RunMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _middlewares.Add(middleware);
        return this;
    }

    public Task<RunResult> ExecuteAsync(
        RunRequest request,
        RunExecutionDelegate terminal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(terminal);

        RunExecutionDelegate next = terminal;

        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var captured = next;
            next = (req, ct) => middleware(req, ct, captured);
        }

        return next(request, cancellationToken);
    }
}

public interface IAgentRuntimeAdapter
{
    Task<RunResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default);
}

public sealed class RunExecutionService(AgentRuntimePipeline pipeline, IAgentRuntimeAdapter adapter)
{
    private readonly AgentRuntimePipeline _pipeline = pipeline;
    private readonly IAgentRuntimeAdapter _adapter = adapter;

    public Task<RunResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _pipeline.ExecuteAsync(request, _adapter.ExecuteAsync, cancellationToken);
    }
}

public sealed record RunStartResult(string RunId, string Status);

public sealed record RunEvent(long Seq, string RunId, string Event, string? Data = null);

public sealed record RunSnapshot(string RunId, string Status, string? LastError = null);

public sealed class RunCoordinator
{
    private sealed class RunState
    {
        public required string RunId { get; init; }
        public required string Status { get; set; }
        public string? LastError { get; set; }
        public CancellationTokenSource Cancellation { get; } = new();
        public Channel<RunEvent> Events { get; } = Channel.CreateUnbounded<RunEvent>();
    }

    private readonly ConcurrentDictionary<string, RunState> _runs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _idempotencyMap = new(StringComparer.Ordinal);
    private long _eventSeq;

    public async Task<RunStartResult> StartAsync(
        string input,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!string.IsNullOrWhiteSpace(idempotencyKey)
            && _idempotencyMap.TryGetValue(idempotencyKey, out var existingRunId)
            && _runs.TryGetValue(existingRunId, out var existingState)
            && (existingState.Status is "started" or "running"))
        {
            return new RunStartResult(existingRunId, "in_flight");
        }

        var runId = $"run-{Guid.NewGuid():N}";
        var state = new RunState
        {
            RunId = runId,
            Status = "started"
        };

        _runs[runId] = state;

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _idempotencyMap[idempotencyKey] = runId;
        }

        await PublishAsync(state, "run.started", input, cancellationToken).ConfigureAwait(false);
        _ = ExecuteRunAsync(state);
        return new RunStartResult(runId, "started");
    }

    public async Task<RunSnapshot> AbortAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        if (!_runs.TryGetValue(runId, out var state))
        {
            return new RunSnapshot(runId, "not_found");
        }

        if (state.Status is "completed" or "failed")
        {
            return new RunSnapshot(runId, state.Status, state.LastError);
        }

        if (state.Status == "aborted")
        {
            return new RunSnapshot(runId, state.Status, state.LastError);
        }

        state.Cancellation.Cancel();
        state.Status = "aborted";
        state.LastError = "aborted-by-operator";

        try
        {
            await PublishAsync(state, "run.aborted", null, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            return new RunSnapshot(runId, state.Status, state.LastError);
        }

        state.Events.Writer.TryComplete();

        return new RunSnapshot(runId, state.Status, state.LastError);
    }

    public RunSnapshot GetSnapshot(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        if (!_runs.TryGetValue(runId, out var state))
        {
            return new RunSnapshot(runId, "not_found");
        }

        return new RunSnapshot(runId, state.Status, state.LastError);
    }

    public async IAsyncEnumerable<RunEvent> ReadEventsAsync(
        string runId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        if (!_runs.TryGetValue(runId, out var state))
        {
            yield break;
        }

        while (await state.Events.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (state.Events.Reader.TryRead(out var evt))
            {
                yield return evt;
            }
        }
    }

    private async Task ExecuteRunAsync(RunState state)
    {
        try
        {
            state.Status = "running";
            await PublishAsync(state, "run.running", null, CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(25, state.Cancellation.Token).ConfigureAwait(false);
            await PublishAsync(state, "run.delta", "delta-1", CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(25, state.Cancellation.Token).ConfigureAwait(false);
            await PublishAsync(state, "run.delta", "delta-2", CancellationToken.None).ConfigureAwait(false);

            state.Cancellation.Token.ThrowIfCancellationRequested();

            state.Status = "completed";
            await PublishAsync(state, "run.completed", null, CancellationToken.None).ConfigureAwait(false);
            state.Events.Writer.TryComplete();
        }
        catch (OperationCanceledException)
        {
            if (state.Status != "aborted")
            {
                state.Status = "aborted";
                state.LastError = "aborted";
                await PublishAsync(state, "run.aborted", null, CancellationToken.None).ConfigureAwait(false);
                state.Events.Writer.TryComplete();
            }
        }
        catch (Exception ex)
        {
            state.Status = "failed";
            state.LastError = ex.Message;
            await PublishAsync(state, "run.failed", ex.Message, CancellationToken.None).ConfigureAwait(false);
            state.Events.Writer.TryComplete(ex);
        }
    }

    private ValueTask PublishAsync(RunState state, string evt, string? data, CancellationToken cancellationToken)
    {
        var seq = Interlocked.Increment(ref _eventSeq);
        return state.Events.Writer.WriteAsync(new RunEvent(seq, state.RunId, evt, data), cancellationToken);
    }
}
