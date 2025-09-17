using Orchestra.Core.Models;

namespace Orchestra.Core;

public class SimpleOrchestrator
{
    private readonly Dictionary<string, AgentInfo> _agents = new();
    private readonly Queue<TaskRequest> _taskQueue = new();
    private readonly string _stateFilePath;
    private readonly ClaudeSessionDiscovery _sessionDiscovery;

    public SimpleOrchestrator(string stateFilePath = "orchestrator-state.json")
    {
        _stateFilePath = stateFilePath;
        _sessionDiscovery = new ClaudeSessionDiscovery();
        LoadState();
    }

    public void RegisterAgent(string id, string name, string type, string repositoryPath)
    {
        var agent = new AgentInfo(id, name, type, repositoryPath, AgentStatus.Idle, DateTime.Now);
        _agents[id] = agent;
        SaveState();
    }

    public void UpdateAgentStatus(string agentId, AgentStatus status, string? currentTask = null)
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

    public void QueueTask(string command, string repositoryPath, TaskPriority priority = TaskPriority.Normal)
    {
        var availableAgent = FindAvailableAgent(repositoryPath);
        var agentId = availableAgent?.Id ?? ""; // Empty if no agent available yet

        var task = new TaskRequest(
            Guid.NewGuid().ToString(),
            agentId,
            command,
            repositoryPath,
            DateTime.Now,
            priority
        );

        _taskQueue.Enqueue(task);
        SaveState();
    }

    public TaskRequest? GetNextTaskForAgent(string agentId)
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

    private void RemoveTaskFromQueue(string taskId)
    {
        var newQueue = new Queue<TaskRequest>(_taskQueue.Where(t => t.Id != taskId));
        _taskQueue.Clear();
        foreach (var t in newQueue) _taskQueue.Enqueue(t);
        SaveState();
    }

    public List<AgentInfo> GetAllAgents() => _agents.Values.ToList();

    public void RefreshAgents()
    {
        var discoveredAgents = _sessionDiscovery.DiscoverActiveSessions();

        // Clear current agents and add discovered ones
        _agents.Clear();
        foreach (var agent in discoveredAgents)
        {
            _agents[agent.Id] = agent;
        }

        SaveState();
    }

    public Dictionary<string, RepositoryInfo> GetRepositories()
    {
        RefreshAgents();
        return _sessionDiscovery.GroupAgentsByRepository(_agents.Values.ToList());
    }

    public OrchestratorState GetCurrentState()
    {
        var repositories = _sessionDiscovery.GroupAgentsByRepository(_agents.Values.ToList());
        return new OrchestratorState(_agents, _taskQueue, DateTime.Now, repositories);
    }

    public List<AgentHistoryEntry> GetAgentHistory(string sessionId, int maxEntries = 50)
    {
        return _sessionDiscovery.GetAgentHistory(sessionId, maxEntries);
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

    private void SaveState()
    {
        var repositories = _sessionDiscovery.GroupAgentsByRepository(_agents.Values.ToList());
        var state = new OrchestratorState(_agents, _taskQueue, DateTime.Now, repositories);
        var json = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_stateFilePath, json);
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
}