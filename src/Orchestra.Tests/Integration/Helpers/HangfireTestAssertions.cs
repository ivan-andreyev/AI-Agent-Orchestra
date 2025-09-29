using Orchestra.Core.Data.Entities;
using Orchestra.Core.Models;
using Xunit;
using TaskStatus = Orchestra.Core.Models.TaskStatus;

namespace Orchestra.Tests.Integration.Helpers;

/// <summary>
/// Custom assertions for Hangfire integration tests.
/// Provides domain-specific assertion methods for better test readability and maintainability.
/// </summary>
public static class HangfireTestAssertions
{
    /// <summary>
    /// Asserts that a task has completed successfully with expected results
    /// </summary>
    public static void AssertTaskCompletedSuccessfully(TaskRecord? task, string expectedCommand)
    {
        Assert.NotNull(task);
        Assert.Equal(TaskStatus.Completed, task.Status);
        Assert.Equal(expectedCommand, task.Command);
        Assert.NotNull(task.CompletedAt);
        Assert.True(task.CompletedAt > task.CreatedAt, "Completion time should be after creation time");
        Assert.Null(task.ErrorMessage);
    }

    /// <summary>
    /// Asserts that a task has failed with expected error information
    /// </summary>
    public static void AssertTaskFailed(TaskRecord? task, string expectedCommand, string? expectedErrorSubstring = null)
    {
        Assert.NotNull(task);
        Assert.Equal(TaskStatus.Failed, task.Status);
        Assert.Equal(expectedCommand, task.Command);
        Assert.NotNull(task.ErrorMessage);

        if (!string.IsNullOrEmpty(expectedErrorSubstring))
        {
            Assert.Contains(expectedErrorSubstring, task.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Asserts that a task is in the expected status with proper metadata
    /// </summary>
    public static void AssertTaskStatus(TaskRecord? task, TaskStatus expectedStatus, string expectedCommand)
    {
        Assert.NotNull(task);
        Assert.Equal(expectedStatus, task.Status);
        Assert.Equal(expectedCommand, task.Command);
        Assert.True(task.CreatedAt <= DateTime.UtcNow, "Creation time should not be in the future");

        if (expectedStatus == TaskStatus.Completed)
        {
            Assert.NotNull(task.CompletedAt);
        }

        if (expectedStatus == TaskStatus.Failed)
        {
            Assert.NotNull(task.ErrorMessage);
        }
    }

    /// <summary>
    /// Asserts that multiple tasks completed within expected time ranges
    /// </summary>
    public static void AssertTaskCompletionTimes(IEnumerable<TaskRecord> tasks, TimeSpan maxExpectedDuration)
    {
        var taskList = tasks.ToList();
        Assert.True(taskList.Count > 0, "Should have tasks to verify");

        foreach (var task in taskList)
        {
            Assert.NotNull(task);
            Assert.Equal(TaskStatus.Completed, task.Status);

            if (task.CompletedAt.HasValue)
            {
                var duration = task.CompletedAt.Value - task.CreatedAt;
                Assert.True(duration <= maxExpectedDuration,
                    $"Task {task.Id} took {duration.TotalSeconds:F1}s, expected <= {maxExpectedDuration.TotalSeconds:F1}s");
            }
        }
    }

    /// <summary>
    /// Asserts that tasks are distributed across multiple agents
    /// </summary>
    public static void AssertTaskDistribution(IEnumerable<TaskRecord> tasks, int expectedAgentCount)
    {
        var taskList = tasks.ToList();
        var agentIds = taskList.Where(t => !string.IsNullOrEmpty(t.AgentId))
                              .Select(t => t.AgentId)
                              .Distinct()
                              .ToList();

        Assert.True(agentIds.Count >= Math.Min(expectedAgentCount, taskList.Count),
            $"Tasks should be distributed across agents. Found {agentIds.Count}, expected >= {expectedAgentCount}");
    }

    /// <summary>
    /// Asserts that high-priority tasks completed before low-priority tasks
    /// </summary>
    public static void AssertPriorityOrdering(IEnumerable<TaskRecord> tasks)
    {
        var taskList = tasks.Where(t => t.CompletedAt.HasValue)
                           .OrderBy(t => t.CompletedAt!.Value)
                           .ToList();

        if (taskList.Count <= 1) return; // Need at least 2 tasks to check ordering

        var highPriorityTasks = taskList.Where(t => t.Priority == TaskPriority.High).ToList();
        var lowPriorityTasks = taskList.Where(t => t.Priority == TaskPriority.Low).ToList();

        if (highPriorityTasks.Any() && lowPriorityTasks.Any())
        {
            var lastHighPriorityCompletion = highPriorityTasks.Max(t => t.CompletedAt!.Value);
            var firstLowPriorityCompletion = lowPriorityTasks.Min(t => t.CompletedAt!.Value);

            // Allow some tolerance for concurrent execution
            var tolerance = TimeSpan.FromSeconds(5);
            Assert.True(lastHighPriorityCompletion <= firstLowPriorityCompletion.Add(tolerance),
                "High-priority tasks should generally complete before low-priority tasks");
        }
    }

    /// <summary>
    /// Asserts that tasks for different repositories are properly isolated
    /// </summary>
    public static void AssertRepositoryIsolation(IEnumerable<TaskRecord> tasks)
    {
        var taskList = tasks.ToList();
        var repositories = taskList.GroupBy(t => t.RepositoryPath).ToList();

        Assert.True(repositories.Count > 1, "Should have tasks from multiple repositories for isolation testing");

        foreach (var repoGroup in repositories)
        {
            var repoTasks = repoGroup.ToList();
            Assert.All(repoTasks, task =>
            {
                Assert.Equal(repoGroup.Key, task.RepositoryPath);
                Assert.True(task.Status == TaskStatus.Completed || task.Status == TaskStatus.Failed,
                    $"Repository {repoGroup.Key} tasks should complete (success or failure)");
            });
        }
    }

    /// <summary>
    /// Asserts that task execution performance meets expected criteria
    /// </summary>
    public static void AssertPerformanceMetrics(IEnumerable<TaskRecord> tasks, double maxAverageExecutionSeconds)
    {
        var completedTasks = tasks.Where(t => t.Status == TaskStatus.Completed && t.CompletedAt.HasValue).ToList();

        Assert.True(completedTasks.Count > 0, "Should have completed tasks to measure performance");

        var executionTimes = completedTasks.Select(t => (t.CompletedAt!.Value - t.CreatedAt).TotalSeconds).ToList();
        var averageExecutionTime = executionTimes.Average();

        Assert.True(averageExecutionTime <= maxAverageExecutionSeconds,
            $"Average execution time {averageExecutionTime:F2}s should be <= {maxAverageExecutionSeconds:F2}s");
    }

    /// <summary>
    /// Asserts that error handling is working correctly across multiple tasks
    /// </summary>
    public static void AssertErrorHandling(IEnumerable<TaskRecord> allTasks,
                                          IEnumerable<TaskRecord> expectedFailedTasks,
                                          IEnumerable<TaskRecord> expectedSuccessfulTasks)
    {
        var allTasksList = allTasks.ToList();
        var expectedFailedList = expectedFailedTasks.ToList();
        var expectedSuccessfulList = expectedSuccessfulTasks.ToList();

        // Verify failed tasks
        foreach (var expectedFailed in expectedFailedList)
        {
            var actualTask = allTasksList.FirstOrDefault(t => t.Id == expectedFailed.Id);
            Assert.NotNull(actualTask);
            Assert.Equal(TaskStatus.Failed, actualTask.Status);
            Assert.NotNull(actualTask.ErrorMessage);
        }

        // Verify successful tasks
        foreach (var expectedSuccessful in expectedSuccessfulList)
        {
            var actualTask = allTasksList.FirstOrDefault(t => t.Id == expectedSuccessful.Id);
            Assert.NotNull(actualTask);
            Assert.Equal(TaskStatus.Completed, actualTask.Status);
            Assert.Null(actualTask.ErrorMessage);
        }

        // Verify no unexpected failures
        var actualFailedCount = allTasksList.Count(t => t.Status == TaskStatus.Failed);
        Assert.Equal(expectedFailedList.Count, actualFailedCount);
    }

    /// <summary>
    /// Asserts that the system maintains stability under load
    /// </summary>
    public static void AssertSystemStability(IEnumerable<TaskRecord> tasks, double minSuccessRate = 0.8)
    {
        var taskList = tasks.ToList();
        var completedCount = taskList.Count(t => t.Status == TaskStatus.Completed);
        var totalCount = taskList.Count;

        Assert.True(totalCount > 0, "Should have tasks to measure stability");

        var successRate = (double)completedCount / totalCount;
        Assert.True(successRate >= minSuccessRate,
            $"Success rate {successRate:P1} should be >= {minSuccessRate:P1} for system stability");

        // Verify no tasks are stuck in pending state for too long
        var oldPendingTasks = taskList.Where(t =>
            t.Status == TaskStatus.Pending &&
            (DateTime.UtcNow - t.CreatedAt) > TimeSpan.FromMinutes(2)
        ).ToList();

        Assert.True(oldPendingTasks.Count == 0,
            $"Found {oldPendingTasks.Count} tasks stuck in pending state");
    }
}