namespace SharpClaw.Configuration;

public sealed record ConfigurationRevision(
    string Revision,
    string Hash,
    bool IsActive,
    DateTimeOffset UpdatedAt);
