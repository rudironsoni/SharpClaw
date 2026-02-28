using SharpClaw.Protocol.Contracts;

namespace SharpClaw.Protocol.UnitTests;

public class ProtocolContractsUnitTests
{
    [Fact]
    public void ProtocolConstants_AreExpected()
    {
        Assert.Equal(3, ProtocolConstants.CurrentProtocolVersion);
        Assert.Equal(18789, ProtocolConstants.DefaultGatewayPort);
    }

    [Fact]
    public void ErrorCodes_AreStableStrings()
    {
        Assert.Equal("NOT_LINKED", ErrorCodes.NotLinked);
        Assert.Equal("NOT_PAIRED", ErrorCodes.NotPaired);
        Assert.Equal("AGENT_TIMEOUT", ErrorCodes.AgentTimeout);
        Assert.Equal("INVALID_REQUEST", ErrorCodes.InvalidRequest);
        Assert.Equal("UNAVAILABLE", ErrorCodes.Unavailable);
        Assert.Equal(5, ErrorCodes.All.Count);
    }

    [Fact]
    public void RequestAndResponseFrames_HaveExpectedTypeDiscriminators()
    {
        var request = new RequestFrame("id-1", "chat.send", new { message = "hi" }, "idem-1");
        var response = new ResponseFrame("id-1", Ok: true);

        Assert.Equal(FrameType.Request, request.Type);
        Assert.Equal("idem-1", request.IdempotencyKey);
        Assert.Equal(FrameType.Response, response.Type);
    }

    [Fact]
    public void EventFrame_SupportsOptionalSequenceAndStateVersion()
    {
        var frame = new EventFrame("chat.delta", Payload: "hi", Seq: 41, StateVersion: 9);

        Assert.Equal(FrameType.Event, frame.Type);
        Assert.Equal(41, frame.Seq);
        Assert.Equal(9, frame.StateVersion);
    }

    [Fact]
    public void HelloOk_ContainsPolicyAndFeatures()
    {
        var hello = new HelloOk(
            Protocol: ProtocolConstants.CurrentProtocolVersion,
            Server: new HelloServerInfo("0.1.0", "conn-1"),
            Features: new HelloFeatures(
                Methods: ["chat.send", "chat.abort"],
                Events: ["chat.delta", "chat.done"]),
            Snapshot: null,
            CanvasHostUrl: null,
            Auth: new HelloAuth("dev-token", ConnectionRole.Operator, ["operator.write"], 1730000000),
            Policy: new HelloPolicy(1_048_576, 8_388_608, 1_000));

        Assert.Equal(ProtocolConstants.CurrentProtocolVersion, hello.Protocol);
        Assert.Contains("chat.send", hello.Features.Methods);
        Assert.Equal(1_000, hello.Policy.TickIntervalMs);
    }
}
