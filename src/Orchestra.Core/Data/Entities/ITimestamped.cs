namespace Orchestra.Core.Data.Entities;

public interface ITimestamped
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}

public interface IIdentifiable
{
    string Id { get; set; }
}