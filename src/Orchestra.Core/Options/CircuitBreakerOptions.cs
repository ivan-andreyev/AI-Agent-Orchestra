using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Options;

/// <summary>
/// Конфигурационные опции для circuit breaker
/// </summary>
/// <remarks>
/// Определяет параметры для circuit breaker паттерна:
/// - Пороги для открытия circuit (failure rate, consecutive failures)
/// - Длительность периода "открытого" состояния
/// - Параметры sampling window
/// </remarks>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Порог процента ошибок для открытия circuit (0.0-1.0)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 0.5 (50% ошибок)
    /// Circuit открывается когда failure rate >= threshold
    /// </remarks>
    [Range(0.1, 1.0)]
    public double FailureRateThreshold { get; set; } = 0.5;

    /// <summary>
    /// Порог последовательных ошибок для открытия circuit (1-100)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 5 ошибок подряд
    /// Circuit открывается когда N ошибок подряд достигнуто
    /// </remarks>
    [Range(1, 100)]
    public int ConsecutiveFailuresThreshold { get; set; } = 5;

    /// <summary>
    /// Минимальное количество запросов для расчёта failure rate (1-1000)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 10 запросов
    /// Failure rate не учитывается пока не достигнут минимальный throughput
    /// </remarks>
    [Range(1, 1000)]
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Длительность периода sampling в секундах (5-300)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 30 секунд
    /// Окно для расчёта failure rate
    /// </remarks>
    [Range(5, 300)]
    public int SamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Длительность открытого состояния circuit в секундах (5-600)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 30 секунд
    /// После этого времени circuit переходит в Half-Open состояние
    /// </remarks>
    [Range(5, 600)]
    public int BreakDurationSeconds { get; set; } = 30;
}
