namespace SharpClaw.Identity;

public sealed record DeviceIdentity(
    string DeviceId,
    bool IsPaired,
    IReadOnlySet<string> Scopes,
    DateTimeOffset UpdatedAt);

public sealed record AuthContext(
    string DeviceId,
    IReadOnlySet<string> Scopes,
    bool IsPaired);

public static class ScopeRequirements
{
    public const string OperatorRead = "operator.read";
    public const string OperatorWrite = "operator.write";
    public const string OperatorAdmin = "operator.admin";
    public const string OperatorApprovals = "operator.approvals";
    public const string OperatorPairing = "operator.pairing";

    public static readonly IReadOnlyDictionary<string, string> RequiredScopeByMethod =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["chat.list"] = OperatorRead,
            ["chat.send"] = OperatorWrite,
            ["chat.abort"] = OperatorWrite,
            ["config.get"] = OperatorRead,
            ["config.set"] = OperatorAdmin,
            ["exec.approve"] = OperatorApprovals,
            ["exec.deny"] = OperatorApprovals,
            ["device.pair"] = OperatorPairing
        };
}
