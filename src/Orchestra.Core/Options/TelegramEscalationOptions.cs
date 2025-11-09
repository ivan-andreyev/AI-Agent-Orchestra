namespace Orchestra.Core.Options;

/// <summary>
/// Конфигурационные опции для Telegram эскалации
/// </summary>
public class TelegramEscalationOptions
{
    /// <summary>
    /// Токен Telegram бота (получается от BotFather)
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// ID чата для отправки сообщений (или user ID)
    /// </summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>
    /// Включена ли эскалация через Telegram
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Таймаут для HTTP запросов к Telegram API (в миллисекундах)
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Количество повторных попыток при ошибке отправки
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
