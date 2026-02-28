namespace SharpClaw.Approvals;

public enum ApprovalDecision
{
    Pending,
    Approved,
    Denied
}

public sealed record ApprovalRequest(
    string ApprovalId,
    string ToolName,
    string Arguments,
    ApprovalDecision Decision,
    DateTimeOffset UpdatedAt);
