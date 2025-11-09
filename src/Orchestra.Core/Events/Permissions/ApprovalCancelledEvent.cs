namespace Orchestra.Core.Events.Permissions;

/// <summary>
/// Событие отмены approval request
/// </summary>
public class ApprovalCancelledEvent : IEvent
{
    /// <summary>
    /// ID отменённого approval request
    /// </summary>
    public string ApprovalId { get; }

    /// <summary>
    /// Причина отмены
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Время события
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Инициализирует новое событие отмены approval request
    /// </summary>
    /// <param name="approvalId">ID отменённого approval</param>
    /// <param name="reason">Причина отмены</param>
    public ApprovalCancelledEvent(string approvalId, string reason)
    {
        ApprovalId = approvalId ?? throw new ArgumentNullException(nameof(approvalId));
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        Timestamp = DateTime.UtcNow;
    }
}
