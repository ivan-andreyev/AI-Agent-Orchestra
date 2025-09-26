using MediatR;
using Orchestra.Core.Queries.Tasks;
using Orchestra.Core.Models;
using Orchestra.API.Services;

namespace Orchestra.API.Handlers.Tasks;

/// <summary>
/// Обработчик запроса получения следующей задачи для агента
/// </summary>
public class GetNextTaskForAgentQueryHandler : IRequestHandler<GetNextTaskForAgentQuery, TaskRequest>
{
    private readonly TaskRepository _taskRepository;
    private readonly ILogger<GetNextTaskForAgentQueryHandler> _logger;

    public GetNextTaskForAgentQueryHandler(
        TaskRepository taskRepository,
        ILogger<GetNextTaskForAgentQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<TaskRequest> Handle(GetNextTaskForAgentQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting next task for agent: AgentId={AgentId}", request.AgentId);

            var task = await _taskRepository.GetNextTaskForAgentAsync(request.AgentId);

            if (task != null)
            {
                _logger.LogInformation("Found task for agent: AgentId={AgentId}, TaskId={TaskId}",
                    request.AgentId, task.Id);
                return task;
            }
            else
            {
                _logger.LogInformation("No tasks available for agent: AgentId={AgentId}", request.AgentId);
                return TaskRequest.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting next task for agent: AgentId={AgentId}", request.AgentId);
            throw;
        }
    }
}