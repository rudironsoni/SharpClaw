using SharpClaw.Identity;

namespace SharpClaw.Identity.IntegrationTests;

public class IdentityFlowIntegrationTests
{
    [Fact]
    public void PairThenAuthorize_SucceedsForWriteMethods()
    {
        var service = new IdentityService();
        _ = service.UpsertDevice("device-1", isPaired: true, [ScopeRequirements.OperatorWrite]);

        var identity = service.GetDevice("device-1");
        Assert.NotNull(identity);

        var context = new AuthContext(identity!.DeviceId, identity.Scopes, identity.IsPaired);
        var result = service.Authorize(context, "chat.send");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void PairWithReadScope_DeniesAdminMethods()
    {
        var service = new IdentityService();
        _ = service.UpsertDevice("device-2", isPaired: true, [ScopeRequirements.OperatorRead]);

        var identity = service.GetDevice("device-2");
        Assert.NotNull(identity);

        var context = new AuthContext(identity!.DeviceId, identity.Scopes, identity.IsPaired);
        var result = service.Authorize(context, "config.set");

        Assert.False(result.Succeeded);
    }
}
