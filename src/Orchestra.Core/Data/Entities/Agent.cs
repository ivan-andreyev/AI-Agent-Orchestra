using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Data.Entities;

/// <summary>
/// Представляет агента в системе оркестрации AI Agent Orchestra
/// </summary>
public class Agent : ITimestamped
{
    /// <summary>
    /// Уникальный идентификатор агента
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Название агента
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Тип агента
    /// </summary>
    [Required]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Путь к репозиторию, с которым работает агент
    /// </summary>
    [Required]
    public string RepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Текущий статус агента
    /// </summary>
    public AgentStatus Status { get; set; }

    /// <summary>
    /// Время последней активности агента
    /// </summary>
    public DateTime LastPing { get; set; }

    [MaxLength(128)]
    public string? CurrentTask { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    // Configuration
    public string? ConfigurationJson { get; set; }

    public int MaxConcurrentTasks { get; set; } = 1;

    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    // Performance Metrics
    public int TotalTasksCompleted { get; set; }

    public int TotalTasksFailed { get; set; }

    public TimeSpan TotalExecutionTime { get; set; }

    public double AverageExecutionTime { get; set; }

    // Session Information
    [MaxLength(128)]
    public string? SessionId { get; set; }

    // Navigation Properties
    public Repository? Repository { get; set; }

    [MaxLength(128)]
    public string? RepositoryId { get; set; }

    public ICollection<TaskRecord> AssignedTasks { get; set; } = new List<TaskRecord>();

    public ICollection<PerformanceMetric> PerformanceMetrics { get; set; } = new List<PerformanceMetric>();
}

/// <summary>
/// Статус агента в системе
/// </summary>
public enum AgentStatus
{
    /// <summary>Неизвестный статус</summary>
    Unknown = 0,
    /// <summary>Агент свободен и готов к работе</summary>
    Idle = 1,
    /// <summary>Агент выполняет задачу</summary>
    Busy = 2,
    /// <summary>Агент отключен</summary>
    Offline = 3,
    /// <summary>Агент в состоянии ошибки</summary>
    Error = 4
}