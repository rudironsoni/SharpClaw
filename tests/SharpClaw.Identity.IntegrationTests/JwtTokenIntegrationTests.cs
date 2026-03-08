using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SharpClaw.Abstractions.Identity;
using SharpClaw.Identity;

namespace SharpClaw.Identity.IntegrationTests;

public class JwtTokenIntegrationTests
{
    private static IConfiguration CreateTestConfiguration(string secretKey)
    {
        var config = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = secretKey,
            ["Jwt:Issuer"] = "SharpClaw-Test",
            ["Jwt:Audience"] = "SharpClaw-Test-Clients",
            ["Jwt:ExpiryHours"] = "1"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
    }

    [Fact]
    public void GenerateToken_WithValidDevice_ReturnsValidJwt()
    {
        // Arrange
        var config = CreateTestConfiguration("test-secret-key-that-is-at-least-32-characters-long");
        var service = new JwtTokenService(config);
        var device = new DeviceIdentity
        {
            DeviceId = "test-device-123",
            TenantId = "tenant-456",
            IsPaired = true
        };

        // Act
        var token = service.GenerateToken(device);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        // Verify it's a valid JWT format
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    public void GenerateToken_IncludesCorrectClaims()
    {
        // Arrange
        var config = CreateTestConfiguration("test-secret-key-that-is-at-least-32-characters-long");
        var service = new JwtTokenService(config);
        var device = new DeviceIdentity
        {
            DeviceId = "device-abc",
            TenantId = "tenant-xyz",
            IsPaired = true
        };

        // Act
        var token = service.GenerateToken(device);
        var principal = service.ValidateToken(token);

        // Assert
        Assert.NotNull(principal);
        Assert.Equal("device-abc", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("tenant-xyz", principal.FindFirst("tenant_id")?.Value);
        Assert.Equal("True", principal.FindFirst("is_paired")?.Value);
    }

    [Fact]
    public void ValidateToken_WithValidToken_ReturnsPrincipal()
    {
        // Arrange
        var config = CreateTestConfiguration("test-secret-key-that-is-at-least-32-characters-long");
        var service = new JwtTokenService(config);
        var device = new DeviceIdentity
        {
            DeviceId = "test-device",
            TenantId = "test-tenant",
            IsPaired = true
        };
        var token = service.GenerateToken(device);

        // Act
        var principal = service.ValidateToken(token);

        // Assert
        Assert.NotNull(principal);
        Assert.True(principal.Identity?.IsAuthenticated);
    }

    [Fact]
    public void ValidateToken_WithInvalidToken_ReturnsNull()
    {
        // Arrange
        var config = CreateTestConfiguration("test-secret-key-that-is-at-least-32-characters-long");
        var service = new JwtTokenService(config);

        // Act - Use a properly formatted JWT with 3 segments but invalid signature
        var invalidToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0In0.invalidsignature";
        var principal = service.ValidateToken(invalidToken);

        // Assert
        Assert.Null(principal);
    }

    [Fact]
    public void ValidateToken_WithTamperedToken_ReturnsNull()
    {
        // Arrange
        var config = CreateTestConfiguration("test-secret-key-that-is-at-least-32-characters-long");
        var service = new JwtTokenService(config);
        var device = new DeviceIdentity
        {
            DeviceId = "test-device",
            TenantId = "test-tenant",
            IsPaired = true
        };
        var token = service.GenerateToken(device);
        var tamperedToken = token.Substring(0, token.Length - 10) + "tampered123";

        // Act
        var principal = service.ValidateToken(tamperedToken);

        // Assert
        Assert.Null(principal);
    }

    [Fact]
    public void GenerateToken_WithShortSecretKey_ThrowsException()
    {
        // Arrange
        var config = CreateTestConfiguration("short-key");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new JwtTokenService(config));
    }

    [Fact]
    public void GenerateToken_WithMissingSecretKey_ThrowsException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new JwtTokenService(config));
    }

    [Fact]
    public void JwtKeyRotationService_RotateKey_SuccessfullyRotates()
    {
        // Arrange
        var config = CreateTestConfiguration("original-key-that-is-at-least-32-characters-long-abc");
        var rotationService = new JwtKeyRotationService(config);

        // Act
        var newKeyId = rotationService.RotateKey("new-key-that-is-at-least-32-characters-long-xyz");

        // Assert
        Assert.NotNull(newKeyId);
        Assert.NotEmpty(newKeyId);

        var activeKeys = rotationService.GetActiveKeys();
        Assert.Contains(newKeyId, activeKeys.Keys);
    }

    [Fact]
    public void JwtKeyRotationService_ValidateToken_WithRotatedKey_ValidatesNewTokens()
    {
        // Arrange - Create configuration with proper issuer/audience
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "original-key-that-is-at-least-32-characters-long",
                ["Jwt:Issuer"] = "SharpClaw",
                ["Jwt:Audience"] = "SharpClawClients"
            })
            .Build();
        
        var rotationService = new JwtKeyRotationService(config);
        var newKey = "new-key-that-is-at-least-32-characters-long-xyz";

        // Rotate the key
        rotationService.RotateKey(newKey);

        // Create new token with rotated key
        var newCredentials = rotationService.GetCurrentSigningCredentials();
        var tokenHandler = new JwtSecurityTokenHandler();
        var newToken = tokenHandler.CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", "test")]),
            Issuer = "SharpClaw",
            Audience = "SharpClawClients",
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = newCredentials
        });

        // Act
        var newValidated = rotationService.TryValidateToken(newToken, out var newPrincipal);

        // Assert - New token should validate with rotated key
        Assert.True(newValidated);
        Assert.NotNull(newPrincipal);
    }

    [Fact]
    public void JwtKeyRotationService_GetActiveKeys_ReturnsKeyMetadata()
    {
        // Arrange
        var config = CreateTestConfiguration("test-key-that-is-at-least-32-characters-long");
        var rotationService = new JwtKeyRotationService(config);

        // Act
        var keys = rotationService.GetActiveKeys();

        // Assert
        Assert.NotNull(keys);
        Assert.Single(keys);
        Assert.True(keys.Values.All(k => k <= DateTimeOffset.UtcNow));
    }
}
