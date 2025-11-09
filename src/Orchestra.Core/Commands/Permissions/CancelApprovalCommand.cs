using MediatR;

namespace Orchestra.Core.Commands.Permissions;

/// <summary>
/// Команда для отмены approval request (по таймауту или вручную)
/// </summary>
/// <remarks>
/// Эта команда срабатывает когда:
/// 1. Approval request истёк по таймауту (вызывается ApprovalTimeoutService)
/// 2. Оператор вручную отменяет approval request
///
/// Handler:
/// - Обновляет статус в БД на Cancelled
/// - Устанавливает CancellationReason
/// - Логирует отмену
/// - Публикует ApprovalCancelledEvent
/// - Опционально отправляет уведомление через Telegram
/// </remarks>
public record CancelApprovalCommand(
    string ApprovalId,
    string Reason = "Timeout"
) : IRequest<Unit>;
