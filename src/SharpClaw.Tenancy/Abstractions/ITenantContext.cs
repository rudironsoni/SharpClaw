using System;

namespace SharpClaw.Tenancy.Abstractions;

/// <summary>
/// Represents the current tenant context for the executing operation.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the unique identifier of the current tenant.
    /// </summary>
    string TenantId { get; }

    /// <summary>
    /// Gets the display name of the current tenant.
    /// </summary>
    string TenantName { get; }

    /// <summary>
    /// Gets a value indicating whether the tenant context is valid.
    /// </summary>
    bool IsValid { get; }
}

/// <summary>
/// Represents tenant context information.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="TenantName">The tenant display name.</param>
public sealed record TenantInfo(string TenantId, string TenantName);
