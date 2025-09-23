using System.Diagnostics;
using Orchestra.Core.Models.Workflow;

namespace Orchestra.Core.Services;

/// <summary>
/// Сервис для выполнения задач с политикой повторных попыток
/// </summary>
public class RetryExecutor
{
    private readonly ExpressionEvaluator _expressionEvaluator;

    public RetryExecutor(ExpressionEvaluator expressionEvaluator)
    {
        _expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));
    }

    /// <summary>
    /// Выполняет задачу с применением политики повторных попыток
    /// </summary>
    /// <typeparam name="T">Тип результата выполнения задачи</typeparam>
    /// <param name="taskFunc">Функция выполнения задачи</param>
    /// <param name="retryPolicy">Политика повторных попыток</param>
    /// <param name="context">Контекст выполнения для вычисления условий</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат выполнения с деталями всех попыток</returns>
    public async Task<(T? Result, RetryExecutionResult RetryResult)> ExecuteWithRetryAsync<T>(
        Func<Task<T>> taskFunc,
        RetryPolicy retryPolicy,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var attempts = new List<RetryAttemptResult>();
        var totalExecutionTimer = Stopwatch.StartNew();
        Exception? lastException = null;
        T? result = default;

        for (int attempt = 1; attempt <= retryPolicy.MaxRetryCount; attempt++)
        {
            // Проверяем отмену перед попыткой
            cancellationToken.ThrowIfCancellationRequested();
            var attemptTimer = Stopwatch.StartNew();

            try
            {
                result = await taskFunc();
                attemptTimer.Stop();

                attempts.Add(new RetryAttemptResult(
                    AttemptNumber: attempt,
                    Success: true,
                    Exception: null,
                    ExecutionTime: attemptTimer.Elapsed,
                    NextRetryDelay: null
                ));

                totalExecutionTimer.Stop();

                return (result, new RetryExecutionResult(
                    Success: true,
                    TotalAttempts: attempt,
                    TotalExecutionTime: totalExecutionTimer.Elapsed,
                    Attempts: attempts,
                    FinalException: null
                ));
            }
            catch (Exception ex)
            {
                attemptTimer.Stop();
                lastException = ex;

                // Проверяем, подлежит ли исключение повтору
                if (!ShouldRetryForException(ex, retryPolicy))
                {
                    attempts.Add(new RetryAttemptResult(
                        AttemptNumber: attempt,
                        Success: false,
                        Exception: ex,
                        ExecutionTime: attemptTimer.Elapsed,
                        NextRetryDelay: null
                    ));
                    break;
                }

                // Проверяем условие повтора если оно задано
                if (!await ShouldRetryForCondition(ex, retryPolicy, context))
                {
                    attempts.Add(new RetryAttemptResult(
                        AttemptNumber: attempt,
                        Success: false,
                        Exception: ex,
                        ExecutionTime: attemptTimer.Elapsed,
                        NextRetryDelay: null
                    ));
                    break;
                }

                // Вычисляем задержку для следующей попытки
                TimeSpan? nextDelay = attempt < retryPolicy.MaxRetryCount
                    ? CalculateNextDelay(attempt, retryPolicy)
                    : null;

                attempts.Add(new RetryAttemptResult(
                    AttemptNumber: attempt,
                    Success: false,
                    Exception: ex,
                    ExecutionTime: attemptTimer.Elapsed,
                    NextRetryDelay: nextDelay
                ));

                // Если это не последняя попытка, ждем перед следующей
                if (attempt < retryPolicy.MaxRetryCount && nextDelay.HasValue)
                {
                    await Task.Delay(nextDelay.Value, cancellationToken);
                }
            }
        }

        totalExecutionTimer.Stop();

        return (default(T), new RetryExecutionResult(
            Success: false,
            TotalAttempts: attempts.Count,
            TotalExecutionTime: totalExecutionTimer.Elapsed,
            Attempts: attempts,
            FinalException: lastException
        ));
    }

    /// <summary>
    /// Вычисляет задержку до следующей попытки с экспоненциальным backoff
    /// </summary>
    /// <param name="attemptNumber">Номер попытки (начиная с 1)</param>
    /// <param name="retryPolicy">Политика повторных попыток</param>
    /// <returns>Время задержки до следующей попытки</returns>
    public TimeSpan CalculateNextDelay(int attemptNumber, RetryPolicy retryPolicy)
    {
        // Экспоненциальный backoff: BaseDelay * (BackoffMultiplier ^ (attemptNumber - 1))
        var delay = TimeSpan.FromMilliseconds(
            retryPolicy.EffectiveBaseDelay.TotalMilliseconds *
            Math.Pow(retryPolicy.BackoffMultiplier, attemptNumber - 1)
        );

        // Ограничиваем максимальной задержкой
        if (delay > retryPolicy.EffectiveMaxDelay)
        {
            delay = retryPolicy.EffectiveMaxDelay;
        }

        return delay;
    }

    /// <summary>
    /// Проверяет, должно ли исключение привести к повторной попытке
    /// </summary>
    /// <param name="exception">Возникшее исключение</param>
    /// <param name="retryPolicy">Политика повторных попыток</param>
    /// <returns>true, если следует повторить попытку</returns>
    private bool ShouldRetryForException(Exception exception, RetryPolicy retryPolicy)
    {
        // Если список исключений не задан, повторяем для всех исключений
        if (retryPolicy.RetryableExceptions == null || retryPolicy.RetryableExceptions.Count == 0)
        {
            return true;
        }

        var exceptionTypeName = exception.GetType().Name;
        var exceptionTypeFullName = exception.GetType().FullName;

        return retryPolicy.RetryableExceptions.Any(retryableType =>
            string.Equals(retryableType, exceptionTypeName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(retryableType, exceptionTypeFullName, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Проверяет условие повтора если оно задано
    /// </summary>
    /// <param name="exception">Возникшее исключение</param>
    /// <param name="retryPolicy">Политика повторных попыток</param>
    /// <param name="context">Контекст выполнения</param>
    /// <returns>true, если следует повторить попытку</returns>
    private async Task<bool> ShouldRetryForCondition(Exception exception, RetryPolicy retryPolicy, Dictionary<string, object>? context)
    {
        if (string.IsNullOrWhiteSpace(retryPolicy.RetryCondition))
        {
            return true;
        }

        // Создаем контекст с информацией об исключении
        var evaluationContext = new Dictionary<string, object>(context ?? new Dictionary<string, object>())
        {
            ["exception"] = exception,
            ["exception_type"] = exception.GetType().Name,
            ["exception_message"] = exception.Message
        };

        try
        {
            var workflowContext = new WorkflowExecutionContext
            {
                Variables = new Dictionary<string, object>(),
                StepResults = evaluationContext
            };
            return await _expressionEvaluator.EvaluateAsync(retryPolicy.RetryCondition, workflowContext);
        }
        catch
        {
            // Если не удается вычислить условие, по умолчанию не повторяем
            return false;
        }
    }
}