namespace Orchestra.Core.Options.Dtos;

/// <summary>
/// Результат тестирования подключения к Telegram
/// </summary>
/// <param name="Success">Успешность подключения</param>
/// <param name="Message">Сообщение о результате тестирования</param>
/// <param name="ErrorCode">Код ошибки (если произошла ошибка)</param>
public record TestTelegramConnectionResult(
    bool Success,
    string Message,
    string? ErrorCode = null);
