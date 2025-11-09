using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Events.Sessions;

namespace Orchestra.Core.Commands.Sessions;

/// <summary>
/// Обработчик команды обновления статуса сессии агента
/// </summary>
public class UpdateSessionStatusCommandHandler : IRequestHandler<UpdateSessionStatusCommand, AgentSession>
{
    private readonly OrchestraDbContext _context;
    private readonly IMediator _mediator;
    private readonly ILogger<UpdateSessionStatusCommandHandler> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр UpdateSessionStatusCommandHandler
    /// </summary>
    /// <param name="context">Контекст базы данных</param>
    /// <param name="mediator">Медиатор для публикации событий</param>
    /// <param name="logger">Логгер</param>
    public UpdateSessionStatusCommandHandler(
        OrchestraDbContext context,
        IMediator mediator,
        ILogger<UpdateSessionStatusCommandHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AgentSession> Handle(UpdateSessionStatusCommand request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new ArgumentException("SessionId is required", nameof(request));
        }

        _logger.LogDebug("Updating session {SessionId} to status {NewStatus}",
            request.SessionId, request.NewStatus);

        try
        {
            // Поиск сессии по SessionId
            var session = await _context.AgentSessions
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

            if (session == null)
            {
                _logger.LogWarning("Session {SessionId} not found for status update", request.SessionId);
                throw new InvalidOperationException($"Session {request.SessionId} not found");
            }

            var oldStatus = session.Status;

            // Обновление статуса
            session.Status = request.NewStatus;
            session.UpdatedAt = DateTime.UtcNow;

            // Обновление ProcessId если указан
            if (request.ProcessId.HasValue)
            {
                session.ProcessId = request.ProcessId.Value;
            }

            // Накопление метрик
            if (request.AddCost.HasValue)
            {
                session.TotalCostUsd += request.AddCost.Value;
            }

            if (request.AddDurationMs.HasValue)
            {
                session.TotalDurationMs += request.AddDurationMs.Value;
            }

            // Инкремент счетчика сообщений (если есть новые метрики)
            if (request.AddCost.HasValue || request.AddDurationMs.HasValue)
            {
                session.MessageCount++;
            }

            // Обработка специфичных статусов
            switch (request.NewStatus)
            {
                case SessionStatus.Paused:
                    session.LastResumedAt = DateTime.UtcNow;
                    session.ProcessId = null;
                    _logger.LogInformation("Session {SessionId} paused", request.SessionId);
                    break;

                case SessionStatus.Closed:
                    session.ClosedAt = DateTime.UtcNow;
                    session.ProcessId = null;
                    _logger.LogInformation("Session {SessionId} closed. Total: {Cost} USD, {Duration} ms, {Messages} messages",
                        request.SessionId, session.TotalCostUsd, session.TotalDurationMs, session.MessageCount);
                    break;

                case SessionStatus.Active:
                    if (oldStatus == SessionStatus.Paused)
                    {
                        session.LastResumedAt = DateTime.UtcNow;
                        _logger.LogInformation("Session {SessionId} resumed", request.SessionId);
                    }
                    break;

                case SessionStatus.Error:
                    _logger.LogWarning("Session {SessionId} entered error state", request.SessionId);
                    break;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Session {SessionId} updated successfully. Status: {OldStatus} -> {NewStatus}",
                request.SessionId, oldStatus, request.NewStatus);

            // Публикация события
            await _mediator.Publish(new AgentSessionStatusChangedEvent(
                session.SessionId,
                oldStatus,
                request.NewStatus,
                session), cancellationToken);

            return session;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update session {SessionId} status: {Error}",
                request.SessionId, ex.Message);
            throw new InvalidOperationException($"Session status update failed: {ex.Message}", ex);
        }
    }
}
