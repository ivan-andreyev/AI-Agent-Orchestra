using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Options;

/// <summary>
/// Конфигурационные опции для retry политики Telegram API
/// </summary>
/// <remarks>
/// Определяет параметры для экспоненциального backoff с jitter
/// при повторных попытках вызовов Telegram API через Polly
/// </remarks>
public class TelegramRetryOptions
{
    /// <summary>
    /// Максимальное количество попыток повтора (1-10)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 3 попытки (первая + 2 retry)
    /// </remarks>
    [Range(1, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Начальная задержка в миллисекундах (100-60000ms)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 1000ms (1 секунда)
    /// Используется для экспоненциального backoff: delay = InitialDelayMs * 2^(attempt-1)
    /// </remarks>
    [Range(100, 60000)]
    public int InitialDelayMs { get; set; } = 1000;

    /// <summary>
    /// Максимальная задержка в миллисекундах (1000-300000ms)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 16000ms (16 секунд)
    /// Ограничивает максимальное время ожидания при экспоненциальном backoff
    /// </remarks>
    [Range(1000, 300000)]
    public int MaxDelayMs { get; set; } = 16000;

    /// <summary>
    /// Включена ли рандомизация (jitter) для задержек
    /// </summary>
    /// <remarks>
    /// По умолчанию: true
    /// Jitter помогает избежать thundering herd problem при одновременных retry от нескольких клиентов
    /// </remarks>
    public bool JitterEnabled { get; set; } = true;

    /// <summary>
    /// HTTP статус-коды, которые триггерят retry (по умолчанию: 429, 500, 502, 503, 504)
    /// </summary>
    /// <remarks>
    /// 429 - Too Many Requests (rate limiting)
    /// 500 - Internal Server Error (transient)
    /// 502 - Bad Gateway (transient)
    /// 503 - Service Unavailable (transient)
    /// 504 - Gateway Timeout (transient)
    /// </remarks>
    public int[] RetryOn { get; set; } = { 429, 500, 502, 503, 504 };
}
