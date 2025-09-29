using Orchestra.Core.Models;
using Orchestra.Core.Services;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Abstractions;
using TaskStatus = Orchestra.Core.Models.TaskStatus;
using TaskPriority = Orchestra.Core.Models.TaskPriority;

namespace Orchestra.Core;

public class SimpleOrchestrator : IDisposable
{
    private readonly Queue<TaskRequest> _taskQueue = new();
    private readonly string _stateFilePath;
    private readonly ClaudeSessionDiscovery _sessionDiscovery;
    private readonly object _lock = new();
    private readonly IClaudeCodeCoreService? _claudeCodeService;
    private readonly IAgentStateStore _agentStateStore;

    public SimpleOrchestrator(IAgentStateStore agentStateStore, IClaudeCodeCoreService? claudeCodeService = null, string stateFilePath = "orchestrator-state.json")
    {
        _agentStateStore = agentStateStore ?? throw new ArgumentNullException(nameof(agentStateStore));
        _claudeCodeService = claudeCodeService;

        // Generate unique file path for tests to avoid conflicts
        if (stateFilePath == "orchestrator-state.json" && IsRunningInTest())
        {
            _stateFilePath = $"orchestrator-state-{Guid.NewGuid():N}.json";
        }
        else
        {
            _stateFilePath = stateFilePath;
        }

        _sessionDiscovery = new ClaudeSessionDiscovery();
        LoadState();
    }

    private static bool IsRunningInTest()
    {
        // Check if we're running in a test environment
        return AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => a.FullName?.Contains("xunit") == true ||
                      a.FullName?.Contains("Microsoft.TestPlatform") == true);
    }

    public async Task RegisterAgentAsync(string id, string name, string type, string repositoryPath)
    {
        var agent = new AgentInfo(id, name, type, repositoryPath, AgentStatus.Idle, DateTime.UtcNow);
        await _agentStateStore.RegisterAgentAsync(agent);
        SaveState();
    }

    public void RegisterAgent(string id, string name, string type, string repositoryPath)
    {
        // Синхронная версия для обратной совместимости
        RegisterAgentAsync(id, name, type, repositoryPath).Wait();
    }

    public async Task UpdateAgentStatusAsync(string agentId, AgentStatus status, string? currentTask = null)
    {
        await _agentStateStore.UpdateAgentStatusAsync(agentId, status, currentTask);
        SaveState();
    }

    public void UpdateAgentStatus(string agentId, AgentStatus status, string? currentTask = null)
    {
        // Синхронная версия для обратной совместимости
        UpdateAgentStatusAsync(agentId, status, currentTask).Wait();
    }

    public void QueueTask(string command, string repositoryPath, TaskPriority priority = TaskPriority.Normal)
    {
        lock (_lock)
        {
            var availableAgent = FindAvailableAgent(repositoryPath);
            var agentId = availableAgent?.Id ?? ""; // Empty if no agent available yet

            var taskStatus = string.IsNullOrEmpty(agentId) ? TaskStatus.Pending : TaskStatus.Assigned;
            var startedAt = taskStatus == TaskStatus.Assigned ? DateTime.Now : (DateTime?)null;

            var task = new TaskRequest(
                Guid.NewGuid().ToString(),
                agentId,
                command,
                repositoryPath,
                DateTime.Now,
                priority,
                taskStatus,
                startedAt,
                null
            );

            _taskQueue.Enqueue(task);
            SaveState();

            // Phase 4.3.1: Automatic assignment trigger for tasks created without immediate agent
            if (string.IsNullOrEmpty(agentId))
            {
                TriggerTaskAssignment();
            }
        }
    }

    public TaskRequest? GetNextTaskForAgent(string agentId)
    {
        lock (_lock)
        {
            // First check for tasks already assigned to this agent
            var tasksForAgent = _taskQueue.Where(t => t.AgentId == agentId).ToList();
            if (tasksForAgent.Any())
            {
                var anyTask = tasksForAgent.First();
                RemoveTaskFromQueue(anyTask.Id);
                return anyTask;
            }

            // If no assigned tasks, look for unassigned tasks that could be handled by this agent
            var agent = _agentStateStore.GetAgentAsync(agentId).Result;
            if (agent == null)
            {
                return null;
            }

            var unassignedTasks = _taskQueue.Where(t => string.IsNullOrEmpty(t.AgentId) ||
                                                        (t.RepositoryPath == agent.RepositoryPath))
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToList();

            if (!unassignedTasks.Any())
            {
                return null;
            }

            var task = unassignedTasks.First();
            RemoveTaskFromQueue(task.Id);

            // Update task with agent assignment
            var assignedTask = task with { AgentId = agentId };
            SaveState();
            return assignedTask;
        }
    }

    private void RemoveTaskFromQueue(string taskId)
    {
        var newQueue = new Queue<TaskRequest>(_taskQueue.Where(t => t.Id != taskId));
        _taskQueue.Clear();
        foreach (var t in newQueue)
        {
            _taskQueue.Enqueue(t);
        }
        SaveState();
    }

    public async Task<List<AgentInfo>> GetAllAgentsAsync()
    {
        return await _agentStateStore.GetAllAgentsAsync();
    }

    public List<AgentInfo> GetAllAgents()
    {
        // Синхронная версия для обратной совместимости
        return GetAllAgentsAsync().Result;
    }

    public async Task ClearAllAgentsAsync()
    {
        await _agentStateStore.ClearAllAgentsAsync();
        lock (_lock)
        {
            _taskQueue.Clear();
        }
    }

    public void ClearAllAgents()
    {
        // Синхронная версия для обратной совместимости
        ClearAllAgentsAsync().Wait();
    }

    public void RefreshAgents()
    {
        lock (_lock)
        {
            var discoveredAgents = _sessionDiscovery.DiscoverActiveSessions();

            // Clear current agents and add discovered ones
            _agentStateStore.ClearAllAgentsAsync().Wait();
            foreach (var agent in discoveredAgents)
            {
                _agentStateStore.RegisterAgentAsync(agent).Wait();
            }

            // After refreshing agents, try to assign any unassigned tasks
            AssignUnassignedTasks();

            SaveState();
        }
    }

    public Dictionary<string, RepositoryInfo> GetRepositories()
    {
        RefreshAgents();
        lock (_lock)
        {
            var allAgents = _agentStateStore.GetAllAgentsAsync().Result;
            return _sessionDiscovery.GroupAgentsByRepository(allAgents);
        }
    }

    public OrchestratorState GetCurrentState()
    {
        lock (_lock)
        {
            var allAgents = _agentStateStore.GetAllAgentsAsync().Result;
            var agentsDict = allAgents.ToDictionary(a => a.Id, a => a);
            var repositories = _sessionDiscovery.GroupAgentsByRepository(allAgents);
            return new OrchestratorState(agentsDict, new Queue<TaskRequest>(_taskQueue), DateTime.Now, repositories);
        }
    }

    public List<AgentHistoryEntry> GetAgentHistory(string sessionId, int maxEntries = 50)
    {
        return _sessionDiscovery.GetAgentHistory(sessionId, maxEntries);
    }

    /// <summary>
    /// Manually triggers assignment of unassigned tasks to available agents.
    /// Can be called from API to force assignment without full agent refresh.
    /// </summary>
    public void TriggerTaskAssignment()
    {
        lock (_lock)
        {
            AssignUnassignedTasks();
            SaveState();
        }
    }

    private AgentInfo? FindAvailableAgent(string repositoryPath)
    {
        var allAgents = _agentStateStore.GetAllAgentsAsync().Result;
        return allAgents
            .Where(a => a.Status == AgentStatus.Idle && a.RepositoryPath == repositoryPath)
            .OrderBy(a => a.LastPing)
            .FirstOrDefault()
            ?? allAgents
                .Where(a => a.Status == AgentStatus.Idle)
                .OrderBy(a => a.LastPing)
                .FirstOrDefault();
    }

    /// <summary>
    /// Assigns unassigned tasks to available agents.
    /// Called after agent refresh to handle tasks that were created when no agents were available.
    /// </summary>
    private void AssignUnassignedTasks()
    {
        var pendingTasks = _taskQueue.Where(t => t.Status == TaskStatus.Pending).ToList();
        var tasksToUpdate = new List<(TaskRequest oldTask, TaskRequest newTask)>();

        foreach (var task in pendingTasks)
        {
            var availableAgent = FindAvailableAgent(task.RepositoryPath);
            if (availableAgent != null)
            {
                // Create updated task with agent assignment and status change
                var assignedTask = task with
                {
                    AgentId = availableAgent.Id,
                    Status = TaskStatus.Assigned,
                    StartedAt = DateTime.Now
                };
                tasksToUpdate.Add((task, assignedTask));
            }
        }

        // Update the queue with assigned tasks
        if (tasksToUpdate.Any())
        {
            var newQueue = new Queue<TaskRequest>();
            foreach (var task in _taskQueue)
            {
                var update = tasksToUpdate.FirstOrDefault(u => u.oldTask.Id == task.Id);
                if (update.oldTask != null)
                {
                    newQueue.Enqueue(update.newTask);
                }
                else
                {
                    newQueue.Enqueue(task);
                }
            }

            _taskQueue.Clear();
            foreach (var task in newQueue)
            {
                _taskQueue.Enqueue(task);
            }
        }
    }

    /// <summary>
    /// Updates the status of a specific task
    /// </summary>
    public void UpdateTaskStatus(string taskId, TaskStatus newStatus)
    {
        lock (_lock)
        {
            var tasksToUpdate = new List<TaskRequest>();
            var newQueue = new Queue<TaskRequest>();

            while (_taskQueue.Count > 0)
            {
                var task = _taskQueue.Dequeue();
                if (task.Id == taskId)
                {
                    var updatedTask = task with
                    {
                        Status = newStatus,
                        StartedAt = newStatus == TaskStatus.InProgress ? (task.StartedAt ?? DateTime.Now) : task.StartedAt,
                        CompletedAt = newStatus is TaskStatus.Completed or TaskStatus.Failed or TaskStatus.Cancelled ? DateTime.Now : null
                    };
                    newQueue.Enqueue(updatedTask);
                }
                else
                {
                    newQueue.Enqueue(task);
                }
            }

            _taskQueue.Clear();
            foreach (var task in newQueue)
            {
                _taskQueue.Enqueue(task);
            }

            SaveState();
        }
    }

    /// <summary>
    /// Marks a task as started when an agent begins working on it
    /// </summary>
    public void StartTask(string taskId, string agentId)
    {
        UpdateTaskStatus(taskId, TaskStatus.InProgress);
    }

    /// <summary>
    /// Marks a task as completed successfully
    /// </summary>
    public void CompleteTask(string taskId)
    {
        UpdateTaskStatus(taskId, TaskStatus.Completed);
    }

    /// <summary>
    /// Marks a task as failed
    /// </summary>
    public void FailTask(string taskId)
    {
        UpdateTaskStatus(taskId, TaskStatus.Failed);
    }

    /// <summary>
    /// Cancels a task
    /// </summary>
    public void CancelTask(string taskId)
    {
        UpdateTaskStatus(taskId, TaskStatus.Cancelled);
    }

    /// <summary>
    /// Gets an agent by ID
    /// </summary>
    public AgentInfo? GetAgentById(string agentId)
    {
        lock (_lock)
        {
            return _agentStateStore.GetAgentAsync(agentId).Result;
        }
    }

    /// <summary>
    /// Gets a task by ID
    /// </summary>
    public TaskRequest? GetTaskById(string taskId)
    {
        lock (_lock)
        {
            return _taskQueue.FirstOrDefault(t => t.Id == taskId);
        }
    }

    /// <summary>
    /// Проверяет, является ли агент типом Claude Code
    /// </summary>
    /// <param name="agentId">Идентификатор агента</param>
    /// <returns>True, если агент типа Claude Code</returns>
    public bool IsClaudeCodeAgent(string agentId)
    {
        lock (_lock)
        {
            var agent = _agentStateStore.GetAgentAsync(agentId).Result;
            return agent?.Type == "claude-code";
        }
    }

    /// <summary>
    /// Получает всех агентов типа Claude Code
    /// </summary>
    /// <returns>Список агентов типа Claude Code</returns>
    public List<AgentInfo> GetClaudeCodeAgents()
    {
        lock (_lock)
        {
            var allAgents = _agentStateStore.GetAllAgentsAsync().Result;
            return allAgents
                .Where(agent => agent.Type == "claude-code")
                .ToList();
        }
    }

    /// <summary>
    /// Асинхронно выполняет задачу через Claude Code агента, если доступно
    /// </summary>
    /// <param name="task">Задача для выполнения</param>
    /// <returns>Результат выполнения задачи или null, если агент не Claude Code</returns>
    public async Task<string?> AssignTaskToClaudeCodeAgent(TaskRequest task)
    {
        // Найти доступного Claude Code агента
        var agent = FindAvailableAgent(task.RepositoryPath);

        if (agent == null || agent.Type != "claude-code")
        {
            return null; // Нет доступного Claude Code агента
        }

        if (_claudeCodeService == null)
        {
            return "Claude Code service not available";
        }

        try
        {
            // Проверить доступность агента перед выполнением
            var isAvailable = await _claudeCodeService.IsAgentAvailableAsync(agent.Id);
            if (!isAvailable)
            {
                return "Claude Code agent is not available";
            }

            // Выполнить команду через Claude Code service
            var parameters = new Dictionary<string, object>
            {
                ["repositoryPath"] = task.RepositoryPath,
                ["timeout"] = TimeSpan.FromMinutes(10),
                ["priority"] = task.Priority.ToString()
            };

            var result = await _claudeCodeService.ExecuteCommandAsync(
                agent.Id,
                task.Command,
                parameters);

            // Обновить статус задачи
            if (result.Success)
            {
                UpdateTaskStatus(task.Id, TaskStatus.Completed);
                return $"Task completed successfully by Claude Code agent {agent.Name}";
            }
            else
            {
                UpdateTaskStatus(task.Id, TaskStatus.Failed);
                return $"Task failed: {result.ErrorMessage ?? "Unknown error"}";
            }
        }
        catch (Exception ex)
        {
            UpdateTaskStatus(task.Id, TaskStatus.Failed);
            return $"Error executing task via Claude Code: {ex.Message}";
        }
    }

    /// <summary>
    /// Определяет наилучший доступный Claude Code агент для задачи
    /// </summary>
    /// <param name="repositoryPath">Путь к репозиторию</param>
    /// <returns>Информация об агенте или null, если нет доступного</returns>
    public AgentInfo? GetBestClaudeCodeAgent(string repositoryPath)
    {
        lock (_lock)
        {
            var allAgents = _agentStateStore.GetAllAgentsAsync().Result;
            // Предпочтение агентам с тем же репозиторием
            return allAgents
                .Where(a => a.Status == AgentStatus.Idle && a.Type == "claude-code" && a.RepositoryPath == repositoryPath)
                .OrderBy(a => a.LastPing)
                .FirstOrDefault()
                ?? allAgents
                    .Where(a => a.Status == AgentStatus.Idle && a.Type == "claude-code")
                    .OrderBy(a => a.LastPing)
                    .FirstOrDefault();
        }
    }

    private void SaveState()
    {
        // Create thread-safe copies for serialization
        var allAgents = _agentStateStore.GetAllAgentsAsync().Result;
        var agentsCopy = allAgents.ToDictionary(a => a.Id, a => a);
        var taskQueueCopy = new Queue<TaskRequest>(_taskQueue);
        var repositories = _sessionDiscovery.GroupAgentsByRepository(allAgents);
        var state = new OrchestratorState(agentsCopy, taskQueueCopy, DateTime.Now, repositories);
        var json = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        // Retry mechanism for file access conflicts
        var maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                File.WriteAllText(_stateFilePath, json);
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Thread.Sleep(10 * (i + 1)); // Exponential backoff: 10ms, 20ms, 30ms
            }
            catch (UnauthorizedAccessException) when (i < maxRetries - 1)
            {
                Thread.Sleep(10 * (i + 1)); // Exponential backoff for permission issues
            }
        }
    }

    private void LoadState()
    {
        if (File.Exists(_stateFilePath))
        {
            try
            {
                var json = File.ReadAllText(_stateFilePath);
                var state = System.Text.Json.JsonSerializer.Deserialize<OrchestratorState>(json);
                if (state != null)
                {
                    foreach (var agent in state.Agents)
                    {
                        _agentStateStore.RegisterAgentAsync(agent.Value).Wait();
                    }
                    foreach (var task in state.TaskQueue)
                    {
                        _taskQueue.Enqueue(task);
                    }
                }
            }
            catch
            {
                // Ignore errors, start fresh
            }
        }
    }

    public void Dispose()
    {
        // Clean up temporary test files
        if (IsRunningInTest() && File.Exists(_stateFilePath) && _stateFilePath.Contains("orchestrator-state-"))
        {
            try
            {
                File.Delete(_stateFilePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}