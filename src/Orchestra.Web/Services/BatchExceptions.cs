namespace Orchestra.Web.Services;

/// <summary>
/// Exception thrown when circular dependencies are detected in the task graph
/// </summary>
public class CircularDependencyException : Exception
{
    public CircularDependencyException(string message) : base(message) { }
    public CircularDependencyException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when repository access validation fails
/// </summary>
public class RepositoryAccessException : Exception
{
    public RepositoryAccessException(string message) : base(message) { }
    public RepositoryAccessException(string message, Exception innerException) : base(message, innerException) { }
}