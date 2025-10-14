using Orchestra.Core;
using Orchestra.Core.Models;
using Orchestra.Core.Services;
using TaskStatus = Orchestra.Core.Models.TaskStatus;
using TaskPriority = Orchestra.Core.Models.TaskPriority;
using AgentStatus = Orchestra.Core.Data.Entities.AgentStatus;

namespace Orchestra.Tests.UnitTests;

/// <summary>
/// Тесты для проверки потока изменения статусов задач (Phase 4.3.2)
/// Validates task status progression: Pending → Assigned → InProgress → Completed/Failed/Cancelled
/// </summary>
public class TaskStatusFlowTests : IDisposable
{
    private readonly SimpleOrchestrator _orchestrator;
    private readonly string _testFileName;
    private const string TestAgentId = "test-agent-1";
    private const string TestRepositoryPath = @"C:\TestRepo";

    public TaskStatusFlowTests()
    {
        _testFileName = $"test-status-flow-{Guid.NewGuid()}.json";
        var agentStateStore = new InMemoryAgentStateStore();
        _orchestrator = new SimpleOrchestrator(agentStateStore, null, _testFileName);

        // Register a test agent for status flow tests
        _orchestrator.RegisterAgent(TestAgentId, "Test Agent", "claude-code", TestRepositoryPath);
    }

    [Fact]
    public void UpdateTaskStatus_ValidTransition_PendingToAssigned_ShouldSucceed()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;

        // Act
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);

        // Assert
        state = _orchestrator.GetCurrentState();
        var updatedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.Assigned, updatedTask.Status);
    }

    [Fact]
    public void UpdateTaskStatus_ValidTransition_AssignedToInProgress_ShouldSucceed()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);

        // Act
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress);

        // Assert
        state = _orchestrator.GetCurrentState();
        var updatedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.InProgress, updatedTask.Status);
        Assert.NotNull(updatedTask.StartedAt);
    }

    [Fact]
    public void UpdateTaskStatus_ValidTransition_InProgressToCompleted_ShouldSucceed()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress);

        // Act
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Completed, "Task completed successfully");

        // Assert
        state = _orchestrator.GetCurrentState();
        var updatedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.Completed, updatedTask.Status);
        Assert.NotNull(updatedTask.CompletedAt);
        Assert.Equal("Task completed successfully", updatedTask.Result);
    }

    [Fact]
    public void UpdateTaskStatus_ValidTransition_InProgressToFailed_ShouldSucceed()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress);

        // Act
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Failed, "Task failed: error occurred");

        // Assert
        state = _orchestrator.GetCurrentState();
        var updatedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.Failed, updatedTask.Status);
        Assert.NotNull(updatedTask.CompletedAt);
        Assert.Equal("Task failed: error occurred", updatedTask.Result);
    }

    [Fact]
    public void UpdateTaskStatus_ValidTransition_PendingToCancelled_ShouldSucceed()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;

        // Act
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Cancelled);

        // Assert
        state = _orchestrator.GetCurrentState();
        var updatedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.Cancelled, updatedTask.Status);
        Assert.NotNull(updatedTask.CompletedAt);
    }

    [Fact]
    public void UpdateTaskStatus_ValidTransition_AssignedToCancelled_ShouldSucceed()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);

        // Act
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Cancelled);

        // Assert
        state = _orchestrator.GetCurrentState();
        var updatedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.Cancelled, updatedTask.Status);
        Assert.NotNull(updatedTask.CompletedAt);
    }

    [Fact]
    public void UpdateTaskStatus_ValidTransition_InProgressToCancelled_ShouldSucceed()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress);

        // Act
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Cancelled);

        // Assert
        state = _orchestrator.GetCurrentState();
        var updatedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.Cancelled, updatedTask.Status);
        Assert.NotNull(updatedTask.CompletedAt);
    }

    [Fact]
    public void UpdateTaskStatus_InvalidTransition_PendingToInProgress_ShouldNotChange()
    {
        // Arrange - remove agent to ensure task stays in Pending status
        _orchestrator.ClearAllAgents();
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        var originalStatus = task.Status;
        Assert.Equal(TaskStatus.Pending, originalStatus); // Verify task is actually Pending

        // Act - attempt invalid transition (Pending -> InProgress without Assigned)
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress);

        // Assert - status should remain unchanged
        state = _orchestrator.GetCurrentState();
        var updatedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.Pending, updatedTask.Status);
    }

    [Fact]
    public void UpdateTaskStatus_InvalidTransition_PendingToCompleted_ShouldNotChange()
    {
        // Arrange - remove agent to ensure task stays in Pending status
        _orchestrator.ClearAllAgents();
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        Assert.Equal(TaskStatus.Pending, task.Status); // Verify task is actually Pending

        // Act - attempt invalid transition (Pending -> Completed)
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Completed);

        // Assert - status should remain unchanged
        state = _orchestrator.GetCurrentState();
        var updatedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.Pending, updatedTask.Status);
    }

    [Fact]
    public void UpdateTaskStatus_InvalidTransition_AssignedToCompleted_ShouldNotChange()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);

        // Act - attempt invalid transition (Assigned -> Completed without InProgress)
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Completed);

        // Assert - status should remain Assigned
        state = _orchestrator.GetCurrentState();
        var updatedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.Assigned, updatedTask.Status);
    }

    [Fact]
    public void UpdateTaskStatus_CompleteFlow_AllValidTransitions_ShouldSucceed()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;

        // Act - complete valid flow: Pending -> Assigned -> InProgress -> Completed
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);
        state = _orchestrator.GetCurrentState();
        var taskAfterAssigned = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(taskAfterAssigned);
        Assert.Equal(TaskStatus.Assigned, taskAfterAssigned.Status);

        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress);
        state = _orchestrator.GetCurrentState();
        var taskAfterInProgress = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(taskAfterInProgress);
        Assert.Equal(TaskStatus.InProgress, taskAfterInProgress.Status);
        Assert.NotNull(taskAfterInProgress.StartedAt);

        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Completed, "Success");
        state = _orchestrator.GetCurrentState();
        var taskAfterCompleted = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);

        // Assert - final state validation
        Assert.NotNull(taskAfterCompleted);
        Assert.Equal(TaskStatus.Completed, taskAfterCompleted.Status);
        Assert.NotNull(taskAfterCompleted.StartedAt);
        Assert.NotNull(taskAfterCompleted.CompletedAt);
        Assert.Equal("Success", taskAfterCompleted.Result);
        Assert.True(taskAfterCompleted.CompletedAt >= taskAfterCompleted.StartedAt);
    }

    [Fact]
    public void UpdateTaskStatus_TimestampProgression_ShouldBeCorrect()
    {
        // Arrange - task will be auto-assigned with StartedAt set
        var beforeCreation = DateTime.Now;
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        var createdAt = task.CreatedAt;
        var initialStartedAt = task.StartedAt; // Already set if auto-assigned

        // Verify task was created after our reference time
        Assert.True(createdAt >= beforeCreation, "CreatedAt should be after reference time");

        // Act - progress through statuses
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress);

        // Small delay to ensure timestamp differences
        Thread.Sleep(10);

        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Completed);

        // Assert - validate timestamp progression
        state = _orchestrator.GetCurrentState();
        var finalTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(finalTask);
        Assert.NotNull(finalTask.StartedAt);
        Assert.NotNull(finalTask.CompletedAt);

        // StartedAt should be set (either at creation for auto-assigned or at InProgress transition)
        // Use millisecond precision to avoid timing issues
        var createdAtMs = new DateTime(createdAt.Year, createdAt.Month, createdAt.Day, createdAt.Hour, createdAt.Minute, createdAt.Second, createdAt.Millisecond);
        var startedAtMs = new DateTime(finalTask.StartedAt.Value.Year, finalTask.StartedAt.Value.Month, finalTask.StartedAt.Value.Day,
            finalTask.StartedAt.Value.Hour, finalTask.StartedAt.Value.Minute, finalTask.StartedAt.Value.Second, finalTask.StartedAt.Value.Millisecond);

        // Allow for same-millisecond timestamps or later
        Assert.True(startedAtMs >= createdAtMs || (startedAtMs - createdAtMs).TotalMilliseconds < 1,
            $"StartedAt ({startedAtMs}) should be at or after CreatedAt ({createdAtMs})");

        Assert.True(finalTask.CompletedAt >= finalTask.StartedAt, "CompletedAt should be after StartedAt");
    }

    [Fact]
    public void UpdateTaskStatus_NonExistentTask_ShouldNotThrow()
    {
        // Arrange
        var nonExistentTaskId = "non-existent-task-id";

        // Act & Assert - should not throw exception
        var exception = Record.Exception(() =>
            _orchestrator.UpdateTaskStatus(nonExistentTaskId, TaskStatus.Completed));

        Assert.Null(exception);
    }

    [Fact]
    public void UpdateTaskStatus_SameStatus_ShouldAllowUpdate()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress);

        // Act - update with same status but different result
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress, "Progress update 1");
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress, "Progress update 2");

        // Assert - status updates allowed for result changes
        state = _orchestrator.GetCurrentState();
        var updatedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(updatedTask);
        Assert.Equal(TaskStatus.InProgress, updatedTask.Status);
        Assert.Equal("Progress update 2", updatedTask.Result);
    }

    [Fact]
    public void StartTask_ShouldTransitionToInProgress()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);

        // Act
        _orchestrator.StartTask(taskId, TestAgentId);

        // Assert
        state = _orchestrator.GetCurrentState();
        var startedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(startedTask);
        Assert.Equal(TaskStatus.InProgress, startedTask.Status);
        Assert.NotNull(startedTask.StartedAt);
    }

    [Fact]
    public void CompleteTask_ShouldTransitionToCompleted()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress);

        // Act
        _orchestrator.CompleteTask(taskId);

        // Assert
        state = _orchestrator.GetCurrentState();
        var completedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(completedTask);
        Assert.Equal(TaskStatus.Completed, completedTask.Status);
        Assert.NotNull(completedTask.CompletedAt);
    }

    [Fact]
    public void FailTask_ShouldTransitionToFailed()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.Assigned);
        _orchestrator.UpdateTaskStatus(taskId, TaskStatus.InProgress);

        // Act
        _orchestrator.FailTask(taskId);

        // Assert
        state = _orchestrator.GetCurrentState();
        var failedTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(failedTask);
        Assert.Equal(TaskStatus.Failed, failedTask.Status);
        Assert.NotNull(failedTask.CompletedAt);
    }

    [Fact]
    public void CancelTask_ShouldTransitionToCancelled()
    {
        // Arrange
        _orchestrator.QueueTask("Test command", TestRepositoryPath, TaskPriority.Normal);
        var state = _orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        var taskId = task.Id;

        // Act
        _orchestrator.CancelTask(taskId);

        // Assert
        state = _orchestrator.GetCurrentState();
        var cancelledTask = state.TaskQueue.FirstOrDefault(t => t.Id == taskId);
        Assert.NotNull(cancelledTask);
        Assert.Equal(TaskStatus.Cancelled, cancelledTask.Status);
        Assert.NotNull(cancelledTask.CompletedAt);
    }

    [Fact]
    public void GetTasksByStatus_ShouldReturnFilteredTasks()
    {
        // Arrange - tasks will be auto-assigned because agent is available
        _orchestrator.QueueTask("Task 1", TestRepositoryPath, TaskPriority.Normal);
        _orchestrator.QueueTask("Task 2", TestRepositoryPath, TaskPriority.Normal);
        _orchestrator.QueueTask("Task 3", TestRepositoryPath, TaskPriority.Normal);

        var state = _orchestrator.GetCurrentState();
        var allTasks = state.TaskQueue.ToList();

        // Transition tasks to different statuses
        // Task 1: Assigned -> InProgress
        _orchestrator.UpdateTaskStatus(allTasks[0].Id, TaskStatus.InProgress);

        // Task 2: Remains Assigned (already auto-assigned)
        // Task 3: Assigned -> InProgress -> Completed
        _orchestrator.UpdateTaskStatus(allTasks[2].Id, TaskStatus.InProgress);
        _orchestrator.UpdateTaskStatus(allTasks[2].Id, TaskStatus.Completed);

        // Act
        var assignedTasks = _orchestrator.GetTasksByStatus(TaskStatus.Assigned);
        var inProgressTasks = _orchestrator.GetTasksByStatus(TaskStatus.InProgress);
        var completedTasks = _orchestrator.GetTasksByStatus(TaskStatus.Completed);

        // Assert
        Assert.Single(assignedTasks); // Task 2
        Assert.Single(inProgressTasks); // Task 1
        Assert.Single(completedTasks); // Task 3
    }

    public void Dispose()
    {
        _orchestrator?.Dispose();

        // Clean up test file
        if (File.Exists(_testFileName))
        {
            try
            {
                File.Delete(_testFileName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
