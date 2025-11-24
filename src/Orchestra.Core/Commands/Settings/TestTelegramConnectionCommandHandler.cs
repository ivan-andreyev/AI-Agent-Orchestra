using System.Net.Http;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Options.Dtos;

namespace Orchestra.Core.Commands.Settings;

/// <summary>
/// Обработчик команды тестирования подключения к Telegram
/// </summary>
public class TestTelegramConnectionCommandHandler : IRequestHandler<TestTelegramConnectionCommand, TestTelegramConnectionResult>
{
    private readonly ILogger<TestTelegramConnectionCommandHandler> _logger;

    /// <summary>
    /// Базовый URL Telegram Bot API
    /// </summary>
    private const string TelegramApiUrl = "https://api.telegram.org/bot";

    /// <summary>
    /// Таймаут для тестового подключения (10 секунд)
    /// </summary>
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Инициализирует новый экземпляр обработчика
    /// </summary>
    /// <param name="logger">Логгер</param>
    public TestTelegramConnectionCommandHandler(ILogger<TestTelegramConnectionCommandHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Обрабатывает команду тестирования подключения к Telegram
    /// </summary>
    /// <param name="request">Команда с параметрами подключения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат тестирования подключения</returns>
    public async Task<TestTelegramConnectionResult> Handle(TestTelegramConnectionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Тестирование подключения к Telegram");

        // Валидация входных данных
        if (string.IsNullOrWhiteSpace(request.BotToken))
        {
            return new TestTelegramConnectionResult(
                Success: false,
                Message: "Токен бота не указан",
                ErrorCode: "MISSING_BOT_TOKEN");
        }

        if (string.IsNullOrWhiteSpace(request.ChatId))
        {
            return new TestTelegramConnectionResult(
                Success: false,
                Message: "Chat ID не указан",
                ErrorCode: "MISSING_CHAT_ID");
        }

        // Проверяем, не является ли токен маскированным
        if (request.BotToken.Contains('*'))
        {
            return new TestTelegramConnectionResult(
                Success: false,
                Message: "Токен бота маскирован. Введите полный токен для тестирования.",
                ErrorCode: "MASKED_TOKEN");
        }

        try
        {
            // Создаём новый HttpClient с коротким таймаутом для тестирования
            using var httpClient = new HttpClient
            {
                Timeout = ConnectionTimeout
            };

            // Сначала проверяем валидность токена через getMe
            var getMeUrl = $"{TelegramApiUrl}{request.BotToken}/getMe";

            _logger.LogDebug("Проверка токена бота через getMe");

            var getMeResponse = await httpClient.GetAsync(getMeUrl, cancellationToken);

            if (!getMeResponse.IsSuccessStatusCode)
            {
                var errorContent = await getMeResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Ошибка проверки токена бота: {StatusCode} - {Response}",
                    getMeResponse.StatusCode, errorContent);

                return new TestTelegramConnectionResult(
                    Success: false,
                    Message: $"Неверный токен бота. Проверьте токен и попробуйте снова. HTTP {(int)getMeResponse.StatusCode}",
                    ErrorCode: "INVALID_BOT_TOKEN");
            }

            // Токен валидный, отправляем тестовое сообщение
            var sendMessageUrl = $"{TelegramApiUrl}{request.BotToken}/sendMessage";

            var testMessage = new
            {
                chat_id = request.ChatId,
                text = "Test message from AI Agent Orchestra\n\nThis confirms that your Telegram bot is configured correctly.",
                parse_mode = "HTML"
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(testMessage),
                System.Text.Encoding.UTF8,
                "application/json");

            _logger.LogDebug("Отправка тестового сообщения в чат {ChatId}", request.ChatId);

            var sendResponse = await httpClient.PostAsync(sendMessageUrl, jsonContent, cancellationToken);
            var responseContent = await sendResponse.Content.ReadAsStringAsync(cancellationToken);

            if (sendResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Тестовое сообщение успешно отправлено в чат {ChatId}", request.ChatId);

                return new TestTelegramConnectionResult(
                    Success: true,
                    Message: "Подключение успешно! Тестовое сообщение отправлено в Telegram.");
            }
            else
            {
                _logger.LogWarning("Ошибка отправки тестового сообщения: {StatusCode} - {Response}",
                    sendResponse.StatusCode, responseContent);

                // Пытаемся распарсить ошибку от Telegram
                var errorMessage = ParseTelegramError(responseContent) ??
                    $"Не удалось отправить сообщение. HTTP {(int)sendResponse.StatusCode}";

                return new TestTelegramConnectionResult(
                    Success: false,
                    Message: errorMessage,
                    ErrorCode: "SEND_MESSAGE_FAILED");
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Таймаут подключения к Telegram API");

            return new TestTelegramConnectionResult(
                Success: false,
                Message: "Таймаут подключения к Telegram API. Проверьте интернет-соединение.",
                ErrorCode: "TIMEOUT");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ошибка HTTP запроса к Telegram API");

            return new TestTelegramConnectionResult(
                Success: false,
                Message: $"Ошибка подключения к Telegram API: {ex.Message}",
                ErrorCode: "HTTP_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непредвиденная ошибка при тестировании подключения к Telegram");

            return new TestTelegramConnectionResult(
                Success: false,
                Message: $"Непредвиденная ошибка: {ex.Message}",
                ErrorCode: "UNKNOWN_ERROR");
        }
    }

    /// <summary>
    /// Парсит сообщение об ошибке из ответа Telegram API
    /// </summary>
    /// <param name="responseContent">JSON ответ от Telegram API</param>
    /// <returns>Человекочитаемое сообщение об ошибке или null</returns>
    private static string? ParseTelegramError(string responseContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            if (doc.RootElement.TryGetProperty("description", out var descriptionElement))
            {
                var description = descriptionElement.GetString();

                // Переводим типичные ошибки Telegram на русский
                return description switch
                {
                    "Bad Request: chat not found" => "Чат не найден. Убедитесь, что вы отправили сообщение боту и используете правильный Chat ID.",
                    "Forbidden: bot was blocked by the user" => "Бот заблокирован пользователем. Разблокируйте бота и попробуйте снова.",
                    "Forbidden: bot can't send messages to bots" => "Бот не может отправлять сообщения другим ботам.",
                    _ => description
                };
            }
        }
        catch
        {
            // Игнорируем ошибки парсинга
        }

        return null;
    }
}
