// <copyright file="AgentRuntimePipeline.cs" company="SharpClaw">
// Licensed under the MIT License. See LICENSE file.
// </copyright>

using SharpClaw.Abstractions;

namespace SharpClaw.Runs;

/// <summary>
/// Represents a request to execute a run through the pipeline.
/// </summary>
/// <param name="RunId">Unique identifier for the run.</param>
/// <param name="Input">The user input to process.</param>
/// <param name="OnEvent">Callback for streaming agent events.</param>
public sealed record RunRequest(string RunId, string Input, Func<string, string?, Task>? OnEvent = null);

/// <summary>
/// Represents the result of a pipeline run.
/// </summary>
/// <param name="RunId">Unique identifier for the run.</param>
/// <param name="Result">The operation result.</param>
public sealed record RunResult(string RunId, OperationResult Result);

/// <summary>
/// Middleware delegate for the agent runtime pipeline.
/// </summary>
public delegate Task<RunResult> RunMiddleware(RunRequest request, CancellationToken cancellationToken, RunExecutionDelegate next);

/// <summary>
/// Delegate for executing a run.
/// </summary>
public delegate Task<RunResult> RunExecutionDelegate(RunRequest request, CancellationToken cancellationToken);

/// <summary>
/// Pipeline for executing agent runs with middleware support.
/// </summary>
public sealed class AgentRuntimePipeline
{
    private readonly List<RunMiddleware> _middlewares = [];

    /// <summary>
    /// Adds middleware to the pipeline.
    /// </summary>
    /// <param name="middleware">The middleware to add.</param>
    /// <returns>The pipeline for chaining.</returns>
    public AgentRuntimePipeline Use(RunMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>
    /// Executes the pipeline with the given request.
    /// </summary>
    /// <param name="request">The run request.</param>
    /// <param name="terminal">The terminal handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run result.</returns>
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

/// <summary>
/// Adapter interface for agent runtime implementations.
/// </summary>
public interface IAgentRuntimeAdapter
{
    /// <summary>
    /// Executes the agent runtime with the given request.
    /// </summary>
    /// <param name="request">The run request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run result.</returns>
    Task<RunResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service that executes runs through the agent runtime pipeline.
/// Implements both the pipeline-based execution and the IRunExecutionService interface.
/// </summary>
public sealed class RunExecutionService : IRunExecutionService
{
    private readonly AgentRuntimePipeline _pipeline;
    private readonly IAgentRuntimeAdapter _adapter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunExecutionService"/> class.
    /// </summary>
    /// <param name="pipeline">The agent runtime pipeline.</param>
    /// <param name="adapter">The agent runtime adapter.</param>
    public RunExecutionService(AgentRuntimePipeline pipeline, IAgentRuntimeAdapter adapter)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    /// <summary>
    /// Executes a run using the pipeline.
    /// </summary>
    /// <param name="request">The run request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run result.</returns>
    public Task<RunResult> ExecutePipelineAsync(RunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _pipeline.ExecuteAsync(request, _adapter.ExecuteAsync, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<RunExecutionResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await ExecutePipelineAsync(request, cancellationToken).ConfigureAwait(false);
            return new RunExecutionResult(result.RunId, result.Result);
        }
        catch (OperationCanceledException)
        {
            return new RunExecutionResult(request.RunId, OperationResult.Failure("Execution was cancelled"));
        }
        catch (Exception ex)
        {
            return new RunExecutionResult(request.RunId, OperationResult.Failure(ex.Message));
        }
    }
}
