# Task 3: Implement EscalationMetricsService

**Parent Plan**: [ClaudeCodeSubprocessConnector-Phase4.3-Monitoring-Metrics.md](../ClaudeCodeSubprocessConnector-Phase4.3-Monitoring-Metrics.md)
**Estimate**: 60 minutes
**Priority**: P0 (Core metrics implementation)

## Overview

Create the main metrics service for the escalation system that tracks all approval-related metrics, queue operations, and Telegram API interactions. This service extends MetricsProvider and provides specific methods for recording escalation events.

## Detailed Implementation Steps

### 3.1A: Create EscalationMetricsService Interface (10 minutes)

#### Step 1: Define Service Interface
**File**: src/Orchestra.Core/Services/Metrics/IEscalationMetricsService.cs
**Tool Calls**: 3

```csharp
namespace Orchestra.Core.Services.Metrics
{
    /// <summary>
    /// Сервис метрик для системы эскалации
    /// </summary>
    public interface IEscalationMetricsService : IMetricsProvider
    {
        // Queue Operations
        /// <summary>
        /// Записать добавление элемента в очередь
        /// </summary>
        void RecordEscalationInitiated(string approvalId, string? sessionId = null);

        /// <summary>
        /// Записать удаление элемента из очереди
        /// </summary>
        void RecordEscalationCompleted(string approvalId);

        /// <summary>
        /// Обновить текущий размер очереди
        /// </summary>
        void UpdateQueueSize(int delta);

        /// <summary>
        /// Получить текущий размер очереди
        /// </summary>
        int GetCurrentQueueSize();

        // Approval Metrics
        /// <summary>
        /// Записать принятое одобрение
        /// </summary>
        void RecordApprovalAccepted(string approvalId, double responseTimeSeconds);

        /// <summary>
        /// Записать отклонённое одобрение
        /// </summary>
        void RecordApprovalRejected(string approvalId, double responseTimeSeconds, string? reason = null);

        /// <summary>
        /// Записать истекшее по таймауту одобрение
        /// </summary>
        void RecordApprovalTimeout(string approvalId);

        /// <summary>
        /// Записать время ответа на запрос одобрения
        /// </summary>
        void RecordResponseTime(double seconds, string result);

        // Telegram API Metrics
        /// <summary>
        /// Записать вызов Telegram API
        /// </summary>
        void RecordTelegramCall(
            string endpoint,
            double durationSeconds,
            bool success,
            int retryCount = 0,
            int? statusCode = null);

        /// <summary>
        /// Записать попадание в rate limit
        /// </summary>
        void RecordRateLimitHit(string endpoint);

        // Health Metrics
        /// <summary>
        /// Обновить состояние здоровья сервиса
        /// </summary>
        void UpdateServiceHealth(string serviceName, bool isHealthy);

        /// <summary>
        /// Обновить метрики пула подключений
        /// </summary>
        void UpdateConnectionPoolMetrics(int active, int idle);

        // Statistics
        /// <summary>
        /// Получить статистику метрик
        /// </summary>
        EscalationMetricsSnapshot GetMetricsSnapshot();
    }

    /// <summary>
    /// Снимок текущих метрик
    /// </summary>
    public class EscalationMetricsSnapshot
    {
        public int QueueSize { get; set; }
        public long TotalApprovals { get; set; }
        public long AcceptedApprovals { get; set; }
        public long RejectedApprovals { get; set; }
        public long TimedOutApprovals { get; set; }
        public double AverageResponseTimeSeconds { get; set; }
        public long TelegramApiCalls { get; set; }
        public long TelegramApiFailures { get; set; }
        public Dictionary<string, bool> ServiceHealth { get; set; } = new();
    }
}
```

### 3.2B: Implement EscalationMetricsService (35 minutes)

#### Step 1: Create Main Service Implementation
**File**: src/Orchestra.Core/Services/Metrics/EscalationMetricsService.cs
**Tool Calls**: 10

```csharp
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Options;

namespace Orchestra.Core.Services.Metrics
{
    /// <summary>
    /// Реализация сервиса метрик эскалации
    /// </summary>
    public class EscalationMetricsService : MetricsProvider, IEscalationMetricsService
    {
        private const string MeterNameConst = "Orchestra.Escalation";

        // Queue Metrics
        private readonly Counter<long> _queueEnqueueCounter;
        private readonly Counter<long> _queueDequeueCounter;
        private readonly UpDownCounter<int> _queueSizeCounter;
        private readonly Histogram<double> _queueDepthHistogram;

        // Approval Metrics
        private readonly Counter<long> _approvalsAcceptedCounter;
        private readonly Counter<long> _approvalsRejectedCounter;
        private readonly Counter<long> _approvalsTimeoutCounter;
        private readonly Histogram<double> _responseTimeHistogram;

        // Telegram API Metrics
        private readonly Counter<long> _telegramRequestsCounter;
        private readonly Counter<long> _telegramFailuresCounter;
        private readonly Counter<long> _telegramRetryCounter;
        private readonly Histogram<double> _telegramDurationHistogram;
        private readonly Counter<long> _rateLimitCounter;

        // Health Metrics
        private readonly Dictionary<string, bool> _serviceHealthStatus;
        private readonly ObservableGauge<int> _serviceHealthGauge;
        private readonly ObservableGauge<int> _connectionPoolActiveGauge;
        private readonly ObservableGauge<int> _connectionPoolIdleGauge;

        // Internal state
        private int _currentQueueSize = 0;
        private int _activeConnections = 0;
        private int _idleConnections = 0;
        private readonly object _stateLock = new();

        // Statistics tracking
        private long _totalResponseTimeMs = 0;
        private long _responseCount = 0;

        public EscalationMetricsService(
            IMeterFactory meterFactory,
            IOptions<OpenTelemetryOptions> options,
            ILogger<EscalationMetricsService> logger)
            : base(meterFactory, options, logger, MeterNameConst)
        {
            _serviceHealthStatus = new Dictionary<string, bool>();
            var meter = GetMeter(MeterName);

            // Initialize Queue Metrics
            _queueEnqueueCounter = CreateCounter<long>(
                "escalation_queue_enqueue_rate",
                unit: "items",
                description: "Total items added to escalation queue");

            _queueDequeueCounter = CreateCounter<long>(
                "escalation_queue_dequeue_rate",
                unit: "items",
                description: "Total items removed from escalation queue");

            _queueSizeCounter = CreateUpDownCounter<int>(
                "escalation_queue_size_changes",
                unit: "items",
                description: "Queue size changes tracker");

            _queueDepthHistogram = CreateHistogram<double>(
                "escalation_queue_depth_histogram",
                unit: "items",
                description: "Distribution of queue depths",
                buckets: new[] { 0, 1, 2, 5, 10, 20, 50, 100, 200 });

            // Initialize Approval Metrics
            _approvalsAcceptedCounter = CreateCounter<long>(
                "escalation_approvals_accepted",
                unit: "approvals",
                description: "Total accepted approval requests");

            _approvalsRejectedCounter = CreateCounter<long>(
                "escalation_approvals_rejected",
                unit: "approvals",
                description: "Total rejected approval requests");

            _approvalsTimeoutCounter = CreateCounter<long>(
                "escalation_approvals_timeout",
                unit: "approvals",
                description: "Total timed out approval requests");

            _responseTimeHistogram = CreateHistogram<double>(
                "escalation_response_time_seconds",
                unit: "seconds",
                description: "Time from approval request to decision",
                buckets: new[] { 1, 5, 10, 30, 60, 120, 300, 600, 1800 });

            // Initialize Telegram Metrics
            _telegramRequestsCounter = CreateCounter<long>(
                "telegram_api_requests",
                unit: "requests",
                description: "Total Telegram API requests");

            _telegramFailuresCounter = CreateCounter<long>(
                "telegram_api_failures",
                unit: "failures",
                description: "Failed Telegram API requests");

            _telegramRetryCounter = CreateCounter<long>(
                "telegram_api_retry_attempts",
                unit: "retries",
                description: "Telegram API retry attempts");

            _telegramDurationHistogram = CreateHistogram<double>(
                "telegram_api_duration_seconds",
                unit: "seconds",
                description: "Telegram API call duration",
                buckets: new[] { 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 });

            _rateLimitCounter = CreateCounter<long>(
                "telegram_api_rate_limit_hits",
                unit: "hits",
                description: "Rate limit responses from Telegram");

            // Initialize Health Gauges
            _serviceHealthGauge = CreateGauge<int>(
                "service_health_status",
                () => CalculateOverallHealth(),
                unit: "status",
                description: "Service health status (1=healthy, 0=unhealthy)");

            _connectionPoolActiveGauge = CreateGauge<int>(
                "database_connection_pool_active",
                () => _activeConnections,
                unit: "connections",
                description: "Active database connections");

            _connectionPoolIdleGauge = CreateGauge<int>(
                "database_connection_pool_idle",
                () => _idleConnections,
                unit: "connections",
                description: "Idle database connections");
        }

        // Queue Operations Implementation
        public void RecordEscalationInitiated(string approvalId, string? sessionId = null)
        {
            if (!IsEnabled) return;

            _queueEnqueueCounter.Add(1);
            UpdateQueueSize(1);

            var tags = new[]
            {
                new KeyValuePair<string, object?>("approval_id", approvalId),
                new KeyValuePair<string, object?>("session_id", sessionId ?? "unknown")
            };

            RecordEvent("escalation_initiated", tags.ToDictionary(k => k.Key, v => v.Value));
        }

        public void RecordEscalationCompleted(string approvalId)
        {
            if (!IsEnabled) return;

            _queueDequeueCounter.Add(1);
            UpdateQueueSize(-1);
        }

        public void UpdateQueueSize(int delta)
        {
            if (!IsEnabled) return;

            lock (_stateLock)
            {
                _currentQueueSize = Math.Max(0, _currentQueueSize + delta);
                _queueSizeCounter.Add(delta);
                _queueDepthHistogram.Record(_currentQueueSize);
            }
        }

        public int GetCurrentQueueSize()
        {
            lock (_stateLock)
            {
                return _currentQueueSize;
            }
        }

        // Approval Metrics Implementation
        public void RecordApprovalAccepted(string approvalId, double responseTimeSeconds)
        {
            if (!IsEnabled) return;

            _approvalsAcceptedCounter.Add(1,
                new KeyValuePair<string, object?>("approval_id", approvalId));

            RecordResponseTime(responseTimeSeconds, "accepted");
            RecordEscalationCompleted(approvalId);
        }

        public void RecordApprovalRejected(string approvalId, double responseTimeSeconds, string? reason = null)
        {
            if (!IsEnabled) return;

            var tags = new[]
            {
                new KeyValuePair<string, object?>("approval_id", approvalId),
                new KeyValuePair<string, object?>("reason", reason ?? "unspecified")
            };

            _approvalsRejectedCounter.Add(1, tags);
            RecordResponseTime(responseTimeSeconds, "rejected");
            RecordEscalationCompleted(approvalId);
        }

        public void RecordApprovalTimeout(string approvalId)
        {
            if (!IsEnabled) return;

            _approvalsTimeoutCounter.Add(1,
                new KeyValuePair<string, object?>("approval_id", approvalId));

            RecordEscalationCompleted(approvalId);
        }

        public void RecordResponseTime(double seconds, string result)
        {
            if (!IsEnabled) return;

            _responseTimeHistogram.Record(seconds,
                new KeyValuePair<string, object?>("result", result));

            lock (_stateLock)
            {
                _totalResponseTimeMs += (long)(seconds * 1000);
                _responseCount++;
            }
        }

        // Telegram API Metrics Implementation
        public void RecordTelegramCall(
            string endpoint,
            double durationSeconds,
            bool success,
            int retryCount = 0,
            int? statusCode = null)
        {
            if (!IsEnabled) return;

            var tags = new[]
            {
                new KeyValuePair<string, object?>("endpoint", endpoint),
                new KeyValuePair<string, object?>("method", "POST"),
                new KeyValuePair<string, object?>("status_code", statusCode?.ToString() ?? "unknown")
            };

            _telegramRequestsCounter.Add(1, tags);

            if (!success)
            {
                _telegramFailuresCounter.Add(1, tags);
            }

            if (retryCount > 0)
            {
                _telegramRetryCounter.Add(retryCount,
                    new KeyValuePair<string, object?>("endpoint", endpoint));
            }

            _telegramDurationHistogram.Record(durationSeconds, tags);
        }

        public void RecordRateLimitHit(string endpoint)
        {
            if (!IsEnabled) return;

            _rateLimitCounter.Add(1,
                new KeyValuePair<string, object?>("endpoint", endpoint));
        }

        // Health Metrics Implementation
        public void UpdateServiceHealth(string serviceName, bool isHealthy)
        {
            if (!IsEnabled) return;

            lock (_stateLock)
            {
                _serviceHealthStatus[serviceName] = isHealthy;
            }
        }

        public void UpdateConnectionPoolMetrics(int active, int idle)
        {
            if (!IsEnabled) return;

            lock (_stateLock)
            {
                _activeConnections = active;
                _idleConnections = idle;
            }
        }

        // Statistics Implementation
        public EscalationMetricsSnapshot GetMetricsSnapshot()
        {
            lock (_stateLock)
            {
                var avgResponseTime = _responseCount > 0
                    ? (_totalResponseTimeMs / _responseCount) / 1000.0
                    : 0;

                return new EscalationMetricsSnapshot
                {
                    QueueSize = _currentQueueSize,
                    TotalApprovals = _approvalsAcceptedCounter.GetValue() +
                                   _approvalsRejectedCounter.GetValue() +
                                   _approvalsTimeoutCounter.GetValue(),
                    AcceptedApprovals = _approvalsAcceptedCounter.GetValue(),
                    RejectedApprovals = _approvalsRejectedCounter.GetValue(),
                    TimedOutApprovals = _approvalsTimeoutCounter.GetValue(),
                    AverageResponseTimeSeconds = avgResponseTime,
                    TelegramApiCalls = _telegramRequestsCounter.GetValue(),
                    TelegramApiFailures = _telegramFailuresCounter.GetValue(),
                    ServiceHealth = new Dictionary<string, bool>(_serviceHealthStatus)
                };
            }
        }

        // Helper Methods
        private int CalculateOverallHealth()
        {
            lock (_stateLock)
            {
                return _serviceHealthStatus.Values.All(h => h) ? 1 : 0;
            }
        }
    }

    // Extension to get counter values (for statistics)
    internal static class CounterExtensions
    {
        private static readonly Dictionary<object, long> _counterValues = new();

        public static long GetValue<T>(this Counter<T> counter) where T : struct
        {
            // This is a simplified implementation
            // In production, you'd use proper OpenTelemetry APIs
            _counterValues.TryGetValue(counter, out var value);
            return value;
        }
    }
}
```

### 3.3C: Create Metrics Configuration Helper (15 minutes)

#### Step 1: Create Histogram Bucket Configuration
**File**: src/Orchestra.Core/Services/Metrics/MetricBuckets.cs
**Tool Calls**: 2

```csharp
namespace Orchestra.Core.Services.Metrics
{
    /// <summary>
    /// Конфигурация границ бакетов для гистограмм
    /// </summary>
    public static class MetricBuckets
    {
        /// <summary>
        /// Бакеты для размера очереди (0, 1, 2, 5, 10, 20, 50, 100, 200)
        /// </summary>
        public static double[] QueueDepth => new[] { 0, 1, 2, 5, 10, 20, 50, 100, 200 };

        /// <summary>
        /// Бакеты для времени ответа в секундах (1s, 5s, 10s, 30s, 1m, 2m, 5m, 10m, 30m)
        /// </summary>
        public static double[] ResponseTimeSeconds => new[] { 1, 5, 10, 30, 60, 120, 300, 600, 1800 };

        /// <summary>
        /// Бакеты для длительности API вызовов (100ms, 250ms, 500ms, 1s, 2.5s, 5s, 10s)
        /// </summary>
        public static double[] ApiDurationSeconds => new[] { 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 };

        /// <summary>
        /// Бакеты для количества попыток (0, 1, 2, 3, 5, 10)
        /// </summary>
        public static double[] RetryAttempts => new[] { 0, 1, 2, 3, 5, 10 };
    }
}
```

## Integration Points

### Dependency Injection
```csharp
// In Program.cs or ServiceExtensions
services.AddSingleton<IEscalationMetricsService, EscalationMetricsService>();
```

### Usage in Handlers
```csharp
// Example in RequestHumanApprovalCommandHandler
private readonly IEscalationMetricsService _metrics;

public async Task<string> Handle(RequestHumanApprovalCommand request, CancellationToken cancellationToken)
{
    var approvalId = Guid.NewGuid().ToString();

    // Record metric
    _metrics.RecordEscalationInitiated(approvalId, request.SessionId);

    // ... handler logic

    return approvalId;
}
```

## Validation Checklist

### Implementation
- [ ] EscalationMetricsService compiles without errors
- [ ] All metric types initialized
- [ ] Thread-safe state management
- [ ] Proper disposal pattern

### Metrics
- [ ] All counters increment correctly
- [ ] Gauges report current values
- [ ] Histograms record distributions
- [ ] Tags applied consistently

### Integration
- [ ] Service registered in DI
- [ ] Injectable into handlers
- [ ] No null reference exceptions

## Testing

### Unit Test Examples
```csharp
[Fact]
public void RecordEscalationInitiated_IncrementsQueueSize()
{
    // Arrange
    var service = CreateService();
    var initialSize = service.GetCurrentQueueSize();

    // Act
    service.RecordEscalationInitiated("test-approval-1");

    // Assert
    service.GetCurrentQueueSize().Should().Be(initialSize + 1);
}

[Fact]
public void RecordApprovalAccepted_DecrementQueueSize()
{
    // Arrange
    var service = CreateService();
    service.RecordEscalationInitiated("test-approval-1");
    var queueSize = service.GetCurrentQueueSize();

    // Act
    service.RecordApprovalAccepted("test-approval-1", 5.5);

    // Assert
    service.GetCurrentQueueSize().Should().Be(queueSize - 1);
}

[Fact]
public void ConcurrentUpdates_MaintainsConsistency()
{
    // Arrange
    var service = CreateService();
    var tasks = new List<Task>();

    // Act
    for (int i = 0; i < 100; i++)
    {
        tasks.Add(Task.Run(() => service.UpdateQueueSize(1)));
        tasks.Add(Task.Run(() => service.UpdateQueueSize(-1)));
    }

    Task.WaitAll(tasks.ToArray());

    // Assert
    service.GetCurrentQueueSize().Should().Be(0);
}

[Fact]
public void GetMetricsSnapshot_ReturnsAccurateData()
{
    // Arrange
    var service = CreateService();
    service.RecordEscalationInitiated("approval-1");
    service.RecordApprovalAccepted("approval-1", 10.0);
    service.RecordTelegramCall("sendMessage", 1.5, true, 2, 200);

    // Act
    var snapshot = service.GetMetricsSnapshot();

    // Assert
    snapshot.QueueSize.Should().Be(0);
    snapshot.AcceptedApprovals.Should().BeGreaterThan(0);
    snapshot.TelegramApiCalls.Should().BeGreaterThan(0);
}
```

## Common Issues and Solutions

### Issue 1: Counter Values Not Accessible
**Solution**: Use OpenTelemetry's MetricReader API or export to test collector

### Issue 2: Memory Growth Over Time
**Solution**: Ensure bounded collections, implement periodic cleanup

### Issue 3: Thread Contention on State Lock
**Solution**: Use ReaderWriterLockSlim if read-heavy, or partition state

## Next Steps

1. Proceed to Task 4: Instrument existing services
2. Test metrics collection with sample data
3. Verify Prometheus export format

## Success Criteria

- ✅ All metric types recording correctly
- ✅ Thread-safe queue size tracking
- ✅ Accurate statistics snapshot
- ✅ No memory leaks or excessive allocations
- ✅ Integration with MetricsProvider base class working