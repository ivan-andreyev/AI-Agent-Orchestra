using System.ComponentModel.DataAnnotations;
using Orchestra.Core.Models;
using TaskPriority = Orchestra.Core.Models.TaskPriority;

namespace Orchestra.Core.Data.Entities;

public class TaskTemplate : ITimestamped
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
    [MaxLength(2000)]
    public string CommandTemplate { get; set; } = string.Empty;

    public TaskPriority DefaultPriority { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;

    public string? ParametersJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}