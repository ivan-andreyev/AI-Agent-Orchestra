using MediatR;
using Orchestra.Core.Commands.Tasks;
using Orchestra.Core.Events.Tasks;
using Orchestra.API.Services;
using Orchestra.Core.Models;

namespace Orchestra.API.Handlers.Tasks;

/// <summary>
/// Обработчик команды обновления статуса задачи
/// </summary>
public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, bool>
{
    private readonly TaskRepository _taskRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<UpdateTaskStatusCommandHandler> _logger;

    public UpdateTaskStatusCommandHandler(
        TaskRepository taskRepository,
        IMediator mediator,
        ILogger<UpdateTaskStatusCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<bool> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating task status: TaskId={TaskId}, Status={Status}",
                request.TaskId, request.Status);

            // Получаем текущий статус для события
            var currentTasks = await _taskRepository.GetTaskQueueAsync();
            var currentTask = currentTasks.FirstOrDefault(t => t.Id == request.TaskId);
            var oldStatus = currentTask?.Status ?? Orchestra.Core.Models.TaskStatus.Pending;

            var success = await _taskRepository.UpdateTaskStatusAsync(
                request.TaskId,
                request.Status,
                request.Result,
                request.ErrorMessage);

            if (success)
            {
                // Публикуем событие изменения статуса
                await _mediator.Publish(new TaskStatusChangedEvent(
                    request.TaskId,
                    oldStatus,
                    request.Status,
                    request.Result,
                    request.ErrorMessage,
                    DateTime.UtcNow
                ), cancellationToken);

                _logger.LogInformation("Task status updated successfully: TaskId={TaskId}, Status={Status}",
                    request.TaskId, request.Status);
            }
            else
            {
                _logger.LogWarning("Failed to update task status: TaskId={TaskId}, Status={Status}",
                    request.TaskId, request.Status);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task status: TaskId={TaskId}", request.TaskId);
            throw;
        }
    }
}