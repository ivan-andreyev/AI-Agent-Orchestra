using Microsoft.AspNetCore.SignalR;
using Orchestra.Core;
using System.ComponentModel.DataAnnotations;

namespace Orchestra.API.Services;

/// <summary>
/// SignalR hub for real-time bidirectional communication between users and AI agents.
/// Enables interactive command sending, intermediate result streaming, and manual intervention capabilities.
/// </summary>
public class AgentCommunicationHub : Hub
{
    private readonly SimpleOrchestrator _orchestrator;
    private readonly ILogger<AgentCommunicationHub> _logger;

    public AgentCommunicationHub(SimpleOrchestrator orchestrator, ILogger<AgentCommunicationHub> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Adds the current connection to a specific agent group for targeted messaging
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent to join</param>
    public async Task JoinAgentGroup(string agentId)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(agentId))
            {
                await Clients.Caller.SendAsync("Error", "Invalid agent ID provided");
                return;
            }

            // Add connection to agent group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"agent_{agentId}");

            // Notify group members of new user
            await Clients.Group($"agent_{agentId}").SendAsync("UserJoined", new
            {
                ConnectionId = Context.ConnectionId,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Connection {ConnectionId} joined agent group {AgentId}",
                Context.ConnectionId, agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining agent group {AgentId} for connection {ConnectionId}",
                agentId, Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", "Failed to join agent group");
        }
    }

    /// <summary>
    /// Removes the current connection from a specific agent group
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent group to leave</param>
    public async Task LeaveAgentGroup(string agentId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                await Clients.Caller.SendAsync("Error", "Invalid agent ID provided");
                return;
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"agent_{agentId}");

            // Notify group members of user leaving
            await Clients.Group($"agent_{agentId}").SendAsync("UserLeft", new
            {
                ConnectionId = Context.ConnectionId,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Connection {ConnectionId} left agent group {AgentId}",
                Context.ConnectionId, agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving agent group {AgentId} for connection {ConnectionId}",
                agentId, Context.ConnectionId);
        }
    }

    /// <summary>
    /// Sends a command from user to a specific agent for execution
    /// </summary>
    /// <param name="agentId">The target agent identifier</param>
    /// <param name="command">The command content to execute</param>
    /// <param name="sessionId">The session identifier for tracking</param>
    public async Task SendCommandToAgent(string agentId, string command, string sessionId)
    {
        try
        {
            // Validate inputs
            var validationResult = ValidateCommandInput(agentId, command, sessionId);
            if (!validationResult.IsValid)
            {
                await Clients.Caller.SendAsync("CommandValidationError", validationResult.ErrorMessage);
                return;
            }

            // Create command request
            var taskRequest = new AgentCommandRequest
            {
                Id = Guid.NewGuid().ToString(),
                AgentId = agentId,
                Command = command,
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                UserId = Context.UserIdentifier ?? Context.ConnectionId
            };

            // Queue command for execution through orchestrator
            // Note: This is a basic implementation - full orchestrator integration would be added in future phases
            _logger.LogInformation("Command received for agent {AgentId}: {Command}", agentId, command);

            // Broadcast command received to agent group
            await Clients.Group($"agent_{agentId}").SendAsync("CommandReceived", new
            {
                taskRequest.Id,
                taskRequest.AgentId,
                Command = command.Length > 100 ? command.Substring(0, 100) + "..." : command,
                taskRequest.SessionId,
                taskRequest.Timestamp,
                taskRequest.UserId
            });

            // Send confirmation to caller
            await Clients.Caller.SendAsync("CommandSent", new
            {
                RequestId = taskRequest.Id,
                Status = "Queued",
                Timestamp = taskRequest.Timestamp
            });

            _logger.LogInformation("Command {RequestId} queued for agent {AgentId} from user {UserId}",
                taskRequest.Id, agentId, taskRequest.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command for agent {AgentId}", agentId);
            await Clients.Caller.SendAsync("Error", "Failed to process command");
        }
    }

    /// <summary>
    /// Sends a response to an intervention request from an agent
    /// </summary>
    /// <param name="agentId">The agent requesting intervention</param>
    /// <param name="responseData">The user's response data</param>
    public async Task SendInterventionResponse(string agentId, string responseData)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(responseData))
            {
                await Clients.Caller.SendAsync("Error", "Invalid intervention response data");
                return;
            }

            // Broadcast intervention response to agent group
            await Clients.Group($"agent_{agentId}").SendAsync("InterventionResponse", new
            {
                AgentId = agentId,
                ResponseData = responseData,
                UserId = Context.UserIdentifier ?? Context.ConnectionId,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Intervention response sent to agent {AgentId} from user {UserId}",
                agentId, Context.UserIdentifier ?? Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending intervention response to agent {AgentId}", agentId);
            await Clients.Caller.SendAsync("Error", "Failed to send intervention response");
        }
    }

    /// <summary>
    /// Broadcasts agent status updates to all connected clients in the agent's group
    /// </summary>
    /// <param name="agentId">The agent whose status changed</param>
    /// <param name="status">The new status</param>
    /// <param name="message">Optional status message</param>
    public async Task BroadcastAgentStatus(string agentId, string status, string? message = null)
    {
        try
        {
            await Clients.Group($"agent_{agentId}").SendAsync("AgentStatusChanged", new
            {
                AgentId = agentId,
                Status = status,
                Message = message,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogDebug("Agent status broadcast for {AgentId}: {Status}", agentId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting agent status for {AgentId}", agentId);
        }
    }

    /// <summary>
    /// Handles client disconnection and cleanup
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            _logger.LogInformation("Client {ConnectionId} disconnected. Reason: {Exception}",
                Context.ConnectionId, exception?.Message ?? "Normal disconnect");

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during client disconnect cleanup for {ConnectionId}", Context.ConnectionId);
        }
    }

    /// <summary>
    /// Validates command input parameters
    /// </summary>
    private static CommandValidationResult ValidateCommandInput(string agentId, string command, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            return new CommandValidationResult(false, "Agent ID is required");

        if (string.IsNullOrWhiteSpace(command))
            return new CommandValidationResult(false, "Command content is required");

        if (command.Length > 2000)
            return new CommandValidationResult(false, "Command exceeds maximum length of 2000 characters");

        if (string.IsNullOrWhiteSpace(sessionId))
            return new CommandValidationResult(false, "Session ID is required");

        // Basic validation - more sophisticated security checks would be added in production
        return new CommandValidationResult(true, null);
    }
}

/// <summary>
/// Represents a command request from user to agent
/// </summary>
public class AgentCommandRequest
{
    public string Id { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// Result of command validation
/// </summary>
public class CommandValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    public CommandValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }
}