using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Events.Sessions;

namespace Orchestra.Core.Commands.Sessions;

/// <summary>
/// Обработчик команды создания сессии агента
/// </summary>
public class CreateAgentSessionCommandHandler : IRequestHandler<CreateAgentSessionCommand, AgentSession>
{
    private readonly OrchestraDbContext _context;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateAgentSessionCommandHandler> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр CreateAgentSessionCommandHandler
    /// </summary>
    /// <param name="context">Контекст базы данных</param>
    /// <param name="mediator">Медиатор для публикации событий</param>
    /// <param name="logger">Логгер</param>
    public CreateAgentSessionCommandHandler(
        OrchestraDbContext context,
        IMediator mediator,
        ILogger<CreateAgentSessionCommandHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AgentSession> Handle(CreateAgentSessionCommand request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.AgentId))
        {
            throw new ArgumentException("AgentId is required", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new ArgumentException("SessionId is required", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            throw new ArgumentException("WorkingDirectory is required", nameof(request));
        }

        _logger.LogDebug("Creating session for agent {AgentId} with SessionId {SessionId}",
            request.AgentId, request.SessionId);

        try
        {
            // Валидация: AgentId должен существовать в БД
            var agentExists = await _context.Agents
                .AnyAsync(a => a.Id == request.AgentId, cancellationToken);

            if (!agentExists)
            {
                _logger.LogWarning("Agent {AgentId} not found for session creation", request.AgentId);
                throw new InvalidOperationException($"Agent {request.AgentId} not found");
            }

            // Создание новой сессии
            var session = new AgentSession
            {
                Id = Guid.NewGuid().ToString(),
                AgentId = request.AgentId,
                SessionId = request.SessionId,
                WorkingDirectory = request.WorkingDirectory,
                ProcessId = request.ProcessId,
                Status = SessionStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                TotalCostUsd = 0,
                TotalDurationMs = 0,
                MessageCount = 0
            };

            _context.AgentSessions.Add(session);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created session {SessionId} for agent {AgentId}",
                session.SessionId, session.AgentId);

            // Публикация события
            await _mediator.Publish(new AgentSessionCreatedEvent(session), cancellationToken);

            return session;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session for agent {AgentId}: {Error}",
                request.AgentId, ex.Message);
            throw new InvalidOperationException($"Session creation failed: {ex.Message}", ex);
        }
    }
}
