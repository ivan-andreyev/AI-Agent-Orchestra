using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Commands.Agents;

/// <summary>
/// Команда для обновления статуса агента
/// </summary>
public class UpdateAgentStatusCommand : ICommand<UpdateAgentStatusResult>
{
    /// <summary>
    /// Идентификатор агента
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Новый статус агента
    /// </summary>
    public Orchestra.Core.Data.Entities.AgentStatus Status { get; set; }

    /// <summary>
    /// Текущая задача агента (опционально)
    /// </summary>
    public string? CurrentTask { get; set; }

    /// <summary>
    /// Время последней активности агента
    /// </summary>
    public DateTime? LastPing { get; set; }

    /// <summary>
    /// Дополнительная информация о статусе
    /// </summary>
    public string? StatusMessage { get; set; }
}

/// <summary>
/// Результат обновления статуса агента
/// </summary>
public class UpdateAgentStatusResult
{
    /// <summary>
    /// Успешность обновления
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Обновленный агент
    /// </summary>
    public Agent? Agent { get; set; }

    /// <summary>
    /// Предыдущий статус агента
    /// </summary>
    public Orchestra.Core.Data.Entities.AgentStatus? PreviousStatus { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Был ли изменен статус
    /// </summary>
    public bool StatusChanged { get; set; }
}