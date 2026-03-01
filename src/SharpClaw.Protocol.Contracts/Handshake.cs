namespace SharpClaw.Protocol.Contracts;

public enum ConnectionRole
{
    Operator,
    Node
}

public sealed record ConnectClient(
    string Id,
    string Version,
    string? Platform = null,
    string? Mode = null);

public sealed record ConnectDeviceIdentity(
    string Id,
    string PublicKey,
    string Signature,
    long SignedAt,
    string Nonce);

public sealed record ConnectAuth(
    string? Token = null,
    string? DeviceToken = null,
    string? Password = null);

public sealed record ConnectParams(
    int MinProtocol,
    int MaxProtocol,
    ConnectClient Client,
    ConnectionRole? Role = null,
    IReadOnlyList<string>? Scopes = null,
    IReadOnlyList<string>? Caps = null,
    IReadOnlyList<string>? Commands = null,
    IReadOnlyList<string>? Permissions = null,
    ConnectDeviceIdentity? Device = null,
    ConnectAuth? Auth = null,
    string? Locale = null,
    string? UserAgent = null);

public sealed record HelloServerInfo(string Version, string ConnectionId);

public sealed record HelloFeatures(
    IReadOnlyList<string> Methods,
    IReadOnlyList<string> Events);

public sealed record HelloAuth(
    string? DeviceToken,
    ConnectionRole? Role,
    IReadOnlyList<string>? Scopes,
    long? IssuedAtMs);

public sealed record HelloPolicy(
    int MaxPayload,
    int MaxBufferedBytes,
    int TickIntervalMs);

public sealed record HelloOk(
    int Protocol,
    HelloServerInfo Server,
    HelloFeatures Features,
    object? Snapshot,
    Uri? CanvasHostUrl,
    HelloAuth? Auth,
    HelloPolicy Policy);
