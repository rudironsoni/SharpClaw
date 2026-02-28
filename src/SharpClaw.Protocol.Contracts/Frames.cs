namespace SharpClaw.Protocol.Contracts;

public enum FrameType
{
    Request,
    Response,
    Event
}

public interface IProtocolFrame
{
    FrameType Type { get; }
}

public sealed record RequestFrame(
    string Id,
    string Method,
    object? Payload = null,
    string? IdempotencyKey = null) : IProtocolFrame
{
    public FrameType Type => FrameType.Request;
}

public sealed record ResponseFrame(
    string Id,
    bool Ok,
    object? Payload = null,
    ErrorShape? Error = null) : IProtocolFrame
{
    public FrameType Type => FrameType.Response;
}

public sealed record EventFrame(
    string Event,
    object? Payload = null,
    long? Seq = null,
    long? StateVersion = null) : IProtocolFrame
{
    public FrameType Type => FrameType.Event;
}
