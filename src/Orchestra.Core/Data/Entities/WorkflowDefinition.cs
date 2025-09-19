using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Data.Entities;

public class WorkflowDefinition : ITimestamped
{
    [Key]
    [MaxLength(128)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public string DefinitionJson { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation Properties
    public ICollection<TaskRecord> Tasks { get; set; } = new List<TaskRecord>();
}