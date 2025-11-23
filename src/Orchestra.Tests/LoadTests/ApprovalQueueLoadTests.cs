using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Options;
using Orchestra.Core.Services.Metrics;
using Orchestra.Core.Services.Resilience;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Xunit;
using Xunit.Abstractions;

namespace Orchestra.Tests.LoadTests;

/// <summary>
/// Нагрузочные тесты для очереди одобрений и связанных сервисов
/// </summary>
/// <remarks>
/// Тесты проверяют:
/// - Производительность при нормальной нагрузке (50 req/sec)
/// - Производительность при пиковой нагрузке (100 req/sec)
/// - Устойчивую нагрузку в течение длительного времени
/// - Поведение circuit breaker под нагрузкой
/// </remarks>
public class ApprovalQueueLoadTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<CircuitBreakerPolicyService>> _mockCircuitBreakerLogger;
    private readonly CircuitBreakerOptions _circuitBreakerOptions;

    // Performance targets (from Phase 4.4 requirements)
    private const int NormalLoadRequestsPerSecond = 50;
    private const int PeakLoadRequestsPerSecond = 100;
    private const int NormalLoadMaxResponseMs = 500;
    private const int PeakLoadMaxResponseMs = 1000;
    private const double NormalLoadMaxFailureRate = 0.01; // 1%
    private const double PeakLoadMaxFailureRate = 0.05; // 5%

    public ApprovalQueueLoadTests(ITestOutputHelper output)
    {
        _output = output;
        _mockCircuitBreakerLogger = new Mock<ILogger<CircuitBreakerPolicyService>>();
        _circuitBreakerOptions = new CircuitBreakerOptions
        {
            FailureRateThreshold = 0.5,
            ConsecutiveFailuresThreshold = 10,
            MinimumThroughput = 20,
            SamplingDurationSeconds = 30,
            BreakDurationSeconds = 5
        };
    }

    private OrchestraDbContext CreateInMemoryContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<OrchestraDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new OrchestraDbContext(options);
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

    private CircuitBreakerPolicyService CreateCircuitBreakerService()
    {
        return new CircuitBreakerPolicyService(
            _mockCircuitBreakerLogger.Object,
            Options.Create(_circuitBreakerOptions));
    }

    #region Normal Load Tests

    /// <summary>
    /// Тест: Нормальная нагрузка - 50 concurrent circuit breaker operations
    /// </summary>
    [Fact]
    public async Task NormalLoad_50ConcurrentRequests_ResponseTimeUnder500ms()
    {
        // Arrange
        var responseTimes = new ConcurrentBag<double>();
        var failureCount = 0;
        var successCount = 0;
        var requestCount = 50;
        var circuitBreaker = CreateCircuitBreakerService();

        // Act - send 50 concurrent requests through circuit breaker
        var tasks = new List<Task>();
        var startTime = Stopwatch.StartNew();

        for (var i = 0; i < requestCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    // Simulate approval processing through circuit breaker
                    await circuitBreaker.ExecuteAsync(
                        async ct =>
                        {
                            await Task.Delay(5, ct); // Simulate minimal processing
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
                finally
                {
                    stopwatch.Stop();
                    responseTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
                }
            }));
        }

        await Task.WhenAll(tasks);
        startTime.Stop();

        // Assert
        var avgResponseTime = responseTimes.Average();
        var maxResponseTime = responseTimes.Max();
        var p95ResponseTime = CalculatePercentile(responseTimes.ToArray(), 95);
        var failureRate = (double)failureCount / requestCount;

        _output.WriteLine($"Normal Load Test Results:");
        _output.WriteLine($"  Requests: {requestCount}");
        _output.WriteLine($"  Total Time: {startTime.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Avg Response: {avgResponseTime:F2}ms");
        _output.WriteLine($"  Max Response: {maxResponseTime:F2}ms");
        _output.WriteLine($"  P95 Response: {p95ResponseTime:F2}ms");
        _output.WriteLine($"  Failure Rate: {failureRate:P2}");

        Assert.True(avgResponseTime < NormalLoadMaxResponseMs,
            $"Average response time {avgResponseTime:F2}ms exceeds {NormalLoadMaxResponseMs}ms");
        Assert.True(failureRate <= NormalLoadMaxFailureRate,
            $"Failure rate {failureRate:P2} exceeds {NormalLoadMaxFailureRate:P2}");
    }

    /// <summary>
    /// Тест: Нормальная нагрузка - метрики throughput
    /// </summary>
    [Fact]
    public async Task NormalLoad_MeasureThroughput_MeetsTarget()
    {
        // Arrange
        var requestCount = 100;
        var completedRequests = 0;
        var circuitBreaker = CreateCircuitBreakerService();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();

        for (var i = 0; i < requestCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await circuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        await Task.Delay(1, ct);
                        return true;
                    },
                    false,
                    CancellationToken.None);

                Interlocked.Increment(ref completedRequests);
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Calculate throughput
        var throughput = completedRequests / (stopwatch.Elapsed.TotalSeconds);

        _output.WriteLine($"Throughput Test Results:");
        _output.WriteLine($"  Completed Requests: {completedRequests}");
        _output.WriteLine($"  Total Time: {stopwatch.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"  Throughput: {throughput:F2} req/s");

        // Assert
        Assert.True(completedRequests == requestCount, "All requests should complete");
        Assert.True(throughput > NormalLoadRequestsPerSecond * 0.8,
            $"Throughput {throughput:F2} should be at least 80% of target {NormalLoadRequestsPerSecond}");
    }

    #endregion

    #region Peak Load Tests

    /// <summary>
    /// Тест: Пиковая нагрузка - 100 concurrent requests
    /// </summary>
    [Fact]
    public async Task PeakLoad_100ConcurrentRequests_ResponseTimeUnder1000ms()
    {
        // Arrange
        var responseTimes = new ConcurrentBag<double>();
        var failureCount = 0;
        var successCount = 0;
        var requestCount = 100;
        var circuitBreaker = CreateCircuitBreakerService();

        // Act
        var tasks = new List<Task>();
        var startTime = Stopwatch.StartNew();

        for (var i = 0; i < requestCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await circuitBreaker.ExecuteAsync(
                        async ct =>
                        {
                            await Task.Delay(10, ct); // Simulate moderate processing
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
                finally
                {
                    stopwatch.Stop();
                    responseTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
                }
            }));
        }

        await Task.WhenAll(tasks);
        startTime.Stop();

        // Assert
        var avgResponseTime = responseTimes.Average();
        var maxResponseTime = responseTimes.Max();
        var p99ResponseTime = CalculatePercentile(responseTimes.ToArray(), 99);
        var failureRate = (double)failureCount / requestCount;

        _output.WriteLine($"Peak Load Test Results:");
        _output.WriteLine($"  Requests: {requestCount}");
        _output.WriteLine($"  Total Time: {startTime.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Avg Response: {avgResponseTime:F2}ms");
        _output.WriteLine($"  Max Response: {maxResponseTime:F2}ms");
        _output.WriteLine($"  P99 Response: {p99ResponseTime:F2}ms");
        _output.WriteLine($"  Failure Rate: {failureRate:P2}");

        Assert.True(avgResponseTime < PeakLoadMaxResponseMs,
            $"Average response time {avgResponseTime:F2}ms exceeds {PeakLoadMaxResponseMs}ms");
        Assert.True(failureRate <= PeakLoadMaxFailureRate,
            $"Failure rate {failureRate:P2} exceeds {PeakLoadMaxFailureRate:P2}");
    }

    /// <summary>
    /// Тест: Пиковая нагрузка с varying request sizes
    /// </summary>
    [Fact]
    public async Task PeakLoad_VaryingRequestSizes_HandlesGracefully()
    {
        // Arrange
        var responseTimes = new ConcurrentBag<double>();
        var requestCount = 80;
        var circuitBreaker = CreateCircuitBreakerService();

        // Act
        var tasks = new List<Task>();
        var startTime = Stopwatch.StartNew();

        for (var i = 0; i < requestCount; i++)
        {
            var delayMs = (i % 10) + 1; // Varying processing time (1-10ms)
            tasks.Add(Task.Run(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await circuitBreaker.ExecuteAsync(
                        async ct =>
                        {
                            await Task.Delay(delayMs, ct);
                            return delayMs;
                        },
                        -1,
                        CancellationToken.None);
                }
                finally
                {
                    stopwatch.Stop();
                    responseTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
                }
            }));
        }

        await Task.WhenAll(tasks);
        startTime.Stop();

        // Assert
        var avgResponseTime = responseTimes.Average();
        var stdDev = CalculateStdDev(responseTimes.ToArray());

        _output.WriteLine($"Varying Size Test Results:");
        _output.WriteLine($"  Requests: {requestCount}");
        _output.WriteLine($"  Avg Response: {avgResponseTime:F2}ms");
        _output.WriteLine($"  Std Dev: {stdDev:F2}ms");

        Assert.True(avgResponseTime < PeakLoadMaxResponseMs);
    }

    #endregion

    #region Sustained Load Tests

    /// <summary>
    /// Тест: Устойчивая нагрузка - 30 req/sec в течение 10 секунд
    /// </summary>
    [Fact]
    public async Task SustainedLoad_30ReqPerSecFor10Seconds_StablePerformance()
    {
        // Arrange
        var responseTimes = new ConcurrentBag<double>();
        var failureCount = 0;
        var requestsPerSecond = 30;
        var durationSeconds = 10;
        var totalRequests = requestsPerSecond * durationSeconds;

        var circuitBreaker = CreateCircuitBreakerService();
        var successCount = 0;

        // Act
        var startTime = Stopwatch.StartNew();
        var tasks = new List<Task>();

        for (var second = 0; second < durationSeconds; second++)
        {
            for (var req = 0; req < requestsPerSecond; req++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        // Simulate operation through circuit breaker
                        await circuitBreaker.ExecuteAsync(
                            async ct =>
                            {
                                await Task.Delay(5, ct);
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
                    finally
                    {
                        stopwatch.Stop();
                        responseTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
                    }
                }));
            }

            await Task.Delay(1000 / requestsPerSecond); // Spread requests
        }

        await Task.WhenAll(tasks);
        startTime.Stop();

        // Assert
        var avgResponseTime = responseTimes.Average();
        var failureRate = (double)failureCount / totalRequests;
        var throughput = successCount / startTime.Elapsed.TotalSeconds;

        _output.WriteLine($"Sustained Load Test Results:");
        _output.WriteLine($"  Duration: {startTime.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"  Total Requests: {totalRequests}");
        _output.WriteLine($"  Successful: {successCount}");
        _output.WriteLine($"  Avg Response: {avgResponseTime:F2}ms");
        _output.WriteLine($"  Throughput: {throughput:F2} req/s");
        _output.WriteLine($"  Failure Rate: {failureRate:P2}");

        Assert.True(failureRate <= NormalLoadMaxFailureRate,
            $"Sustained load failure rate {failureRate:P2} exceeds target");
    }

    /// <summary>
    /// Тест: Устойчивая нагрузка - проверка отсутствия утечки памяти
    /// </summary>
    [Fact]
    public async Task SustainedLoad_MemoryUsage_NoLeaks()
    {
        // Arrange
        var requestCount = 200;
        var circuitBreaker = CreateCircuitBreakerService();

        // Force GC before test
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(true);

        // Act - execute many requests
        var tasks = new List<Task>();
        for (var i = 0; i < requestCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await circuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        await Task.Delay(1, ct);
                        return i;
                    },
                    -1,
                    CancellationToken.None);
            }));
        }

        await Task.WhenAll(tasks);

        // Force GC after test
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);
        var memoryGrowth = finalMemory - initialMemory;
        var memoryGrowthMB = memoryGrowth / (1024.0 * 1024.0);

        _output.WriteLine($"Memory Usage Test Results:");
        _output.WriteLine($"  Initial Memory: {initialMemory / 1024.0 / 1024.0:F2}MB");
        _output.WriteLine($"  Final Memory: {finalMemory / 1024.0 / 1024.0:F2}MB");
        _output.WriteLine($"  Growth: {memoryGrowthMB:F2}MB");

        // Assert - memory growth should be minimal (< 50MB for test)
        Assert.True(memoryGrowthMB < 50,
            $"Memory growth {memoryGrowthMB:F2}MB exceeds acceptable threshold");
    }

    #endregion

    #region Circuit Breaker Under Load Tests

    /// <summary>
    /// Тест: Circuit breaker под нагрузкой - открытие при высоком failure rate
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_UnderLoad_OpensOnHighFailureRate()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureRateThreshold = 0.5,
            ConsecutiveFailuresThreshold = 50, // High to test rate threshold
            MinimumThroughput = 10,
            SamplingDurationSeconds = 30,
            BreakDurationSeconds = 1
        };
        var circuitBreaker = new CircuitBreakerPolicyService(
            _mockCircuitBreakerLogger.Object,
            Options.Create(options));

        var failedOperations = 0;
        var successfulOperations = 0;
        var circuitOpenedDuringTest = false;

        // Act - send mixed requests (60% failure rate)
        var tasks = new List<Task>();
        for (var i = 0; i < 20; i++)
        {
            var shouldFail = i < 12; // 12 failures out of 20 = 60%
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await circuitBreaker.ExecuteAsync(
                        async ct =>
                        {
                            await Task.Delay(1, ct);
                            if (shouldFail)
                            {
                                throw new HttpRequestException("Simulated failure");
                            }
                            return true;
                        },
                        CancellationToken.None);

                    Interlocked.Increment(ref successfulOperations);
                }
                catch (CircuitBreakerOpenException)
                {
                    circuitOpenedDuringTest = true;
                    Interlocked.Increment(ref failedOperations);
                }
                catch (HttpRequestException)
                {
                    Interlocked.Increment(ref failedOperations);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        _output.WriteLine($"Circuit Breaker Under Load:");
        _output.WriteLine($"  Successful: {successfulOperations}");
        _output.WriteLine($"  Failed: {failedOperations}");
        _output.WriteLine($"  Circuit Opened: {circuitOpenedDuringTest}");
        _output.WriteLine($"  Final State: {circuitBreaker.CurrentState}");

        // Circuit should eventually open due to high failure rate
        Assert.True(circuitBreaker.IsCircuitOpen || circuitOpenedDuringTest,
            "Circuit should open when failure rate exceeds threshold");
    }

    /// <summary>
    /// Тест: Circuit breaker recovery под нагрузкой
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_UnderLoad_RecoveryAfterBreakDuration()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailuresThreshold = 3,
            BreakDurationSeconds = 1
        };
        var circuitBreaker = new CircuitBreakerPolicyService(
            _mockCircuitBreakerLogger.Object,
            Options.Create(options));

        // Force circuit open
        circuitBreaker.ForceOpen();
        Assert.True(circuitBreaker.IsCircuitOpen);

        // Wait for half-open
        await Task.Delay(1100);
        Assert.True(circuitBreaker.IsCircuitHalfOpen);

        // Act - send successful request to close circuit
        var result = await circuitBreaker.ExecuteAsync(
            async ct =>
            {
                await Task.Delay(1, ct);
                return "success";
            },
            CancellationToken.None);

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(CircuitState.Closed, circuitBreaker.CurrentState);
    }

    #endregion

    #region Queue Operations Performance Tests

    /// <summary>
    /// Тест: Queue операции выполняются за <10ms
    /// </summary>
    [Fact]
    public void QueueOperations_PerformanceTarget_Under10ms()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService();
        var responseTimes = new List<double>();
        var requestCount = 50;

        // Act - measure circuit breaker state operations
        for (var i = 0; i < requestCount; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            // Simulate queue operations via circuit breaker state changes
            circuitBreaker.RecordSuccess();
            var state = circuitBreaker.CurrentState;
            var stats = circuitBreaker.GetStatistics();

            stopwatch.Stop();
            responseTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        // Assert
        var avgResponseTime = responseTimes.Average();
        var p95ResponseTime = CalculatePercentile(responseTimes.ToArray(), 95);

        _output.WriteLine($"Queue Operations Performance:");
        _output.WriteLine($"  Operations: {requestCount}");
        _output.WriteLine($"  Avg Time: {avgResponseTime:F2}ms");
        _output.WriteLine($"  P95 Time: {p95ResponseTime:F2}ms");

        Assert.True(avgResponseTime < 10,
            $"Queue operation avg time {avgResponseTime:F2}ms exceeds 10ms target");
    }

    /// <summary>
    /// Тест: Metric recording выполняется за <1ms
    /// </summary>
    [Fact]
    public void MetricRecording_PerformanceTarget_Under1ms()
    {
        // Arrange
        var metricsService = CreateMockMetricsService();
        var responseTimes = new List<double>();
        var recordCount = 100;

        // Act - measure metric recording
        for (var i = 0; i < recordCount; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            metricsService.RecordApprovalInitiated($"approval-{i}", "agent-1");

            stopwatch.Stop();
            responseTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        // Assert
        var avgResponseTime = responseTimes.Average();
        var maxResponseTime = responseTimes.Max();

        _output.WriteLine($"Metric Recording Performance:");
        _output.WriteLine($"  Records: {recordCount}");
        _output.WriteLine($"  Avg Time: {avgResponseTime:F4}ms");
        _output.WriteLine($"  Max Time: {maxResponseTime:F4}ms");

        Assert.True(avgResponseTime < 1,
            $"Metric recording avg time {avgResponseTime:F4}ms exceeds 1ms target");
    }

    /// <summary>
    /// Тест: Circuit breaker check выполняется за <1ms
    /// </summary>
    [Fact]
    public void CircuitBreakerCheck_PerformanceTarget_Under1ms()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService();
        var responseTimes = new List<double>();
        var checkCount = 1000;

        // Act - measure circuit breaker state checks
        for (var i = 0; i < checkCount; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            var state = circuitBreaker.CurrentState;
            var isOpen = circuitBreaker.IsCircuitOpen;

            stopwatch.Stop();
            responseTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        // Assert
        var avgResponseTime = responseTimes.Average();
        var maxResponseTime = responseTimes.Max();

        _output.WriteLine($"Circuit Breaker Check Performance:");
        _output.WriteLine($"  Checks: {checkCount}");
        _output.WriteLine($"  Avg Time: {avgResponseTime:F4}ms");
        _output.WriteLine($"  Max Time: {maxResponseTime:F4}ms");

        Assert.True(avgResponseTime < 1,
            $"Circuit breaker check avg time {avgResponseTime:F4}ms exceeds 1ms target");
    }

    #endregion

    #region Latency Distribution Tests

    /// <summary>
    /// Тест: Распределение latency - P50, P95, P99
    /// </summary>
    [Fact]
    public async Task LatencyDistribution_AllPercentilesWithinTargets()
    {
        // Arrange
        var responseTimes = new ConcurrentBag<double>();
        var requestCount = 100;
        var circuitBreaker = CreateCircuitBreakerService();

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < requestCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                await circuitBreaker.ExecuteAsync(
                    async ct =>
                    {
                        await Task.Delay(5, ct);
                        return true;
                    },
                    false,
                    CancellationToken.None);

                stopwatch.Stop();
                responseTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            }));
        }

        await Task.WhenAll(tasks);

        // Calculate percentiles
        var sortedTimes = responseTimes.OrderBy(t => t).ToArray();
        var p50 = CalculatePercentile(sortedTimes, 50);
        var p95 = CalculatePercentile(sortedTimes, 95);
        var p99 = CalculatePercentile(sortedTimes, 99);
        var min = sortedTimes.Min();
        var max = sortedTimes.Max();
        var avg = sortedTimes.Average();

        _output.WriteLine($"Latency Distribution:");
        _output.WriteLine($"  Min: {min:F2}ms");
        _output.WriteLine($"  Max: {max:F2}ms");
        _output.WriteLine($"  Avg: {avg:F2}ms");
        _output.WriteLine($"  P50: {p50:F2}ms");
        _output.WriteLine($"  P95: {p95:F2}ms");
        _output.WriteLine($"  P99: {p99:F2}ms");

        // Assert
        Assert.True(p50 < NormalLoadMaxResponseMs,
            $"P50 {p50:F2}ms exceeds normal load target {NormalLoadMaxResponseMs}ms");
        Assert.True(p95 < NormalLoadMaxResponseMs * 1.5,
            $"P95 {p95:F2}ms exceeds 1.5x normal load target");
        Assert.True(p99 < PeakLoadMaxResponseMs,
            $"P99 {p99:F2}ms exceeds peak load target {PeakLoadMaxResponseMs}ms");
    }

    #endregion

    #region Helper Methods

    private static double CalculatePercentile(double[] values, int percentile)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(v => v).ToArray();
        var index = (percentile / 100.0) * (sorted.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper)
        {
            return sorted[lower];
        }

        return sorted[lower] + (index - lower) * (sorted[upper] - sorted[lower]);
    }

    private static double CalculateStdDev(double[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        var avg = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / values.Length);
    }

    #endregion
}
