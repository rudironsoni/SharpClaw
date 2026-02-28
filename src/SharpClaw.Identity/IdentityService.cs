using System.Collections.Concurrent;
using SharpClaw.Abstractions;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.Identity;

public sealed class IdentityService
{
    private readonly ConcurrentDictionary<string, DeviceIdentity> _devices = new(StringComparer.Ordinal);

    public OperationResult UpsertDevice(string deviceId, bool isPaired, IEnumerable<string> scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        var scopeSet = scopes
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var identity = new DeviceIdentity(deviceId, isPaired, scopeSet, DateTimeOffset.UtcNow);
        _devices[deviceId] = identity;
        return OperationResult.Success();
    }

    public DeviceIdentity? GetDevice(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        _devices.TryGetValue(deviceId, out var identity);
        return identity;
    }

    public OperationResult Authorize(AuthContext authContext, string method)
    {
        ArgumentNullException.ThrowIfNull(authContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        if (!authContext.IsPaired)
        {
            return OperationResult.Failure(ErrorCodes.NotPaired);
        }

        if (!ScopeRequirements.RequiredScopeByMethod.TryGetValue(method, out var requiredScope))
        {
            return OperationResult.Failure(ErrorCodes.InvalidRequest);
        }

        return authContext.Scopes.Contains(requiredScope)
            ? OperationResult.Success()
            : OperationResult.Failure(ErrorCodes.Unavailable);
    }
}
