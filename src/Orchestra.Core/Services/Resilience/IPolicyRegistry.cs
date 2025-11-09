using Polly;

namespace Orchestra.Core.Services.Resilience;

/// <summary>
/// Реестр политик отказоустойчивости (resilience policies)
/// </summary>
/// <remarks>
/// Централизованное хранилище Polly policies для различных сценариев
/// </remarks>
public interface IPolicyRegistry
{
    /// <summary>
    /// Получает retry policy для Telegram API вызовов
    /// </summary>
    /// <returns>
    /// Polly ResiliencePipeline с настроенным exponential backoff, jitter и retry на transient failures
    /// </returns>
    ResiliencePipeline<HttpResponseMessage> GetTelegramRetryPolicy();

    /// <summary>
    /// Получает generic retry policy с типизированным результатом
    /// </summary>
    /// <typeparam name="T">Тип результата операции</typeparam>
    /// <returns>
    /// ResiliencePipeline для произвольного типа с retry логикой
    /// </returns>
    ResiliencePipeline<T> GetGenericRetryPolicy<T>();
}
