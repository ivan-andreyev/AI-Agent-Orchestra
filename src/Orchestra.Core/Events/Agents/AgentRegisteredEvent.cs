using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Events.Agents;

/// <summary>
/// Событие регистрации нового агента в системе
/// </summary>
public class AgentRegisteredEvent : IEvent
{
    /// <summary>
    /// Зарегистрированный агент
    /// </summary>
    public Agent Agent { get; }

    /// <summary>
    /// Время события
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Инициализирует новое событие регистрации агента
    /// </summary>
    /// <param name="agent">Зарегистрированный агент</param>
    public AgentRegisteredEvent(Agent agent)
    {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Событие обновления существующего агента
/// </summary>
public class AgentUpdatedEvent : IEvent
{
    /// <summary>
    /// Обновленный агент
    /// </summary>
    public Agent Agent { get; }

    /// <summary>
    /// Время события
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Инициализирует новое событие обновления агента
    /// </summary>
    /// <param name="agent">Обновленный агент</param>
    public AgentUpdatedEvent(Agent agent)
    {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Событие изменения статуса агента
/// </summary>
public class AgentStatusChangedEvent : IEvent
{
    /// <summary>
    /// Идентификатор агента
    /// </summary>
    public string AgentId { get; }

    /// <summary>
    /// Предыдущий статус
    /// </summary>
    public Orchestra.Core.Data.Entities.AgentStatus PreviousStatus { get; }

    /// <summary>
    /// Новый статус
    /// </summary>
    public Orchestra.Core.Data.Entities.AgentStatus NewStatus { get; }

    /// <summary>
    /// Время события
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Дополнительная информация о статусе
    /// </summary>
    public string? StatusMessage { get; }

    /// <summary>
    /// Инициализирует новое событие изменения статуса агента
    /// </summary>
    /// <param name="agentId">Идентификатор агента</param>
    /// <param name="previousStatus">Предыдущий статус</param>
    /// <param name="newStatus">Новый статус</param>
    /// <param name="statusMessage">Дополнительная информация</param>
    public AgentStatusChangedEvent(string agentId, Orchestra.Core.Data.Entities.AgentStatus previousStatus, Orchestra.Core.Data.Entities.AgentStatus newStatus, string? statusMessage = null)
    {
        AgentId = agentId ?? throw new ArgumentNullException(nameof(agentId));
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        StatusMessage = statusMessage;
        Timestamp = DateTime.UtcNow;
    }
}