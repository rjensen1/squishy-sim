namespace SquishySim.Domain;

public record ConversationMessage(
    DateTimeOffset Timestamp,
    string FromAgentId,
    string ToAgentId,
    string Text);
