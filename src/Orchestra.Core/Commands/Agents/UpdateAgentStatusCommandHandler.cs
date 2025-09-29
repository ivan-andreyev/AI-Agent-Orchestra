using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Events.Agents;

namespace Orchestra.Core.Commands.Agents;

/// <summary>
/// Обработчик команды обновления статуса агента
/// </summary>
public class UpdateAgentStatusCommandHandler : IRequestHandler<UpdateAgentStatusCommand, UpdateAgentStatusResult>
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<UpdateAgentStatusCommandHandler> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Инициализирует новый экземпляр UpdateAgentStatusCommandHandler
    /// </summary>
    /// <param name="context">Контекст базы данных</param>
    /// <param name="logger">Логгер</param>
    /// <param name="mediator">Медиатор для публикации событий</param>
    public UpdateAgentStatusCommandHandler(
        OrchestraDbContext context,
        ILogger<UpdateAgentStatusCommandHandler> logger,
        IMediator mediator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <inheritdoc />
    public async Task<UpdateAgentStatusResult> Handle(UpdateAgentStatusCommand request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.AgentId))
        {
            return new UpdateAgentStatusResult
            {
                Success = false,
                ErrorMessage = "Agent ID is required"
            };
        }

        _logger.LogDebug("Updating status for agent {AgentId} to {Status}",
            request.AgentId, request.Status);

        try
        {
            var agent = await _context.Agents
                .FirstOrDefaultAsync(a => a.Id == request.AgentId, cancellationToken);

            if (agent == null)
            {
                _logger.LogWarning("Agent {AgentId} not found for status update", request.AgentId);
                return new UpdateAgentStatusResult
                {
                    Success = false,
                    ErrorMessage = $"Agent {request.AgentId} not found"
                };
            }

            var previousStatus = agent.Status;
            var statusChanged = previousStatus != request.Status;

            // Обновляем статус агента
            agent.Status = request.Status;
            agent.LastPing = request.LastPing ?? DateTime.UtcNow;

            if (!string.IsNullOrEmpty(request.CurrentTask))
            {
                agent.CurrentTask = request.CurrentTask;
            }
            else if (request.Status == AgentStatus.Idle)
            {
                // Очищаем текущую задачу при переходе в статус Idle
                agent.CurrentTask = null;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Agent {AgentId} status updated from {PreviousStatus} to {NewStatus}",
                request.AgentId, previousStatus, request.Status);

            // Публикуем событие только если статус действительно изменился
            if (statusChanged)
            {
                var statusChangedEvent = new AgentStatusChangedEvent(
                    request.AgentId,
                    previousStatus,
                    request.Status,
                    request.StatusMessage);

                await _mediator.Publish(statusChangedEvent, cancellationToken);
            }

            return new UpdateAgentStatusResult
            {
                Success = true,
                Agent = agent,
                PreviousStatus = previousStatus,
                StatusChanged = statusChanged
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update status for agent {AgentId}: {Error}",
                request.AgentId, ex.Message);

            return new UpdateAgentStatusResult
            {
                Success = false,
                ErrorMessage = $"Status update failed: {ex.Message}"
            };
        }
    }
}