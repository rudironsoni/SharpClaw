using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SharpClaw.Abstractions.Protocol;

/// <summary>
/// Protocol frame types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrameType
{
    Request,
    Response,
    Event
}

/// <summary>
/// Base protocol frame interface.
/// </summary>
public interface IProtocolFrame
{
    FrameType Type { get; }
}

/// <summary>
/// A protocol request frame.
/// </summary>
public sealed record RequestFrame : IProtocolFrame
{
    public FrameType Type => FrameType.Request;
    public required string Id { get; init; }
    public required string Method { get; init; }
    public object? Payload { get; init; }
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// A protocol response frame.
/// </summary>
public sealed record ResponseFrame : IProtocolFrame
{
    public FrameType Type => FrameType.Response;
    public required string Id { get; init; }
    public required bool Ok { get; init; }
    public object? Payload { get; init; }
    public ErrorShape? Error { get; init; }
}

/// <summary>
/// A protocol event frame.
/// </summary>
public sealed record EventFrame : IProtocolFrame
{
    public FrameType Type => FrameType.Event;
    public required string Event { get; init; }
    public object? Payload { get; init; }
    public long? Seq { get; init; }
    public long? StateVersion { get; init; }
}

/// <summary>
/// Error shape for protocol responses.
/// </summary>
public sealed record ErrorShape
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? Details { get; init; }
    public bool Retryable { get; init; }
    public int? RetryAfterMs { get; init; }
}

/// <summary>
/// Protocol error codes.
/// </summary>
public static class ErrorCodes
{
    public const string NotLinked = "NOT_LINKED";
    public const string NotPaired = "NOT_PAIRED";
    public const string AgentTimeout = "AGENT_TIMEOUT";
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string Unavailable = "UNAVAILABLE";
    public const string InternalError = "INTERNAL_ERROR";
    public const string RateLimited = "RATE_LIMITED";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
}

/// <summary>
/// Handshake connection role.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectionRole
{
    Operator,
    Node
}

/// <summary>
/// Handshake client information.
/// </summary>
public sealed record ConnectClient
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Platform { get; init; }
    public required string Mode { get; init; }
}

/// <summary>
/// Handshake device identity.
/// </summary>
public sealed record ConnectDeviceIdentity
{
    public required string Id { get; init; }
    public required string PublicKey { get; init; }
    public string? Signature { get; init; }
    public long? SignedAt { get; init; }
    public string? Nonce { get; init; }
}

/// <summary>
/// Handshake authentication.
/// </summary>
public sealed record ConnectAuth
{
    public string? Token { get; init; }
    public string? DeviceToken { get; init; }
    public string? Password { get; init; }
}

/// <summary>
/// Handshake connection parameters.
/// </summary>
public sealed record ConnectParams
{
    public required int MinProtocol { get; init; }
    public required int MaxProtocol { get; init; }
    public required ConnectClient Client { get; init; }
    public required ConnectionRole Role { get; init; }
    public IReadOnlyList<string>? Scopes { get; init; }
    public IReadOnlyList<string>? Caps { get; init; }
    public IReadOnlyList<string>? Commands { get; init; }
    public IReadOnlyList<string>? Permissions { get; init; }
    public ConnectDeviceIdentity? Device { get; init; }
    public ConnectAuth? Auth { get; init; }
    public string? Locale { get; init; }
    public string? UserAgent { get; init; }
}

/// <summary>
/// Server information in handshake response.
/// </summary>
public sealed record HelloServerInfo
{
    public required string Version { get; init; }
    public required string ConnectionId { get; init; }
}

/// <summary>
/// Features in handshake response.
/// </summary>
public sealed record HelloFeatures
{
    public IReadOnlyList<string> Methods { get; init; } = new List<string>();
    public IReadOnlyList<string> Events { get; init; } = new List<string>();
}

/// <summary>
/// Auth information in handshake response.
/// </summary>
public sealed record HelloAuth
{
    public string? DeviceToken { get; init; }
    public ConnectionRole? Role { get; init; }
    public IReadOnlyList<string>? Scopes { get; init; }
    public long? IssuedAtMs { get; init; }
}

/// <summary>
/// Policy in handshake response.
/// </summary>
public sealed record HelloPolicy
{
    public int MaxPayload { get; init; }
    public int MaxBufferedBytes { get; init; }
    public int TickIntervalMs { get; init; }
}

/// <summary>
/// Successful handshake response.
/// </summary>
public sealed record HelloOk
{
    public required int Protocol { get; init; }
    public required HelloServerInfo Server { get; init; }
    public required HelloFeatures Features { get; init; }
    public object? Snapshot { get; init; }
    public string? CanvasHostUrl { get; init; }
    public HelloAuth? Auth { get; init; }
    public HelloPolicy? Policy { get; init; }
}
