using Polly;

namespace Orchestra.Core.Services.Resilience;

/// <summary>
/// Интерфейс сервиса circuit breaker для graceful degradation
/// </summary>
public interface ICircuitBreakerPolicyService
{
    /// <summary>
    /// Получает текущее состояние circuit breaker
    /// </summary>
    CircuitState CurrentState { get; }

    /// <summary>
    /// Получает количество последовательных ошибок
    /// </summary>
    int ConsecutiveFailures { get; }

    /// <summary>
    /// Получает общее количество ошибок
    /// </summary>
    int TotalFailures { get; }

    /// <summary>
    /// Получает общее количество успешных запросов
    /// </summary>
    int TotalSuccesses { get; }

    /// <summary>
    /// Проверяет, открыт ли circuit
    /// </summary>
    bool IsCircuitOpen { get; }

    /// <summary>
    /// Проверяет, находится ли circuit в полуоткрытом состоянии
    /// </summary>
    bool IsCircuitHalfOpen { get; }

    /// <summary>
    /// Время, когда circuit был открыт
    /// </summary>
    DateTime? CircuitOpenedAt { get; }

    /// <summary>
    /// Получает HTTP circuit breaker pipeline
    /// </summary>
    ResiliencePipeline<HttpResponseMessage> GetHttpCircuitBreaker();

    /// <summary>
    /// Получает generic circuit breaker pipeline
    /// </summary>
    ResiliencePipeline GetGenericCircuitBreaker();

    /// <summary>
    /// Выполняет операцию через circuit breaker с fallback
    /// </summary>
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        TResult fallbackValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет операцию через circuit breaker без fallback
    /// </summary>
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет void операцию через circuit breaker
    /// </summary>
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает успешный результат
    /// </summary>
    void RecordSuccess();

    /// <summary>
    /// Записывает неуспешный результат
    /// </summary>
    void RecordFailure(Exception? exception = null);

    /// <summary>
    /// Сбрасывает circuit breaker в начальное состояние
    /// </summary>
    void Reset();

    /// <summary>
    /// Принудительно открывает circuit breaker
    /// </summary>
    void ForceOpen();

    /// <summary>
    /// Принудительно закрывает circuit breaker
    /// </summary>
    void ForceClose();

    /// <summary>
    /// Получает статистику circuit breaker
    /// </summary>
    CircuitBreakerStatistics GetStatistics();
}
