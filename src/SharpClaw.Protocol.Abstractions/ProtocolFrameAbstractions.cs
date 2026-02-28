namespace SharpClaw.Protocol.Abstractions;

public interface IProtocolFrameCodec
{
    byte[] Serialize<TFrame>(TFrame frame);

    TFrame Deserialize<TFrame>(ReadOnlySpan<byte> payload);
}

public interface IProtocolFrameValidator<in TFrame>
{
    bool IsValid(TFrame frame, out string? reason);
}
