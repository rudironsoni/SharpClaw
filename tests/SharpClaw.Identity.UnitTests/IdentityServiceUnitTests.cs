using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpClaw.Abstractions.Identity;
using SharpClaw.Abstractions.Persistence;
using SharpClaw.Persistence.Contracts.Entities;

namespace SharpClaw.Identity.UnitTests;

public class IdentityServiceUnitTests
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
    public async Task AuthorizeAsync_ReturnsNotPaired_WhenDeviceNotPaired()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = false,
            Scopes = ScopeRequirements.OperatorWrite
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);

        var service = CreateService(repository);
        var result = await service.AuthorizeAsync("device-1", "tenant-1", new HashSet<string> { ScopeRequirements.OperatorWrite });

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.NotPaired, result.ErrorCode);
    }

    [Fact]
    public async Task AuthorizeAsync_ReturnsMissingScopes_WhenMissingRequiredScope()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = true,
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);

        var service = CreateService(repository);
        var result = await service.AuthorizeAsync("device-1", "tenant-1", new HashSet<string> { ScopeRequirements.OperatorWrite });

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.MissingScopes, result.ErrorCode);
    }

    [Fact]
    public async Task AuthorizeAsync_Succeeds_WithRequiredScope()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var tokenService = Substitute.For<ITokenService>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = true,
            Scopes = ScopeRequirements.OperatorWrite
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);
        tokenService.GenerateToken(Arg.Any<DeviceIdentity>()).Returns("test-token");

        var service = CreateService(repository, tokenService);
        var result = await service.AuthorizeAsync("device-1", "tenant-1", new HashSet<string> { ScopeRequirements.OperatorWrite });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Token);
    }

    [Fact]
    public async Task UpsertDeviceAsync_PersistsIdentity()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var service = CreateService(repository);

        var identity = new DeviceIdentity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = true,
            Scopes = new HashSet<string> { ScopeRequirements.OperatorRead }
        };

        await service.UpsertDeviceAsync("device-1", "tenant-1", identity);

        await repository.Received(1).UpsertAsync(
            "device-1",
            "tenant-1",
            Arg.Is<DeviceIdentityEntity>(e =>
                e.DeviceId == "device-1" &&
                e.TenantId == "tenant-1" &&
                e.IsPaired == true &&
                e.Scopes.Contains(ScopeRequirements.OperatorRead)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDeviceAsync_ReturnsNull_WhenDeviceNotFound()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns((DeviceIdentityEntity?)null);

        var service = CreateService(repository);
        var result = await service.GetDeviceAsync("device-1", "tenant-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDeviceAsync_ReturnsDevice_WhenFound()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = true,
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-1", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);

        var service = CreateService(repository);
        var result = await service.GetDeviceAsync("device-1", "tenant-1");

        Assert.NotNull(result);
        Assert.Equal("device-1", result!.DeviceId);
        Assert.Equal("tenant-1", result.TenantId);
        Assert.True(result.IsPaired);
    }
}
