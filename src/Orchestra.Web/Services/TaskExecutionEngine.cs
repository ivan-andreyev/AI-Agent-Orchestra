using Orchestra.Web.Models;
using System.Collections.Concurrent;

namespace Orchestra.Web.Services;

/// <summary>
/// Engine responsible for executing tasks with dependency resolution and parallel execution
/// Handles task scheduling, progress tracking, and error handling policies
/// </summary>
public class TaskExecutionEngine
{
    private readonly ILogger<TaskExecutionEngine> _logger;
    private readonly IOrchestratorService _orchestratorService;

    public TaskExecutionEngine(ILogger<TaskExecutionEngine> logger, IOrchestratorService orchestratorService)
    {
        _logger = logger;
        _orchestratorService = orchestratorService;
    }

    /// <summary>
    /// Execute tasks with dependency resolution and parallel execution
    /// </summary>
    public async Task<BatchExecutionResult> ExecuteTasksWithDependencyResolutionAsync(
        ExecutionGraph graph,
        List<TaskNode> executionOrder,
        TaskExecutionContext context,
        IProgress<BatchProgress> progress,
        CancellationToken cancellationToken)
    {
        var successful = new ConcurrentBag<BatchTaskResult>();
        var failed = new ConcurrentBag<BatchTaskResult>();
        var completedTasks = new ConcurrentDictionary<string, TaskExecutionResult>();
        var readyTasks = new ConcurrentQueue<TaskNode>();
        var executionSemaphore = new SemaphoreSlim(context.Options.MaxConcurrency, context.Options.MaxConcurrency);

        // Initialize ready tasks (no dependencies)
        foreach (var node in executionOrder)
        {
            if (!graph.GetIncomingEdges(node.TaskId).Any())
            {
                readyTasks.Enqueue(node);
            }
        }

        var activeTasks = new List<Task<TaskExecutionResult>>();
        var totalCompleted = 0;

        while (totalCompleted < graph.Nodes.Count && !cancellationToken.IsCancellationRequested)
        {
            // Start tasks that are ready
            while (readyTasks.TryDequeue(out var readyTask) && activeTasks.Count < context.Options.MaxConcurrency)
            {
                var task = ExecuteTaskWithProgressAsync(readyTask, context, executionSemaphore, cancellationToken);
                activeTasks.Add(task);
            }

            if (activeTasks.Count == 0)
            {
                break; // No more tasks to execute
            }

            // Wait for at least one task to complete
            var completedTask = await Task.WhenAny(activeTasks);
            activeTasks.Remove(completedTask);

            try
            {
                var result = await completedTask;
                completedTasks[result.TaskId] = result;
                totalCompleted++;

                if (result.IsSuccess)
                {
                    successful.Add(result.ToBatchTaskResult());

                    // Check for newly ready dependent tasks
                    var dependentNodes = graph.GetOutgoingEdges(result.TaskId).Select(e => e.ToNode);
                    foreach (var dependentNode in dependentNodes)
                    {
                        if (AreAllDependenciesSatisfied(dependentNode, graph, completedTasks))
                        {
                            readyTasks.Enqueue(dependentNode);
                        }
                    }
                }
                else
                {
                    failed.Add(result.ToBatchTaskResult());

                    if (context.Options.ErrorPolicy == BatchErrorPolicy.StopOnFirstError)
                    {
                        _logger.LogWarning("Stopping batch execution due to first error: {Error}", result.ErrorMessage);
                        break;
                    }
                }

                // Report progress
                var progressData = CalculateBatchProgress(context, successful.Count, failed.Count, totalCompleted);
                progress?.Report(progressData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during task execution");
                totalCompleted++;
            }
        }

        // Wait for any remaining active tasks
        await Task.WhenAll(activeTasks);

        return new BatchExecutionResult(
            context.BatchId,
            graph.Nodes.Count,
            successful.ToList(),
            failed.ToList(),
            DateTime.UtcNow);
    }

    private bool AreAllDependenciesSatisfied(TaskNode node, ExecutionGraph graph, ConcurrentDictionary<string, TaskExecutionResult> completedTasks)
    {
        var incomingEdges = graph.GetIncomingEdges(node.TaskId);

        foreach (var edge in incomingEdges)
        {
            if (!completedTasks.TryGetValue(edge.FromNode.TaskId, out var dependency))
            {
                return false; // Dependency not completed
            }

            if (edge.RequiresPreviousSuccess && !dependency.IsSuccess)
            {
                return false; // Dependency failed and success was required
            }
        }

        return true;
    }

    private async Task<TaskExecutionResult> ExecuteTaskWithProgressAsync(
        TaskNode node,
        TaskExecutionContext context,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Starting task execution: {TaskId} - {Command}", node.TaskId, node.Command);

            try
            {
                await _orchestratorService.QueueTaskAsync(node.Command, node.TargetRepository, node.Priority);

                var endTime = DateTime.UtcNow;
                _logger.LogInformation("Task completed successfully: {TaskId}", node.TaskId);

                return new TaskExecutionResult(
                    node.TaskId,
                    node.Command,
                    node.TargetRepository,
                    true,
                    "Executed successfully",
                    startTime,
                    endTime);
            }
            catch (Exception ex)
            {
                var endTime = DateTime.UtcNow;
                _logger.LogError(ex, "Task failed: {TaskId} - {Command}", node.TaskId, node.Command);

                return new TaskExecutionResult(
                    node.TaskId,
                    node.Command,
                    node.TargetRepository,
                    false,
                    ex.Message,
                    startTime,
                    endTime);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private BatchProgress CalculateBatchProgress(TaskExecutionContext context, int successful, int failed, int completed)
    {
        var totalTasks = context.TotalTasks;
        var completionPercentage = (double)completed / totalTasks;

        // Estimate remaining time based on average task duration
        var elapsed = context.Stopwatch.Elapsed;
        var averageTaskDuration = completed > 0 ? elapsed.TotalMilliseconds / completed : 2000; // Default 2 seconds
        var remainingTasks = totalTasks - completed;
        var estimatedTimeRemaining = TimeSpan.FromMilliseconds(averageTaskDuration * remainingTasks / context.Options.MaxConcurrency);

        return new BatchProgress(
            context.BatchId,
            totalTasks,
            completed,
            successful,
            failed,
            $"Progress: {completed}/{totalTasks} ({completionPercentage:P0})",
            estimatedTimeRemaining);
    }
}