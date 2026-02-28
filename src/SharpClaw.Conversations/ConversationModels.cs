namespace SharpClaw.Conversations;

public sealed record ConversationMessage(
    string MessageId,
    string Role,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record ConversationState(
    string ConversationId,
    IReadOnlyList<ConversationMessage> Messages,
    DateTimeOffset UpdatedAt);
