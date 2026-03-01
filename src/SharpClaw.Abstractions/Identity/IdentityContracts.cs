using System;
using System.Collections.Generic;

namespace SharpClaw.Abstractions.Identity;

/// <summary>
/// Device identity information.
/// </summary>
public sealed record DeviceIdentity
{
    public required string DeviceId { get; init; }
    public required string TenantId { get; init; }
    public required bool IsPaired { get; init; }
    public IReadOnlySet<string> Scopes { get; init; } = new HashSet<string>();
    public DateTimeOffset UpdatedAt { get; init; }
    public string? PublicKey { get; init; }
}

/// <summary>
/// Authentication context for a device.
/// </summary>
public sealed record DeviceContext
{
    public required string DeviceId { get; init; }
    public required string TenantId { get; init; }
    public IReadOnlySet<string> Scopes { get; init; } = new HashSet<string>();
    public bool IsPaired { get; init; }
}

/// <summary>
/// Device authentication result.
/// </summary>
public sealed record DeviceAuthResult
{
    public bool IsSuccess { get; init; }
    public DeviceIdentity? Device { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Token { get; init; }
}

/// <summary>
/// Identity service interface.
/// </summary>
public interface IIdentityService
{
    Task<DeviceIdentity?> GetDeviceAsync(string deviceId, string tenantId, CancellationToken ct = default);
    Task<DeviceIdentity> UpsertDeviceAsync(string deviceId, string tenantId, DeviceIdentity identity, CancellationToken ct = default);
    Task<DeviceAuthResult> AuthorizeAsync(string deviceId, string tenantId, IReadOnlySet<string> requiredScopes, CancellationToken ct = default);
    Task<DeviceAuthResult> PairDeviceAsync(string deviceId, string tenantId, string publicKey, CancellationToken ct = default);
}

/// <summary>
/// Scope requirements.
/// </summary>
public static class ScopeRequirements
{
    public const string OperatorRead = "operator:read";
    public const string OperatorWrite = "operator:write";
    public const string OperatorAdmin = "operator:admin";
    public const string OperatorApprovals = "operator:approvals";
    public const string OperatorPairing = "operator:pairing";

    public static readonly IReadOnlyDictionary<string, string> RequiredScopeByMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["chat.list"] = OperatorRead,
        ["chat.send"] = OperatorWrite,
        ["chat.abort"] = OperatorWrite,
        ["config.get"] = OperatorRead,
        ["config.set"] = OperatorAdmin,
        ["exec.approve"] = OperatorApprovals,
        ["exec.deny"] = OperatorApprovals,
        ["device.pair"] = OperatorPairing
    };

    public static string? GetRequiredScope(string method)
    {
        return RequiredScopeByMethod.TryGetValue(method, out var scope) ? scope : null;
    }
}
