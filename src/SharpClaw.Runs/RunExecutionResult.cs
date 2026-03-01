// <copyright file="RunExecutionResult.cs" company="SharpClaw">
// Licensed under the MIT License. See LICENSE file.
// </copyright>

using SharpClaw.Abstractions;

namespace SharpClaw.Runs;

/// <summary>
/// Represents the result of a run execution.
/// </summary>
/// <param name="RunId">Unique identifier for the run.</param>
/// <param name="Result">The operation result.</param>
public sealed record RunExecutionResult(string RunId, OperationResult Result);
