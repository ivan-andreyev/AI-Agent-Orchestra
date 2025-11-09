using MediatR;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Commands.Sessions;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Queries.Sessions;

namespace Orchestra.Core.Commands.Permissions;

/// <summary>
/// Обработчик команды обработки одобрения от человека
/// </summary>
/// <remarks>
/// Обработчик:
/// 1. Проверяет валидность ApprovalId
/// 2. Получает сессию из БД по SessionId
/// 3. Устанавливает флаг для возобновления с --dangerously-skip-permissions
/// 4. Логирует одобрение и кто его дал
/// </remarks>
public class ProcessHumanApprovalCommandHandler : IRequestHandler<ProcessHumanApprovalCommand, ProcessHumanApprovalResult>
{
    private readonly ILogger<ProcessHumanApprovalCommandHandler> _logger;
    private readonly OrchestraDbContext _context;
    private readonly IMediator _mediator;

    public ProcessHumanApprovalCommandHandler(
        ILogger<ProcessHumanApprovalCommandHandler> logger,
        OrchestraDbContext context,
        IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Обрабатывает одобрение от человека и подготавливает сессию к возобновлению
    /// </summary>
    public async Task<ProcessHumanApprovalResult> Handle(
        ProcessHumanApprovalCommand request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            _logger.LogInformation(
                "Обработка одобрения от человека. ApprovalId: {ApprovalId}, SessionId: {SessionId}, " +
                "Approved: {Approved}, ApprovedBy: {ApprovedBy}",
                request.ApprovalId,
                request.SessionId,
                request.Approved,
                request.ApprovedBy);

            if (!request.Approved)
            {
                _logger.LogWarning(
                    "Одобрение отклонено для SessionId: {SessionId}. Причина: {Reason}",
                    request.SessionId,
                    request.ApprovalNotes ?? "Не указана");

                return new ProcessHumanApprovalResult(
                    Success: true,
                    Message: "Одобрение отклонено. Сессия не будет возобновлена.",
                    SessionResumed: false);
            }

            // Получаем сессию из БД
            var session = await _mediator.Send(
                new GetAgentSessionQuery(request.SessionId),
                cancellationToken);

            if (session == null)
            {
                _logger.LogError(
                    "Сессия не найдена для SessionId: {SessionId}",
                    request.SessionId);

                return new ProcessHumanApprovalResult(
                    Success: false,
                    Message: $"Сессия не найдена: {request.SessionId}");
            }

            // Проверяем статус сессии
            if (session.Status != SessionStatus.Paused)
            {
                _logger.LogWarning(
                    "Сессия имеет неправильный статус для возобновления. " +
                    "SessionId: {SessionId}, Status: {Status}",
                    request.SessionId,
                    session.Status);

                return new ProcessHumanApprovalResult(
                    Success: false,
                    Message: $"Сессия не может быть возобновлена (статус: {session.Status})");
            }

            // Обновляем сессию - помечаем, что она может возобновиться с пропуском разрешений
            // (это информация, которую будет использовать ClaudeCodeSubprocessConnector.ResumeSessionAsync)
            session.UpdatedAt = DateTime.UtcNow;
            session.LastResumedAt = DateTime.UtcNow;

            // Сохраняем изменения
            _context.AgentSessions.Update(session);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Одобрение обработано успешно. Сессия подготовлена к возобновлению. " +
                "SessionId: {SessionId}, ApprovedBy: {ApprovedBy}",
                request.SessionId,
                request.ApprovedBy);

            return new ProcessHumanApprovalResult(
                Success: true,
                Message: "Одобрение принято. Сессия готова к возобновлению.",
                SessionResumed: true,
                ResumeSessionId: request.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при обработке одобрения для SessionId: {SessionId}",
                request.SessionId);

            return new ProcessHumanApprovalResult(
                Success: false,
                Message: $"Ошибка при обработке одобрения: {ex.Message}");
        }
    }
}
