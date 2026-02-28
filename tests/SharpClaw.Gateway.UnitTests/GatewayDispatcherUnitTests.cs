using SharpClaw.Gateway;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.Gateway.UnitTests;

public class GatewayDispatcherUnitTests
{
    [Fact]
    public async Task DispatchAsync_UsesRegisteredHandler()
    {
        var dispatcher = new GatewayDispatcher();
        dispatcher.Register(
            "chat.send",
            static (request, _) => Task.FromResult(new ResponseFrame(request.Id, true, new { status = "started" })));

        var response = await dispatcher.DispatchAsync(new RequestFrame("id-1", "chat.send"));

        Assert.True(response.Ok);
        Assert.Equal("id-1", response.Id);
    }

    [Fact]
    public void Register_DuplicateMethod_ThrowsInvalidOperationException()
    {
        var dispatcher = new GatewayDispatcher();
        dispatcher.Register("chat.send", static (request, _) => Task.FromResult(new ResponseFrame(request.Id, true)));

        Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Register("CHAT.SEND", static (request, _) => Task.FromResult(new ResponseFrame(request.Id, true))));
    }

    [Fact]
    public async Task DispatchAsync_UnknownMethod_ReturnsInvalidRequest()
    {
        var dispatcher = new GatewayDispatcher();

        var response = await dispatcher.DispatchAsync(new RequestFrame("id-2", "unknown.method"));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal(ErrorCodes.InvalidRequest, response.Error!.Code);
    }

    [Fact]
    public void KeepalivePolicy_Default_IsValid()
    {
        var policy = KeepalivePolicy.Default;

        Assert.True(policy.IsValid());
    }

    [Fact]
    public void KeepalivePolicy_Invalid_WhenTimeoutLessThanPing()
    {
        var policy = new KeepalivePolicy(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));

        Assert.False(policy.IsValid());
    }
}
