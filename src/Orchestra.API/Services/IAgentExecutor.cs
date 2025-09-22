namespace Orchestra.API.Services;

/// <summary>
/// Interface for agent execution services
/// </summary>
public interface IAgentExecutor
{
    /// <summary>
    /// Executes a command using the agent and returns the response
    /// </summary>
    /// <param name="command">Command to execute</param>
    /// <param name="workingDirectory">Working directory for the agent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent response</returns>
    Task<AgentExecutionResponse> ExecuteCommandAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the agent type/name for logging and identification
    /// </summary>
    string AgentType { get; }
}

/// <summary>
/// Response from agent execution
/// </summary>
public class AgentExecutionResponse
{
    /// <summary>
    /// Whether the execution was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Output from the agent
    /// </summary>
    public string Output { get; set; } = "";

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken for execution
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Additional metadata from the agent
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}