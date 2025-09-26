using Orchestra.Core.Commands;
using Orchestra.Core.Models;

namespace Orchestra.Core.Commands.Tasks;

/// <summary>
/// Команда создания новой задачи
/// </summary>
public record CreateTaskCommand(
    string Command,
    string RepositoryPath,
    TaskPriority Priority = TaskPriority.Normal
) : ICommand<string>;