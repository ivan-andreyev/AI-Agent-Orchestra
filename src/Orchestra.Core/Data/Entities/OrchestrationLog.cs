using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Data.Entities;

public class OrchestrationLog : ITimestamped
{
    [Key]
    [MaxLength(128)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    public LogLevel Level { get; set; }

    [MaxLength(128)]
    public string? AgentId { get; set; }

    [MaxLength(128)]
    public string? TaskId { get; set; }

    [MaxLength(128)]
    public string? RepositoryId { get; set; }

    public string? AdditionalData { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation Properties
    public Agent? Agent { get; set; }
    public TaskRecord? Task { get; set; }
    public Repository? Repository { get; set; }
}

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}