using Orchestra.Core.Options.Dtos;

namespace Orchestra.Core.Commands.Settings;

/// <summary>
/// Команда для тестирования подключения к Telegram боту
/// </summary>
/// <param name="BotToken">Токен бота для тестирования</param>
/// <param name="ChatId">ID чата для отправки тестового сообщения</param>
public record TestTelegramConnectionCommand(
    string BotToken,
    string ChatId) : ICommand<TestTelegramConnectionResult>;
