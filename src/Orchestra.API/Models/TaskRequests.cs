using Orchestra.Core.Models;

namespace Orchestra.API.Models;

/// <summary>
/// Запрос создания задачи
/// </summary>
public record CreateTaskRequest(
    string Command,
    string RepositoryPath,
    TaskPriority Priority = TaskPriority.Normal
);

/// <summary>
/// Запрос обновления статуса задачи
/// </summary>
public record UpdateTaskStatusRequest(
    Orchestra.Core.Models.TaskStatus Status,
    string? Result = null,
    string? ErrorMessage = null
);