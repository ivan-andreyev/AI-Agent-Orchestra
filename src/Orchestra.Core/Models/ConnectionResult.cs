namespace Orchestra.Core.Models;

/// <summary>
/// Результат попытки подключения к внешнему агенту
/// </summary>
public class ConnectionResult
{
    /// <summary>
    /// Успешно ли выполнено подключение
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Идентификатор сессии (если подключение успешно)
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Сообщение об ошибке (если подключение неуспешно)
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Дополнительные метаданные о подключении
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Время подключения
    /// </summary>
    public DateTime ConnectedAt { get; init; }

    /// <summary>
    /// Создает успешный результат подключения
    /// </summary>
    public static ConnectionResult CreateSuccess(string sessionId, Dictionary<string, object>? metadata = null)
    {
        return new ConnectionResult
        {
            Success = true,
            SessionId = sessionId,
            ConnectedAt = DateTime.UtcNow,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Создает неуспешный результат подключения
    /// </summary>
    public static ConnectionResult CreateFailure(string errorMessage, Dictionary<string, object>? metadata = null)
    {
        return new ConnectionResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ConnectedAt = DateTime.UtcNow,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }
}
