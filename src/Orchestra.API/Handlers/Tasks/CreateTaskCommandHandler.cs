using MediatR;
using Orchestra.Core.Commands.Tasks;
using Orchestra.Core.Events.Tasks;
using Orchestra.API.Services;

namespace Orchestra.API.Handlers.Tasks;

/// <summary>
/// Обработчик команды создания задачи
/// </summary>
public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, string>
{
    private readonly TaskRepository _taskRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateTaskCommandHandler> _logger;

    public CreateTaskCommandHandler(
        TaskRepository taskRepository,
        IMediator mediator,
        ILogger<CreateTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<string> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating task: Command={Command}, RepositoryPath={RepositoryPath}, Priority={Priority}",
                request.Command, request.RepositoryPath, request.Priority);

            var taskId = await _taskRepository.QueueTaskAsync(request.Command, request.RepositoryPath, request.Priority);

            // Публикуем событие создания задачи
            await _mediator.Publish(new TaskCreatedEvent(
                taskId,
                request.Command,
                request.RepositoryPath,
                request.Priority,
                DateTime.UtcNow
            ), cancellationToken);

            _logger.LogInformation("Task created successfully: TaskId={TaskId}", taskId);
            return taskId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create task: Command={Command}", request.Command);
            throw;
        }
    }
}