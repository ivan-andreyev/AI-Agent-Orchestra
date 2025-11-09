using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Events.Sessions;

/// <summary>
/// Событие изменения статуса сессии агента
/// </summary>
public class AgentSessionStatusChangedEvent : IEvent
{
    /// <summary>
    /// Идентификатор сессии (SessionId UUID)
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Предыдущий статус сессии
    /// </summary>
    public SessionStatus PreviousStatus { get; }

    /// <summary>
    /// Новый статус сессии
    /// </summary>
    public SessionStatus NewStatus { get; }

    /// <summary>
    /// Обновлённая сессия (опционально)
    /// </summary>
    public AgentSession? Session { get; }

    /// <summary>
    /// Время события
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Инициализирует новое событие изменения статуса сессии
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="previousStatus">Предыдущий статус</param>
    /// <param name="newStatus">Новый статус</param>
    /// <param name="session">Обновлённая сессия (опционально)</param>
    public AgentSessionStatusChangedEvent(
        string sessionId,
        SessionStatus previousStatus,
        SessionStatus newStatus,
        AgentSession? session = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Session = session;
        Timestamp = DateTime.UtcNow;
    }
}
