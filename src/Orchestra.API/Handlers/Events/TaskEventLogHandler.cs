using MediatR;
using Orchestra.Core.Events.Tasks;

namespace Orchestra.API.Handlers.Events;

/// <summary>
/// Обработчик событий задач для логирования
/// </summary>
public class TaskEventLogHandler :
    INotificationHandler<TaskCreatedEvent>,
    INotificationHandler<TaskStatusChangedEvent>
{
    private readonly ILogger<TaskEventLogHandler> _logger;

    public TaskEventLogHandler(ILogger<TaskEventLogHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(TaskCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("📋 Task Created: {TaskId} - {Command} for {RepositoryPath} with priority {Priority}",
            notification.TaskId,
            notification.Command.Substring(0, Math.Min(50, notification.Command.Length)) + "...",
            notification.RepositoryPath,
            notification.Priority);

        // Здесь можно добавить дополнительную логику: отправка уведомлений, метрики и т.д.
        await Task.CompletedTask;
    }

    public async Task Handle(TaskStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        var statusEmoji = notification.NewStatus.ToString() switch
        {
            "Completed" => "✅",
            "Failed" => "❌",
            "InProgress" => "🔄",
            "Assigned" => "📌",
            _ => "📝"
        };

        _logger.LogInformation("{Emoji} Task Status Changed: {TaskId} - {OldStatus} → {NewStatus}",
            statusEmoji,
            notification.TaskId,
            notification.OldStatus,
            notification.NewStatus);

        if (!string.IsNullOrEmpty(notification.ErrorMessage))
        {
            _logger.LogWarning("Task {TaskId} failed with error: {ErrorMessage}",
                notification.TaskId, notification.ErrorMessage);
        }

        await Task.CompletedTask;
    }
}