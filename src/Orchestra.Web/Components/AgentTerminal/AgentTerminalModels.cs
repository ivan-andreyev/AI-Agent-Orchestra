namespace Orchestra.Web.Components.AgentTerminal
{
    /// <summary>
    /// Client-side DTOs for AgentInteractionHub communication.
    /// </summary>
    /// <remarks>
    /// These mirror the server-side DTOs in Orchestra.API.Hubs.Models
    /// to avoid circular dependencies while maintaining type safety.
    /// </remarks>

    /// <summary>
    /// Request to connect to an agent via SignalR hub.
    /// </summary>
    public record ConnectToAgentRequest(
        string AgentId,
        string ConnectorType,
        Dictionary<string, string> ConnectionParams);

    /// <summary>
    /// Response from agent connection attempt.
    /// </summary>
    public record ConnectToAgentResponse(
        string SessionId,
        bool Success,
        string? ErrorMessage);

    /// <summary>
    /// Request to send a command to connected agent.
    /// </summary>
    public record SendCommandRequest(
        string SessionId,
        string Command);

    /// <summary>
    /// Notification that a command was sent to the agent.
    /// </summary>
    public record CommandSentNotification(
        string SessionId,
        string Command,
        bool Success,
        DateTime Timestamp);

    /// <summary>
    /// Session information from agent hub.
    /// </summary>
    public record SessionInfo(
        string SessionId,
        string AgentId,
        string Status,
        string ConnectorType,
        DateTime CreatedAt,
        DateTime LastActivityAt,
        int OutputLineCount);
}
