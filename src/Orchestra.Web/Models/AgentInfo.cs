namespace Orchestra.Web.Models;

public record AgentInfo(
    string Id,
    string Name,
    string Type,
    string RepositoryPath,
    AgentStatus Status,
    DateTime LastPing,
    string? CurrentTask = null,
    string? SessionId = null,
    string? Repository = null,
    DateTime? TaskStartTime = null
);

public enum AgentStatus
{
    Idle,
    Working,
    Error,
    Offline
}

public record TaskRequest(
    string Id,
    string AgentId,
    string Command,
    string RepositoryPath,
    DateTime CreatedAt,
    TaskPriority Priority = TaskPriority.Normal,
    TaskStatus Status = TaskStatus.Pending,
    DateTime? StartedAt = null,
    DateTime? CompletedAt = null
);

public enum TaskPriority
{
    Low,
    Normal,
    High,
    Critical
}

public enum TaskStatus
{
    Pending,
    Assigned,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public record RepositoryInfo(
    string Name,
    string Path,
    List<AgentInfo> Agents,
    int IdleCount,
    int WorkingCount,
    int ErrorCount,
    int OfflineCount,
    DateTime LastUpdate
);

public record OrchestratorState(
    Dictionary<string, AgentInfo> Agents,
    Queue<TaskRequest> TaskQueue,
    DateTime LastUpdate,
    Dictionary<string, RepositoryInfo>? Repositories = null
);

public record QueueTaskRequest(string Command, string RepositoryPath, TaskPriority Priority);
public record RegisterAgentRequest(string Id, string Name, string Type, string RepositoryPath);

public record AgentHistoryEntry(
    DateTime Timestamp,
    string Type,
    string Content
);