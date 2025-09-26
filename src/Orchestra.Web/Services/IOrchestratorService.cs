using Orchestra.Web.Models;
using Orchestra.Core.Models;
using TaskPriority = Orchestra.Core.Models.TaskPriority;
using AgentHistoryEntry = Orchestra.Web.Models.AgentHistoryEntry;

namespace Orchestra.Web.Services;

/// <summary>
/// Interface for orchestrator service operations
/// Provides abstraction for task management and state monitoring
/// </summary>
public interface IOrchestratorService
{
    Task<OrchestratorState?> GetStateAsync();
    Task<List<AgentInfo>?> GetAgentsAsync();
    Task<bool> QueueTaskAsync(string command, string repositoryPath, TaskPriority priority = TaskPriority.Normal);
    Task<bool> RegisterAgentAsync(string id, string name, string type, string repositoryPath);
    Task<Dictionary<string, RepositoryInfo>?> GetRepositoriesAsync();
    Task<bool> RefreshAgentsAsync();
    Task<bool> PingAsync();
    Task<List<AgentHistoryEntry>?> GetAgentHistoryAsync(string sessionId, int maxEntries = 50);
}