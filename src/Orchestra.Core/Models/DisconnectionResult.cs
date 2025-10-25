namespace Orchestra.Core.Models;

/// <summary>
/// Результат отключения от внешнего агента
/// </summary>
public class DisconnectionResult
{
    /// <summary>
    /// Успешно ли выполнено отключение
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Сообщение об ошибке (если отключение неуспешно)
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Время отключения
    /// </summary>
    public DateTime DisconnectedAt { get; init; }

    /// <summary>
    /// Причина отключения
    /// </summary>
    public DisconnectionReason Reason { get; init; }

    /// <summary>
    /// Дополнительные метаданные
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Создает успешный результат отключения
    /// </summary>
    public static DisconnectionResult CreateSuccess(DisconnectionReason reason, Dictionary<string, object>? metadata = null)
    {
        return new DisconnectionResult
        {
            Success = true,
            Reason = reason,
            DisconnectedAt = DateTime.UtcNow,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Создает неуспешный результат отключения
    /// </summary>
    public static DisconnectionResult CreateFailure(string errorMessage, Dictionary<string, object>? metadata = null)
    {
        return new DisconnectionResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Reason = DisconnectionReason.Error,
            DisconnectedAt = DateTime.UtcNow,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }
}

/// <summary>
/// Причина отключения от агента
/// </summary>
public enum DisconnectionReason
{
    /// <summary>
    /// Пользователь явно запросил отключение
    /// </summary>
    UserRequested,

    /// <summary>
    /// Внешний агент закрылся или прекратил работу
    /// </summary>
    AgentClosed,

    /// <summary>
    /// Таймаут неактивности сессии
    /// </summary>
    Timeout,

    /// <summary>
    /// Ошибка во время работы
    /// </summary>
    Error,

    /// <summary>
    /// Потеря соединения
    /// </summary>
    ConnectionLost
}
