using MediatR;

namespace Orchestra.Core.Events;

/// <summary>
/// Базовый интерфейс для событий домена
/// </summary>
public interface IEvent : INotification
{
    /// <summary>
    /// Время возникновения события
    /// </summary>
    DateTime Timestamp { get; }
}