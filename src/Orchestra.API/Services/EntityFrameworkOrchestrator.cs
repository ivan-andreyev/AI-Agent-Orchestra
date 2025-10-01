using Orchestra.Core.Models;
using Orchestra.Web.Models;
using TaskStatus = Orchestra.Core.Models.TaskStatus;
using AgentHistoryEntry = Orchestra.Web.Models.AgentHistoryEntry;

namespace Orchestra.API.Services;

/// <summary>
/// Оркестратор на основе Entity Framework для интеграции с базой данных
/// </summary>
public class EntityFrameworkOrchestrator
{
    private readonly AgentRepository _agentRepository;
    private readonly TaskRepository _taskRepository;

    public EntityFrameworkOrchestrator(AgentRepository agentRepository, TaskRepository taskRepository)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
    }

    /// <summary>
    /// Получить текущее состояние оркестратора
    /// </summary>
    public async Task<OrchestratorState> GetCurrentStateAsync()
    {
        var agents = await _agentRepository.GetAllActiveAgentsAsync();
        var taskQueue = await _taskRepository.GetTaskQueueAsync();
        var repositories = await _agentRepository.GetRepositoriesWithAgentsAsync();

        var agentDict = agents.ToDictionary(a => a.Id, a => a);

        return new OrchestratorState(
            Agents: agentDict,
            TaskQueue: taskQueue,
            LastUpdate: DateTime.UtcNow,
            Repositories: repositories
        );
    }

    /// <summary>
    /// Получить всех агентов
    /// </summary>
    public async Task<List<AgentInfo>> GetAllAgentsAsync()
    {
        return await _agentRepository.GetAllActiveAgentsAsync();
    }

    /// <summary>
    /// Зарегистрировать агента
    /// </summary>
    public async Task<bool> RegisterAgentAsync(string id, string name, string type, string repositoryPath)
    {
        return await _agentRepository.RegisterAgentAsync(id, name, type, repositoryPath);
    }

    /// <summary>
    /// Обновить статус агента
    /// </summary>
    public async Task<bool> UpdateAgentStatusAsync(string agentId, AgentStatus status, string? currentTask = null)
    {
        return await _agentRepository.UpdateAgentStatusAsync(agentId, status, currentTask);
    }

    /// <summary>
    /// Добавить задачу в очередь
    /// </summary>
    public async Task<string> QueueTaskAsync(string command, string repositoryPath, TaskPriority priority)
    {
        return await _taskRepository.QueueTaskAsync(command, repositoryPath, priority);
    }

    /// <summary>
    /// Получить следующую задачу для агента
    /// </summary>
    public async Task<TaskRequest?> GetNextTaskForAgentAsync(string agentId)
    {
        return await _taskRepository.GetNextTaskForAgentAsync(agentId);
    }

    /// <summary>
    /// Получить репозитории с агентами
    /// </summary>
    public async Task<Dictionary<string, RepositoryInfo>> GetRepositoriesAsync()
    {
        return await _agentRepository.GetRepositoriesWithAgentsAsync();
    }

    /// <summary>
    /// Обновить статус задачи
    /// </summary>
    public async Task<bool> UpdateTaskStatusAsync(string taskId, TaskStatus status, string? agentId = null, string? result = null, string? errorMessage = null)
    {
        return await _taskRepository.UpdateTaskStatusAsync(taskId, status, agentId, result, errorMessage);
    }

    /// <summary>
    /// Получить задачи для репозитория
    /// </summary>
    public async Task<List<TaskRequest>> GetTasksByRepositoryAsync(string repositoryPath)
    {
        return await _taskRepository.GetTasksByRepositoryAsync(repositoryPath);
    }

    /// <summary>
    /// Получить историю агента (заглушка - можно реализовать позже)
    /// </summary>
    public List<AgentHistoryEntry> GetAgentHistory(string sessionId, int maxEntries = 50)
    {
        // Заглушка - можно реализовать через логи орекстрации
        return new List<AgentHistoryEntry>();
    }

    /// <summary>
    /// Обновить агентов (заглушка)
    /// </summary>
    public void RefreshAgents()
    {
        // Заглушка - можно добавить логику обновления
    }

    /// <summary>
    /// Инициировать назначение задач (заглушка)
    /// </summary>
    public void TriggerTaskAssignment()
    {
        // Заглушка - можно добавить логику назначения задач
    }
}