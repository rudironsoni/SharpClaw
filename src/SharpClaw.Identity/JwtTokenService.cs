using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SharpClaw.Abstractions.Identity;

namespace SharpClaw.Identity;

/// <summary>
/// JWT token service implementation.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

        if (secretKey.Length < 32)
        {
            throw new InvalidOperationException("JWT SecretKey must be at least 32 characters long.");
        }

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    }

    public string GenerateToken(DeviceIdentity device)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(device.DeviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(device.TenantId);

        var issuer = _configuration["Jwt:Issuer"] ?? "SharpClaw";
        var audience = _configuration["Jwt:Audience"] ?? "SharpClawClients";
        var expiryHours = _configuration.GetValue<int?>("Jwt:ExpiryHours") ?? 24;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, device.DeviceId),
            new Claim("tenant_id", device.TenantId),
            new Claim("is_paired", device.IsPaired.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var tokenHandler = new JwtSecurityTokenHandler();
        var issuer = _configuration["Jwt:Issuer"] ?? "SharpClaw";
        var audience = _configuration["Jwt:Audience"] ?? "SharpClawClients";

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = _signingKey,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}

/// <summary>
/// Service for managing JWT signing key rotation with zero-downtime support.
/// </summary>
public sealed class JwtKeyRotationService : IJwtKeyRotationService
{
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, SymmetricSecurityKey> _keys;
    private string _primaryKeyId = "";
    private readonly Lock _lock = new();

    public JwtKeyRotationService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _keys = new ConcurrentDictionary<string, SymmetricSecurityKey>();
        LoadKeys();
    }

    public SigningCredentials GetCurrentSigningCredentials()
    {
        var keyId = _primaryKeyId;
        if (!_keys.TryGetValue(keyId, out var key))
        {
            throw new InvalidOperationException("No valid signing key available.");
        }

        return new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public bool TryValidateToken(string token, out ClaimsPrincipal? principal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var tokenHandler = new JwtSecurityTokenHandler();
        var issuer = _configuration["Jwt:Issuer"] ?? "SharpClaw";
        var audience = _configuration["Jwt:Audience"] ?? "SharpClawClients";

        foreach (var keyEntry in _keys)
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = keyEntry.Value,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            try
            {
                principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return true;
            }
            catch (SecurityTokenException)
            {
                continue;
            }
        }

        principal = null;
        return false;
    }

    public string RotateKey(string newKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newKey);

        if (newKey.Length < 32)
        {
            throw new ArgumentException("New key must be at least 32 characters long.", nameof(newKey));
        }

        var keyId = Guid.NewGuid().ToString("N")[..16];
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(newKey));

        lock (_lock)
        {
            _keys[keyId] = securityKey;
            _primaryKeyId = keyId;

            while (_keys.Count > 2)
            {
                var oldestKey = _keys.Keys.FirstOrDefault(k => k != _primaryKeyId);
                if (oldestKey != null)
                {
                    _keys.TryRemove(oldestKey, out _);
                }
            }
        }

        return keyId;
    }

    public IReadOnlyDictionary<string, DateTimeOffset> GetActiveKeys()
    {
        lock (_lock)
        {
            return _keys.ToDictionary(
                kvp => kvp.Key,
                _ => DateTimeOffset.UtcNow);
        }
    }

    private void LoadKeys()
    {
        var keyValue = _configuration["Jwt:SecretKey"];
        if (!string.IsNullOrWhiteSpace(keyValue) && keyValue.Length >= 32)
        {
            var keyId = Guid.NewGuid().ToString("N")[..16];
            _keys[keyId] = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
            _primaryKeyId = keyId;
        }
    }
}

/// <summary>
/// Service interface for JWT key rotation management.
/// </summary>
public interface IJwtKeyRotationService
{
    SigningCredentials GetCurrentSigningCredentials();
    bool TryValidateToken(string token, out ClaimsPrincipal? principal);
    string RotateKey(string newKey);
    IReadOnlyDictionary<string, DateTimeOffset> GetActiveKeys();
}
