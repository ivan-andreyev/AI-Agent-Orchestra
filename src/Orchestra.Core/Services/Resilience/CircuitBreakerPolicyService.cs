using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Options;
using Polly;
using Polly.CircuitBreaker;

namespace Orchestra.Core.Services.Resilience;

/// <summary>
/// Сервис circuit breaker для graceful degradation
/// </summary>
/// <remarks>
/// Реализует паттерн Circuit Breaker с использованием Polly 8.x:
/// - Closed: Нормальная работа, запросы проходят
/// - Open: Circuit открыт после N ошибок, запросы fail fast
/// - Half-Open: Проверочный режим после таймаута
/// </remarks>
public class CircuitBreakerPolicyService : ICircuitBreakerPolicyService
{
    private readonly ILogger<CircuitBreakerPolicyService> _logger;
    private readonly CircuitBreakerOptions _options;
    private ResiliencePipeline<HttpResponseMessage>? _httpPipeline;
    private ResiliencePipeline? _genericPipeline;

    // Circuit breaker state tracking
    private CircuitState _currentState = CircuitState.Closed;
    private int _consecutiveFailures;
    private int _totalFailures;
    private int _totalSuccesses;
    private DateTime? _circuitOpenedAt;
    private readonly object _stateLock = new();

    /// <summary>
    /// Конструктор CircuitBreakerPolicyService
    /// </summary>
    public CircuitBreakerPolicyService(
        ILogger<CircuitBreakerPolicyService> logger,
        IOptions<CircuitBreakerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _logger.LogInformation(
            "CircuitBreakerPolicyService initialized. FailureThreshold: {FailureThreshold}, " +
            "ConsecutiveFailuresThreshold: {ConsecutiveFailuresThreshold}, BreakDuration: {BreakDuration}s",
            _options.FailureRateThreshold,
            _options.ConsecutiveFailuresThreshold,
            _options.BreakDurationSeconds);

        InitializePipelines();
    }

    /// <summary>
    /// Получает текущее состояние circuit breaker
    /// </summary>
    public CircuitState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                // Check if we should transition from Open to HalfOpen
                if (_currentState == CircuitState.Open && _circuitOpenedAt.HasValue)
                {
                    var breakDuration = TimeSpan.FromSeconds(_options.BreakDurationSeconds);
                    if (DateTime.UtcNow - _circuitOpenedAt.Value >= breakDuration)
                    {
                        TransitionState(CircuitState.HalfOpen);
                    }
                }
                return _currentState;
            }
        }
    }

    /// <summary>
    /// Получает количество последовательных ошибок
    /// </summary>
    public int ConsecutiveFailures
    {
        get
        {
            lock (_stateLock)
            {
                return _consecutiveFailures;
            }
        }
    }

    /// <summary>
    /// Получает общее количество ошибок
    /// </summary>
    public int TotalFailures
    {
        get
        {
            lock (_stateLock)
            {
                return _totalFailures;
            }
        }
    }

    /// <summary>
    /// Получает общее количество успешных запросов
    /// </summary>
    public int TotalSuccesses
    {
        get
        {
            lock (_stateLock)
            {
                return _totalSuccesses;
            }
        }
    }

    /// <summary>
    /// Проверяет, открыт ли circuit
    /// </summary>
    public bool IsCircuitOpen => CurrentState == CircuitState.Open;

    /// <summary>
    /// Проверяет, находится ли circuit в полуоткрытом состоянии
    /// </summary>
    public bool IsCircuitHalfOpen => CurrentState == CircuitState.HalfOpen;

    /// <summary>
    /// Время, когда circuit был открыт
    /// </summary>
    public DateTime? CircuitOpenedAt
    {
        get
        {
            lock (_stateLock)
            {
                return _circuitOpenedAt;
            }
        }
    }

    /// <summary>
    /// Получает HTTP circuit breaker pipeline
    /// </summary>
    public ResiliencePipeline<HttpResponseMessage> GetHttpCircuitBreaker()
    {
        return _httpPipeline ?? throw new InvalidOperationException("HTTP pipeline not initialized");
    }

    /// <summary>
    /// Получает generic circuit breaker pipeline
    /// </summary>
    public ResiliencePipeline GetGenericCircuitBreaker()
    {
        return _genericPipeline ?? throw new InvalidOperationException("Generic pipeline not initialized");
    }

    /// <summary>
    /// Выполняет операцию через circuit breaker
    /// </summary>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        TResult fallbackValue,
        CancellationToken cancellationToken = default)
    {
        // Check if circuit is open - return fallback
        if (CurrentState == CircuitState.Open)
        {
            _logger.LogWarning("Circuit breaker is OPEN. Returning fallback value");
            return fallbackValue;
        }

        try
        {
            var result = await operation(cancellationToken);
            RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(ex);

            // Check if we should return fallback after this failure
            if (CurrentState == CircuitState.Open)
            {
                _logger.LogWarning(ex, "Operation failed, circuit now OPEN. Returning fallback");
                return fallbackValue;
            }

            throw;
        }
    }

    /// <summary>
    /// Выполняет операцию через circuit breaker без fallback
    /// </summary>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        // Check if circuit is open - throw immediately
        if (CurrentState == CircuitState.Open)
        {
            _logger.LogWarning("Circuit breaker is OPEN. Failing fast");
            throw new CircuitBreakerOpenException("Circuit breaker is open");
        }

        try
        {
            var result = await operation(cancellationToken);
            RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            throw;
        }
    }

    /// <summary>
    /// Выполняет void операцию через circuit breaker
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        // Check if circuit is open - throw immediately
        if (CurrentState == CircuitState.Open)
        {
            _logger.LogWarning("Circuit breaker is OPEN. Failing fast");
            throw new CircuitBreakerOpenException("Circuit breaker is open");
        }

        try
        {
            await operation(cancellationToken);
            RecordSuccess();
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            throw;
        }
    }

    /// <summary>
    /// Записывает успешный результат
    /// </summary>
    public void RecordSuccess()
    {
        lock (_stateLock)
        {
            _consecutiveFailures = 0;
            _totalSuccesses++;

            if (_currentState == CircuitState.HalfOpen)
            {
                // Successful request in HalfOpen state - close the circuit
                TransitionState(CircuitState.Closed);
            }

            _logger.LogDebug(
                "Success recorded. State: {State}, ConsecutiveFailures: {ConsecutiveFailures}, " +
                "TotalSuccesses: {TotalSuccesses}",
                _currentState,
                _consecutiveFailures,
                _totalSuccesses);
        }
    }

    /// <summary>
    /// Записывает неуспешный результат
    /// </summary>
    public void RecordFailure(Exception? exception = null)
    {
        lock (_stateLock)
        {
            _consecutiveFailures++;
            _totalFailures++;

            _logger.LogDebug(
                "Failure recorded. ConsecutiveFailures: {ConsecutiveFailures}, " +
                "TotalFailures: {TotalFailures}, Exception: {ExceptionType}",
                _consecutiveFailures,
                _totalFailures,
                exception?.GetType().Name ?? "None");

            // Check if we should open the circuit
            if (ShouldOpenCircuit())
            {
                TransitionState(CircuitState.Open);
            }
            else if (_currentState == CircuitState.HalfOpen)
            {
                // Failure in HalfOpen state - re-open the circuit
                TransitionState(CircuitState.Open);
            }
        }
    }

    /// <summary>
    /// Сбрасывает circuit breaker в начальное состояние
    /// </summary>
    public void Reset()
    {
        lock (_stateLock)
        {
            _currentState = CircuitState.Closed;
            _consecutiveFailures = 0;
            _totalFailures = 0;
            _totalSuccesses = 0;
            _circuitOpenedAt = null;

            _logger.LogInformation("Circuit breaker reset to Closed state");
        }
    }

    /// <summary>
    /// Принудительно открывает circuit breaker
    /// </summary>
    public void ForceOpen()
    {
        lock (_stateLock)
        {
            TransitionState(CircuitState.Open);
            _logger.LogWarning("Circuit breaker forcefully opened");
        }
    }

    /// <summary>
    /// Принудительно закрывает circuit breaker
    /// </summary>
    public void ForceClose()
    {
        lock (_stateLock)
        {
            TransitionState(CircuitState.Closed);
            _consecutiveFailures = 0;
            _logger.LogInformation("Circuit breaker forcefully closed");
        }
    }

    /// <summary>
    /// Получает статистику circuit breaker
    /// </summary>
    public CircuitBreakerStatistics GetStatistics()
    {
        lock (_stateLock)
        {
            var totalRequests = _totalSuccesses + _totalFailures;
            var failureRate = totalRequests > 0
                ? (double)_totalFailures / totalRequests * 100
                : 0;

            return new CircuitBreakerStatistics
            {
                State = _currentState,
                ConsecutiveFailures = _consecutiveFailures,
                TotalFailures = _totalFailures,
                TotalSuccesses = _totalSuccesses,
                FailureRate = failureRate,
                CircuitOpenedAt = _circuitOpenedAt
            };
        }
    }

    private bool ShouldOpenCircuit()
    {
        // Check consecutive failures threshold
        if (_consecutiveFailures >= _options.ConsecutiveFailuresThreshold)
        {
            _logger.LogWarning(
                "Consecutive failures threshold reached: {Failures} >= {Threshold}",
                _consecutiveFailures,
                _options.ConsecutiveFailuresThreshold);
            return true;
        }

        // Check failure rate threshold
        var totalRequests = _totalSuccesses + _totalFailures;
        if (totalRequests >= _options.MinimumThroughput)
        {
            var failureRate = (double)_totalFailures / totalRequests;
            if (failureRate >= _options.FailureRateThreshold)
            {
                _logger.LogWarning(
                    "Failure rate threshold reached: {FailureRate:P} >= {Threshold:P}",
                    failureRate,
                    _options.FailureRateThreshold);
                return true;
            }
        }

        return false;
    }

    private void TransitionState(CircuitState newState)
    {
        var oldState = _currentState;
        _currentState = newState;

        if (newState == CircuitState.Open)
        {
            _circuitOpenedAt = DateTime.UtcNow;
        }
        else if (newState == CircuitState.Closed)
        {
            _circuitOpenedAt = null;
        }

        _logger.LogInformation(
            "Circuit breaker state transition: {OldState} -> {NewState}",
            oldState,
            newState);
    }

    private void InitializePipelines()
    {
        // Initialize HTTP circuit breaker pipeline
        _httpPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = _options.FailureRateThreshold,
                MinimumThroughput = _options.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(_options.SamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(_options.BreakDurationSeconds),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(r => !r.IsSuccessStatusCode),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        "HTTP Circuit breaker opened. BreakDuration: {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("HTTP Circuit breaker closed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("HTTP Circuit breaker half-opened");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        // Initialize generic circuit breaker pipeline
        _genericPipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = _options.FailureRateThreshold,
                MinimumThroughput = _options.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(_options.SamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(_options.BreakDurationSeconds),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        "Generic Circuit breaker opened. BreakDuration: {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("Generic Circuit breaker closed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("Generic Circuit breaker half-opened");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}

/// <summary>
/// Состояния circuit breaker
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Закрыт - нормальная работа
    /// </summary>
    Closed,

    /// <summary>
    /// Открыт - fail fast
    /// </summary>
    Open,

    /// <summary>
    /// Полуоткрыт - тестовый режим
    /// </summary>
    HalfOpen
}

/// <summary>
/// Статистика circuit breaker
/// </summary>
public class CircuitBreakerStatistics
{
    /// <summary>
    /// Текущее состояние
    /// </summary>
    public CircuitState State { get; init; }

    /// <summary>
    /// Количество последовательных ошибок
    /// </summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>
    /// Общее количество ошибок
    /// </summary>
    public int TotalFailures { get; init; }

    /// <summary>
    /// Общее количество успешных запросов
    /// </summary>
    public int TotalSuccesses { get; init; }

    /// <summary>
    /// Процент ошибок
    /// </summary>
    public double FailureRate { get; init; }

    /// <summary>
    /// Время открытия circuit
    /// </summary>
    public DateTime? CircuitOpenedAt { get; init; }
}

/// <summary>
/// Исключение при открытом circuit breaker
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message)
    {
    }

    public CircuitBreakerOpenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
