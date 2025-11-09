using System;
using System.Diagnostics.Metrics;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Orchestra.Core.Services.Metrics;

/// <summary>
/// Сервис для сбора метрик системы эскалации и утверждений.
/// Собирает данные о производительности очереди, времени ответа, Telegram API и здоровье системы.
/// </summary>
/// <remarks>
/// Этот сервис предоставляет метрики для мониторинга:
/// - Состояния очереди эскалаций (размер, скорость обработки)
/// - Статистики утверждений (принятые, отклоненные, истекшие)
/// - Производительности Telegram API (успешные/неудачные вызовы, повторы)
/// - Общего здоровья системы эскалаций
///
/// Все операции потокобезопасны и не влияют на основную бизнес-логику.
/// </remarks>
public class EscalationMetricsService : MetricsProvider
{
    // ===== QUEUE METRICS =====

    /// <summary>
    /// Счетчик добавленных эскалаций в очередь.
    /// </summary>
    private readonly Counter<long>? _queueEnqueuedCounter;

    /// <summary>
    /// Счетчик обработанных эскалаций из очереди.
    /// </summary>
    private readonly Counter<long>? _queueDequeuedCounter;

    /// <summary>
    /// Наблюдаемый gauge для текущего размера очереди.
    /// </summary>
    private readonly ObservableGauge<long>? _queueSizeGauge;

    /// <summary>
    /// Гистограмма времени ожидания в очереди (в секундах).
    /// </summary>
    private readonly Histogram<double>? _queueDepthHistogram;

    /// <summary>
    /// Текущий размер очереди (атомарная переменная для потокобезопасности).
    /// </summary>
    private long _currentQueueSize = 0;

    // ===== APPROVAL METRICS =====

    /// <summary>
    /// Счетчик инициированных запросов на утверждение.
    /// </summary>
    private readonly Counter<long>? _approvalInitiatedCounter;

    /// <summary>
    /// Счетчик принятых утверждений.
    /// </summary>
    private readonly Counter<long>? _approvalAcceptedCounter;

    /// <summary>
    /// Счетчик отклоненных утверждений.
    /// </summary>
    private readonly Counter<long>? _approvalRejectedCounter;

    /// <summary>
    /// Счетчик истекших утверждений (таймаут).
    /// </summary>
    private readonly Counter<long>? _approvalTimeoutCounter;

    /// <summary>
    /// Гистограмма времени ответа на утверждение (в секундах).
    /// </summary>
    private readonly Histogram<double>? _approvalResponseTimeHistogram;

    // ===== TELEGRAM API METRICS =====

    /// <summary>
    /// Счетчик успешно отправленных сообщений Telegram.
    /// </summary>
    private readonly Counter<long>? _telegramMessagesSentCounter;

    /// <summary>
    /// Счетчик неудачных отправок сообщений Telegram.
    /// </summary>
    private readonly Counter<long>? _telegramMessagesFailedCounter;

    /// <summary>
    /// Счетчик попыток повторной отправки через Telegram API.
    /// </summary>
    private readonly Counter<long>? _telegramApiRetriesCounter;

    /// <summary>
    /// Гистограмма длительности вызовов Telegram API (в миллисекундах).
    /// </summary>
    private readonly Histogram<double>? _telegramApiDurationHistogram;

    /// <summary>
    /// Наблюдаемый gauge для последнего кода ошибки Telegram API.
    /// </summary>
    private readonly ObservableGauge<long>? _telegramApiLastErrorCodeGauge;

    /// <summary>
    /// Последний код ошибки Telegram API (атомарная переменная).
    /// </summary>
    private long _lastTelegramErrorCode = 0;

    // ===== SYSTEM HEALTH METRICS =====

    /// <summary>
    /// Наблюдаемый gauge для статуса здоровья сервиса (1=здоров, 0=нездоров).
    /// </summary>
    private readonly ObservableGauge<long>? _serviceHealthGauge;

    /// <summary>
    /// Счетчик общего количества ошибок в сервисе эскалаций.
    /// </summary>
    private readonly Counter<long>? _serviceErrorsCounter;

    /// <summary>
    /// Текущий статус здоровья сервиса (атомарная переменная).
    /// </summary>
    private long _isServiceHealthy = 1; // 1 = healthy, 0 = unhealthy

    /// <summary>
    /// Инициализирует новый экземпляр сервиса метрик эскалаций.
    /// </summary>
    /// <param name="logger">Логгер для записи диагностической информации.</param>
    /// <param name="meterFactory">Фабрика для создания измерителей метрик.</param>
    /// <exception cref="ArgumentNullException">Если logger или meterFactory равны null.</exception>
    public EscalationMetricsService(
        ILogger<EscalationMetricsService> logger,
        IMeterFactory meterFactory)
        : base(logger, meterFactory, "Orchestra.Escalation", "1.0.0")
    {
        // Initialize Queue Metrics
        _queueEnqueuedCounter = CreateCounter(
            "escalation_queue_enqueued_total",
            "Общее количество эскалаций, добавленных в очередь");

        _queueDequeuedCounter = CreateCounter(
            "escalation_queue_dequeued_total",
            "Общее количество эскалаций, обработанных из очереди");

        _queueSizeGauge = CreateObservableGauge(
            "escalation_queue_size",
            "Текущее количество ожидающих эскалаций в очереди",
            () => Interlocked.Read(ref _currentQueueSize));

        _queueDepthHistogram = CreateHistogram(
            "escalation_queue_depth_seconds",
            "Время ожидания эскалации в очереди",
            "s");

        // Initialize Approval Metrics
        _approvalInitiatedCounter = CreateCounter(
            "approval_requests_initiated_total",
            "Общее количество инициированных запросов на утверждение");

        _approvalAcceptedCounter = CreateCounter(
            "approval_requests_accepted_total",
            "Общее количество принятых утверждений");

        _approvalRejectedCounter = CreateCounter(
            "approval_requests_rejected_total",
            "Общее количество отклоненных утверждений");

        _approvalTimeoutCounter = CreateCounter(
            "approval_requests_timeout_total",
            "Общее количество утверждений, истекших по таймауту");

        _approvalResponseTimeHistogram = CreateHistogram(
            "approval_response_time_seconds",
            "Время от запроса утверждения до ответа",
            "s");

        // Initialize Telegram API Metrics
        _telegramMessagesSentCounter = CreateCounter(
            "telegram_messages_sent_total",
            "Общее количество успешно отправленных сообщений Telegram");

        _telegramMessagesFailedCounter = CreateCounter(
            "telegram_messages_failed_total",
            "Общее количество неудачных отправок сообщений Telegram");

        _telegramApiRetriesCounter = CreateCounter(
            "telegram_api_retries_total",
            "Общее количество попыток повторной отправки через Telegram API");

        _telegramApiDurationHistogram = CreateHistogram(
            "telegram_api_call_duration_seconds",
            "Длительность вызовов Telegram API",
            "s");

        _telegramApiLastErrorCodeGauge = CreateObservableGauge(
            "telegram_api_last_error_code",
            "Последний код ошибки Telegram API",
            () => Interlocked.Read(ref _lastTelegramErrorCode));

        // Initialize System Health Metrics
        _serviceHealthGauge = CreateObservableGauge(
            "escalation_service_healthy",
            "Статус здоровья сервиса эскалаций (1=здоров, 0=нездоров)",
            () => Interlocked.Read(ref _isServiceHealthy));

        _serviceErrorsCounter = CreateCounter(
            "escalation_service_errors_total",
            "Общее количество ошибок в сервисе эскалаций");

        Logger.LogInformation(
            "Инициализирован EscalationMetricsService с {MetricCount} метриками",
            16);
    }

    // ===== QUEUE METRIC RECORDING METHODS =====

    /// <summary>
    /// Записывает событие добавления эскалации в очередь.
    /// </summary>
    /// <param name="escalationId">Идентификатор эскалации.</param>
    /// <param name="agentId">Идентификатор агента, инициировавшего эскалацию.</param>
    public void RecordEscalationEnqueued(string escalationId, string agentId)
    {
        if (string.IsNullOrWhiteSpace(escalationId) || string.IsNullOrWhiteSpace(agentId))
        {
            Logger.LogWarning("Попытка записать enqueue событие с пустым escalationId или agentId");
            return;
        }

        SafeAdd(_queueEnqueuedCounter, 1,
            new KeyValuePair<string, object?>("escalation_id", escalationId),
            new KeyValuePair<string, object?>("agent_id", agentId));

        // Увеличиваем размер очереди
        Interlocked.Increment(ref _currentQueueSize);

        Logger.LogDebug(
            "Записано событие enqueue для эскалации {EscalationId} от агента {AgentId}. Размер очереди: {QueueSize}",
            escalationId,
            agentId,
            _currentQueueSize);
    }

    /// <summary>
    /// Записывает событие обработки эскалации из очереди.
    /// </summary>
    /// <param name="escalationId">Идентификатор обработанной эскалации.</param>
    public void RecordEscalationDequeued(string escalationId)
    {
        if (string.IsNullOrWhiteSpace(escalationId))
        {
            Logger.LogWarning("Попытка записать dequeue событие с пустым escalationId");
            return;
        }

        SafeAdd(_queueDequeuedCounter, 1,
            new KeyValuePair<string, object?>("escalation_id", escalationId));

        // Уменьшаем размер очереди (но не ниже 0)
        long newSize;
        long currentSize;
        do
        {
            currentSize = Interlocked.Read(ref _currentQueueSize);
            newSize = Math.Max(0, currentSize - 1);
        }
        while (Interlocked.CompareExchange(ref _currentQueueSize, newSize, currentSize) != currentSize);

        Logger.LogDebug(
            "Записано событие dequeue для эскалации {EscalationId}. Размер очереди: {QueueSize}",
            escalationId,
            newSize);
    }

    /// <summary>
    /// Обновляет текущий размер очереди эскалаций.
    /// </summary>
    /// <param name="size">Новый размер очереди.</param>
    /// <remarks>
    /// Используется для синхронизации метрики с фактическим состоянием очереди
    /// (например, после перезапуска сервиса или восстановления из базы данных).
    /// </remarks>
    public void UpdateQueueSize(long size)
    {
        if (size < 0)
        {
            Logger.LogWarning("Попытка установить отрицательный размер очереди: {Size}", size);
            return;
        }

        Interlocked.Exchange(ref _currentQueueSize, size);

        Logger.LogDebug("Размер очереди обновлен: {QueueSize}", size);
    }

    /// <summary>
    /// Записывает время ожидания эскалации в очереди.
    /// </summary>
    /// <param name="seconds">Время ожидания в секундах.</param>
    public void RecordQueueDwellTime(double seconds)
    {
        if (seconds < 0)
        {
            Logger.LogWarning("Попытка записать отрицательное время ожидания в очереди: {Seconds}", seconds);
            return;
        }

        SafeRecord(_queueDepthHistogram, seconds);

        Logger.LogDebug("Записано время ожидания в очереди: {Seconds:F2}s", seconds);
    }

    // ===== APPROVAL METRIC RECORDING METHODS =====

    /// <summary>
    /// Записывает событие инициирования запроса на утверждение.
    /// </summary>
    /// <param name="approvalId">Идентификатор запроса на утверждение.</param>
    /// <param name="agentId">Идентификатор агента, запросившего утверждение.</param>
    public void RecordApprovalInitiated(string approvalId, string agentId)
    {
        if (string.IsNullOrWhiteSpace(approvalId) || string.IsNullOrWhiteSpace(agentId))
        {
            Logger.LogWarning("Попытка записать initiated событие с пустым approvalId или agentId");
            return;
        }

        SafeAdd(_approvalInitiatedCounter, 1,
            new KeyValuePair<string, object?>("approval_id", approvalId),
            new KeyValuePair<string, object?>("agent_id", agentId));

        Logger.LogDebug(
            "Записано событие инициирования утверждения {ApprovalId} от агента {AgentId}",
            approvalId,
            agentId);
    }

    /// <summary>
    /// Записывает событие принятия утверждения.
    /// </summary>
    /// <param name="approvalId">Идентификатор утверждения.</param>
    /// <param name="responseTimeSeconds">Время ответа в секундах.</param>
    public void RecordApprovalAccepted(string approvalId, double responseTimeSeconds)
    {
        if (string.IsNullOrWhiteSpace(approvalId))
        {
            Logger.LogWarning("Попытка записать accepted событие с пустым approvalId");
            return;
        }

        if (responseTimeSeconds < 0)
        {
            Logger.LogWarning("Попытка записать отрицательное время ответа: {ResponseTime}", responseTimeSeconds);
            return;
        }

        SafeAdd(_approvalAcceptedCounter, 1,
            new KeyValuePair<string, object?>("approval_id", approvalId));

        SafeRecord(_approvalResponseTimeHistogram, responseTimeSeconds,
            new KeyValuePair<string, object?>("status", "accepted"));

        Logger.LogDebug(
            "Записано событие принятия утверждения {ApprovalId} за {ResponseTime:F2}s",
            approvalId,
            responseTimeSeconds);
    }

    /// <summary>
    /// Записывает событие отклонения утверждения.
    /// </summary>
    /// <param name="approvalId">Идентификатор утверждения.</param>
    /// <param name="responseTimeSeconds">Время ответа в секундах.</param>
    public void RecordApprovalRejected(string approvalId, double responseTimeSeconds)
    {
        if (string.IsNullOrWhiteSpace(approvalId))
        {
            Logger.LogWarning("Попытка записать rejected событие с пустым approvalId");
            return;
        }

        if (responseTimeSeconds < 0)
        {
            Logger.LogWarning("Попытка записать отрицательное время ответа: {ResponseTime}", responseTimeSeconds);
            return;
        }

        SafeAdd(_approvalRejectedCounter, 1,
            new KeyValuePair<string, object?>("approval_id", approvalId));

        SafeRecord(_approvalResponseTimeHistogram, responseTimeSeconds,
            new KeyValuePair<string, object?>("status", "rejected"));

        Logger.LogDebug(
            "Записано событие отклонения утверждения {ApprovalId} за {ResponseTime:F2}s",
            approvalId,
            responseTimeSeconds);
    }

    /// <summary>
    /// Записывает событие истечения времени утверждения.
    /// </summary>
    /// <param name="approvalId">Идентификатор утверждения.</param>
    public void RecordApprovalTimeout(string approvalId)
    {
        if (string.IsNullOrWhiteSpace(approvalId))
        {
            Logger.LogWarning("Попытка записать timeout событие с пустым approvalId");
            return;
        }

        SafeAdd(_approvalTimeoutCounter, 1,
            new KeyValuePair<string, object?>("approval_id", approvalId));

        Logger.LogDebug("Записано событие таймаута утверждения {ApprovalId}", approvalId);
    }

    // ===== TELEGRAM API METRIC RECORDING METHODS =====

    /// <summary>
    /// Записывает событие успешной отправки сообщения через Telegram.
    /// </summary>
    /// <param name="agentId">Идентификатор агента, которому было отправлено сообщение.</param>
    /// <param name="durationMs">Длительность вызова API в миллисекундах.</param>
    public void RecordTelegramMessageSent(string agentId, double durationMs)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            Logger.LogWarning("Попытка записать Telegram sent событие с пустым agentId");
            return;
        }

        if (durationMs < 0)
        {
            Logger.LogWarning("Попытка записать отрицательную длительность вызова API: {Duration}", durationMs);
            return;
        }

        SafeAdd(_telegramMessagesSentCounter, 1,
            new KeyValuePair<string, object?>("agent_id", agentId));

        // Convert milliseconds to seconds for histogram
        SafeRecord(_telegramApiDurationHistogram, durationMs / 1000.0,
            new KeyValuePair<string, object?>("agent_id", agentId),
            new KeyValuePair<string, object?>("status", "success"));

        Logger.LogDebug(
            "Записано успешное отправление Telegram сообщения агенту {AgentId} за {Duration:F2}ms",
            agentId,
            durationMs);
    }

    /// <summary>
    /// Записывает событие неудачной отправки сообщения через Telegram.
    /// </summary>
    /// <param name="agentId">Идентификатор агента.</param>
    /// <param name="errorCode">Код ошибки API (если доступен).</param>
    public void RecordTelegramMessageFailed(string agentId, int? errorCode)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            Logger.LogWarning("Попытка записать Telegram failed событие с пустым agentId");
            return;
        }

        var tags = new[]
        {
            new KeyValuePair<string, object?>("agent_id", agentId),
            new KeyValuePair<string, object?>("error_code", errorCode?.ToString() ?? "unknown")
        };

        SafeAdd(_telegramMessagesFailedCounter, 1, tags);

        // Update last error code if provided
        if (errorCode.HasValue)
        {
            Interlocked.Exchange(ref _lastTelegramErrorCode, errorCode.Value);
        }

        Logger.LogDebug(
            "Записана неудачная отправка Telegram сообщения агенту {AgentId} с кодом ошибки {ErrorCode}",
            agentId,
            errorCode?.ToString() ?? "unknown");
    }

    /// <summary>
    /// Записывает попытку повторной отправки через Telegram API.
    /// </summary>
    /// <param name="agentId">Идентификатор агента.</param>
    /// <param name="retryCount">Номер попытки повторной отправки.</param>
    public void RecordTelegramRetryAttempt(string agentId, int retryCount)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            Logger.LogWarning("Попытка записать Telegram retry событие с пустым agentId");
            return;
        }

        if (retryCount < 0)
        {
            Logger.LogWarning("Попытка записать отрицательное количество попыток: {RetryCount}", retryCount);
            return;
        }

        SafeAdd(_telegramApiRetriesCounter, 1,
            new KeyValuePair<string, object?>("agent_id", agentId),
            new KeyValuePair<string, object?>("retry_count", retryCount));

        Logger.LogDebug(
            "Записана попытка повторной отправки #{RetryCount} для агента {AgentId}",
            retryCount,
            agentId);
    }

    // ===== SYSTEM HEALTH METRIC RECORDING METHODS =====

    /// <summary>
    /// Устанавливает статус здоровья сервиса эскалаций.
    /// </summary>
    /// <param name="isHealthy">true если сервис здоров, false в противном случае.</param>
    public void SetServiceHealth(bool isHealthy)
    {
        long healthValue = isHealthy ? 1 : 0;
        Interlocked.Exchange(ref _isServiceHealthy, healthValue);

        Logger.LogInformation(
            "Статус здоровья сервиса эскалаций обновлен: {HealthStatus}",
            isHealthy ? "Здоров" : "Нездоров");
    }

    /// <summary>
    /// Записывает событие ошибки в сервисе эскалаций.
    /// </summary>
    /// <param name="errorType">Тип ошибки для классификации.</param>
    public void RecordServiceError(string errorType)
    {
        if (string.IsNullOrWhiteSpace(errorType))
        {
            Logger.LogWarning("Попытка записать ошибку с пустым типом");
            return;
        }

        SafeAdd(_serviceErrorsCounter, 1,
            new KeyValuePair<string, object?>("error_type", errorType));

        Logger.LogDebug("Записана ошибка сервиса типа {ErrorType}", errorType);
    }
}
