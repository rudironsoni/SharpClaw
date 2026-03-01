// <copyright file="IRunExecutionService.cs" company="SharpClaw">
// Licensed under the MIT License. See LICENSE file.
// </copyright>

namespace SharpClaw.Runs;

/// <summary>
/// Service that executes agent runs using the Microsoft Agent Framework runtime.
/// </summary>
public interface IRunExecutionService
{
    /// <summary>
    /// Executes a run with the given request, streaming events through the callback.
    /// </summary>
    /// <param name="request">The run request containing input and event callback.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The execution result indicating success or failure.</returns>
    Task<RunExecutionResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default);
}
