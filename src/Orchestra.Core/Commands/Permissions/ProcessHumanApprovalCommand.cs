namespace Orchestra.Core.Commands.Permissions;

/// <summary>
/// Команда для обработки одобрения от человека
/// </summary>
/// <remarks>
/// Эта команда срабатывает когда человек одобрил запрос разрешений через Telegram или другой канал.
/// После одобрения сессия Claude Code возобновляется с флагом --dangerously-skip-permissions.
/// </remarks>
public record ProcessHumanApprovalCommand(
    string ApprovalId,
    string SessionId,
    string AgentId,
    bool Approved,
    string ApprovedBy,
    DateTime ApprovedAt,
    string? ApprovalNotes = null
) : ICommand<ProcessHumanApprovalResult>;

/// <summary>
/// Результат обработки одобрения от человека
/// </summary>
public record ProcessHumanApprovalResult(
    bool Success,
    string Message,
    bool SessionResumed = false,
    string? ResumeSessionId = null
);
