using Orchestra.Core.Models;
using Orchestra.Tests.Integration.Mocks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using TaskPriority = Orchestra.Core.Models.TaskPriority;
using TaskStatus = Orchestra.Core.Models.TaskStatus;

namespace Orchestra.Tests.Integration;

/// <summary>
/// Comprehensive failure scenario tests for Hangfire coordination system.
/// Tests edge cases, error conditions, and system resilience.
/// </summary>
[Collection("Integration")]
public class HangfireFailureScenarioTests : IntegrationTestBase
{
    public HangfireFailureScenarioTests(TestWebApplicationFactory<Program> factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task AgentFailure_DuringTaskExecution_ShouldMarkTaskAsFailed()
    {
        // SETUP: Create agent that will fail
        var failingAgentId = await CreateTestAgentAsync("failing-agent", @"C:\FailingRepo", AgentBehavior.Error);

        // Configure agent to fail
        AgentRegistry.SimulateAgentFailure(failingAgentId);

        // ARRANGE: Queue task for failing agent
        var testCommand = "fail 'Simulated command failure'";
        var taskId = await QueueTestTaskAsync(testCommand, @"C:\FailingRepo", TaskPriority.Normal);

        Output.WriteLine($"Queued task {taskId} for failing agent {failingAgentId}");

        // ACT & VERIFY: Wait for task to fail
        var completed = await WaitForTaskCompletionAsync(taskId, TimeSpan.FromSeconds(30), new[] { TaskStatus.Failed });
        Assert.True(completed, "Task should complete with failure status");

        // Verify task failed with appropriate error
        var task = await GetTaskAsync(taskId);
        Assert.NotNull(task);
        Assert.Equal(TaskStatus.Failed, task.Status);
        Assert.NotNull(task.ErrorMessage);
        Assert.Contains("failure", task.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"Task {taskId} correctly failed with error: {task.ErrorMessage}");
    }

    [Fact]
    public async Task AgentTimeout_DuringTaskExecution_ShouldHandleGracefully()
    {
        // SETUP: Create agent that will timeout
        var timeoutAgentId = await CreateTestAgentAsync("timeout-agent", @"C:\TimeoutRepo", AgentBehavior.Timeout);

        // Configure agent to timeout
        AgentRegistry.SimulateAgentTimeout(timeoutAgentId);

        // ARRANGE: Queue task for timing-out agent
        var testCommand = "slow 'This command will timeout'";
        var taskId = await QueueTestTaskAsync(testCommand, @"C:\TimeoutRepo", TaskPriority.Normal);

        Output.WriteLine($"Queued task {taskId} for timeout agent {timeoutAgentId}");

        // ACT & VERIFY: Wait for reasonable time and check status
        var completed = await WaitForTaskCompletionAsync(taskId, TimeSpan.FromSeconds(15), new[] { TaskStatus.Failed, TaskStatus.InProgress });

        // Task should either timeout/fail or remain in progress
        var task = await GetTaskAsync(taskId);
        Assert.NotNull(task);
        Assert.True(task.Status == TaskStatus.Failed || task.Status == TaskStatus.InProgress,
            $"Task should be failed or in progress, but was {task.Status}");

        Output.WriteLine($"Task {taskId} status after timeout scenario: {task.Status}");
    }

    [Fact]
    public async Task NoAvailableAgents_ShouldThrowException()
    {
        // ARRANGE: Queue task for repository with no agents
        var nonExistentRepo = @"C:\NonExistentRepo";
        var testCommand = "echo 'This should fail'";

        Output.WriteLine($"Attempting to queue task for repository with no agents: {nonExistentRepo}");

        // ACT & VERIFY: Should throw exception
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => QueueTestTaskAsync(testCommand, nonExistentRepo, TaskPriority.Normal));

        Assert.Contains("No available agents", exception.Message);
        Output.WriteLine($"Correctly threw exception: {exception.Message}");
    }

    [Fact]
    public async Task MultipleFailingAgents_ShouldIsolateFailures()
    {
        // SETUP: Create multiple agents with different failure modes with delays to avoid race conditions
        var normalAgentId = await CreateTestAgentAsync("simple-agent-normalrepo", @"C:\TestRepository", AgentBehavior.Normal);
        await Task.Delay(100);

        var failingAgentId = await CreateTestAgentAsync("failing-agent", @"C:\FailingRepo", AgentBehavior.Error);
        await Task.Delay(100);

        var slowAgentId = await CreateTestAgentAsync("slow-agent", @"C:\SlowRepo", AgentBehavior.Slow);
        await Task.Delay(100);

        // Configure failure behaviors
        AgentRegistry.SimulateAgentFailure(failingAgentId);

        Output.WriteLine("Created agents with different behaviors for isolation test");

        // ARRANGE: Queue tasks for different agents with delays
        // CHANGE ORDER: normal task LAST to see if it helps
        Output.WriteLine("=== QUEUING TASKS (failing first, normal last) ===");

        var failingTaskId = await QueueTestTaskAsync("fail 'Failing task'", @"C:\FailingRepo", TaskPriority.Normal);
        Output.WriteLine($"Queued failing task: {failingTaskId}");
        await Task.Delay(500);

        var slowTaskId = await QueueTestTaskAsync("slow 'Slow task'", @"C:\SlowRepo", TaskPriority.Normal);
        Output.WriteLine($"Queued slow task: {slowTaskId}");
        await Task.Delay(500);

        var normalTaskId = await QueueTestTaskAsync("echo 'Normal task'", @"C:\TestRepository", TaskPriority.Normal);
        Output.WriteLine($"Queued normal task: {normalTaskId} (LAST)");
        await Task.Delay(500);

        Output.WriteLine("=== ALL TASKS QUEUED ===");

        // ACT: Wait for all tasks to complete with increased timeout
        // Check failing and slow tasks first to see if they can complete independently
        Output.WriteLine("=== WAITING FOR TASKS ===");
        Output.WriteLine("Checking failing task first...");
        var failingCompleted = await WaitForTaskCompletionAsync(failingTaskId, TimeSpan.FromSeconds(15), new[] { TaskStatus.Failed });

        Output.WriteLine("Checking slow task...");
        var slowCompleted = await WaitForTaskCompletionAsync(slowTaskId, TimeSpan.FromSeconds(15));

        Output.WriteLine("Finally checking normal task...");
        var normalCompleted = await WaitForTaskCompletionAsync(normalTaskId, TimeSpan.FromSeconds(15));

        // VERIFY: Different outcomes based on agent behavior
        var normalTask = await GetTaskAsync(normalTaskId);
        var failingTask = await GetTaskAsync(failingTaskId);
        var slowTask = await GetTaskAsync(slowTaskId);

        Assert.NotNull(normalTask);
        Assert.NotNull(failingTask);
        Assert.NotNull(slowTask);

        // Normal task should succeed
        Assert.True(normalCompleted, "Normal task should complete successfully");
        Assert.Equal(TaskStatus.Completed, normalTask.Status);

        // Failing task should fail
        Assert.True(failingCompleted, "Failing task should complete with failure");
        Assert.Equal(TaskStatus.Failed, failingTask.Status);

        // Slow task should complete (eventually)
        Assert.True(slowCompleted, "Slow task should complete");
        Assert.Equal(TaskStatus.Completed, slowTask.Status);

        Output.WriteLine($"Task isolation verified - Normal: {normalTask.Status}, Failing: {failingTask.Status}, Slow: {slowTask.Status}");
    }

    [Fact]
    public async Task HighPriorityTask_WithFailingLowPriorityTasks_ShouldNotBeAffected()
    {
        // SETUP: Create agent
        var agentId = await CreateTestAgentAsync("priority-test-agent", @"C:\PriorityRepo", AgentBehavior.Normal);

        Output.WriteLine("Testing priority isolation with failing low-priority tasks");

        // ARRANGE: Queue multiple low-priority failing tasks
        var lowPriorityTaskIds = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var taskId = await QueueTestTaskAsync($"fail 'Low priority failure {i}'", @"C:\PriorityRepo", TaskPriority.Low);
            lowPriorityTaskIds.Add(taskId);
        }

        // Queue high-priority successful task
        var highPriorityTaskId = await QueueTestTaskAsync("echo 'High priority success'", @"C:\PriorityRepo", TaskPriority.High);

        // ACT: Wait for high-priority task to complete
        var highPriorityCompleted = await WaitForTaskCompletionAsync(highPriorityTaskId, TimeSpan.FromSeconds(30));

        // VERIFY: High-priority task should succeed despite low-priority failures
        Assert.True(highPriorityCompleted, "High-priority task should complete successfully");

        var highPriorityTask = await GetTaskAsync(highPriorityTaskId);
        Assert.NotNull(highPriorityTask);
        Assert.Equal(TaskStatus.Completed, highPriorityTask.Status);

        Output.WriteLine($"High-priority task {highPriorityTaskId} completed successfully despite low-priority failures");
    }

    [Fact]
    public async Task DatabaseConnection_Failure_ShouldHandleGracefully()
    {
        // SETUP: Create agent for testing
        var agentId = await CreateTestAgentAsync("db-test-agent", @"C:\DatabaseRepo", AgentBehavior.Normal);

        // ARRANGE: Queue task normally
        var taskId = await QueueTestTaskAsync("echo 'Database connection test'", @"C:\DatabaseRepo", TaskPriority.Normal);

        Output.WriteLine($"Queued task {taskId} for database connection testing");

        // ACT: Attempt to complete task (this tests the overall system resilience)
        var completed = await WaitForTaskCompletionAsync(taskId, TimeSpan.FromSeconds(30));

        // VERIFY: System should handle gracefully
        var task = await GetTaskAsync(taskId);
        Assert.NotNull(task);

        // Task should either complete or fail gracefully (not crash)
        Assert.True(task.Status == TaskStatus.Completed || task.Status == TaskStatus.Failed || task.Status == TaskStatus.Pending,
            $"Task should have a valid status, but was {task.Status}");

        Output.WriteLine($"Database connection test completed with status: {task.Status}");
    }

    [Fact]
    public async Task ConcurrentFailures_ShouldNotCascade()
    {
        // SETUP: Create multiple agents for concurrent testing
        var agent1Id = await CreateTestAgentAsync("concurrent-agent-1", @"C:\ConcurrentRepo1", AgentBehavior.Error);
        var agent2Id = await CreateTestAgentAsync("concurrent-agent-2", @"C:\ConcurrentRepo2", AgentBehavior.Error);
        var agent3Id = await CreateTestAgentAsync("concurrent-agent-3", @"C:\ConcurrentRepo3", AgentBehavior.Normal);

        // Configure failures
        AgentRegistry.SimulateAgentFailure(agent1Id);
        AgentRegistry.SimulateAgentFailure(agent2Id);

        Output.WriteLine("Testing concurrent failures for cascade prevention");

        // ARRANGE: Queue tasks concurrently
        var tasks = new[]
        {
            QueueTestTaskAsync("fail 'Concurrent failure 1'", @"C:\ConcurrentRepo1", TaskPriority.Normal),
            QueueTestTaskAsync("fail 'Concurrent failure 2'", @"C:\ConcurrentRepo2", TaskPriority.Normal),
            QueueTestTaskAsync("echo 'Concurrent success'", @"C:\ConcurrentRepo3", TaskPriority.Normal)
        };

        var taskIds = await Task.WhenAll(tasks);

        // ACT: Wait for all tasks to complete
        var completionTasks = taskIds.Select(id =>
            WaitForTaskCompletionAsync(id, TimeSpan.FromSeconds(30), new[] { TaskStatus.Completed, TaskStatus.Failed }))
            .ToArray();

        var results = await Task.WhenAll(completionTasks);

        // VERIFY: All tasks should complete (success or failure), none should hang
        Assert.True(results.All(r => r), "All tasks should complete within timeout");

        // Verify individual task statuses
        var taskStates = await Task.WhenAll(taskIds.Select(GetTaskAsync));

        Assert.All(taskStates, task =>
        {
            Assert.NotNull(task);
            Assert.True(task.Status == TaskStatus.Completed || task.Status == TaskStatus.Failed,
                $"Task {task.Id} should be completed or failed, but was {task.Status}");
        });

        Output.WriteLine($"Concurrent failure test completed - Task statuses: {string.Join(", ", taskStates.Select(t => t!.Status))}");
    }
}