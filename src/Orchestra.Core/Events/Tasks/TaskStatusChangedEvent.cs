using Orchestra.Core.Events;
using Orchestra.Core.Models;
using TaskStatus = Orchestra.Core.Models.TaskStatus;

namespace Orchestra.Core.Events.Tasks;

/// <summary>
/// Событие изменения статуса задачи
/// </summary>
public record TaskStatusChangedEvent(
    string TaskId,
    TaskStatus OldStatus,
    TaskStatus NewStatus,
    string? Result,
    string? ErrorMessage,
    DateTime Timestamp
) : IEvent;