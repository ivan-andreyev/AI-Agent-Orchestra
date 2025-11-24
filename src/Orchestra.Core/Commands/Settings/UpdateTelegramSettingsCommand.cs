using Orchestra.Core.Options.Dtos;

namespace Orchestra.Core.Commands.Settings;

/// <summary>
/// Команда для обновления настроек Telegram бота
/// </summary>
/// <param name="BotToken">Токен бота (полный, не маскированный)</param>
/// <param name="ChatId">ID чата или пользователя</param>
/// <param name="Enabled">Включить ли интеграцию</param>
public record UpdateTelegramSettingsCommand(
    string BotToken,
    string ChatId,
    bool Enabled) : ICommand<UpdateTelegramSettingsResult>;
