# Phase 4.3: Monitoring & Metrics

**Parent Plan**: [ClaudeCodeSubprocessConnector-Phase4-ProductionHardening.md](../ClaudeCodeSubprocessConnector-Phase4-ProductionHardening.md)
**Estimate**: 2-3 hours
**Priority**: P0 (Essential for production observability)

## Overview

Implement comprehensive monitoring using OpenTelemetry, the industry standard for observability. This provides metrics, traces, and integration with APM systems like Prometheus, Grafana, and Azure Monitor.

## Task Breakdown

### Task 3.1: Setup OpenTelemetry (1 hour)

#### 3.1A: Install OpenTelemetry Packages
**Tool Calls**: ~5
- Edit Orchestra.Core.csproj to add packages
- Edit Orchestra.API.csproj for ASP.NET instrumentation
- Run dotnet restore

**Package References**:
```xml
<!-- Orchestra.Core.csproj -->
<PackageReference Include="OpenTelemetry" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.10.0" />

<!-- Orchestra.API.csproj -->
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.10.0-beta.1" />
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.10.0" /> <!-- For debugging -->
```

#### 3.1B: Configure OpenTelemetry in Program.cs
**Tool Calls**: ~8
- Configure metrics pipeline
- Setup Prometheus exporter
- Add ASP.NET Core instrumentation
- Configure resource attributes

**Configuration Code**:
```csharp
// In Program.cs
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "AI-Agent-Orchestra",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            serviceInstanceId: Environment.MachineName))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddMeter("Orchestra.Core.Escalation")  // Custom metrics
        .AddMeter("Orchestra.Core.ClaudeCode")
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .AddHttpClientInstrumentation()
        .AddSource("Orchestra.Core.Escalation")
        .AddConsoleExporter()); // Remove in production

// Add Prometheus scraping endpoint
app.UseOpenTelemetryPrometheusScrapingEndpoint(
    context => context.Request.Path == "/metrics");
```

#### 3.1C: Create Metrics Configuration Options
**Tool Calls**: ~5
- Create Options/MonitoringOptions.cs
- Add configuration section
- Support feature toggles

**Options Class**:
```csharp
public class MonitoringOptions
{
    public bool MetricsEnabled { get; set; } = true;
    public bool TracingEnabled { get; set; } = true;
    public string MetricsEndpoint { get; set; } = "/metrics";
    public bool DetailedMetrics { get; set; } = false;
    public int HistogramBuckets { get; set; } = 10;
    public string[] CustomDimensions { get; set; } = Array.Empty<string>();
}
```

**Acceptance Criteria**:
- ✅ OpenTelemetry packages installed
- ✅ Metrics pipeline configured
- ✅ Prometheus endpoint exposed at /metrics
- ✅ Basic runtime metrics available

### Task 3.2: Implement Custom Metrics (1.5 hours)

#### 3.2A: Create EscalationMetricsService
**Tool Calls**: ~10
- Create Services/Monitoring/IEscalationMetricsService.cs
- Create Services/Monitoring/EscalationMetricsService.cs
- Define custom meters and instruments
- Register in DI container

**Service Implementation**:
```csharp
public interface IEscalationMetricsService
{
    void RecordApprovalRequest(string sessionId, string operatorId);
    void RecordApprovalResponse(string sessionId, bool approved, TimeSpan duration);
    void RecordTelegramFailure(string failureType);
    void RecordRetryAttempt(int attemptNumber);
    void RecordCircuitBreakerStateChange(string newState);
    void UpdateQueueSize(int size);
}

public class EscalationMetricsService : IEscalationMetricsService
{
    private readonly Meter _meter;
    private readonly Counter<long> _approvalRequestCounter;
    private readonly Counter<long> _approvalResponseCounter;
    private readonly Histogram<double> _responseTimeHistogram;
    private readonly Counter<long> _telegramFailureCounter;
    private readonly Counter<long> _retryCounter;
    private readonly ObservableGauge<int> _queueSizeGauge;
    private readonly UpDownCounter<int> _circuitBreakerState;

    private int _currentQueueSize = 0;

    public EscalationMetricsService(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("Orchestra.Core.Escalation", "1.0.0");

        _approvalRequestCounter = _meter.CreateCounter<long>(
            "escalation.approval.requests",
            description: "Total number of approval requests sent");

        _approvalResponseCounter = _meter.CreateCounter<long>(
            "escalation.approval.responses",
            description: "Total number of approval responses received");

        _responseTimeHistogram = _meter.CreateHistogram<double>(
            "escalation.response.time",
            unit: "ms",
            description: "Time from approval request to response");

        _telegramFailureCounter = _meter.CreateCounter<long>(
            "telegram.api.failures",
            description: "Number of Telegram API failures");

        _retryCounter = _meter.CreateCounter<long>(
            "telegram.retry.attempts",
            description: "Number of retry attempts");

        _queueSizeGauge = _meter.CreateObservableGauge<int>(
            "escalation.queue.size",
            () => _currentQueueSize,
            description: "Current number of pending approvals");

        _circuitBreakerState = _meter.CreateUpDownCounter<int>(
            "circuit.breaker.state",
            description: "Circuit breaker state (0=closed, 1=open, 2=half-open)");
    }

    public void RecordApprovalRequest(string sessionId, string operatorId)
    {
        _approvalRequestCounter.Add(1,
            new KeyValuePair<string, object?>("session_id", sessionId),
            new KeyValuePair<string, object?>("operator_id", operatorId));
    }

    public void RecordApprovalResponse(string sessionId, bool approved, TimeSpan duration)
    {
        _approvalResponseCounter.Add(1,
            new KeyValuePair<string, object?>("session_id", sessionId),
            new KeyValuePair<string, object?>("approved", approved));

        _responseTimeHistogram.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("approved", approved));
    }

    public void RecordTelegramFailure(string failureType)
    {
        _telegramFailureCounter.Add(1,
            new KeyValuePair<string, object?>("failure_type", failureType));
    }

    public void UpdateQueueSize(int size)
    {
        Interlocked.Exchange(ref _currentQueueSize, size);
    }
}
```

#### 3.2B: Integrate Metrics into Services
**Tool Calls**: ~8
- Update TelegramEscalationService to record metrics
- Update ApprovalTimeoutService to track queue size
- Update command handlers to record timings
- Add correlation with traces

**Integration Examples**:
```csharp
// In TelegramEscalationService
public async Task<string> SendApprovalRequestAsync(request)
{
    var stopwatch = Stopwatch.StartNew();
    _metricsService.RecordApprovalRequest(request.SessionId, request.OperatorId);

    try
    {
        // Send message...
        return messageId;
    }
    catch (Exception ex)
    {
        _metricsService.RecordTelegramFailure(ex.GetType().Name);
        throw;
    }
}

// In ProcessHumanApprovalCommandHandler
public async Task<Unit> Handle(command, cancellationToken)
{
    var approval = await GetApprovalDetails(command.ApprovalId);
    var duration = DateTime.UtcNow - approval.RequestedAt;

    _metricsService.RecordApprovalResponse(
        approval.SessionId,
        command.Approved,
        duration);

    // Process approval...
}

// In PolicyRegistry (retry handler)
onRetry: (outcome, timespan, retryCount, context) =>
{
    _metricsService.RecordRetryAttempt(retryCount);
    // Log retry...
}
```

#### 3.2C: Add Calculated Metrics
**Tool Calls**: ~6
- Create approval rate calculator
- Add response time percentiles
- Calculate retry success rate
- Track circuit breaker effectiveness

**Calculated Metrics**:
```csharp
// Add to EscalationMetricsService constructor
_meter.CreateObservableGauge<double>(
    "escalation.approval.rate",
    () => CalculateApprovalRate(),
    description: "Percentage of approvals vs rejections");

_meter.CreateObservableGauge<double>(
    "telegram.retry.success.rate",
    () => CalculateRetrySuccessRate(),
    description: "Percentage of successful retries");

private double CalculateApprovalRate()
{
    // Calculate from counters
    return (_approvedCount / (double)(_approvedCount + _rejectedCount)) * 100;
}
```

**Acceptance Criteria**:
- ✅ All custom metrics defined and registered
- ✅ Metrics integrated into service calls
- ✅ Queue size tracked accurately
- ✅ Response times measured in milliseconds

### Task 3.3: Add Distributed Tracing (30 minutes)

#### 3.3A: Create Activity Sources
**Tool Calls**: ~5
- Define activity sources for escalation flow
- Add to OpenTelemetry configuration
- Create span attributes

**Activity Source Setup**:
```csharp
public class TelemetryConstants
{
    public static readonly ActivitySource EscalationActivitySource =
        new("Orchestra.Core.Escalation", "1.0.0");

    public static class SpanAttributes
    {
        public const string ApprovalId = "approval.id";
        public const string SessionId = "session.id";
        public const string OperatorId = "operator.id";
        public const string Approved = "approval.approved";
        public const string TimeoutOccurred = "approval.timeout";
    }
}

// In services
using var activity = TelemetryConstants.EscalationActivitySource
    .StartActivity("SendApprovalRequest", ActivityKind.Client);

activity?.SetTag(TelemetryConstants.SpanAttributes.SessionId, sessionId);
activity?.SetTag(TelemetryConstants.SpanAttributes.OperatorId, operatorId);
```

#### 3.3B: Implement Trace Context Propagation
**Tool Calls**: ~6
- Add baggage for session context
- Ensure trace IDs flow through async calls
- Link related activities
- Add exception recording

**Context Propagation**:
```csharp
// Propagate context through MediatR pipeline
public class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity(
            $"MediatR.{typeof(TRequest).Name}",
            ActivityKind.Internal);

        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

#### 3.3C: Add Trace Visualization Support
**Tool Calls**: ~4
- Configure trace sampling
- Add trace metadata
- Support correlation IDs
- Enable W3C trace context

**Acceptance Criteria**:
- ✅ Traces span async boundaries
- ✅ Exceptions recorded in traces
- ✅ Correlation IDs maintained
- ✅ W3C trace context supported

### Task 3.4: Create Grafana Dashboard Configuration (30 minutes)

#### 3.4A: Create Dashboard JSON
**Tool Calls**: ~5
- Create Monitoring/grafana-dashboard.json
- Define panels for key metrics
- Add alert rules
- Configure data sources

**Dashboard Panels**:
```json
{
  "dashboard": {
    "title": "ClaudeCode Subprocess Connector",
    "panels": [
      {
        "title": "Approval Queue Size",
        "targets": [{
          "expr": "escalation_queue_size"
        }]
      },
      {
        "title": "Approval Response Time (p95)",
        "targets": [{
          "expr": "histogram_quantile(0.95, escalation_response_time_bucket)"
        }]
      },
      {
        "title": "Telegram API Failures",
        "targets": [{
          "expr": "rate(telegram_api_failures_total[5m])"
        }]
      },
      {
        "title": "Retry Success Rate",
        "targets": [{
          "expr": "telegram_retry_success_rate"
        }]
      },
      {
        "title": "Circuit Breaker State",
        "targets": [{
          "expr": "circuit_breaker_state"
        }]
      }
    ]
  }
}
```

#### 3.4B: Create Alert Rules
**Tool Calls**: ~4
- Define Prometheus alert rules
- Set thresholds for key metrics
- Configure notification channels

**Alert Rules**:
```yaml
groups:
- name: escalation_alerts
  rules:
  - alert: HighApprovalQueueSize
    expr: escalation_queue_size > 50
    for: 5m
    annotations:
      summary: "High number of pending approvals"

  - alert: TelegramAPIFailureRate
    expr: rate(telegram_api_failures_total[5m]) > 0.1
    for: 2m
    annotations:
      summary: "High Telegram API failure rate"

  - alert: CircuitBreakerOpen
    expr: circuit_breaker_state == 1
    for: 1m
    annotations:
      summary: "Circuit breaker is open"
```

**Acceptance Criteria**:
- ✅ Dashboard JSON importable to Grafana
- ✅ All key metrics visualized
- ✅ Alert rules defined
- ✅ Documentation for setup

## Success Metrics

### Implementation Checklist
- [ ] OpenTelemetry fully configured
- [ ] Prometheus endpoint exposing metrics
- [ ] Custom metrics implemented
- [ ] Distributed tracing operational
- [ ] Grafana dashboard configured
- [ ] Alert rules defined

### Performance Metrics
- Metrics collection overhead <5ms per operation
- Prometheus scrape time <100ms
- Trace sampling appropriate for load
- No memory leaks from metrics

### Operational Metrics
- All key operations instrumented
- Metrics available within 1 minute
- Traces correlate across services
- Dashboard updates in real-time

## Dependencies

- OpenTelemetry 1.10.0
- OpenTelemetry.Extensions.Hosting
- OpenTelemetry.Instrumentation.AspNetCore
- OpenTelemetry.Exporter.Prometheus.AspNetCore
- IMeterFactory (.NET 8+)

## Production Considerations

1. **Cardinality Control**: Limit label values to prevent explosion
2. **Sampling Strategy**: Configure appropriate trace sampling
3. **Retention Policy**: Define metric retention periods
4. **Export Batching**: Configure batch export for efficiency
5. **Security**: Protect /metrics endpoint if needed

## Next Steps

After completing monitoring setup:
1. Proceed to Task 4: Error Handling & Recovery
2. Integrate metrics with circuit breaker
3. Setup production APM system
4. Configure alerting channels

---

**Status**: READY FOR IMPLEMENTATION
**Estimated Completion**: 2-3 hours
**Test Coverage Target**: 90%+ (metrics are cross-cutting)