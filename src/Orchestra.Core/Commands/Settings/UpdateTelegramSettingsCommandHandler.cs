using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Options;
using Orchestra.Core.Options.Dtos;
using System.Text.RegularExpressions;

namespace Orchestra.Core.Commands.Settings;

/// <summary>
/// Обработчик команды обновления настроек Telegram бота
/// </summary>
public partial class UpdateTelegramSettingsCommandHandler : IRequestHandler<UpdateTelegramSettingsCommand, UpdateTelegramSettingsResult>
{
    private readonly IOptionsMonitor<TelegramEscalationOptions> _optionsMonitor;
    private readonly ILogger<UpdateTelegramSettingsCommandHandler> _logger;

    /// <summary>
    /// Паттерн для валидации формата токена бота
    /// Формат: числовой_id:alphanumeric_string (например, 123456789:ABCdefGHIjklmNOPQRstUVWxyz)
    /// </summary>
    [GeneratedRegex(@"^\d{8,10}:[A-Za-z0-9_-]{35}$")]
    private static partial Regex BotTokenPattern();

    /// <summary>
    /// Паттерн для валидации ChatId (числовой, может быть отрицательным для групп)
    /// </summary>
    [GeneratedRegex(@"^-?\d+$")]
    private static partial Regex ChatIdPattern();

    /// <summary>
    /// Инициализирует новый экземпляр обработчика
    /// </summary>
    /// <param name="optionsMonitor">Монитор настроек Telegram эскалации</param>
    /// <param name="logger">Логгер</param>
    public UpdateTelegramSettingsCommandHandler(
        IOptionsMonitor<TelegramEscalationOptions> optionsMonitor,
        ILogger<UpdateTelegramSettingsCommandHandler> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Обрабатывает команду обновления настроек Telegram
    /// </summary>
    /// <param name="request">Команда с новыми настройками</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат обновления</returns>
    public Task<UpdateTelegramSettingsResult> Handle(UpdateTelegramSettingsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Обновление настроек Telegram. Enabled: {Enabled}", request.Enabled);

        // Валидация токена бота (если включено и токен предоставлен)
        if (request.Enabled && !string.IsNullOrEmpty(request.BotToken))
        {
            // Проверяем, не является ли это маскированным токеном
            if (request.BotToken.Contains('*'))
            {
                _logger.LogWarning("Попытка сохранить маскированный токен бота");
                return Task.FromResult(new UpdateTelegramSettingsResult(
                    Success: false,
                    Message: "Не удалось обновить настройки: токен бота маскирован. Введите полный токен.",
                    Settings: null));
            }

            // Мягкая валидация формата токена (не блокируем, если формат немного отличается)
            if (!IsValidBotTokenFormat(request.BotToken))
            {
                _logger.LogWarning("Токен бота имеет нестандартный формат, но будет сохранён");
            }
        }

        // Валидация ChatId
        if (request.Enabled && !string.IsNullOrEmpty(request.ChatId))
        {
            if (!ChatIdPattern().IsMatch(request.ChatId))
            {
                _logger.LogWarning("Неверный формат ChatId: {ChatId}", request.ChatId);
                return Task.FromResult(new UpdateTelegramSettingsResult(
                    Success: false,
                    Message: "Неверный формат Chat ID. Chat ID должен быть числом (отрицательным для групп).",
                    Settings: null));
            }
        }

        // Проверка обязательных полей при включении
        if (request.Enabled)
        {
            if (string.IsNullOrWhiteSpace(request.BotToken))
            {
                return Task.FromResult(new UpdateTelegramSettingsResult(
                    Success: false,
                    Message: "Токен бота обязателен при включении интеграции.",
                    Settings: null));
            }

            if (string.IsNullOrWhiteSpace(request.ChatId))
            {
                return Task.FromResult(new UpdateTelegramSettingsResult(
                    Success: false,
                    Message: "Chat ID обязателен при включении интеграции.",
                    Settings: null));
            }
        }

        // Обновляем настройки в runtime
        var currentOptions = _optionsMonitor.CurrentValue;
        currentOptions.BotToken = request.BotToken;
        currentOptions.ChatId = request.ChatId;
        currentOptions.Enabled = request.Enabled;

        _logger.LogInformation(
            "Настройки Telegram успешно обновлены. Enabled: {Enabled}, ChatId: {ChatId}",
            request.Enabled,
            !string.IsNullOrEmpty(request.ChatId) ? $"***{request.ChatId[^4..]}" : "(empty)");

        // Создаём DTO с маскированным токеном для ответа
        var maskedToken = MaskBotToken(request.BotToken);
        var settingsDto = new TelegramSettingsDto(
            BotTokenMasked: maskedToken,
            ChatId: request.ChatId,
            Enabled: request.Enabled,
            LastUpdatedAt: DateTime.UtcNow);

        return Task.FromResult(new UpdateTelegramSettingsResult(
            Success: true,
            Message: "Настройки Telegram успешно обновлены.",
            Settings: settingsDto));
    }

    /// <summary>
    /// Проверяет формат токена бота
    /// </summary>
    /// <param name="token">Токен для проверки</param>
    /// <returns>true если формат валидный</returns>
    private static bool IsValidBotTokenFormat(string token)
    {
        // Базовая проверка: токен должен содержать двоеточие и иметь определённую длину
        if (string.IsNullOrEmpty(token) || !token.Contains(':'))
        {
            return false;
        }

        var parts = token.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        // Первая часть должна быть числом
        if (!long.TryParse(parts[0], out _))
        {
            return false;
        }

        // Вторая часть должна быть непустой и содержать только разрешённые символы
        return !string.IsNullOrEmpty(parts[1]) && parts[1].All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
    }

    /// <summary>
    /// Маскирует токен бота, оставляя только последние 10 символов видимыми
    /// </summary>
    /// <param name="token">Оригинальный токен</param>
    /// <returns>Маскированный токен</returns>
    private static string MaskBotToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return string.Empty;
        }

        if (token.Length <= 10)
        {
            return new string('*', token.Length);
        }

        var visiblePart = token[^10..];
        var maskedPart = new string('*', token.Length - 10);

        return $"{maskedPart}{visiblePart}";
    }
}
