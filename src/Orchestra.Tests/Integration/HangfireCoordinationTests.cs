using Orchestra.Core.Models;
using Orchestra.Tests.Integration.Mocks;
using Xunit;
using Xunit.Abstractions;
using TaskPriority = Orchestra.Core.Models.TaskPriority;
using TaskStatus = Orchestra.Core.Models.TaskStatus;

namespace Orchestra.Tests.Integration;

/// <summary>
/// Comprehensive end-to-end integration tests for Hangfire coordination system.
/// This test validates the complete workflow from task submission through
/// TaskExecutionJob execution to completion with mock agent execution.
/// </summary>
[Collection("Integration")]
public class HangfireCoordinationTests : IntegrationTestBase, IClassFixture<TestWebApplicationFactory<Program>>
{
    public HangfireCoordinationTests(TestWebApplicationFactory<Program> factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    private async Task SetupTestEnvironmentAsync()
    {
        // Create test agents using IntegrationTestBase helper
        await CreateTestAgentAsync("claude-hangfire-1", @"C:\E2ETest-Hangfire-1", AgentBehavior.Normal);
        await CreateTestAgentAsync("claude-hangfire-2", @"C:\E2ETest-Hangfire-2", AgentBehavior.Normal);

        Output.WriteLine("Test environment setup completed - 2 agents registered");
    }

    [Fact]
    public async Task EndToEnd_TaskCreationToHangfireExecution_ShouldCompleteSuccessfully()
    {
        // SETUP: Register test agents
        await SetupTestEnvironmentAsync();

        // ARRANGE: Prepare test scenario
        var testCommand = "echo 'End-to-end Hangfire coordination test'";
        var testRepository = @"C:\E2ETest-Hangfire-1";

        Output.WriteLine($"Starting end-to-end test: {testCommand}");

        // ACT: Queue task using IntegrationTestBase helper
        var taskId = await QueueTestTaskAsync(testCommand, testRepository, TaskPriority.High);

        Assert.NotNull(taskId);

        // WAIT for task to be processed
        var completed = await WaitForTaskCompletionAsync(taskId, TimeSpan.FromSeconds(30));
        Assert.True(completed, "Task should complete successfully");

        // VERIFY: Task was executed and completed
        var finalState = await HangfireOrchestrator.GetCurrentStateAsync();
        Output.WriteLine($"Final state - Total agents: {finalState.Agents.Count}");

        // Verify agent processed the task
        var testAgent = finalState.Agents.Values.FirstOrDefault(a => a.Id == "claude-hangfire-1");
        Assert.NotNull(testAgent);

        Output.WriteLine($"Test agent status: {testAgent.Status}, LastPing: {testAgent.LastPing}");
    }

    [Fact]
    public async Task MultipleTasksWithDifferentAgents_ShouldCoordinateCorrectly()
    {
        // SETUP: Register test agents
        await SetupTestEnvironmentAsync();

        // ARRANGE: Prepare multiple tasks for different agents
        var tasks = new[]
        {
            new { Command = "echo 'Task 1 for Agent 1'", Repository = @"C:\E2ETest-Hangfire-1" },
            new { Command = "echo 'Task 2 for Agent 2'", Repository = @"C:\E2ETest-Hangfire-2" },
            new { Command = "echo 'Task 3 for Agent 1'", Repository = @"C:\E2ETest-Hangfire-1" }
        };

        Output.WriteLine("Starting multi-agent coordination test");

        // ACT: Queue all tasks simultaneously
        var taskIds = new List<string>();
        foreach (var task in tasks)
        {
            var taskId = await QueueTestTaskAsync(task.Command, task.Repository, TaskPriority.Normal);
            taskIds.Add(taskId);
        }

        // WAIT for all tasks to complete
        var completionTasks = taskIds.Select(id => WaitForTaskCompletionAsync(id, TimeSpan.FromSeconds(45))).ToArray();
        var results = await Task.WhenAll(completionTasks);

        // VERIFY: All tasks were processed
        Assert.True(results.All(r => r), "All tasks should complete successfully");

        var finalState = await HangfireOrchestrator.GetCurrentStateAsync();
        Assert.Equal(2, finalState.Agents.Count); // Should still have 2 agents
        Output.WriteLine("Multi-agent coordination test completed successfully");
    }

    [Fact]
    public async Task TaskRepository_Integration_ShouldTrackTaskLifecycle()
    {
        // SETUP: Register test agents
        await SetupTestEnvironmentAsync();

        // ARRANGE: Test TaskRepository integration
        var testCommand = "echo 'TaskRepository integration test'";
        var testRepository = @"C:\E2ETest-Hangfire-1";

        Output.WriteLine("Starting TaskRepository integration test");

        // ACT: Queue task using helper
        var taskId = await QueueTestTaskAsync(testCommand, testRepository, TaskPriority.High);

        // Verify task exists in database before execution
        var initialTask = await GetTaskAsync(taskId);
        Assert.NotNull(initialTask);
        Assert.Equal(TaskStatus.Pending, initialTask.Status);
        Output.WriteLine($"Task created in database: {initialTask.Id}, Status: {initialTask.Status}");

        // WAIT for task completion
        var completed = await WaitForTaskCompletionAsync(taskId, TimeSpan.FromSeconds(30));
        Assert.True(completed, "Task should complete successfully");

        // VERIFY: Task status updated in database
        var finalTask = await GetTaskAsync(taskId);
        Assert.NotNull(finalTask);
        Output.WriteLine($"Final task status in database: {finalTask.Status}");
        Output.WriteLine($"Task result: {finalTask.Result ?? "No result"}");

        // Task should be completed or at least no longer pending
        Assert.NotEqual(TaskStatus.Pending, finalTask.Status);
    }

    [Fact]
    public async Task TaskRepository_DatabaseIntegration_ShouldPersistCorrectly()
    {
        // SETUP: Register test agents
        await SetupTestEnvironmentAsync();

        // ARRANGE: Test database persistence of tasks
        var testCommand = "echo 'Database persistence test'";
        var testRepository = @"C:\E2ETest-Hangfire-1";

        Output.WriteLine("Starting database persistence test");

        // ACT: Queue task using helper method
        var taskId = await QueueTestTaskAsync(testCommand, testRepository, TaskPriority.High);

        // Wait for task to be processed
        var completed = await WaitForTaskCompletionAsync(taskId, TimeSpan.FromSeconds(30));
        Assert.True(completed, "Task should be processed successfully");

        // VERIFY: Task exists in database with correct status
        var dbTask = await GetTaskAsync(taskId);
        Assert.NotNull(dbTask);
        Output.WriteLine($"Database task found: {dbTask.Id}, Status: {dbTask.Status}");

        // Task should not be in pending state (should be processed by Hangfire)
        Assert.NotEqual(TaskStatus.Pending, dbTask.Status);
    }

    [Fact]
    public async Task PriorityQueue_Processing_ShouldHandleCorrectly()
    {
        // SETUP: Register test agents
        await SetupTestEnvironmentAsync();

        // ARRANGE: Queue tasks with different priorities
        var highPriorityTask = "echo 'HIGH PRIORITY TASK'";
        var normalPriorityTask = "echo 'Normal priority task'";
        var lowPriorityTask = "echo 'Low priority task'";
        var testRepository = @"C:\E2ETest-Hangfire-1";

        Output.WriteLine("Starting priority queue processing test");

        // ACT: Queue tasks in reverse priority order (low, normal, high)
        var lowTaskId = await QueueTestTaskAsync(lowPriorityTask, testRepository, TaskPriority.Low);
        var normalTaskId = await QueueTestTaskAsync(normalPriorityTask, testRepository, TaskPriority.Normal);
        var highTaskId = await QueueTestTaskAsync(highPriorityTask, testRepository, TaskPriority.High);

        Output.WriteLine($"Queued tasks - Low: {lowTaskId}, Normal: {normalTaskId}, High: {highTaskId}");

        // WAIT for all tasks to complete
        var completionResults = await Task.WhenAll(
            WaitForTaskCompletionAsync(lowTaskId, TimeSpan.FromSeconds(45)),
            WaitForTaskCompletionAsync(normalTaskId, TimeSpan.FromSeconds(45)),
            WaitForTaskCompletionAsync(highTaskId, TimeSpan.FromSeconds(45))
        );

        // VERIFY: All tasks were processed
        Assert.True(completionResults.All(r => r), "All priority tasks should complete successfully");

        var dbTasks = new[]
        {
            await GetTaskAsync(lowTaskId),
            await GetTaskAsync(normalTaskId),
            await GetTaskAsync(highTaskId)
        };

        Assert.All(dbTasks, task => Assert.NotNull(task));
        Assert.Equal(3, dbTasks.Length);

        foreach (var task in dbTasks)
        {
            Output.WriteLine($"Task {task!.Id} final status: {task.Status}");
        }
    }

    // Removed custom WaitForTaskCompletion method - using IntegrationTestBase.WaitForTaskCompletionAsync instead
}

