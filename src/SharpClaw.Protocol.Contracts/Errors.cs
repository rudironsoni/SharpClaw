namespace SharpClaw.Protocol.Contracts;

public static class ErrorCodes
{
    public const string NotLinked = "NOT_LINKED";
    public const string NotPaired = "NOT_PAIRED";
    public const string AgentTimeout = "AGENT_TIMEOUT";
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string Unavailable = "UNAVAILABLE";
    public const string InternalError = "INTERNAL_ERROR";

    public const string MethodNotFound = "METHOD_NOT_FOUND";

    public static readonly IReadOnlyList<string> All =
    [
        NotLinked,
        NotPaired,
        AgentTimeout,
        InvalidRequest,
        Unavailable,
        InternalError,
        MethodNotFound
    ];
}

public sealed record ErrorShape(
    string Code,
    string Message,
    object? Details = null,
    bool? Retryable = null,
    int? RetryAfterMs = null);
