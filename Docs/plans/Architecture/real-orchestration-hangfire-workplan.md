# Real Orchestration with Hangfire Work Plan

## Executive Summary

Transform the current in-memory task queuing system into a robust, persistent orchestration platform using Hangfire for background job processing, enabling task queues that survive application restarts, comprehensive lifecycle management, retry logic, error handling, and an integrated dashboard for monitoring and management.

## Current State Analysis

**Existing Task Management:**
- In-memory `Queue<TaskRequest>` in SimpleOrchestrator
- File-based state persistence (orchestrator-state.json)
- Basic task status tracking (Pending, Assigned, InProgress, Completed, Failed)
- Manual agent assignment and task distribution
- No retry mechanisms or error recovery

**Limitations:**
- Tasks lost on application restart
- No automatic retry on failures
- Limited error handling and logging
- Manual intervention required for stuck tasks
- No comprehensive monitoring or analytics

## Target Architecture

### 1. Hangfire Integration Architecture

**Core Components:**
```
Hangfire Dashboard (Built-in)
├── Job Monitoring & Management
├── Queue Performance Metrics
├── Failed Job Recovery
└── Real-time Job Status

HangfireOrchestrator (new)
├── Job Creation & Scheduling
├── Task Lifecycle Management
├── Agent Assignment Logic
└── Retry & Error Handling

Hangfire Background Services
├── TaskExecutionJob
├── AgentHealthCheckJob
├── TaskCleanupJob
└── PerformanceMonitoringJob
```

**Technology Stack:**
- **Hangfire.Core**: Background job processing
- **Hangfire.SQLite**: SQLite storage for lightweight desktop deployment
- **Hangfire.Dashboard**: Web-based monitoring
- **Custom Job Implementations**: Agent-specific task execution

### 2. Enhanced Task Lifecycle

**Job States:**
```
Created → Enqueued → Processing → Succeeded
    ↓         ↓         ↓         ↑
  Failed ← Failed ← Failed ← Retry ←
    ↓
  Deleted (after retention period)
```

**Advanced Features:**
- **Delayed Jobs**: Schedule tasks for future execution
- **Recurring Jobs**: Automated maintenance and monitoring
- **Continuation Jobs**: Chain dependent tasks
- **Batch Jobs**: Group related tasks with collective success/failure
- **Job Filters**: Logging, performance monitoring, custom behavior

### 3. Persistent Queue Management

**Queue Types:**
- **Default**: General task execution
- **High Priority**: Critical operations
- **Long Running**: Extended execution tasks
- **Agent Specific**: Per-agent task queues
- **Maintenance**: System housekeeping jobs

**Storage Strategy:**
- Primary storage in SQL database
- Agent state synchronization with database
- Task result caching and cleanup
- Performance metrics collection

## Implementation Phases

### Phase 1: Hangfire Infrastructure Setup (Estimated: 10-14 hours)

**Tasks:**
1. **Install and Configure Hangfire**
   - Add Hangfire packages to Orchestra.API project
   - Configure SQLite storage
   - Set up Hangfire dashboard and security
   - **Acceptance Criteria**: Hangfire dashboard accessible and functional

2. **Create Core Job Classes**
   - `TaskExecutionJob` - Primary task processing
   - `AgentHealthCheckJob` - Agent availability monitoring
   - `TaskCleanupJob` - Cleanup completed/expired tasks
   - **Acceptance Criteria**: Jobs can be enqueued and executed

3. **Implement HangfireOrchestrator**
   - Replace in-memory queue with Hangfire jobs
   - Maintain compatibility with existing OrchestratorService API
   - Add job state management and monitoring
   - **Acceptance Criteria**: Tasks execute through Hangfire with same external API

**Technical Implementation:**

**Startup.cs Configuration:**
```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Database configuration
    services.AddDbContext<OrchestraDbContext>(options =>
        options.UseSqlite(connectionString));

    // Hangfire configuration
    services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSQLiteStorage(connectionString, new SQLiteStorageOptions
        {
            QueuePollInterval = TimeSpan.Zero,
            JobExpirationCheckInterval = TimeSpan.FromHours(1),
            CountersAggregateInterval = TimeSpan.FromMinutes(5),
            PrepareSchemaIfNecessary = true,
            DashboardJobListLimit = 50000,
            TransactionTimeout = TimeSpan.FromMinutes(1)
        }));

    services.AddHangfireServer(options =>
    {
        options.Queues = new[] { "default", "high-priority", "long-running", "maintenance" };
        options.WorkerCount = Environment.ProcessorCount * 2;
    });

    // Custom services
    services.AddScoped<HangfireOrchestrator>();
    services.AddScoped<TaskExecutionService>();
    services.AddScoped<AgentCommunicationService>();
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // Existing configuration...

    // Hangfire Dashboard
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });

    // Schedule recurring jobs
    RecurringJob.AddOrUpdate<AgentHealthCheckJob>(
        "agent-health-check",
        x => x.ExecuteAsync(CancellationToken.None),
        Cron.Minutely);

    RecurringJob.AddOrUpdate<TaskCleanupJob>(
        "task-cleanup",
        x => x.ExecuteAsync(CancellationToken.None),
        Cron.Hourly);
}
```

### Phase 2: Task Execution Engine (Estimated: 12-16 hours)

**Tasks:**
1. **Implement TaskExecutionJob**
   - Execute agent commands through existing infrastructure
   - Handle real-time progress reporting
   - Implement comprehensive error handling
   - **Detailed Algorithm for Task Execution**:
     ```
     ALGORITHM: ExecuteTask(taskId, agentId, command, repositoryPath, priority)
     1. VALIDATE execution context:
        - VERIFY taskId is valid UUID and exists in database
        - CHECK agentId is valid and agent is available
        - VALIDATE command is non-empty and under 2000 characters
        - ENSURE repositoryPath exists and is accessible
     2. ACQUIRE agent lock:
        - SET agent status to "Busy" with atomic database update
        - STORE current job ID in agent record
        - SET timeout for agent lock (25 minutes max)
        - IF agent lock fails THROW AgentUnavailableException
     3. INITIALIZE execution tracking:
        - CREATE correlation ID for operation tracing
        - START performance timer for metrics
        - SETUP progress reporter with 5% increment updates
        - CREATE cancellation token with configured timeout
     4. PREPARE execution environment:
        - VALIDATE repository access permissions
        - SET working directory to repository path
        - CONFIGURE environment variables for agent context
        - SETUP output capture and logging
     5. EXECUTE command with monitoring:
        - LAUNCH agent command with timeout and cancellation
        - MONITOR progress every 30 seconds
        - CAPTURE stdout/stderr streams in real-time
        - REPORT progress updates via SignalR
        - HANDLE interruption signals gracefully
     6. PROCESS execution results:
        - CAPTURE final exit code and execution duration
        - EXTRACT structured results from agent output
        - VALIDATE output format and content
        - STORE results in database with proper encoding
     7. UPDATE task and agent status:
        - SET task status based on execution outcome
        - RELEASE agent lock and set status to "Idle"
        - UPDATE agent performance metrics
        - BROADCAST completion notification
     8. CLEANUP and audit:
        - DISPOSE of temporary resources
        - LOG execution metrics and outcome
        - TRIGGER any dependent job continuations
        - ARCHIVE execution logs if configured
     ```
   - **Error Handling Strategies**:
     - AgentUnavailableException → Retry after 60 seconds (max 3 attempts)
     - TaskTimeoutException → Cancel gracefully, save partial output
     - CommandExecutionException → Log error details, mark task as failed
     - RepositoryAccessException → Check permissions, retry once
     - DatabaseException → Retry with exponential backoff (max 5 attempts)
   - **Progress Reporting Algorithm**:
     ```
     ALGORITHM: ReportProgress(percentage, message, metadata)
     1. VALIDATE progress data:
        - percentage must be 0-100 integer
        - message must be under 500 characters
        - metadata must be serializable JSON
     2. UPDATE job parameters in Hangfire:
        - SET Progress parameter to percentage
        - SET Status parameter to message
        - SET LastUpdate parameter to current UTC timestamp
     3. BROADCAST via SignalR:
        - SEND to all clients in job group
        - INCLUDE job ID, task ID, agent ID
        - ADD ETA calculation based on current progress rate
     4. STORE in database:
        - UPDATE task progress in database
        - LOG progress event for audit trail
        - TRIGGER any progress-based notifications
     ```
   - **Acceptance Criteria**: Tasks execute with full logging, comprehensive error recovery, and accurate progress reporting

2. **Add Advanced Job Features**
   - Job continuation for dependent tasks
   - Batch job processing for related operations
   - Delayed job scheduling for future execution
   - **Acceptance Criteria**: Complex task workflows can be automated

3. **Integrate with SignalR for Real-time Updates**
   - Job progress broadcasting to connected clients
   - Real-time dashboard updates
   - Agent status synchronization
   - **Acceptance Criteria**: UI receives real-time job status updates

**TaskExecutionJob.cs:**
```csharp
public class TaskExecutionJob
{
    private readonly IAgentCommunicationService _agentService;
    private readonly IHubContext<AgentCommunicationHub> _hubContext;
    private readonly ILogger<TaskExecutionJob> _logger;

    public async Task ExecuteAsync(
        string taskId,
        string agentId,
        string command,
        string repositoryPath,
        TaskPriority priority,
        CancellationToken cancellationToken)
    {
        var jobId = BackgroundJob.CurrentId;
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            // Update job status to processing
            await UpdateJobProgress(jobId, 0, "Starting task execution");

            // Validate agent availability
            var agent = await _agentService.GetAgentAsync(agentId);
            if (agent == null || agent.Status != AgentStatus.Idle)
            {
                throw new InvalidOperationException($"Agent {agentId} is not available");
            }

            // Mark agent as busy
            await _agentService.UpdateAgentStatusAsync(agentId, AgentStatus.Busy, taskId);

            // Execute command
            var result = await _agentService.ExecuteCommandAsync(
                agentId, command, repositoryPath, correlationId, cancellationToken);

            // Process result and update progress
            await ProcessExecutionResult(jobId, taskId, result);

            // Broadcast completion
            await _hubContext.Clients.All.SendAsync(
                "TaskCompleted",
                new { TaskId = taskId, AgentId = agentId, Result = result },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task execution failed for task {TaskId}", taskId);

            // Update job with error details
            await UpdateJobProgress(jobId, 100, $"Failed: {ex.Message}");

            // Broadcast failure
            await _hubContext.Clients.All.SendAsync(
                "TaskFailed",
                new { TaskId = taskId, AgentId = agentId, Error = ex.Message },
                cancellationToken);

            throw; // Re-throw to trigger Hangfire retry
        }
        finally
        {
            // Release agent
            await _agentService.UpdateAgentStatusAsync(agentId, AgentStatus.Idle);
        }
    }

    private async Task UpdateJobProgress(string jobId, int percentage, string message)
    {
        BackgroundJob.SetJobParameter(jobId, "Progress", percentage);
        BackgroundJob.SetJobParameter(jobId, "Status", message);

        await _hubContext.Clients.All.SendAsync("JobProgressUpdate", new
        {
            JobId = jobId,
            Progress = percentage,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }
}
```

### Phase 3: Advanced Orchestration Features (Estimated: 14-18 hours)

**Tasks:**
1. **Implement Job Workflows**
   - Sequential task execution with dependencies
   - Parallel task execution for independent operations
   - Conditional task execution based on results
   - **Acceptance Criteria**: Complex workflows can be defined and executed automatically

2. **Add Retry and Error Recovery**
   - Configurable retry policies per task type
   - Exponential backoff for transient failures
   - Dead letter queue for permanently failed tasks
   - **Acceptance Criteria**: Tasks automatically retry with intelligent failure handling

3. **Create Performance Monitoring**
   - Task execution time tracking
   - Agent utilization metrics
   - Queue performance analytics
   - **Acceptance Criteria**: Comprehensive metrics available via dashboard and API

**WorkflowOrchestrator.cs:**
```csharp
public class WorkflowOrchestrator
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public async Task<string> ExecuteWorkflowAsync(WorkflowDefinition workflow)
    {
        var workflowBatch = new BatchJob();
        var jobIds = new List<string>();

        foreach (var stage in workflow.Stages)
        {
            if (stage.ExecutionType == StageExecutionType.Sequential)
            {
                string? previousJobId = null;
                foreach (var task in stage.Tasks)
                {
                    var jobId = previousJobId == null
                        ? _backgroundJobClient.Enqueue<TaskExecutionJob>(
                            x => x.ExecuteAsync(task.Id, task.AgentId, task.Command,
                                              task.RepositoryPath, task.Priority, CancellationToken.None))
                        : _backgroundJobClient.ContinueJobWith<TaskExecutionJob>(
                            previousJobId,
                            x => x.ExecuteAsync(task.Id, task.AgentId, task.Command,
                                              task.RepositoryPath, task.Priority, CancellationToken.None));

                    jobIds.Add(jobId);
                    previousJobId = jobId;
                }
            }
            else // Parallel execution
            {
                var parallelJobs = stage.Tasks.Select(task =>
                    _backgroundJobClient.Enqueue<TaskExecutionJob>(
                        x => x.ExecuteAsync(task.Id, task.AgentId, task.Command,
                                          task.RepositoryPath, task.Priority, CancellationToken.None))
                ).ToList();

                jobIds.AddRange(parallelJobs);

                // Wait for all parallel jobs to complete before next stage
                if (workflow.Stages.IndexOf(stage) < workflow.Stages.Count - 1)
                {
                    var waitJobId = _backgroundJobClient.ContinueJobWith(
                        parallelJobs.ToArray(),
                        () => Console.WriteLine($"Stage {stage.Name} completed"));
                    jobIds.Add(waitJobId);
                }
            }
        }

        return string.Join(",", jobIds);
    }
}
```

### Phase 4: Dashboard Integration & Monitoring (Estimated: 8-12 hours)

**Tasks:**
1. **Customize Hangfire Dashboard**
   - Add custom metrics and charts
   - Integrate with existing UI theme
   - Add agent-specific filtering and views
   - **Acceptance Criteria**: Dashboard provides comprehensive orchestration insights

2. **Create Orchestration Analytics**
   - Task success/failure rates over time
   - Agent performance and utilization tracking
   - Queue health and bottleneck identification
   - **Acceptance Criteria**: Analytics support operational decision making

3. **Implement Alerting System**
   - Failed job notifications
   - Agent unavailability alerts
   - Performance threshold breaches
   - **Acceptance Criteria**: Operations team receives timely alerts for issues

## Technical Specifications

### Database Schema

**Hangfire Tables** (Auto-created):
- `HangFire.Job` - Job definitions and status
- `HangFire.Queue` - Queue management
- `HangFire.Server` - Server instances
- `HangFire.Set` - Scheduled and delayed jobs
- `HangFire.Counter` - Performance counters
- `HangFire.Hash` - Job parameters and metadata
- `HangFire.List` - Queue contents

**Custom Extensions Tables:**
```sql
CREATE TABLE OrchestrationJobs (
    Id TEXT PRIMARY KEY,
    HangfireJobId TEXT NOT NULL,
    TaskId TEXT NOT NULL,
    AgentId TEXT NOT NULL,
    WorkflowId TEXT NULL,
    Priority INTEGER NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    StartedAt DATETIME NULL,
    CompletedAt DATETIME NULL,
    Status TEXT NOT NULL,
    Result TEXT NULL,
    ErrorMessage TEXT NULL,
    RetryCount INTEGER DEFAULT 0
);

CREATE INDEX IX_OrchestrationJobs_Status ON OrchestrationJobs(Status);
CREATE INDEX IX_OrchestrationJobs_AgentId ON OrchestrationJobs(AgentId);
CREATE INDEX IX_OrchestrationJobs_CreatedAt ON OrchestrationJobs(CreatedAt);

CREATE TABLE WorkflowDefinitions (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Definition TEXT NOT NULL, -- JSON workflow definition
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

### Job Configuration

**Retry Policies:**
```csharp
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
public class TaskExecutionJob : IJob
{
    // Implementation
}

[AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 10, 30, 60, 180, 300 })]
public class AgentHealthCheckJob : IJob
{
    // Implementation
}
```

**Queue Configuration:**
```csharp
public static class QueueNames
{
    public const string Default = "default";
    public const string HighPriority = "high-priority";
    public const string LongRunning = "long-running";
    public const string Maintenance = "maintenance";
    public const string AgentSpecific = "agent-{0}"; // Format with agent ID
}

public static class JobFilters
{
    public static void Configure()
    {
        GlobalJobFilters.Filters.Add(new LogJobExecutionFilter());
        GlobalJobFilters.Filters.Add(new PerformanceMonitoringFilter());
        GlobalJobFilters.Filters.Add(new AgentAvailabilityFilter());
    }
}
```

### Service Integration

**HangfireOrchestrator.cs:**
```csharp
public class HangfireOrchestrator : IOrchestrator
{
    private readonly IBackgroundJobClient _jobClient;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly OrchestraDbContext _dbContext;

    public async Task<string> QueueTaskAsync(string command, string repositoryPath,
        TaskPriority priority = TaskPriority.Normal)
    {
        var taskId = Guid.NewGuid().ToString();
        var availableAgent = await FindAvailableAgentAsync(repositoryPath);

        if (availableAgent == null)
        {
            throw new InvalidOperationException("No available agents for repository");
        }

        var queueName = priority switch
        {
            TaskPriority.Critical => QueueNames.HighPriority,
            TaskPriority.High => QueueNames.HighPriority,
            _ => QueueNames.Default
        };

        var jobId = _jobClient.Enqueue<TaskExecutionJob>(
            x => x.ExecuteAsync(taskId, availableAgent.Id, command,
                               repositoryPath, priority, CancellationToken.None),
            queueName);

        // Store orchestration metadata
        await _dbContext.OrchestrationJobs.AddAsync(new OrchestrationJob
        {
            Id = taskId,
            HangfireJobId = jobId,
            TaskId = taskId,
            AgentId = availableAgent.Id,
            Priority = (int)priority,
            CreatedAt = DateTime.UtcNow,
            Status = "Enqueued"
        });

        await _dbContext.SaveChangesAsync();
        return taskId;
    }

    public async Task<string> ScheduleTaskAsync(string command, string repositoryPath,
        DateTime executeAt, TaskPriority priority = TaskPriority.Normal)
    {
        var taskId = Guid.NewGuid().ToString();
        var availableAgent = await FindAvailableAgentAsync(repositoryPath);

        var jobId = _jobClient.Schedule<TaskExecutionJob>(
            x => x.ExecuteAsync(taskId, availableAgent.Id, command,
                               repositoryPath, priority, CancellationToken.None),
            executeAt);

        await StoreJobMetadataAsync(taskId, jobId, availableAgent.Id, priority, "Scheduled");
        return taskId;
    }

    public async Task<string> ExecuteWorkflowAsync(string workflowId,
        Dictionary<string, object> parameters)
    {
        var workflow = await _dbContext.WorkflowDefinitions
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.IsActive);

        if (workflow == null)
        {
            throw new ArgumentException($"Workflow {workflowId} not found");
        }

        var workflowDefinition = JsonSerializer.Deserialize<WorkflowDefinition>(workflow.Definition);
        var orchestrator = new WorkflowOrchestrator(_jobClient);

        return await orchestrator.ExecuteWorkflowAsync(workflowDefinition);
    }
}
```

## Integration with Existing Systems

### OrchestratorService Compatibility

**Migration Strategy:**
```csharp
public class OrchestratorService
{
    private readonly SimpleOrchestrator _legacyOrchestrator;
    private readonly HangfireOrchestrator _hangfireOrchestrator;
    private readonly IConfiguration _configuration;

    public async Task<bool> QueueTaskAsync(string command, string repositoryPath,
        TaskPriority priority = TaskPriority.Normal)
    {
        if (_configuration.GetValue<bool>("UseHangfire"))
        {
            var taskId = await _hangfireOrchestrator.QueueTaskAsync(command, repositoryPath, priority);
            return !string.IsNullOrEmpty(taskId);
        }
        else
        {
            _legacyOrchestrator.QueueTask(command, repositoryPath, priority);
            return true;
        }
    }
}
```

### SignalR Integration

**Real-time Job Updates:**
```csharp
public class JobProgressFilter : JobFilterAttribute, IServerFilter
{
    public void OnPerforming(PerformingContext filterContext)
    {
        var hubContext = filterContext.ServiceProvider
            .GetRequiredService<IHubContext<AgentCommunicationHub>>();

        var jobId = filterContext.BackgroundJob.Id;
        hubContext.Clients.All.SendAsync("JobStarted", new { JobId = jobId });
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        var hubContext = filterContext.ServiceProvider
            .GetRequiredService<IHubContext<AgentCommunicationHub>>();

        var jobId = filterContext.BackgroundJob.Id;
        var success = filterContext.Exception == null;

        hubContext.Clients.All.SendAsync("JobCompleted", new
        {
            JobId = jobId,
            Success = success,
            Error = filterContext.Exception?.Message
        });
    }
}
```

## Performance Considerations

### Scaling Configuration

**Worker Configuration:**
```csharp
services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount * 2; // CPU-bound tasks
    options.Queues = new[] { "high-priority", "default", "long-running", "maintenance" };
    options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
    options.ServerTimeout = TimeSpan.FromMinutes(5);
    options.ServerCheckInterval = TimeSpan.FromMinutes(1);
    options.HeartbeatInterval = TimeSpan.FromSeconds(30);
});
```

**Database Optimization:**
```csharp
services.AddHangfire(configuration => configuration
    .UseSQLiteStorage(connectionString, new SQLiteStorageOptions
    {
        QueuePollInterval = TimeSpan.Zero, // Instant polling
        JobExpirationCheckInterval = TimeSpan.FromHours(1),
        CountersAggregateInterval = TimeSpan.FromMinutes(5),
        PrepareSchemaIfNecessary = true,
        DashboardJobListLimit = 50000,
        TransactionTimeout = TimeSpan.FromMinutes(1)
    }));
```

### Memory Management

**Job Cleanup Configuration:**
```csharp
public class TaskCleanupJob
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Clean up succeeded jobs older than 7 days
        var cutoffDate = DateTime.UtcNow.AddDays(-7);

        using var connection = JobStorage.Current.GetConnection();
        var expiredJobs = connection.GetJobs(JobStatuses.Succeeded, 0, int.MaxValue)
            .Where(job => job.Value.CreatedAt < cutoffDate)
            .Select(job => job.Key)
            .ToList();

        foreach (var jobId in expiredJobs)
        {
            BackgroundJob.Delete(jobId);
        }

        // Clean up failed jobs older than 30 days
        var longTermCutoff = DateTime.UtcNow.AddDays(-30);
        var expiredFailedJobs = connection.GetJobs(JobStatuses.Failed, 0, int.MaxValue)
            .Where(job => job.Value.CreatedAt < longTermCutoff)
            .Select(job => job.Key)
            .ToList();

        foreach (var jobId in expiredFailedJobs)
        {
            BackgroundJob.Delete(jobId);
        }
    }
}
```

## Quality Assurance

### Testing Strategy
- **Unit Tests**: Job execution logic, retry mechanisms, workflow orchestration
- **Integration Tests**: Database persistence, SignalR integration, API compatibility
- **Performance Tests**: High-load job processing, memory usage under load
- **Reliability Tests**: Failure recovery, data consistency, job persistence

### Monitoring & Alerting

**Custom Metrics:**
```csharp
public class PerformanceMonitoringFilter : JobFilterAttribute, IServerFilter
{
    private readonly IMetricsCollector _metrics;

    public void OnPerforming(PerformingContext filterContext)
    {
        _metrics.Increment("jobs.started");
        _metrics.Timer("jobs.execution_time").Start();
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        _metrics.Timer("jobs.execution_time").Stop();

        if (filterContext.Exception != null)
        {
            _metrics.Increment("jobs.failed");
        }
        else
        {
            _metrics.Increment("jobs.succeeded");
        }
    }
}
```

## Migration Strategy

### Phase 1: Parallel Operation
- Deploy Hangfire alongside existing SimpleOrchestrator
- Feature flag to control which system handles new tasks
- Maintain dual monitoring and logging

### Phase 2: Gradual Migration
- Migrate low-priority tasks to Hangfire first
- Monitor performance and stability
- Migrate high-priority tasks after validation

### Phase 3: Full Migration
- Switch all new tasks to Hangfire
- Maintain SimpleOrchestrator for in-flight tasks
- Complete migration when all legacy tasks complete

### Phase 4: Legacy Cleanup
- Remove SimpleOrchestrator dependencies
- Clean up obsolete configuration
- Update documentation and training

## Success Criteria

1. **Persistence**: Tasks survive application restarts without data loss
2. **Reliability**: 99.9% task execution success rate with automatic retry
3. **Performance**: No degradation in task execution times
4. **Monitoring**: Comprehensive dashboard with real-time job status
5. **Scalability**: Support for 10x current task volume without architectural changes
6. **Integration**: Seamless compatibility with existing OrchestratorService API

This work plan establishes a production-ready task orchestration system that provides enterprise-grade reliability, monitoring, and scalability while maintaining backward compatibility with the existing application architecture.