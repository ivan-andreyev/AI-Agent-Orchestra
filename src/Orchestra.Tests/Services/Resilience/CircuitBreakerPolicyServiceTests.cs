using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchestra.Core.Options;
using Orchestra.Core.Services.Resilience;
using Xunit;

namespace Orchestra.Tests.Services.Resilience;

/// <summary>
/// Тесты для CircuitBreakerPolicyService
/// </summary>
/// <remarks>
/// Проверяет корректность работы circuit breaker паттерна:
/// - Состояния Closed, Open, HalfOpen
/// - Переходы между состояниями
/// - Fallback механизм
/// - Сбор метрик
/// </remarks>
public class CircuitBreakerPolicyServiceTests
{
    private readonly Mock<ILogger<CircuitBreakerPolicyService>> _mockLogger;
    private readonly CircuitBreakerOptions _defaultOptions;

    public CircuitBreakerPolicyServiceTests()
    {
        _mockLogger = new Mock<ILogger<CircuitBreakerPolicyService>>();
        _defaultOptions = new CircuitBreakerOptions
        {
            FailureRateThreshold = 0.5,
            ConsecutiveFailuresThreshold = 5,
            MinimumThroughput = 10,
            SamplingDurationSeconds = 30,
            BreakDurationSeconds = 1 // Short duration for tests
        };
    }

    private CircuitBreakerPolicyService CreateService(CircuitBreakerOptions? options = null)
    {
        var opts = Options.Create(options ?? _defaultOptions);
        return new CircuitBreakerPolicyService(_mockLogger.Object, opts);
    }

    #region Closed State Tests

    /// <summary>
    /// Тест: В состоянии Closed запросы проходят нормально
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenCircuitClosed_RequestsPassThrough()
    {
        // Arrange
        var service = CreateService();
        var callCount = 0;

        // Act
        var result = await service.ExecuteAsync(
            async ct =>
            {
                callCount++;
                await Task.Delay(1, ct);
                return "success";
            },
            CancellationToken.None);

        // Assert
        Assert.Equal(CircuitState.Closed, service.CurrentState);
        Assert.Equal("success", result);
        Assert.Equal(1, callCount);
        Assert.Equal(1, service.TotalSuccesses);
        Assert.Equal(0, service.TotalFailures);
    }

    /// <summary>
    /// Тест: Успешные запросы в Closed состоянии сбрасывают счётчик ошибок
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SuccessfulRequest_ResetsConsecutiveFailures()
    {
        // Arrange
        var service = CreateService();

        // Simulate 3 failures (below threshold)
        for (var i = 0; i < 3; i++)
        {
            service.RecordFailure(new Exception("Test"));
        }

        Assert.Equal(3, service.ConsecutiveFailures);

        // Act - successful request
        await service.ExecuteAsync(
            async ct =>
            {
                await Task.Delay(1, ct);
                return true;
            },
            CancellationToken.None);

        // Assert
        Assert.Equal(0, service.ConsecutiveFailures);
        Assert.Equal(CircuitState.Closed, service.CurrentState);
    }

    /// <summary>
    /// Тест: Множественные успешные запросы в Closed состоянии
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MultipleSuccessfulRequests_StayClosed()
    {
        // Arrange
        var service = CreateService();
        var tasks = new List<Task<int>>();

        // Act - execute 10 successful requests
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(service.ExecuteAsync(
                async ct =>
                {
                    await Task.Delay(1, ct);
                    return index;
                },
                CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(CircuitState.Closed, service.CurrentState);
        Assert.Equal(10, service.TotalSuccesses);
        Assert.Equal(0, service.TotalFailures);
    }

    #endregion

    #region Open State Tests

    /// <summary>
    /// Тест: Circuit открывается после достижения порога последовательных ошибок
    /// </summary>
    [Fact]
    public void RecordFailure_ConsecutiveFailuresThresholdReached_OpensCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailuresThreshold = 3,
            BreakDurationSeconds = 30
        };
        var service = CreateService(options);

        // Act - record 3 consecutive failures
        for (var i = 0; i < 3; i++)
        {
            service.RecordFailure(new Exception($"Failure {i}"));
        }

        // Assert
        Assert.Equal(CircuitState.Open, service.CurrentState);
        Assert.True(service.IsCircuitOpen);
        Assert.Equal(3, service.ConsecutiveFailures);
    }

    /// <summary>
    /// Тест: В состоянии Open запросы fail fast
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenCircuitOpen_FailsFast()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailuresThreshold = 2,
            BreakDurationSeconds = 30
        };
        var service = CreateService(options);

        // Force circuit to open
        service.ForceOpen();
        var callCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
        {
            await service.ExecuteAsync(
                async ct =>
                {
                    callCount++;
                    await Task.Delay(1, ct);
                    return "should not execute";
                },
                CancellationToken.None);
        });

        Assert.Equal(0, callCount); // Operation should not be called
        Assert.True(service.IsCircuitOpen);
    }

    /// <summary>
    /// Тест: В состоянии Open возвращается fallback значение
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenCircuitOpenWithFallback_ReturnsFallbackValue()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailuresThreshold = 2,
            BreakDurationSeconds = 30
        };
        var service = CreateService(options);
        service.ForceOpen();
        var callCount = 0;

        // Act
        var result = await service.ExecuteAsync(
            async ct =>
            {
                callCount++;
                await Task.Delay(1, ct);
                return "real value";
            },
            "fallback value",
            CancellationToken.None);

        // Assert
        Assert.Equal("fallback value", result);
        Assert.Equal(0, callCount); // Operation should not be called
    }

    /// <summary>
    /// Тест: Время открытия circuit записывается
    /// </summary>
    [Fact]
    public void ForceOpen_RecordsCircuitOpenedTime()
    {
        // Arrange
        var service = CreateService();
        var beforeOpen = DateTime.UtcNow;

        // Act
        service.ForceOpen();
        var afterOpen = DateTime.UtcNow;

        // Assert
        Assert.NotNull(service.CircuitOpenedAt);
        Assert.True(service.CircuitOpenedAt >= beforeOpen);
        Assert.True(service.CircuitOpenedAt <= afterOpen);
    }

    #endregion

    #region Half-Open State Tests

    /// <summary>
    /// Тест: Circuit переходит в HalfOpen после истечения break duration
    /// </summary>
    [Fact]
    public async Task CurrentState_AfterBreakDuration_TransitionsToHalfOpen()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailuresThreshold = 2,
            BreakDurationSeconds = 1 // 1 second break
        };
        var service = CreateService(options);
        service.ForceOpen();

        Assert.Equal(CircuitState.Open, service.CurrentState);

        // Act - wait for break duration
        await Task.Delay(1100); // Wait slightly more than 1 second

        // Assert
        Assert.Equal(CircuitState.HalfOpen, service.CurrentState);
        Assert.True(service.IsCircuitHalfOpen);
    }

    /// <summary>
    /// Тест: Успешный запрос в HalfOpen закрывает circuit
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SuccessInHalfOpen_ClosesCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailuresThreshold = 2,
            BreakDurationSeconds = 1
        };
        var service = CreateService(options);
        service.ForceOpen();

        // Wait for transition to HalfOpen
        await Task.Delay(1100);
        Assert.Equal(CircuitState.HalfOpen, service.CurrentState);

        // Act - execute successful request
        var result = await service.ExecuteAsync(
            async ct =>
            {
                await Task.Delay(1, ct);
                return "success";
            },
            CancellationToken.None);

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(CircuitState.Closed, service.CurrentState);
        Assert.False(service.IsCircuitOpen);
        Assert.False(service.IsCircuitHalfOpen);
    }

    /// <summary>
    /// Тест: Неуспешный запрос в HalfOpen снова открывает circuit
    /// </summary>
    [Fact]
    public async Task RecordFailure_InHalfOpen_ReopensCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailuresThreshold = 2,
            BreakDurationSeconds = 1
        };
        var service = CreateService(options);
        service.ForceOpen();

        // Wait for transition to HalfOpen
        await Task.Delay(1100);
        Assert.Equal(CircuitState.HalfOpen, service.CurrentState);

        // Act - record failure
        service.RecordFailure(new Exception("Test failure in half-open"));

        // Assert
        Assert.Equal(CircuitState.Open, service.CurrentState);
        Assert.True(service.IsCircuitOpen);
    }

    #endregion

    #region State Transition Tests

    /// <summary>
    /// Тест: Полный цикл переходов: Closed -> Open -> HalfOpen -> Closed
    /// </summary>
    [Fact]
    public async Task StateTransitions_FullCycle_Closed_Open_HalfOpen_Closed()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailuresThreshold = 3,
            BreakDurationSeconds = 1
        };
        var service = CreateService(options);

        // Assert initial state
        Assert.Equal(CircuitState.Closed, service.CurrentState);

        // Act 1: Trigger failures to open circuit
        for (var i = 0; i < 3; i++)
        {
            service.RecordFailure(new Exception($"Failure {i}"));
        }

        Assert.Equal(CircuitState.Open, service.CurrentState);

        // Act 2: Wait for transition to HalfOpen
        await Task.Delay(1100);
        Assert.Equal(CircuitState.HalfOpen, service.CurrentState);

        // Act 3: Success in HalfOpen closes circuit
        service.RecordSuccess();
        Assert.Equal(CircuitState.Closed, service.CurrentState);
    }

    /// <summary>
    /// Тест: Reset сбрасывает все состояния
    /// </summary>
    [Fact]
    public void Reset_ClearsAllStateAndCounters()
    {
        // Arrange
        var service = CreateService();

        // Build up some state
        for (var i = 0; i < 10; i++)
        {
            service.RecordSuccess();
        }

        for (var i = 0; i < 3; i++)
        {
            service.RecordFailure();
        }

        Assert.True(service.TotalSuccesses > 0);
        Assert.True(service.TotalFailures > 0);

        // Act
        service.Reset();

        // Assert
        Assert.Equal(CircuitState.Closed, service.CurrentState);
        Assert.Equal(0, service.ConsecutiveFailures);
        Assert.Equal(0, service.TotalFailures);
        Assert.Equal(0, service.TotalSuccesses);
        Assert.Null(service.CircuitOpenedAt);
    }

    /// <summary>
    /// Тест: ForceClose закрывает circuit принудительно
    /// </summary>
    [Fact]
    public void ForceClose_ClosesCircuitAndResetsConsecutiveFailures()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailuresThreshold = 3,
            BreakDurationSeconds = 30
        };
        var service = CreateService(options);

        // Open the circuit
        service.ForceOpen();
        Assert.True(service.IsCircuitOpen);

        // Act
        service.ForceClose();

        // Assert
        Assert.Equal(CircuitState.Closed, service.CurrentState);
        Assert.False(service.IsCircuitOpen);
        Assert.Equal(0, service.ConsecutiveFailures);
    }

    #endregion

    #region Statistics Tests

    /// <summary>
    /// Тест: GetStatistics возвращает корректную статистику
    /// </summary>
    [Fact]
    public void GetStatistics_ReturnsCorrectValues()
    {
        // Arrange
        var service = CreateService();

        // Build up some state
        for (var i = 0; i < 8; i++)
        {
            service.RecordSuccess();
        }

        for (var i = 0; i < 2; i++)
        {
            service.RecordFailure();
        }

        // Act
        var stats = service.GetStatistics();

        // Assert
        Assert.Equal(CircuitState.Closed, stats.State);
        Assert.Equal(2, stats.ConsecutiveFailures);
        Assert.Equal(2, stats.TotalFailures);
        Assert.Equal(8, stats.TotalSuccesses);
        Assert.Equal(20, stats.FailureRate, 0.1); // 2/10 = 20%
    }

    /// <summary>
    /// Тест: Статистика корректно рассчитывает failure rate
    /// </summary>
    [Fact]
    public void GetStatistics_CalculatesFailureRateCorrectly()
    {
        // Arrange
        var service = CreateService();

        // 30 successes, 70 failures = 70% failure rate
        for (var i = 0; i < 30; i++)
        {
            service.RecordSuccess();
        }

        for (var i = 0; i < 70; i++)
        {
            service.RecordFailure();
        }

        // Act
        var stats = service.GetStatistics();

        // Assert
        Assert.Equal(70, stats.FailureRate, 0.1);
    }

    #endregion

    #region Pipeline Tests

    /// <summary>
    /// Тест: GetHttpCircuitBreaker возвращает валидный pipeline
    /// </summary>
    [Fact]
    public void GetHttpCircuitBreaker_ReturnsValidPipeline()
    {
        // Arrange
        var service = CreateService();

        // Act
        var pipeline = service.GetHttpCircuitBreaker();

        // Assert
        Assert.NotNull(pipeline);
    }

    /// <summary>
    /// Тест: GetGenericCircuitBreaker возвращает валидный pipeline
    /// </summary>
    [Fact]
    public void GetGenericCircuitBreaker_ReturnsValidPipeline()
    {
        // Arrange
        var service = CreateService();

        // Act
        var pipeline = service.GetGenericCircuitBreaker();

        // Assert
        Assert.NotNull(pipeline);
    }

    #endregion

    #region Exception Handling Tests

    /// <summary>
    /// Тест: Исключение в операции записывается как failure
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_OperationThrows_RecordsFailure()
    {
        // Arrange
        var service = CreateService();
        var testException = new InvalidOperationException("Test exception");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.ExecuteAsync<string>(
                async _ =>
                {
                    await Task.Delay(1);
                    throw testException;
                },
                CancellationToken.None);
        });

        Assert.Equal(1, service.TotalFailures);
        Assert.Equal(1, service.ConsecutiveFailures);
    }

    /// <summary>
    /// Тест: Void операция через circuit breaker
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_VoidOperation_Success()
    {
        // Arrange
        var service = CreateService();
        var executed = false;

        // Act
        await service.ExecuteAsync(
            async ct =>
            {
                await Task.Delay(1, ct);
                executed = true;
            },
            CancellationToken.None);

        // Assert
        Assert.True(executed);
        Assert.Equal(1, service.TotalSuccesses);
    }

    /// <summary>
    /// Тест: Void операция при открытом circuit выбрасывает исключение
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_VoidOperationWhenOpen_ThrowsCircuitBreakerOpenException()
    {
        // Arrange
        var service = CreateService();
        service.ForceOpen();
        var executed = false;

        // Act & Assert
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
        {
            await service.ExecuteAsync(
                async ct =>
                {
                    executed = true;
                    await Task.Delay(1, ct);
                },
                CancellationToken.None);
        });

        Assert.False(executed);
    }

    #endregion

    #region Failure Rate Threshold Tests

    /// <summary>
    /// Тест: Circuit открывается при достижении failure rate threshold
    /// </summary>
    [Fact]
    public void RecordFailure_FailureRateThresholdReached_OpensCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureRateThreshold = 0.5, // 50%
            ConsecutiveFailuresThreshold = 100, // High so it won't trigger
            MinimumThroughput = 10
        };
        var service = CreateService(options);

        // Record 5 successes
        for (var i = 0; i < 5; i++)
        {
            service.RecordSuccess();
        }

        // Record 5 failures (5/10 = 50% failure rate)
        for (var i = 0; i < 5; i++)
        {
            service.RecordFailure();
        }

        // Assert - should open at exactly 50% threshold
        Assert.Equal(CircuitState.Open, service.CurrentState);
    }

    /// <summary>
    /// Тест: Circuit не открывается если минимальный throughput не достигнут
    /// </summary>
    [Fact]
    public void RecordFailure_BelowMinimumThroughput_DoesNotOpenCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureRateThreshold = 0.5,
            ConsecutiveFailuresThreshold = 100, // High so it won't trigger
            MinimumThroughput = 20 // Need at least 20 requests
        };
        var service = CreateService(options);

        // Record only 5 successes and 5 failures (total 10, below minimum 20)
        for (var i = 0; i < 5; i++)
        {
            service.RecordSuccess();
        }

        for (var i = 0; i < 4; i++)
        {
            service.RecordFailure();
        }

        // Assert - should still be closed because minimum throughput not reached
        Assert.Equal(CircuitState.Closed, service.CurrentState);
    }

    #endregion

    #region Constructor Validation Tests

    /// <summary>
    /// Тест: Конструктор выбрасывает исключение при null logger
    /// </summary>
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CircuitBreakerPolicyService(null!, Options.Create(_defaultOptions)));
    }

    /// <summary>
    /// Тест: Конструктор выбрасывает исключение при null options
    /// </summary>
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CircuitBreakerPolicyService(_mockLogger.Object, null!));
    }

    #endregion
}
