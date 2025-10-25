namespace Orchestra.Core.Models;

/// <summary>
/// Результат выполнения команды в внешнем агенте
/// </summary>
public class CommandResult
{
    /// <summary>
    /// Успешно ли отправлена команда
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Отправленная команда
    /// </summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>
    /// Сообщение об ошибке (если команда не отправлена)
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Время отправки команды
    /// </summary>
    public DateTime SentAt { get; init; }

    /// <summary>
    /// Дополнительные метаданные
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Создает успешный результат отправки команды
    /// </summary>
    public static CommandResult CreateSuccess(string command, Dictionary<string, object>? metadata = null)
    {
        return new CommandResult
        {
            Success = true,
            Command = command,
            SentAt = DateTime.UtcNow,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Создает неуспешный результат отправки команды
    /// </summary>
    public static CommandResult CreateFailure(string command, string errorMessage, Dictionary<string, object>? metadata = null)
    {
        return new CommandResult
        {
            Success = false,
            Command = command,
            ErrorMessage = errorMessage,
            SentAt = DateTime.UtcNow,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }
}
