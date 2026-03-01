using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpClaw.Abstractions.Identity;
using SharpClaw.Abstractions.Persistence;
using SharpClaw.Persistence.Contracts.Entities;

namespace SharpClaw.Identity.UnitTests;

public class IdentityServicePairDeviceTests
{
    private static IdentityService CreateService(
        IRepository<DeviceIdentityEntity>? repository = null,
        ITokenService? tokenService = null)
    {
        return new IdentityService(
            repository ?? Substitute.For<IRepository<DeviceIdentityEntity>>(),
            tokenService ?? Substitute.For<ITokenService>(),
            NullLogger<IdentityService>.Instance);
    }

    [Fact]
    public async Task PairDeviceAsync_WhenDeviceNotFound_ReturnsNotLinked()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns((DeviceIdentityEntity?)null);

        var service = CreateService(repository);
        var result = await service.PairDeviceAsync("device-1", "tenant-1", "public-key");

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.NotLinked, result.ErrorCode);
    }

    [Fact]
    public async Task PairDeviceAsync_WhenAlreadyPairedWithDifferentKey_ReturnsAlreadyPaired()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = true,
            PublicKey = "different-key",
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);

        var service = CreateService(repository);
        var result = await service.PairDeviceAsync("device-1", "tenant-1", "new-key");

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.AlreadyPaired, result.ErrorCode);
    }

    [Fact]
    public async Task PairDeviceAsync_WhenAlreadyPairedWithSameKey_ReturnsSuccessWithToken()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var tokenService = Substitute.For<ITokenService>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = true,
            PublicKey = "same-key",
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);
        tokenService.GenerateToken(Arg.Any<DeviceIdentity>()).Returns("existing-token");

        var service = CreateService(repository, tokenService);
        var result = await service.PairDeviceAsync("device-1", "tenant-1", "same-key");

        Assert.True(result.IsSuccess);
        Assert.Equal("existing-token", result.Token);
        Assert.NotNull(result.Device);
    }

    [Fact]
    public async Task PairDeviceAsync_WhenNotPaired_CreatesNewPairing()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var tokenService = Substitute.For<ITokenService>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = false,
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);
        tokenService.GenerateToken(Arg.Any<DeviceIdentity>()).Returns("new-token");

        var service = CreateService(repository, tokenService);
        var result = await service.PairDeviceAsync("device-1", "tenant-1", "new-public-key");

        Assert.True(result.IsSuccess);
        Assert.Equal("new-token", result.Token);
        Assert.NotNull(result.Device);
        Assert.True(result.Device!.IsPaired);
        Assert.Equal("new-public-key", result.Device.PublicKey);

        await repository.Received(1).UpsertAsync(
            "device-1",
            "tenant-1",
            Arg.Is<DeviceIdentityEntity>(e =>
                e.IsPaired == true &&
                e.PublicKey == "new-public-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PairDeviceAsync_Success_SetsCorrectScopes()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var tokenService = Substitute.For<ITokenService>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = false,
            Scopes = ScopeRequirements.OperatorWrite,
            PublicKey = ""
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);
        tokenService.GenerateToken(Arg.Any<DeviceIdentity>()).Returns("token");

        var service = CreateService(repository, tokenService);
        var result = await service.PairDeviceAsync("device-1", "tenant-1", "public-key");

        Assert.True(result.IsSuccess);
        Assert.Contains(ScopeRequirements.OperatorWrite, result.Device!.Scopes);
    }

    [Fact]
    public async Task PairDeviceAsync_Success_SetsUpdatedAt()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var tokenService = Substitute.For<ITokenService>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = false,
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);
        tokenService.GenerateToken(Arg.Any<DeviceIdentity>()).Returns("token");

        var before = DateTimeOffset.UtcNow;
        var service = CreateService(repository, tokenService);
        var result = await service.PairDeviceAsync("device-1", "tenant-1", "public-key");
        var after = DateTimeOffset.UtcNow;

        Assert.True(result.IsSuccess);
        Assert.True(result.Device!.UpdatedAt >= before);
        Assert.True(result.Device.UpdatedAt <= after);
    }

    [Fact]
    public async Task PairDeviceAsync_WithCancellationToken_PassesThrough()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = false,
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);

        var service = CreateService(repository);
        using var cts = new CancellationTokenSource();

        await service.PairDeviceAsync("device-1", "tenant-1", "public-key", cts.Token);

        await repository.Received(1).UpsertAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DeviceIdentityEntity>(),
            cts.Token);
    }

    [Fact]
    public async Task PairDeviceAsync_WithEmptyPublicKey_PairsSuccessfully()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var tokenService = Substitute.For<ITokenService>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = false,
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);
        tokenService.GenerateToken(Arg.Any<DeviceIdentity>()).Returns("token");

        var service = CreateService(repository, tokenService);
        var result = await service.PairDeviceAsync("device-1", "tenant-1", "");

        Assert.True(result.IsSuccess);
        Assert.Equal("", result.Device!.PublicKey);
    }

    [Fact]
    public async Task PairDeviceAsync_WithLongPublicKey_PairsSuccessfully()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var tokenService = Substitute.For<ITokenService>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = false,
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);
        tokenService.GenerateToken(Arg.Any<DeviceIdentity>()).Returns("token");

        var longKey = new string('a', 10000);
        var service = CreateService(repository, tokenService);
        var result = await service.PairDeviceAsync("device-1", "tenant-1", longKey);

        Assert.True(result.IsSuccess);
        Assert.Equal(longKey, result.Device!.PublicKey);
    }

    [Fact]
    public async Task PairDeviceAsync_PreservesTenantId()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var tokenService = Substitute.For<ITokenService>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = false,
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);
        tokenService.GenerateToken(Arg.Any<DeviceIdentity>()).Returns("token");

        var service = CreateService(repository, tokenService);
        var result = await service.PairDeviceAsync("device-1", "tenant-1", "key");

        Assert.True(result.IsSuccess);
        Assert.Equal("tenant-1", result.Device!.TenantId);
    }

    [Fact]
    public async Task PairDeviceAsync_PreservesDeviceId()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var tokenService = Substitute.For<ITokenService>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = false,
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);
        tokenService.GenerateToken(Arg.Any<DeviceIdentity>()).Returns("token");

        var service = CreateService(repository, tokenService);
        var result = await service.PairDeviceAsync("device-1", "tenant-1", "key");

        Assert.True(result.IsSuccess);
        Assert.Equal("device-1", result.Device!.DeviceId);
    }

    [Fact]
    public async Task PairDeviceAsync_CallsTokenServiceWithPairedDevice()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var tokenService = Substitute.For<ITokenService>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = false,
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);
        tokenService.GenerateToken(Arg.Any<DeviceIdentity>()).Returns("token");

        var service = CreateService(repository, tokenService);
        await service.PairDeviceAsync("device-1", "tenant-1", "public-key");

        tokenService.Received(1).GenerateToken(Arg.Is<DeviceIdentity>(d =>
            d.IsPaired == true &&
            d.PublicKey == "public-key"));
    }
}

public static class ScopeRequirements
{
    public const string OperatorRead = "operator:read";
    public const string OperatorWrite = "operator:write";
    public const string Admin = "admin";
}
