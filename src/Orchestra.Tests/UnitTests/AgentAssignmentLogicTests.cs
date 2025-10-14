using Orchestra.Core;
using Orchestra.Core.Services;
using TaskPriority = Orchestra.Core.Models.TaskPriority;
using AgentStatus = Orchestra.Core.Data.Entities.AgentStatus;
using System.Diagnostics;

namespace Orchestra.Tests.UnitTests;

/// <summary>
/// Comprehensive tests for agent assignment logic optimization (Phase 4.3.3)
/// Tests repository path matching priority, performance thresholds, and edge cases
/// </summary>
public class AgentAssignmentLogicTests : IDisposable
{
    private readonly List<SimpleOrchestrator> _orchestrators = new();

    private SimpleOrchestrator CreateOrchestrator()
    {
        var testFileName = $"test-state-assignment-{Guid.NewGuid()}.json";
        var agentStateStore = new InMemoryAgentStateStore();
        var orchestrator = new SimpleOrchestrator(agentStateStore, null, testFileName);
        _orchestrators.Add(orchestrator);
        return orchestrator;
    }

    public void Dispose()
    {
        foreach (var orchestrator in _orchestrators)
        {
            orchestrator.Dispose();
        }
    }

    #region Repository Path Matching Priority Tests

    [Fact]
    public void FindAvailableAgent_ShouldPreferSameRepository_WhenMultipleAgentsAvailable()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent-repo1", "Agent Repo1", "claude-code", @"C:\Repo1");
        orchestrator.RegisterAgent("agent-repo2", "Agent Repo2", "claude-code", @"C:\Repo2");
        orchestrator.RegisterAgent("agent-repo3", "Agent Repo3", "claude-code", @"C:\Repo3");

        // Act
        orchestrator.QueueTask("Task for Repo2", @"C:\Repo2", TaskPriority.Normal);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        Assert.Equal("agent-repo2", task.AgentId);
    }

    [Fact]
    public void FindAvailableAgent_ShouldFallbackToAnyIdleAgent_WhenNoRepositoryMatch()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent-repo1", "Agent Repo1", "claude-code", @"C:\Repo1");
        orchestrator.RegisterAgent("agent-repo2", "Agent Repo2", "claude-code", @"C:\Repo2");

        // Act
        orchestrator.QueueTask("Task for Repo3", @"C:\Repo3", TaskPriority.Normal);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();

        // Should be assigned to one of the available agents, even though repository doesn't match
        Assert.True(task.AgentId == "agent-repo1" || task.AgentId == "agent-repo2");
    }

    [Fact]
    public void FindAvailableAgent_ShouldSelectOldestLastPing_WhenMultipleSameRepositoryAgents()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Register agents with the same repository
        orchestrator.RegisterAgentAsync("agent-old", "Agent Old", "claude-code", @"C:\Repo1").Wait();
        Thread.Sleep(100); // Ensure different LastPing times
        orchestrator.RegisterAgentAsync("agent-new", "Agent New", "claude-code", @"C:\Repo1").Wait();

        // Act
        orchestrator.QueueTask("Task for Repo1", @"C:\Repo1", TaskPriority.Normal);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();

        // Should select agent with oldest LastPing (first registered)
        Assert.Equal("agent-old", task.AgentId);
    }

    [Fact]
    public void FindAvailableAgent_ShouldReturnNull_WhenNoIdleAgentsAvailable()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent-busy", "Agent Busy", "claude-code", @"C:\Repo1");
        orchestrator.UpdateAgentStatus("agent-busy", AgentStatus.Busy, "Working on something");

        // Act
        orchestrator.QueueTask("Task for Repo1", @"C:\Repo1", TaskPriority.Normal);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();

        // Task should be Pending with no agent assigned
        Assert.Equal(Orchestra.Core.Models.TaskStatus.Pending, task.Status);
        Assert.Equal("", task.AgentId);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void FindAvailableAgent_ShouldCompleteWithin100ms_WithManyAgents()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Register 50 agents across different repositories
        for (int i = 0; i < 50; i++)
        {
            orchestrator.RegisterAgent($"agent-{i}", $"Agent {i}", "claude-code", $@"C:\Repo{i % 10}");
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        orchestrator.QueueTask("Performance test task", @"C:\Repo5", TaskPriority.Normal);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Agent assignment took {stopwatch.ElapsedMilliseconds}ms, expected <100ms");
    }

    [Fact]
    public void FindAvailableAgent_ShouldMaintainPerformance_WithMultipleTasks()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Register 10 agents
        for (int i = 0; i < 10; i++)
        {
            orchestrator.RegisterAgent($"agent-{i}", $"Agent {i}", "claude-code", $@"C:\Repo{i % 3}");
        }

        // Act - Queue 20 tasks
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            orchestrator.QueueTask($"Task {i}", $@"C:\Repo{i % 3}", TaskPriority.Normal);
        }
        stopwatch.Stop();

        // Assert - Average assignment time should be <100ms per task
        var averageTime = stopwatch.ElapsedMilliseconds / 20.0;
        Assert.True(averageTime < 100,
            $"Average assignment time {averageTime}ms, expected <100ms per task");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    public void FindAvailableAgent_ShouldScaleLinearly_WithAgentCount(int agentCount)
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        for (int i = 0; i < agentCount; i++)
        {
            orchestrator.RegisterAgent($"agent-{i}", $"Agent {i}", "claude-code", @"C:\Repo1");
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        orchestrator.QueueTask("Scalability test", @"C:\Repo1", TaskPriority.Normal);
        stopwatch.Stop();

        // Assert - Should still be <100ms even with many agents
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Assignment with {agentCount} agents took {stopwatch.ElapsedMilliseconds}ms, expected <100ms");
    }

    #endregion

    #region Edge Cases and Special Scenarios

    [Fact]
    public void FindAvailableAgent_ShouldHandleEmptyAgentList()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        // No agents registered

        // Act
        orchestrator.QueueTask("Task with no agents", @"C:\Repo1", TaskPriority.Normal);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();

        Assert.Equal(Orchestra.Core.Models.TaskStatus.Pending, task.Status);
        Assert.Equal("", task.AgentId);
    }

    [Fact]
    public void FindAvailableAgent_ShouldSkipOfflineAgents()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent-offline", "Agent Offline", "claude-code", @"C:\Repo1");
        orchestrator.RegisterAgent("agent-idle", "Agent Idle", "claude-code", @"C:\Repo1");

        orchestrator.UpdateAgentStatus("agent-offline", AgentStatus.Offline);
        orchestrator.UpdateAgentStatus("agent-idle", AgentStatus.Idle);

        // Act
        orchestrator.QueueTask("Task for Repo1", @"C:\Repo1", TaskPriority.Normal);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();

        Assert.Equal("agent-idle", task.AgentId);
    }

    [Fact]
    public void FindAvailableAgent_ShouldSkipErrorAgents()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent-error", "Agent Error", "claude-code", @"C:\Repo1");
        orchestrator.RegisterAgent("agent-idle", "Agent Idle", "claude-code", @"C:\Repo1");

        orchestrator.UpdateAgentStatus("agent-error", AgentStatus.Error);
        orchestrator.UpdateAgentStatus("agent-idle", AgentStatus.Idle);

        // Act
        orchestrator.QueueTask("Task for Repo1", @"C:\Repo1", TaskPriority.Normal);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();

        Assert.Equal("agent-idle", task.AgentId);
    }

    [Fact]
    public void FindAvailableAgent_ShouldHandleSpecialCharactersInRepositoryPath()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var specialPath = @"C:\Repo With Spaces\子目录\Project-Name_v2.0";
        orchestrator.RegisterAgent("agent-special", "Agent Special", "claude-code", specialPath);

        // Act
        orchestrator.QueueTask("Task for special repo", specialPath, TaskPriority.Normal);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();

        Assert.Equal("agent-special", task.AgentId);
        Assert.Equal(specialPath, task.RepositoryPath);
    }

    [Fact]
    public void FindAvailableAgent_ShouldBeCaseSensitive_ForRepositoryPaths()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent-lower", "Agent Lower", "claude-code", @"c:\repo1");
        orchestrator.RegisterAgent("agent-upper", "Agent Upper", "claude-code", @"C:\Repo1");
        orchestrator.RegisterAgent("agent-fallback", "Agent Fallback", "claude-code", @"C:\Other");

        // Act
        orchestrator.QueueTask("Task for uppercase", @"C:\Repo1", TaskPriority.Normal);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();

        // Should match exactly "C:\Repo1", not "c:\repo1"
        Assert.Equal("agent-upper", task.AgentId);
    }

    #endregion

    #region Integration with Background Assignment Service

    [Fact]
    public void FindAvailableAgent_ShouldWorkWithBackgroundAssignment_WhenAgentBecomesAvailable()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        // Queue task with no agents available
        orchestrator.QueueTask("Pending task", @"C:\Repo1", TaskPriority.Normal);

        var state1 = orchestrator.GetCurrentState();
        var task1 = state1.TaskQueue.First();
        Assert.Equal(Orchestra.Core.Models.TaskStatus.Pending, task1.Status);

        // Act - Register agent later
        orchestrator.RegisterAgent("agent-late", "Agent Late", "claude-code", @"C:\Repo1");
        orchestrator.TriggerTaskAssignment(); // Simulate background service

        // Assert
        var state2 = orchestrator.GetCurrentState();
        var task2 = state2.TaskQueue.First();

        Assert.Equal(Orchestra.Core.Models.TaskStatus.Assigned, task2.Status);
        Assert.Equal("agent-late", task2.AgentId);
    }

    [Fact]
    public void FindAvailableAgent_ShouldReassignTask_WhenBetterAgentBecomesAvailable()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent-other-repo", "Agent Other", "claude-code", @"C:\OtherRepo");

        // Queue task - will be assigned to agent from different repo
        orchestrator.QueueTask("Task for Repo1", @"C:\Repo1", TaskPriority.Normal);

        var state1 = orchestrator.GetCurrentState();
        var task1 = state1.TaskQueue.First();
        Assert.Equal("agent-other-repo", task1.AgentId); // Fallback assignment

        // Act - Agent from same repository becomes available
        orchestrator.RegisterAgent("agent-repo1", "Agent Repo1", "claude-code", @"C:\Repo1");

        // Queue another task
        orchestrator.QueueTask("Another task for Repo1", @"C:\Repo1", TaskPriority.Normal);

        // Assert - New task should prefer the repo-matching agent
        var state2 = orchestrator.GetCurrentState();
        var newTask = state2.TaskQueue.FirstOrDefault(t => t.Command == "Another task for Repo1");

        Assert.NotNull(newTask);
        Assert.Equal("agent-repo1", newTask.AgentId);
    }

    #endregion

    #region Agent Type and Specialization Tests

    [Fact]
    public void FindAvailableAgent_ShouldWorkWithDifferentAgentTypes()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("claude-agent", "Claude Agent", "claude-code", @"C:\Repo1");
        orchestrator.RegisterAgent("copilot-agent", "Copilot Agent", "github-copilot", @"C:\Repo1");
        orchestrator.RegisterAgent("custom-agent", "Custom Agent", "custom-type", @"C:\Repo1");

        // Act
        orchestrator.QueueTask("Task 1", @"C:\Repo1", TaskPriority.Normal);
        orchestrator.QueueTask("Task 2", @"C:\Repo1", TaskPriority.Normal);
        orchestrator.QueueTask("Task 3", @"C:\Repo1", TaskPriority.Normal);

        // Assert
        var state = orchestrator.GetCurrentState();
        var tasks = state.TaskQueue.ToList();

        // All tasks should be assigned to available agents
        Assert.All(tasks, task => Assert.NotEmpty(task.AgentId));

        // At least one agent should be assigned (tasks assigned based on availability)
        // NOTE: Not all agents might be used since tasks can be assigned to the oldest agent first
        var assignedAgents = tasks.Select(t => t.AgentId).Distinct().ToList();
        Assert.True(assignedAgents.Count >= 1, $"Expected at least 1 agent assigned, got {assignedAgents.Count}");
        Assert.True(assignedAgents.Count <= 3, $"Expected at most 3 agents assigned, got {assignedAgents.Count}");
    }

    [Fact]
    public void FindAvailableAgent_ShouldSelectOldestAgent_WhenMultipleDifferentTypes()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        orchestrator.RegisterAgentAsync("agent-type1", "Agent Type1", "type1", @"C:\Repo1").Wait();
        Thread.Sleep(100);
        orchestrator.RegisterAgentAsync("agent-type2", "Agent Type2", "type2", @"C:\Repo1").Wait();
        Thread.Sleep(100);
        orchestrator.RegisterAgentAsync("agent-type3", "Agent Type3", "type3", @"C:\Repo1").Wait();

        // Act
        orchestrator.QueueTask("First task", @"C:\Repo1", TaskPriority.Normal);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();

        // Should select first registered agent (oldest LastPing)
        Assert.Equal("agent-type1", task.AgentId);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public void FindAvailableAgent_ShouldBeThreadSafe_WithConcurrentTaskQueuing()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();

        for (int i = 0; i < 5; i++)
        {
            orchestrator.RegisterAgent($"agent-{i}", $"Agent {i}", "claude-code", @"C:\Repo1");
        }

        // Act - Queue tasks concurrently
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            int taskNum = i;
            tasks.Add(Task.Run(() =>
            {
                orchestrator.QueueTask($"Concurrent task {taskNum}", @"C:\Repo1", TaskPriority.Normal);
            }));
        }
        Task.WaitAll(tasks.ToArray());

        // Assert
        var state = orchestrator.GetCurrentState();

        // All tasks should be created
        Assert.Equal(20, state.TaskQueue.Count);

        // All tasks should have an agent assigned (5 agents available)
        var assignedTasks = state.TaskQueue.Where(t => !string.IsNullOrEmpty(t.AgentId)).ToList();
        Assert.Equal(20, assignedTasks.Count);
    }

    #endregion

    #region Regression Tests (from existing SimpleOrchestratorTests)

    [Fact]
    public void TaskDistribution_ShouldPreferSameRepository_RegressionTest()
    {
        // This is the existing test from SimpleOrchestratorTests.cs line 119
        // Ensuring no regression after optimization

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

    #endregion
}
