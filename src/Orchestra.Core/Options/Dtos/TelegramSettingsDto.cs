namespace Orchestra.Core.Options.Dtos;

/// <summary>
/// DTO для передачи настроек Telegram между слоями приложения
/// </summary>
/// <param name="BotTokenMasked">Маскированный токен бота (отображаются только последние 10 символов)</param>
/// <param name="ChatId">ID чата или пользователя для отправки сообщений</param>
/// <param name="Enabled">Включена ли интеграция с Telegram</param>
/// <param name="LastUpdatedAt">Дата последнего обновления настроек</param>
public record TelegramSettingsDto(
    string BotTokenMasked,
    string ChatId,
    bool Enabled,
    DateTime LastUpdatedAt);
