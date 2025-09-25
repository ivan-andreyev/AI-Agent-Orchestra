using Orchestra.Core;
using Orchestra.Core.Models;
using Orchestra.API.Jobs;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Orchestra.API.Services;

/// <summary>
/// Hangfire-based orchestrator that replaces SimpleOrchestrator's in-memory queue
/// with persistent background job processing through Hangfire infrastructure.
/// This service provides the critical integration bridge between API endpoints
/// and Hangfire job execution.
/// </summary>
public class HangfireOrchestrator
{
    private readonly IBackgroundJobClient _jobClient;
    private readonly SimpleOrchestrator _legacyOrchestrator;
    private readonly ILogger<HangfireOrchestrator> _logger;

    public HangfireOrchestrator(
        IBackgroundJobClient jobClient,
        SimpleOrchestrator legacyOrchestrator,
        ILogger<HangfireOrchestrator> logger)
    {
        _jobClient = jobClient ?? throw new ArgumentNullException(nameof(jobClient));
        _legacyOrchestrator = legacyOrchestrator ?? throw new ArgumentNullException(nameof(legacyOrchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Queues a task for execution through Hangfire background jobs instead of in-memory queue.
    /// This method provides the critical integration between API calls and Hangfire execution.
    /// </summary>
    /// <param name="command">Command to execute</param>
    /// <param name="repositoryPath">Repository path for execution context</param>
    /// <param name="priority">Task priority level</param>
    /// <param name="connectionId">SignalR connection ID for targeted result delivery</param>
    /// <returns>Task ID for tracking</returns>
    public async Task<string> QueueTaskAsync(string command, string repositoryPath, TaskPriority priority = TaskPriority.Normal, string? connectionId = null)
    {
        var taskId = Guid.NewGuid().ToString();

        _logger.LogInformation("Queuing task via Hangfire - TaskId: {TaskId}, Command: {Command}, Repository: {RepositoryPath}, Priority: {Priority}, ConnectionId: {ConnectionId}",
            taskId, command, repositoryPath, priority, connectionId ?? "[none]");

        try
        {
            // Find available agent using existing orchestrator logic
            var availableAgent = await FindAvailableAgentAsync(repositoryPath);

            if (availableAgent == null)
            {
                _logger.LogWarning("No available agents found for repository: {RepositoryPath}", repositoryPath);
                throw new InvalidOperationException($"No available agents for repository: {repositoryPath}");
            }

            // Determine queue based on priority
            var queueName = GetQueueNameForPriority(priority);

            // **CRITICAL INTEGRATION**: Enqueue TaskExecutionJob through Hangfire
            // This replaces the legacy SimpleOrchestrator.QueueTask() in-memory approach
            var jobId = _jobClient.Enqueue<TaskExecutionJob>(
                queueName,
                job => job.ExecuteAsync(
                    taskId,
                    availableAgent.Id,
                    command,
                    repositoryPath,
                    priority,
                    connectionId,
                    null!)); // PerformContext is injected by Hangfire

            _logger.LogInformation("Task successfully enqueued via Hangfire - TaskId: {TaskId}, JobId: {JobId}, AgentId: {AgentId}, Queue: {Queue}",
                taskId, jobId, availableAgent.Id, queueName);

            return taskId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue task via Hangfire - TaskId: {TaskId}, Command: {Command}",
                taskId, command);
            throw;
        }
    }

    /// <summary>
    /// Schedules a task for future execution through Hangfire delayed jobs.
    /// </summary>
    /// <param name="command">Command to execute</param>
    /// <param name="repositoryPath">Repository path for execution context</param>
    /// <param name="executeAt">When to execute the task</param>
    /// <param name="priority">Task priority level</param>
    /// <param name="connectionId">SignalR connection ID for targeted result delivery</param>
    /// <returns>Task ID for tracking</returns>
    public async Task<string> ScheduleTaskAsync(string command, string repositoryPath, DateTime executeAt, TaskPriority priority = TaskPriority.Normal, string? connectionId = null)
    {
        var taskId = Guid.NewGuid().ToString();

        _logger.LogInformation("Scheduling task via Hangfire - TaskId: {TaskId}, ExecuteAt: {ExecuteAt}", taskId, executeAt);

        try
        {
            var availableAgent = await FindAvailableAgentAsync(repositoryPath);

            if (availableAgent == null)
            {
                throw new InvalidOperationException($"No available agents for repository: {repositoryPath}");
            }

            var jobId = _jobClient.Schedule<TaskExecutionJob>(
                job => job.ExecuteAsync(
                    taskId,
                    availableAgent.Id,
                    command,
                    repositoryPath,
                    priority,
                    connectionId,
                    null!), // PerformContext is injected by Hangfire
                executeAt);

            _logger.LogInformation("Task successfully scheduled via Hangfire - TaskId: {TaskId}, JobId: {JobId}", taskId, jobId);
            return taskId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule task via Hangfire - TaskId: {TaskId}", taskId);
            throw;
        }
    }

    // **DELEGATED STATE METHODS**: These delegate to SimpleOrchestrator for state queries
    // since Hangfire handles execution but we still need agent/state management
    public OrchestratorState GetCurrentState() => _legacyOrchestrator.GetCurrentState();
    public List<AgentInfo> GetAllAgents() => _legacyOrchestrator.GetAllAgents();
    public void RegisterAgent(string id, string name, string type, string repositoryPath) => _legacyOrchestrator.RegisterAgent(id, name, type, repositoryPath);
    public void UpdateAgentStatus(string agentId, AgentStatus status, string? currentTask = null) => _legacyOrchestrator.UpdateAgentStatus(agentId, status, currentTask);
    public TaskRequest? GetNextTaskForAgent(string agentId) => _legacyOrchestrator.GetNextTaskForAgent(agentId);
    public Dictionary<string, RepositoryInfo> GetRepositories() => _legacyOrchestrator.GetRepositories();
    public void RefreshAgents() => _legacyOrchestrator.RefreshAgents();
    public void TriggerTaskAssignment() => _legacyOrchestrator.TriggerTaskAssignment();
    public List<AgentHistoryEntry> GetAgentHistory(string sessionId, int maxEntries = 50) => _legacyOrchestrator.GetAgentHistory(sessionId, maxEntries);

    /// <summary>
    /// Initializes Claude Code CLI agents with a warm-up command to avoid cold start delays.
    /// This should be called on application startup.
    /// </summary>
    public async Task WarmupClaudeCodeAgentsAsync()
    {
        try
        {
            _logger.LogInformation("Starting Claude Code CLI warm-up sequence");

            var allAgents = _legacyOrchestrator.GetAllAgents();
            var claudeCodeAgents = allAgents.Where(a => a.Type.Contains("ClaudeCode", StringComparison.OrdinalIgnoreCase)).ToList();

            if (!claudeCodeAgents.Any())
            {
                _logger.LogWarning("No Claude Code agents found for warm-up");
                return;
            }

            foreach (var agent in claudeCodeAgents)
            {
                try
                {
                    // Queue a simple warm-up command with low priority
                    var warmupTaskId = Guid.NewGuid().ToString();
                    var jobId = _jobClient.Enqueue<TaskExecutionJob>(
                        "low-priority",
                        job => job.ExecuteAsync(
                            warmupTaskId,
                            agent.Id,
                            "echo 'Claude Code CLI initialized'",
                            agent.RepositoryPath ?? string.Empty,
                            TaskPriority.Low,
                            null, // No connectionId for warmup
                            null!));

                    _logger.LogInformation("Warm-up task queued for agent {AgentId} - TaskId: {TaskId}, JobId: {JobId}",
                        agent.Id, warmupTaskId, jobId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to queue warm-up task for agent {AgentId}", agent.Id);
                }
            }

            _logger.LogInformation("Claude Code CLI warm-up sequence completed - {Count} agents warmed up", claudeCodeAgents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm up Claude Code agents");
        }
    }

    /// <summary>
    /// Finds an available agent for the specified repository using existing orchestrator logic.
    /// </summary>
    /// <param name="repositoryPath">Repository path to find agent for</param>
    /// <returns>Available agent or null if none found</returns>
    private async Task<AgentInfo?> FindAvailableAgentAsync(string repositoryPath)
    {
        // Use existing orchestrator logic to find available agents
        var allAgents = _legacyOrchestrator.GetAllAgents();

        _logger.LogInformation("FindAvailableAgentAsync - Total agents: {AgentCount}, Repository: {RepositoryPath}",
            allAgents.Count, repositoryPath);

        foreach (var agent in allAgents)
        {
            _logger.LogInformation("Available agent: {AgentId}, Status: {Status}, Repository: {AgentRepository}",
                agent.Id, agent.Status, agent.RepositoryPath);
        }

        // Find agents that are available (Idle or Working) and match the repository
        // Use flexible path matching to handle subdirectory paths
        var availableAgent = allAgents
            .Where(agent => agent.Status == AgentStatus.Idle || agent.Status == AgentStatus.Working)
            .Where(agent => string.IsNullOrEmpty(repositoryPath) ||
                           IsRepositoryPathMatch(agent.RepositoryPath, repositoryPath))
            .OrderBy(agent => agent.LastActiveTime) // Prefer least recently used
            .FirstOrDefault();

        if (availableAgent != null)
        {
            _logger.LogDebug("Found available agent: {AgentId} for repository: {RepositoryPath}",
                availableAgent.Id, repositoryPath);
            return availableAgent;
        }

        // If no agent found, try to create one automatically for the coordinator
        _logger.LogInformation("No available agents found for repository: {RepositoryPath}. Attempting to create a new agent.", repositoryPath);

        var newAgent = await CreateAgentForRepositoryAsync(repositoryPath);
        if (newAgent != null)
        {
            _logger.LogInformation("Successfully created new agent: {AgentId} for repository: {RepositoryPath}",
                newAgent.Id, repositoryPath);
            return newAgent;
        }

        _logger.LogWarning("Failed to create agent for repository: {RepositoryPath}", repositoryPath);
        return null;
    }

    /// <summary>
    /// Determines the appropriate Hangfire queue based on task priority.
    /// </summary>
    /// <param name="priority">Task priority</param>
    /// <returns>Queue name</returns>
    private static string GetQueueNameForPriority(TaskPriority priority)
    {
        return priority switch
        {
            TaskPriority.Critical => "high-priority",
            TaskPriority.High => "high-priority",
            TaskPriority.Low => "default",
            _ => "default"
        };
    }

    /// <summary>
    /// Flexible repository path matching that handles subdirectory paths
    /// </summary>
    /// <param name="agentPath">Agent's repository path</param>
    /// <param name="requestPath">Requested repository path</param>
    /// <returns>True if paths match or are related</returns>
    private static bool IsRepositoryPathMatch(string? agentPath, string requestPath)
    {
        if (string.IsNullOrEmpty(agentPath) || string.IsNullOrEmpty(requestPath))
            return false;

        // Normalize paths
        var normalizedAgentPath = Path.GetFullPath(agentPath).TrimEnd(Path.DirectorySeparatorChar);
        var normalizedRequestPath = Path.GetFullPath(requestPath).TrimEnd(Path.DirectorySeparatorChar);

        // Exact match
        if (normalizedAgentPath.Equals(normalizedRequestPath, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if request path is a subdirectory of agent path
        if (normalizedRequestPath.StartsWith(normalizedAgentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if agent path is a subdirectory of request path
        if (normalizedAgentPath.StartsWith(normalizedRequestPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Creates a new agent for the specified repository automatically.
    /// This makes the coordinator system "smart" by auto-provisioning agents as needed.
    /// </summary>
    /// <param name="repositoryPath">Repository path to create agent for</param>
    /// <returns>Newly created agent or null if creation failed</returns>
    private async Task<AgentInfo?> CreateAgentForRepositoryAsync(string repositoryPath)
    {
        try
        {
            // Generate unique agent ID
            var agentId = $"claude-coordinator-{Guid.NewGuid().ToString()[..8]}";
            var agentName = $"Claude Code Agent (Auto-created for Coordinator)";

            _logger.LogInformation("Creating new agent: {AgentId} for repository: {RepositoryPath}", agentId, repositoryPath);

            // Create new agent info using constructor
            var newAgent = new AgentInfo(agentId, agentName, "claude-code", repositoryPath, AgentStatus.Idle, DateTime.Now);

            // Register agent with the orchestrator
            _legacyOrchestrator.RegisterAgent(newAgent.Id, newAgent.Name, newAgent.Type, newAgent.RepositoryPath);

            _logger.LogInformation("Successfully created and registered agent: {AgentId} for repository: {RepositoryPath}",
                agentId, repositoryPath);

            return newAgent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create agent for repository: {RepositoryPath}", repositoryPath);
            return null;
        }
    }
}