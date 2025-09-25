using Orchestra.Core;
using Orchestra.Core.Models;
using Orchestra.API.Jobs;
using Orchestra.Web.Models;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Orchestra.API.Services;

/// <summary>
/// Hangfire-based orchestrator that replaces SimpleOrchestrator's in-memory queue
/// with persistent background job processing through Hangfire infrastructure.
/// Enhanced with Entity Framework integration for robust agent registration and discovery.
/// </summary>
public class HangfireOrchestrator
{
    private readonly IBackgroundJobClient _jobClient;
    private readonly EntityFrameworkOrchestrator _entityOrchestrator;
    private readonly SimpleOrchestrator _legacyOrchestrator;
    private readonly ILogger<HangfireOrchestrator> _logger;

    public HangfireOrchestrator(
        IBackgroundJobClient jobClient,
        EntityFrameworkOrchestrator entityOrchestrator,
        SimpleOrchestrator legacyOrchestrator,
        ILogger<HangfireOrchestrator> logger)
    {
        _jobClient = jobClient ?? throw new ArgumentNullException(nameof(jobClient));
        _entityOrchestrator = entityOrchestrator ?? throw new ArgumentNullException(nameof(entityOrchestrator));
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
    public async Task<string> QueueTaskAsync(string command, string repositoryPath, Orchestra.Core.TaskPriority priority = Orchestra.Core.TaskPriority.Normal, string? connectionId = null)
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
    public async Task<string> ScheduleTaskAsync(string command, string repositoryPath, DateTime executeAt, Orchestra.Core.TaskPriority priority = Orchestra.Core.TaskPriority.Normal, string? connectionId = null)
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

    // **ENHANCED STATE METHODS**: Use Entity Framework orchestrator for persistence with fallback to legacy
    public async Task<Orchestra.Web.Models.OrchestratorState> GetCurrentStateAsync() => await _entityOrchestrator.GetCurrentStateAsync();
    public Orchestra.Core.OrchestratorState GetCurrentState() => _legacyOrchestrator.GetCurrentState(); // Sync fallback

    public async Task<List<Orchestra.Web.Models.AgentInfo>> GetAllAgentsAsync() => await _entityOrchestrator.GetAllAgentsAsync();
    public List<Orchestra.Core.AgentInfo> GetAllAgents() => _legacyOrchestrator.GetAllAgents(); // Legacy fallback with different model type

    public async Task<bool> RegisterAgentAsync(string id, string name, string type, string repositoryPath)
    {
        var result = await _entityOrchestrator.RegisterAgentAsync(id, name, type, repositoryPath);
        // Also register with legacy orchestrator for backward compatibility
        _legacyOrchestrator.RegisterAgent(id, name, type, repositoryPath);
        return result;
    }
    public void RegisterAgent(string id, string name, string type, string repositoryPath) => _legacyOrchestrator.RegisterAgent(id, name, type, repositoryPath);

    public async Task<bool> UpdateAgentStatusAsync(string agentId, Orchestra.Web.Models.AgentStatus status, string? currentTask = null)
    {
        var result = await _entityOrchestrator.UpdateAgentStatusAsync(agentId, status, currentTask);
        // Update legacy orchestrator for backward compatibility
        _legacyOrchestrator.UpdateAgentStatus(agentId, ConvertToLegacyAgentStatus(status), currentTask);
        return result;
    }
    public void UpdateAgentStatus(string agentId, Orchestra.Core.AgentStatus status, string? currentTask = null) => _legacyOrchestrator.UpdateAgentStatus(agentId, status, currentTask);

    public async Task<Orchestra.Web.Models.TaskRequest?> GetNextTaskForAgentAsync(string agentId) => await _entityOrchestrator.GetNextTaskForAgentAsync(agentId);
    public Orchestra.Core.TaskRequest? GetNextTaskForAgent(string agentId) => _legacyOrchestrator.GetNextTaskForAgent(agentId);

    public async Task<Dictionary<string, Orchestra.Web.Models.RepositoryInfo>> GetRepositoriesAsync() => await _entityOrchestrator.GetRepositoriesAsync();
    public Dictionary<string, Orchestra.Core.RepositoryInfo> GetRepositories() => _legacyOrchestrator.GetRepositories();

    public void RefreshAgents() => _legacyOrchestrator.RefreshAgents();
    public void TriggerTaskAssignment() => _legacyOrchestrator.TriggerTaskAssignment();
    public List<Orchestra.Core.Models.AgentHistoryEntry> GetAgentHistory(string sessionId, int maxEntries = 50) => new List<Orchestra.Core.Models.AgentHistoryEntry>();

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
                            Orchestra.Core.TaskPriority.Low,
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
    /// Finds an available agent for the specified repository using Entity Framework orchestrator with fallback.
    /// Enhanced with automatic agent discovery and creation capabilities.
    /// </summary>
    /// <param name="repositoryPath">Repository path to find agent for</param>
    /// <returns>Available agent or null if none found</returns>
    private async Task<Orchestra.Core.AgentInfo?> FindAvailableAgentAsync(string repositoryPath)
    {
        try
        {
            // First try Entity Framework orchestrator for persistent agents
            var entityAgents = await _entityOrchestrator.GetAllAgentsAsync();

            _logger.LogInformation("FindAvailableAgentAsync - EF agents: {AgentCount}, Repository: {RepositoryPath}",
                entityAgents.Count, repositoryPath);

            // Find available agents from Entity Framework store
            var availableEntityAgent = entityAgents
                .Where(agent => agent.Status == Orchestra.Web.Models.AgentStatus.Idle ||
                               agent.Status == Orchestra.Web.Models.AgentStatus.Working)
                .Where(agent => string.IsNullOrEmpty(repositoryPath) ||
                               IsRepositoryPathMatch(agent.RepositoryPath, repositoryPath))
                .OrderBy(agent => agent.LastPing) // Prefer least recently used
                .FirstOrDefault();

            if (availableEntityAgent != null)
            {
                _logger.LogDebug("Found available EF agent: {AgentId} for repository: {RepositoryPath}",
                    availableEntityAgent.Id, repositoryPath);
                return ConvertToLegacyAgentInfo(availableEntityAgent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Entity Framework agents, falling back to legacy orchestrator");
        }

        // Fallback to legacy orchestrator for in-memory agents
        var allAgents = _legacyOrchestrator.GetAllAgents();

        _logger.LogInformation("FindAvailableAgentAsync - Legacy agents: {AgentCount}, Repository: {RepositoryPath}",
            allAgents.Count, repositoryPath);

        foreach (var agent in allAgents)
        {
            _logger.LogInformation("Available legacy agent: {AgentId}, Status: {Status}, Repository: {AgentRepository}",
                agent.Id, agent.Status, agent.RepositoryPath);
        }

        var availableAgent = allAgents
            .Where(agent => agent.Status == Orchestra.Core.AgentStatus.Idle || agent.Status == Orchestra.Core.AgentStatus.Working)
            .Where(agent => string.IsNullOrEmpty(repositoryPath) ||
                           IsRepositoryPathMatch(agent.RepositoryPath, repositoryPath))
            .OrderBy(agent => agent.LastActiveTime)
            .FirstOrDefault();

        if (availableAgent != null)
        {
            _logger.LogDebug("Found available legacy agent: {AgentId} for repository: {RepositoryPath}",
                availableAgent.Id, repositoryPath);
            return availableAgent;
        }

        // If no agent found, try to create one automatically
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
    private static string GetQueueNameForPriority(Orchestra.Core.TaskPriority priority)
    {
        return priority switch
        {
            Orchestra.Core.TaskPriority.Critical => "high-priority",
            Orchestra.Core.TaskPriority.High => "high-priority",
            Orchestra.Core.TaskPriority.Low => "default",
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
    /// Creates a new agent for the specified repository automatically using Entity Framework.
    /// This makes the coordinator system "smart" by auto-provisioning agents as needed.
    /// </summary>
    /// <param name="repositoryPath">Repository path to create agent for</param>
    /// <returns>Newly created agent or null if creation failed</returns>
    private async Task<Orchestra.Core.AgentInfo?> CreateAgentForRepositoryAsync(string repositoryPath)
    {
        try
        {
            // Generate unique agent ID
            var agentId = $"claude-coordinator-{Guid.NewGuid().ToString()[..8]}";
            var agentName = $"Claude Code Agent (Auto-created for Coordinator)";

            _logger.LogInformation("Creating new agent: {AgentId} for repository: {RepositoryPath}", agentId, repositoryPath);

            // Register agent with Entity Framework orchestrator first
            var success = await _entityOrchestrator.RegisterAgentAsync(agentId, agentName, "claude-code", repositoryPath);

            if (success)
            {
                // Also register with legacy orchestrator for backward compatibility
                _legacyOrchestrator.RegisterAgent(agentId, agentName, "claude-code", repositoryPath);

                // Create new agent info for return
                var newAgent = new Orchestra.Core.AgentInfo(agentId, agentName, "claude-code", repositoryPath, Orchestra.Core.AgentStatus.Idle, DateTime.Now);

                _logger.LogInformation("Successfully created and registered agent: {AgentId} for repository: {RepositoryPath}",
                    agentId, repositoryPath);

                return newAgent;
            }
            else
            {
                _logger.LogError("Failed to register agent {AgentId} with Entity Framework orchestrator", agentId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create agent for repository: {RepositoryPath}", repositoryPath);
            return null;
        }
    }

    /// <summary>
    /// Converts Entity Framework AgentInfo to legacy AgentInfo model
    /// </summary>
    /// <param name="entityAgent">Entity Framework agent info</param>
    /// <returns>Legacy agent info</returns>
    private static Orchestra.Core.AgentInfo ConvertToLegacyAgentInfo(Orchestra.Web.Models.AgentInfo entityAgent)
    {
        return new Orchestra.Core.AgentInfo(
            entityAgent.Id,
            entityAgent.Name,
            entityAgent.Type,
            entityAgent.RepositoryPath,
            ConvertToLegacyAgentStatus(entityAgent.Status),
            entityAgent.LastPing
        );
    }

    /// <summary>
    /// Converts Web Models AgentStatus to Core Models AgentStatus
    /// </summary>
    /// <param name="status">Web models agent status</param>
    /// <returns>Core models agent status</returns>
    private static Orchestra.Core.AgentStatus ConvertToLegacyAgentStatus(Orchestra.Web.Models.AgentStatus status)
    {
        return status switch
        {
            Orchestra.Web.Models.AgentStatus.Idle => Orchestra.Core.AgentStatus.Idle,
            Orchestra.Web.Models.AgentStatus.Working => Orchestra.Core.AgentStatus.Working,
            Orchestra.Web.Models.AgentStatus.Error => Orchestra.Core.AgentStatus.Error,
            Orchestra.Web.Models.AgentStatus.Offline => Orchestra.Core.AgentStatus.Offline,
            _ => Orchestra.Core.AgentStatus.Offline
        };
    }
}