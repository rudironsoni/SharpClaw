// <copyright file="DummyAgentRuntimeAdapter.cs" company="SharpClaw">
// Licensed under the MIT License. See LICENSE file.
// </copyright>

using SharpClaw.Abstractions;

namespace SharpClaw.Runs;

/// <summary>
/// A dummy adapter that provides a fallback implementation when no AI client is configured.
/// This allows the application to start and run without a real AI backend.
/// </summary>
public sealed class DummyAgentRuntimeAdapter : IAgentRuntimeAdapter
{
    /// <inheritdoc/>
    public async Task<RunResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Publish events to simulate agent activity
        if (request.OnEvent is not null)
        {
            await request.OnEvent("run.delta", "This is a dummy response. No AI client is configured.").ConfigureAwait(false);
        }

        // Simulate some async work
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);

        return new RunResult(request.RunId, OperationResult.Success());
    }
}
