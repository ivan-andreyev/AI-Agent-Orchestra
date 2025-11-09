using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Commands.Sessions;

/// <summary>
/// Команда для обновления статуса и метрик сессии агента
/// </summary>
public record UpdateSessionStatusCommand(
    string SessionId,
    SessionStatus NewStatus,
    int? ProcessId = null,
    double? AddCost = null,
    long? AddDurationMs = null
) : ICommand<AgentSession>;
