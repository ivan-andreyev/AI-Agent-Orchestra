using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Data.Entities;

public class UserPreference : ITimestamped
{
    [Key]
    [MaxLength(128)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string UserId { get; set; } = "default"; // Support for future multi-user

    [Required]
    [MaxLength(255)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    public PreferenceType Type { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Repository-specific preferences
    [MaxLength(128)]
    public string? RepositoryId { get; set; }

    public Repository? Repository { get; set; }
}

public enum PreferenceType
{
    UI = 0,
    Behavior = 1,
    Performance = 2,
    Security = 3,
    Integration = 4
}