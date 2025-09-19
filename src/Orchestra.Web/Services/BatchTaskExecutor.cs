using Orchestra.Web.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Orchestra.Web.Services;

/// <summary>
/// Main coordinator for batch task execution with DAG dependency resolution
/// Orchestrates validation, dependency graph building, and task execution
/// </summary>
public class BatchTaskExecutor
{
    private readonly ILogger<BatchTaskExecutor> _logger;
    private readonly IOrchestratorService _orchestratorService;
    private readonly DependencyGraphBuilder _graphBuilder;
    private readonly TaskExecutionEngine _executionEngine;
    private readonly ConcurrentDictionary<string, TaskExecutionContext> _activeBatches;

    public BatchTaskExecutor(
        ILogger<BatchTaskExecutor> logger,
        IOrchestratorService orchestratorService,
        DependencyGraphBuilder graphBuilder,
        TaskExecutionEngine executionEngine)
    {
        _logger = logger;
        _orchestratorService = orchestratorService;
        _graphBuilder = graphBuilder;
        _executionEngine = executionEngine;
        _activeBatches = new ConcurrentDictionary<string, TaskExecutionContext>();
    }

    /// <summary>
    /// Execute tasks with DAG dependency resolution and parallel execution
    /// </summary>
    public async Task<BatchExecutionResult> ExecuteBatchAsync(
        List<BatchTaskRequest> tasks,
        BatchExecutionOptions options,
        IProgress<BatchProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Starting sophisticated batch execution with {Count} tasks, Batch ID: {BatchId}",
            tasks.Count, batchId);

        try
        {
            // Step 1: Validate batch request
            await ValidateBatchRequestAsync(tasks, options, cancellationToken);

            // Step 2: Build dependency graph (DAG)
            var executionGraph = await _graphBuilder.BuildDependencyGraphAsync(tasks, cancellationToken);

            // Step 3: Detect circular dependencies
            _graphBuilder.ValidateNoCyclicDependencies(executionGraph);

            // Step 4: Calculate topological ordering
            var executionOrder = _graphBuilder.CalculateTopologicalOrder(executionGraph);

            // Step 5: Validate repository access
            await ValidateRepositoryAccessAsync(tasks, cancellationToken);

            // Step 6: Execute tasks in dependency order
            var executionContext = new TaskExecutionContext(batchId, tasks.Count, options, stopwatch);
            _activeBatches[batchId] = executionContext;

            var result = await _executionEngine.ExecuteTasksWithDependencyResolutionAsync(
                executionGraph, executionOrder, executionContext, progress, cancellationToken);

            _logger.LogInformation("Batch execution completed. Batch ID: {BatchId}, Duration: {Duration}ms, Success: {Success}/{Total}",
                batchId, stopwatch.ElapsedMilliseconds, result.SuccessfulTasks.Count, result.TotalTasks);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch execution failed. Batch ID: {BatchId}, Duration: {Duration}ms",
                batchId, stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            _activeBatches.TryRemove(batchId, out _);
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Validate batch request parameters and constraints
    /// </summary>
    private async Task ValidateBatchRequestAsync(List<BatchTaskRequest> tasks, BatchExecutionOptions options, CancellationToken cancellationToken)
    {
        if (tasks == null || !tasks.Any())
        {
            throw new ArgumentException("Tasks array must not be empty", nameof(tasks));
        }

        if (tasks.Count > 100)
        {
            throw new ArgumentException("Maximum 100 tasks allowed per batch", nameof(tasks));
        }

        if (options.MaxConcurrency < 1 || options.MaxConcurrency > 20)
        {
            throw new ArgumentException("MaxConcurrency must be between 1 and 20", nameof(options));
        }

        // Validate all tasks have valid target repositories
        var invalidTasks = tasks.Where(t => string.IsNullOrWhiteSpace(t.TargetRepository)).ToList();
        if (invalidTasks.Any())
        {
            throw new ArgumentException($"All tasks must have valid target repositories. Invalid tasks: {invalidTasks.Count}");
        }

        await Task.CompletedTask; // Placeholder for future async validation
    }

    /// <summary>
    /// Validate repository access permissions
    /// </summary>
    private async Task ValidateRepositoryAccessAsync(List<BatchTaskRequest> tasks, CancellationToken cancellationToken)
    {
        var uniqueRepositories = tasks.Select(t => t.TargetRepository).Distinct().ToList();

        _logger.LogInformation("Validating access to {Count} unique repositories", uniqueRepositories.Count);

        foreach (var repository in uniqueRepositories)
        {
            // TODO: Implement actual repository access validation
            // For now, we assume all repositories are accessible
            if (string.IsNullOrWhiteSpace(repository))
            {
                throw new RepositoryAccessException($"Repository cannot be null or empty");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Cancel a running batch operation
    /// </summary>
    public async Task CancelBatchAsync(string batchId)
    {
        if (_activeBatches.TryGetValue(batchId, out var context))
        {
            context.CancellationTokenSource.Cancel();
            _logger.LogInformation("Batch cancellation requested: {BatchId}", batchId);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validate batch configuration
    /// </summary>
    public async Task<bool> ValidateBatchAsync(List<BatchTaskRequest> tasks)
    {
        try
        {
            await ValidateBatchRequestAsync(tasks, new BatchExecutionOptions(), CancellationToken.None);
            var graph = await _graphBuilder.BuildDependencyGraphAsync(tasks, CancellationToken.None);
            _graphBuilder.ValidateNoCyclicDependencies(graph);
            return true;
        }
        catch
        {
            return false;
        }
    }
}