using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Services;
using Orchestra.Web.Models;
using Orchestra.Web.Services;
using System.Collections.Concurrent;
using Xunit;

namespace Orchestra.Tests.UnitTests;

/// <summary>
/// Unit tests for TaskExecutionEngine component
/// Testing parallel execution, dependency resolution, and progress tracking
/// </summary>
public class TaskExecutionEngineTests
{
    private readonly TaskExecutionEngine _engine;
    private readonly Mock<IOrchestratorService> _mockOrchestratorService;
    private readonly ILogger<TaskExecutionEngine> _logger;

    public TaskExecutionEngineTests()
    {
        _logger = new LoggerFactory().CreateLogger<TaskExecutionEngine>();
        _mockOrchestratorService = new Mock<IOrchestratorService>();
        _engine = new TaskExecutionEngine(_logger, _mockOrchestratorService.Object);
    }

    [Fact]
    public async Task ExecuteTasksWithDependencyResolutionAsync_WithSingleTask_ExecutesSuccessfully()
    {
        var graph = CreateSingleTaskGraph();
        var executionOrder = new List<TaskNode> { graph.Nodes.Values.First() };
        var context = CreateExecutionContext();
        var progress = new Mock<IProgress<BatchProgress>>();

        _mockOrchestratorService
            .Setup(o => o.QueueTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Orchestra.Web.Models.TaskPriority>()))
            .ReturnsAsync(true);

        var result = await _engine.ExecuteTasksWithDependencyResolutionAsync(
            graph, executionOrder, context, progress.Object, CancellationToken.None);

        Assert.Equal(1, result.TotalTasks);
        Assert.Single(result.SuccessfulTasks);
        Assert.Empty(result.FailedTasks);
    }

    [Fact]
    public async Task ExecuteTasksWithDependencyResolutionAsync_WithFailedTask_ReportsFailure()
    {
        var graph = CreateSingleTaskGraph();
        var executionOrder = new List<TaskNode> { graph.Nodes.Values.First() };
        var context = CreateExecutionContext();
        var progress = new Mock<IProgress<BatchProgress>>();

        _mockOrchestratorService
            .Setup(o => o.QueueTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Orchestra.Web.Models.TaskPriority>()))
            .ThrowsAsync(new Exception("Task failed"));

        var result = await _engine.ExecuteTasksWithDependencyResolutionAsync(
            graph, executionOrder, context, progress.Object, CancellationToken.None);

        Assert.Equal(1, result.TotalTasks);
        Assert.Empty(result.SuccessfulTasks);
        Assert.Single(result.FailedTasks);
        Assert.Equal("Task failed", result.FailedTasks.First().Message);
    }

    [Fact]
    public async Task ExecuteTasksWithDependencyResolutionAsync_WithLinearDependency_ExecutesInOrder()
    {
        var graph = CreateLinearDependencyGraph();
        var executionOrder = graph.Nodes.Values.OrderBy(n => n.TaskId).ToList();
        var context = CreateExecutionContext();
        var progress = new Mock<IProgress<BatchProgress>>();
        var executionTimes = new ConcurrentBag<(string TaskId, DateTime Time)>();

        // Create mapping from command to TaskId
        var commandToTaskId = new Dictionary<string, string>
        {
            { "cmd1", "1" },
            { "cmd2", "2" }
        };

        _mockOrchestratorService
            .Setup(o => o.QueueTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Orchestra.Web.Models.TaskPriority>()))
            .Returns(async (string command, string repo, Orchestra.Web.Models.TaskPriority priority) =>
            {
                var taskId = commandToTaskId.ContainsKey(command) ? commandToTaskId[command] : command;
                executionTimes.Add((taskId, DateTime.UtcNow));
                await Task.Delay(10); // Small delay to ensure ordering
                return true;
            });

        var result = await _engine.ExecuteTasksWithDependencyResolutionAsync(
            graph, executionOrder, context, progress.Object, CancellationToken.None);

        Assert.Equal(2, result.TotalTasks);
        Assert.Equal(2, result.SuccessfulTasks.Count);

        var orderedExecutions = executionTimes.OrderBy(e => e.Time).ToList();
        Assert.Equal("1", orderedExecutions[0].TaskId);
        Assert.Equal("2", orderedExecutions[1].TaskId);
    }

    [Fact]
    public async Task ExecuteTasksWithDependencyResolutionAsync_WithStopOnFirstError_StopsOnFailure()
    {
        var graph = CreateLinearDependencyGraph();
        var executionOrder = graph.Nodes.Values.OrderBy(n => n.TaskId).ToList();
        var options = new BatchExecutionOptions(ErrorPolicy: BatchErrorPolicy.StopOnFirstError);
        var context = CreateExecutionContext(options);
        var progress = new Mock<IProgress<BatchProgress>>();

        _mockOrchestratorService
            .Setup(o => o.QueueTaskAsync("cmd1", It.IsAny<string>(), It.IsAny<Orchestra.Web.Models.TaskPriority>()))
            .ThrowsAsync(new Exception("First task failed"));

        _mockOrchestratorService
            .Setup(o => o.QueueTaskAsync("cmd2", It.IsAny<string>(), It.IsAny<Orchestra.Web.Models.TaskPriority>()))
            .ReturnsAsync(true);

        var result = await _engine.ExecuteTasksWithDependencyResolutionAsync(
            graph, executionOrder, context, progress.Object, CancellationToken.None);

        Assert.Equal(2, result.TotalTasks);
        Assert.Empty(result.SuccessfulTasks);
        Assert.Single(result.FailedTasks);
        Assert.Equal("First task failed", result.FailedTasks.First().Message);
    }

    [Fact]
    public async Task ExecuteTasksWithDependencyResolutionAsync_WithProgressReporting_ReportsProgress()
    {
        var graph = CreateSingleTaskGraph();
        var executionOrder = new List<TaskNode> { graph.Nodes.Values.First() };
        var context = CreateExecutionContext();
        var progressReports = new List<BatchProgress>();
        var progress = new Progress<BatchProgress>(p => progressReports.Add(p));

        _mockOrchestratorService
            .Setup(o => o.QueueTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Orchestra.Web.Models.TaskPriority>()))
            .ReturnsAsync(true);

        await _engine.ExecuteTasksWithDependencyResolutionAsync(
            graph, executionOrder, context, progress, CancellationToken.None);

        Assert.NotEmpty(progressReports);
        Assert.All(progressReports, p => Assert.Equal(context.BatchId, p.BatchId));
        Assert.All(progressReports, p => Assert.Equal(1, p.TotalTasks));
    }

    private ExecutionGraph CreateSingleTaskGraph()
    {
        var graph = new ExecutionGraph();
        var node = new TaskNode("1", "cmd1", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string>());
        graph.AddNode(node);
        return graph;
    }

    private ExecutionGraph CreateLinearDependencyGraph()
    {
        var graph = new ExecutionGraph();

        var node1 = new TaskNode("1", "cmd1", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string>());
        var node2 = new TaskNode("2", "cmd2", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string> { "1" });

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddEdge(new DependencyEdge(node1, node2, true));

        return graph;
    }

    private TaskExecutionContext CreateExecutionContext(BatchExecutionOptions? options = null)
    {
        options ??= new BatchExecutionOptions();
        return new TaskExecutionContext(
            Guid.NewGuid().ToString(),
            1,
            options,
            System.Diagnostics.Stopwatch.StartNew());
    }
}