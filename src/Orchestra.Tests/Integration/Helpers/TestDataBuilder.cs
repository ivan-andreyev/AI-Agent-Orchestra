using Orchestra.Core.Models;
using Orchestra.Tests.Integration.Mocks;
using TaskPriority = Orchestra.Core.Models.TaskPriority;

namespace Orchestra.Tests.Integration.Helpers;

/// <summary>
/// Builder pattern for creating test data and scenarios in integration tests.
/// Provides fluent API for setting up complex test scenarios.
/// </summary>
public class TestDataBuilder
{
    private readonly IntegrationTestBase _testBase;
    private readonly List<TestAgentDefinition> _agents = new();
    private readonly List<TestTaskDefinition> _tasks = new();

    public TestDataBuilder(IntegrationTestBase testBase)
    {
        _testBase = testBase ?? throw new ArgumentNullException(nameof(testBase));
    }

    /// <summary>
    /// Adds an agent to the test scenario
    /// </summary>
    public TestDataBuilder WithAgent(string id, string repositoryPath, AgentBehavior behavior = AgentBehavior.Normal)
    {
        _agents.Add(new TestAgentDefinition
        {
            Id = id,
            RepositoryPath = repositoryPath,
            Behavior = behavior
        });
        return this;
    }

    /// <summary>
    /// Adds multiple agents with sequential naming
    /// </summary>
    public TestDataBuilder WithAgents(int count, string baseRepositoryPath, AgentBehavior behavior = AgentBehavior.Normal)
    {
        for (int i = 1; i <= count; i++)
        {
            WithAgent($"test-agent-{i}", $"{baseRepositoryPath}{i}", behavior);
        }
        return this;
    }

    /// <summary>
    /// Adds a task to the test scenario
    /// </summary>
    public TestDataBuilder WithTask(string command, string repositoryPath, TaskPriority priority = TaskPriority.Normal)
    {
        _tasks.Add(new TestTaskDefinition
        {
            Command = command,
            RepositoryPath = repositoryPath,
            Priority = priority
        });
        return this;
    }

    /// <summary>
    /// Adds multiple tasks for the same repository
    /// </summary>
    public TestDataBuilder WithTasks(int count, string repositoryPath, string baseCommand, TaskPriority priority = TaskPriority.Normal)
    {
        for (int i = 1; i <= count; i++)
        {
            WithTask($"{baseCommand} {i}", repositoryPath, priority);
        }
        return this;
    }

    /// <summary>
    /// Adds tasks with mixed priorities
    /// </summary>
    public TestDataBuilder WithMixedPriorityTasks(string repositoryPath, string baseCommand)
    {
        WithTask($"{baseCommand} (High)", repositoryPath, TaskPriority.High);
        WithTask($"{baseCommand} (Normal)", repositoryPath, TaskPriority.Normal);
        WithTask($"{baseCommand} (Low)", repositoryPath, TaskPriority.Low);
        return this;
    }

    /// <summary>
    /// Adds failing tasks for error testing
    /// </summary>
    public TestDataBuilder WithFailingTasks(int count, string repositoryPath)
    {
        for (int i = 1; i <= count; i++)
        {
            WithTask($"fail 'Test failure {i}'", repositoryPath, TaskPriority.Normal);
        }
        return this;
    }

    /// <summary>
    /// Adds slow tasks for performance testing
    /// </summary>
    public TestDataBuilder WithSlowTasks(int count, string repositoryPath)
    {
        for (int i = 1; i <= count; i++)
        {
            WithTask($"slow 'Slow task {i}'", repositoryPath, TaskPriority.Normal);
        }
        return this;
    }

    /// <summary>
    /// Creates a load testing scenario
    /// </summary>
    public TestDataBuilder WithLoadTestScenario(int agentCount, int tasksPerAgent)
    {
        // Create agents
        for (int i = 1; i <= agentCount; i++)
        {
            WithAgent($"load-agent-{i}", $@"C:\LoadTest{i}", AgentBehavior.Normal);
        }

        // Create tasks distributed across agents
        for (int agent = 1; agent <= agentCount; agent++)
        {
            for (int task = 1; task <= tasksPerAgent; task++)
            {
                WithTask($"echo 'Load test Agent {agent} Task {task}'", $@"C:\LoadTest{agent}", TaskPriority.Normal);
            }
        }

        return this;
    }

    /// <summary>
    /// Creates a failover testing scenario
    /// </summary>
    public TestDataBuilder WithFailoverScenario(string repositoryPath)
    {
        WithAgent("primary-agent", repositoryPath, AgentBehavior.Normal);
        WithAgent("backup-agent", repositoryPath, AgentBehavior.Normal);

        // Add tasks that will test failover
        WithTasks(5, repositoryPath, "echo 'Failover test task'", TaskPriority.Normal);

        return this;
    }

    /// <summary>
    /// Creates agents and tasks, returns the created data
    /// </summary>
    public async Task<TestScenarioResult> BuildAsync()
    {
        var result = new TestScenarioResult();

        // Create agents
        foreach (var agentDef in _agents)
        {
            var agentId = await _testBase.CreateTestAgentAsync(agentDef.Id, agentDef.RepositoryPath, agentDef.Behavior);
            result.AgentIds.Add(agentId);
        }

        // Create tasks
        foreach (var taskDef in _tasks)
        {
            var taskId = await _testBase.QueueTestTaskAsync(taskDef.Command, taskDef.RepositoryPath, taskDef.Priority);
            result.TaskIds.Add(taskId);
        }

        return result;
    }

    /// <summary>
    /// Creates a quick test scenario with default values
    /// </summary>
    public static TestDataBuilder CreateQuickScenario(IntegrationTestBase testBase)
    {
        return new TestDataBuilder(testBase)
            .WithAgent("quick-agent", @"C:\QuickTest", AgentBehavior.Normal)
            .WithTask("echo 'Quick test'", @"C:\QuickTest", TaskPriority.Normal);
    }

    /// <summary>
    /// Creates a comprehensive test scenario
    /// </summary>
    public static TestDataBuilder CreateComprehensiveScenario(IntegrationTestBase testBase)
    {
        return new TestDataBuilder(testBase)
            .WithAgents(3, @"C:\ComprehensiveTest", AgentBehavior.Normal)
            .WithAgent("slow-agent", @"C:\ComprehensiveTest1", AgentBehavior.Slow)
            .WithAgent("failing-agent", @"C:\ComprehensiveTest2", AgentBehavior.Error)
            .WithMixedPriorityTasks(@"C:\ComprehensiveTest1", "echo 'Mixed priority'")
            .WithFailingTasks(2, @"C:\ComprehensiveTest2")
            .WithSlowTasks(1, @"C:\ComprehensiveTest3");
    }
}

/// <summary>
/// Definition of a test agent
/// </summary>
public class TestAgentDefinition
{
    public string Id { get; set; } = "";
    public string RepositoryPath { get; set; } = "";
    public AgentBehavior Behavior { get; set; } = AgentBehavior.Normal;
}

/// <summary>
/// Definition of a test task
/// </summary>
public class TestTaskDefinition
{
    public string Command { get; set; } = "";
    public string RepositoryPath { get; set; } = "";
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
}

/// <summary>
/// Result of building a test scenario
/// </summary>
public class TestScenarioResult
{
    public List<string> AgentIds { get; } = new();
    public List<string> TaskIds { get; } = new();

    public int AgentCount => AgentIds.Count;
    public int TaskCount => TaskIds.Count;
}

/// <summary>
/// Fluent extensions for test assertions
/// </summary>
public static class TestScenarioExtensions
{
    /// <summary>
    /// Waits for all tasks in the scenario to complete
    /// </summary>
    public static async Task<List<bool>> WaitForAllTasksAsync(this TestScenarioResult scenario,
        IntegrationTestBase testBase,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(60);

        var completionTasks = scenario.TaskIds
            .Select(id => testBase.WaitForTaskCompletionAsync(id, timeout))
            .ToArray();

        return (await Task.WhenAll(completionTasks)).ToList();
    }

    /// <summary>
    /// Gets all task records for the scenario
    /// </summary>
    public static async Task<List<Orchestra.Core.Data.Entities.TaskRecord?>> GetAllTasksAsync(this TestScenarioResult scenario,
        IntegrationTestBase testBase)
    {
        var taskQueries = scenario.TaskIds
            .Select(id => testBase.GetTaskAsync(id))
            .ToArray();

        return (await Task.WhenAll(taskQueries)).ToList();
    }

    /// <summary>
    /// Asserts that all tasks in the scenario completed successfully
    /// </summary>
    public static async Task AssertAllTasksCompletedAsync(this TestScenarioResult scenario,
        IntegrationTestBase testBase)
    {
        var completionResults = await scenario.WaitForAllTasksAsync(testBase);
        var tasks = await scenario.GetAllTasksAsync(testBase);

        Assert.True(completionResults.All(r => r), "All scenario tasks should complete");
        Assert.All(tasks, task =>
        {
            Assert.NotNull(task);
            Assert.True(task.Status == Orchestra.Core.Models.TaskStatus.Completed ||
                       task.Status == Orchestra.Core.Models.TaskStatus.Failed,
                       $"Task {task.Id} should be completed or failed, but was {task.Status}");
        });
    }
}