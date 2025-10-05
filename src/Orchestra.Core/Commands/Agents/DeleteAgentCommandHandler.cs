using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Events.Agents;

namespace Orchestra.Core.Commands.Agents;

/// <summary>
/// Обработчик команды удаления агента
/// </summary>
public class DeleteAgentCommandHandler : IRequestHandler<DeleteAgentCommand, DeleteAgentResult>
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<DeleteAgentCommandHandler> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Инициализирует новый экземпляр DeleteAgentCommandHandler
    /// </summary>
    /// <param name="context">Контекст базы данных</param>
    /// <param name="logger">Логгер</param>
    /// <param name="mediator">Медиатор для публикации событий</param>
    public DeleteAgentCommandHandler(
        OrchestraDbContext context,
        ILogger<DeleteAgentCommandHandler> logger,
        IMediator mediator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <inheritdoc />
    public async Task<DeleteAgentResult> Handle(DeleteAgentCommand request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // Валидация входных данных
        if (string.IsNullOrWhiteSpace(request.AgentId))
        {
            return new DeleteAgentResult
            {
                Success = false,
                ErrorMessage = "Agent ID is required"
            };
        }

        _logger.LogInformation("Deleting agent {AgentId} (HardDelete: {HardDelete})",
            request.AgentId, request.HardDelete);

        try
        {
            // Проверяем существование агента
            var agent = await _context.Agents
                .FirstOrDefaultAsync(a => a.Id == request.AgentId, cancellationToken);

            if (agent == null)
            {
                _logger.LogWarning("Agent {AgentId} not found for deletion", request.AgentId);
                return new DeleteAgentResult
                {
                    Success = false,
                    ErrorMessage = $"Agent {request.AgentId} not found"
                };
            }

            // Проверяем, что агент не занят выполнением задачи
            if (agent.Status == AgentStatus.Busy)
            {
                _logger.LogWarning("Cannot delete agent {AgentId} - agent is currently busy", request.AgentId);
                return new DeleteAgentResult
                {
                    Success = false,
                    ErrorMessage = "Cannot delete agent while it is busy. Please stop the agent first."
                };
            }

            if (request.HardDelete)
            {
                // Жёсткое удаление: удаляем из базы данных
                _context.Agents.Remove(agent);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Agent {AgentId} hard deleted successfully", request.AgentId);

                // Публикуем событие удаления
                await _mediator.Publish(new AgentDeletedEvent(agent, wasHardDeleted: true), cancellationToken);

                return new DeleteAgentResult
                {
                    Success = true,
                    DeletedAgent = agent,
                    WasHardDeleted = true
                };
            }
            else
            {
                // Мягкое удаление: деактивируем агента
                agent.Status = AgentStatus.Offline;
                agent.LastPing = DateTime.UtcNow;
                // Можно добавить поле IsActive = false, если оно существует в модели

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Agent {AgentId} soft deleted (deactivated) successfully", request.AgentId);

                // Публикуем событие удаления
                await _mediator.Publish(new AgentDeletedEvent(agent, wasHardDeleted: false), cancellationToken);

                return new DeleteAgentResult
                {
                    Success = true,
                    DeletedAgent = agent,
                    WasHardDeleted = false
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete agent {AgentId}: {Error}",
                request.AgentId, ex.Message);

            return new DeleteAgentResult
            {
                Success = false,
                ErrorMessage = $"Deletion failed: {ex.Message}"
            };
        }
    }
}
