using SharpClaw.Abstractions.Identity;

namespace SharpClaw.Identity;

/// <summary>
/// JWT token service implementation.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    public string GenerateToken(DeviceIdentity device)
    {
        // Simple token generation for now - replace with JWT in production
        return $"{device.DeviceId}:{device.TenantId}:{DateTimeOffset.UtcNow.Ticks}";
    }
}
