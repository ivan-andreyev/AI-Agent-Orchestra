using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Options;
using Orchestra.Core.Options.Dtos;

namespace Orchestra.Core.Commands.Settings;

/// <summary>
/// Обработчик запроса на получение настроек Telegram бота
/// </summary>
public class GetTelegramSettingsQueryHandler : IRequestHandler<GetTelegramSettingsQuery, TelegramSettingsDto>
{
    private readonly IOptions<TelegramEscalationOptions> _options;
    private readonly ILogger<GetTelegramSettingsQueryHandler> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр обработчика
    /// </summary>
    /// <param name="options">Конфигурация Telegram эскалации</param>
    /// <param name="logger">Логгер</param>
    public GetTelegramSettingsQueryHandler(
        IOptions<TelegramEscalationOptions> options,
        ILogger<GetTelegramSettingsQueryHandler> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Обрабатывает запрос на получение настроек Telegram
    /// </summary>
    /// <param name="request">Запрос</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>DTO с настройками Telegram (токен замаскирован)</returns>
    public Task<TelegramSettingsDto> Handle(GetTelegramSettingsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Получение настроек Telegram");

        var options = _options.Value;

        // Маскируем токен бота для безопасности - показываем только последние 10 символов
        var maskedToken = MaskBotToken(options.BotToken);

        var dto = new TelegramSettingsDto(
            BotTokenMasked: maskedToken,
            ChatId: options.ChatId ?? string.Empty,
            Enabled: options.Enabled,
            LastUpdatedAt: DateTime.UtcNow);

        _logger.LogInformation("Настройки Telegram получены. Enabled: {Enabled}, ChatId configured: {ChatIdConfigured}",
            options.Enabled,
            !string.IsNullOrEmpty(options.ChatId));

        return Task.FromResult(dto);
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
