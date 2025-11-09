using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Commands.Sessions;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Queries.Sessions;
using Orchestra.Core.Services.Metrics;

namespace Orchestra.Core.Commands.Permissions;

/// <summary>
/// Обработчик команды обработки одобрения от человека
/// </summary>
/// <remarks>
/// Обработчик:
/// 1. Проверяет валидность ApprovalId
/// 2. Проверяет, что approval не expired и не cancelled
/// 3. Получает сессию из БД по SessionId
/// 4. Устанавливает флаг для возобновления с --dangerously-skip-permissions
/// 5. Логирует одобрение и кто его дал
/// </remarks>
public class ProcessHumanApprovalCommandHandler : IRequestHandler<ProcessHumanApprovalCommand, ProcessHumanApprovalResult>
{
    private readonly ILogger<ProcessHumanApprovalCommandHandler> _logger;
    private readonly OrchestraDbContext _context;
    private readonly IMediator _mediator;
    private readonly EscalationMetricsService _metricsService;

    public ProcessHumanApprovalCommandHandler(
        ILogger<ProcessHumanApprovalCommandHandler> logger,
        OrchestraDbContext context,
        IMediator mediator,
        EscalationMetricsService metricsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
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

            // НОВАЯ ПРОВЕРКА: Проверяем approval request в БД
            var approvalRequest = await _context.ApprovalRequests
                .FirstOrDefaultAsync(a => a.ApprovalId == request.ApprovalId, cancellationToken);

            if (approvalRequest == null)
            {
                _logger.LogWarning(
                    "Approval request не найден в БД. ApprovalId: {ApprovalId}",
                    request.ApprovalId);

                return new ProcessHumanApprovalResult(
                    Success: false,
                    Message: $"Approval request не найден: {request.ApprovalId}");
            }

            // НОВАЯ ПРОВЕРКА: Проверяем, что approval не отменён
            if (approvalRequest.Status == ApprovalStatus.Cancelled)
            {
                _logger.LogWarning(
                    "Approval request уже отменён. ApprovalId: {ApprovalId}, Reason: {Reason}",
                    request.ApprovalId,
                    approvalRequest.CancellationReason);

                return new ProcessHumanApprovalResult(
                    Success: false,
                    Message: $"Approval request отменён: {approvalRequest.CancellationReason}");
            }

            // НОВАЯ ПРОВЕРКА: Проверяем, что approval не истёк
            if (approvalRequest.ExpiresAt <= DateTime.UtcNow)
            {
                var timeoutDuration = DateTime.UtcNow - approvalRequest.CreatedAt;
                _logger.LogWarning(
                    "Approval request истёк. ApprovalId: {ApprovalId}, ExpiresAt: {ExpiresAt}, Duration: {Duration:mm\\:ss}",
                    request.ApprovalId,
                    approvalRequest.ExpiresAt,
                    timeoutDuration);

                return new ProcessHumanApprovalResult(
                    Success: false,
                    Message: $"Approval request истёк (timeout: {timeoutDuration:mm\\:ss})");
            }

            if (!request.Approved)
            {
                _logger.LogWarning(
                    "Одобрение отклонено для SessionId: {SessionId}. Причина: {Reason}",
                    request.SessionId,
                    request.ApprovalNotes ?? "Не указана");

                // Обновляем статус на Rejected
                approvalRequest.Status = ApprovalStatus.Rejected;
                approvalRequest.UpdatedAt = DateTime.UtcNow;
                _context.ApprovalRequests.Update(approvalRequest);
                await _context.SaveChangesAsync(cancellationToken);

                // Record metrics: Approval rejected
                try
                {
                    var responseTime = (DateTime.UtcNow - approvalRequest.CreatedAt).TotalSeconds;
                    _metricsService.RecordApprovalRejected(request.ApprovalId, responseTime);
                    _metricsService.RecordEscalationDequeued(request.ApprovalId);

                    _logger.LogDebug(
                        "Метрики отклонения approval записаны. ApprovalId: {ApprovalId}, ResponseTime: {ResponseTime:F2}s",
                        request.ApprovalId,
                        responseTime);
                }
                catch (Exception metricsEx)
                {
                    _logger.LogWarning(
                        metricsEx,
                        "Не удалось записать метрики отклонения approval. ApprovalId: {ApprovalId}",
                        request.ApprovalId);
                }

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

            // Обновляем approval request - помечаем как Approved
            approvalRequest.Status = ApprovalStatus.Approved;
            approvalRequest.ApprovedAt = DateTime.UtcNow;
            approvalRequest.ApprovedBy = request.ApprovedBy;
            approvalRequest.UpdatedAt = DateTime.UtcNow;

            // Reload session from our context to avoid entity tracking issues
            var trackedSession = await _context.AgentSessions
                .FirstOrDefaultAsync(s => s.Id == session.Id, cancellationToken);

            if (trackedSession != null)
            {
                // Обновляем сессию - помечаем, что она может возобновиться с пропуском разрешений
                // (это информация, которую будет использовать ClaudeCodeSubprocessConnector.ResumeSessionAsync)
                trackedSession.UpdatedAt = DateTime.UtcNow;
                trackedSession.LastResumedAt = DateTime.UtcNow;
            }

            // Сохраняем изменения
            _context.ApprovalRequests.Update(approvalRequest);
            if (trackedSession != null)
            {
                _context.AgentSessions.Update(trackedSession);
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Record metrics: Approval accepted
            try
            {
                var responseTime = (DateTime.UtcNow - approvalRequest.CreatedAt).TotalSeconds;
                _metricsService.RecordApprovalAccepted(request.ApprovalId, responseTime);
                _metricsService.RecordEscalationDequeued(request.ApprovalId);

                _logger.LogInformation(
                    "Одобрение обработано успешно. Сессия подготовлена к возобновлению. " +
                    "SessionId: {SessionId}, ApprovedBy: {ApprovedBy}, ResponseTime: {ResponseTime:F2}s",
                    request.SessionId,
                    request.ApprovedBy,
                    responseTime);
            }
            catch (Exception metricsEx)
            {
                _logger.LogWarning(
                    metricsEx,
                    "Не удалось записать метрики принятия approval. ApprovalId: {ApprovalId}",
                    request.ApprovalId);

                _logger.LogInformation(
                    "Одобрение обработано успешно. Сессия подготовлена к возобновлению. " +
                    "SessionId: {SessionId}, ApprovedBy: {ApprovedBy}",
                    request.SessionId,
                    request.ApprovedBy);
            }

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
