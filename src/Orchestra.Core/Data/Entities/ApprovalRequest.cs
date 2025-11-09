using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Data.Entities;

/// <summary>
/// Представляет запрос на одобрение разрешений от человека
/// </summary>
public class ApprovalRequest : ITimestamped
{
    /// <summary>
    /// Уникальный идентификатор запроса на одобрение
    /// </summary>
    [Key]
    [MaxLength(128)]
    public string ApprovalId { get; set; } = string.Empty;

    /// <summary>
    /// ID сессии, к которой относится запрос
    /// </summary>
    [MaxLength(128)]
    [Required]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// ID агента, создавшего запрос
    /// </summary>
    [MaxLength(128)]
    [Required]
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Текущий статус запроса на одобрение
    /// </summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// Время создания запроса (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Время последнего обновления запроса (UTC)
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Время истечения срока действия запроса (UTC)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Время одобрения запроса (UTC)
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Идентификатор пользователя, который одобрил запрос
    /// </summary>
    [MaxLength(128)]
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Дополнительные детали запроса (JSON)
    /// </summary>
    [MaxLength(4000)]
    public string? RequestDetails { get; set; }

    /// <summary>
    /// Причина отмены запроса (если применимо)
    /// </summary>
    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Навигационное свойство к агенту
    /// </summary>
    public Agent? Agent { get; set; }
}

/// <summary>
/// Статус запроса на одобрение
/// </summary>
public enum ApprovalStatus
{
    /// <summary>Запрос ожидает одобрения</summary>
    Pending = 1,

    /// <summary>Запрос одобрен</summary>
    Approved = 2,

    /// <summary>Запрос отклонён</summary>
    Rejected = 3,

    /// <summary>Запрос отменён (по таймауту или вручную)</summary>
    Cancelled = 4
}
