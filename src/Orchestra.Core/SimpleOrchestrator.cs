using Orchestra.Core.Models;
using TaskStatus = Orchestra.Core.Models.TaskStatus;
using TaskPriority = Orchestra.Core.Models.TaskPriority;

namespace Orchestra.Core;

public class SimpleOrchestrator : IDisposable
{
    private readonly Dictionary<string, AgentInfo> _agents = new();
    private readonly Queue<TaskRequest> _taskQueue = new();
    private readonly string _stateFilePath;
    private readonly ClaudeSessionDiscovery _sessionDiscovery;
    private readonly object _lock = new();

    public SimpleOrchestrator(string stateFilePath = "orchestrator-state.json")
    {
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

    public void RegisterAgent(string id, string name, string type, string repositoryPath)
    {
        lock (_lock)
        {
            var agent = new AgentInfo(id, name, type, repositoryPath, AgentStatus.Idle, DateTime.Now);
            _agents[id] = agent;
            SaveState();
        }
    }

    public void UpdateAgentStatus(string agentId, AgentStatus status, string? currentTask = null)
    {
        lock (_lock)
        {
            if (!_agents.TryGetValue(agentId, out var agent))
            {
                return;
            }

            _agents[agentId] = agent with
            {
                Status = status,
                LastPing = DateTime.Now,
                CurrentTask = currentTask
            };
            SaveState();
        }
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
            var agent = _agents.GetValueOrDefault(agentId);
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

    public List<AgentInfo> GetAllAgents()
    {
        lock (_lock)
        {
            return _agents.Values.ToList();
        }
    }

    public void RefreshAgents()
    {
        lock (_lock)
        {
            var discoveredAgents = _sessionDiscovery.DiscoverActiveSessions();

            // Clear current agents and add discovered ones
            _agents.Clear();
            foreach (var agent in discoveredAgents)
            {
                _agents[agent.Id] = agent;
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
            return _sessionDiscovery.GroupAgentsByRepository(_agents.Values.ToList());
        }
    }

    public OrchestratorState GetCurrentState()
    {
        lock (_lock)
        {
            var repositories = _sessionDiscovery.GroupAgentsByRepository(_agents.Values.ToList());
            return new OrchestratorState(new Dictionary<string, AgentInfo>(_agents), new Queue<TaskRequest>(_taskQueue), DateTime.Now, repositories);
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
        return _agents.Values
            .Where(a => a.Status == AgentStatus.Idle && a.RepositoryPath == repositoryPath)
            .OrderBy(a => a.LastPing)
            .FirstOrDefault()
            ?? _agents.Values
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
            return _agents.GetValueOrDefault(agentId);
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

    private void SaveState()
    {
        // Create thread-safe copies for serialization
        var agentsCopy = new Dictionary<string, AgentInfo>(_agents);
        var taskQueueCopy = new Queue<TaskRequest>(_taskQueue);
        var repositories = _sessionDiscovery.GroupAgentsByRepository(agentsCopy.Values.ToList());
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
                        _agents[agent.Key] = agent.Value;
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