using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;

namespace Orchestra.Core.Commands.Agents;

/// <summary>
/// Обработчик команды обновления SessionId агента
/// </summary>
public class UpdateAgentSessionIdCommandHandler : IRequestHandler<UpdateAgentSessionIdCommand, UpdateAgentSessionIdResult>
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<UpdateAgentSessionIdCommandHandler> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр UpdateAgentSessionIdCommandHandler
    /// </summary>
    /// <param name="context">Контекст базы данных</param>
    /// <param name="logger">Логгер</param>
    public UpdateAgentSessionIdCommandHandler(
        OrchestraDbContext context,
        ILogger<UpdateAgentSessionIdCommandHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<UpdateAgentSessionIdResult> Handle(UpdateAgentSessionIdCommand request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.AgentId))
        {
            return new UpdateAgentSessionIdResult
            {
                Success = false,
                ErrorMessage = "Agent ID is required"
            };
        }

        _logger.LogDebug("Updating SessionId for agent {AgentId} to {SessionId}",
            request.AgentId, request.SessionId);

        try
        {
            var agent = await _context.Agents
                .FirstOrDefaultAsync(a => a.Id == request.AgentId, cancellationToken);

            if (agent == null)
            {
                _logger.LogWarning("Agent {AgentId} not found for SessionId update", request.AgentId);
                return new UpdateAgentSessionIdResult
                {
                    Success = false,
                    ErrorMessage = $"Agent {request.AgentId} not found"
                };
            }

            // Update SessionId
            agent.SessionId = request.SessionId;
            agent.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Agent {AgentId} SessionId updated to {SessionId}",
                request.AgentId, request.SessionId);

            return new UpdateAgentSessionIdResult
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update SessionId for agent {AgentId}: {Error}",
                request.AgentId, ex.Message);

            return new UpdateAgentSessionIdResult
            {
                Success = false,
                ErrorMessage = $"SessionId update failed: {ex.Message}"
            };
        }
    }
}
