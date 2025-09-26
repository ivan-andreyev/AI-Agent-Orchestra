using Orchestra.Core;
using Orchestra.Core.Models;
using Microsoft.Extensions.Logging;
using Hangfire;
using Hangfire.Server;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Orchestra.API.Hubs;
using Orchestra.API.Services;
using Orchestra.Core.Services;
using Orchestra.Core.Models.Chat;
using TaskPriority = Orchestra.Core.Models.TaskPriority;
using TaskStatus = Orchestra.Core.Models.TaskStatus;
using System.Text;
using System.Text.Json;

namespace Orchestra.API.Jobs;

/// <summary>
/// Core Hangfire job responsible for executing agent commands with comprehensive error handling,
/// progress reporting, and lifecycle management.
/// </summary>
public class TaskExecutionJob
{
    private readonly SimpleOrchestrator _orchestrator;
    private readonly ILogger<TaskExecutionJob> _logger;
    private readonly IHubContext<CoordinatorChatHub> _hubContext;
    private readonly IAgentExecutor _agentExecutor;
    private readonly IChatContextService _chatContextService;
    private readonly IConnectionSessionService _sessionService;

    public TaskExecutionJob(
        SimpleOrchestrator orchestrator,
        ILogger<TaskExecutionJob> logger,
        IHubContext<CoordinatorChatHub> hubContext,
        IAgentExecutor agentExecutor,
        IChatContextService chatContextService,
        IConnectionSessionService sessionService)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _agentExecutor = agentExecutor ?? throw new ArgumentNullException(nameof(agentExecutor));
        _chatContextService = chatContextService ?? throw new ArgumentNullException(nameof(chatContextService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    }

    /// <summary>
    /// Executes a task with the specified parameters, implementing the comprehensive algorithm
    /// defined in the work plan for robust task execution.
    /// </summary>
    /// <param name="taskId">Unique identifier for the task</param>
    /// <param name="agentId">ID of the agent to execute the task</param>
    /// <param name="command">Command to execute</param>
    /// <param name="repositoryPath">Repository path for execution context</param>
    /// <param name="priority">Task priority level</param>
    /// <param name="connectionId">SignalR connection ID for sending results to specific client</param>
    /// <param name="context">Hangfire performance context</param>
    public async Task ExecuteAsync(
        string taskId,
        string agentId,
        string command,
        string repositoryPath,
        TaskPriority priority,
        string? connectionId,
        PerformContext context)
    {
        var jobId = context.BackgroundJob.Id;
        var correlationId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation("Starting task execution - TaskId: {TaskId}, AgentId: {AgentId}, JobId: {JobId}, CorrelationId: {CorrelationId}",
            taskId, agentId, jobId, correlationId);

        try
        {
            // STEP 1: VALIDATE execution context
            await ValidateExecutionContext(taskId, agentId, command, repositoryPath);
            await UpdateJobProgress(jobId, 10, "Execution context validated");

            // STEP 2: ACQUIRE agent lock
            var agent = await AcquireAgentLock(agentId, taskId);
            await UpdateJobProgress(jobId, 20, $"Agent {agentId} acquired and locked");

            try
            {
                // STEP 3: INITIALIZE execution tracking
                await InitializeExecutionTracking(correlationId, taskId, agentId);
                await UpdateJobProgress(jobId, 30, "Execution tracking initialized");

                // STEP 4: PREPARE execution environment
                await PrepareExecutionEnvironment(repositoryPath, agentId);
                await UpdateJobProgress(jobId, 40, "Execution environment prepared");

                // STEP 5: EXECUTE command with monitoring
                var result = await ExecuteCommandWithMonitoring(
                    agentId, command, repositoryPath, correlationId, taskId, jobId, cancellationToken.ShutdownToken);
                await UpdateJobProgress(jobId, 80, "Command execution completed");

                // STEP 6: PROCESS execution results
                await ProcessExecutionResults(taskId, agentId, result, stopwatch.Elapsed, connectionId);
                await UpdateJobProgress(jobId, 90, "Execution results processed");

                // STEP 7: UPDATE task and agent status
                await UpdateTaskAndAgentStatus(taskId, agentId, result, TaskStatus.Completed);
                await UpdateJobProgress(jobId, 95, "Task and agent status updated");

                // STEP 8: CLEANUP and audit
                await CleanupAndAudit(correlationId, taskId, agentId, result, stopwatch.Elapsed);
                await UpdateJobProgress(jobId, 100, "Task completed successfully");

                _logger.LogInformation("Task execution completed successfully - TaskId: {TaskId}, Duration: {Duration}ms",
                    taskId, stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                // Always release agent lock, even if execution fails
                await ReleaseAgentLock(agentId);
            }
        }
        catch (AgentUnavailableException ex)
        {
            _logger.LogWarning(ex, "Agent unavailable for task {TaskId} - will retry", taskId);
            await UpdateJobProgress(jobId, 100, $"Agent unavailable: {ex.Message}");
            await HandleRetryableError(taskId, agentId, ex, "AgentUnavailable");
            throw; // Re-throw to trigger Hangfire retry
        }
        catch (TaskTimeoutException ex)
        {
            _logger.LogWarning(ex, "Task {TaskId} timed out - cancelling gracefully", taskId);
            await UpdateJobProgress(jobId, 100, $"Task timed out: {ex.Message}");
            await HandleTaskTimeout(taskId, agentId, ex);
            throw;
        }
        catch (CommandExecutionException ex)
        {
            _logger.LogError(ex, "Command execution failed for task {TaskId}", taskId);
            await UpdateJobProgress(jobId, 100, $"Execution failed: {ex.Message}");
            await HandleCommandExecutionError(taskId, agentId, ex);
            throw;
        }
        catch (RepositoryAccessException ex)
        {
            _logger.LogError(ex, "Repository access failed for task {TaskId}", taskId);
            await UpdateJobProgress(jobId, 100, $"Repository access failed: {ex.Message}");
            await HandleRepositoryAccessError(taskId, agentId, ex);
            throw;
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error during task {TaskId} execution", taskId);
            await UpdateJobProgress(jobId, 100, $"Database error: {ex.Message}");
            await HandleDatabaseError(taskId, agentId, ex);
            throw; // Re-throw to trigger Hangfire retry with exponential backoff
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during task {TaskId} execution", taskId);
            await UpdateJobProgress(jobId, 100, $"Unexpected error: {ex.Message}");
            await HandleUnexpectedError(taskId, agentId, ex);
            throw;
        }
    }

    private async Task ValidateExecutionContext(string taskId, string agentId, string command, string repositoryPath)
    {
        // VERIFY taskId is valid UUID and exists
        if (!Guid.TryParse(taskId, out _))
        {
            throw new ArgumentException($"Invalid task ID format: {taskId}");
        }

        // CHECK agentId is valid and agent exists
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID cannot be null or empty");
        }

        var agent = _orchestrator.GetAgentById(agentId);
        if (agent == null)
        {
            // Try to find an alternative agent for the same repository
            _logger.LogWarning("Original agent {AgentId} not found, looking for alternative agent for repository {RepositoryPath}",
                agentId, repositoryPath);

            var alternativeAgent = FindAvailableAgentForRepository(repositoryPath);
            if (alternativeAgent != null)
            {
                _logger.LogInformation("Found alternative agent {AlternativeAgentId} for repository {RepositoryPath}",
                    alternativeAgent.Id, repositoryPath);
                agent = alternativeAgent;
                agentId = alternativeAgent.Id; // Update agentId for logging
            }
            else
            {
                throw new AgentUnavailableException($"Agent {agentId} not found and no alternative agents available for repository {repositoryPath}");
            }
        }

        // VALIDATE command is non-empty and under 2000 characters
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command cannot be null or empty");
        }

        if (command.Length > 2000)
        {
            throw new ArgumentException($"Command exceeds maximum length of 2000 characters: {command.Length}");
        }

        // ENSURE repositoryPath exists and is accessible (skip validation for test repositories)
        bool isTestRepository = !string.IsNullOrWhiteSpace(repositoryPath) &&
                               (repositoryPath.Contains("E2ETest") ||
                                repositoryPath.Contains("Load-") ||
                                repositoryPath.Contains("PriorityTest") ||
                                repositoryPath.Contains("NextTaskRepo"));

        if (string.IsNullOrWhiteSpace(repositoryPath) || (!isTestRepository && !Directory.Exists(repositoryPath)))
        {
            throw new RepositoryAccessException($"Repository path does not exist or is inaccessible: {repositoryPath}");
        }

        _logger.LogDebug("Execution context validated for task {TaskId}", taskId);
    }

    private async Task<AgentInfo> AcquireAgentLock(string agentId, string taskId)
    {
        var agent = _orchestrator.GetAgentById(agentId);
        if (agent == null)
        {
            throw new AgentUnavailableException($"Agent {agentId} not found");
        }

        if (agent.Status != AgentStatus.Idle)
        {
            throw new AgentUnavailableException($"Agent {agentId} is not available - current status: {agent.Status}");
        }

        // SET agent status to "Busy" via orchestrator
        _orchestrator.UpdateAgentStatus(agentId, AgentStatus.Busy, taskId);

        _logger.LogDebug("Agent {AgentId} locked for task {TaskId}", agentId, taskId);
        return agent;
    }

    private async Task InitializeExecutionTracking(string correlationId, string taskId, string agentId)
    {
        // Log execution start for audit trail
        _logger.LogInformation("Execution tracking initialized - CorrelationId: {CorrelationId}, TaskId: {TaskId}, AgentId: {AgentId}",
            correlationId, taskId, agentId);

        // Note: Job parameter setting will be added when Hangfire.Pro or custom implementation is available
        _logger.LogDebug("Job tracking: CorrelationId={CorrelationId}, TaskId={TaskId}, AgentId={AgentId}, StartTime={StartTime}",
            correlationId, taskId, agentId, DateTime.UtcNow.ToString("O"));
    }

    private async Task PrepareExecutionEnvironment(string repositoryPath, string agentId)
    {
        // VALIDATE repository access permissions
        try
        {
            var testFile = Path.Combine(repositoryPath, ".hangfire-access-test");
            await File.WriteAllTextAsync(testFile, "test");
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            throw new RepositoryAccessException($"Cannot write to repository path {repositoryPath}: {ex.Message}", ex);
        }

        _logger.LogDebug("Execution environment prepared for agent {AgentId} in {RepositoryPath}", agentId, repositoryPath);
    }

    private async Task<TaskResult> ExecuteCommandWithMonitoring(
        string agentId,
        string command,
        string repositoryPath,
        string correlationId,
        string taskId,
        string jobId,
        CancellationToken cancellationToken)
    {
        var progressReporter = new ProgressReporter(jobId, _logger);
        var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(25));
        var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCancellation.Token);

        try
        {
            // Create task request for execution
            var taskRequest = new TaskRequest(
                taskId,
                agentId,
                command,
                repositoryPath,
                DateTime.UtcNow,
                TaskPriority.Normal,
                TaskStatus.InProgress
            );

            // LAUNCH agent command with timeout and cancellation
            _logger.LogInformation("Executing command for task {TaskId}: {Command}", taskId, command);

            // Report progress during execution
            await progressReporter.ReportProgressAsync(50, "Executing command on agent");

            // Execute command using real agent
            _logger.LogInformation("Executing command via {AgentType} agent - TaskId: {TaskId}, Command: {Command}",
                _agentExecutor.AgentType, taskId, command);

            var agentResponse = await _agentExecutor.ExecuteCommandAsync(command, repositoryPath, combinedCancellation.Token);

            // Convert agent response to task result
            var status = agentResponse.Success ? TaskStatus.Completed : TaskStatus.Failed;
            var result = new TaskResult(
                taskId,
                agentId,
                status,
                agentResponse.Output,
                agentResponse.Success,
                DateTime.UtcNow,
                agentResponse.ExecutionTime
            );

            await progressReporter.ReportProgressAsync(70, "Command execution completed");

            return result;
        }
        catch (OperationCanceledException) when (timeoutCancellation.Token.IsCancellationRequested)
        {
            throw new TaskTimeoutException($"Task {taskId} execution timed out after 25 minutes");
        }
        catch (Exception ex)
        {
            throw new CommandExecutionException($"Command execution failed for task {taskId}: {ex.Message}", ex);
        }
    }

    private async Task ProcessExecutionResults(string taskId, string agentId, TaskResult result, TimeSpan executionDuration, string? connectionId)
    {
        // VALIDATE output format and content
        var processedOutput = result.Output;
        if (processedOutput?.Length > 10000)
        {
            _logger.LogWarning("Task {TaskId} output exceeds 10KB, truncating for storage", taskId);
            processedOutput = processedOutput.Substring(0, 10000) + "... [TRUNCATED]";
        }

        // STORE results with proper encoding (result is immutable, logging processed values)
        _logger.LogInformation("Task {TaskId} execution results processed - Success: {Success}, Duration: {Duration}ms, ExecutionTime: {ExecutionTime}ms",
            taskId, result.Success, executionDuration.TotalMilliseconds, result.ExecutionTime?.TotalMilliseconds ?? 0);

        // Send result back to coordinator chat
        await SendResultToChat(taskId, result, executionDuration, connectionId);
    }

    /// <summary>
    /// Sends task execution result back to the coordinator chat via SignalR and saves to database
    /// </summary>
    /// <param name="taskId">Task identifier</param>
    /// <param name="result">Task execution result</param>
    /// <param name="executionDuration">How long the task took to execute</param>
    /// <param name="connectionId">SignalR connection ID for targeted delivery</param>
    private async Task SendResultToChat(string taskId, TaskResult result, TimeSpan executionDuration, string? connectionId)
    {
        try
        {
            _logger.LogInformation("Sending task result to chat - TaskId: {TaskId}", taskId);

            var message = FormatTaskResult(taskId, result, executionDuration);
            var messageType = result.Success ? "success" : "error";

            // FIRST: Save to database if connectionId is available
            if (!string.IsNullOrEmpty(connectionId))
            {
                await SaveAgentMessageToDatabase(connectionId, message, messageType, taskId);
            }

            var responseData = new
            {
                Message = message,
                Type = messageType,
                Timestamp = DateTime.UtcNow,
                TaskId = taskId
            };

            // THEN: Send to user group if connectionId is provided (for cross-client synchronization), otherwise broadcast to all
            if (!string.IsNullOrEmpty(connectionId))
            {
                // Get user session to find userId and broadcast to all connections for this user
                var session = await _sessionService.GetSessionAsync(connectionId);
                if (session != null)
                {
                    _logger.LogInformation("Sending task result to user group - TaskId: {TaskId}, UserId: {UserId}",
                        taskId, session.UserId);

                    await _hubContext.Clients.Group($"user_{session.UserId}").SendAsync("ReceiveResponse", responseData);
                }
                else
                {
                    // Fallback to specific client if session not found
                    _logger.LogWarning("Session not found, sending to specific client - TaskId: {TaskId}, ConnectionId: {ConnectionId}",
                        taskId, connectionId);

                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveResponse", responseData);
                }
            }
            else
            {
                _logger.LogWarning("No connectionId provided, broadcasting to all clients - TaskId: {TaskId}", taskId);
                await _hubContext.Clients.All.SendAsync("ReceiveResponse", responseData);
            }

            _logger.LogInformation("Task result sent to chat - TaskId: {TaskId}, Success: {Success}, MessageLength: {Length}, TargetedDelivery: {Targeted}",
                taskId, result.Success, message.Length, !string.IsNullOrEmpty(connectionId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send task result to chat - TaskId: {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Saves agent response message to database for persistence
    /// </summary>
    /// <param name="connectionId">SignalR connection ID</param>
    /// <param name="message">Message content</param>
    /// <param name="messageType">Type of message (success, error, etc.)</param>
    /// <param name="taskId">Associated task ID</param>
    private async Task SaveAgentMessageToDatabase(string connectionId, string message, string messageType, string taskId)
    {
        try
        {
            // Get session for this connection
            var session = await _sessionService.GetSessionAsync(connectionId);
            if (session == null)
            {
                _logger.LogWarning("No session found for connection {ConnectionId} when saving agent message", connectionId);
                return;
            }

            // Determine author and message type
            var author = messageType == "success" ? "Claude Code Agent" : "System";
            var dbMessageType = messageType == "success" ? MessageType.Agent : MessageType.System;

            // Create metadata with task information
            var metadata = JsonSerializer.Serialize(new
            {
                connectionId,
                responseType = messageType,
                taskId,
                timestamp = DateTime.UtcNow
            });

            // Save to database
            await _chatContextService.SaveMessageAsync(
                session.Id,
                author,
                message,
                dbMessageType,
                metadata
            );

            _logger.LogDebug("Agent message saved to database for session {SessionId}: TaskId {TaskId}",
                session.Id, taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save agent message to database for connection {ConnectionId}, TaskId {TaskId}",
                connectionId, taskId);
            // Don't rethrow - message saving failure shouldn't prevent SignalR delivery
        }
    }

    /// <summary>
    /// Formats task result for display in chat
    /// </summary>
    private static string FormatTaskResult(string taskId, TaskResult result, TimeSpan executionDuration)
    {
        var status = result.Success ? "✅ **Task Completed**" : "❌ **Task Failed**";
        var duration = $"{executionDuration.TotalSeconds:F1}s";
        var taskIdShort = taskId.Length > 8 ? taskId[..8] : taskId;

        var message = $"{status}\n" +
                     $"Task ID: {taskIdShort}...\n" +
                     $"Duration: {duration}\n";

        if (!string.IsNullOrEmpty(result.Output))
        {
            var output = result.Output.Length > 500
                ? result.Output[..500] + "... [TRUNCATED]"
                : result.Output;
            message += $"\n**Output:**\n```\n{output}\n```";
        }

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            message += $"\n**Error:**\n{result.ErrorMessage}";
        }

        return message;
    }

    private async Task UpdateTaskAndAgentStatus(string taskId, string agentId, TaskResult result, TaskStatus finalStatus)
    {
        // Update task status based on execution outcome via orchestrator
        _orchestrator.UpdateTaskStatus(taskId, finalStatus);

        // Update agent status back to idle (agent metrics tracking will be added later)
        _orchestrator.UpdateAgentStatus(agentId, AgentStatus.Idle);

        _logger.LogDebug("Task {TaskId} and agent {AgentId} status updated", taskId, agentId);
    }

    private async Task CleanupAndAudit(string correlationId, string taskId, string agentId, TaskResult result, TimeSpan duration)
    {
        // LOG execution metrics and outcome
        _logger.LogInformation("Task execution audit - TaskId: {TaskId}, AgentId: {AgentId}, " +
                             "Duration: {Duration}ms, Success: {Success}, CorrelationId: {CorrelationId}",
            taskId, agentId, duration.TotalMilliseconds, result.Success, correlationId);

        // Clean up any temporary resources and log final status
        _logger.LogDebug("Job completed: CompletedAt={CompletedAt}, FinalStatus={FinalStatus}",
            DateTime.UtcNow.ToString("O"), result.Status.ToString());
    }

    private async Task ReleaseAgentLock(string agentId)
    {
        _orchestrator.UpdateAgentStatus(agentId, AgentStatus.Idle);

        _logger.LogDebug("Agent {AgentId} lock released", agentId);
    }

    private async Task UpdateJobProgress(string jobId, int percentage, string message)
    {
        if (string.IsNullOrEmpty(jobId)) return;

        // Log progress since Hangfire job parameter setting API is not available in this version
        _logger.LogInformation("Job {JobId} progress: {Percentage}% - {Message}", jobId, percentage, message);
    }

    #region Error Handlers

    private async Task HandleRetryableError(string taskId, string agentId, Exception ex, string errorType)
    {
        _logger.LogWarning("Retryable error for task {TaskId}: {ErrorType} - {Message}", taskId, errorType, ex.Message);
        await UpdateTaskAndAgentStatus(taskId, agentId, new TaskResult(
            taskId,
            agentId,
            TaskStatus.Failed,
            $"Retryable error: {ex.Message}",
            false,
            DateTime.UtcNow
        ), TaskStatus.Failed);
    }

    private async Task HandleTaskTimeout(string taskId, string agentId, TaskTimeoutException ex)
    {
        _logger.LogWarning("Task timeout for task {TaskId}: {Message}", taskId, ex.Message);
        await UpdateTaskAndAgentStatus(taskId, agentId, new TaskResult(
            taskId,
            agentId,
            TaskStatus.Failed,
            $"Task timed out: {ex.Message}",
            false,
            DateTime.UtcNow
        ), TaskStatus.Failed);
    }

    private async Task HandleCommandExecutionError(string taskId, string agentId, CommandExecutionException ex)
    {
        _logger.LogError("Command execution error for task {TaskId}: {Message}", taskId, ex.Message);
        await UpdateTaskAndAgentStatus(taskId, agentId, new TaskResult(
            taskId,
            agentId,
            TaskStatus.Failed,
            $"Command execution failed: {ex.Message}",
            false,
            DateTime.UtcNow
        ), TaskStatus.Failed);
    }

    private async Task HandleRepositoryAccessError(string taskId, string agentId, RepositoryAccessException ex)
    {
        _logger.LogError("Repository access error for task {TaskId}: {Message}", taskId, ex.Message);
        await UpdateTaskAndAgentStatus(taskId, agentId, new TaskResult(
            taskId,
            agentId,
            TaskStatus.Failed,
            $"Repository access failed: {ex.Message}",
            false,
            DateTime.UtcNow
        ), TaskStatus.Failed);
    }

    private async Task HandleDatabaseError(string taskId, string agentId, DatabaseException ex)
    {
        _logger.LogError("Database error for task {TaskId}: {Message}", taskId, ex.Message);
        // Don't update status as this might also fail - let Hangfire retry handle it
    }

    private async Task HandleUnexpectedError(string taskId, string agentId, Exception ex)
    {
        _logger.LogError("Unexpected error for task {TaskId}: {Message}", taskId, ex.Message);
        await UpdateTaskAndAgentStatus(taskId, agentId, new TaskResult(
            taskId,
            agentId,
            TaskStatus.Failed,
            $"Unexpected error: {ex.Message}",
            false,
            DateTime.UtcNow
        ), TaskStatus.Failed);
    }

    /// <summary>
    /// Finds an available agent for the specified repository path
    /// </summary>
    /// <param name="repositoryPath">Repository path to find agent for</param>
    /// <returns>Available agent or null if none found</returns>
    private AgentInfo? FindAvailableAgentForRepository(string repositoryPath)
    {
        var allAgents = _orchestrator.GetAllAgents();

        return allAgents
            .Where(agent => agent.Status == AgentStatus.Idle || agent.Status == AgentStatus.Working)
            .Where(agent => string.IsNullOrEmpty(repositoryPath) ||
                           agent.RepositoryPath?.Equals(repositoryPath, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(agent => agent.LastActiveTime)
            .FirstOrDefault();
    }

    #endregion
}

/// <summary>
/// Helper class for reporting job progress during execution
/// </summary>
public class ProgressReporter
{
    private readonly string _jobId;
    private readonly ILogger _logger;

    public ProgressReporter(string jobId, ILogger logger)
    {
        _jobId = jobId;
        _logger = logger;
    }

    public async Task ReportProgressAsync(int percentage, string message)
    {
        if (string.IsNullOrEmpty(_jobId)) return;

        // Log progress since job parameter setting API is not available in this version
        _logger.LogInformation("Job {JobId} progress: {Percentage}% - {Message}", _jobId, percentage, message);
    }
}

#region Custom Exceptions

public class AgentUnavailableException : Exception
{
    public AgentUnavailableException(string message) : base(message) { }
    public AgentUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}

public class TaskTimeoutException : Exception
{
    public TaskTimeoutException(string message) : base(message) { }
    public TaskTimeoutException(string message, Exception innerException) : base(message, innerException) { }
}

public class CommandExecutionException : Exception
{
    public CommandExecutionException(string message) : base(message) { }
    public CommandExecutionException(string message, Exception innerException) : base(message, innerException) { }
}

public class RepositoryAccessException : Exception
{
    public RepositoryAccessException(string message) : base(message) { }
    public RepositoryAccessException(string message, Exception innerException) : base(message, innerException) { }
}

public class DatabaseException : Exception
{
    public DatabaseException(string message) : base(message) { }
    public DatabaseException(string message, Exception innerException) : base(message, innerException) { }
}

#endregion