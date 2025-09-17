namespace Orchestra.Core.Models;

public record AgentHistoryEntry(
    DateTime Timestamp,
    string Type,
    string Content
);