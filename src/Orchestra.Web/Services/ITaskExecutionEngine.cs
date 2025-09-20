using Orchestra.Web.Models;

namespace Orchestra.Web.Services;

/// <summary>
/// Interface for task execution engine responsible for executing tasks with dependency resolution
/// </summary>
public interface ITaskExecutionEngine
{
    /// <summary>
    /// Execute tasks with dependency resolution and parallel execution
    /// </summary>
    /// <param name="graph">Execution graph containing nodes and dependencies</param>
    /// <param name="executionOrder">Ordered list of tasks to execute</param>
    /// <param name="context">Execution context with configuration and state</param>
    /// <param name="progress">Progress reporting mechanism</param>
    /// <param name="cancellationToken">Cancellation token for operation control</param>
    /// <returns>Batch execution result with success/failure details</returns>
    Task<BatchExecutionResult> ExecuteTasksWithDependencyResolutionAsync(
        ExecutionGraph graph,
        List<TaskNode> executionOrder,
        TaskExecutionContext context,
        IProgress<BatchProgress> progress,
        CancellationToken cancellationToken);
}