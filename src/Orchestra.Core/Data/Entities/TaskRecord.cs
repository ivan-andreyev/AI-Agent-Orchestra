using System.ComponentModel.DataAnnotations;
using Orchestra.Core.Models;
using TaskStatus = Orchestra.Core.Models.TaskStatus;
using TaskPriority = Orchestra.Core.Models.TaskPriority;

namespace Orchestra.Core.Data.Entities;

public class TaskRecord : ITimestamped
{
    [Key]
    [MaxLength(128)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Command { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? AgentId { get; set; }

    [Required]
    [MaxLength(500)]
    public string RepositoryPath { get; set; } = string.Empty;

    public TaskPriority Priority { get; set; }

    public TaskStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public TimeSpan? ExecutionDuration { get; set; }

    public string? Result { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    [MaxLength(128)]
    public string? CorrelationId { get; set; }

    // Workflow Support
    [MaxLength(128)]
    public string? WorkflowId { get; set; }

    [MaxLength(128)]
    public string? ParentTaskId { get; set; }

    public int WorkflowStep { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation Properties
    public Agent? Agent { get; set; }

    public Repository? Repository { get; set; }

    [MaxLength(128)]
    public string? RepositoryId { get; set; }

    public WorkflowDefinition? Workflow { get; set; }

    public TaskRecord? ParentTask { get; set; }

    public ICollection<TaskRecord> ChildTasks { get; set; } = new List<TaskRecord>();
}

