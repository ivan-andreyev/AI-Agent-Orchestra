namespace Orchestra.Core.Models;

/// <summary>
/// Приоритет задачи
/// </summary>
public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Статус задачи
/// </summary>
public enum TaskStatus
{
    Pending = 0,
    Assigned = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

/// <summary>
/// Запрос на выполнение задачи
/// </summary>
public record TaskRequest(
    string Id,
    string AgentId,
    string Command,
    string RepositoryPath,
    DateTime CreatedAt,
    TaskPriority Priority = TaskPriority.Normal,
    TaskStatus Status = TaskStatus.Pending,
    DateTime? StartedAt = null,
    DateTime? CompletedAt = null,
    string? Result = null
)
{
    /// <summary>
    /// Пустой запрос для случаев когда задачи нет
    /// </summary>
    public static readonly TaskRequest Empty = new TaskRequest(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        DateTime.MinValue
    );

    /// <summary>
    /// Проверяет, является ли запрос пустым
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(Id);
}