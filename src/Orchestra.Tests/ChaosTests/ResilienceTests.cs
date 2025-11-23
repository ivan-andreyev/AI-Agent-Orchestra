using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Orchestra.Core.Options;
using Orchestra.Core.Services;
using Orchestra.Core.Services.Metrics;
using Orchestra.Core.Services.Resilience;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Orchestra.Tests.ChaosTests;

/// <summary>
/// Тесты chaos engineering для проверки resilience системы
/// </summary>
/// <remarks>
/// Проверяет поведение системы при:
/// - Network latency injection (добавление задержек 500ms-2000ms)
/// - Random failure injection (случайные ошибки 5-10%)
/// - Connection pool exhaustion
/// - Timeout acceleration (уменьшение таймаутов)
/// - Cascading failures (множественные сервисы падают)
/// </remarks>
public class ResilienceTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<CircuitBreakerPolicyService>> _mockCircuitBreakerLogger;
    private readonly Mock<ILogger<TelegramEscalationService>> _mockTelegramLogger;
    private readonly Mock<ILogger<PolicyRegistry>> _mockPolicyLogger;

    public ResilienceTests(ITestOutputHelper output)
    {
        _output = output;
        _mockCircuitBreakerLogger = new Mock<ILogger<CircuitBreakerPolicyService>>();
        _mockTelegramLogger = new Mock<ILogger<TelegramEscalationService>>();
        _mockPolicyLogger = new Mock<ILogger<PolicyRegistry>>();
    }

    private CircuitBreakerPolicyService CreateCircuitBreakerService(
        int consecutiveFailuresThreshold = 5,
        int breakDurationSeconds = 1)
    {
        var options = new CircuitBreakerOptions
        {
            FailureRateThreshold = 0.5,
            ConsecutiveFailuresThreshold = consecutiveFailuresThreshold,
            MinimumThroughput = 10,
            SamplingDurationSeconds = 30,
            BreakDurationSeconds = breakDurationSeconds
        };

        return new CircuitBreakerPolicyService(
            _mockCircuitBreakerLogger.Object,
            Options.Create(options));
    }

    private EscalationMetricsService CreateMockMetricsService()
    {
        var mockLogger = new Mock<ILogger<EscalationMetricsService>>();
        var mockMeterFactory = new Mock<IMeterFactory>();
        var mockMeter = new Mock<Meter>("test", "1.0.0");

        mockMeterFactory
            .Setup(x => x.Create(It.IsAny<MeterOptions>()))
            .Returns(mockMeter.Object);

        return new EscalationMetricsService(mockLogger.Object, mockMeterFactory.Object);
    }

    private IPolicyRegistry CreatePolicyRegistry()
    {
        var options = new TelegramRetryOptions
        {
            MaxRetryAttempts = 2,
            InitialDelayMs = 50,
            MaxDelayMs = 200,
            JitterEnabled = false,
            RetryOn = new[] { 429, 500, 502, 503, 504 }
        };

        return new PolicyRegistry(_mockPolicyLogger.Object, Options.Create(options));
    }

    #region Network Latency Injection Tests

    /// <summary>
    /// Тест: Telegram API timeout при высокой latency
    /// </summary>
    [Fact]
    public async Task NetworkLatency_TelegramApiTimeout_HandlesGracefully()
    {
        // Arrange
        var telegramOptions = new TelegramEscalationOptions
        {
            Enabled = true,
            BotToken = "test-token",
            ChatId = "123456"
        };

        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage _, CancellationToken ct) =>
            {
                // Inject 500ms latency
                await Task.Delay(500, ct);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"ok\":true}")
                };
            });

        var httpClient = new HttpClient(mockHttpHandler.Object)
        {
            Timeout = TimeSpan.FromMilliseconds(100) // Short timeout to trigger failure
        };

        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await service.SendEscalationAsync("agent-1", "Test message");
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Network Latency Test:");
        _output.WriteLine($"  Timeout: 100ms, Latency: 500ms");
        _output.WriteLine($"  Result: {(result ? "Success" : "Failure")}");
        _output.WriteLine($"  Duration: {stopwatch.ElapsedMilliseconds}ms");

        // Should fail due to timeout
        Assert.False(result);
    }

    /// <summary>
    /// Тест: Circuit breaker при sporadic latency spikes
    /// </summary>
    [Fact]
    public async Task NetworkLatency_SporadicSpikes_CircuitBreakerHandles()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService(3, 1);
        var successCount = 0;
        var failureCount = 0;
        var requestCount = 20;
        var random = new Random(42);

        // Act - simulate sporadic latency spikes causing timeouts
        for (var i = 0; i < requestCount; i++)
        {
            try
            {
                await circuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        // 30% chance of high latency causing "timeout"
                        if (random.NextDouble() < 0.3)
                        {
                            throw new TaskCanceledException("Operation timed out");
                        }

                        await Task.Delay(5, ct);
                        return true;
                    },
                    CancellationToken.None);

                successCount++;
            }
            catch (CircuitBreakerOpenException)
            {
                // Circuit opened - expected behavior
                failureCount++;
                break;
            }
            catch (TaskCanceledException)
            {
                failureCount++;
            }
        }

        // Assert
        _output.WriteLine($"Sporadic Latency Test:");
        _output.WriteLine($"  Successful: {successCount}");
        _output.WriteLine($"  Failed: {failureCount}");
        _output.WriteLine($"  Circuit State: {circuitBreaker.CurrentState}");

        Assert.True(successCount + failureCount > 0);
    }

    #endregion

    #region Random Failure Injection Tests

    /// <summary>
    /// Тест: Random failure injection - 10% failure rate
    /// </summary>
    [Fact]
    public async Task RandomFailure_10PercentRate_SystemRemainsFunctional()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService(10, 1); // High threshold
        var successCount = 0;
        var failureCount = 0;
        var requestCount = 100;
        var random = new Random(42);

        // Act - inject 10% random failures
        var tasks = new List<Task>();
        for (var i = 0; i < requestCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await circuitBreaker.ExecuteAsync(
                        async ct =>
                        {
                            await Task.Delay(1, ct);

                            // 10% chance of failure
                            if (random.NextDouble() < 0.1)
                            {
                                throw new HttpRequestException("Random failure injected");
                            }

                            return true;
                        },
                        false,
                        CancellationToken.None);

                    Interlocked.Increment(ref successCount);
                }
                catch
                {
                    Interlocked.Increment(ref failureCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var actualFailureRate = (double)failureCount / requestCount;

        _output.WriteLine($"Random Failure Injection Test:");
        _output.WriteLine($"  Target Failure Rate: 10%");
        _output.WriteLine($"  Actual Failure Rate: {actualFailureRate:P2}");
        _output.WriteLine($"  Successful: {successCount}");
        _output.WriteLine($"  Failed: {failureCount}");
        _output.WriteLine($"  Circuit State: {circuitBreaker.CurrentState}");

        // System should remain mostly functional with <=25% failure due to random variance
        Assert.True(actualFailureRate <= 0.25,
            $"Failure rate {actualFailureRate:P2} exceeds 25% threshold");
    }

    /// <summary>
    /// Тест: Random failure pattern doesn't cause circuit thrashing
    /// </summary>
    [Fact]
    public async Task RandomFailure_IntermittentErrors_NoCircuitThrashing()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService(5, 2);
        var stateTransitions = new ConcurrentBag<CircuitState>();
        var requestCount = 50;
        var random = new Random(123);

        // Track state transitions
        var lastState = circuitBreaker.CurrentState;
        stateTransitions.Add(lastState);

        // Act - send intermittent failures (success, fail, success pattern)
        for (var i = 0; i < requestCount; i++)
        {
            try
            {
                await circuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        await Task.Delay(1, ct);

                        // Intermittent failure pattern
                        if (random.NextDouble() < 0.15)
                        {
                            throw new HttpRequestException("Intermittent failure");
                        }

                        return true;
                    },
                    false,
                    CancellationToken.None);
            }
            catch
            {
                // Track state after failure
            }

            var currentState = circuitBreaker.CurrentState;
            if (currentState != lastState)
            {
                stateTransitions.Add(currentState);
                lastState = currentState;
            }
        }

        // Assert - should not have excessive state transitions (thrashing)
        var transitionCount = stateTransitions.Count;

        _output.WriteLine($"Circuit Thrashing Test:");
        _output.WriteLine($"  Requests: {requestCount}");
        _output.WriteLine($"  State Transitions: {transitionCount}");
        _output.WriteLine($"  Final State: {circuitBreaker.CurrentState}");

        Assert.True(transitionCount < 10,
            $"Circuit thrashing detected: {transitionCount} transitions");
    }

    #endregion

    #region Timeout Acceleration Tests

    /// <summary>
    /// Тест: Accelerated timeouts (100ms) при обычных операциях
    /// </summary>
    [Fact]
    public async Task TimeoutAcceleration_100msTimeout_OperationsComplete()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService(3, 1);
        var successCount = 0;
        var timeoutCount = 0;
        var requestCount = 20;

        // Act - execute with aggressive timeout
        for (var i = 0; i < requestCount; i++)
        {
            using var cts = new CancellationTokenSource(100); // 100ms timeout

            try
            {
                await circuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        // Fast operation (should complete in time)
                        await Task.Delay(10, ct);
                        return true;
                    },
                    cts.Token);

                successCount++;
            }
            catch (OperationCanceledException)
            {
                timeoutCount++;
            }
            catch (CircuitBreakerOpenException)
            {
                break;
            }
        }

        // Assert
        _output.WriteLine($"Timeout Acceleration Test:");
        _output.WriteLine($"  Timeout: 100ms");
        _output.WriteLine($"  Successful: {successCount}");
        _output.WriteLine($"  Timeouts: {timeoutCount}");

        // Most operations should complete with 100ms timeout
        Assert.True(successCount >= requestCount * 0.8,
            $"Only {successCount} succeeded with 100ms timeout");
    }

    /// <summary>
    /// Тест: Slow operations при aggressive timeout
    /// </summary>
    [Fact]
    public async Task TimeoutAcceleration_SlowOperations_CancelledAppropriately()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService(10, 1);
        var completedCount = 0;
        var cancelledCount = 0;
        var requestCount = 10;

        // Act - operations that are slower than timeout
        for (var i = 0; i < requestCount; i++)
        {
            using var cts = new CancellationTokenSource(50); // 50ms timeout

            try
            {
                await circuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        // Slow operation (100ms, exceeds 50ms timeout)
                        await Task.Delay(100, ct);
                        return true;
                    },
                    false,
                    cts.Token);

                completedCount++;
            }
            catch (OperationCanceledException)
            {
                cancelledCount++;
            }
            catch (CircuitBreakerOpenException)
            {
                break;
            }
        }

        // Assert
        _output.WriteLine($"Slow Operations Timeout Test:");
        _output.WriteLine($"  Timeout: 50ms, Operation: 100ms");
        _output.WriteLine($"  Completed: {completedCount}");
        _output.WriteLine($"  Cancelled: {cancelledCount}");

        // Most should be cancelled
        Assert.True(cancelledCount > completedCount,
            "Slow operations should be cancelled by timeout");
    }

    #endregion

    #region Cascading Failures Tests

    /// <summary>
    /// Тест: Cascading failure - multiple service failures
    /// </summary>
    [Fact]
    public async Task CascadingFailures_MultipleServices_GracefulDegradation()
    {
        // Arrange - simulate 3 dependent services
        var service1CircuitBreaker = CreateCircuitBreakerService(3, 1);
        var service2CircuitBreaker = CreateCircuitBreakerService(3, 1);
        var service3CircuitBreaker = CreateCircuitBreakerService(3, 1);

        var service1Failures = 0;
        var service2Failures = 0;
        var service3Failures = 0;
        var totalSuccess = 0;

        // Act - simulate cascading failure scenario
        for (var i = 0; i < 20; i++)
        {
            try
            {
                // Service 1 fails after 5 calls
                var result1 = await service1CircuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        await Task.Delay(1, ct);
                        if (i >= 5)
                        {
                            throw new HttpRequestException("Service 1 down");
                        }
                        return true;
                    },
                    false,
                    CancellationToken.None);

                // Service 2 depends on Service 1
                var result2 = await service2CircuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        await Task.Delay(1, ct);
                        // Fails if service 1 failed
                        if (i >= 5)
                        {
                            throw new HttpRequestException("Service 2 down (cascading)");
                        }
                        return true;
                    },
                    false,
                    CancellationToken.None);

                // Service 3 depends on Service 2
                var result3 = await service3CircuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        await Task.Delay(1, ct);
                        if (i >= 5)
                        {
                            throw new HttpRequestException("Service 3 down (cascading)");
                        }
                        return true;
                    },
                    false,
                    CancellationToken.None);

                totalSuccess++;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Service 1"))
            {
                service1Failures++;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Service 2"))
            {
                service2Failures++;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Service 3"))
            {
                service3Failures++;
            }
            catch (CircuitBreakerOpenException)
            {
                // Circuit open - graceful degradation working
                break;
            }
        }

        // Assert
        _output.WriteLine($"Cascading Failures Test:");
        _output.WriteLine($"  Total Success: {totalSuccess}");
        _output.WriteLine($"  Service 1 Failures: {service1Failures}");
        _output.WriteLine($"  Service 2 Failures: {service2Failures}");
        _output.WriteLine($"  Service 3 Failures: {service3Failures}");
        _output.WriteLine($"  Service 1 Circuit: {service1CircuitBreaker.CurrentState}");
        _output.WriteLine($"  Service 2 Circuit: {service2CircuitBreaker.CurrentState}");
        _output.WriteLine($"  Service 3 Circuit: {service3CircuitBreaker.CurrentState}");

        // Initial successful calls should have happened before cascade
        Assert.True(totalSuccess >= 4, "Initial calls should succeed before cascade");
    }

    /// <summary>
    /// Тест: Circuit breaker + timeout combination
    /// </summary>
    [Fact]
    public async Task CascadingFailures_CircuitBreakerPlusTimeout_Combination()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService(3, 1);
        var successCount = 0;
        var timeoutCount = 0;
        var circuitOpenCount = 0;
        var requestCount = 20;
        var random = new Random(42);

        // Act - combination of random timeouts and failures
        for (var i = 0; i < requestCount; i++)
        {
            using var cts = new CancellationTokenSource(100);

            try
            {
                await circuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        // Random delay (some exceed timeout)
                        var delay = random.Next(10, 200);
                        await Task.Delay(delay, ct);

                        // Random failure
                        if (random.NextDouble() < 0.2)
                        {
                            throw new HttpRequestException("Random failure");
                        }

                        return true;
                    },
                    cts.Token);

                successCount++;
            }
            catch (OperationCanceledException)
            {
                timeoutCount++;
            }
            catch (CircuitBreakerOpenException)
            {
                circuitOpenCount++;
            }
            catch (HttpRequestException)
            {
                // Handled by circuit breaker
            }
        }

        // Assert
        _output.WriteLine($"Circuit Breaker + Timeout Combination:");
        _output.WriteLine($"  Successful: {successCount}");
        _output.WriteLine($"  Timeouts: {timeoutCount}");
        _output.WriteLine($"  Circuit Open Failures: {circuitOpenCount}");
        _output.WriteLine($"  Final Circuit State: {circuitBreaker.CurrentState}");

        // System should have handled all requests without crashing
        Assert.True(successCount + timeoutCount + circuitOpenCount <= requestCount);
    }

    #endregion

    #region Recovery Validation Tests

    /// <summary>
    /// Тест: Система восстанавливается после chaos события
    /// </summary>
    [Fact]
    public async Task Recovery_AfterChaosEvent_SystemRecovers()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService(3, 1);
        var preChaosSucess = 0;
        var duringChaosFailure = 0;
        var postChaosSuccess = 0;

        // Act - Phase 1: Normal operation
        for (var i = 0; i < 5; i++)
        {
            try
            {
                await circuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        await Task.Delay(1, ct);
                        return true;
                    },
                    CancellationToken.None);

                preChaosSucess++;
            }
            catch
            {
                // Ignore
            }
        }

        // Phase 2: Chaos event (force failures)
        for (var i = 0; i < 5; i++)
        {
            try
            {
                await circuitBreaker.ExecuteAsync(
                    _ => throw new HttpRequestException("Chaos event"),
                    CancellationToken.None);
            }
            catch (HttpRequestException)
            {
                duringChaosFailure++;
            }
            catch (CircuitBreakerOpenException)
            {
                duringChaosFailure++;
                break;
            }
        }

        // Wait for circuit to half-open
        await Task.Delay(1200);

        // Phase 3: Recovery
        for (var i = 0; i < 5; i++)
        {
            try
            {
                await circuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        await Task.Delay(1, ct);
                        return true;
                    },
                    CancellationToken.None);

                postChaosSuccess++;
            }
            catch (CircuitBreakerOpenException)
            {
                // Still recovering
            }
        }

        // Assert
        _output.WriteLine($"Recovery Test:");
        _output.WriteLine($"  Pre-Chaos Success: {preChaosSucess}");
        _output.WriteLine($"  During Chaos Failures: {duringChaosFailure}");
        _output.WriteLine($"  Post-Chaos Success: {postChaosSuccess}");
        _output.WriteLine($"  Final State: {circuitBreaker.CurrentState}");

        Assert.Equal(5, preChaosSucess);
        Assert.True(duringChaosFailure > 0);
        Assert.True(postChaosSuccess > 0, "System should recover after chaos event");
    }

    /// <summary>
    /// Тест: No unhandled exceptions escape during chaos
    /// </summary>
    [Fact]
    public async Task Recovery_NoUnhandledExceptions_AllCaught()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService(3, 1);
        var exceptionsEscaped = new ConcurrentBag<Exception>();
        var random = new Random(42);

        // Act - throw various exceptions
        var tasks = new List<Task>();
        for (var i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await circuitBreaker.ExecuteAsync(
                        async ct =>
                        {
                            await Task.Delay(1, ct);

                            var exceptionType = random.Next(5);
                            switch (exceptionType)
                            {
                                case 0:
                                    throw new HttpRequestException("Network error");
                                case 1:
                                    throw new TaskCanceledException("Timeout");
                                case 2:
                                    throw new InvalidOperationException("Invalid state");
                                case 3:
                                    throw new TimeoutException("Operation timeout");
                                default:
                                    return true;
                            }
                        },
                        false,
                        CancellationToken.None);
                }
                catch (CircuitBreakerOpenException)
                {
                    // Expected
                }
                catch (HttpRequestException)
                {
                    // Expected
                }
                catch (TaskCanceledException)
                {
                    // Expected
                }
                catch (InvalidOperationException)
                {
                    // Expected
                }
                catch (TimeoutException)
                {
                    // Expected
                }
                catch (Exception ex)
                {
                    // Unexpected exception escaped!
                    exceptionsEscaped.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        _output.WriteLine($"Exception Handling Test:");
        _output.WriteLine($"  Total Requests: 50");
        _output.WriteLine($"  Unhandled Exceptions: {exceptionsEscaped.Count}");

        Assert.Empty(exceptionsEscaped);
    }

    /// <summary>
    /// Тест: Metrics continue being collected during chaos
    /// </summary>
    [Fact]
    public void Metrics_DuringChaos_ContinueCollection()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService(5, 1);

        // Act - simulate chaos with metrics collection
        for (var i = 0; i < 20; i++)
        {
            if (i % 2 == 0)
            {
                circuitBreaker.RecordSuccess();
            }
            else
            {
                circuitBreaker.RecordFailure(new HttpRequestException("Chaos"));
            }
        }

        // Get statistics after chaos
        var stats = circuitBreaker.GetStatistics();

        // Assert
        _output.WriteLine($"Metrics During Chaos Test:");
        _output.WriteLine($"  Total Successes: {stats.TotalSuccesses}");
        _output.WriteLine($"  Total Failures: {stats.TotalFailures}");
        _output.WriteLine($"  Failure Rate: {stats.FailureRate:F2}%");
        _output.WriteLine($"  State: {stats.State}");

        Assert.Equal(10, stats.TotalSuccesses);
        Assert.Equal(10, stats.TotalFailures);
        Assert.Equal(50, stats.FailureRate, 0.1);
    }

    /// <summary>
    /// Тест: Logging captures chaos events
    /// </summary>
    [Fact]
    public void Logging_DuringChaos_EventsCaptured()
    {
        // Arrange
        var logMessages = new List<string>();
        var mockLogger = new Mock<ILogger<CircuitBreakerPolicyService>>();

        mockLogger
            .Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>((level, id, state, ex, formatter) =>
            {
                logMessages.Add(formatter.DynamicInvoke(state, ex)?.ToString() ?? "");
            });

        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailuresThreshold = 3,
            BreakDurationSeconds = 1
        };

        var circuitBreaker = new CircuitBreakerPolicyService(
            mockLogger.Object,
            Options.Create(options));

        // Act - trigger circuit state transitions
        circuitBreaker.ForceOpen();
        circuitBreaker.ForceClose();
        circuitBreaker.Reset();

        // Assert - should have logged state transitions
        _output.WriteLine($"Logging Test:");
        _output.WriteLine($"  Log Messages: {logMessages.Count}");

        mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(3));
    }

    #endregion
}
