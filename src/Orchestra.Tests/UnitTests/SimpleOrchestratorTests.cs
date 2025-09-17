using Orchestra.Core;

namespace Orchestra.Tests.UnitTests;

public class SimpleOrchestratorTests
{
    private SimpleOrchestrator CreateOrchestrator()
    {
        var testFileName = $"test-state-unit-{Guid.NewGuid()}.json";
        return new SimpleOrchestrator(testFileName);
    }

    [Fact]
    public void RegisterAgent_ShouldAddAgentToCollection()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var agentId = "test-agent";
        var agentName = "Test Agent";
        var agentType = "claude-code";
        var repositoryPath = @"C:\TestRepo";

        // Act
        orchestrator.RegisterAgent(agentId, agentName, agentType, repositoryPath);

        // Assert
        var agents = orchestrator.GetAllAgents();
        var agent = agents.FirstOrDefault(a => a.Id == agentId);

        Assert.NotNull(agent);
        Assert.Equal(agentName, agent.Name);
        Assert.Equal(agentType, agent.Type);
        Assert.Equal(repositoryPath, agent.RepositoryPath);
        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    [Fact]
    public void UpdateAgentStatus_ShouldUpdateExistingAgent()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var agentId = "test-agent";
        orchestrator.RegisterAgent(agentId, "Test Agent", "claude-code", @"C:\TestRepo");

        // Act
        orchestrator.UpdateAgentStatus(agentId, AgentStatus.Working, "Running tests");

        // Assert
        var agents = orchestrator.GetAllAgents();
        var agent = agents.FirstOrDefault(a => a.Id == agentId);

        Assert.NotNull(agent);
        Assert.Equal(AgentStatus.Working, agent.Status);
        Assert.Equal("Running tests", agent.CurrentTask);
    }

    [Fact]
    public void QueueTask_ShouldAddTaskToQueue()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");

        // Act
        orchestrator.QueueTask("Test command", @"C:\TestRepo", TaskPriority.High);

        // Assert
        var state = orchestrator.GetCurrentState();
        Assert.True(state.TaskQueue.Count > 0);

        var task = state.TaskQueue.First();
        Assert.Equal("Test command", task.Command);
        Assert.Equal(@"C:\TestRepo", task.RepositoryPath);
        Assert.Equal(TaskPriority.High, task.Priority);
    }

    [Fact]
    public void GetNextTaskForAgent_ShouldReturnAssignedTask()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var agentId = "agent1";
        orchestrator.RegisterAgent(agentId, "Agent 1", "claude-code", @"C:\TestRepo");
        orchestrator.QueueTask("Test command", @"C:\TestRepo", TaskPriority.Normal);

        // Act
        var task = orchestrator.GetNextTaskForAgent(agentId);

        // Assert
        Assert.NotNull(task);
        Assert.Equal("Test command", task.Command);
        Assert.Equal(agentId, task.AgentId);
    }

    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Normal)]
    [InlineData(TaskPriority.High)]
    [InlineData(TaskPriority.Critical)]
    public void QueueTask_ShouldHandleAllPriorityLevels(TaskPriority priority)
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");

        // Act
        orchestrator.QueueTask("Test command", @"C:\TestRepo", priority);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        Assert.Equal(priority, task.Priority);
    }

    [Fact]
    public void TaskDistribution_ShouldPreferSameRepository()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\Repo1");
        orchestrator.RegisterAgent("agent2", "Agent 2", "claude-code", @"C:\Repo2");

        // Act
        orchestrator.QueueTask("Task for Repo1", @"C:\Repo1", TaskPriority.Normal);

        // Assert
        var taskForRepo1Agent = orchestrator.GetNextTaskForAgent("agent1");
        var taskForRepo2Agent = orchestrator.GetNextTaskForAgent("agent2");

        Assert.NotNull(taskForRepo1Agent);
        Assert.Null(taskForRepo2Agent);
        Assert.Equal("agent1", taskForRepo1Agent.AgentId);
    }

    [Fact]
    public void MultipleAgents_ShouldBeHandledCorrectly()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Act
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\Repo1");
        orchestrator.RegisterAgent("agent2", "Agent 2", "claude-code", @"C:\Repo2");
        orchestrator.RegisterAgent("agent3", "Agent 3", "github-copilot", @"C:\Repo3");

        // Assert
        var agents = orchestrator.GetAllAgents();
        Assert.Equal(3, agents.Count);

        var agentTypes = agents.Select(a => a.Type).Distinct().ToList();
        Assert.Contains("claude-code", agentTypes);
        Assert.Contains("github-copilot", agentTypes);
    }

    [Fact]
    public void GetCurrentState_ShouldReturnValidState()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");

        // Act
        var state = orchestrator.GetCurrentState();

        // Assert
        Assert.NotNull(state);
        Assert.True(state.Agents.Count > 0);
        Assert.True(state.LastUpdate <= DateTime.Now);
        Assert.NotNull(state.TaskQueue);
    }
}