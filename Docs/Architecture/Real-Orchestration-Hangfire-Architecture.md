# Real Orchestration with Hangfire Architecture

## Architecture Overview

This document defines the comprehensive architecture for transforming the current in-memory task queuing system into a robust, persistent orchestration platform using Hangfire for background job processing. The system enables task queues that survive application restarts, comprehensive lifecycle management, retry logic, error handling, and an integrated dashboard for monitoring and management.

## System Context Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    AI Agent Orchestra - Hangfire Integration               │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    Web Interface Layer                              │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐    ┌──────────────────────────────────────┐   │   │
│  │  │ Task Management │    │        Hangfire Dashboard           │   │   │
│  │  │ Components      │    │  - Job Monitoring                   │   │   │
│  │  │ - TaskQueue     │    │  - Performance Metrics             │   │   │
│  │  │ - QuickActions  │    │  - Failed Job Recovery              │   │   │
│  │  │ - AgentSidebar  │    │  - Real-time Status                 │   │   │
│  │  └─────────────────┘    └──────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                   │                                         │
│                                   ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                      API Layer                                      │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐    ┌──────────────────────────────────────┐   │   │
│  │  │ OrchestratorService ││       HangfireOrchestrator           │   │   │
│  │  │ (Compatibility) │    │  - Job Creation & Scheduling         │   │   │
│  │  │ - Legacy API    │    │  - Task Lifecycle Management        │   │   │
│  │  │ - Migration     │    │  - Agent Assignment Logic           │   │   │
│  │  │   Support       │    │  - Retry & Error Handling           │   │   │
│  │  └─────────────────┘    └──────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                   │                                         │
│                                   ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                   Hangfire Background Services                      │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐    │   │
│  │  │ TaskExecution   │  │ AgentHealthCheck│  │ TaskCleanup     │    │   │
│  │  │ Job             │  │ Job             │  │ Job             │    │   │
│  │  │ - Agent Command │  │ - Availability  │  │ - Expired Task  │    │   │
│  │  │   Execution     │  │   Monitoring    │  │   Cleanup       │    │   │
│  │  │ - Progress      │  │ - Health Status │  │ - Performance   │    │   │
│  │  │   Reporting     │  │   Updates       │  │   Monitoring    │    │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘    │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐    │   │
│  │  │ WorkflowExecution│ │ PerformanceMonitor│ │ NotificationService │ │   │
│  │  │ Job             │  │ Job             │  │ Job             │    │   │
│  │  │ - Batch         │  │ - Metrics       │  │ - Alerts        │    │   │
│  │  │   Operations    │  │   Collection    │  │ - Status        │    │   │
│  │  │ - Dependencies  │  │ - Analytics     │  │   Updates       │    │   │
│  │  │ - Conditionals  │  │ - Reporting     │  │ - Notifications │    │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘    │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                   │                                         │
│                                   ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                        Persistence Layer                            │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐    ┌──────────────────────────────────────┐   │   │
│  │  │ SQLite Database │    │        Hangfire Storage              │   │   │
│  │  │ - Agent State   │    │  - Job Definitions                   │   │   │
│  │  │ - Task History  │    │  - Queue Management                  │   │   │
│  │  │ - Configuration │    │  - Job Status & History              │   │   │
│  │  │ - Analytics     │    │  - Retry & Schedule Information      │   │   │
│  │  └─────────────────┘    └──────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                   │                                         │
│                                   ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                      Agent Execution Layer                          │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐    ┌──────────────────────────────────────┐   │   │
│  │  │ Claude Agents   │    │       Agent Communication           │   │   │
│  │  │ - Command       │    │  - Status Broadcasting              │   │   │
│  │  │   Processing    │    │  - Progress Reporting               │   │   │
│  │  │ - File          │    │  - Error Handling                   │   │   │
│  │  │   Operations    │    │  - Result Collection                │   │   │
│  │  │ - Repository    │    │                                      │   │   │
│  │  │   Management    │    │                                      │   │   │
│  │  └─────────────────┘    └──────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Hangfire Infrastructure Architecture

### 1. Hangfire Configuration and Setup

```csharp
public class HangfireConfiguration
{
    public static void ConfigureHangfire(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var hangfireSettings = configuration.GetSection("Hangfire").Get<HangfireSettings>();

        // Configure Hangfire with SQLite storage
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSQLiteStorage(connectionString, new SQLiteStorageOptions
            {
                QueuePollInterval = TimeSpan.FromSeconds(hangfireSettings.QueuePollIntervalSeconds),
                JobExpirationCheckInterval = TimeSpan.FromHours(hangfireSettings.JobExpirationCheckIntervalHours),
                CountersAggregateInterval = TimeSpan.FromMinutes(hangfireSettings.CountersAggregateIntervalMinutes),
                PrepareSchemaIfNecessary = hangfireSettings.PrepareSchemaIfNecessary,
                DashboardJobListLimit = hangfireSettings.DashboardJobListLimit,
                TransactionTimeout = TimeSpan.FromMinutes(hangfireSettings.TransactionTimeoutMinutes)
            }));

        // Configure Hangfire server
        services.AddHangfireServer(options =>
        {
            options.Queues = hangfireSettings.Queues;
            options.WorkerCount = hangfireSettings.WorkerCount ?? Environment.ProcessorCount * 2;
            options.ServerName = $"{Environment.MachineName}-{Environment.ProcessId}";
            options.SchedulePollingInterval = TimeSpan.FromSeconds(hangfireSettings.SchedulePollingIntervalSeconds);
            options.ServerTimeout = TimeSpan.FromMinutes(hangfireSettings.ServerTimeoutMinutes);
            options.ServerCheckInterval = TimeSpan.FromMinutes(hangfireSettings.ServerCheckIntervalMinutes);
            options.HeartbeatInterval = TimeSpan.FromSeconds(hangfireSettings.HeartbeatIntervalSeconds);
            options.ShutdownTimeout = TimeSpan.FromMinutes(hangfireSettings.ShutdownTimeoutMinutes);
        });

        // Register custom services
        services.AddScoped<IHangfireOrchestrator, HangfireOrchestrator>();
        services.AddScoped<ITaskExecutionService, TaskExecutionService>();
        services.AddScoped<IWorkflowOrchestrator, WorkflowOrchestrator>();
        services.AddScoped<IJobRetryPolicyProvider, JobRetryPolicyProvider>();
        services.AddScoped<IJobPerformanceMonitor, JobPerformanceMonitor>();

        // Configure job filters
        ConfigureJobFilters(hangfireSettings);
    }

    private static void ConfigureJobFilters(HangfireSettings settings)
    {
        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
        {
            Attempts = settings.DefaultRetryAttempts,
            DelaysInSeconds = settings.DefaultRetryDelaysInSeconds,
            LogEvents = true,
            OnAttemptsExceeded = AttemptsExceededAction.Fail
        });

        GlobalJobFilters.Filters.Add(new JobTimeoutAttribute(TimeSpan.FromMinutes(settings.DefaultJobTimeoutMinutes)));
        GlobalJobFilters.Filters.Add(new LogJobExecutionFilter());
        GlobalJobFilters.Filters.Add(new PerformanceMonitoringFilter());
        GlobalJobFilters.Filters.Add(new AgentAvailabilityFilter());
        GlobalJobFilters.Filters.Add(new SecurityValidationFilter());
    }
}

public class HangfireSettings
{
    public string[] Queues { get; set; } = { "default", "high-priority", "long-running", "maintenance" };
    public int? WorkerCount { get; set; }
    public int QueuePollIntervalSeconds { get; set; } = 15;
    public int SchedulePollingIntervalSeconds { get; set; } = 15;
    public int ServerTimeoutMinutes { get; set; } = 5;
    public int ServerCheckIntervalMinutes { get; set; } = 1;
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int ShutdownTimeoutMinutes { get; set; } = 2;
    public int JobExpirationCheckIntervalHours { get; set; } = 1;
    public int CountersAggregateIntervalMinutes { get; set; } = 5;
    public bool PrepareSchemaIfNecessary { get; set; } = true;
    public int DashboardJobListLimit { get; set; } = 50000;
    public int TransactionTimeoutMinutes { get; set; } = 5;
    public int DefaultRetryAttempts { get; set; } = 3;
    public int[] DefaultRetryDelaysInSeconds { get; set; } = { 60, 300, 900 };
    public int DefaultJobTimeoutMinutes { get; set; } = 30;
}
```

### 2. Job Execution Architecture

```csharp
public interface ITaskExecutionJob
{
    Task ExecuteAsync(
        string taskId,
        string agentId,
        string command,
        string repositoryPath,
        TaskPriority priority,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
}

[Queue("default")]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
[JobTimeout(30)] // 30 minutes
public class TaskExecutionJob : ITaskExecutionJob
{
    private readonly IAgentCommunicationService _agentService;
    private readonly IHubContext<AgentCommunicationHub> _hubContext;
    private readonly ITaskRepository _taskRepository;
    private readonly IJobPerformanceMonitor _performanceMonitor;
    private readonly ILogger<TaskExecutionJob> _logger;

    public async Task ExecuteAsync(
        string taskId,
        string agentId,
        string command,
        string repositoryPath,
        TaskPriority priority,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var jobId = BackgroundJob.CurrentId;
        var correlationId = Guid.NewGuid().ToString();
        var performanceTracker = _performanceMonitor.StartTracking(jobId, taskId);

        try
        {
            _logger.LogInformation("Starting task execution - Task: {TaskId}, Agent: {AgentId}, Job: {JobId}",
                taskId, agentId, jobId);

            // 1. Validate and prepare execution context
            var executionContext = await PrepareExecutionContextAsync(taskId, agentId, command, repositoryPath, parameters);

            // 2. Update job status to processing
            await UpdateJobProgressAsync(jobId, taskId, 0, "Initializing task execution");

            // 3. Validate agent availability and capability
            var agent = await ValidateAndReserveAgentAsync(agentId, repositoryPath, cancellationToken);

            // 4. Update task status in database
            await _taskRepository.UpdateTaskStatusAsync(taskId, TaskStatus.InProgress, agentId, DateTime.UtcNow);

            // 5. Execute command with comprehensive monitoring
            var result = await ExecuteCommandWithMonitoringAsync(
                executionContext,
                performanceTracker,
                cancellationToken);

            // 6. Process and validate execution result
            var processedResult = await ProcessExecutionResultAsync(taskId, result, performanceTracker);

            // 7. Update final status and broadcast completion
            await CompleteTaskExecutionAsync(jobId, taskId, agentId, processedResult);

            _logger.LogInformation("Task execution completed successfully - Task: {TaskId}, Duration: {Duration}ms",
                taskId, performanceTracker.GetElapsedMilliseconds());
        }
        catch (AgentUnavailableException ex)
        {
            await HandleAgentUnavailableAsync(jobId, taskId, agentId, ex);
            throw new BackgroundJobException("Agent unavailable", ex, shouldRetry: true);
        }
        catch (TaskTimeoutException ex)
        {
            await HandleTaskTimeoutAsync(jobId, taskId, agentId, ex);
            throw new BackgroundJobException("Task execution timeout", ex, shouldRetry: false);
        }
        catch (TaskExecutionException ex)
        {
            await HandleTaskExecutionErrorAsync(jobId, taskId, agentId, ex);
            throw new BackgroundJobException("Task execution failed", ex, shouldRetry: ex.IsRetriable);
        }
        catch (Exception ex)
        {
            await HandleUnexpectedErrorAsync(jobId, taskId, agentId, ex);
            throw new BackgroundJobException("Unexpected error during task execution", ex, shouldRetry: false);
        }
        finally
        {
            // Always release agent and clean up resources
            await ReleaseAgentAsync(agentId);
            performanceTracker.Complete();
        }
    }

    private async Task<TaskExecutionContext> PrepareExecutionContextAsync(
        string taskId,
        string agentId,
        string command,
        string repositoryPath,
        Dictionary<string, object>? parameters)
    {
        return new TaskExecutionContext
        {
            TaskId = taskId,
            AgentId = agentId,
            Command = command,
            RepositoryPath = repositoryPath,
            Parameters = parameters ?? new Dictionary<string, object>(),
            StartedAt = DateTime.UtcNow,
            TimeoutAt = DateTime.UtcNow.AddMinutes(25), // 5 minutes buffer before job timeout
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

    private async Task<Agent> ValidateAndReserveAgentAsync(string agentId, string repositoryPath, CancellationToken cancellationToken)
    {
        var agent = await _agentService.GetAgentAsync(agentId);

        if (agent == null)
        {
            throw new AgentUnavailableException($"Agent {agentId} not found");
        }

        if (agent.Status != AgentStatus.Idle)
        {
            throw new AgentUnavailableException($"Agent {agentId} is not available (Status: {agent.Status})");
        }

        // Validate agent can handle the repository
        if (!await _agentService.CanHandleRepositoryAsync(agentId, repositoryPath))
        {
            throw new AgentUnavailableException($"Agent {agentId} cannot handle repository {repositoryPath}");
        }

        // Reserve agent for this task
        await _agentService.UpdateAgentStatusAsync(agentId, AgentStatus.Busy, BackgroundJob.CurrentId);

        return agent;
    }

    private async Task<AgentExecutionResult> ExecuteCommandWithMonitoringAsync(
        TaskExecutionContext context,
        IPerformanceTracker performanceTracker,
        CancellationToken cancellationToken)
    {
        var progressHandler = new TaskProgressHandler(context.TaskId, UpdateJobProgressAsync);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(context.TimeoutAt - DateTime.UtcNow);

        var result = await _agentService.ExecuteCommandAsync(
            context.AgentId,
            context.Command,
            context.RepositoryPath,
            context.CorrelationId,
            progressHandler,
            timeoutCts.Token);

        performanceTracker.RecordMetric("command_execution_time", result.ExecutionDuration.TotalMilliseconds);
        performanceTracker.RecordMetric("output_size_bytes", result.Output?.Length ?? 0);

        return result;
    }

    private async Task UpdateJobProgressAsync(string jobId, string taskId, int percentage, string message, Dictionary<string, object>? metadata = null)
    {
        // Update Hangfire job parameters
        BackgroundJob.SetJobParameter(jobId, "Progress", percentage);
        BackgroundJob.SetJobParameter(jobId, "Status", message);
        BackgroundJob.SetJobParameter(jobId, "LastUpdate", DateTime.UtcNow.ToString("O"));

        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                BackgroundJob.SetJobParameter(jobId, $"Metadata_{kvp.Key}", kvp.Value);
            }
        }

        // Broadcast real-time updates via SignalR
        await _hubContext.Clients.All.SendAsync("JobProgressUpdate", new
        {
            JobId = jobId,
            TaskId = taskId,
            Progress = percentage,
            Message = message,
            Metadata = metadata,
            Timestamp = DateTime.UtcNow
        });

        // Update task in database
        await _taskRepository.UpdateTaskProgressAsync(taskId, percentage, message);

        _logger.LogDebug("Job progress updated - Job: {JobId}, Task: {TaskId}, Progress: {Progress}%, Status: {Status}",
            jobId, taskId, percentage, message);
    }

    private async Task CompleteTaskExecutionAsync(string jobId, string taskId, string agentId, ProcessedTaskResult result)
    {
        // Update task status in database
        await _taskRepository.CompleteTaskAsync(taskId, result.Status, result.Output, result.ErrorMessage, result.Metrics);

        // Set final job parameters
        BackgroundJob.SetJobParameter(jobId, "CompletedAt", DateTime.UtcNow.ToString("O"));
        BackgroundJob.SetJobParameter(jobId, "FinalStatus", result.Status.ToString());
        BackgroundJob.SetJobParameter(jobId, "ExecutionTimeMs", result.ExecutionDuration.TotalMilliseconds);

        // Broadcast completion notification
        await _hubContext.Clients.All.SendAsync("TaskCompleted", new
        {
            JobId = jobId,
            TaskId = taskId,
            AgentId = agentId,
            Status = result.Status,
            Result = result.Output,
            ExecutionTime = result.ExecutionDuration,
            Timestamp = DateTime.UtcNow
        });
    }
}
```

### 3. Workflow Orchestration Architecture

```csharp
public interface IWorkflowOrchestrator
{
    Task<string> ExecuteWorkflowAsync(WorkflowDefinition workflow, Dictionary<string, object>? parameters = null);
    Task<string> ExecuteBatchAsync(List<TaskRequest> tasks, BatchExecutionOptions options);
    Task<WorkflowExecutionStatus> GetWorkflowStatusAsync(string workflowId);
    Task CancelWorkflowAsync(string workflowId);
    Task PauseWorkflowAsync(string workflowId);
    Task ResumeWorkflowAsync(string workflowId);
}

public class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IDependencyResolver _dependencyResolver;
    private readonly ILogger<WorkflowOrchestrator> _logger;

    public async Task<string> ExecuteWorkflowAsync(WorkflowDefinition workflow, Dictionary<string, object>? parameters = null)
    {
        var workflowExecutionId = Guid.NewGuid().ToString();

        _logger.LogInformation("Starting workflow execution - Workflow: {WorkflowName}, ID: {WorkflowExecutionId}",
            workflow.Name, workflowExecutionId);

        try
        {
            // 1. Validate workflow definition
            var validationResult = await ValidateWorkflowAsync(workflow);
            if (!validationResult.IsValid)
            {
                throw new WorkflowValidationException($"Workflow validation failed: {string.Join(", ", validationResult.Errors)}");
            }

            // 2. Resolve parameters and prepare execution context
            var resolvedParameters = await ResolveWorkflowParametersAsync(workflow, parameters);
            var executionContext = new WorkflowExecutionContext
            {
                WorkflowExecutionId = workflowExecutionId,
                WorkflowDefinition = workflow,
                Parameters = resolvedParameters,
                StartedAt = DateTime.UtcNow,
                Status = WorkflowExecutionStatus.Running
            };

            // 3. Save workflow execution record
            await _workflowRepository.SaveWorkflowExecutionAsync(executionContext);

            // 4. Build execution plan based on workflow stages
            var executionPlan = await BuildWorkflowExecutionPlanAsync(workflow, resolvedParameters);

            // 5. Execute workflow stages
            await ExecuteWorkflowStagesAsync(executionContext, executionPlan);

            _logger.LogInformation("Workflow execution started successfully - Workflow: {WorkflowName}, ID: {WorkflowExecutionId}",
                workflow.Name, workflowExecutionId);

            return workflowExecutionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start workflow execution - Workflow: {WorkflowName}",
                workflow.Name);

            await _workflowRepository.UpdateWorkflowExecutionStatusAsync(
                workflowExecutionId,
                WorkflowExecutionStatus.Failed,
                ex.Message);

            throw;
        }
    }

    public async Task<string> ExecuteBatchAsync(List<TaskRequest> tasks, BatchExecutionOptions options)
    {
        var batchId = Guid.NewGuid().ToString();

        _logger.LogInformation("Starting batch execution - Batch: {BatchId}, Tasks: {TaskCount}",
            batchId, tasks.Count);

        try
        {
            // 1. Validate batch request
            var validationResult = await ValidateBatchRequestAsync(tasks, options);
            if (!validationResult.IsValid)
            {
                throw new BatchValidationException($"Batch validation failed: {string.Join(", ", validationResult.Errors)}");
            }

            // 2. Build dependency graph
            var dependencyGraph = await _dependencyResolver.BuildDependencyGraphAsync(tasks);

            // 3. Create batch execution record
            var batchExecution = new BatchExecution
            {
                Id = batchId,
                Tasks = tasks,
                Options = options,
                DependencyGraph = dependencyGraph,
                Status = BatchExecutionStatus.Running,
                StartedAt = DateTime.UtcNow
            };

            await _workflowRepository.SaveBatchExecutionAsync(batchExecution);

            // 4. Execute tasks according to dependency graph
            await ExecuteBatchTasksAsync(batchExecution);

            return batchId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start batch execution - Batch: {BatchId}", batchId);
            throw;
        }
    }

    private async Task ExecuteWorkflowStagesAsync(WorkflowExecutionContext context, WorkflowExecutionPlan plan)
    {
        var stageResults = new Dictionary<string, StageExecutionResult>();

        foreach (var stage in plan.Stages)
        {
            try
            {
                _logger.LogInformation("Executing workflow stage - Workflow: {WorkflowExecutionId}, Stage: {StageName}",
                    context.WorkflowExecutionId, stage.Name);

                var stageResult = await ExecuteWorkflowStageAsync(context, stage, stageResults);
                stageResults[stage.Id] = stageResult;

                // Check if stage failed and workflow should stop
                if (stageResult.Status == StageExecutionStatus.Failed && stage.StopOnFailure)
                {
                    await _workflowRepository.UpdateWorkflowExecutionStatusAsync(
                        context.WorkflowExecutionId,
                        WorkflowExecutionStatus.Failed,
                        $"Stage '{stage.Name}' failed: {stageResult.ErrorMessage}");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage execution failed - Workflow: {WorkflowExecutionId}, Stage: {StageName}",
                    context.WorkflowExecutionId, stage.Name);

                stageResults[stage.Id] = new StageExecutionResult
                {
                    StageId = stage.Id,
                    Status = StageExecutionStatus.Failed,
                    ErrorMessage = ex.Message,
                    CompletedAt = DateTime.UtcNow
                };

                if (stage.StopOnFailure)
                {
                    await _workflowRepository.UpdateWorkflowExecutionStatusAsync(
                        context.WorkflowExecutionId,
                        WorkflowExecutionStatus.Failed,
                        $"Stage '{stage.Name}' failed with exception: {ex.Message}");
                    return;
                }
            }
        }

        // All stages completed
        await _workflowRepository.UpdateWorkflowExecutionStatusAsync(
            context.WorkflowExecutionId,
            WorkflowExecutionStatus.Completed,
            "All workflow stages completed successfully");
    }

    private async Task<StageExecutionResult> ExecuteWorkflowStageAsync(
        WorkflowExecutionContext context,
        WorkflowStage stage,
        Dictionary<string, StageExecutionResult> previousResults)
    {
        var stageJobIds = new List<string>();

        if (stage.ExecutionType == StageExecutionType.Sequential)
        {
            // Execute tasks sequentially
            string? previousJobId = null;

            foreach (var task in stage.Tasks)
            {
                var resolvedTask = await ResolveTaskParametersAsync(task, context.Parameters, previousResults);

                var jobId = previousJobId == null
                    ? _backgroundJobClient.Enqueue<TaskExecutionJob>(
                        x => x.ExecuteAsync(resolvedTask.Id, resolvedTask.AgentId, resolvedTask.Command,
                                          resolvedTask.RepositoryPath, resolvedTask.Priority, resolvedTask.Parameters, CancellationToken.None))
                    : _backgroundJobClient.ContinueJobWith<TaskExecutionJob>(
                        previousJobId,
                        x => x.ExecuteAsync(resolvedTask.Id, resolvedTask.AgentId, resolvedTask.Command,
                                          resolvedTask.RepositoryPath, resolvedTask.Priority, resolvedTask.Parameters, CancellationToken.None));

                stageJobIds.Add(jobId);
                previousJobId = jobId;

                // Store job metadata
                BackgroundJob.SetJobParameter(jobId, "WorkflowExecutionId", context.WorkflowExecutionId);
                BackgroundJob.SetJobParameter(jobId, "StageId", stage.Id);
                BackgroundJob.SetJobParameter(jobId, "StageName", stage.Name);
            }
        }
        else // Parallel execution
        {
            var parallelJobs = new List<string>();

            foreach (var task in stage.Tasks)
            {
                var resolvedTask = await ResolveTaskParametersAsync(task, context.Parameters, previousResults);

                var jobId = _backgroundJobClient.Enqueue<TaskExecutionJob>(
                    x => x.ExecuteAsync(resolvedTask.Id, resolvedTask.AgentId, resolvedTask.Command,
                                      resolvedTask.RepositoryPath, resolvedTask.Priority, resolvedTask.Parameters, CancellationToken.None));

                parallelJobs.Add(jobId);
                stageJobIds.Add(jobId);

                // Store job metadata
                BackgroundJob.SetJobParameter(jobId, "WorkflowExecutionId", context.WorkflowExecutionId);
                BackgroundJob.SetJobParameter(jobId, "StageId", stage.Id);
                BackgroundJob.SetJobParameter(jobId, "StageName", stage.Name);
            }

            // Create a continuation job that waits for all parallel jobs to complete
            if (parallelJobs.Count > 1)
            {
                var continuationJobId = _backgroundJobClient.ContinueJobWith(
                    parallelJobs.ToArray(),
                    () => _logger.LogInformation("Parallel stage '{StageName}' completed", stage.Name));

                stageJobIds.Add(continuationJobId);
            }
        }

        return new StageExecutionResult
        {
            StageId = stage.Id,
            Status = StageExecutionStatus.Running,
            JobIds = stageJobIds,
            StartedAt = DateTime.UtcNow
        };
    }
}
```

### 4. Job Retry and Error Handling Architecture

```csharp
public interface IJobRetryPolicyProvider
{
    RetryPolicy GetRetryPolicy(string jobType, TaskPriority priority);
    bool ShouldRetry(Exception exception, int attemptCount);
    TimeSpan CalculateRetryDelay(int attemptCount, RetryPolicy policy);
}

public class JobRetryPolicyProvider : IJobRetryPolicyProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JobRetryPolicyProvider> _logger;

    public RetryPolicy GetRetryPolicy(string jobType, TaskPriority priority)
    {
        return jobType switch
        {
            "TaskExecution" => priority switch
            {
                TaskPriority.Critical => new RetryPolicy
                {
                    MaxAttempts = 5,
                    BackoffStrategy = BackoffStrategy.Exponential,
                    BaseDelay = TimeSpan.FromSeconds(30),
                    MaxDelay = TimeSpan.FromMinutes(10),
                    RetriableExceptions = new[] { typeof(AgentUnavailableException), typeof(TransientNetworkException) }
                },
                TaskPriority.High => new RetryPolicy
                {
                    MaxAttempts = 3,
                    BackoffStrategy = BackoffStrategy.Exponential,
                    BaseDelay = TimeSpan.FromSeconds(60),
                    MaxDelay = TimeSpan.FromMinutes(15),
                    RetriableExceptions = new[] { typeof(AgentUnavailableException), typeof(TransientNetworkException) }
                },
                _ => new RetryPolicy
                {
                    MaxAttempts = 2,
                    BackoffStrategy = BackoffStrategy.Linear,
                    BaseDelay = TimeSpan.FromMinutes(2),
                    MaxDelay = TimeSpan.FromMinutes(30),
                    RetriableExceptions = new[] { typeof(AgentUnavailableException) }
                }
            },
            "AgentHealthCheck" => new RetryPolicy
            {
                MaxAttempts = 5,
                BackoffStrategy = BackoffStrategy.Exponential,
                BaseDelay = TimeSpan.FromSeconds(10),
                MaxDelay = TimeSpan.FromMinutes(5),
                RetriableExceptions = new[] { typeof(AgentUnavailableException), typeof(TimeoutException) }
            },
            "TaskCleanup" => new RetryPolicy
            {
                MaxAttempts = 3,
                BackoffStrategy = BackoffStrategy.Linear,
                BaseDelay = TimeSpan.FromMinutes(1),
                MaxDelay = TimeSpan.FromMinutes(10),
                RetriableExceptions = new[] { typeof(DatabaseException), typeof(FileSystemException) }
            },
            _ => GetDefaultRetryPolicy()
        };
    }

    public bool ShouldRetry(Exception exception, int attemptCount)
    {
        // Never retry these exceptions
        var nonRetriableExceptions = new[]
        {
            typeof(SecurityException),
            typeof(AuthenticationException),
            typeof(AuthorizationException),
            typeof(ArgumentException),
            typeof(InvalidOperationException),
            typeof(TaskCancelledException)
        };

        if (nonRetriableExceptions.Contains(exception.GetType()))
        {
            return false;
        }

        // Always retry these exceptions (up to max attempts)
        var alwaysRetriableExceptions = new[]
        {
            typeof(AgentUnavailableException),
            typeof(TransientNetworkException),
            typeof(TemporaryResourceException)
        };

        if (alwaysRetriableExceptions.Contains(exception.GetType()))
        {
            return true;
        }

        // For other exceptions, check if they contain retriable indicators
        var exceptionMessage = exception.Message.ToLowerInvariant();
        var retriableKeywords = new[] { "timeout", "temporary", "transient", "unavailable", "busy" };

        return retriableKeywords.Any(keyword => exceptionMessage.Contains(keyword));
    }

    public TimeSpan CalculateRetryDelay(int attemptCount, RetryPolicy policy)
    {
        TimeSpan delay = policy.BackoffStrategy switch
        {
            BackoffStrategy.Linear => TimeSpan.FromMilliseconds(policy.BaseDelay.TotalMilliseconds * attemptCount),
            BackoffStrategy.Exponential => TimeSpan.FromMilliseconds(policy.BaseDelay.TotalMilliseconds * Math.Pow(2, attemptCount - 1)),
            BackoffStrategy.Fixed => policy.BaseDelay,
            _ => policy.BaseDelay
        };

        // Cap the delay at maximum
        return delay > policy.MaxDelay ? policy.MaxDelay : delay;
    }
}

// Custom job filters for comprehensive error handling
public class LogJobExecutionFilter : JobFilterAttribute, IServerFilter
{
    private readonly ILogger<LogJobExecutionFilter> _logger;

    public LogJobExecutionFilter()
    {
        _logger = LogManager.GetLogger<LogJobExecutionFilter>();
    }

    public void OnPerforming(PerformingContext filterContext)
    {
        var jobId = filterContext.BackgroundJob.Id;
        var jobType = filterContext.BackgroundJob.Job.Type.Name;
        var methodName = filterContext.BackgroundJob.Job.Method.Name;

        _logger.LogInformation("Job execution starting - ID: {JobId}, Type: {JobType}, Method: {MethodName}",
            jobId, jobType, methodName);

        filterContext.Items["StartTime"] = DateTime.UtcNow;
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        var jobId = filterContext.BackgroundJob.Id;
        var startTime = (DateTime)filterContext.Items["StartTime"];
        var duration = DateTime.UtcNow - startTime;
        var success = filterContext.Exception == null;

        if (success)
        {
            _logger.LogInformation("Job execution completed successfully - ID: {JobId}, Duration: {Duration}ms",
                jobId, duration.TotalMilliseconds);
        }
        else
        {
            _logger.LogError(filterContext.Exception, "Job execution failed - ID: {JobId}, Duration: {Duration}ms",
                jobId, duration.TotalMilliseconds);
        }

        // Record execution metrics
        BackgroundJob.SetJobParameter(jobId, "ExecutionDuration", duration.TotalMilliseconds);
        BackgroundJob.SetJobParameter(jobId, "Success", success);
        BackgroundJob.SetJobParameter(jobId, "CompletedAt", DateTime.UtcNow.ToString("O"));
    }
}

public class PerformanceMonitoringFilter : JobFilterAttribute, IServerFilter
{
    private readonly IJobPerformanceMonitor _performanceMonitor;

    public void OnPerforming(PerformingContext filterContext)
    {
        _performanceMonitor.JobStarted(filterContext.BackgroundJob.Id, filterContext.BackgroundJob.Job.Type.Name);
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        _performanceMonitor.JobCompleted(
            filterContext.BackgroundJob.Id,
            filterContext.Exception == null,
            filterContext.Exception?.GetType().Name);
    }
}
```

### 5. Dashboard Integration and Monitoring

```csharp
public class HangfireDashboardConfiguration
{
    public static void ConfigureDashboard(IApplicationBuilder app, IConfiguration configuration)
    {
        var dashboardOptions = new DashboardOptions
        {
            Authorization = new[] { new HangfireAuthorizationFilter() },
            DashboardTitle = "AI Agent Orchestra - Job Dashboard",
            StatsPollingInterval = 2000,
            DisplayStorageConnectionString = false,
            DarkModeEnabled = true,
            DefaultRecordsPerPage = 20
        };

        // Add custom dashboard pages
        DashboardRoutes.Routes.Add("/agents", new AgentStatusDashboardPage());
        DashboardRoutes.Routes.Add("/workflows", new WorkflowDashboardPage());
        DashboardRoutes.Routes.Add("/performance", new PerformanceDashboardPage());

        app.UseHangfireDashboard("/hangfire", dashboardOptions);
    }
}

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // For development: allow all access
        // For production: implement proper authorization
        var httpContext = context.GetHttpContext();

        // Check if user is authenticated and has admin role
        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole("Administrator");
    }
}

// Custom dashboard pages for enhanced monitoring
public class AgentStatusDashboardPage : RazorPage
{
    private readonly IAgentRepository _agentRepository;

    public override async Task ExecuteAsync()
    {
        Layout = new LayoutPage("Agent Status");

        var agents = await _agentRepository.GetAllAsync();
        var agentMetrics = await GetAgentMetricsAsync();

        WriteLiteral($@"
            <div class='row'>
                <div class='col-md-12'>
                    <h3>Agent Status Overview</h3>
                    <div class='table-responsive'>
                        <table class='table table-hover'>
                            <thead>
                                <tr>
                                    <th>Agent ID</th>
                                    <th>Name</th>
                                    <th>Status</th>
                                    <th>Repository</th>
                                    <th>Current Task</th>
                                    <th>Last Ping</th>
                                    <th>Performance</th>
                                </tr>
                            </thead>
                            <tbody>
                                {string.Join("", agents.Select(GenerateAgentRow))}
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        ");
    }

    private string GenerateAgentRow(Agent agent)
    {
        var statusBadge = agent.Status switch
        {
            AgentStatus.Idle => "<span class='badge badge-success'>Idle</span>",
            AgentStatus.Busy => "<span class='badge badge-warning'>Busy</span>",
            AgentStatus.Offline => "<span class='badge badge-danger'>Offline</span>",
            _ => "<span class='badge badge-secondary'>Unknown</span>"
        };

        var lastPingFormatted = agent.LastPing.ToString("yyyy-MM-dd HH:mm:ss");
        var currentTask = string.IsNullOrEmpty(agent.CurrentTask) ? "-" : agent.CurrentTask;

        return $@"
            <tr>
                <td>{agent.Id}</td>
                <td>{agent.Name}</td>
                <td>{statusBadge}</td>
                <td>{agent.RepositoryPath}</td>
                <td>{currentTask}</td>
                <td>{lastPingFormatted}</td>
                <td>
                    <div class='progress'>
                        <div class='progress-bar' style='width: {agent.SuccessRate * 100}%'>
                            {agent.SuccessRate:P0}
                        </div>
                    </div>
                </td>
            </tr>
        ";
    }
}

public class WorkflowDashboardPage : RazorPage
{
    private readonly IWorkflowRepository _workflowRepository;

    public override async Task ExecuteAsync()
    {
        Layout = new LayoutPage("Workflow Status");

        var activeWorkflows = await _workflowRepository.GetActiveWorkflowExecutionsAsync();
        var workflowStats = await _workflowRepository.GetWorkflowStatisticsAsync(TimeSpan.FromDays(7));

        WriteLiteral($@"
            <div class='row'>
                <div class='col-md-4'>
                    <div class='card'>
                        <div class='card-header'>Workflow Statistics (7 days)</div>
                        <div class='card-body'>
                            <p>Total Executions: {workflowStats.TotalExecutions}</p>
                            <p>Successful: {workflowStats.SuccessfulExecutions}</p>
                            <p>Failed: {workflowStats.FailedExecutions}</p>
                            <p>Success Rate: {workflowStats.SuccessRate:P1}</p>
                        </div>
                    </div>
                </div>
                <div class='col-md-8'>
                    <h3>Active Workflows</h3>
                    <div class='table-responsive'>
                        <table class='table table-hover'>
                            <thead>
                                <tr>
                                    <th>Workflow ID</th>
                                    <th>Name</th>
                                    <th>Status</th>
                                    <th>Started</th>
                                    <th>Progress</th>
                                    <th>Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {string.Join("", activeWorkflows.Select(GenerateWorkflowRow))}
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        ");
    }
}
```

## Performance and Scaling Architecture

### 1. Database Optimization for Hangfire

```csharp
public class HangfirePerformanceOptimizer
{
    public static void OptimizeDatabase(IServiceCollection services, string connectionString)
    {
        services.Configure<SQLiteStorageOptions>(options =>
        {
            // Connection pooling
            options.PrepareSchemaIfNecessary = true;
            options.QueuePollInterval = TimeSpan.FromSeconds(1); // Faster polling for responsiveness
            options.InvisibilityTimeout = TimeSpan.FromMinutes(5);

            // Performance optimizations
            options.JobExpirationCheckInterval = TimeSpan.FromHours(1);
            options.CountersAggregateInterval = TimeSpan.FromMinutes(5);
            options.DashboardJobListLimit = 50000;

            // Memory management
            options.TransactionTimeout = TimeSpan.FromMinutes(1);
        });

        // Custom connection factory for connection pooling
        services.AddSingleton<IDbConnectionFactory>(provider =>
            new SQLiteConnectionFactory(connectionString, ConfigureSQLiteConnection));
    }

    private static void ConfigureSQLiteConnection(SQLiteConnection connection)
    {
        // Performance pragmas
        connection.Execute("PRAGMA journal_mode=WAL");
        connection.Execute("PRAGMA synchronous=NORMAL");
        connection.Execute("PRAGMA cache_size=10000");
        connection.Execute("PRAGMA temp_store=MEMORY");
        connection.Execute("PRAGMA mmap_size=268435456"); // 256MB
    }

    // Cleanup jobs for performance maintenance
    [AutomaticRetry(Attempts = 2)]
    [Queue("maintenance")]
    public class DatabaseMaintenanceJob
    {
        public async Task OptimizeDatabaseAsync()
        {
            using var connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();

            // Vacuum database to reclaim space
            await connection.ExecuteAsync("VACUUM");

            // Analyze tables for query optimization
            await connection.ExecuteAsync("ANALYZE");

            // Cleanup old job records
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            await connection.ExecuteAsync(@"
                DELETE FROM HangFire.Job
                WHERE CreatedAt < @CutoffDate
                AND StateName IN ('Succeeded', 'Deleted')",
                new { CutoffDate = cutoffDate });
        }
    }
}
```

### 2. Memory Management and Resource Optimization

```csharp
public class HangfireResourceManager
{
    private readonly IMemoryMonitor _memoryMonitor;
    private readonly IJobLoadBalancer _loadBalancer;
    private readonly ILogger<HangfireResourceManager> _logger;

    // Dynamic worker scaling based on system resources
    public async Task OptimizeWorkerCountAsync()
    {
        var systemMetrics = await _memoryMonitor.GetSystemMetricsAsync();
        var currentLoad = await _loadBalancer.GetCurrentLoadAsync();

        var optimalWorkerCount = CalculateOptimalWorkerCount(systemMetrics, currentLoad);

        if (optimalWorkerCount != GetCurrentWorkerCount())
        {
            await AdjustWorkerCountAsync(optimalWorkerCount);
            _logger.LogInformation("Adjusted worker count to {WorkerCount} based on system metrics", optimalWorkerCount);
        }
    }

    private int CalculateOptimalWorkerCount(SystemMetrics metrics, LoadMetrics load)
    {
        // Base worker count on CPU cores
        var baseWorkerCount = Environment.ProcessorCount;

        // Adjust based on memory usage
        if (metrics.MemoryUsagePercent > 85)
        {
            baseWorkerCount = Math.Max(1, baseWorkerCount / 2);
        }
        else if (metrics.MemoryUsagePercent < 50)
        {
            baseWorkerCount = Math.Min(baseWorkerCount * 2, 16);
        }

        // Adjust based on current load
        if (load.QueueLength > 100)
        {
            baseWorkerCount = Math.Min(baseWorkerCount * 2, 20);
        }
        else if (load.QueueLength < 10)
        {
            baseWorkerCount = Math.Max(1, baseWorkerCount / 2);
        }

        return Math.Max(1, Math.Min(baseWorkerCount, 20)); // Cap at 20 workers
    }

    // Job queue optimization
    public async Task OptimizeJobQueuesAsync()
    {
        var queueStats = await GetQueueStatisticsAsync();

        foreach (var queue in queueStats)
        {
            if (queue.Value.AverageProcessingTime > TimeSpan.FromMinutes(5) && queue.Value.Length > 50)
            {
                // Move long-running jobs to dedicated queue
                await MoveJobsToLongRunningQueueAsync(queue.Key);
            }

            if (queue.Value.FailureRate > 0.2) // 20% failure rate
            {
                // Increase retry delays for problematic queue
                await AdjustRetryPolicyAsync(queue.Key, increase: true);
            }
        }
    }
}
```

## Security Architecture

### 1. Job Execution Security

```csharp
public class JobSecurityService
{
    private readonly ISecurityValidator _securityValidator;
    private readonly IAuditLogger _auditLogger;

    public async Task ValidateJobExecutionAsync(PerformingContext context)
    {
        var jobType = context.BackgroundJob.Job.Type;
        var methodName = context.BackgroundJob.Job.Method.Name;
        var arguments = context.BackgroundJob.Job.Args;

        // Validate job type is allowed
        if (!IsAllowedJobType(jobType))
        {
            throw new SecurityException($"Job type {jobType.Name} is not allowed for execution");
        }

        // Validate method parameters for security risks
        foreach (var arg in arguments)
        {
            if (arg is string stringArg && ContainsMaliciousContent(stringArg))
            {
                throw new SecurityException("Job parameters contain potentially malicious content");
            }
        }

        // Log job execution for audit
        await _auditLogger.LogJobExecutionAsync(new JobExecutionAuditRecord
        {
            JobId = context.BackgroundJob.Id,
            JobType = jobType.Name,
            MethodName = methodName,
            ExecutedAt = DateTime.UtcNow,
            UserId = GetCurrentUserId(),
            Parameters = SerializeParameters(arguments)
        });
    }

    private bool IsAllowedJobType(Type jobType)
    {
        var allowedJobTypes = new[]
        {
            typeof(TaskExecutionJob),
            typeof(AgentHealthCheckJob),
            typeof(TaskCleanupJob),
            typeof(WorkflowExecutionJob),
            typeof(PerformanceMonitoringJob),
            typeof(DatabaseMaintenanceJob)
        };

        return allowedJobTypes.Contains(jobType) || jobType.IsSubclassOf(typeof(BaseSecureJob));
    }

    private bool ContainsMaliciousContent(string content)
    {
        var maliciousPatterns = new[]
        {
            @"<script[^>]*>",
            @"javascript:",
            @"vbscript:",
            @"data:text/html",
            @"eval\s*\(",
            @"exec\s*\(",
            @"system\s*\(",
            @"shell\s*\(",
            @"\brm\s+-rf",
            @"\bformat\s+c:",
            @">\s*/dev/null"
        };

        return maliciousPatterns.Any(pattern =>
            Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase));
    }
}

// Base class for secure job execution
public abstract class BaseSecureJob
{
    protected readonly ISecurityContext _securityContext;
    protected readonly ILogger _logger;

    protected BaseSecureJob(ISecurityContext securityContext, ILogger logger)
    {
        _securityContext = securityContext;
        _logger = logger;
    }

    protected async Task<T> ExecuteSecurelyAsync<T>(Func<Task<T>> operation, string operationName)
    {
        try
        {
            // Pre-execution security checks
            await ValidateSecurityContextAsync();

            // Execute operation with monitoring
            _logger.LogInformation("Starting secure operation: {OperationName}", operationName);
            var result = await operation();

            _logger.LogInformation("Completed secure operation: {OperationName}", operationName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Secure operation failed: {OperationName}", operationName);
            throw;
        }
    }

    private async Task ValidateSecurityContextAsync()
    {
        if (!_securityContext.IsValid)
        {
            throw new SecurityException("Invalid security context for job execution");
        }

        if (_securityContext.IsExpired)
        {
            throw new SecurityException("Security context has expired");
        }

        // Additional security validations as needed
    }
}
```

## Integration Points

### 1. Legacy System Compatibility

The Hangfire orchestration system maintains full compatibility with the existing SimpleOrchestrator while providing enhanced capabilities:

```csharp
public class OrchestratorServiceAdapter
{
    private readonly SimpleOrchestrator _legacyOrchestrator;
    private readonly IHangfireOrchestrator _hangfireOrchestrator;
    private readonly IFeatureToggleService _featureToggle;

    public async Task<bool> QueueTaskAsync(string command, string repositoryPath, TaskPriority priority = TaskPriority.Normal)
    {
        if (await _featureToggle.IsEnabledAsync("UseHangfireOrchestrator"))
        {
            var taskId = await _hangfireOrchestrator.QueueTaskAsync(command, repositoryPath, priority);
            return !string.IsNullOrEmpty(taskId);
        }
        else
        {
            return _legacyOrchestrator.QueueTask(command, repositoryPath, priority);
        }
    }

    // Gradual migration support
    public async Task MigratePendingTasksAsync()
    {
        var pendingTasks = _legacyOrchestrator.GetPendingTasks();

        foreach (var task in pendingTasks)
        {
            await _hangfireOrchestrator.QueueTaskAsync(
                task.Command,
                task.RepositoryPath,
                task.Priority);
        }

        _legacyOrchestrator.ClearPendingTasks();
    }
}
```

### 2. Cross-System Integration

The Hangfire system integrates seamlessly with all other planned components:

- **SQLite Database**: Task persistence and history tracking
- **SignalR Hub**: Real-time job status broadcasting
- **Agent Chat**: Interactive task execution monitoring
- **Actions Block**: Enhanced template execution with workflows
- **Performance Monitoring**: Comprehensive job analytics

This architecture provides a robust, scalable, and secure foundation for enterprise-grade task orchestration while maintaining backward compatibility and enabling smooth migration from the existing in-memory system.