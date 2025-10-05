using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Commands.Agents;

/// <summary>
/// Команда для удаления агента из системы
/// </summary>
public class DeleteAgentCommand : ICommand<DeleteAgentResult>
{
    /// <summary>
    /// Уникальный идентификатор агента для удаления
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Флаг жёсткого удаления (true) или мягкого удаления/деактивации (false)
    /// </summary>
    public bool HardDelete { get; set; } = false;
}

/// <summary>
/// Результат удаления агента
/// </summary>
public class DeleteAgentResult
{
    /// <summary>
    /// Успешность удаления
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Удалённый агент (для логирования и событий)
    /// </summary>
    public Agent? DeletedAgent { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Был ли агент удалён жёстко (true) или деактивирован (false)
    /// </summary>
    public bool WasHardDeleted { get; set; }
}
