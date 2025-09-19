using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Data.Entities;

public class Repository : ITimestamped
{
    [Key]
    [MaxLength(128)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Path { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public RepositoryType Type { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastAccessedAt { get; set; }

    // Configuration
    public string? SettingsJson { get; set; }

    [MaxLength(100)]
    public string? DefaultBranch { get; set; }

    public string AllowedOperationsJson { get; set; } = "[]";

    // Statistics
    public int TotalTasks { get; set; }

    public int SuccessfulTasks { get; set; }

    public int FailedTasks { get; set; }

    public TimeSpan TotalExecutionTime { get; set; }

    // Navigation Properties
    public ICollection<Agent> Agents { get; set; } = new List<Agent>();

    public ICollection<TaskRecord> Tasks { get; set; } = new List<TaskRecord>();

    public ICollection<UserPreference> UserPreferences { get; set; } = new List<UserPreference>();

    // Helper property for AllowedOperations
    public List<string> AllowedOperations
    {
        get
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(AllowedOperationsJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
        set
        {
            AllowedOperationsJson = System.Text.Json.JsonSerializer.Serialize(value);
        }
    }
}

public enum RepositoryType
{
    Git = 0,
    Svn = 1,
    Local = 2,
    Remote = 3
}