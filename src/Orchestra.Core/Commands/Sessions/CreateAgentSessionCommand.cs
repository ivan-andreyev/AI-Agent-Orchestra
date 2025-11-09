using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Commands.Sessions;

/// <summary>
/// Команда для создания новой сессии агента
/// </summary>
public record CreateAgentSessionCommand(
    string AgentId,
    string SessionId,
    string WorkingDirectory,
    int? ProcessId = null
) : ICommand<AgentSession>;
