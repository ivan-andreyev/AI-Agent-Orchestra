using Orchestra.Web.Models;
using Orchestra.Core.Models;
using Microsoft.Extensions.Logging;
using TaskPriority = Orchestra.Core.Models.TaskPriority;
using AgentHistoryEntry = Orchestra.Web.Models.AgentHistoryEntry;

namespace Orchestra.Web.Services;

/// <summary>
/// Performance-monitored wrapper for OrchestratorService.
/// Implements continuous monitoring as required by Phase 0.2 of the UI-Fixes-WorkPlan.
/// </summary>
public class MonitoredOrchestratorService
{
    private readonly OrchestratorService _orchestratorService;
    private readonly PerformanceMonitoringService _performanceService;
    private readonly ILogger<MonitoredOrchestratorService> _logger;

    public MonitoredOrchestratorService(
        OrchestratorService orchestratorService,
        PerformanceMonitoringService performanceService,
        ILogger<MonitoredOrchestratorService> logger)
    {
        _orchestratorService = orchestratorService;
        _performanceService = performanceService;
        _logger = logger;
    }

    /// <summary>
    /// Get orchestrator state with performance monitoring
    /// </summary>
    public async Task<OrchestratorState?> GetStateAsync()
    {
        var result = await _performanceService.MeasureApiResponseAsync("state", async () =>
        {
            var state = await _orchestratorService.GetStateAsync();
            if (state == null)
            {
                throw new InvalidOperationException("Failed to retrieve orchestrator state");
            }
        });

        if (result.IsRegression)
        {
            _logger.LogWarning("Performance regression detected in GetStateAsync: {Duration}ms", result.Duration);
        }

        return await _orchestratorService.GetStateAsync();
    }

    /// <summary>
    /// Get agents list with performance monitoring
    /// </summary>
    public async Task<List<AgentInfo>?> GetAgentsAsync()
    {
        var result = await _performanceService.MeasureApiResponseAsync("agents", async () =>
        {
            await _orchestratorService.GetAgentsAsync();
        });

        if (result.IsRegression)
        {
            _logger.LogWarning("Performance regression detected in GetAgentsAsync: {Duration}ms", result.Duration);
        }

        return await _orchestratorService.GetAgentsAsync();
    }

    /// <summary>
    /// Get repositories with performance monitoring
    /// </summary>
    public async Task<Dictionary<string, RepositoryInfo>?> GetRepositoriesAsync()
    {
        var result = await _performanceService.MeasureApiResponseAsync("repositories", async () =>
        {
            await _orchestratorService.GetRepositoriesAsync();
        });

        if (result.IsRegression)
        {
            _logger.LogWarning("Performance regression detected in GetRepositoriesAsync: {Duration}ms", result.Duration);
        }

        return await _orchestratorService.GetRepositoriesAsync();
    }

    /// <summary>
    /// Queue task with performance monitoring
    /// </summary>
    public async Task<bool> QueueTaskAsync(string command, string repositoryPath, TaskPriority priority = TaskPriority.Normal)
    {
        var result = await _performanceService.MeasureApiResponseAsync("tasks/queue", async () =>
        {
            await _orchestratorService.QueueTaskAsync(command, repositoryPath, priority);
        });

        if (result.IsRegression)
        {
            _logger.LogWarning("Performance regression detected in QueueTaskAsync: {Duration}ms", result.Duration);
        }

        return await _orchestratorService.QueueTaskAsync(command, repositoryPath, priority);
    }

    /// <summary>
    /// Register agent with performance monitoring
    /// </summary>
    public async Task<bool> RegisterAgentAsync(string id, string name, string type, string repositoryPath, int maxConcurrentTasks = 1)
    {
        var result = await _performanceService.MeasureApiResponseAsync("agents/register", async () =>
        {
            await _orchestratorService.RegisterAgentAsync(id, name, type, repositoryPath, maxConcurrentTasks);
        });

        if (result.IsRegression)
        {
            _logger.LogWarning("Performance regression detected in RegisterAgentAsync: {Duration}ms", result.Duration);
        }

        return await _orchestratorService.RegisterAgentAsync(id, name, type, repositoryPath, maxConcurrentTasks);
    }

    /// <summary>
    /// Delete agent with performance monitoring
    /// </summary>
    public async Task<bool> DeleteAgentAsync(string agentId, bool hardDelete = false)
    {
        var result = await _performanceService.MeasureApiResponseAsync("agents/delete", async () =>
        {
            await _orchestratorService.DeleteAgentAsync(agentId, hardDelete);
        });

        if (result.IsRegression)
        {
            _logger.LogWarning("Performance regression detected in DeleteAgentAsync: {Duration}ms", result.Duration);
        }

        return await _orchestratorService.DeleteAgentAsync(agentId, hardDelete);
    }

    /// <summary>
    /// Update agent status with performance monitoring
    /// </summary>
    public async Task<bool> UpdateAgentStatusAsync(string agentId, string status, string? currentTask = null, string? statusMessage = null)
    {
        var result = await _performanceService.MeasureApiResponseAsync("agents/status", async () =>
        {
            await _orchestratorService.UpdateAgentStatusAsync(agentId, status, currentTask, statusMessage);
        });

        if (result.IsRegression)
        {
            _logger.LogWarning("Performance regression detected in UpdateAgentStatusAsync: {Duration}ms", result.Duration);
        }

        return await _orchestratorService.UpdateAgentStatusAsync(agentId, status, currentTask, statusMessage);
    }

    /// <summary>
    /// Get agent history with performance monitoring
    /// </summary>
    public async Task<List<AgentHistoryEntry>?> GetAgentHistoryAsync(string sessionId, int maxEntries = 50)
    {
        var result = await _performanceService.MeasureApiResponseAsync("agents/history", async () =>
        {
            await _orchestratorService.GetAgentHistoryAsync(sessionId, maxEntries);
        });

        if (result.IsRegression)
        {
            _logger.LogWarning("Performance regression detected in GetAgentHistoryAsync: {Duration}ms", result.Duration);
        }

        return await _orchestratorService.GetAgentHistoryAsync(sessionId, maxEntries);
    }

    /// <summary>
    /// Refresh agents with performance monitoring
    /// </summary>
    public async Task<bool> RefreshAgentsAsync()
    {
        var result = await _performanceService.MeasureApiResponseAsync("refresh", async () =>
        {
            await _orchestratorService.RefreshAgentsAsync();
        });

        if (result.IsRegression)
        {
            _logger.LogWarning("Performance regression detected in RefreshAgentsAsync: {Duration}ms", result.Duration);
        }

        return await _orchestratorService.RefreshAgentsAsync();
    }

    /// <summary>
    /// Ping service with performance monitoring
    /// </summary>
    public async Task<bool> PingAsync()
    {
        var result = await _performanceService.MeasureApiResponseAsync("ping", async () =>
        {
            await _orchestratorService.PingAsync();
        });

        if (result.IsRegression)
        {
            _logger.LogWarning("Performance regression detected in PingAsync: {Duration}ms", result.Duration);
        }

        return await _orchestratorService.PingAsync();
    }

    /// <summary>
    /// Measure statistics calculation performance (for 221 agent aggregation)
    /// </summary>
    public T MeasureStatisticsCalculation<T>(Func<T> calculationFunc, string operationName = "statistics")
    {
        var result = _performanceService.MeasureStatisticsCalculation(() => calculationFunc());

        if (result.IsRegression)
        {
            _logger.LogWarning("Statistics calculation performance regression detected in {Operation}: {Duration}ms",
                operationName, result.Duration);
        }

        return calculationFunc();
    }

    /// <summary>
    /// Get current performance metrics summary
    /// </summary>
    public PerformanceMetricsSummary GetPerformanceMetrics()
    {
        return _performanceService.GetMetricsSummary();
    }

    /// <summary>
    /// Get specific performance metric
    /// </summary>
    public PerformanceMetric? GetPerformanceMetric(string metricName)
    {
        return _performanceService.GetMetric(metricName);
    }

    /// <summary>
    /// Clear old performance metrics
    /// </summary>
    public void ClearOldMetrics(TimeSpan maxAge)
    {
        _performanceService.ClearOldMetrics(maxAge);
    }
}