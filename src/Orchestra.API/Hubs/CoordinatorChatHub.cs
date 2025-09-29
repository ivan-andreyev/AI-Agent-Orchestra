using Microsoft.AspNetCore.SignalR;
using Orchestra.Core;
using Orchestra.Core.Models;
using Orchestra.Core.Models.Chat;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Services;
using Orchestra.API.Services;
using System.Text;
using TaskPriority = Orchestra.Core.Models.TaskPriority;
using TaskRequest = Orchestra.Core.Models.TaskRequest;

namespace Orchestra.API.Hubs;

/// <summary>
/// SignalR hub for interactive chat with the coordinator agent.
/// Provides command-line style interface for managing the orchestration system.
/// </summary>
public class CoordinatorChatHub : Hub
{
    private readonly SimpleOrchestrator _orchestrator;
    private readonly HangfireOrchestrator _hangfireOrchestrator;
    private readonly IChatContextService _chatContextService;
    private readonly IConnectionSessionService _sessionService;
    private readonly IConfiguration _configuration;
    private readonly IRepositoryPathService _repositoryPathService;
    private readonly ILogger<CoordinatorChatHub> _logger;

    public CoordinatorChatHub(
        SimpleOrchestrator orchestrator,
        HangfireOrchestrator hangfireOrchestrator,
        IChatContextService chatContextService,
        IConnectionSessionService sessionService,
        IConfiguration configuration,
        IRepositoryPathService repositoryPathService,
        ILogger<CoordinatorChatHub> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _hangfireOrchestrator = hangfireOrchestrator ?? throw new ArgumentNullException(nameof(hangfireOrchestrator));
        _chatContextService = chatContextService ?? throw new ArgumentNullException(nameof(chatContextService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _repositoryPathService = repositoryPathService ?? throw new ArgumentNullException(nameof(repositoryPathService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles commands sent to the coordinator
    /// </summary>
    /// <param name="command">The command to execute</param>
    public async Task SendCommand(string command)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                await SendResponse("❌ Empty command. Type 'help' for available commands.", "error");
                return;
            }

            _logger.LogInformation("Coordinator command received: {Command} from {ConnectionId}",
                command, Context.ConnectionId);

            // Save user command to database
            await SaveUserMessage(command.Trim());

            var response = await ProcessCommand(command.Trim());
            await SendResponse(response.Message, response.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing coordinator command: {Command}", command);
            await SendResponse($"❌ Error processing command: {ex.Message}", "error");
        }
    }

    /// <summary>
    /// Processes a coordinator command using Claude Code agent
    /// </summary>
    private async Task<CommandResponse> ProcessCommand(string command)
    {
        try
        {
            // Check if it's a built-in command first
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var mainCommand = parts[0].ToLowerInvariant();

            // Handle built-in system commands
            if (mainCommand == "help")
            {
                return GetHelpResponse();
            }

            // For all other commands, delegate to Claude Code agent
            _logger.LogInformation("Delegating command to Claude Code agent: {Command}", command);

            // Queue the command as a task for the Claude agent using Hangfire
            // Get repository path using the repository path service
            var repositoryPath = _repositoryPathService.GetRepositoryPath();
            _logger.LogInformation("Using repository path: {RepositoryPath}", repositoryPath);
            var jobId = await _hangfireOrchestrator.QueueTaskAsync(command, repositoryPath, TaskPriority.High, Context.ConnectionId);

            _logger.LogInformation("Task queued via Hangfire - JobId: {JobId}", jobId);

            return new CommandResponse($"🤖 **Command sent to Claude Code agent:**\n" +
                                     $"Command: {command}\n" +
                                     $"Status: Queued for processing via Hangfire\n" +
                                     $"Job ID: {jobId}\n\n" +
                                     $"The agent will process your request and execute the command.", "success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command: {Command}", command);
            return new CommandResponse($"❌ Error processing command: {ex.Message}", "error");
        }
    }

    /// <summary>
    /// Returns help information for available commands
    /// </summary>
    private static CommandResponse GetHelpResponse()
    {
        var help = new StringBuilder();
        help.AppendLine("🤖 **Claude Code Agent Coordinator**");
        help.AppendLine();
        help.AppendLine("**How it works:**");
        help.AppendLine("• Any command you send will be processed by Claude Code agent");
        help.AppendLine("• The agent can execute code, analyze files, run tests, and more");
        help.AppendLine("• Commands are queued and processed asynchronously");
        help.AppendLine();
        help.AppendLine("**Example commands:**");
        help.AppendLine("• `Test the application` - Run tests");
        help.AppendLine("• `Fix compilation errors` - Analyze and fix build issues");
        help.AppendLine("• `Add logging to the authentication service` - Code modifications");
        help.AppendLine("• `Review the latest changes` - Code analysis");
        help.AppendLine("• `Create a new controller for users` - Generate new code");
        help.AppendLine();
        help.AppendLine("**Built-in commands:**");
        help.AppendLine("• `help` - Show this help message");
        help.AppendLine();
        help.AppendLine("**Pro tip:** Write commands in natural language - Claude Code understands context!");

        return new CommandResponse(help.ToString(), "info");
    }

    /// <summary>
    /// Returns system status information
    /// </summary>
    private CommandResponse GetStatusResponse()
    {
        var allAgents = _orchestrator.GetAllAgents();
        var state = _orchestrator.GetCurrentState();

        var status = new StringBuilder();
        status.AppendLine("📊 **System Status:**");
        status.AppendLine();
        status.AppendLine($"**Agents:** {allAgents.Count} total");
        status.AppendLine($"• 🟢 Idle: {allAgents.Count(a => a.Status == AgentStatus.Idle)}");
        status.AppendLine($"• 🔄 Busy: {allAgents.Count(a => a.Status == AgentStatus.Busy)}");
        status.AppendLine($"• ⚠️ Error: {allAgents.Count(a => a.Status == AgentStatus.Error)}");
        status.AppendLine($"• 🔴 Offline: {allAgents.Count(a => a.Status == AgentStatus.Offline)}");
        status.AppendLine();
        status.AppendLine($"**Tasks:** {state.TaskQueue?.Count ?? 0} in queue");

        if (state.TaskQueue?.Any() == true)
        {
            var tasksByPriority = state.TaskQueue.GroupBy(t => t.Priority).OrderBy(g => g.Key);
            foreach (var group in tasksByPriority)
            {
                var icon = group.Key switch
                {
                    TaskPriority.Critical => "🚨",
                    TaskPriority.High => "🔴",
                    TaskPriority.Normal => "🟡",
                    TaskPriority.Low => "🟢",
                    _ => "❓"
                };
                status.AppendLine($"• {icon} {group.Key}: {group.Count()}");
            }
        }

        return new CommandResponse(status.ToString(), "success");
    }

    /// <summary>
    /// Returns agents information based on filter
    /// </summary>
    private CommandResponse GetAgentsResponse(string[] args)
    {
        var allAgents = _orchestrator.GetAllAgents();
        var filter = args.Length > 0 ? args[0].ToLowerInvariant() : "all";

        var filteredAgents = filter switch
        {
            "idle" => allAgents.Where(a => a.Status == AgentStatus.Idle),
            "working" => allAgents.Where(a => a.Status == AgentStatus.Busy),
            "offline" => allAgents.Where(a => a.Status == AgentStatus.Offline),
            "error" => allAgents.Where(a => a.Status == AgentStatus.Error),
            "all" => allAgents,
            _ => allAgents.Where(a => a.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                     a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        };

        var agents = filteredAgents.Take(20).ToList(); // Limit to 20 for readability
        var totalCount = filteredAgents.Count();

        var response = new StringBuilder();
        response.AppendLine($"🤖 **Agents ({filter}):** {totalCount} found");
        response.AppendLine();

        if (!agents.Any())
        {
            response.AppendLine("No agents found matching the filter.");
            return new CommandResponse(response.ToString(), "info");
        }

        foreach (var agent in agents)
        {
            var statusIcon = agent.Status switch
            {
                AgentStatus.Idle => "🟢",
                AgentStatus.Busy => "🔄",
                AgentStatus.Error => "⚠️",
                AgentStatus.Offline => "🔴",
                _ => "❓"
            };

            var shortId = agent.Id.Length > 8 ? agent.Id[..8] : agent.Id;
            var shortRepo = Path.GetFileName(agent.RepositoryPath?.TrimEnd(Path.DirectorySeparatorChar) ?? "Unknown");
            var lastPing = agent.LastPing != null ?
                $"{(DateTime.Now - agent.LastPing).TotalMinutes:F0}m ago" : "never";

            response.AppendLine($"{statusIcon} `{shortId}` **{shortRepo}** (ping: {lastPing})");

            if (!string.IsNullOrEmpty(agent.CurrentTask))
            {
                var shortTask = agent.CurrentTask.Length > 50 ?
                    agent.CurrentTask[..50] + "..." : agent.CurrentTask;
                response.AppendLine($"   Current: {shortTask}");
            }
        }

        if (totalCount > 20)
        {
            response.AppendLine();
            response.AppendLine($"... and {totalCount - 20} more agents");
        }

        return new CommandResponse(response.ToString(), "success");
    }

    /// <summary>
    /// Queues a task for execution with specified repository and command
    /// </summary>
    /// <param name="args">Array containing repository path and command arguments</param>
    /// <returns>Command response indicating success or failure of queuing operation</returns>
    private async Task<CommandResponse> QueueTaskCommand(string[] args)
    {
        if (args.Length < 2)
        {
            return new CommandResponse("❌ Usage: queue <repository_path> <command>", "error");
        }

        var repositoryPath = args[0].Trim('"');
        var command = string.Join(" ", args.Skip(1)).Trim('"');

        try
        {
            _orchestrator.QueueTask(command, repositoryPath, TaskPriority.Normal);

            return new CommandResponse($"✅ **Task queued successfully**\n" +
                                     $"Repository: {Path.GetFileName(repositoryPath)}\n" +
                                     $"Command: {command}\n" +
                                     $"Priority: Normal", "success");
        }
        catch (Exception ex)
        {
            return new CommandResponse($"❌ Failed to queue task: {ex.Message}", "error");
        }
    }

    /// <summary>
    /// Shows task history
    /// </summary>
    private CommandResponse GetHistoryResponse(string[] args)
    {
        var count = 10; // Default count
        if (args.Length > 0 && int.TryParse(args[0], out var parsedCount))
        {
            count = Math.Min(parsedCount, 50); // Max 50 entries
        }

        var state = _orchestrator.GetCurrentState();
        var tasks = state.TaskQueue?.OrderByDescending(t => t.CreatedAt).Take(count).ToList() ?? new List<TaskRequest>();

        var response = new StringBuilder();
        response.AppendLine($"📋 **Recent Tasks:** {tasks.Count} shown");
        response.AppendLine();

        if (!tasks.Any())
        {
            response.AppendLine("No tasks in history.");
            return new CommandResponse(response.ToString(), "info");
        }

        foreach (var task in tasks)
        {
            var statusIcon = task.Status switch
            {
                Orchestra.Core.Models.TaskStatus.Pending => "⏳",
                Orchestra.Core.Models.TaskStatus.Assigned => "📤",
                Orchestra.Core.Models.TaskStatus.InProgress => "🔄",
                Orchestra.Core.Models.TaskStatus.Completed => "✅",
                Orchestra.Core.Models.TaskStatus.Failed => "❌",
                Orchestra.Core.Models.TaskStatus.Cancelled => "🚫",
                _ => "❓"
            };

            var shortRepo = Path.GetFileName(task.RepositoryPath?.TrimEnd(Path.DirectorySeparatorChar) ?? "Unknown");
            var shortCommand = task.Command.Length > 40 ? task.Command[..40] + "..." : task.Command;
            var timeAgo = $"{(DateTime.Now - task.CreatedAt).TotalMinutes:F0}m ago";

            response.AppendLine($"{statusIcon} **{shortRepo}** `{shortCommand}` ({timeAgo})");

            if (!string.IsNullOrEmpty(task.AgentId))
            {
                var shortAgentId = task.AgentId.Length > 8 ? task.AgentId[..8] : task.AgentId;
                response.AppendLine($"   Agent: {shortAgentId}");
            }
        }

        return new CommandResponse(response.ToString(), "success");
    }

    /// <summary>
    /// Pings a specific agent
    /// </summary>
    private async Task<CommandResponse> PingAgentCommand(string[] args)
    {
        if (args.Length == 0)
        {
            return new CommandResponse("❌ Usage: ping <agentId_or_partial>", "error");
        }

        var agentIdFilter = args[0];
        var allAgents = _orchestrator.GetAllAgents();
        var matchingAgents = allAgents.Where(a =>
            a.Id.Contains(agentIdFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!matchingAgents.Any())
        {
            return new CommandResponse($"❌ No agents found matching: {agentIdFilter}", "error");
        }

        if (matchingAgents.Count > 1)
        {
            var agentList = string.Join(", ", matchingAgents.Take(5).Select(a => a.Id[..8]));
            return new CommandResponse($"❌ Multiple agents match '{agentIdFilter}': {agentList}...", "error");
        }

        var agent = matchingAgents.First();

        // Update agent ping time (simulate ping)
        _orchestrator.UpdateAgentStatus(agent.Id, agent.Status);

        return new CommandResponse($"🏓 **Pinged agent:** {agent.Id[..8]}\n" +
                                 $"Status: {agent.Status}\n" +
                                 $"Repository: {Path.GetFileName(agent.RepositoryPath ?? "Unknown")}\n" +
                                 $"Last Ping: just now", "success");
    }

    /// <summary>
    /// Sends a response message to the client
    /// </summary>
    private async Task SendResponse(string message, string type)
    {
        try
        {
            // Save response to database
            await SaveSystemMessage(message, type);

            // Get user ID for broadcasting to all connections for this user
            var session = await _sessionService.GetSessionAsync(Context.ConnectionId);
            if (session != null)
            {
                await Clients.Group($"user_{session.UserId}").SendAsync("ReceiveResponse", new
                {
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                // Fallback to caller if session not found
                await Clients.Caller.SendAsync("ReceiveResponse", new
                {
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending response to client");

            // Still try to send to caller even if database save fails
            await Clients.Caller.SendAsync("ReceiveResponse", new
            {
                Message = message,
                Type = type,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Handles client connection
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Coordinator chat client connected: {ConnectionId}", Context.ConnectionId);

        try
        {
            // Get persistent user ID and add to SignalR group for cross-client synchronization
            var userId = GetOrCreatePersistentUserId();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            _logger.LogInformation("Added connection {ConnectionId} to user group: user_{UserId}",
                Context.ConnectionId, userId);

            // Initialize chat session for this connection
            await InitializeChatSession();

            // Load and send chat history
            await LoadAndSendChatHistory();

            await SendResponse("🤖 **Coordinator Agent Connected**\n" +
                              "Type 'help' for available commands or 'status' for system overview.", "info");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing chat session for {ConnectionId}", Context.ConnectionId);

            // Still send basic connection message if session initialization fails
            await SendResponse("🤖 **Coordinator Agent Connected** (Session initialization failed)\n" +
                              "Type 'help' for available commands or 'status' for system overview.", "info");
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Handles client disconnection
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Coordinator chat client disconnected: {ConnectionId}. Reason: {Exception}",
            Context.ConnectionId, exception?.Message ?? "Normal disconnect");

        // Remove from SignalR group (note: SignalR automatically removes on disconnect, but explicit is better)
        try
        {
            var session = await _sessionService.GetSessionAsync(Context.ConnectionId);
            if (session != null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{session.UserId}");
                _logger.LogInformation("Removed connection {ConnectionId} from user group: user_{UserId}",
                    Context.ConnectionId, session.UserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing connection {ConnectionId} from SignalR group", Context.ConnectionId);
        }

        // Clean up session data for this connection
        try
        {
            await _sessionService.RemoveSessionAsync(Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up session data for disconnected connection {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    #region Chat Context Management Methods

    /// <summary>
    /// Initializes chat session for the current connection
    /// </summary>
    private async Task InitializeChatSession()
    {
        try
        {
            // Use persistent user ID based on cookies to maintain session across page reloads
            var userId = GetOrCreatePersistentUserId();
            var instanceId = "coordinator-main"; // Static instance identifier for now

            var session = await _chatContextService.GetOrCreateSessionAsync(userId, instanceId);
            await _sessionService.SetSessionAsync(Context.ConnectionId, session);

            _logger.LogInformation("Chat session initialized for connection {ConnectionId}, session {SessionId}, user {UserId}",
                Context.ConnectionId, session.Id, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize chat session for connection {ConnectionId}", Context.ConnectionId);
            throw;
        }
    }

    /// <summary>
    /// Gets or creates a persistent user ID using query string parameter for initial setup
    /// </summary>
    private string GetOrCreatePersistentUserId()
    {
        try
        {
            // Try to get user ID from query string (sent by client)
            var httpContext = Context.GetHttpContext();
            var query = httpContext?.Request.Query;

            if (query != null && query.TryGetValue("userId", out var userIdParam)
                && !string.IsNullOrWhiteSpace(userIdParam))
            {
                var userId = userIdParam.ToString();
                _logger.LogDebug("Using user ID from query parameter: {UserId}", userId);
                return userId;
            }

            // Try to get existing user ID from cookie (if available)
            if (httpContext?.Request.Cookies.TryGetValue("orchestrator_user_id", out var existingUserId) == true
                && !string.IsNullOrWhiteSpace(existingUserId))
            {
                _logger.LogDebug("Found existing user ID in cookie: {UserId}", existingUserId);
                return existingUserId;
            }

            // Fallback: use a deterministic ID based on IP + UserAgent for basic persistence
            var userAgent = httpContext?.Request.Headers.UserAgent.ToString() ?? "unknown";
            var remoteIp = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var fallbackId = $"user_{(userAgent + remoteIp).GetHashCode():x8}";

            _logger.LogInformation("Created fallback persistent user ID: {UserId}", fallbackId);
            return fallbackId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get/create persistent user ID, falling back to connection ID");
            // Ultimate fallback to connection ID
            return Context.ConnectionId;
        }
    }

    /// <summary>
    /// Loads and sends chat history to the connected client
    /// </summary>
    private async Task LoadAndSendChatHistory()
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(Context.ConnectionId);
            if (session == null)
            {
                _logger.LogWarning("No session found for connection {ConnectionId} when loading history", Context.ConnectionId);
                return;
            }

            var history = await _chatContextService.GetSessionHistoryAsync(session.Id, limit: 50);

            if (history.Any())
            {
                _logger.LogInformation("Loading {Count} messages from chat history for session {SessionId}",
                    history.Count, session.Id);

                // Send each historical message to client
                foreach (var message in history)
                {
                    var messageType = message.MessageType switch
                    {
                        MessageType.User => "user",
                        MessageType.System => "system",
                        MessageType.Agent => "agent",
                        _ => "info"
                    };

                    await Clients.Caller.SendAsync("ReceiveHistoryMessage", new
                    {
                        Message = message.Content,
                        Type = messageType,
                        Author = message.Author,
                        Timestamp = message.CreatedAt
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chat history for connection {ConnectionId}", Context.ConnectionId);
            // Don't rethrow - history loading failure shouldn't prevent connection
        }
    }

    /// <summary>
    /// Saves user message to database
    /// </summary>
    /// <param name="content">The message content to save</param>
    /// <returns>Task representing the asynchronous operation</returns>
    private async Task SaveUserMessage(string content)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(Context.ConnectionId);
            if (session == null)
            {
                _logger.LogWarning("No session found for connection {ConnectionId} when saving user message", Context.ConnectionId);
                return;
            }

            await _chatContextService.SaveMessageAsync(
                session.Id,
                "User",
                content,
                MessageType.User,
                metadata: $"{{\"connectionId\":\"{Context.ConnectionId}\"}}"
            );

            _logger.LogDebug("User message saved for session {SessionId}: {Content}",
                session.Id, content.Length > 50 ? content[..50] + "..." : content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save user message for connection {ConnectionId}", Context.ConnectionId);
            // Don't rethrow - message saving failure shouldn't prevent command processing
        }
    }

    /// <summary>
    /// Saves system/agent response to database
    /// </summary>
    /// <param name="content">The message content to save</param>
    /// <param name="messageType">Type of message (error, info, success)</param>
    /// <returns>Task representing the asynchronous operation</returns>
    private async Task SaveSystemMessage(string content, string messageType)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(Context.ConnectionId);
            if (session == null)
            {
                _logger.LogWarning("No session found for connection {ConnectionId} when saving system message", Context.ConnectionId);
                return;
            }

            var author = messageType switch
            {
                "error" => "System",
                "info" => "System",
                "success" => "Agent",
                _ => "System"
            };

            var dbMessageType = messageType switch
            {
                "success" => MessageType.Agent,
                _ => MessageType.System
            };

            await _chatContextService.SaveMessageAsync(
                session.Id,
                author,
                content,
                dbMessageType,
                metadata: $"{{\"connectionId\":\"{Context.ConnectionId}\",\"responseType\":\"{messageType}\"}}"
            );

            _logger.LogDebug("System message saved for session {SessionId}: {Content}",
                session.Id, content.Length > 50 ? content[..50] + "..." : content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save system message for connection {ConnectionId}", Context.ConnectionId);
            // Don't rethrow - message saving failure shouldn't prevent response sending
        }
    }

    #endregion
}

/// <summary>
/// Represents a command response from the coordinator
/// </summary>
public class CommandResponse
{
    public string Message { get; }
    public string Type { get; }

    public CommandResponse(string message, string type)
    {
        Message = message;
        Type = type;
    }
}