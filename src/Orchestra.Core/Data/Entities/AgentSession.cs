using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Data.Entities;

/// <summary>
/// Представляет сессию Claude Code subprocess для агента
/// </summary>
public class AgentSession : ITimestamped
{
    /// <summary>
    /// Уникальный идентификатор записи сессии
    /// </summary>
    [Key]
    [MaxLength(128)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// ID агента, к которому привязана сессия
    /// </summary>
    [MaxLength(128)]
    [Required]
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// UUID сессии для --session-id параметра Claude Code
    /// </summary>
    [MaxLength(128)]
    [Required]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// ID процесса текущего subprocess (может быть null если процесс остановлен)
    /// </summary>
    public int? ProcessId { get; set; }

    /// <summary>
    /// Рабочая директория Claude Code subprocess
    /// </summary>
    [MaxLength(500)]
    [Required]
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Текущий статус сессии
    /// </summary>
    public SessionStatus Status { get; set; } = SessionStatus.Active;

    /// <summary>
    /// Время создания сессии (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Время последнего обновления сессии (UTC)
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Время последнего resume операции (UTC)
    /// </summary>
    public DateTime? LastResumedAt { get; set; }

    /// <summary>
    /// Время закрытия сессии (UTC)
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Общая стоимость всех операций в сессии (USD)
    /// </summary>
    public double TotalCostUsd { get; set; } = 0;

    /// <summary>
    /// Общая продолжительность работы сессии (миллисекунды)
    /// </summary>
    public long TotalDurationMs { get; set; } = 0;

    /// <summary>
    /// Количество сообщений в сессии
    /// </summary>
    public int MessageCount { get; set; } = 0;

    /// <summary>
    /// Навигационное свойство к агенту
    /// </summary>
    public Agent Agent { get; set; } = null!;
}

/// <summary>
/// Статус сессии Claude Code
/// </summary>
public enum SessionStatus
{
    /// <summary>Сессия активна и subprocess работает</summary>
    Active = 1,

    /// <summary>Сессия на паузе (subprocess остановлен, но может быть возобновлен)</summary>
    Paused = 2,

    /// <summary>Сессия закрыта (subprocess завершён)</summary>
    Closed = 3,

    /// <summary>Сессия в состоянии ошибки</summary>
    Error = 4
}
