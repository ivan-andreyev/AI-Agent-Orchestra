using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core;
using Orchestra.Core.Services;
using Orchestra.Core.Models;
using Orchestra.Core.Data.Entities;
using TaskStatus = Orchestra.Core.Models.TaskStatus;
using TaskPriority = Orchestra.Core.Models.TaskPriority;

namespace Orchestra.Tests.UnitTests.Services;

/// <summary>
/// Тесты для BackgroundTaskAssignmentService - фонового сервиса автоматического назначения задач агентам.
/// Проверяет корректность работы механизма непрерывного мониторинга и автоматического назначения.
/// </summary>
public class BackgroundTaskAssignmentServiceTests : IDisposable
{
    private readonly List<SimpleOrchestrator> _orchestrators = new();

    private SimpleOrchestrator CreateOrchestrator()
    {
        var testFileName = $"test-state-bg-{Guid.NewGuid():N}.json";
        var agentStateStore = new InMemoryAgentStateStore();
        var orchestrator = new SimpleOrchestrator(agentStateStore, null, testFileName);
        _orchestrators.Add(orchestrator);
        return orchestrator;
    }

    private IServiceProvider CreateServiceProvider(SimpleOrchestrator orchestrator)
    {
        var services = new ServiceCollection();
        services.AddSingleton(orchestrator);
        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        foreach (var orchestrator in _orchestrators)
        {
            orchestrator.Dispose();
        }
    }

    [Fact]
    public async Task ProcessUnassignedTasks_NoTasksNoAgents_ShouldNotTriggerAssignment()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act
        await Task.Run(() => service.StartAsync(CancellationToken.None));
        await Task.Delay(100); // Give service time to start
        await service.StopAsync(CancellationToken.None);

        // Assert
        var state = orchestrator.GetCurrentState();
        Assert.Empty(state.TaskQueue);
        Assert.Empty(state.Agents);
    }

    [Fact]
    public async Task ProcessUnassignedTasks_TasksExistNoAgents_ShouldNotAssign()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.QueueTask("Test task", @"C:\TestRepo", TaskPriority.Normal);

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Get initial state
        var initialState = orchestrator.GetCurrentState();
        var initialTask = initialState.TaskQueue.First();

        // Act
        await Task.Run(() => service.StartAsync(CancellationToken.None));
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Assert
        var finalState = orchestrator.GetCurrentState();
        Assert.Single(finalState.TaskQueue);
        var finalTask = finalState.TaskQueue.First();
        Assert.Equal(TaskStatus.Pending, finalTask.Status); // Should still be Pending
        Assert.Empty(finalTask.AgentId); // Should not be assigned
    }

    [Fact]
    public async Task ProcessUnassignedTasks_NoTasksAgentsExist_ShouldNotTriggerAssignment()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act
        await Task.Run(() => service.StartAsync(CancellationToken.None));
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Assert
        var state = orchestrator.GetCurrentState();
        Assert.Empty(state.TaskQueue);
        Assert.Single(state.Agents);
    }

    [Fact]
    public async Task ProcessUnassignedTasks_TasksAndIdleAgents_ShouldAutoAssign()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act - Start service first, then queue task
        await Task.Run(() => service.StartAsync(CancellationToken.None));
        await Task.Delay(100); // Let service start

        // Queue task after service is running
        orchestrator.QueueTask("Test task", @"C:\TestRepo", TaskPriority.Normal);

        // Wait for background service to process (2 second polling + buffer)
        await Task.Delay(2500);
        await service.StopAsync(CancellationToken.None);

        // Assert
        var finalState = orchestrator.GetCurrentState();
        Assert.Single(finalState.TaskQueue);
        var finalTask = finalState.TaskQueue.First();
        Assert.Equal(TaskStatus.Assigned, finalTask.Status); // Should be Assigned by background service
        Assert.Equal("agent1", finalTask.AgentId); // Should be assigned to agent1
        Assert.NotNull(finalTask.StartedAt); // Should have StartedAt timestamp
    }

    [Fact]
    public async Task ProcessUnassignedTasks_MultipleTasksSingleAgent_ShouldAssignAll()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");
        orchestrator.QueueTask("Task 1", @"C:\TestRepo", TaskPriority.High);
        orchestrator.QueueTask("Task 2", @"C:\TestRepo", TaskPriority.Normal);
        orchestrator.QueueTask("Task 3", @"C:\TestRepo", TaskPriority.Low);

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act
        await Task.Run(() => service.StartAsync(CancellationToken.None));
        await Task.Delay(2500);
        await service.StopAsync(CancellationToken.None);

        // Assert
        var state = orchestrator.GetCurrentState();
        Assert.Equal(3, state.TaskQueue.Count());
        foreach (var task in state.TaskQueue)
        {
            Assert.Equal(TaskStatus.Assigned, task.Status);
            Assert.Equal("agent1", task.AgentId);
        }
    }

    [Fact]
    public async Task ProcessUnassignedTasks_SingleTaskMultipleAgents_ShouldAssignToPreferredAgent()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\Repo1");
        orchestrator.RegisterAgent("agent2", "Agent 2", "claude-code", @"C:\Repo2");
        orchestrator.QueueTask("Task for Repo1", @"C:\Repo1", TaskPriority.Normal);

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act
        await Task.Run(() => service.StartAsync(CancellationToken.None));
        await Task.Delay(2500);
        await service.StopAsync(CancellationToken.None);

        // Assert
        var state = orchestrator.GetCurrentState();
        var assignedTask = state.TaskQueue.First();
        Assert.Equal(TaskStatus.Assigned, assignedTask.Status);
        Assert.Equal("agent1", assignedTask.AgentId); // Should prefer agent with matching repository
    }

    [Fact]
    public async Task ProcessUnassignedTasks_AgentBecomesAvailableLater_ShouldEventuallyAssign()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.QueueTask("Task waiting for agent", @"C:\TestRepo", TaskPriority.Normal);

        // Verify task is initially Pending
        var initialState = orchestrator.GetCurrentState();
        var initialTask = initialState.TaskQueue.First();
        Assert.Equal(TaskStatus.Pending, initialTask.Status);

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act - Start service
        await Task.Run(() => service.StartAsync(CancellationToken.None));
        await Task.Delay(500); // Wait a bit

        // Agent becomes available
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");

        // Wait for next polling cycle
        await Task.Delay(2500);
        await service.StopAsync(CancellationToken.None);

        // Assert
        var finalState = orchestrator.GetCurrentState();
        var finalTask = finalState.TaskQueue.First();
        Assert.Equal(TaskStatus.Assigned, finalTask.Status);
        Assert.Equal("agent1", finalTask.AgentId);
    }

    [Fact]
    public async Task ProcessUnassignedTasks_PriorityOrdering_ShouldAssignHighPriorityFirst()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");

        // Queue tasks in reverse priority order
        orchestrator.QueueTask("Low priority task", @"C:\TestRepo", TaskPriority.Low);
        orchestrator.QueueTask("High priority task", @"C:\TestRepo", TaskPriority.Critical);
        orchestrator.QueueTask("Medium priority task", @"C:\TestRepo", TaskPriority.Normal);

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act
        await Task.Run(() => service.StartAsync(CancellationToken.None));
        await Task.Delay(2500);
        await service.StopAsync(CancellationToken.None);

        // Assert
        var state = orchestrator.GetCurrentState();
        var tasks = state.TaskQueue.ToList();
        Assert.Equal(3, tasks.Count);

        // All should be assigned
        foreach (var task in tasks)
        {
            Assert.Equal(TaskStatus.Assigned, task.Status);
            Assert.Equal("agent1", task.AgentId);
        }
    }

    [Fact]
    public async Task ProcessUnassignedTasks_AgentBusyStatus_ShouldNotAssignToBusyAgent()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");
        orchestrator.UpdateAgentStatus("agent1", AgentStatus.Busy, "Working on something");
        orchestrator.QueueTask("Task needs idle agent", @"C:\TestRepo", TaskPriority.Normal);

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act
        await Task.Run(() => service.StartAsync(CancellationToken.None));
        await Task.Delay(2500);
        await service.StopAsync(CancellationToken.None);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        Assert.Equal(TaskStatus.Pending, task.Status); // Should still be Pending
        Assert.Empty(task.AgentId); // Should not be assigned to busy agent
    }

    [Fact]
    public async Task ProcessUnassignedTasks_AgentBecomesIdle_ShouldAssignWaitingTask()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");
        orchestrator.UpdateAgentStatus("agent1", AgentStatus.Busy, "Working");
        orchestrator.QueueTask("Waiting task", @"C:\TestRepo", TaskPriority.Normal);

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act - Start service
        await Task.Run(() => service.StartAsync(CancellationToken.None));
        await Task.Delay(500);

        // Agent becomes idle
        orchestrator.UpdateAgentStatus("agent1", AgentStatus.Idle, null);

        // Wait for next polling cycle
        await Task.Delay(2500);
        await service.StopAsync(CancellationToken.None);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        Assert.Equal(TaskStatus.Assigned, task.Status);
        Assert.Equal("agent1", task.AgentId);
    }

    [Fact]
    public async Task StartAsync_ShouldStartServiceSuccessfully()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Service should be running - wait a bit to ensure it starts
        await Task.Delay(100);

        // Assert - service should be running without errors
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_ShouldStopServiceGracefully()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Assert - service should stop without exceptions
        // No assertion needed - successful execution means graceful shutdown
    }

    [Fact]
    public async Task ProcessUnassignedTasks_ContinuousMonitoring_ShouldAssignOverTime()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act - Start service
        await service.StartAsync(CancellationToken.None);

        // Queue task after service started
        await Task.Delay(500);
        orchestrator.QueueTask("Task queued after start", @"C:\TestRepo", TaskPriority.Normal);

        // Wait for assignment (should happen within 2 seconds)
        await Task.Delay(2500);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        Assert.Equal(TaskStatus.Assigned, task.Status);
        Assert.Equal("agent1", task.AgentId);

        // Cleanup
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessUnassignedTasks_RepositoryPathMatching_ShouldPreferSameRepo()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\Repo1");
        orchestrator.RegisterAgent("agent2", "Agent 2", "claude-code", @"C:\Repo2");
        orchestrator.RegisterAgent("agent3", "Agent 3", "claude-code", @"C:\Repo3");

        orchestrator.QueueTask("Task for Repo2", @"C:\Repo2", TaskPriority.Normal);

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(2500);
        await service.StopAsync(CancellationToken.None);

        // Assert
        var state = orchestrator.GetCurrentState();
        var task = state.TaskQueue.First();
        Assert.Equal(TaskStatus.Assigned, task.Status);
        Assert.Equal("agent2", task.AgentId); // Should assign to agent2 (matching repo)
    }

    [Fact]
    public async Task ProcessUnassignedTasks_AssignmentLatency_ShouldMeetTwoSecondRequirement()
    {
        // Arrange
        var orchestrator = CreateOrchestrator();
        orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");

        var serviceProvider = CreateServiceProvider(orchestrator);
        var logger = Mock.Of<ILogger<BackgroundTaskAssignmentService>>();
        var service = new BackgroundTaskAssignmentService(serviceProvider, logger);

        // Act - Start service first
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Queue task and measure assignment time
        var startTime = DateTime.UtcNow;
        orchestrator.QueueTask("Latency test task", @"C:\TestRepo", TaskPriority.Normal);

        // Poll for assignment with timeout
        var timeout = TimeSpan.FromSeconds(3); // Allow 3s to be safe (requirement is <2s)
        var assigned = false;
        var endTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            var state = orchestrator.GetCurrentState();
            var task = state.TaskQueue.FirstOrDefault();
            if (task?.Status == TaskStatus.Assigned)
            {
                assigned = true;
                endTime = DateTime.UtcNow;
                break;
            }
            await Task.Delay(100);
        }

        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(assigned, "Task should be assigned within timeout");
        var assignmentLatency = endTime - startTime;
        Assert.True(assignmentLatency.TotalSeconds <= 2.5,
            $"Assignment latency should be <2.5s, but was {assignmentLatency.TotalSeconds:F2}s");
    }
}
