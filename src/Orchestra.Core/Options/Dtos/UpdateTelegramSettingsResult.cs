namespace Orchestra.Core.Options.Dtos;

/// <summary>
/// Результат обновления настроек Telegram
/// </summary>
/// <param name="Success">Успешность обновления</param>
/// <param name="Message">Сообщение о результате обновления</param>
/// <param name="Settings">Обновлённые настройки (если обновление успешно)</param>
public record UpdateTelegramSettingsResult(
    bool Success,
    string Message,
    TelegramSettingsDto? Settings = null);
