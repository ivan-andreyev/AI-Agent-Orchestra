using Orchestra.Core.Commands;
using Orchestra.Core.Models;
using TaskStatus = Orchestra.Core.Models.TaskStatus;

namespace Orchestra.Core.Commands.Tasks;

/// <summary>
/// Команда обновления статуса задачи
/// </summary>
public record UpdateTaskStatusCommand(
    string TaskId,
    TaskStatus Status,
    string? Result = null,
    string? ErrorMessage = null
) : ICommand<bool>;