namespace SharpClaw.Operations;

public sealed record OperationMetrics(
    int ActiveConnections,
    int ActiveRuns,
    int PendingApprovals,
    DateTimeOffset ObservedAt);
