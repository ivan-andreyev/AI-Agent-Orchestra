# Phase 4.4: Error Handling & Recovery

**Parent Plan**: [ClaudeCodeSubprocessConnector-Phase4-ProductionHardening.md](../ClaudeCodeSubprocessConnector-Phase4-ProductionHardening.md)
**Estimate**: 2-3 hours
**Priority**: P0 (Critical for production resilience)

## Overview

Implement circuit breaker pattern using Polly to prevent cascade failures, create fallback mechanisms for when Telegram is unavailable, and add comprehensive health checks for monitoring system status.

## Task Breakdown

### Task 4.1: Implement Circuit Breaker (1 hour)

#### 4.1A: Configure Circuit Breaker Policy
**Tool Calls**: ~6
- Update PolicyRegistry with circuit breaker
- Configure thresholds and timings
- Add state change handlers

**Circuit Breaker Configuration**:
```csharp
public class PolicyRegistry : IPolicyRegistry
{
    private readonly IOptions<CircuitBreakerOptions> _circuitBreakerOptions;
    private readonly IEscalationMetricsService _metricsService;
    private readonly ILogger<PolicyRegistry> _logger;

    public IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        var options = _circuitBreakerOptions.Value;

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: options.FailureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(options.BreakDurationSeconds),
                onBreak: (result, duration) =>
                {
                    _logger.LogWarning(
                        "Circuit breaker opened for {Duration} seconds due to repeated failures",
                        duration.TotalSeconds);

                    _metricsService.RecordCircuitBreakerStateChange("open");

                    // Publish domain event
                    _mediator.Publish(new CircuitBreakerOpenedEvent(duration));
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker closed, normal operation resumed");
                    _metricsService.RecordCircuitBreakerStateChange("closed");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open, testing with next call");
                    _metricsService.RecordCircuitBreakerStateChange("half-open");
                });
    }

    public IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
    {
        // Wrap retry inside circuit breaker for proper ordering
        return Policy.WrapAsync(
            GetCircuitBreakerPolicy(),
            GetTelegramRetryPolicy());
    }
}
```

#### 4.1B: Create Circuit Breaker Options
**Tool Calls**: ~4
- Create Options/CircuitBreakerOptions.cs
- Add validation logic
- Configure defaults

**Options Class**:
```csharp
public class CircuitBreakerOptions
{
    [Range(1, 100)]
    public int FailureThreshold { get; set; } = 5;

    [Range(10, 300)]
    public int SamplingDurationSeconds { get; set; } = 60;

    [Range(1, 10)]
    public int MinimumThroughput { get; set; } = 2;

    [Range(5, 300)]
    public int BreakDurationSeconds { get; set; } = 30;

    public bool EnableAutoReset { get; set; } = true;

    public string[] ExcludeStatusCodes { get; set; } = { "404" };
}
```

#### 4.1C: Integrate Circuit Breaker with TelegramEscalationService
**Tool Calls**: ~6
- Update service to use combined policy
- Handle circuit breaker exceptions
- Add fallback invocation

**Integration Code**:
```csharp
public class TelegramEscalationService : ITelegramEscalationService
{
    private readonly IAsyncPolicy<HttpResponseMessage> _resilientPolicy;
    private readonly IFallbackService _fallbackService;

    public async Task<string> SendApprovalRequestAsync(request)
    {
        try
        {
            return await _resilientPolicy.ExecuteAsync(async () =>
            {
                // Send Telegram message
                return await SendTelegramMessageInternalAsync(request);
            });
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(
                "Circuit breaker is open, using fallback for approval {ApprovalId}",
                request.ApprovalId);

            // Use fallback mechanism
            return await _fallbackService.HandleApprovalRequestAsync(request);
        }
        catch (HttpRequestException ex) when (_resilientPolicy.CircuitState == CircuitState.Open)
        {
            // Circuit just opened, use fallback
            return await _fallbackService.HandleApprovalRequestAsync(request);
        }
    }
}
```

**Acceptance Criteria**:
- ✅ Circuit breaker configured with Polly
- ✅ State changes logged and tracked
- ✅ Proper policy wrapping order
- ✅ Fallback invoked when circuit open

### Task 4.2: Create Fallback Mechanisms (1 hour)

#### 4.2A: Define Fallback Service Interface
**Tool Calls**: ~5
- Create Services/Fallback/IFallbackService.cs
- Create Services/Fallback/FallbackService.cs
- Define fallback strategies

**Fallback Service**:
```csharp
public interface IFallbackService
{
    Task<string> HandleApprovalRequestAsync(ApprovalRequest request);
    Task NotifyOperatorAsync(string message, NotificationPriority priority);
    FallbackStrategy GetActiveStrategy();
}

public enum FallbackStrategy
{
    None,
    Console,
    Email,
    FileLog,
    EventLog
}

public class FallbackService : IFallbackService
{
    private readonly IOptions<FallbackOptions> _options;
    private readonly ILogger<FallbackService> _logger;
    private readonly IEmailService _emailService;
    private readonly IHostEnvironment _environment;

    public async Task<string> HandleApprovalRequestAsync(ApprovalRequest request)
    {
        var strategy = DetermineStrategy();

        _logger.LogWarning(
            "Using fallback strategy {Strategy} for approval {ApprovalId}",
            strategy,
            request.ApprovalId);

        switch (strategy)
        {
            case FallbackStrategy.Console:
                return await HandleViaConsole(request);

            case FallbackStrategy.Email:
                return await HandleViaEmail(request);

            case FallbackStrategy.FileLog:
                return await HandleViaFileLog(request);

            case FallbackStrategy.EventLog:
                return await HandleViaEventLog(request);

            default:
                throw new InvalidOperationException(
                    "No fallback mechanism available for approval requests");
        }
    }

    private async Task<string> HandleViaConsole(ApprovalRequest request)
    {
        var approvalId = Guid.NewGuid().ToString();

        // Write to console with formatting
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("APPROVAL REQUEST - TELEGRAM UNAVAILABLE");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"Approval ID: {approvalId}");
        Console.WriteLine($"Session ID: {request.SessionId}");
        Console.WriteLine($"Tool: {request.ToolName}");
        Console.WriteLine($"Parameters: {request.Parameters}");
        Console.WriteLine($"Requested: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine("ACTION REQUIRED: Manually approve in the application");
        Console.ResetColor();

        // Store for manual processing
        await StoreForManualProcessing(approvalId, request);

        return approvalId;
    }

    private async Task<string> HandleViaEmail(ApprovalRequest request)
    {
        if (!_emailService.IsConfigured)
        {
            // Fall back to next strategy
            return await HandleViaFileLog(request);
        }

        var approvalId = Guid.NewGuid().ToString();

        await _emailService.SendAsync(new EmailMessage
        {
            To = _options.Value.FallbackEmailRecipients,
            Subject = $"[URGENT] Approval Request - {request.ToolName}",
            Body = BuildEmailBody(approvalId, request),
            Priority = MailPriority.High
        });

        await StoreForManualProcessing(approvalId, request);
        return approvalId;
    }

    private async Task<string> HandleViaFileLog(ApprovalRequest request)
    {
        var approvalId = Guid.NewGuid().ToString();
        var logPath = Path.Combine(
            _options.Value.FallbackLogDirectory,
            $"approval_{approvalId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

        var logEntry = new
        {
            ApprovalId = approvalId,
            Request = request,
            Timestamp = DateTime.UtcNow,
            Strategy = FallbackStrategy.FileLog
        };

        await File.WriteAllTextAsync(
            logPath,
            JsonSerializer.Serialize(logEntry, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

        _logger.LogWarning(
            "Approval request logged to file: {FilePath}",
            logPath);

        return approvalId;
    }

    private FallbackStrategy DetermineStrategy()
    {
        // Priority-based selection
        if (_options.Value.EnableConsoleFallback && _environment.IsDevelopment())
            return FallbackStrategy.Console;

        if (_options.Value.EnableEmailFallback && _emailService.IsConfigured)
            return FallbackStrategy.Email;

        if (_options.Value.EnableFileFallback)
            return FallbackStrategy.FileLog;

        if (OperatingSystem.IsWindows() && _options.Value.EnableEventLogFallback)
            return FallbackStrategy.EventLog;

        return FallbackStrategy.None;
    }
}
```

#### 4.2B: Create Fallback Configuration
**Tool Calls**: ~4
- Create Options/FallbackOptions.cs
- Update appsettings.json
- Add environment-specific settings

**Fallback Options**:
```csharp
public class FallbackOptions
{
    public bool EnableConsoleFallback { get; set; } = true;
    public bool EnableEmailFallback { get; set; } = false;
    public bool EnableFileFallback { get; set; } = true;
    public bool EnableEventLogFallback { get; set; } = false;

    public string[] FallbackEmailRecipients { get; set; } = Array.Empty<string>();
    public string FallbackLogDirectory { get; set; } = "Logs/Fallback";

    public int MaxFallbackQueueSize { get; set; } = 100;
    public int FallbackRetentionHours { get; set; } = 24;
}
```

#### 4.2C: Implement Manual Processing Queue
**Tool Calls**: ~6
- Create in-memory queue for manual approvals
- Add API endpoint for manual processing
- Implement cleanup mechanism

**Manual Processing Support**:
```csharp
public interface IManualApprovalQueue
{
    Task<string> EnqueueAsync(ApprovalRequest request);
    Task<ApprovalRequest?> GetPendingAsync(string approvalId);
    Task<IReadOnlyList<ApprovalRequest>> GetAllPendingAsync();
    Task<bool> ProcessManuallyAsync(string approvalId, bool approved);
}

// Add controller endpoint
[HttpPost("approvals/{approvalId}/manual")]
public async Task<IActionResult> ProcessManually(
    string approvalId,
    [FromBody] ManualApprovalRequest request)
{
    var processed = await _manualQueue.ProcessManuallyAsync(
        approvalId,
        request.Approved);

    if (!processed)
        return NotFound($"Approval {approvalId} not found in manual queue");

    return Ok(new { message = "Approval processed manually" });
}
```

**Acceptance Criteria**:
- ✅ Multiple fallback strategies implemented
- ✅ Priority-based strategy selection
- ✅ Console warnings in development
- ✅ File logging as last resort

### Task 4.3: Add Health Checks (1 hour)

#### 4.3A: Create TelegramHealthCheck
**Tool Calls**: ~6
- Create HealthChecks/TelegramHealthCheck.cs
- Implement IHealthCheck interface
- Add connectivity test

**Health Check Implementation**:
```csharp
public class TelegramHealthCheck : IHealthCheck
{
    private readonly ITelegramBotClient _botClient;
    private readonly IOptions<TelegramOptions> _options;
    private readonly IPolicyRegistry _policyRegistry;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check circuit breaker state first
            var circuitState = _policyRegistry.GetCircuitBreakerState();
            if (circuitState == CircuitState.Open)
            {
                return HealthCheckResult.Unhealthy(
                    "Circuit breaker is open",
                    data: new Dictionary<string, object>
                    {
                        ["circuit_state"] = "open",
                        ["service"] = "telegram"
                    });
            }

            // Try to get bot info
            var botInfo = await _botClient.GetMeAsync(cancellationToken);

            if (botInfo == null || string.IsNullOrEmpty(botInfo.Username))
            {
                return HealthCheckResult.Degraded(
                    "Telegram bot info incomplete");
            }

            // Check webhook status if configured
            if (_options.Value.UseWebhook)
            {
                var webhookInfo = await _botClient.GetWebhookInfoAsync(cancellationToken);
                if (webhookInfo.LastErrorDate != null)
                {
                    return HealthCheckResult.Degraded(
                        $"Webhook error: {webhookInfo.LastErrorMessage}",
                        data: new Dictionary<string, object>
                        {
                            ["last_error"] = webhookInfo.LastErrorMessage ?? "Unknown",
                            ["error_date"] = webhookInfo.LastErrorDate.Value
                        });
                }
            }

            return HealthCheckResult.Healthy(
                $"Telegram bot @{botInfo.Username} is operational",
                data: new Dictionary<string, object>
                {
                    ["bot_username"] = botInfo.Username,
                    ["bot_id"] = botInfo.Id,
                    ["circuit_state"] = circuitState.ToString()
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to connect to Telegram",
                ex,
                data: new Dictionary<string, object>
                {
                    ["error_type"] = ex.GetType().Name
                });
        }
    }
}
```

#### 4.3B: Create ApprovalQueueHealthCheck
**Tool Calls**: ~5
- Create HealthChecks/ApprovalQueueHealthCheck.cs
- Check queue size and age
- Add performance metrics

**Queue Health Check**:
```csharp
public class ApprovalQueueHealthCheck : IHealthCheck
{
    private readonly IApprovalTimeoutService _timeoutService;
    private readonly IManualApprovalQueue _manualQueue;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var pendingApprovals = _timeoutService.GetPendingCount();
        var oldestApproval = _timeoutService.GetOldestPendingAge();
        var manualQueueSize = await _manualQueue.GetCountAsync();

        var data = new Dictionary<string, object>
        {
            ["pending_approvals"] = pendingApprovals,
            ["manual_queue_size"] = manualQueueSize,
            ["oldest_approval_minutes"] = oldestApproval?.TotalMinutes ?? 0
        };

        // Check thresholds
        if (pendingApprovals > 100)
        {
            return HealthCheckResult.Unhealthy(
                $"Too many pending approvals: {pendingApprovals}",
                data: data);
        }

        if (oldestApproval?.TotalMinutes > 60)
        {
            return HealthCheckResult.Degraded(
                $"Oldest approval is {oldestApproval.Value.TotalMinutes:F1} minutes old",
                data: data);
        }

        if (manualQueueSize > 50)
        {
            return HealthCheckResult.Degraded(
                $"Manual queue has {manualQueueSize} items",
                data: data);
        }

        return HealthCheckResult.Healthy(
            $"Queue healthy with {pendingApprovals} pending approvals",
            data: data);
    }
}
```

#### 4.3C: Register Health Checks
**Tool Calls**: ~5
- Update Program.cs to register checks
- Configure health check UI
- Add liveness/readiness endpoints

**Registration Code**:
```csharp
// In Program.cs
builder.Services.AddHealthChecks()
    .AddTypeActivatedCheck<TelegramHealthCheck>(
        "telegram",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "external", "messaging" })
    .AddTypeActivatedCheck<ApprovalQueueHealthCheck>(
        "approval_queue",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "internal", "queue" })
    .AddCheck("circuit_breaker", () =>
    {
        var registry = app.Services.GetRequiredService<IPolicyRegistry>();
        var state = registry.GetCircuitBreakerState();

        return state == CircuitState.Open
            ? HealthCheckResult.Unhealthy("Circuit breaker is open")
            : HealthCheckResult.Healthy($"Circuit breaker is {state}");
    },
    tags: new[] { "internal", "resilience" });

// Map endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("internal")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("external")
});
```

**Acceptance Criteria**:
- ✅ Telegram connectivity checked
- ✅ Queue health monitored
- ✅ Circuit breaker state exposed
- ✅ Separate liveness/readiness probes

## Success Metrics

### Implementation Checklist
- [ ] Circuit breaker implemented with Polly
- [ ] Multiple fallback strategies available
- [ ] Health checks for all components
- [ ] Manual processing queue operational
- [ ] Comprehensive error handling
- [ ] Test coverage 95%+

### Performance Metrics
- Circuit breaker opens in <100ms
- Fallback invocation adds <50ms overhead
- Health checks complete in <200ms
- Manual queue operations <10ms

### Operational Metrics
- 100% of circuit breaks logged
- All fallback invocations tracked
- Health status accurate within 1 minute
- Manual approvals processable

## Dependencies

- Polly (for circuit breaker)
- Microsoft.Extensions.Diagnostics.HealthChecks
- AspNetCore.HealthChecks.UI (optional)
- Existing retry policies
- Email service (optional)

## Edge Cases Handled

1. **Rapid Circuit Breaking**: Multiple failures in quick succession
2. **Fallback Cascade**: Primary fallback also fails
3. **Manual Queue Overflow**: Too many manual approvals
4. **Health Check Timeout**: Health check itself times out
5. **State Recovery**: Circuit breaker state after restart

## Next Steps

After completing error recovery:
1. Proceed to Task 5: Comprehensive Testing
2. Test circuit breaker thresholds
3. Verify fallback mechanisms
4. Load test with failures

---

**Status**: READY FOR IMPLEMENTATION
**Estimated Completion**: 2-3 hours
**Test Coverage Target**: 95%+