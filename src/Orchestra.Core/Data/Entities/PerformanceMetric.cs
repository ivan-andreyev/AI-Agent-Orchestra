using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Data.Entities;

public class PerformanceMetric : ITimestamped
{
    [Key]
    [MaxLength(128)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string AgentId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string MetricName { get; set; } = string.Empty;

    public double Value { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    public DateTime MeasuredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    // Navigation Properties
    public Agent Agent { get; set; } = null!;
}