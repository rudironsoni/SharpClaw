using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.Execution.Abstractions;

namespace SharpClaw.Runs;

public sealed record RunStartResult(string RunId, string Status);

public sealed record RunEvent(long Seq, string RunId, string Event, string? Data = null);

public sealed record RunSnapshot(string RunId, string TenantId, string Status, string? LastError = null);

public sealed class RunCoordinator(SandboxManagerService sandboxManager, RunExecutionService runExecutionService)
{
    private readonly SandboxManagerService _sandboxManager = sandboxManager ?? throw new ArgumentNullException(nameof(sandboxManager));
    private readonly RunExecutionService _runExecutionService = runExecutionService ?? throw new ArgumentNullException(nameof(runExecutionService));

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
        string tenantId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

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
        _ = ExecuteRunAsync(state, input);
        return new RunStartResult(runId, "started");
    }

    public async Task<RunSnapshot> AbortAsync(string runId, string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (!_runs.TryGetValue(runId, out var state))
        {
            return new RunSnapshot(runId, tenantId, "not_found");
        }

        if (state.Status is "completed" or "failed")
        {
            return new RunSnapshot(runId, tenantId, state.Status, state.LastError);
        }

        if (state.Status == "aborted")
        {
            return new RunSnapshot(runId, tenantId, state.Status, state.LastError);
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
            return new RunSnapshot(runId, tenantId, state.Status, state.LastError);
        }

        state.Events.Writer.TryComplete();

        return new RunSnapshot(runId, tenantId, state.Status, state.LastError);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Interface contract")]
    public Task<RunSnapshot> GetSnapshotAsync(string runId, string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (!_runs.TryGetValue(runId, out var state))
        {
            return Task.FromResult(new RunSnapshot(runId, tenantId, "not_found"));
        }

        return Task.FromResult(new RunSnapshot(runId, tenantId, state.Status, state.LastError));
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

    private async Task ExecuteRunAsync(RunState state, string input)
    {
        SandboxHandle? sandbox = null;
        try
        {
            state.Status = "running";
            await PublishAsync(state, "run.running", null, CancellationToken.None).ConfigureAwait(false);

            // 1. Start sandbox
            sandbox = await _sandboxManager.StartSandboxAsync(new SandboxStartRequest(state.RunId), state.Cancellation.Token).ConfigureAwait(false);

            // 2. Map pipeline events
            var request = new RunRequest(state.RunId, input, async (evt, data) => 
            {
                await PublishAsync(state, evt, data, CancellationToken.None).ConfigureAwait(false);
            });

            // 3. Execute
            var result = await _runExecutionService.ExecutePipelineAsync(request, state.Cancellation.Token).ConfigureAwait(false);

            state.Cancellation.Token.ThrowIfCancellationRequested();

            state.Status = "completed";
            await PublishAsync(state, "run.completed", result.Result.Succeeded.ToString(), CancellationToken.None).ConfigureAwait(false);
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
        finally
        {
            if (sandbox != null)
            {
                await _sandboxManager.StopSandboxAsync(state.RunId, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private ValueTask PublishAsync(RunState state, string evt, string? data, CancellationToken cancellationToken)
    {
        var seq = Interlocked.Increment(ref _eventSeq);
        return state.Events.Writer.WriteAsync(new RunEvent(seq, state.RunId, evt, data), cancellationToken);
    }
}
