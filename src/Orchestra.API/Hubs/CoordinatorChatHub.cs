using Microsoft.AspNetCore.SignalR;
using Orchestra.Core;
using Orchestra.Core.Models;
using Orchestra.API.Services;
using System.Text;

namespace Orchestra.API.Hubs;

/// <summary>
/// SignalR hub for interactive chat with the coordinator agent.
/// Provides command-line style interface for managing the orchestration system.
/// </summary>
public class CoordinatorChatHub : Hub
{
    private readonly SimpleOrchestrator _orchestrator;
    private readonly HangfireOrchestrator _hangfireOrchestrator;
    private readonly ILogger<CoordinatorChatHub> _logger;

    public CoordinatorChatHub(SimpleOrchestrator orchestrator, HangfireOrchestrator hangfireOrchestrator, ILogger<CoordinatorChatHub> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _hangfireOrchestrator = hangfireOrchestrator ?? throw new ArgumentNullException(nameof(hangfireOrchestrator));
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
            // Use the correct repository path for agents
            var repositoryPath = @"C:\Users\mrred\RiderProjects\AI-Agent-Orchestra";
            _logger.LogInformation("Using hardcoded repository path for testing: {RepositoryPath}", repositoryPath);
            var jobId = await _hangfireOrchestrator.QueueTaskAsync(command, repositoryPath, TaskPriority.High);

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
        status.AppendLine($"• 🔄 Working: {allAgents.Count(a => a.Status == AgentStatus.Working)}");
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
            "working" => allAgents.Where(a => a.Status == AgentStatus.Working),
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
                AgentStatus.Working => "🔄",
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
    /// Queues a task for execution
    /// </summary>
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
                Orchestra.Core.TaskStatus.Pending => "⏳",
                Orchestra.Core.TaskStatus.Assigned => "📤",
                Orchestra.Core.TaskStatus.InProgress => "🔄",
                Orchestra.Core.TaskStatus.Completed => "✅",
                Orchestra.Core.TaskStatus.Failed => "❌",
                Orchestra.Core.TaskStatus.Cancelled => "🚫",
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
        await Clients.Caller.SendAsync("ReceiveResponse", new
        {
            Message = message,
            Type = type,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Handles client connection
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Coordinator chat client connected: {ConnectionId}", Context.ConnectionId);

        await SendResponse("🤖 **Coordinator Agent Connected**\n" +
                          "Type 'help' for available commands or 'status' for system overview.", "info");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Handles client disconnection
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Coordinator chat client disconnected: {ConnectionId}. Reason: {Exception}",
            Context.ConnectionId, exception?.Message ?? "Normal disconnect");

        await base.OnDisconnectedAsync(exception);
    }
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