using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Orchestra.Core.Services;

/// <summary>
/// Реализация сервиса эскалации через Telegram
/// </summary>
/// <remarks>
/// Сервис отправляет запросы на одобрение через Telegram бота.
/// Требует конфигурацию BotToken и ChatId в appsettings.json
/// </remarks>
public class TelegramEscalationService : ITelegramEscalationService
{
    private readonly ILogger<TelegramEscalationService> _logger;
    private readonly TelegramEscalationOptions _options;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Базовый URL Telegram Bot API
    /// </summary>
    private const string TelegramApiUrl = "https://api.telegram.org/bot";

    public TelegramEscalationService(
        ILogger<TelegramEscalationService> logger,
        IOptions<TelegramEscalationOptions> options,
        HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        _logger.LogInformation(
            "TelegramEscalationService инициализирован. Enabled: {Enabled}, Configured: {Configured}",
            _options.Enabled,
            !string.IsNullOrEmpty(_options.BotToken));
    }

    /// <summary>
    /// Отправляет эскалационное сообщение в Telegram
    /// </summary>
    public async Task<bool> SendEscalationAsync(
        string agentId,
        string message,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        // Проверяем конфигурацию
        if (!_options.Enabled)
        {
            _logger.LogWarning("TelegramEscalationService отключен в конфигурации");
            return false;
        }

        if (string.IsNullOrEmpty(_options.BotToken) || string.IsNullOrEmpty(_options.ChatId))
        {
            _logger.LogError(
                "TelegramEscalationService не сконфигурирован. BotToken: {BotTokenExists}, ChatId: {ChatIdExists}",
                !string.IsNullOrEmpty(_options.BotToken),
                !string.IsNullOrEmpty(_options.ChatId));
            return false;
        }

        try
        {
            var escapedMessage = EscapeMarkdownV2(message);
            var telegramMessage = $"*⚠️ Требуется одобрение от оператора*\n\n" +
                                 $"Агент: `{EscapeMarkdownV2(agentId)}`\n\n" +
                                 $"{escapedMessage}";

            // Подготавливаем payload для Telegram API
            var requestPayload = new
            {
                chat_id = _options.ChatId,
                text = telegramMessage,
                parse_mode = "MarkdownV2",
                disable_notification = false
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestPayload),
                System.Text.Encoding.UTF8,
                "application/json");

            var url = $"{TelegramApiUrl}{_options.BotToken}/sendMessage";
            var response = await _httpClient.PostAsync(url, jsonContent, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Эскалационное сообщение успешно отправлено в Telegram для агента {AgentId}",
                    agentId);
                return true;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Ошибка при отправке сообщения в Telegram. StatusCode: {StatusCode}, Response: {Response}",
                response.StatusCode,
                responseContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Исключение при отправке эскалационного сообщения в Telegram для агента {AgentId}",
                agentId);
            return false;
        }
    }

    /// <summary>
    /// Проверяет доступность Telegram API
    /// </summary>
    public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        if (string.IsNullOrEmpty(_options.BotToken))
        {
            return false;
        }

        try
        {
            var url = $"{TelegramApiUrl}{_options.BotToken}/getMe";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при проверке подключения к Telegram API");
            return false;
        }
    }

    /// <summary>
    /// Получает статус конфигурации сервиса
    /// </summary>
    public string GetConfigurationStatus()
    {
        if (!_options.Enabled)
            return "disabled";

        if (string.IsNullOrEmpty(_options.BotToken) || string.IsNullOrEmpty(_options.ChatId))
            return "not_configured";

        return "configured";
    }

    /// <summary>
    /// Экранирует специальные символы для Markdown V2 формата Telegram
    /// </summary>
    private static string EscapeMarkdownV2(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Специальные символы в MarkdownV2, которые нужно экранировать
        var specialChars = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };

        var result = text;
        foreach (var ch in specialChars)
        {
            result = result.Replace(ch.ToString(), $"\\{ch}");
        }

        return result;
    }
}

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
