using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Services;
using Orchestra.Web.Models;
using Orchestra.Web.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests;

/// <summary>
/// Unit tests for BatchTaskExecutor coordinator component
/// Testing validation, coordination, and integration with sub-components
/// </summary>
public class BatchTaskExecutorTests
{
    private readonly BatchTaskExecutor _executor;
    private readonly Mock<IOrchestratorService> _mockOrchestratorService;
    private readonly Mock<DependencyGraphBuilder> _mockGraphBuilder;
    private readonly Mock<TaskExecutionEngine> _mockExecutionEngine;
    private readonly ILogger<BatchTaskExecutor> _logger;

    public BatchTaskExecutorTests()
    {
        _logger = new LoggerFactory().CreateLogger<BatchTaskExecutor>();
        _mockOrchestratorService = new Mock<IOrchestratorService>();
        _mockGraphBuilder = new Mock<DependencyGraphBuilder>(Mock.Of<ILogger<DependencyGraphBuilder>>());
        _mockExecutionEngine = new Mock<TaskExecutionEngine>(Mock.Of<ILogger<TaskExecutionEngine>>(), _mockOrchestratorService.Object);

        _executor = new BatchTaskExecutor(
            _logger,
            _mockOrchestratorService.Object,
            _mockGraphBuilder.Object,
            _mockExecutionEngine.Object);
    }

    [Fact]
    public async Task ExecuteBatchAsync_WithValidTasks_ExecutesSuccessfully()
    {
        var tasks = CreateValidTaskList();
        var options = new BatchExecutionOptions();
        var progress = new Mock<IProgress<BatchProgress>>();
        var expectedGraph = CreateMockGraph();
        var expectedOrder = CreateMockExecutionOrder();
        var expectedResult = CreateMockResult();

        SetupMockComponents(expectedGraph, expectedOrder, expectedResult);

        var result = await _executor.ExecuteBatchAsync(tasks, options, progress.Object);

        Assert.Equal(expectedResult.BatchId, result.BatchId);
        Assert.Equal(expectedResult.TotalTasks, result.TotalTasks);
        VerifyMockComponentsCalled(tasks, expectedGraph, expectedOrder);
    }

    [Fact]
    public async Task ExecuteBatchAsync_WithEmptyTasks_ThrowsArgumentException()
    {
        var tasks = new List<BatchTaskRequest>();
        var options = new BatchExecutionOptions();
        var progress = new Mock<IProgress<BatchProgress>>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _executor.ExecuteBatchAsync(tasks, options, progress.Object));
    }

    [Fact]
    public async Task ExecuteBatchAsync_WithTooManyTasks_ThrowsArgumentException()
    {
        var tasks = Enumerable.Range(1, 101)
            .Select(i => new BatchTaskRequest($"cmd{i}", $"repo{i}", Orchestra.Web.Models.TaskPriority.Normal))
            .ToList();
        var options = new BatchExecutionOptions();
        var progress = new Mock<IProgress<BatchProgress>>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _executor.ExecuteBatchAsync(tasks, options, progress.Object));
    }

    [Fact]
    public async Task ExecuteBatchAsync_WithInvalidConcurrency_ThrowsArgumentException()
    {
        var tasks = CreateValidTaskList();
        var options = new BatchExecutionOptions(MaxConcurrency: 0);
        var progress = new Mock<IProgress<BatchProgress>>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _executor.ExecuteBatchAsync(tasks, options, progress.Object));
    }

    [Fact]
    public async Task ExecuteBatchAsync_WithCircularDependency_ThrowsCircularDependencyException()
    {
        var tasks = CreateValidTaskList();
        var options = new BatchExecutionOptions();
        var progress = new Mock<IProgress<BatchProgress>>();
        var expectedGraph = CreateMockGraph();

        _mockGraphBuilder
            .Setup(g => g.BuildDependencyGraphAsync(It.IsAny<List<BatchTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGraph);

        _mockGraphBuilder
            .Setup(g => g.ValidateNoCyclicDependencies(It.IsAny<ExecutionGraph>()))
            .Throws(new CircularDependencyException("Circular dependency detected"));

        await Assert.ThrowsAsync<CircularDependencyException>(() =>
            _executor.ExecuteBatchAsync(tasks, options, progress.Object));
    }

    [Fact]
    public async Task ValidateBatchAsync_WithValidTasks_ReturnsTrue()
    {
        var tasks = CreateValidTaskList();
        var expectedGraph = CreateMockGraph();

        _mockGraphBuilder
            .Setup(g => g.BuildDependencyGraphAsync(It.IsAny<List<BatchTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGraph);

        _mockGraphBuilder
            .Setup(g => g.ValidateNoCyclicDependencies(It.IsAny<ExecutionGraph>()));

        var result = await _executor.ValidateBatchAsync(tasks);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateBatchAsync_WithInvalidTasks_ReturnsFalse()
    {
        var tasks = CreateValidTaskList();

        _mockGraphBuilder
            .Setup(g => g.BuildDependencyGraphAsync(It.IsAny<List<BatchTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid dependency"));

        var result = await _executor.ValidateBatchAsync(tasks);

        Assert.False(result);
    }

    [Fact]
    public async Task CancelBatchAsync_WithActiveBatch_CancelsSuccessfully()
    {
        var batchId = "test-batch-id";

        var exception = await Record.ExceptionAsync(() => _executor.CancelBatchAsync(batchId));

        Assert.Null(exception);
    }

    private List<BatchTaskRequest> CreateValidTaskList()
    {
        return new List<BatchTaskRequest>
        {
            new("cmd1", "repo1", Orchestra.Web.Models.TaskPriority.Normal, "task1"),
            new("cmd2", "repo1", Orchestra.Web.Models.TaskPriority.Normal, "task2")
        };
    }

    private ExecutionGraph CreateMockGraph()
    {
        var graph = new ExecutionGraph();
        var node = new TaskNode("task1", "cmd1", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string>());
        graph.AddNode(node);
        return graph;
    }

    private List<TaskNode> CreateMockExecutionOrder()
    {
        return new List<TaskNode>
        {
            new("task1", "cmd1", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string>())
        };
    }

    private BatchExecutionResult CreateMockResult()
    {
        return new BatchExecutionResult(
            "test-batch-id",
            1,
            new List<BatchTaskResult>(),
            new List<BatchTaskResult>(),
            DateTime.UtcNow);
    }

    private void SetupMockComponents(ExecutionGraph graph, List<TaskNode> order, BatchExecutionResult result)
    {
        _mockGraphBuilder
            .Setup(g => g.BuildDependencyGraphAsync(It.IsAny<List<BatchTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);

        _mockGraphBuilder
            .Setup(g => g.ValidateNoCyclicDependencies(It.IsAny<ExecutionGraph>()));

        _mockGraphBuilder
            .Setup(g => g.CalculateTopologicalOrder(It.IsAny<ExecutionGraph>()))
            .Returns(order);

        _mockExecutionEngine
            .Setup(e => e.ExecuteTasksWithDependencyResolutionAsync(
                It.IsAny<ExecutionGraph>(),
                It.IsAny<List<TaskNode>>(),
                It.IsAny<TaskExecutionContext>(),
                It.IsAny<IProgress<BatchProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private void VerifyMockComponentsCalled(List<BatchTaskRequest> tasks, ExecutionGraph graph, List<TaskNode> order)
    {
        _mockGraphBuilder.Verify(g => g.BuildDependencyGraphAsync(tasks, It.IsAny<CancellationToken>()), Times.Once);
        _mockGraphBuilder.Verify(g => g.ValidateNoCyclicDependencies(graph), Times.Once);
        _mockGraphBuilder.Verify(g => g.CalculateTopologicalOrder(graph), Times.Once);
        _mockExecutionEngine.Verify(e => e.ExecuteTasksWithDependencyResolutionAsync(
            graph, order, It.IsAny<TaskExecutionContext>(), It.IsAny<IProgress<BatchProgress>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}