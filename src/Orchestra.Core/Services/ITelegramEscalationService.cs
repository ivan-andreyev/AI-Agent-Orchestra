namespace Orchestra.Core.Services;

/// <summary>
/// Сервис для эскалации уведомлений через Telegram
/// </summary>
/// <remarks>
/// Используется для отправки запросов на одобрение permission_denials человеческому оператору
/// через Telegram бота, например когда агент требует разрешение на выполнение инструмента.
/// </remarks>
public interface ITelegramEscalationService
{
    /// <summary>
    /// Отправляет эскалационное уведомление в Telegram
    /// </summary>
    /// <param name="agentId">Идентификатор агента, запросившего одобрение</param>
    /// <param name="message">Сообщение для отправки оператору</param>
    /// <param name="metadata">Дополнительные метаданные для кнопок одобрения/отказа</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True если сообщение успешно отправлено, false если произошла ошибка</returns>
    Task<bool> SendEscalationAsync(
        string agentId,
        string message,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет доступность Telegram API и конфигурацию
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True если Telegram доступен и конфигурирован, false иначе</returns>
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает статус конфигурации Telegram эскалации
    /// </summary>
    /// <returns>Текстовое описание статуса (enabled/disabled/configured)</returns>
    string GetConfigurationStatus();
}
