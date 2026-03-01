using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpClaw.Abstractions.Identity;
using SharpClaw.Abstractions.Persistence;
using SharpClaw.Persistence.Contracts.Entities;

namespace SharpClaw.Identity.IntegrationTests;

public class IdentityFlowIntegrationTests
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
    public async Task PairThenAuthorize_SucceedsForWriteMethods()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var tokenService = Substitute.For<ITokenService>();
        var service = CreateService(repository, tokenService);

        // Setup: Create and upsert device with write scope
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

        // Verify device exists
        var identity = await service.GetDeviceAsync("device-1", "tenant-1");
        Assert.NotNull(identity);

        // Authorize for chat.send which requires operator:write
        var result = await service.AuthorizeAsync("device-1", "tenant-1", new HashSet<string> { ScopeRequirements.OperatorWrite });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PairWithReadScope_DeniesWriteMethods()
    {
        var repository = Substitute.For<IRepository<DeviceIdentityEntity>>();
        var service = CreateService(repository);

        // Setup: Create device with only read scope
        var device = new DeviceIdentityEntity
        {
            DeviceId = "device-2",
            TenantId = "tenant-1",
            IsPaired = true,
            Scopes = ScopeRequirements.OperatorRead
        };
        repository.GetAsync("device-2", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(device);

        // Verify device exists
        var identity = await service.GetDeviceAsync("device-2", "tenant-1");
        Assert.NotNull(identity);

        // Try to authorize for write operation (chat.send requires operator:write)
        var result = await service.AuthorizeAsync("device-2", "tenant-1", new HashSet<string> { ScopeRequirements.OperatorWrite });

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.MissingScopes, result.ErrorCode);
    }
}
