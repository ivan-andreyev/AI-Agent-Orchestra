using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Orchestra.Core.Models.Workflow;

/// <summary>
/// Определяет политику повторных попыток для выполнения задач в workflow
/// </summary>
/// <param name="MaxRetryCount">Максимальное количество попыток выполнения</param>
/// <param name="BaseDelay">Базовая задержка между попытками</param>
/// <param name="MaxDelay">Максимальная задержка между попытками</param>
/// <param name="BackoffMultiplier">Множитель для экспоненциального увеличения задержки</param>
/// <param name="RetryableExceptions">Типы исключений, при которых следует повторить попытку</param>
/// <param name="RetryCondition">Условие для определения необходимости повтора</param>
public record RetryPolicy(
    [Range(1, 10)]
    [property: JsonPropertyName("maxRetryCount")]
    int MaxRetryCount = 3,

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    [property: JsonPropertyName("baseDelay")]
    TimeSpan BaseDelay = default,

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    [property: JsonPropertyName("maxDelay")]
    TimeSpan MaxDelay = default,

    [Range(1.0, 10.0)]
    [property: JsonPropertyName("backoffMultiplier")]
    double BackoffMultiplier = 2.0,

    [property: JsonPropertyName("retryableExceptions")]
    IReadOnlyList<string>? RetryableExceptions = null,

    [property: JsonPropertyName("retryCondition")]
    string? RetryCondition = null
)
{
    /// <summary>
    /// Конструктор по умолчанию с разумными значениями
    /// </summary>
    public RetryPolicy() : this(
        MaxRetryCount: 3,
        BaseDelay: TimeSpan.FromSeconds(1),
        MaxDelay: TimeSpan.FromMinutes(5),
        BackoffMultiplier: 2.0,
        RetryableExceptions: null,
        RetryCondition: null)
    {
    }

    /// <summary>
    /// Конструктор для простых случаев с только количеством попыток
    /// </summary>
    /// <param name="maxRetryCount">Максимальное количество попыток</param>
    public RetryPolicy(int maxRetryCount) : this(
        MaxRetryCount: maxRetryCount,
        BaseDelay: TimeSpan.FromSeconds(1),
        MaxDelay: TimeSpan.FromMinutes(5),
        BackoffMultiplier: 2.0,
        RetryableExceptions: null,
        RetryCondition: null)
    {
    }

    /// <summary>
    /// Получает эффективное значение BaseDelay (если 0, возвращает значение по умолчанию)
    /// </summary>
    public TimeSpan EffectiveBaseDelay => BaseDelay == TimeSpan.Zero ? TimeSpan.FromSeconds(1) : BaseDelay;

    /// <summary>
    /// Получает эффективное значение MaxDelay (если 0, возвращает значение по умолчанию)
    /// </summary>
    public TimeSpan EffectiveMaxDelay => MaxDelay == TimeSpan.Zero ? TimeSpan.FromMinutes(5) : MaxDelay;
}

/// <summary>
/// Результат выполнения попытки в рамках retry policy
/// </summary>
/// <param name="AttemptNumber">Номер попытки (начиная с 1)</param>
/// <param name="Success">Успешно ли выполнена попытка</param>
/// <param name="Exception">Исключение, возникшее при выполнении</param>
/// <param name="ExecutionTime">Время выполнения попытки</param>
/// <param name="NextRetryDelay">Задержка до следующей попытки</param>
public record RetryAttemptResult(
    int AttemptNumber,
    bool Success,
    Exception? Exception,
    TimeSpan ExecutionTime,
    TimeSpan? NextRetryDelay
);

/// <summary>
/// Результат выполнения всех попыток согласно retry policy
/// </summary>
/// <param name="Success">Успешно ли выполнена задача после всех попыток</param>
/// <param name="TotalAttempts">Общее количество попыток</param>
/// <param name="TotalExecutionTime">Общее время выполнения включая задержки</param>
/// <param name="Attempts">Детали всех попыток выполнения</param>
/// <param name="FinalException">Последнее исключение, если задача не выполнена</param>
public record RetryExecutionResult(
    bool Success,
    int TotalAttempts,
    TimeSpan TotalExecutionTime,
    IReadOnlyList<RetryAttemptResult> Attempts,
    Exception? FinalException
);
