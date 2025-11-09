using Orchestra.Core.Models;
using Orchestra.Core.Services.Connectors;

namespace Orchestra.Core.Commands.Permissions;

/// <summary>
/// Команда для запроса одобрения от человека при возникновении permission_denials
/// </summary>
/// <remarks>
/// Эта команда срабатывает когда Claude Code требует разрешение на выполнение инструмента.
/// Человек может одобрить запрос (например, через Telegram), и тогда сессия возобновляется
/// с флагом --dangerously-skip-permissions.
/// </remarks>
public record RequestHumanApprovalCommand(
    string AgentId,
    string SessionId,
    List<PermissionDenial> PermissionDenials,
    string OriginalCommand,
    DateTime RequestedAt
) : ICommand<RequestHumanApprovalResult>;

/// <summary>
/// Результат запроса одобрения от человека
/// </summary>
public record RequestHumanApprovalResult(
    bool Success,
    string Message,
    string? ApprovalId = null,
    DateTime? ApprovedAt = null,
    string? ApprovedBy = null,
    bool IsApproved = false
);
