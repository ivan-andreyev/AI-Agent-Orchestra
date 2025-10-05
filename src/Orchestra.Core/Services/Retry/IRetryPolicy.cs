using Microsoft.Extensions.Logging;

namespace Orchestra.Core.Services.Retry;

/// <summary>
/// Результат проверки на возможность повтора
/// </summary>
public record RetryDecision
{
    /// <summary>
    /// Можно ли повторить операцию
    /// </summary>
    public bool ShouldRetry { get; init; }

    /// <summary>
    /// Задержка перед повтором
    /// </summary>
    public TimeSpan Delay { get; init; }

    /// <summary>
    /// Причина решения
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Создает решение о повторе
    /// </summary>
    public static RetryDecision Retry(TimeSpan delay, string? reason = null) =>
        new() { ShouldRetry = true, Delay = delay, Reason = reason };

    /// <summary>
    /// Создает решение об отказе от повтора
    /// </summary>
    public static RetryDecision DoNotRetry(string? reason = null) =>
        new() { ShouldRetry = false, Delay = TimeSpan.Zero, Reason = reason };
}

/// <summary>
/// Контекст retry операции
/// </summary>
public record RetryContext
{
    /// <summary>
    /// Номер текущей попытки (начиная с 0)
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// Последнее исключение (если было)
    /// </summary>
    public Exception? LastException { get; init; }

    /// <summary>
    /// Время начала первой попытки
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Общее затраченное время на все попытки
    /// </summary>
    public TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;
}

/// <summary>
/// Интерфейс политики повтора операций
/// </summary>
/// <remarks>
/// <para>
/// Определяет стратегию повтора неудачных операций с поддержкой
/// различных алгоритмов backoff (экспоненциальный, линейный, фиксированный).
/// </para>
/// <para>
/// <b>Реализации:</b>
/// <list type="bullet">
/// <item><description>ExponentialBackoffRetryPolicy - Экспоненциальная задержка с jitter</description></item>
/// <item><description>LinearBackoffRetryPolicy - Линейная задержка</description></item>
/// <item><description>FixedDelayRetryPolicy - Фиксированная задержка</description></item>
/// <item><description>NoRetryPolicy - Без повторов</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IRetryPolicy
{
    /// <summary>
    /// Определяет, нужно ли повторить операцию
    /// </summary>
    /// <param name="context">Контекст retry операции</param>
    /// <returns>Решение о повторе операции</returns>
    RetryDecision ShouldRetry(RetryContext context);

    /// <summary>
    /// Проверяет, является ли исключение ретраибельным
    /// </summary>
    /// <param name="exception">Исключение для проверки</param>
    /// <returns>true если исключение можно повторить, иначе false</returns>
    bool IsRetryableException(Exception exception);

    /// <summary>
    /// Выполняет операцию с retry логикой
    /// </summary>
    /// <typeparam name="T">Тип результата</typeparam>
    /// <param name="operation">Операция для выполнения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат операции</returns>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет операцию с retry логикой
    /// </summary>
    /// <param name="operation">Операция для выполнения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Базовый абстрактный класс для политик повтора
/// </summary>
public abstract class RetryPolicyBase : IRetryPolicy
{
    protected readonly ILogger Logger;
    protected readonly int MaxAttempts;

    protected RetryPolicyBase(ILogger logger, int maxAttempts)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        MaxAttempts = maxAttempts;

        if (maxAttempts < 0)
        {
            throw new ArgumentException("Max attempts must be non-negative", nameof(maxAttempts));
        }
    }

    /// <inheritdoc />
    public abstract RetryDecision ShouldRetry(RetryContext context);

    /// <inheritdoc />
    public virtual bool IsRetryableException(Exception exception)
    {
        return exception is HttpRequestException ||
               exception is TimeoutException ||
               exception is IOException ||
               exception is OperationCanceledException ||
               (exception is InvalidOperationException && exception.Message.Contains("process", StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        var context = new RetryContext
        {
            AttemptNumber = 0,
            StartTime = DateTime.UtcNow
        };

        Exception? lastException = null;

        for (int attempt = 0; attempt <= MaxAttempts; attempt++)
        {
            context = context with { AttemptNumber = attempt, LastException = lastException };

            try
            {
                Logger.LogDebug("Executing operation (attempt {Attempt}/{MaxAttempts})",
                    attempt + 1, MaxAttempts + 1);

                var result = await operation(cancellationToken);

                if (attempt > 0)
                {
                    Logger.LogInformation("Operation succeeded after {Attempts} attempt(s)",
                        attempt + 1);
                }

                return result;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning("Operation cancelled");
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (!IsRetryableException(ex))
                {
                    Logger.LogWarning(ex, "Non-retryable exception occurred: {ExceptionType}",
                        ex.GetType().Name);
                    throw;
                }

                var decision = ShouldRetry(context);

                if (!decision.ShouldRetry)
                {
                    Logger.LogError(ex, "Retry policy decided not to retry. Reason: {Reason}",
                        decision.Reason ?? "Max attempts reached");
                    throw;
                }

                Logger.LogWarning(ex,
                    "Operation failed (attempt {Attempt}/{MaxAttempts}), retrying after {Delay}ms. Reason: {Reason}",
                    attempt + 1, MaxAttempts + 1, decision.Delay.TotalMilliseconds, decision.Reason ?? "Retryable exception");

                await Task.Delay(decision.Delay, cancellationToken);
            }
        }

        // Should never reach here, but keeping for safety
        Logger.LogError(lastException, "All {MaxAttempts} retry attempts exhausted",
            MaxAttempts);
        throw new InvalidOperationException(
            $"All {MaxAttempts} retry attempts exhausted",
            lastException);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async (ct) =>
        {
            await operation(ct);
            return true; // Dummy return value
        }, cancellationToken);
    }
}
