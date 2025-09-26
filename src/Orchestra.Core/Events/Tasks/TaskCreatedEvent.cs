using Orchestra.Core.Events;
using Orchestra.Core.Models;

namespace Orchestra.Core.Events.Tasks;

/// <summary>
/// Событие создания новой задачи
/// </summary>
public record TaskCreatedEvent(
    string TaskId,
    string Command,
    string RepositoryPath,
    TaskPriority Priority,
    DateTime Timestamp
) : IEvent;