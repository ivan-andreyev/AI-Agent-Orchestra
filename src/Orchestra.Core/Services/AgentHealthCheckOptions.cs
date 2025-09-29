namespace Orchestra.Core.Services;

/// <summary>
/// Настройки для сервиса мониторинга здоровья агентов
/// </summary>
public class AgentHealthCheckOptions
{
    /// <summary>
    /// Интервал между проверками здоровья агентов
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Таймаут для проверки отдельного агента
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Таймаут, после которого агент считается недоступным
    /// </summary>
    public TimeSpan AgentTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Таймаут для восстановления агента из Error/Offline в Idle
    /// </summary>
    public TimeSpan RecoveryTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Включить подробное логирование
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// Максимальное количество агентов для проверки за один раз
    /// </summary>
    public int MaxConcurrentChecks { get; set; } = 10;

    /// <summary>
    /// Включена ли проверка здоровья агентов
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Максимальное количество попыток проверки здоровья агента
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Задержка между попытками проверки здоровья
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}