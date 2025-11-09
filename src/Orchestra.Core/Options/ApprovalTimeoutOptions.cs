using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Options;

/// <summary>
/// Конфигурационные опции для управления таймаутами approval requests
/// </summary>
/// <remarks>
/// Определяет параметры для автоматической отмены approval requests при превышении timeout,
/// а также настройки background service для мониторинга истекших approvals
/// </remarks>
public class ApprovalTimeoutOptions
{
    /// <summary>
    /// Таймаут по умолчанию для approval requests в минутах (1-1440 минут = 1 минута - 24 часа)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 30 минут
    /// После истечения этого времени approval request автоматически отменяется
    /// </remarks>
    [Range(1, 1440)]
    public int DefaultTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Максимальный таймаут для approval requests в минутах (1-1440 минут)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 120 минут (2 часа)
    /// Используется для валидации custom timeout значений
    /// </remarks>
    [Range(1, 1440)]
    public int MaxTimeoutMinutes { get; set; } = 120;

    /// <summary>
    /// Интервал проверки истекших approval requests в секундах (10-300 секунд = 10 секунд - 5 минут)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 30 секунд
    /// Определяет, как часто background service проверяет наличие expired approvals
    /// </remarks>
    [Range(10, 300)]
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Grace period в секундах перед отменой approval (0-60 секунд)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 0 секунд (нет grace period)
    /// Дополнительное время перед отменой для предотвращения race conditions
    /// </remarks>
    [Range(0, 60)]
    public int GracePeriodSeconds { get; set; } = 0;

    /// <summary>
    /// Максимальное количество одновременно обрабатываемых timeouts (1-100)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 10
    /// Ограничивает нагрузку на систему при массовой отмене approvals
    /// </remarks>
    [Range(1, 100)]
    public int MaxConcurrentTimeouts { get; set; } = 10;

    /// <summary>
    /// Включена ли отправка предупреждений о скором истечении timeout
    /// </summary>
    /// <remarks>
    /// По умолчанию: true
    /// Позволяет уведомлять оператора за N минут до автоматической отмены
    /// </remarks>
    public bool SendTimeoutWarnings { get; set; } = true;

    /// <summary>
    /// За сколько минут до истечения отправлять предупреждение (1-30 минут)
    /// </summary>
    /// <remarks>
    /// По умолчанию: 5 минут
    /// Актуально только если SendTimeoutWarnings = true
    /// </remarks>
    [Range(1, 30)]
    public int WarningMinutesBeforeTimeout { get; set; } = 5;
}
