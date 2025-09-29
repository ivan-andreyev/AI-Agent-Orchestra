using System.Collections.Concurrent;
using Orchestra.Core.Abstractions;
using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Services;

/// <summary>
/// In-memory реализация хранилища состояния агентов.
/// Используется как основная реализация для production, обеспечивает быструю работу.
/// При использовании как Scoped сервис автоматически изолируется между HTTP запросами/тестами.
/// </summary>
public class InMemoryAgentStateStore : IAgentStateStore
{
    private readonly ConcurrentDictionary<string, AgentInfo> _agents = new();
    private readonly object _lock = new();

    public Task<AgentInfo?> GetAgentAsync(string id)
    {
        _agents.TryGetValue(id, out var agent);
        return Task.FromResult(agent);
    }

    public Task<List<AgentInfo>> GetAllAgentsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_agents.Values.ToList());
        }
    }

    public virtual Task<bool> RegisterAgentAsync(AgentInfo agent)
    {
        if (agent == null || string.IsNullOrEmpty(agent.Id))
        {
            return Task.FromResult(false);
        }

        lock (_lock)
        {
            _agents[agent.Id] = agent;
            return Task.FromResult(true);
        }
    }

    public Task<bool> UpdateAgentAsync(AgentInfo agent)
    {
        if (agent == null || string.IsNullOrEmpty(agent.Id))
        {
            return Task.FromResult(false);
        }

        lock (_lock)
        {
            if (_agents.ContainsKey(agent.Id))
            {
                _agents[agent.Id] = agent;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task<bool> UpdateAgentStatusAsync(string agentId, AgentStatus status, string? currentTask = null)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            return Task.FromResult(false);
        }

        lock (_lock)
        {
            if (_agents.TryGetValue(agentId, out var agent))
            {
                // Create new AgentInfo instance since records have init-only properties
                var updatedAgent = agent with
                {
                    Status = status,
                    LastPing = DateTime.UtcNow,
                    CurrentTask = currentTask ?? agent.CurrentTask
                };

                _agents[agentId] = updatedAgent;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task<List<AgentInfo>> FindAvailableAgentsAsync(string repositoryPath)
    {
        lock (_lock)
        {
            var availableAgents = _agents.Values
                .Where(agent => agent.Status == AgentStatus.Idle || agent.Status == AgentStatus.Busy)
                .Where(agent => string.IsNullOrEmpty(repositoryPath) ||
                               IsRepositoryPathMatch(agent.RepositoryPath, repositoryPath))
                .ToList();

            return Task.FromResult(availableAgents);
        }
    }

    public Task<AgentInfo?> FindBestAgentAsync(string repositoryPath)
    {
        lock (_lock)
        {
            var bestAgent = _agents.Values
                .Where(agent => agent.Status == AgentStatus.Idle || agent.Status == AgentStatus.Busy)
                .Where(agent => string.IsNullOrEmpty(repositoryPath) ||
                               IsRepositoryPathMatch(agent.RepositoryPath, repositoryPath))
                .Where(agent => agent.Type == "claude-code") // Предпочитаем Claude Code агентов
                .OrderBy(agent => agent.LastPing) // Выбираем наименее недавно использованного
                .FirstOrDefault();

            return Task.FromResult(bestAgent);
        }
    }

    public Task ClearAllAgentsAsync()
    {
        lock (_lock)
        {
            _agents.Clear();
            return Task.CompletedTask;
        }
    }

    public Task<bool> IsClaudeCodeAgentAsync(string agentId)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            return Task.FromResult(false);
        }

        var isClaudeCode = _agents.TryGetValue(agentId, out var agent) &&
                          agent.Type == "claude-code";

        return Task.FromResult(isClaudeCode);
    }

    public Task<List<AgentInfo>> GetClaudeCodeAgentsAsync()
    {
        lock (_lock)
        {
            var claudeCodeAgents = _agents.Values
                .Where(agent => agent.Type == "claude-code")
                .ToList();

            return Task.FromResult(claudeCodeAgents);
        }
    }

    /// <summary>
    /// Проверяет соответствие пути репозитория с учетом различных форматов путей
    /// </summary>
    private static bool IsRepositoryPathMatch(string agentRepositoryPath, string requestedRepositoryPath)
    {
        if (string.IsNullOrEmpty(agentRepositoryPath) || string.IsNullOrEmpty(requestedRepositoryPath))
        {
            return false;
        }

        // Нормализуем пути для сравнения
        var normalizedAgentPath = agentRepositoryPath.Replace('\\', '/').TrimEnd('/');
        var normalizedRequestedPath = requestedRepositoryPath.Replace('\\', '/').TrimEnd('/');

        return normalizedAgentPath.Equals(normalizedRequestedPath, StringComparison.OrdinalIgnoreCase);
    }
}