namespace Orchestra.Core.Commands.Agents;

/// <summary>
/// Команда для обновления SessionId агента
/// </summary>
public class UpdateAgentSessionIdCommand : ICommand<UpdateAgentSessionIdResult>
{
    /// <summary>
    /// Идентификатор агента
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Новый SessionId агента (UUID из .claude/projects)
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Результат обновления SessionId агента
/// </summary>
public class UpdateAgentSessionIdResult
{
    /// <summary>
    /// Успешность обновления
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }
}
