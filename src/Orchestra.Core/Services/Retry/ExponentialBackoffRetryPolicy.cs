using Microsoft.Extensions.Logging;

namespace Orchestra.Core.Services.Retry;

/// <summary>
/// Политика повтора с экспоненциальной задержкой и jitter
/// </summary>
/// <remarks>
/// <para>
/// Реализует экспоненциальный backoff: delay = baseDelay * 2^attemptNumber + jitter.
/// Jitter добавляется для предотвращения "thundering herd" проблемы.
/// </para>
/// <para>
/// <b>Формула задержки:</b>
/// <code>
/// delay = baseDelay * 2^attemptNumber * (1 + random(0, jitterFactor))
/// </code>
/// </para>
/// <para>
/// <b>Пример использования:</b>
/// <code>
/// var policy = new ExponentialBackoffRetryPolicy(
///     logger,
///     maxAttempts: 3,
///     baseDelay: TimeSpan.FromSeconds(1),
///     maxDelay: TimeSpan.FromSeconds(30),
///     jitterFactor: 0.3);
///
/// var result = await policy.ExecuteAsync(async ct =>
/// {
///     return await SomeOperationAsync(ct);
/// });
/// </code>
/// </para>
/// </remarks>
public class ExponentialBackoffRetryPolicy : RetryPolicyBase
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly double _jitterFactor;

    /// <summary>
    /// Инициализирует новый экземпляр ExponentialBackoffRetryPolicy
    /// </summary>
    /// <param name="logger">Логгер для отслеживания операций</param>
    /// <param name="maxAttempts">Максимальное количество попыток</param>
    /// <param name="baseDelay">Базовая задержка перед первым повтором</param>
    /// <param name="maxDelay">Максимальная задержка между попытками</param>
    /// <param name="jitterFactor">Фактор jitter (0.0 - 1.0), по умолчанию 0.3 (30%)</param>
    public ExponentialBackoffRetryPolicy(
        ILogger<ExponentialBackoffRetryPolicy> logger,
        int maxAttempts,
        TimeSpan baseDelay,
        TimeSpan? maxDelay = null,
        double jitterFactor = 0.3)
        : base(logger, maxAttempts)
    {
        if (baseDelay < TimeSpan.Zero)
        {
            throw new ArgumentException("Base delay must be non-negative", nameof(baseDelay));
        }

        if (jitterFactor < 0.0 || jitterFactor > 1.0)
        {
            throw new ArgumentException("Jitter factor must be between 0.0 and 1.0", nameof(jitterFactor));
        }

        _baseDelay = baseDelay;
        _maxDelay = maxDelay ?? TimeSpan.FromMinutes(1);
        _jitterFactor = jitterFactor;

        if (_maxDelay < _baseDelay)
        {
            throw new ArgumentException("Max delay cannot be less than base delay", nameof(maxDelay));
        }
    }

    /// <inheritdoc />
    public override RetryDecision ShouldRetry(RetryContext context)
    {
        if (context.AttemptNumber >= MaxAttempts)
        {
            return RetryDecision.DoNotRetry($"Max attempts ({MaxAttempts}) reached");
        }

        var delay = CalculateDelay(context.AttemptNumber);

        return RetryDecision.Retry(
            delay,
            $"Exponential backoff: attempt {context.AttemptNumber + 1}, delay {delay.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Вычисляет задержку с экспоненциальным backoff и jitter
    /// </summary>
    private TimeSpan CalculateDelay(int attemptNumber)
    {
        var baseDelayMs = _baseDelay.TotalMilliseconds;

        // Exponential backoff: baseDelay * 2^attemptNumber
        var exponentialDelayMs = baseDelayMs * Math.Pow(2, attemptNumber);

        // Add jitter (0% to jitterFactor% of delay)
        var jitterMs = Random.Shared.NextDouble() * _jitterFactor * exponentialDelayMs;
        var totalDelayMs = exponentialDelayMs + jitterMs;

        // Cap at max delay
        var cappedDelayMs = Math.Min(totalDelayMs, _maxDelay.TotalMilliseconds);

        return TimeSpan.FromMilliseconds(cappedDelayMs);
    }
}

/// <summary>
/// Политика повтора с фиксированной задержкой
/// </summary>
public class FixedDelayRetryPolicy : RetryPolicyBase
{
    private readonly TimeSpan _delay;

    /// <summary>
    /// Инициализирует новый экземпляр FixedDelayRetryPolicy
    /// </summary>
    public FixedDelayRetryPolicy(
        ILogger<FixedDelayRetryPolicy> logger,
        int maxAttempts,
        TimeSpan delay)
        : base(logger, maxAttempts)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentException("Delay must be non-negative", nameof(delay));
        }

        _delay = delay;
    }

    /// <inheritdoc />
    public override RetryDecision ShouldRetry(RetryContext context)
    {
        if (context.AttemptNumber >= MaxAttempts)
        {
            return RetryDecision.DoNotRetry($"Max attempts ({MaxAttempts}) reached");
        }

        return RetryDecision.Retry(_delay, $"Fixed delay retry: {_delay.TotalMilliseconds}ms");
    }
}

/// <summary>
/// Политика повтора с линейной задержкой
/// </summary>
public class LinearBackoffRetryPolicy : RetryPolicyBase
{
    private readonly TimeSpan _incrementDelay;
    private readonly TimeSpan _maxDelay;

    /// <summary>
    /// Инициализирует новый экземпляр LinearBackoffRetryPolicy
    /// </summary>
    public LinearBackoffRetryPolicy(
        ILogger<LinearBackoffRetryPolicy> logger,
        int maxAttempts,
        TimeSpan incrementDelay,
        TimeSpan? maxDelay = null)
        : base(logger, maxAttempts)
    {
        if (incrementDelay < TimeSpan.Zero)
        {
            throw new ArgumentException("Increment delay must be non-negative", nameof(incrementDelay));
        }

        _incrementDelay = incrementDelay;
        _maxDelay = maxDelay ?? TimeSpan.FromMinutes(1);
    }

    /// <inheritdoc />
    public override RetryDecision ShouldRetry(RetryContext context)
    {
        if (context.AttemptNumber >= MaxAttempts)
        {
            return RetryDecision.DoNotRetry($"Max attempts ({MaxAttempts}) reached");
        }

        var delay = TimeSpan.FromMilliseconds(
            Math.Min(
                _incrementDelay.TotalMilliseconds * (context.AttemptNumber + 1),
                _maxDelay.TotalMilliseconds));

        return RetryDecision.Retry(delay, $"Linear backoff: attempt {context.AttemptNumber + 1}");
    }
}

/// <summary>
/// Политика без повторов (fail-fast)
/// </summary>
public class NoRetryPolicy : RetryPolicyBase
{
    /// <summary>
    /// Инициализирует новый экземпляр NoRetryPolicy
    /// </summary>
    public NoRetryPolicy(ILogger<NoRetryPolicy> logger)
        : base(logger, 0)
    {
    }

    /// <inheritdoc />
    public override RetryDecision ShouldRetry(RetryContext context)
    {
        return RetryDecision.DoNotRetry("No retry policy - fail fast");
    }

    /// <inheritdoc />
    public override bool IsRetryableException(Exception exception)
    {
        return false; // Never retry
    }
}
