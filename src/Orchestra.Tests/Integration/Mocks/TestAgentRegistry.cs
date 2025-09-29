using Orchestra.Core.Data.Entities;
using Orchestra.Tests.Integration.Mocks;

namespace Orchestra.Tests.Integration.Mocks;

/// <summary>
/// Registry for managing test agents in integration tests
/// Provides centralized control over agent behavior and state
/// </summary>
public class TestAgentRegistry
{
    private readonly Dictionary<string, TestAgent> _agents;
    private readonly MockAgentExecutor _mockExecutor;

    public TestAgentRegistry(MockAgentExecutor mockExecutor)
    {
        _agents = new Dictionary<string, TestAgent>();
        _mockExecutor = mockExecutor ?? throw new ArgumentNullException(nameof(mockExecutor));
    }

    /// <summary>
    /// Registers a new test agent with specified behavior
    /// </summary>
    public TestAgent RegisterTestAgent(
        string agentId,
        string name,
        string repositoryPath,
        AgentBehavior behavior = AgentBehavior.Normal)
    {
        var agent = new TestAgent
        {
            Id = agentId,
            Name = name,
            RepositoryPath = repositoryPath,
            Behavior = behavior,
            Status = AgentStatus.Idle,
            CreatedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow
        };

        _agents[agentId] = agent;
        _mockExecutor.SetAgentBehavior(agentId, behavior);
        _mockExecutor.RegisterAgent(agentId, repositoryPath);

        return agent;
    }

    /// <summary>
    /// Updates agent behavior during test execution
    /// </summary>
    public void SetAgentBehavior(string agentId, AgentBehavior behavior)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.Behavior = behavior;
            _mockExecutor.SetAgentBehavior(agentId, behavior);
        }
    }

    /// <summary>
    /// Simulates agent going offline
    /// </summary>
    public void SimulateAgentOffline(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.Status = AgentStatus.Offline;
            agent.LastPing = DateTime.UtcNow.AddMinutes(-10); // Simulate old ping
        }
    }

    /// <summary>
    /// Simulates agent failure
    /// </summary>
    public void SimulateAgentFailure(string agentId)
    {
        SetAgentBehavior(agentId, AgentBehavior.Error);
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.Status = AgentStatus.Error;
        }
    }

    /// <summary>
    /// Simulates agent timeout
    /// </summary>
    public void SimulateAgentTimeout(string agentId)
    {
        SetAgentBehavior(agentId, AgentBehavior.Timeout);
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.Status = AgentStatus.Busy; // Busy but not responding
        }
    }

    /// <summary>
    /// Gets agent by ID
    /// </summary>
    public TestAgent? GetAgent(string agentId)
    {
        return _agents.TryGetValue(agentId, out var agent) ? agent : null;
    }

    /// <summary>
    /// Gets all registered agents
    /// </summary>
    public IEnumerable<TestAgent> GetAllAgents()
    {
        return _agents.Values.ToList();
    }

    /// <summary>
    /// Gets agents by repository path
    /// </summary>
    public IEnumerable<TestAgent> GetAgentsByRepository(string repositoryPath)
    {
        return _agents.Values
            .Where(a => string.Equals(a.RepositoryPath, repositoryPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Gets available (idle) agents
    /// </summary>
    public IEnumerable<TestAgent> GetAvailableAgents()
    {
        return _agents.Values
            .Where(a => a.Status == AgentStatus.Idle && a.Behavior != AgentBehavior.Error)
            .ToList();
    }

    /// <summary>
    /// Resets all agents to normal behavior and idle status
    /// </summary>
    public void ResetAllAgents()
    {
        foreach (var agent in _agents.Values)
        {
            agent.Status = AgentStatus.Idle;
            agent.Behavior = AgentBehavior.Normal;
            agent.LastPing = DateTime.UtcNow;
            _mockExecutor.SetAgentBehavior(agent.Id, AgentBehavior.Normal);
        }
    }

    /// <summary>
    /// Removes all agents from registry
    /// </summary>
    public void Clear()
    {
        _agents.Clear();
    }

    /// <summary>
    /// Creates a set of standard test agents for common scenarios
    /// </summary>
    public void CreateStandardTestAgents()
    {
        RegisterTestAgent(
            "test-agent-1",
            "Test Agent 1",
            @"C:\TestRepo1",
            AgentBehavior.Normal
        );

        RegisterTestAgent(
            "test-agent-2",
            "Test Agent 2",
            @"C:\TestRepo2",
            AgentBehavior.Normal
        );

        RegisterTestAgent(
            "slow-agent",
            "Slow Test Agent",
            @"C:\TestRepo1",
            AgentBehavior.Slow
        );
    }
}

/// <summary>
/// Represents a test agent with configurable behavior
/// </summary>
public class TestAgent
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string RepositoryPath { get; set; } = "";
    public AgentBehavior Behavior { get; set; } = AgentBehavior.Normal;
    public AgentStatus Status { get; set; } = AgentStatus.Idle;
    public DateTime CreatedAt { get; set; }
    public DateTime LastPing { get; set; }
    public string? CurrentTask { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}