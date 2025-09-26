using Orchestra.Core.Models;
using TaskStatus = Orchestra.Core.Models.TaskStatus;
using TaskPriority = Orchestra.Core.Models.TaskPriority;

namespace Orchestra.Core;

public record AgentInfo(
    string Id,
    string Name,
    string Type,
    string RepositoryPath,
    AgentStatus Status,
    DateTime LastPing,
    string? CurrentTask = null,
    string? SessionId = null
)
{
    public string? CurrentTaskId { get; set; }
    public DateTime LastActiveTime { get; set; } = LastPing;
    public int TasksCompleted { get; set; } = 0;
};

public enum AgentStatus
{
    Idle,
    Working,
    Busy,
    Error,
    Offline
}


public record TaskResult(
    string TaskId,
    string AgentId,
    TaskStatus Status,
    string? Output,
    bool Success,
    DateTime? CompletedAt,
    TimeSpan? ExecutionTime = null,
    string? ErrorMessage = null
);


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
