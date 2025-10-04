using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Orchestra.API.Services;
using Orchestra.Core;
using Orchestra.Core.Data;
using Orchestra.Core.Models;
using Orchestra.Tests.Integration.Mocks;
using Xunit;
using Xunit.Abstractions;
using Hangfire;
using Orchestra.Core.Data.Entities;
using TaskStatus = Orchestra.Core.Models.TaskStatus;
using TaskPriority = Orchestra.Core.Models.TaskPriority;

namespace Orchestra.Tests.Integration;

/// <summary>
/// Base class for integration tests providing common setup and utilities
/// Uses Collection fixture instead of Class fixture to avoid disposal conflicts
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly TestWebApplicationFactory<Program> Factory;
    protected readonly ITestOutputHelper Output;
    protected readonly IServiceScope TestScope;
    protected readonly MockAgentExecutor MockExecutor;
    protected readonly TestAgentRegistry AgentRegistry;
    protected readonly HangfireOrchestrator HangfireOrchestrator;
    protected readonly IBackgroundJobClient JobClient;
    protected readonly OrchestraDbContext DbContext;
    protected readonly ILogger Logger;

    protected IntegrationTestBase(TestWebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Output = output ?? throw new ArgumentNullException(nameof(output));

        // Create test scope for services
        TestScope = Factory.Services.CreateScope();

        // Get mock services
        MockExecutor = TestScope.ServiceProvider.GetRequiredService<MockAgentExecutor>();
        AgentRegistry = TestScope.ServiceProvider.GetRequiredService<TestAgentRegistry>();

        // Get orchestration services
        HangfireOrchestrator = TestScope.ServiceProvider.GetRequiredService<HangfireOrchestrator>();
        JobClient = TestScope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
        DbContext = TestScope.ServiceProvider.GetRequiredService<OrchestraDbContext>();

        // Get logger
        var loggerFactory = TestScope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        Logger = loggerFactory.CreateLogger(GetType());

        // Initialize database for tests
        InitializeTestDatabase();

        Output.WriteLine($"Integration test setup completed for {GetType().Name}");
    }

    /// <summary>
    /// Initializes test database with required schema
    /// Each test class gets its own database via IClassFixture<TestWebApplicationFactory>
    /// NO data clearing needed - complete isolation between test classes
    /// </summary>
    private void InitializeTestDatabase()
    {
        try
        {
            // Create database if it doesn't exist (first test in this class)
            // Each test class has its own database via IClassFixture
            var created = DbContext.Database.EnsureCreated();

            if (created)
            {
                Output.WriteLine($"Test database created successfully for class {GetType().Name}");
            }
            else
            {
                Output.WriteLine($"Test database already exists for class {GetType().Name} (shared within class)");
            }
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Failed to initialize test database: {ex.Message}");
            Output.WriteLine($"Stack trace: {ex.StackTrace}");
            throw new InvalidOperationException("Could not initialize test database", ex);
        }
    }

    /// <summary>
    /// Creates a test agent with standard configuration
    /// </summary>
    public virtual async Task<string> CreateTestAgentAsync(
        string? agentId = null,
        string? repositoryPath = null,
        AgentBehavior behavior = AgentBehavior.Normal)
    {
        agentId ??= $"test-agent-{Guid.NewGuid().ToString("N")[..8]}";
        repositoryPath ??= @"C:\TestRepository";

        // Register agent in test registry for mock control
        AgentRegistry.RegisterTestAgent(agentId, $"Test Agent {agentId}", repositoryPath, behavior);

        // For tests, register directly with SimpleOrchestrator to ensure availability
        // since FindAvailableAgentAsync now checks SimpleOrchestrator first
        var simpleOrchestrator = TestScope.ServiceProvider.GetRequiredService<SimpleOrchestrator>();
        simpleOrchestrator.RegisterAgent(agentId, $"Test Agent {agentId}", "mock-test", repositoryPath);

        // Also try HangfireOrchestrator for completeness, but don't fail if it doesn't work
        try
        {
            await HangfireOrchestrator.RegisterAgentAsync(
                agentId,
                $"Test Agent {agentId}",
                "mock-test",
                repositoryPath);
        }
        catch (Exception ex)
        {
            // Expected in test environment - EF registration might fail
            Output.WriteLine($"HangfireOrchestrator registration failed (expected in tests): {ex.Message}");
        }

        Output.WriteLine($"Created test agent: {agentId} for repository: {repositoryPath}");
        return agentId;
    }

    /// <summary>
    /// Queues a test task and returns the task ID
    /// </summary>
    public virtual async Task<string> QueueTestTaskAsync(
        string command,
        string? repositoryPath = null,
        TaskPriority priority = TaskPriority.Normal)
    {
        repositoryPath ??= @"C:\TestRepository";

        try
        {
            Output.WriteLine($"Attempting to queue task - Command: {command}, Repository: {repositoryPath}");
            var taskId = await HangfireOrchestrator.QueueTaskAsync(command, repositoryPath, priority);
            Output.WriteLine($"Queued test task: {taskId} - Command: {command}");
            return taskId;
        }
        catch (Exception ex)
        {
            Output.WriteLine($"ERROR queuing task: {ex.Message}");
            Output.WriteLine($"Exception type: {ex.GetType().Name}");
            Output.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Waits for a task to complete with timeout and detailed logging
    /// </summary>
    public virtual async Task<bool> WaitForTaskCompletionAsync(
        string taskId,
        TimeSpan? timeout = null,
        TaskStatus[]? acceptableStatuses = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);
        acceptableStatuses ??= new[] { TaskStatus.Completed, TaskStatus.Failed };

        var startTime = DateTime.UtcNow;
        var maxWaitTime = startTime.Add(timeout.Value);

        Output.WriteLine($"Waiting for task {taskId} to complete (timeout: {timeout.Value.TotalSeconds}s)");

        while (DateTime.UtcNow < maxWaitTime)
        {
            // Check Hangfire job status
            try
            {
                var monitoringApi = JobStorage.Current.GetMonitoringApi();

                var succeededJobs = monitoringApi.SucceededJobs(0, 100);
                if (succeededJobs.Any(j => j.Value.Job.Args.Any(arg => arg?.ToString()?.Contains(taskId) == true)))
                {
                    Output.WriteLine($"Hangfire job for task {taskId} succeeded");
                    // Check database status to confirm the task completed with acceptable status
                    using (var scope = Factory.Services.CreateScope())
                    {
                        using var dbContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();
                        var task = await dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);

                        if (task != null && acceptableStatuses.Contains(task.Status))
                        {
                            var elapsed = DateTime.UtcNow - startTime;
                            Output.WriteLine($"Task {taskId} completed with status {task.Status} in {elapsed.TotalSeconds:F1}s");
                            return true;
                        }
                        else if (task != null)
                        {
                            Output.WriteLine($"Hangfire job succeeded but task status is {task.Status}, waiting for acceptable status...");
                        }
                    }
                }

                var failedJobs = monitoringApi.FailedJobs(0, 100);
                if (failedJobs.Any(j => j.Value.Job.Args.Any(arg => arg?.ToString()?.Contains(taskId) == true)))
                {
                    Output.WriteLine($"Hangfire job for task {taskId} failed");
                    return acceptableStatuses.Contains(TaskStatus.Failed);
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Could not check Hangfire status: {ex.Message}");
            }

            // Check database status
            using (var scope = Factory.Services.CreateScope())
            {
                using var dbContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();
                var task = await dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);

                if (task != null && acceptableStatuses.Contains(task.Status))
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    Output.WriteLine($"Task {taskId} completed with status {task.Status} in {elapsed.TotalSeconds:F1}s");
                    return true;
                }
            }

            await Task.Delay(500); // Check every 500ms
        }

        var totalElapsed = DateTime.UtcNow - startTime;
        Output.WriteLine($"Task {taskId} did not complete within {timeout.Value.TotalSeconds}s (waited {totalElapsed.TotalSeconds:F1}s)");
        return false;
    }

    /// <summary>
    /// Gets task status from database
    /// </summary>
    protected virtual async Task<TaskStatus?> GetTaskStatusAsync(string taskId)
    {
        using var scope = Factory.Services.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();

        var task = await dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
        return task?.Status;
    }

    /// <summary>
    /// Gets task details from database
    /// </summary>
    public virtual async Task<TaskRecord?> GetTaskAsync(string taskId)
    {
        using var scope = Factory.Services.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();

        return await dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
    }

    /// <summary>
    /// Sets up standard test environment with default agents
    /// </summary>
    protected virtual async Task SetupStandardTestEnvironmentAsync()
    {
        // Create standard test agents
        await CreateTestAgentAsync("standard-agent-1", @"C:\TestRepo1", AgentBehavior.Normal);
        await CreateTestAgentAsync("standard-agent-2", @"C:\TestRepo2", AgentBehavior.Normal);

        Output.WriteLine("Standard test environment setup completed");
    }

    public virtual void Dispose()
    {
        try
        {
            // Simplified disposal - just dispose scope
            // Factory cleanup happens automatically when test class is disposed (IClassFixture)
            TestScope?.Dispose();

            // Small delay to allow async operations to complete
            Thread.Sleep(100);
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Error during test disposal: {ex.Message}");
        }
    }
}