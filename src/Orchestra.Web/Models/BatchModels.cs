using Orchestra.Core.Models;
using Orchestra.Web.Models;
using System.Diagnostics;
using TaskPriority = Orchestra.Core.Models.TaskPriority;

namespace Orchestra.Web.Models;

/// <summary>
/// Policy for handling errors during batch execution
/// </summary>
public enum BatchErrorPolicy
{
    ContinueOnError,
    StopOnFirstError,
    StopOnCriticalError
}

/// <summary>
/// Status of an individual task in a batch operation
/// </summary>
public enum BatchTaskStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

/// <summary>
/// Represents the progress of a specific repository in a batch operation
/// </summary>
public record RepositoryProgress(
    string RepositoryName,
    int TotalTasks,
    int CompletedTasks,
    string? CurrentTask = null
);

/// <summary>
/// Detailed information about a specific task in a batch operation
/// </summary>
public record TaskDetail(
    string TaskId,
    string Command,
    string Repository,
    BatchTaskStatus Status,
    DateTime StartTime,
    DateTime EndTime = default,
    string? ErrorMessage = null
);

/// <summary>
/// Status of batch execution
/// </summary>
public record BatchStatus(
    string Id,
    int TotalTasks,
    int CompletedTasks,
    int FailedTasks,
    int SuccessfulTasks,
    bool IsCompleted,
    bool IsSuccess,
    DateTime StartTime,
    DateTime EndTime,
    string? CurrentTask,
    TimeSpan EstimatedTimeRemaining
);

// Enhanced models with complex functionality
public record BatchTaskRequest(
    string Command,
    string TargetRepository,
    TaskPriority Priority,
    string? TaskId = null,
    List<string>? DependsOn = null,
    TimeSpan? EstimatedDuration = null,
    bool RequiresPreviousSuccess = true);

public record BatchExecutionOptions(
    int MaxConcurrency = 5,
    BatchErrorPolicy ErrorPolicy = BatchErrorPolicy.ContinueOnError,
    TimeSpan? Timeout = null,
    bool EnableProgressReporting = true,
    int MaxRetryAttempts = 3);

public record BatchExecutionResult(
    string BatchId,
    int TotalTasks,
    List<BatchTaskResult> SuccessfulTasks,
    List<BatchTaskResult> FailedTasks,
    DateTime CompletedAt);

public record BatchTaskResult(
    string TaskId,
    string Command,
    string Repository,
    bool IsSuccess,
    string Message,
    DateTime StartTime,
    DateTime EndTime);

public record BatchProgress(
    string BatchId,
    int TotalTasks,
    int CompletedTasks,
    int SuccessfulTasks,
    int FailedTasks,
    string CurrentTask,
    TimeSpan EstimatedTimeRemaining);

/// <summary>
/// Execution graph representing task dependencies as a directed acyclic graph (DAG)
/// </summary>
public class ExecutionGraph
{
    public Dictionary<string, TaskNode> Nodes { get; } = new();
    public List<DependencyEdge> Edges { get; } = new();

    public void AddNode(TaskNode node)
    {
        Nodes[node.TaskId] = node;
    }

    public void AddEdge(DependencyEdge edge)
    {
        Edges.Add(edge);
    }

    public IEnumerable<DependencyEdge> GetIncomingEdges(string taskId)
    {
        return Edges.Where(e => e.ToNode.TaskId == taskId);
    }

    public IEnumerable<DependencyEdge> GetOutgoingEdges(string taskId)
    {
        return Edges.Where(e => e.FromNode.TaskId == taskId);
    }
}

/// <summary>
/// Node representing a single task in the execution graph
/// </summary>
public class TaskNode
{
    public string TaskId { get; }
    public string Command { get; }
    public string TargetRepository { get; }
    public TaskPriority Priority { get; }
    public TimeSpan EstimatedDuration { get; }
    public bool RequiresPreviousSuccess { get; }
    public List<string> Dependencies { get; }

    public TaskNode(string taskId, string command, string targetRepository, TaskPriority priority,
                   TimeSpan estimatedDuration, bool requiresPreviousSuccess, List<string> dependencies)
    {
        TaskId = taskId;
        Command = command;
        TargetRepository = targetRepository;
        Priority = priority;
        EstimatedDuration = estimatedDuration;
        RequiresPreviousSuccess = requiresPreviousSuccess;
        Dependencies = dependencies ?? new List<string>();
    }
}

/// <summary>
/// Edge representing a dependency relationship between two tasks
/// </summary>
public class DependencyEdge
{
    public TaskNode FromNode { get; }
    public TaskNode ToNode { get; }
    public bool RequiresPreviousSuccess { get; }

    public DependencyEdge(TaskNode fromNode, TaskNode toNode, bool requiresPreviousSuccess)
    {
        FromNode = fromNode;
        ToNode = toNode;
        RequiresPreviousSuccess = requiresPreviousSuccess;
    }
}

/// <summary>
/// Context for tracking batch execution state
/// </summary>
public class TaskExecutionContext
{
    public string BatchId { get; }
    public int TotalTasks { get; }
    public BatchExecutionOptions Options { get; }
    public Stopwatch Stopwatch { get; }
    public CancellationTokenSource CancellationTokenSource { get; }

    public TaskExecutionContext(string batchId, int totalTasks, BatchExecutionOptions options, Stopwatch stopwatch)
    {
        BatchId = batchId;
        TotalTasks = totalTasks;
        Options = options;
        Stopwatch = stopwatch;
        CancellationTokenSource = new CancellationTokenSource();
    }
}

/// <summary>
/// Result of individual task execution
/// </summary>
public class TaskExecutionResult
{
    public string TaskId { get; }
    public string Command { get; }
    public string TargetRepository { get; }
    public bool IsSuccess { get; }
    public string ErrorMessage { get; }
    public DateTime StartTime { get; }
    public DateTime EndTime { get; }

    public TaskExecutionResult(string taskId, string command, string targetRepository, bool isSuccess,
                              string errorMessage, DateTime startTime, DateTime endTime)
    {
        TaskId = taskId;
        Command = command;
        TargetRepository = targetRepository;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        StartTime = startTime;
        EndTime = endTime;
    }

    public BatchTaskResult ToBatchTaskResult()
    {
        return new BatchTaskResult(TaskId, Command, TargetRepository, IsSuccess, ErrorMessage, StartTime, EndTime);
    }
}