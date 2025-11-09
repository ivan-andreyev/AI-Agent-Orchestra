using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Events.Permissions;
using Orchestra.Core.Services;

namespace Orchestra.Core.Commands.Permissions;

/// <summary>
/// Обработчик команды отмены approval request
/// </summary>
/// <remarks>
/// Обработчик:
/// 1. Находит approval request в БД
/// 2. Проверяет, что он ещё не обработан/не отменён
/// 3. Обновляет статус на Cancelled
/// 4. Устанавливает CancellationReason
/// 5. Логирует отмену
/// 6. Публикует ApprovalCancelledEvent
/// 7. Опционально отправляет уведомление через Telegram
/// </remarks>
public class CancelApprovalCommandHandler : IRequestHandler<CancelApprovalCommand, Unit>
{
    private readonly ILogger<CancelApprovalCommandHandler> _logger;
    private readonly OrchestraDbContext _context;
    private readonly IMediator _mediator;
    private readonly ITelegramEscalationService _telegramService;

    public CancelApprovalCommandHandler(
        ILogger<CancelApprovalCommandHandler> logger,
        OrchestraDbContext context,
        IMediator mediator,
        ITelegramEscalationService telegramService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _telegramService = telegramService ?? throw new ArgumentNullException(nameof(telegramService));
    }

    /// <summary>
    /// Обрабатывает отмену approval request
    /// </summary>
    public async Task<Unit> Handle(
        CancelApprovalCommand request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            _logger.LogInformation(
                "Отмена approval request. ApprovalId: {ApprovalId}, Reason: {Reason}",
                request.ApprovalId,
                request.Reason);

            // Находим approval request в БД
            var approval = await _context.ApprovalRequests
                .FirstOrDefaultAsync(a => a.ApprovalId == request.ApprovalId, cancellationToken);

            if (approval == null)
            {
                _logger.LogWarning(
                    "Approval request не найден в БД. ApprovalId: {ApprovalId}",
                    request.ApprovalId);
                return Unit.Value;
            }

            // Проверяем, что approval ещё не обработан
            if (approval.Status != ApprovalStatus.Pending)
            {
                _logger.LogWarning(
                    "Approval request уже обработан. ApprovalId: {ApprovalId}, Status: {Status}",
                    request.ApprovalId,
                    approval.Status);
                return Unit.Value;
            }

            // Обновляем статус на Cancelled
            approval.Status = ApprovalStatus.Cancelled;
            approval.CancellationReason = request.Reason;
            approval.UpdatedAt = DateTime.UtcNow;

            _context.ApprovalRequests.Update(approval);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Approval request отменён. ApprovalId: {ApprovalId}, Reason: {Reason}",
                request.ApprovalId,
                request.Reason);

            // Отправляем уведомление через Telegram
            try
            {
                var notificationMessage = $"⏰ Approval request {request.ApprovalId} отменён.\n" +
                                         $"Причина: {request.Reason}\n" +
                                         $"SessionId: {approval.SessionId}";

                await _telegramService.SendEscalationAsync(
                    approval.AgentId,
                    notificationMessage,
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Не удалось отправить уведомление об отмене через Telegram. ApprovalId: {ApprovalId}",
                    request.ApprovalId);
                // Не фейлим операцию из-за проблем с Telegram
            }

            // Публикуем событие отмены
            await _mediator.Publish(
                new ApprovalCancelledEvent(request.ApprovalId, request.Reason),
                cancellationToken);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при отмене approval request. ApprovalId: {ApprovalId}",
                request.ApprovalId);
            throw;
        }
    }
}
