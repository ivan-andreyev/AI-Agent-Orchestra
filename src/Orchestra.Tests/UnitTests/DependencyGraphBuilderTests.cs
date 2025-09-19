using Microsoft.Extensions.Logging;
using Orchestra.Web.Models;
using Orchestra.Web.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests;

/// <summary>
/// Unit tests for DependencyGraphBuilder component
/// Testing DAG construction, cycle detection, and topological ordering
/// </summary>
public class DependencyGraphBuilderTests
{
    private readonly DependencyGraphBuilder _builder;
    private readonly ILogger<DependencyGraphBuilder> _logger;

    public DependencyGraphBuilderTests()
    {
        _logger = new LoggerFactory().CreateLogger<DependencyGraphBuilder>();
        _builder = new DependencyGraphBuilder(_logger);
    }

    [Fact]
    public async Task BuildDependencyGraphAsync_WithSimpleTasks_CreatesCorrectGraph()
    {
        var tasks = new List<BatchTaskRequest>
        {
            new("task1", "repo1", Orchestra.Web.Models.TaskPriority.Normal, "1"),
            new("task2", "repo1", Orchestra.Web.Models.TaskPriority.Normal, "2", new List<string> { "1" })
        };

        var graph = await _builder.BuildDependencyGraphAsync(tasks);

        Assert.Equal(2, graph.Nodes.Count);
        Assert.Single(graph.Edges);
        Assert.Equal("1", graph.Edges.First().FromNode.TaskId);
        Assert.Equal("2", graph.Edges.First().ToNode.TaskId);
    }

    [Fact]
    public async Task BuildDependencyGraphAsync_WithMissingDependency_ThrowsException()
    {
        var tasks = new List<BatchTaskRequest>
        {
            new("task1", "repo1", Orchestra.Web.Models.TaskPriority.Normal, "1", new List<string> { "nonexistent" })
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _builder.BuildDependencyGraphAsync(tasks));
    }

    [Fact]
    public void ValidateNoCyclicDependencies_WithValidDAG_DoesNotThrow()
    {
        var graph = CreateSimpleLinearGraph();

        var exception = Record.Exception(() => _builder.ValidateNoCyclicDependencies(graph));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateNoCyclicDependencies_WithCircularDependency_ThrowsException()
    {
        var graph = CreateCircularGraph();

        Assert.Throws<CircularDependencyException>(() =>
            _builder.ValidateNoCyclicDependencies(graph));
    }

    [Fact]
    public void CalculateTopologicalOrder_WithLinearGraph_ReturnsCorrectOrder()
    {
        var graph = CreateSimpleLinearGraph();

        var order = _builder.CalculateTopologicalOrder(graph);

        Assert.Equal(3, order.Count);
        Assert.Equal("1", order[0].TaskId);
        Assert.Equal("2", order[1].TaskId);
        Assert.Equal("3", order[2].TaskId);
    }

    [Fact]
    public void CalculateTopologicalOrder_WithParallelBranches_ReturnsValidOrder()
    {
        var graph = CreateParallelBranchGraph();

        var order = _builder.CalculateTopologicalOrder(graph);

        Assert.Equal(4, order.Count);
        Assert.Equal("1", order[0].TaskId); // Root first
        Assert.Contains("2", order.Select(n => n.TaskId).Take(3)); // Parallel tasks
        Assert.Contains("3", order.Select(n => n.TaskId).Take(3));
        Assert.Equal("4", order[3].TaskId); // Dependent task last
    }

    private ExecutionGraph CreateSimpleLinearGraph()
    {
        var graph = new ExecutionGraph();

        var node1 = new TaskNode("1", "cmd1", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string>());
        var node2 = new TaskNode("2", "cmd2", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string> { "1" });
        var node3 = new TaskNode("3", "cmd3", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string> { "2" });

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);

        graph.AddEdge(new DependencyEdge(node1, node2, true));
        graph.AddEdge(new DependencyEdge(node2, node3, true));

        return graph;
    }

    private ExecutionGraph CreateCircularGraph()
    {
        var graph = new ExecutionGraph();

        var node1 = new TaskNode("1", "cmd1", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string> { "2" });
        var node2 = new TaskNode("2", "cmd2", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string> { "1" });

        graph.AddNode(node1);
        graph.AddNode(node2);

        graph.AddEdge(new DependencyEdge(node1, node2, true));
        graph.AddEdge(new DependencyEdge(node2, node1, true));

        return graph;
    }

    private ExecutionGraph CreateParallelBranchGraph()
    {
        var graph = new ExecutionGraph();

        var node1 = new TaskNode("1", "cmd1", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string>());
        var node2 = new TaskNode("2", "cmd2", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string> { "1" });
        var node3 = new TaskNode("3", "cmd3", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string> { "1" });
        var node4 = new TaskNode("4", "cmd4", "repo1", Orchestra.Web.Models.TaskPriority.Normal, TimeSpan.FromMinutes(1), true, new List<string> { "2", "3" });

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);
        graph.AddNode(node4);

        graph.AddEdge(new DependencyEdge(node1, node2, true));
        graph.AddEdge(new DependencyEdge(node1, node3, true));
        graph.AddEdge(new DependencyEdge(node2, node4, true));
        graph.AddEdge(new DependencyEdge(node3, node4, true));

        return graph;
    }
}