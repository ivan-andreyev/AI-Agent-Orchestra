namespace Orchestra.Core.Models;

/// <summary>
/// Приоритет задачи
/// </summary>
public enum TaskPriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Статус задачи
/// </summary>
public enum TaskStatus
{
    Pending,
    Assigned,
    InProgress,
    Completed,
    Failed,
    Cancelled
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
    DateTime? CompletedAt = null
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