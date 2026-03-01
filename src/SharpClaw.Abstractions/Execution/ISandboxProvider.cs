using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClaw.Abstractions.Execution;

/// <summary>
/// Represents a handle to an active sandbox.
/// </summary>
/// <param name="Provider">The provider name.</param>
/// <param name="SandboxId">The unique sandbox identifier.</param>
public sealed record SandboxHandle(string Provider, string SandboxId);

/// <summary>
/// Configuration for starting a sandbox.
/// </summary>
public sealed record SandboxStartRequest
(
    string Image,
    string RunId,
    string DeviceId,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null,
    IReadOnlyList<string>? Mounts = null,
    long? CpuLimit = null,
    long? MemoryLimit = null,
    int? ProcessLimit = null,
    IsolationLevel IsolationLevel = IsolationLevel.Standard
);

/// <summary>
/// Defines the isolation level for sandbox execution.
/// </summary>
public enum IsolationLevel
{
    /// <summary>
    /// Standard isolation with network access.
    /// </summary>
    Standard,

    /// <summary>
    /// Strict isolation with no network access.
    /// </summary>
    Strict
}

/// <summary>
/// Provides sandbox execution capabilities.
/// </summary>
public interface ISandboxProvider
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Starts a sandbox with the specified configuration.
    /// </summary>
    /// <param name="request">The start request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A handle to the started sandbox.</returns>
    Task<SandboxHandle> StartAsync(SandboxStartRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a running sandbox.
    /// </summary>
    /// <param name="handle">The sandbox handle.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default);
}
