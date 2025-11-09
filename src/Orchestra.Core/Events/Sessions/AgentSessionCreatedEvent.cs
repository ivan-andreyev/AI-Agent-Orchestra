using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Events.Sessions;

/// <summary>
/// Событие создания новой сессии агента
/// </summary>
public class AgentSessionCreatedEvent : IEvent
{
    /// <summary>
    /// Созданная сессия агента
    /// </summary>
    public AgentSession Session { get; }

    /// <summary>
    /// Время события
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Инициализирует новое событие создания сессии
    /// </summary>
    /// <param name="session">Созданная сессия</param>
    public AgentSessionCreatedEvent(AgentSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Timestamp = DateTime.UtcNow;
    }
}
