using SharpClaw.Identity;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.Identity.UnitTests;

public class IdentityServiceUnitTests
{
    [Fact]
    public void Authorize_ReturnsNotPaired_WhenDeviceNotPaired()
    {
        var service = new IdentityService();
        var context = new AuthContext("device-1", new HashSet<string>(StringComparer.OrdinalIgnoreCase), IsPaired: false);

        var result = service.Authorize(context, "chat.send");

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.NotPaired, result.Error);
    }

    [Fact]
    public void Authorize_ReturnsUnavailable_WhenMissingRequiredScope()
    {
        var service = new IdentityService();
        var context = new AuthContext("device-1", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ScopeRequirements.OperatorRead }, IsPaired: true);

        var result = service.Authorize(context, "chat.send");

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.Unavailable, result.Error);
    }

    [Fact]
    public void Authorize_Succeeds_WithRequiredScope()
    {
        var service = new IdentityService();
        var context = new AuthContext("device-1", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ScopeRequirements.OperatorWrite }, IsPaired: true);

        var result = service.Authorize(context, "chat.send");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void UpsertDevice_PersistsIdentity()
    {
        var service = new IdentityService();

        _ = service.UpsertDevice("device-1", isPaired: true, [ScopeRequirements.OperatorRead]);
        var device = service.GetDevice("device-1");

        Assert.NotNull(device);
        Assert.True(device!.IsPaired);
        Assert.Contains(ScopeRequirements.OperatorRead, device.Scopes);
    }
}
