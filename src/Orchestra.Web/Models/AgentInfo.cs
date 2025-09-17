namespace Orchestra.Web.Models;

public record AgentInfo(
    string Id,
    string Name,
    string Type,
    string RepositoryPath,
    AgentStatus Status,
    DateTime LastPing,
    string? CurrentTask = null
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
    TaskPriority Priority = TaskPriority.Normal
);

public enum TaskPriority
{
    Low,
    Normal,
    High,
    Critical
}

public record OrchestratorState(
    Dictionary<string, AgentInfo> Agents,
    Queue<TaskRequest> TaskQueue,
    DateTime LastUpdate
);

public record QueueTaskRequest(string Command, string RepositoryPath, TaskPriority Priority);
public record RegisterAgentRequest(string Id, string Name, string Type, string RepositoryPath);