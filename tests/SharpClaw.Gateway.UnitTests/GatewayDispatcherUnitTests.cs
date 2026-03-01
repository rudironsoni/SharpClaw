using System.Collections.Frozen;
using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Gateway;
using SharpClaw.Gateway.Events;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.Gateway.UnitTests;

public class GatewayDispatcherUnitTests
{
    private static GatewayDispatcher CreateDispatcher()
    {
        var eventPublisher = new ChannelEventPublisher(
            EventPublisherOptions.Default,
            NullLogger<ChannelEventPublisher>.Instance);
        return new GatewayDispatcher(eventPublisher, NullLogger<GatewayDispatcher>.Instance);
    }

    [Fact]
    public async Task DispatchAsync_UsesRegisteredHandler()
    {
        var dispatcher = CreateDispatcher();
        dispatcher.Register(
            "chat.send",
            FrozenSet<string>.Empty,
            static (request, _, _) => Task.FromResult(new ResponseFrame(request.Id, true, new { status = "started" })));

        var response = await dispatcher.DispatchAsync(new RequestFrame("id-1", "chat.send"), new DeviceContext("device-1", FrozenSet<string>.Empty, true));

        Assert.True(response.Ok);
        Assert.Equal("id-1", response.Id);
    }

    [Fact]
    public void Register_DuplicateMethod_ThrowsInvalidOperationException()
    {
        var dispatcher = CreateDispatcher();
        dispatcher.Register("chat.send", FrozenSet<string>.Empty, static (request, _, _) => Task.FromResult(new ResponseFrame(request.Id, true)));

        Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Register("CHAT.SEND", FrozenSet<string>.Empty, static (request, _, _) => Task.FromResult(new ResponseFrame(request.Id, true))));
    }

    [Fact]
    public async Task DispatchAsync_UnknownMethod_ReturnsMethodNotFound()
    {
        var dispatcher = CreateDispatcher();

        var response = await dispatcher.DispatchAsync(new RequestFrame("id-2", "unknown.method"), new DeviceContext("device-1", FrozenSet<string>.Empty, true));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal(ErrorCodes.MethodNotFound, response.Error!.Code);
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrowsException_ReturnsInternalError()
    {
        var dispatcher = CreateDispatcher();
        dispatcher.Register(
            "throw.error",
            FrozenSet<string>.Empty,
            static (request, _, _) => throw new InvalidOperationException("Something went wrong"));

        var response = await dispatcher.DispatchAsync(new RequestFrame("id-3", "throw.error"), new DeviceContext("device-1", FrozenSet<string>.Empty, true));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal(ErrorCodes.InternalError, response.Error!.Code);
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
