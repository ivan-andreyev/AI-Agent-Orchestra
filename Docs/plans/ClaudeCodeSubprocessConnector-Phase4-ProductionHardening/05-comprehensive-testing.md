# Phase 4.5: Comprehensive Testing

**Parent Plan**: [ClaudeCodeSubprocessConnector-Phase4-ProductionHardening.md](../ClaudeCodeSubprocessConnector-Phase4-ProductionHardening.md)
**Estimate**: 3-4 hours
**Priority**: P0 (Essential for production confidence)

## Overview

Create comprehensive test suite covering all production hardening components including retry logic, timeout handling, monitoring metrics, circuit breaker behavior, and integration scenarios. Target 95%+ coverage with focus on edge cases and failure modes.

## Task Breakdown

### Task 5.1: Retry Logic Tests (1 hour)

#### 5.1A: Test Transient Failure Recovery
**Tool Calls**: ~8
- Create Services/Resilience/PolicyRegistryTests.cs
- Test successful retry after transient failure
- Verify retry count and timing

**Test Implementation**:
```csharp
public class PolicyRegistryTests
{
    private readonly Mock<IOptions<TelegramRetryOptions>> _optionsMock;
    private readonly Mock<ILogger<PolicyRegistry>> _loggerMock;
    private readonly PolicyRegistry _policyRegistry;

    [Fact]
    public async Task RetryPolicy_ShouldRecoverFromTransientFailure()
    {
        // Arrange
        var attempts = 0;
        var policy = _policyRegistry.GetTelegramRetryPolicy();

        var handler = new TestMessageHandler(request =>
        {
            attempts++;
            if (attempts <= 2)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = new HttpClient(handler);

        // Act
        var result = await policy.ExecuteAsync(async () =>
            await client.GetAsync("https://api.telegram.org/test"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task RetryPolicy_ShouldExhaustMaxRetries()
    {
        // Arrange
        var attempts = 0;
        var maxRetries = 3;
        var policy = _policyRegistry.GetTelegramRetryPolicy();

        var handler = new TestMessageHandler(request =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        var client = new HttpClient(handler);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await policy.ExecuteAsync(async () =>
                await client.GetAsync("https://api.telegram.org/test")));

        Assert.Equal(maxRetries + 1, attempts); // Initial + retries
    }

    [Theory]
    [InlineData(429)] // Too Many Requests
    [InlineData(500)] // Internal Server Error
    [InlineData(502)] // Bad Gateway
    [InlineData(503)] // Service Unavailable
    [InlineData(504)] // Gateway Timeout
    public async Task RetryPolicy_ShouldRetryOnConfiguredStatusCodes(int statusCode)
    {
        // Arrange
        var attempts = 0;
        var policy = _policyRegistry.GetTelegramRetryPolicy();

        var handler = new TestMessageHandler(request =>
        {
            attempts++;
            if (attempts == 1)
            {
                return new HttpResponseMessage((HttpStatusCode)statusCode);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = new HttpClient(handler);

        // Act
        var result = await policy.ExecuteAsync(async () =>
            await client.GetAsync("https://api.telegram.org/test"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, attempts);
    }
}
```

#### 5.1B: Test Exponential Backoff Timing
**Tool Calls**: ~6
- Test backoff calculation without jitter
- Test jitter distribution
- Verify maximum delay cap

**Timing Tests**:
```csharp
[Fact]
public async Task ExponentialBackoff_ShouldCalculateCorrectDelays()
{
    // Arrange
    var delays = new List<TimeSpan>();
    var options = new TelegramRetryOptions
    {
        MaxRetryAttempts = 4,
        InitialDelayMs = 1000,
        MaxDelayMs = 16000,
        JitterEnabled = false
    };

    var policy = CreatePolicyWithDelayCapture(options, delays);

    // Act
    await policy.ExecuteAsync(async () =>
    {
        throw new HttpRequestException("Test");
    }).ConfigureAwait(false);

    // Assert
    Assert.Equal(4, delays.Count);
    Assert.Equal(1000, delays[0].TotalMilliseconds); // 1st retry: 1s
    Assert.Equal(2000, delays[1].TotalMilliseconds); // 2nd retry: 2s
    Assert.Equal(4000, delays[2].TotalMilliseconds); // 3rd retry: 4s
    Assert.Equal(8000, delays[3].TotalMilliseconds); // 4th retry: 8s
}

[Fact]
public void Jitter_ShouldAddRandomDelay()
{
    // Arrange
    var delaysWithJitter = new List<double>();
    var baseDelay = 1000;

    // Act
    for (int i = 0; i < 100; i++)
    {
        var jitteredDelay = CalculateJitteredDelay(baseDelay);
        delaysWithJitter.Add(jitteredDelay);
    }

    // Assert
    Assert.True(delaysWithJitter.Min() >= baseDelay);
    Assert.True(delaysWithJitter.Max() <= baseDelay + 1000);
    Assert.True(delaysWithJitter.Distinct().Count() > 50); // Good distribution
}
```

#### 5.1C: Test Retry Context and Logging
**Tool Calls**: ~5
- Verify correlation ID propagation
- Test retry logging
- Check metrics recording

**Context Tests**:
```csharp
[Fact]
public async Task RetryPolicy_ShouldPropagateCorrelationId()
{
    // Arrange
    var correlationId = Guid.NewGuid().ToString();
    var capturedContext = new Dictionary<string, object>();

    var policy = CreatePolicyWithContextCapture(capturedContext);

    // Act
    await policy.ExecuteAsync(
        async (context) =>
        {
            if (!context.ContainsKey("retryCount"))
                throw new HttpRequestException();
            return new HttpResponseMessage(HttpStatusCode.OK);
        },
        new Dictionary<string, object> { ["correlationId"] = correlationId });

    // Assert
    Assert.Equal(correlationId, capturedContext["correlationId"]);
}
```

**Acceptance Criteria**:
- ✅ Transient failures recovered
- ✅ Max retries enforced
- ✅ Exponential backoff accurate
- ✅ Jitter properly distributed

### Task 5.2: Timeout Scenario Tests (1 hour)

#### 5.2A: Test Approval Timeout Triggering
**Tool Calls**: ~8
- Create ApprovalTimeoutServiceTests.cs
- Test automatic cancellation
- Verify timer accuracy

**Timeout Tests**:
```csharp
public class ApprovalTimeoutServiceTests
{
    [Fact]
    public async Task ApprovalTimeout_ShouldTriggerAfterConfiguredTime()
    {
        // Arrange
        var options = Options.Create(new ApprovalTimeoutOptions
        {
            DefaultTimeoutMinutes = 1,
            CheckIntervalSeconds = 1
        });

        var mediatorMock = new Mock<IMediator>();
        var cancelCommandCapture = new List<CancelApprovalCommand>();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CancelApprovalCommand>(), default))
            .Callback<IRequest<Unit>, CancellationToken>((cmd, ct) =>
                cancelCommandCapture.Add((CancelApprovalCommand)cmd))
            .ReturnsAsync(Unit.Value);

        var service = new ApprovalTimeoutService(options, mediatorMock.Object, NullLogger.Instance);

        // Act
        await service.StartAsync(CancellationToken.None);
        service.TrackApproval("test-123", "session-456", TimeSpan.FromSeconds(2));

        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert
        Assert.Single(cancelCommandCapture);
        Assert.Equal("test-123", cancelCommandCapture[0].ApprovalId);
    }

    [Fact]
    public void CompleteApproval_ShouldPreventTimeout()
    {
        // Arrange
        var service = CreateService();
        service.TrackApproval("test-123", "session-456");

        // Act
        service.CompleteApproval("test-123");
        var isExpired = service.IsExpired("test-123");

        // Assert
        Assert.False(isExpired);
    }

    [Fact]
    public async Task ConcurrentTimeouts_ShouldAllBeCancelled()
    {
        // Arrange
        var service = CreateService(checkIntervalSeconds: 1);
        var approvalIds = Enumerable.Range(1, 10)
            .Select(i => $"approval-{i}")
            .ToList();

        // Act
        await service.StartAsync(CancellationToken.None);

        foreach (var id in approvalIds)
        {
            service.TrackApproval(id, "session", TimeSpan.FromSeconds(1));
        }

        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert
        foreach (var id in approvalIds)
        {
            Assert.True(service.IsExpired(id));
        }
    }
}
```

#### 5.2B: Test Cancellation Notification
**Tool Calls**: ~6
- Test Telegram notification on timeout
- Verify message content
- Check error handling

**Notification Tests**:
```csharp
[Fact]
public async Task CancelApprovalCommand_ShouldSendTelegramNotification()
{
    // Arrange
    var telegramMock = new Mock<ITelegramEscalationService>();
    var handler = new CancelApprovalCommandHandler(
        telegramMock.Object,
        Mock.Of<IApprovalTimeoutService>(),
        NullLogger.Instance);

    // Act
    await handler.Handle(
        new CancelApprovalCommand("approval-123", "Timeout"),
        CancellationToken.None);

    // Assert
    telegramMock.Verify(t => t.SendNotificationAsync(
        It.Is<string>(msg => msg.Contains("approval-123") && msg.Contains("Timeout"))),
        Times.Once);
}
```

#### 5.2C: Test Memory Cleanup
**Tool Calls**: ~5
- Test memory usage with many approvals
- Verify cleanup after completion
- Check for memory leaks

**Memory Tests**:
```csharp
[Fact]
public async Task TimeoutService_ShouldNotLeakMemory()
{
    // Arrange
    var service = CreateService();
    var initialMemory = GC.GetTotalMemory(true);

    // Act
    for (int i = 0; i < 1000; i++)
    {
        var id = $"approval-{i}";
        service.TrackApproval(id, "session", TimeSpan.FromMinutes(30));
        service.CompleteApproval(id);
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var finalMemory = GC.GetTotalMemory(true);

    // Assert
    var memoryIncrease = finalMemory - initialMemory;
    Assert.True(memoryIncrease < 1024 * 1024); // Less than 1MB increase
}
```

**Acceptance Criteria**:
- ✅ Timeouts trigger correctly
- ✅ Notifications sent
- ✅ Memory cleaned up
- ✅ Concurrent handling works

### Task 5.3: Monitoring & Metrics Tests (1 hour)

#### 5.3A: Test Metric Value Accuracy
**Tool Calls**: ~8
- Create EscalationMetricsServiceTests.cs
- Test counter increments
- Verify histogram values

**Metrics Tests**:
```csharp
public class EscalationMetricsServiceTests
{
    [Fact]
    public void RecordApprovalRequest_ShouldIncrementCounter()
    {
        // Arrange
        var meterFactory = new TestMeterFactory();
        var service = new EscalationMetricsService(meterFactory);

        // Act
        service.RecordApprovalRequest("session-123", "operator-456");
        service.RecordApprovalRequest("session-789", "operator-456");

        // Assert
        var counter = meterFactory.GetCounter("escalation.approval.requests");
        Assert.Equal(2, counter.Value);
    }

    [Fact]
    public void RecordResponseTime_ShouldUpdateHistogram()
    {
        // Arrange
        var meterFactory = new TestMeterFactory();
        var service = new EscalationMetricsService(meterFactory);

        // Act
        service.RecordApprovalResponse("session-123", true, TimeSpan.FromSeconds(5));
        service.RecordApprovalResponse("session-456", false, TimeSpan.FromSeconds(10));

        // Assert
        var histogram = meterFactory.GetHistogram("escalation.response.time");
        Assert.Equal(2, histogram.Count);
        Assert.Equal(7500, histogram.Mean); // (5000 + 10000) / 2
    }

    [Fact]
    public void UpdateQueueSize_ShouldReflectInGauge()
    {
        // Arrange
        var meterFactory = new TestMeterFactory();
        var service = new EscalationMetricsService(meterFactory);

        // Act
        service.UpdateQueueSize(5);
        service.UpdateQueueSize(10);
        service.UpdateQueueSize(3);

        // Assert
        var gauge = meterFactory.GetGauge("escalation.queue.size");
        Assert.Equal(3, gauge.Value);
    }
}
```

#### 5.3B: Test Prometheus Scraping Format
**Tool Calls**: ~6
- Test metrics endpoint
- Verify Prometheus format
- Check metric labels

**Prometheus Tests**:
```csharp
[Fact]
public async Task MetricsEndpoint_ShouldReturnPrometheusFormat()
{
    // Arrange
    var client = _factory.CreateClient();

    // Act
    var response = await client.GetAsync("/metrics");
    var content = await response.Content.ReadAsStringAsync();

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Contains("# TYPE escalation_approval_requests counter", content);
    Assert.Contains("# HELP escalation_approval_requests", content);
    Assert.Contains("escalation_approval_requests{", content);
}
```

#### 5.3C: Test Trace Correlation
**Tool Calls**: ~5
- Test trace ID propagation
- Verify span relationships
- Check exception recording

**Tracing Tests**:
```csharp
[Fact]
public async Task Traces_ShouldCorrelateAcrossAsyncBoundaries()
{
    // Arrange
    var activities = new List<Activity>();
    using var listener = new ActivityListener
    {
        ShouldListenTo = source => source.Name == "Orchestra.Core.Escalation",
        Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            ActivitySamplingResult.AllDataAndRecorded,
        ActivityStarted = activity => activities.Add(activity)
    };

    ActivitySource.AddActivityListener(listener);

    // Act
    using var rootActivity = TelemetryConstants.EscalationActivitySource
        .StartActivity("TestRoot");

    await Task.Run(() =>
    {
        using var childActivity = TelemetryConstants.EscalationActivitySource
            .StartActivity("TestChild");
    });

    // Assert
    Assert.Equal(2, activities.Count);
    Assert.Equal(activities[0].TraceId, activities[1].TraceId);
    Assert.Equal(activities[0].Id, activities[1].ParentId);
}
```

**Acceptance Criteria**:
- ✅ Metrics values accurate
- ✅ Prometheus format correct
- ✅ Traces correlate properly
- ✅ Performance overhead minimal

### Task 5.4: Integration & Load Tests (1 hour)

#### 5.4A: Test Full Approval Flow with Failures
**Tool Calls**: ~10
- Create Integration/FullFlowIntegrationTests.cs
- Test with Telegram failures
- Verify fallback activation

**Integration Tests**:
```csharp
public class FullFlowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task FullApprovalFlow_WithRetryAndTimeout_ShouldComplete()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<IApprovalOrchestrator>();

        // Simulate Telegram being flaky
        var telegramMock = provider.GetRequiredService<Mock<ITelegramBotClient>>();
        var attempts = 0;
        telegramMock
            .Setup(t => t.SendTextMessageAsync(It.IsAny<ChatId>(), It.IsAny<string>(),
                default, default, default, default, default, default, default, default, default))
            .ReturnsAsync(() =>
            {
                attempts++;
                if (attempts < 3)
                    throw new ApiRequestException("Service unavailable", 503);

                return new Message { MessageId = 123 };
            });

        // Act
        var approvalId = await orchestrator.RequestApprovalAsync(new ApprovalRequest
        {
            SessionId = "test-session",
            ToolName = "dangerous-tool",
            Parameters = "rm -rf /"
        });

        // Process approval
        await orchestrator.ProcessApprovalAsync(approvalId, true);

        // Assert
        Assert.Equal(3, attempts); // Should retry twice
        Assert.NotNull(approvalId);
    }

    [Fact]
    public async Task CircuitBreaker_ShouldOpenAfterFailures_AndUseFallback()
    {
        // Arrange
        var services = ConfigureServicesWithCircuitBreaker();
        var provider = services.BuildServiceProvider();

        var escalationService = provider.GetRequiredService<ITelegramEscalationService>();

        // Force failures to open circuit
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await escalationService.SendApprovalRequestAsync(
                    new ApprovalRequest { SessionId = $"session-{i}" });
            }
            catch { /* Expected */ }
        }

        // Act - Circuit should be open, fallback should be used
        var result = await escalationService.SendApprovalRequestAsync(
            new ApprovalRequest { SessionId = "fallback-test" });

        // Assert
        Assert.NotNull(result);
        Assert.Contains("fallback", result, StringComparison.OrdinalIgnoreCase);
    }
}
```

#### 5.4B: Test Circuit Breaker State Transitions
**Tool Calls**: ~6
- Test open -> half-open -> closed
- Verify timing accuracy
- Check state persistence

**Circuit Breaker Tests**:
```csharp
[Fact]
public async Task CircuitBreaker_ShouldTransitionThroughStates()
{
    // Arrange
    var policy = CreateCircuitBreakerPolicy(
        failureThreshold: 2,
        breakDuration: TimeSpan.FromSeconds(1));

    var states = new List<CircuitState>();
    policy.OnBreak += (_, _) => states.Add(CircuitState.Open);
    policy.OnHalfOpen += () => states.Add(CircuitState.HalfOpen);
    policy.OnReset += () => states.Add(CircuitState.Closed);

    // Act
    // Cause failures to open circuit
    for (int i = 0; i < 2; i++)
    {
        try { await policy.ExecuteAsync(ThrowException); }
        catch { }
    }

    // Wait for break duration
    await Task.Delay(TimeSpan.FromSeconds(1.5));

    // Try again - should be half-open
    await policy.ExecuteAsync(SuccessfulOperation);

    // Assert
    Assert.Equal(3, states.Count);
    Assert.Equal(CircuitState.Open, states[0]);
    Assert.Equal(CircuitState.HalfOpen, states[1]);
    Assert.Equal(CircuitState.Closed, states[2]);
}
```

#### 5.4C: Load Test with NBomber
**Tool Calls**: ~8
- Create Load/ApprovalLoadTests.cs
- Test with 100+ concurrent approvals
- Measure performance metrics

**Load Test**:
```csharp
public class ApprovalLoadTests
{
    [Fact]
    public void LoadTest_ShouldHandle100ConcurrentApprovals()
    {
        var scenario = Scenario.Create("approval_load_test", async context =>
        {
            var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

            var requestBody = new
            {
                SessionId = $"session-{context.ScenarioInfo.InstanceId}",
                ToolName = "test-tool",
                Parameters = "test-params"
            };

            var response = await client.PostAsJsonAsync(
                "/api/approvals/request",
                requestBody);

            return response.IsSuccessStatusCode
                ? Response.Ok()
                : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromSeconds(10)),
            Simulation.KeepConstant(copies: 100, during: TimeSpan.FromSeconds(30))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        // Assert
        Assert.True(stats.AllOkCount > 0);
        Assert.True(stats.AllFailCount < stats.AllOkCount * 0.05); // <5% failure rate
        Assert.True(stats.ScenarioStats[0].Ok.Latency.Mean < 1000); // <1s mean latency
    }
}
```

**Acceptance Criteria**:
- ✅ Full flow works with failures
- ✅ Circuit breaker transitions correctly
- ✅ Load test passes with <5% failure
- ✅ Performance meets SLA

## Test Coverage Report

### Coverage Targets
```
Component                   Target    Actual
----------------------------------------
PolicyRegistry              95%       [TBD]
ApprovalTimeoutService      95%       [TBD]
EscalationMetricsService    90%       [TBD]
CircuitBreaker             95%       [TBD]
FallbackService            95%       [TBD]
HealthChecks               90%       [TBD]
Integration Tests          85%       [TBD]
----------------------------------------
Overall                    95%       [TBD]
```

### Test Categories
- **Unit Tests**: 60+ tests
- **Integration Tests**: 15+ tests
- **Load Tests**: 5+ scenarios
- **Total Tests**: 80+ tests

## Success Metrics

### Implementation Checklist
- [ ] Retry logic tests complete
- [ ] Timeout tests complete
- [ ] Metrics tests complete
- [ ] Integration tests complete
- [ ] Load tests passing
- [ ] Coverage targets met

### Quality Metrics
- All tests passing in CI/CD
- No flaky tests
- Test execution time <2 minutes
- Coverage reports generated

## Next Steps

After completing testing:
1. Run full test suite in CI
2. Generate coverage report
3. Document any gaps
4. Create performance baseline
5. Deploy to staging environment

---

**Status**: READY FOR IMPLEMENTATION
**Estimated Completion**: 3-4 hours
**Test Count Target**: 80+ tests
**Coverage Target**: 95%+