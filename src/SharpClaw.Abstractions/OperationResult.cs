namespace SharpClaw.Abstractions;

public readonly record struct OperationResult(bool Succeeded, string? Error = null)
{
    public static OperationResult Success() => new(true, null);

    public static OperationResult Failure(string error) => new(false, error);
}
