using Orchestra.Core.Models;

namespace Orchestra.Core.Services.Connectors;

/// <summary>
/// Аргументы события изменения статуса подключения к внешнему агенту
/// </summary>
public class ConnectionStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// Предыдущий статус подключения
    /// </summary>
    public ConnectionStatus OldStatus { get; init; }

    /// <summary>
    /// Новый статус подключения
    /// </summary>
    public ConnectionStatus NewStatus { get; init; }

    /// <summary>
    /// Идентификатор агента
    /// </summary>
    public string AgentId { get; init; } = string.Empty;

    /// <summary>
    /// Причина изменения статуса
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Дополнительные метаданные
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Время изменения статуса
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
