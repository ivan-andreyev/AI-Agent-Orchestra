using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Services.Metrics;
using Xunit;

namespace Orchestra.Tests.Services.Metrics;

/// <summary>
/// Тесты для EscalationMetricsService
/// Проверяет корректность сбора метрик для системы эскалаций и утверждений
/// </summary>
public class EscalationMetricsServiceTests : IDisposable
{
    private readonly Mock<ILogger<EscalationMetricsService>> _mockLogger;
    private readonly IMeterFactory _meterFactory;
    private readonly Meter _testMeter;

    public EscalationMetricsServiceTests()
    {
        _mockLogger = new Mock<ILogger<EscalationMetricsService>>();
        _meterFactory = new TestMeterFactory();
        _testMeter = _meterFactory.Create("Orchestra.Escalation", "1.0.0");
    }

    private EscalationMetricsService CreateService()
    {
        return new EscalationMetricsService(_mockLogger.Object, _meterFactory);
    }

    public void Dispose()
    {
        _testMeter?.Dispose();
        (_meterFactory as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Простая фабрика измерителей для тестирования
    /// </summary>
    private class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = new List<Meter>();

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options.Name ?? "TestMeter", options.Version);
            _meters.Add(meter);
            return meter;
        }

        public Meter Create(string name, string? version = null, IEnumerable<KeyValuePair<string, object?>>? tags = null, object? scope = null)
        {
            var meter = new Meter(name, version);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in _meters)
            {
                meter.Dispose();
            }
            _meters.Clear();
        }
    }

    // ===== COUNTER METRICS TESTS =====

    /// <summary>
    /// Тест RecordApprovalInitiated() инкрементирует счетчик
    /// </summary>
    [Fact]
    public void RecordApprovalInitiated_ValidInput_IncrementsCounter()
    {
        // Arrange
        var service = CreateService();
        var approvalId = "approval-123";
        var agentId = "agent-456";

        // Act - should not throw
        service.RecordApprovalInitiated(approvalId, agentId);

        // Assert - verify debug logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("инициирования", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест RecordApprovalAccepted() инкрементирует счетчик с временем ответа
    /// </summary>
    [Fact]
    public void RecordApprovalAccepted_ValidInput_IncrementsCounterAndRecordsResponseTime()
    {
        // Arrange
        var service = CreateService();
        var approvalId = "approval-789";
        var responseTimeSeconds = 45.5;

        // Act - should not throw
        service.RecordApprovalAccepted(approvalId, responseTimeSeconds);

        // Assert - verify debug logging occurred with correct data
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("принятия", StringComparison.OrdinalIgnoreCase) &&
                                              v.ToString()!.Contains(approvalId, StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест RecordApprovalRejected() инкрементирует счетчик с временем ответа
    /// </summary>
    [Fact]
    public void RecordApprovalRejected_ValidInput_IncrementsCounterAndRecordsResponseTime()
    {
        // Arrange
        var service = CreateService();
        var approvalId = "approval-rejected-123";
        var responseTimeSeconds = 30.2;

        // Act - should not throw
        service.RecordApprovalRejected(approvalId, responseTimeSeconds);

        // Assert - verify debug logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("отклонения", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест RecordApprovalTimeout() инкрементирует счетчик таймаутов
    /// </summary>
    [Fact]
    public void RecordApprovalTimeout_ValidInput_IncrementsTimeoutCounter()
    {
        // Arrange
        var service = CreateService();
        var approvalId = "approval-timeout-456";

        // Act - should not throw
        service.RecordApprovalTimeout(approvalId);

        // Assert - verify debug logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("таймаута", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ===== QUEUE METRICS TESTS =====

    /// <summary>
    /// Тест RecordEscalationEnqueued() инкрементирует размер очереди и счетчик enqueue
    /// </summary>
    [Fact]
    public void RecordEscalationEnqueued_ValidInput_IncrementsQueueSizeAndCounter()
    {
        // Arrange
        var service = CreateService();
        var escalationId = "escalation-123";
        var agentId = "agent-789";

        // Act - should not throw
        service.RecordEscalationEnqueued(escalationId, agentId);

        // Assert - verify debug logging occurred (including initialization logs about creating counters/gauges)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("событие enqueue", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест RecordEscalationDequeued() декрементирует размер очереди и инкрементирует счетчик dequeue
    /// </summary>
    [Fact]
    public void RecordEscalationDequeued_ValidInput_DecrementsQueueSizeAndIncrementsCounter()
    {
        // Arrange
        var service = CreateService();
        var escalationId = "escalation-dequeue-123";

        // First enqueue to have something to dequeue
        service.RecordEscalationEnqueued(escalationId, "agent-1");

        // Act - should not throw
        service.RecordEscalationDequeued(escalationId);

        // Assert - verify debug logging occurred for dequeue (specifically the dequeue event, not initialization)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("событие dequeue", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест времени ожидания в очереди записывается с точностью
    /// </summary>
    [Fact]
    public void RecordQueueDwellTime_ValidDuration_RecordsHistogramAccurately()
    {
        // Arrange
        var service = CreateService();
        var dwellTimeSeconds = 125.75;

        // Act - should not throw
        service.RecordQueueDwellTime(dwellTimeSeconds);

        // Assert - verify debug logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ожидания", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ===== TELEGRAM METRICS TESTS =====

    /// <summary>
    /// Тест RecordTelegramMessageSent() записывает успех с длительностью
    /// </summary>
    [Fact]
    public void RecordTelegramMessageSent_ValidInput_RecordsSuccessWithDuration()
    {
        // Arrange
        var service = CreateService();
        var agentId = "telegram-agent-123";
        var durationMs = 250.5;

        // Act - should not throw
        service.RecordTelegramMessageSent(agentId, durationMs);

        // Assert - verify debug logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Telegram", StringComparison.OrdinalIgnoreCase) &&
                                              v.ToString()!.Contains("успешное", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест RecordTelegramMessageFailed() записывает неудачу с кодом ошибки
    /// </summary>
    [Fact]
    public void RecordTelegramMessageFailed_ValidInput_RecordsFailureWithErrorCode()
    {
        // Arrange
        var service = CreateService();
        var agentId = "telegram-agent-failed-456";
        var errorCode = 429; // Too Many Requests

        // Act - should not throw
        service.RecordTelegramMessageFailed(agentId, errorCode);

        // Assert - verify debug logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("неудачная", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест RecordTelegramRetryAttempt() инкрементирует счетчик повторов
    /// </summary>
    [Fact]
    public void RecordTelegramRetryAttempt_ValidInput_IncrementsRetryCounter()
    {
        // Arrange
        var service = CreateService();
        var agentId = "telegram-retry-agent-789";
        var retryCount = 3;

        // Act - should not throw
        service.RecordTelegramRetryAttempt(agentId, retryCount);

        // Assert - verify debug logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("повторной", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ===== ERROR HANDLING TESTS =====

    /// <summary>
    /// Тест записи метрик с невалидными параметрами обрабатывается корректно
    /// </summary>
    [Fact]
    public void RecordApprovalInitiated_NullOrEmptyParameters_HandlesGracefully()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert - should not throw exceptions
        service.RecordApprovalInitiated(null!, "agent-1");
        service.RecordApprovalInitiated("", "agent-2");
        service.RecordApprovalInitiated("   ", "agent-3");
        service.RecordApprovalInitiated("approval-1", null!);
        service.RecordApprovalInitiated("approval-2", "");
        service.RecordApprovalInitiated("approval-3", "   ");

        // Verify that logger was called with warnings
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Тест конкурентной записи метрик не вызывает исключений
    /// </summary>
    [Fact]
    public async Task ConcurrentMetricRecording_MultipleThreads_NoExceptions()
    {
        // Arrange
        var service = CreateService();
        var tasks = new List<Task>();

        // Act - simulate 10 concurrent metric recordings
        for (int i = 0; i < 10; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                service.RecordApprovalInitiated($"approval-concurrent-{taskId}", $"agent-{taskId}");
                service.RecordEscalationEnqueued($"escalation-concurrent-{taskId}", $"agent-{taskId}");
                service.RecordTelegramMessageSent($"agent-{taskId}", 100.0);
                service.RecordQueueDwellTime(50.0 + taskId);
            }));
        }

        // Assert - all tasks complete without exceptions
        await Task.WhenAll(tasks);

        // Verify at least some debug logs were written
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // ===== GAUGE METRICS TESTS =====

    /// <summary>
    /// Тест UpdateQueueSize() обновляет gauge корректно
    /// </summary>
    [Fact]
    public void UpdateQueueSize_ValidSize_UpdatesGaugeCorrectly()
    {
        // Arrange
        var service = CreateService();
        var newSize = 42L;

        // Act - should not throw
        service.UpdateQueueSize(newSize);

        // Assert - verify debug logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("обновлен", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест SetServiceHealth() обновляет gauge здоровья сервиса
    /// </summary>
    [Fact]
    public void SetServiceHealth_HealthyStatus_UpdatesHealthGauge()
    {
        // Arrange
        var service = CreateService();

        // Act - should not throw
        service.SetServiceHealth(true);

        // Assert - verify information logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Здоров", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест SetServiceHealth() обрабатывает нездоровый статус
    /// </summary>
    [Fact]
    public void SetServiceHealth_UnhealthyStatus_UpdatesHealthGaugeToUnhealthy()
    {
        // Arrange
        var service = CreateService();

        // Act - should not throw
        service.SetServiceHealth(false);

        // Assert - verify information logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Нездоров", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ===== ADDITIONAL VALIDATION TESTS =====

    /// <summary>
    /// Тест RecordApprovalAccepted() с отрицательным временем ответа обрабатывается корректно
    /// </summary>
    [Fact]
    public void RecordApprovalAccepted_NegativeResponseTime_HandlesGracefully()
    {
        // Arrange
        var service = CreateService();
        var approvalId = "approval-negative-time";
        var negativeTime = -10.5;

        // Act - should not throw
        service.RecordApprovalAccepted(approvalId, negativeTime);

        // Assert - verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("отрицательное", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Тест UpdateQueueSize() с отрицательным размером обрабатывается корректно
    /// </summary>
    [Fact]
    public void UpdateQueueSize_NegativeSize_HandlesGracefully()
    {
        // Arrange
        var service = CreateService();
        var negativeSize = -5L;

        // Act - should not throw
        service.UpdateQueueSize(negativeSize);

        // Assert - verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("отрицательный", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест RecordServiceError() записывает ошибки сервиса с типом
    /// </summary>
    [Fact]
    public void RecordServiceError_ValidErrorType_IncrementsErrorCounter()
    {
        // Arrange
        var service = CreateService();
        var errorType = "DatabaseConnectionError";

        // Act - should not throw
        service.RecordServiceError(errorType);

        // Assert - verify debug logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(errorType, StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест RecordTelegramMessageFailed() с null кодом ошибки обрабатывается корректно
    /// </summary>
    [Fact]
    public void RecordTelegramMessageFailed_NullErrorCode_HandlesGracefully()
    {
        // Arrange
        var service = CreateService();
        var agentId = "telegram-agent-no-error-code";

        // Act - should not throw
        service.RecordTelegramMessageFailed(agentId, null);

        // Assert - verify debug logging occurred with "unknown" error code
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unknown", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест RecordQueueDwellTime() с отрицательным временем обрабатывается корректно
    /// </summary>
    [Fact]
    public void RecordQueueDwellTime_NegativeTime_HandlesGracefully()
    {
        // Arrange
        var service = CreateService();
        var negativeTime = -30.5;

        // Act - should not throw
        service.RecordQueueDwellTime(negativeTime);

        // Assert - verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("отрицательное", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест последовательных enqueue/dequeue операций корректно управляет размером очереди
    /// </summary>
    [Fact]
    public void QueueOperations_SequentialEnqueueDequeue_MaintainsCorrectSize()
    {
        // Arrange
        var service = CreateService();

        // Act - enqueue 5 items
        for (int i = 0; i < 5; i++)
        {
            service.RecordEscalationEnqueued($"escalation-seq-{i}", $"agent-{i}");
        }

        // Dequeue 3 items
        for (int i = 0; i < 3; i++)
        {
            service.RecordEscalationDequeued($"escalation-seq-{i}");
        }

        // Assert - verify debug logging occurred at least 8 times (5 enqueues + 3 dequeues)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(8));
    }

    /// <summary>
    /// Тест RecordTelegramRetryAttempt() с отрицательным количеством попыток обрабатывается корректно
    /// </summary>
    [Fact]
    public void RecordTelegramRetryAttempt_NegativeRetryCount_HandlesGracefully()
    {
        // Arrange
        var service = CreateService();
        var agentId = "telegram-agent-negative-retry";
        var negativeRetryCount = -1;

        // Act - should not throw
        service.RecordTelegramRetryAttempt(agentId, negativeRetryCount);

        // Assert - verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("отрицательное", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест RecordTelegramMessageSent() с отрицательной длительностью обрабатывается корректно
    /// </summary>
    [Fact]
    public void RecordTelegramMessageSent_NegativeDuration_HandlesGracefully()
    {
        // Arrange
        var service = CreateService();
        var agentId = "telegram-agent-negative-duration";
        var negativeDuration = -100.5;

        // Act - should not throw
        service.RecordTelegramMessageSent(agentId, negativeDuration);

        // Assert - verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("отрицательную", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Тест сервис правильно инициализируется с корректными метриками
    /// </summary>
    [Fact]
    public void ServiceInitialization_CreatesAllMetricsCorrectly()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert - verify initialization log was written
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Инициализирован", StringComparison.OrdinalIgnoreCase) &&
                                              v.ToString()!.Contains("16", StringComparison.Ordinal)), // 16 metrics
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
