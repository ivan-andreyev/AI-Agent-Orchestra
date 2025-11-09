# ClaudeCodeSubprocessConnector - Phase 4: Production Hardening Work Plan

**Priority**: P0 (Critical - blocks production deployment)
**Estimate**: 10-14 hours (1.5-2 days)
**Dependencies**: Phase 3 (Permission Escalation) ✅ COMPLETED

## Executive Summary

Prepare ClaudeCodeSubprocessConnector for production deployment by implementing industry-standard resilience patterns, comprehensive monitoring, and robust error handling. This phase leverages **existing proven libraries** (Polly for retry logic, OpenTelemetry for metrics) rather than building custom solutions, ensuring production-grade reliability and observability.

## Key Deliverables

1. **Resilient Telegram Integration** with Polly retry policies
2. **Timeout Management** for approval requests with configurable limits
3. **OpenTelemetry Metrics** for comprehensive monitoring
4. **Circuit Breaker Pattern** for graceful degradation
5. **Comprehensive Test Coverage** (95%+ for hardening components)
6. **Production Configuration** with environment-specific settings

## Implementation Strategy

### Existing Solutions Leverage
- **Polly 8.x** - Industry-standard resilience library for retry, circuit breaker, timeout policies
- **OpenTelemetry.NET** - Standard observability framework for metrics, traces, logs
- **IHostedService** - Built-in .NET pattern for background timeout monitoring
- **IOptions Pattern** - Standard configuration management for production settings

### Why NOT Custom Implementation
- **Retry Logic**: Polly provides battle-tested exponential backoff with jitter
- **Metrics**: OpenTelemetry is the industry standard with APM integration
- **Circuit Breaker**: Polly's implementation handles edge cases we'd miss
- **Configuration**: IOptions pattern provides hot-reload and validation

## Phase Structure

```
ClaudeCodeSubprocessConnector-Phase4-ProductionHardening.md (THIS FILE - Coordinator)
ClaudeCodeSubprocessConnector-Phase4-ProductionHardening/
├── 01-resilient-telegram.md          (Polly integration for Telegram)
├── 02-timeout-management.md          (Approval timeout handling)
├── 03-monitoring-metrics.md          (OpenTelemetry setup)
├── 04-error-recovery.md              (Circuit breaker & fallback)
└── 05-comprehensive-testing.md       (Test coverage & scenarios)
```

## Phase 1: Resilient Telegram Integration (2-3 hours)

### 1.1 Install and Configure Polly
**Estimate**: 30 minutes
**Deliverables**:
- Add Polly.Extensions.Http NuGet package (v8.x)
- Create IPolicyRegistry for centralized policy management
- Register policies in DI container

### 1.2 Implement Retry Policy for TelegramEscalationService
**Estimate**: 1.5 hours
**Deliverables**:
- Exponential backoff policy with jitter (2^n seconds, max 3 retries)
- Wrap TelegramBotClient calls with retry policy
- Log retry attempts with correlation IDs
- Handle specific Telegram API exceptions (429 Too Many Requests, 503 Service Unavailable)

### 1.3 Add Retry Configuration
**Estimate**: 1 hour
**Deliverables**:
- TelegramRetryOptions in appsettings.json
- IOptions<TelegramRetryOptions> pattern
- Environment-specific overrides (Development vs Production)
- Validation attributes for configuration bounds

**Success Criteria**:
- ✅ All Telegram API calls use retry policy
- ✅ Retry attempts logged with appropriate detail
- ✅ Configuration hot-reloadable without restart
- ✅ Unit tests verify retry behavior

## Phase 2: Timeout Management (2-3 hours)

### 2.1 Create ApprovalTimeoutService
**Estimate**: 1.5 hours
**Deliverables**:
- IApprovalTimeoutService interface
- ApprovalTimeoutService implementation as IHostedService
- In-memory tracking of pending approvals with timestamps
- Background timer checking for expired approvals

### 2.2 Implement Timeout Handling
**Estimate**: 1 hour
**Deliverables**:
- CancelApprovalCommand for timeout scenarios
- Update ProcessHumanApprovalCommandHandler for timeout checks
- Send cancellation notification to Telegram
- Clean up expired approval records

### 2.3 Add Timeout Configuration
**Estimate**: 30 minutes
**Deliverables**:
- ApprovalTimeoutOptions (DefaultTimeout: 30 minutes)
- Per-request timeout override capability
- Grace period for network delays
- Maximum timeout limits for safety

**Success Criteria**:
- ✅ Approvals automatically cancelled after timeout
- ✅ Operators notified of cancellations
- ✅ No memory leaks from pending approvals
- ✅ Configurable timeout per environment

## Phase 3: Monitoring & Metrics (2-3 hours)

### 3.1 Setup OpenTelemetry
**Estimate**: 1 hour
**Deliverables**:
- Add OpenTelemetry.Extensions.Hosting package
- Add OpenTelemetry.Exporter.Prometheus package
- Configure metrics pipeline in Program.cs
- Expose /metrics endpoint for Prometheus scraping

### 3.2 Implement Custom Metrics
**Estimate**: 1.5 hours
**Deliverables**:
- EscalationMetricsService with IMeterFactory
- Metrics to track:
  - `escalation.queue.size` - Current pending approvals
  - `escalation.approval.rate` - Approval vs rejection ratio
  - `escalation.response.time` - Time from request to decision
  - `telegram.api.failures` - Failed Telegram API calls
  - `telegram.retry.count` - Retry attempts per time window
  - `circuit.breaker.state` - Open/Closed/Half-Open states

### 3.3 Add Distributed Tracing
**Estimate**: 30 minutes
**Deliverables**:
- Activity sources for approval flow
- Trace correlation across commands
- Baggage propagation for session context
- Integration with existing logging

**Success Criteria**:
- ✅ Metrics exposed on /metrics endpoint
- ✅ All key operations instrumented
- ✅ Grafana dashboard configuration provided
- ✅ Traces correlate across async boundaries

## Phase 4: Error Handling & Recovery (2-3 hours)

### 4.1 Implement Circuit Breaker
**Estimate**: 1 hour
**Deliverables**:
- Circuit breaker policy via Polly
- Threshold configuration (5 failures in 1 minute)
- Half-open test interval (30 seconds)
- State change notifications

### 4.2 Create Fallback Mechanisms
**Estimate**: 1 hour
**Deliverables**:
- Console warning fallback when Telegram unavailable
- Email notification option (if SMTP configured)
- Local file logging as last resort
- Priority-based fallback selection

### 4.3 Add Health Checks
**Estimate**: 1 hour
**Deliverables**:
- TelegramHealthCheck implementation
- Check Telegram bot connectivity
- Check approval queue health
- Integrate with /health endpoint
- Liveness vs Readiness probes

**Success Criteria**:
- ✅ Circuit breaker prevents cascade failures
- ✅ Fallback mechanisms tested and working
- ✅ Health endpoint returns detailed status
- ✅ Graceful degradation under load

## Phase 5: Comprehensive Testing (3-4 hours)

### 5.1 Retry Logic Tests
**Estimate**: 1 hour
**Tests**:
- Transient failure recovery
- Max retry exhaustion
- Exponential backoff timing
- Jitter distribution verification

### 5.2 Timeout Scenario Tests
**Estimate**: 1 hour
**Tests**:
- Approval timeout triggering
- Cancellation notification delivery
- Concurrent timeout handling
- Memory cleanup verification

### 5.3 Monitoring & Metrics Tests
**Estimate**: 1 hour
**Tests**:
- Metric value accuracy
- Prometheus scraping format
- Trace correlation
- Performance impact measurement

### 5.4 Integration & Load Tests
**Estimate**: 1 hour
**Tests**:
- Full approval flow with failures
- Circuit breaker state transitions
- Fallback mechanism activation
- Concurrent approval handling (10+ simultaneous)

**Success Criteria**:
- ✅ 95%+ code coverage for hardening components
- ✅ All edge cases covered
- ✅ Performance benchmarks documented
- ✅ Load test results meet SLA

## Configuration Structure

```json
{
  "ClaudeCodeSubprocess": {
    "TelegramRetry": {
      "MaxRetryAttempts": 3,
      "InitialDelayMs": 1000,
      "MaxDelayMs": 16000,
      "JitterEnabled": true
    },
    "ApprovalTimeout": {
      "DefaultTimeoutMinutes": 30,
      "MaxTimeoutMinutes": 120,
      "CheckIntervalSeconds": 60,
      "GracePeriodSeconds": 30
    },
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "SamplingDurationSeconds": 60,
      "MinimumThroughput": 2,
      "BreakDurationSeconds": 30
    },
    "Monitoring": {
      "MetricsEnabled": true,
      "TracingEnabled": true,
      "MetricsEndpoint": "/metrics",
      "HealthEndpoint": "/health"
    }
  }
}
```

## File Structure

```
src/Orchestra.Core/
├── Services/
│   ├── Resilience/
│   │   ├── IPolicyRegistry.cs
│   │   └── PolicyRegistry.cs
│   ├── Monitoring/
│   │   ├── IEscalationMetricsService.cs
│   │   └── EscalationMetricsService.cs
│   ├── IApprovalTimeoutService.cs
│   └── ApprovalTimeoutService.cs
├── Options/
│   ├── TelegramRetryOptions.cs
│   ├── ApprovalTimeoutOptions.cs
│   └── CircuitBreakerOptions.cs
├── HealthChecks/
│   └── TelegramHealthCheck.cs
└── Commands/
    └── Permissions/
        ├── CancelApprovalCommand.cs
        └── CancelApprovalCommandHandler.cs

src/Orchestra.Tests/
├── Services/
│   ├── Resilience/
│   │   └── PolicyRegistryTests.cs
│   ├── Monitoring/
│   │   └── EscalationMetricsServiceTests.cs
│   └── ApprovalTimeoutServiceTests.cs
├── HealthChecks/
│   └── TelegramHealthCheckTests.cs
├── Integration/
│   ├── RetryIntegrationTests.cs
│   ├── TimeoutIntegrationTests.cs
│   └── CircuitBreakerIntegrationTests.cs
└── Load/
    └── ApprovalLoadTests.cs
```

## Dependencies to Add

```xml
<!-- Orchestra.Core.csproj -->
<PackageReference Include="Polly.Extensions.Http" Version="8.0.0" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.9.0-rc.1" />

<!-- Orchestra.Tests.csproj -->
<PackageReference Include="NBomber" Version="5.7.0" /> <!-- For load testing -->
```

## Risk Assessment

### Identified Risks
1. **Telegram API Rate Limits** - Mitigated by exponential backoff and circuit breaker
2. **Memory Leaks from Pending Approvals** - Mitigated by timeout service cleanup
3. **Metric Collection Overhead** - Mitigated by sampling and async collection
4. **Circuit Breaker Too Aggressive** - Mitigated by configurable thresholds

### Production Readiness Checklist
- [ ] All retry policies tested under load
- [ ] Timeout mechanism verified with concurrent approvals
- [ ] Metrics validated in Prometheus/Grafana
- [ ] Circuit breaker thresholds tuned for production
- [ ] Fallback mechanisms tested in isolation
- [ ] Health checks integrated with orchestration platform
- [ ] Configuration validated across environments
- [ ] Load tests pass with 100+ concurrent operations
- [ ] Documentation updated with operational runbook
- [ ] Alerts configured for key metrics

## Success Metrics

### Performance Targets
- **Retry Success Rate**: >95% recovery from transient failures
- **Timeout Accuracy**: 100% of expired approvals cancelled
- **Metric Collection Latency**: <5ms overhead per operation
- **Circuit Breaker Response**: <100ms to detect and open
- **Health Check Response**: <50ms for all checks

### Quality Targets
- **Code Coverage**: 95%+ for all hardening components
- **Test Scenarios**: 25+ distinct test cases
- **Documentation**: 100% of public APIs documented
- **Configuration**: All settings have sensible defaults

## Next Steps

After Phase 4 completion:
1. **Phase 5**: Performance Optimization (optional, 4-6 hours)
   - Connection pooling optimization
   - Async/await pattern review
   - Memory allocation profiling

2. **Phase 6**: Security Hardening (optional, 6-8 hours)
   - Rate limiting per operator
   - Approval signing/verification
   - Audit logging enhancement

3. **Production Deployment** (2-3 hours)
   - Environment configuration
   - Monitoring dashboard setup
   - Runbook documentation

## Architecture Integration

This phase integrates with existing architecture:
- **TelegramEscalationService** - Enhanced with Polly retry policies
- **RequestHumanApprovalCommand** - Integrated with timeout tracking
- **ProcessHumanApprovalCommand** - Enhanced with timeout validation
- **AgentSession** - Metrics tracked per session

See architecture documentation: `Docs/Architecture/ClaudeCodeSubprocessConnector-Architecture.md`

---

**Plan Status**: READY FOR REVIEW
**Created**: 2025-11-09
**Author**: work-plan-architect agent
**Validation**: Pending work-plan-reviewer validation