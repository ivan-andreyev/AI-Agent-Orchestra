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
    /// <returns>Task ID for tracking</returns>
    public async Task<string> QueueTaskAsync(string command, string repositoryPath, TaskPriority priority = TaskPriority.Normal)
    {
        var taskId = Guid.NewGuid().ToString();

        _logger.LogInformation("Queuing task via Hangfire - TaskId: {TaskId}, Command: {Command}, Repository: {RepositoryPath}, Priority: {Priority}",
            taskId, command, repositoryPath, priority);

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
    /// <returns>Task ID for tracking</returns>
    public async Task<string> ScheduleTaskAsync(string command, string repositoryPath, DateTime executeAt, TaskPriority priority = TaskPriority.Normal)
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
    /// Finds an available agent for the specified repository using existing orchestrator logic.
    /// </summary>
    /// <param name="repositoryPath">Repository path to find agent for</param>
    /// <returns>Available agent or null if none found</returns>
    private async Task<AgentInfo?> FindAvailableAgentAsync(string repositoryPath)
    {
        // Use existing orchestrator logic to find available agents
        var allAgents = _legacyOrchestrator.GetAllAgents();

        // Find agents that are idle and match the repository
        var availableAgent = allAgents
            .Where(agent => agent.Status == AgentStatus.Idle)
            .Where(agent => string.IsNullOrEmpty(repositoryPath) ||
                           agent.RepositoryPath?.Equals(repositoryPath, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(agent => agent.LastActiveTime) // Prefer least recently used
            .FirstOrDefault();

        if (availableAgent != null)
        {
            _logger.LogDebug("Found available agent: {AgentId} for repository: {RepositoryPath}",
                availableAgent.Id, repositoryPath);
        }
        else
        {
            _logger.LogDebug("No available agents found for repository: {RepositoryPath}", repositoryPath);
        }

        return availableAgent;
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
}