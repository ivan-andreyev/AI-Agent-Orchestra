using Orchestra.Core.Models;
using Orchestra.Tests.Integration.Mocks;
using Xunit;
using Xunit.Abstractions;
using TaskPriority = Orchestra.Core.Models.TaskPriority;
using TaskStatus = Orchestra.Core.Models.TaskStatus;

namespace Orchestra.Tests.Integration;

/// <summary>
/// Scaling and performance tests for Hangfire coordination system.
/// Tests system behavior under load and concurrent operations.
/// </summary>
[Collection("Integration")]
public class HangfireScalingTests : IntegrationTestBase, IClassFixture<TestWebApplicationFactory<Program>>
{
    public HangfireScalingTests(TestWebApplicationFactory<Program> factory, ITestOutputHelper output)
        : base(factory, output)
    {
    }

    [Fact]
    public async Task MultipleAgents_ConcurrentTasks_ShouldDistributeWorkload()
    {
        // SETUP: Create multiple agents for load distribution
        var agentCount = 3;
        var agentIds = new List<string>();

        for (int i = 1; i <= agentCount; i++)
        {
            var agentId = await CreateTestAgentAsync($"scaling-agent-{i}", $@"C:\ScalingRepo{i}", AgentBehavior.Normal);
            agentIds.Add(agentId);
        }

        Output.WriteLine($"Created {agentCount} agents for concurrent workload testing");

        // ARRANGE: Queue multiple tasks across different repositories
        var taskCount = 6; // 2 tasks per agent
        var taskIds = new List<string>();

        for (int i = 1; i <= taskCount; i++)
        {
            var repoIndex = ((i - 1) % agentCount) + 1;
            var taskId = await QueueTestTaskAsync(
                $"echo 'Concurrent task {i}'",
                $@"C:\ScalingRepo{repoIndex}",
                TaskPriority.Normal);
            taskIds.Add(taskId);
        }

        Output.WriteLine($"Queued {taskCount} tasks across {agentCount} agents");

        // ACT: Wait for all tasks to complete
        var completionTasks = taskIds.Select(id => WaitForTaskCompletionAsync(id, TimeSpan.FromSeconds(45))).ToArray();
        var results = await Task.WhenAll(completionTasks);

        // VERIFY: All tasks should complete successfully
        Assert.True(results.All(r => r), "All concurrent tasks should complete successfully");

        // Verify all tasks completed
        var finalTasks = await Task.WhenAll(taskIds.Select(GetTaskAsync));
        Assert.All(finalTasks, task =>
        {
            Assert.NotNull(task);
            Assert.Equal(TaskStatus.Completed, task.Status);
        });

        Output.WriteLine($"Successfully completed {taskCount} concurrent tasks across {agentCount} agents");
    }

    [Fact]
    public async Task HighVolumeTaskQueue_ShouldMaintainPerformance()
    {
        // SETUP: Create agent for high-volume testing
        var agentId = await CreateTestAgentAsync("high-volume-agent", @"C:\HighVolumeRepo", AgentBehavior.Normal);

        Output.WriteLine("Testing high-volume task queuing and processing");

        // ARRANGE: Queue many tasks rapidly
        var taskCount = 10;
        var taskIds = new List<string>();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 1; i <= taskCount; i++)
        {
            var taskId = await QueueTestTaskAsync(
                $"echo 'High volume task {i}'",
                @"C:\HighVolumeRepo",
                TaskPriority.Normal);
            taskIds.Add(taskId);
        }

        stopwatch.Stop();
        Output.WriteLine($"Queued {taskCount} tasks in {stopwatch.ElapsedMilliseconds}ms");

        // ACT: Wait for all tasks to complete
        var completionStart = System.Diagnostics.Stopwatch.StartNew();
        var completionTasks = taskIds.Select(id => WaitForTaskCompletionAsync(id, TimeSpan.FromSeconds(60))).ToArray();
        var results = await Task.WhenAll(completionTasks);
        completionStart.Stop();

        // VERIFY: Performance metrics
        Assert.True(results.All(r => r), "All high-volume tasks should complete");

        var avgTimePerTask = completionStart.ElapsedMilliseconds / (double)taskCount;
        Output.WriteLine($"Completed {taskCount} tasks in {completionStart.ElapsedMilliseconds}ms (avg: {avgTimePerTask:F1}ms per task)");

        // Verify all tasks completed successfully
        var finalTasks = await Task.WhenAll(taskIds.Select(GetTaskAsync));
        Assert.All(finalTasks, task =>
        {
            Assert.NotNull(task);
            Assert.Equal(TaskStatus.Completed, task.Status);
        });

        // Performance assertion: Should not take excessively long
        Assert.True(avgTimePerTask < 10000, $"Average time per task ({avgTimePerTask:F1}ms) should be reasonable");
    }

    [Fact]
    public async Task MixedPriorityWorkload_ShouldRespectPriorityOrdering()
    {
        // SETUP: Create agent for priority testing
        var agentId = await CreateTestAgentAsync("priority-agent", @"C:\PriorityRepo", AgentBehavior.Normal);

        Output.WriteLine("Testing mixed priority workload handling");

        // ARRANGE: Queue tasks with mixed priorities
        var taskData = new[]
        {
            new { Priority = TaskPriority.Low, Command = "echo 'Low priority 1'" },
            new { Priority = TaskPriority.High, Command = "echo 'High priority 1'" },
            new { Priority = TaskPriority.Normal, Command = "echo 'Normal priority 1'" },
            new { Priority = TaskPriority.High, Command = "echo 'High priority 2'" },
            new { Priority = TaskPriority.Low, Command = "echo 'Low priority 2'" },
            new { Priority = TaskPriority.Normal, Command = "echo 'Normal priority 2'" }
        };

        var taskIds = new List<string>();
        foreach (var data in taskData)
        {
            var taskId = await QueueTestTaskAsync(data.Command, @"C:\PriorityRepo", data.Priority);
            taskIds.Add(taskId);
        }

        Output.WriteLine($"Queued {taskData.Length} tasks with mixed priorities");

        // ACT: Wait for all tasks to complete
        var completionTasks = taskIds.Select(id => WaitForTaskCompletionAsync(id, TimeSpan.FromSeconds(60))).ToArray();
        var results = await Task.WhenAll(completionTasks);

        // VERIFY: All tasks should complete
        Assert.True(results.All(r => r), "All mixed priority tasks should complete");

        // Verify completion times and priority handling
        var finalTasks = await Task.WhenAll(taskIds.Select(GetTaskAsync));
        var completedTasks = finalTasks.Where(t => t != null && t.Status == TaskStatus.Completed).ToList();

        Assert.Equal(taskData.Length, completedTasks.Count);

        Output.WriteLine($"Completed {completedTasks.Count} mixed priority tasks successfully");
    }

    [Fact]
    public async Task AgentFailover_WithContinuousLoad_ShouldMaintainService()
    {
        // SETUP: Create multiple agents for failover testing
        var primaryAgentId = await CreateTestAgentAsync("primary-agent", @"C:\FailoverRepo", AgentBehavior.Normal);
        var backupAgentId = await CreateTestAgentAsync("backup-agent", @"C:\FailoverRepo", AgentBehavior.Normal);

        Output.WriteLine("Testing agent failover under continuous load");

        // ARRANGE: Start continuous task queuing
        var taskIds = new List<string>();
        var totalTasks = 8;

        // Queue initial batch of tasks
        for (int i = 1; i <= totalTasks / 2; i++)
        {
            var taskId = await QueueTestTaskAsync($"echo 'Pre-failover task {i}'", @"C:\FailoverRepo", TaskPriority.Normal);
            taskIds.Add(taskId);
        }

        // Simulate primary agent failure after initial tasks
        AgentRegistry.SimulateAgentFailure(primaryAgentId);
        Output.WriteLine($"Simulated failure of primary agent {primaryAgentId}");

        // Continue queuing tasks (should use backup agent)
        for (int i = (totalTasks / 2) + 1; i <= totalTasks; i++)
        {
            var taskId = await QueueTestTaskAsync($"echo 'Post-failover task {i}'", @"C:\FailoverRepo", TaskPriority.Normal);
            taskIds.Add(taskId);
        }

        // ACT: Wait for all tasks to complete
        var completionTasks = taskIds.Select(id =>
            WaitForTaskCompletionAsync(id, TimeSpan.FromSeconds(60), new[] { TaskStatus.Completed, TaskStatus.Failed }))
            .ToArray();
        var results = await Task.WhenAll(completionTasks);

        // VERIFY: System should maintain service despite failure
        Assert.True(results.Count(r => r) >= totalTasks / 2, "At least half of tasks should complete despite failover");

        var finalTasks = await Task.WhenAll(taskIds.Select(GetTaskAsync));
        var successfulTasks = finalTasks.Count(t => t?.Status == TaskStatus.Completed);

        Output.WriteLine($"Failover test completed: {successfulTasks}/{totalTasks} tasks successful");
        Assert.True(successfulTasks >= totalTasks / 2, "Service should be maintained during failover");
    }

    [Fact]
    public async Task ResourceContention_MultipleRepositories_ShouldIsolateCorrectly()
    {
        // SETUP: Create agents for different repositories
        var repoCount = 3;
        var agentIds = new List<string>();

        for (int i = 1; i <= repoCount; i++)
        {
            var agentId = await CreateTestAgentAsync($"repo-agent-{i}", $@"C:\RepoContention{i}", AgentBehavior.Normal);
            agentIds.Add(agentId);
        }

        Output.WriteLine($"Created {repoCount} agents for resource contention testing");

        // ARRANGE: Queue tasks for each repository simultaneously
        var tasksPerRepo = 3;
        var allTaskIds = new List<string>();

        var queueTasks = new List<Task<string>>();
        for (int repo = 1; repo <= repoCount; repo++)
        {
            for (int task = 1; task <= tasksPerRepo; task++)
            {
                var taskQueuing = QueueTestTaskAsync(
                    $"echo 'Repo {repo} Task {task}'",
                    $@"C:\RepoContention{repo}",
                    TaskPriority.Normal);
                queueTasks.Add(taskQueuing);
            }
        }

        var taskIds = await Task.WhenAll(queueTasks);
        allTaskIds.AddRange(taskIds);

        Output.WriteLine($"Queued {allTaskIds.Count} tasks across {repoCount} repositories");

        // ACT: Wait for all tasks to complete
        var completionTasks = allTaskIds.Select(id => WaitForTaskCompletionAsync(id, TimeSpan.FromSeconds(60))).ToArray();
        var results = await Task.WhenAll(completionTasks);

        // VERIFY: All repositories should process their tasks independently
        Assert.True(results.All(r => r), "All repository tasks should complete independently");

        // Verify repository isolation
        var finalTasks = await Task.WhenAll(allTaskIds.Select(GetTaskAsync));
        var tasksByRepo = finalTasks
            .Where(t => t != null)
            .GroupBy(t => t!.RepositoryPath)
            .ToList();

        Assert.Equal(repoCount, tasksByRepo.Count);

        foreach (var repoGroup in tasksByRepo)
        {
            var repoTasks = repoGroup.ToList();
            Assert.Equal(tasksPerRepo, repoTasks.Count);
            Assert.All(repoTasks, task => Assert.Equal(TaskStatus.Completed, task.Status));
        }

        Output.WriteLine($"Resource contention test completed: {repoCount} repositories processed {tasksPerRepo} tasks each");
    }

    [Fact]
    public async Task LongRunningTasks_WithShortTasks_ShouldNotBlock()
    {
        // SETUP: Create agent for mixed duration testing
        var agentId = await CreateTestAgentAsync("mixed-duration-agent", @"C:\MixedDurationRepo", AgentBehavior.Normal);

        Output.WriteLine("Testing mixed duration task processing");

        // ARRANGE: Queue mix of long and short tasks
        var taskIds = new List<string>();

        // Queue short tasks
        for (int i = 1; i <= 3; i++)
        {
            var taskId = await QueueTestTaskAsync($"echo 'Quick task {i}'", @"C:\MixedDurationRepo", TaskPriority.High);
            taskIds.Add(taskId);
        }

        // Queue long-running task
        var longTaskId = await QueueTestTaskAsync("slow 'Long running task'", @"C:\MixedDurationRepo", TaskPriority.Normal);
        taskIds.Add(longTaskId);

        // Queue more short tasks
        for (int i = 4; i <= 6; i++)
        {
            var taskId = await QueueTestTaskAsync($"echo 'Quick task {i}'", @"C:\MixedDurationRepo", TaskPriority.High);
            taskIds.Add(taskId);
        }

        Output.WriteLine($"Queued {taskIds.Count} tasks with mixed durations");

        // ACT: Wait for tasks to complete with extended timeout for long task
        var completionTasks = taskIds.Select(id => WaitForTaskCompletionAsync(id, TimeSpan.FromSeconds(90))).ToArray();
        var results = await Task.WhenAll(completionTasks);

        // VERIFY: All tasks should complete
        Assert.True(results.All(r => r), "All mixed duration tasks should complete");

        var finalTasks = await Task.WhenAll(taskIds.Select(GetTaskAsync));
        Assert.All(finalTasks, task =>
        {
            Assert.NotNull(task);
            Assert.Equal(TaskStatus.Completed, task.Status);
        });

        Output.WriteLine("Mixed duration task processing completed successfully");
    }
}