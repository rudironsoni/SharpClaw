using Microsoft.Extensions.Configuration;
using SharpClaw.Abstractions.Identity;
using Xunit;

namespace SharpClaw.Identity.UnitTests;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _tokenService;

    public JwtTokenServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-that-is-at-least-32-characters-long"
            })
            .Build();
        _tokenService = new JwtTokenService(configuration);
    }

    [Fact]
    public void GenerateToken_WithValidDevice_ReturnsNonEmptyToken()
    {
        var device = new DeviceIdentity
        {
            DeviceId = "device-123",
            TenantId = "tenant-456",
            IsPaired = true,
            PublicKey = "test-public-key"
        };

        var token = _tokenService.GenerateToken(device);

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GenerateToken_ContainsDeviceId()
    {
        var device = new DeviceIdentity
        {
            DeviceId = "device-123",
            TenantId = "tenant-456",
            IsPaired = true
        };

        var token = _tokenService.GenerateToken(device);

        Assert.Contains("device-123", token);
    }

    [Fact]
    public void GenerateToken_ContainsTenantId()
    {
        var device = new DeviceIdentity
        {
            DeviceId = "device-123",
            TenantId = "tenant-456",
            IsPaired = true
        };

        var token = _tokenService.GenerateToken(device);

        Assert.Contains("tenant-456", token);
    }

    [Fact]
    public void GenerateToken_GeneratesUniqueTokens()
    {
        var device = new DeviceIdentity
        {
            DeviceId = "device-123",
            TenantId = "tenant-456",
            IsPaired = true
        };

        var token1 = _tokenService.GenerateToken(device);
        System.Threading.Thread.Sleep(10); // Ensure different timestamp
        var token2 = _tokenService.GenerateToken(device);

        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void GenerateToken_WithMinimalDevice_ReturnsToken()
    {
        var device = new DeviceIdentity
        {
            DeviceId = "d",
            TenantId = "t",
            IsPaired = false
        };

        var token = _tokenService.GenerateToken(device);

        Assert.NotNull(token);
        Assert.True(token.Length > 0);
    }

    [Fact]
    public void GenerateToken_WithLongDeviceId_WorksCorrectly()
    {
        var device = new DeviceIdentity
        {
            DeviceId = new string('a', 1000),
            TenantId = new string('b', 1000),
            IsPaired = true
        };

        var token = _tokenService.GenerateToken(device);

        Assert.NotNull(token);
        Assert.Contains(new string('a', 1000), token);
        Assert.Contains(new string('b', 1000), token);
    }

    [Fact]
    public void GenerateToken_WithSpecialCharactersInDeviceId_WorksCorrectly()
    {
        var device = new DeviceIdentity
        {
            DeviceId = "device-123_test@domain.com",
            TenantId = "tenant-456:test",
            IsPaired = true
        };

        var token = _tokenService.GenerateToken(device);

        Assert.NotNull(token);
        Assert.True(token.Length > 0);
    }

    [Fact]
    public void GenerateToken_MultipleCallsProduceDifferentTimestamps()
    {
        var device = new DeviceIdentity
        {
            DeviceId = "device-123",
            TenantId = "tenant-456",
            IsPaired = true
        };

        var tokens = new HashSet<string>();
        for (int i = 0; i < 10; i++)
        {
            tokens.Add(_tokenService.GenerateToken(device));
            System.Threading.Thread.Sleep(1);
        }

        // Most tokens should be unique due to timestamp
        Assert.True(tokens.Count >= 9, "Expected at least 9 unique tokens");
    }

    [Fact]
    public void GenerateToken_WithNullDevice_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _tokenService.GenerateToken(null!));
    }

    [Fact]
    public void GenerateToken_WithNullDeviceId_ThrowsArgumentException()
    {
        var device = new DeviceIdentity
        {
            DeviceId = null!,
            TenantId = "tenant-456",
            IsPaired = true
        };

        Assert.Throws<ArgumentNullException>(() => _tokenService.GenerateToken(device));
    }

    [Fact]
    public void GenerateToken_WithNullTenantId_ThrowsArgumentException()
    {
        var device = new DeviceIdentity
        {
            DeviceId = "device-123",
            TenantId = null!,
            IsPaired = true
        };

        Assert.Throws<ArgumentNullException>(() => _tokenService.GenerateToken(device));
    }

    [Fact]
    public void GenerateToken_ResultIsNotWhitespace()
    {
        var device = new DeviceIdentity
        {
            DeviceId = "device-123",
            TenantId = "tenant-456",
            IsPaired = true
        };

        var token = _tokenService.GenerateToken(device);

        Assert.False(string.IsNullOrWhiteSpace(token));
    }
}
