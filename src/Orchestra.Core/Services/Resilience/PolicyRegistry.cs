using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Options;
using Polly;
using Polly.Retry;

namespace Orchestra.Core.Services.Resilience;

/// <summary>
/// Реализация реестра политик отказоустойчивости
/// </summary>
/// <remarks>
/// Использует Polly 8.x для создания retry policies с exponential backoff и jitter
/// </remarks>
public class PolicyRegistry : IPolicyRegistry
{
    private readonly ILogger<PolicyRegistry> _logger;
    private readonly IOptions<TelegramRetryOptions> _options;

    /// <summary>
    /// Конструктор PolicyRegistry
    /// </summary>
    public PolicyRegistry(
        ILogger<PolicyRegistry> logger,
        IOptions<TelegramRetryOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _logger.LogInformation(
            "PolicyRegistry initialized. MaxRetries: {MaxRetries}, InitialDelay: {InitialDelay}ms, MaxDelay: {MaxDelay}ms, Jitter: {Jitter}",
            _options.Value.MaxRetryAttempts,
            _options.Value.InitialDelayMs,
            _options.Value.MaxDelayMs,
            _options.Value.JitterEnabled);
    }

    /// <summary>
    /// Получает retry policy для Telegram API вызовов
    /// </summary>
    public ResiliencePipeline<HttpResponseMessage> GetTelegramRetryPolicy()
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = _options.Value.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = _options.Value.JitterEnabled,
                Delay = TimeSpan.FromMilliseconds(_options.Value.InitialDelayMs),
                MaxDelay = TimeSpan.FromMilliseconds(_options.Value.MaxDelayMs),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(response =>
                    {
                        var shouldRetry = !response.IsSuccessStatusCode &&
                                        _options.Value.RetryOn.Contains((int)response.StatusCode);
                        return shouldRetry;
                    }),
                OnRetry = args => LogRetryAttempt(args, "Telegram API")
            })
            .Build();
    }

    /// <summary>
    /// Получает generic retry policy с типизированным результатом
    /// </summary>
    public ResiliencePipeline<T> GetGenericRetryPolicy<T>()
    {
        return new ResiliencePipelineBuilder<T>()
            .AddRetry(new RetryStrategyOptions<T>
            {
                MaxRetryAttempts = _options.Value.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = _options.Value.JitterEnabled,
                Delay = TimeSpan.FromMilliseconds(_options.Value.InitialDelayMs),
                MaxDelay = TimeSpan.FromMilliseconds(_options.Value.MaxDelayMs),
                ShouldHandle = new PredicateBuilder<T>()
                    .Handle<Exception>(),
                OnRetry = args => LogRetryAttempt(args, "Operation")
            })
            .Build();
    }

    /// <summary>
    /// Логирует попытку повтора операции
    /// </summary>
    private ValueTask LogRetryAttempt<TResult>(OnRetryArguments<TResult> args, string operationName)
    {
        var exception = args.Outcome.Exception;
        var attemptNumber = args.AttemptNumber + 1;
        var delay = args.RetryDelay;

        if (exception != null)
        {
            _logger.LogWarning(
                exception,
                "{OperationName} failed with exception. Waiting {Delay}ms before retry {AttemptNumber}/{MaxRetries}",
                operationName,
                delay.TotalMilliseconds,
                attemptNumber,
                _options.Value.MaxRetryAttempts);
        }
        else if (args.Outcome.Result is HttpResponseMessage response)
        {
            _logger.LogWarning(
                "{OperationName} call failed with status {StatusCode}. Waiting {Delay}ms before retry {AttemptNumber}/{MaxRetries}",
                operationName,
                response.StatusCode,
                delay.TotalMilliseconds,
                attemptNumber,
                _options.Value.MaxRetryAttempts);
        }
        else
        {
            _logger.LogWarning(
                "{OperationName} failed. Waiting {Delay}ms before retry {AttemptNumber}/{MaxRetries}",
                operationName,
                delay.TotalMilliseconds,
                attemptNumber,
                _options.Value.MaxRetryAttempts);
        }

        return ValueTask.CompletedTask;
    }
}
