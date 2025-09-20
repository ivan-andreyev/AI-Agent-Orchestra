using Orchestra.Web.Models;

namespace Orchestra.Web.Services;

/// <summary>
/// Service responsible for building and validating dependency graphs for batch task execution
/// Handles DAG construction, circular dependency detection, and topological ordering
/// </summary>
public class DependencyGraphBuilder : IDependencyGraphBuilder
{
    private readonly ILogger<DependencyGraphBuilder> _logger;

    public DependencyGraphBuilder(ILogger<DependencyGraphBuilder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Build directed acyclic graph (DAG) from task dependencies
    /// </summary>
    public async Task<ExecutionGraph> BuildDependencyGraphAsync(List<BatchTaskRequest> tasks, CancellationToken cancellationToken = default)
    {
        var graph = new ExecutionGraph();
        var taskNodes = new Dictionary<string, TaskNode>();

        // Create nodes for all tasks
        foreach (var task in tasks)
        {
            var taskId = task.TaskId ?? Guid.NewGuid().ToString();
            var node = new TaskNode(
                taskId,
                task.Command,
                task.TargetRepository,
                task.Priority,
                task.EstimatedDuration ?? TimeSpan.FromMinutes(2),
                task.RequiresPreviousSuccess,
                task.DependsOn ?? new List<string>());

            taskNodes[taskId] = node;
            graph.AddNode(node);
        }

        // Create edges for dependencies
        foreach (var task in tasks)
        {
            var taskId = task.TaskId ?? Guid.NewGuid().ToString();
            if (task.DependsOn?.Any() == true)
            {
                foreach (var dependencyId in task.DependsOn)
                {
                    if (taskNodes.TryGetValue(dependencyId, out var dependencyNode))
                    {
                        var edge = new DependencyEdge(dependencyNode, taskNodes[taskId], task.RequiresPreviousSuccess);
                        graph.AddEdge(edge);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Task {taskId} depends on non-existent task {dependencyId}");
                    }
                }
            }
        }

        await Task.CompletedTask;
        return graph;
    }

    /// <summary>
    /// Detect circular dependencies using depth-first search
    /// </summary>
    public void ValidateNoCyclicDependencies(ExecutionGraph graph)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var node in graph.Nodes.Values)
        {
            if (!visited.Contains(node.TaskId))
            {
                if (HasCyclicDependencyDFS(node, graph, visited, recursionStack))
                {
                    throw new CircularDependencyException($"Circular dependency detected involving task: {node.TaskId}");
                }
            }
        }
    }

    private bool HasCyclicDependencyDFS(TaskNode node, ExecutionGraph graph, HashSet<string> visited, HashSet<string> recursionStack)
    {
        visited.Add(node.TaskId);
        recursionStack.Add(node.TaskId);

        // Check all outgoing edges (dependencies)
        var outgoingEdges = graph.GetOutgoingEdges(node.TaskId);
        foreach (var edge in outgoingEdges)
        {
            var dependentNode = edge.ToNode;

            if (!visited.Contains(dependentNode.TaskId))
            {
                if (HasCyclicDependencyDFS(dependentNode, graph, visited, recursionStack))
                {
                    return true;
                }
            }
            else if (recursionStack.Contains(dependentNode.TaskId))
            {
                return true; // Back edge found - cycle detected
            }
        }

        recursionStack.Remove(node.TaskId);
        return false;
    }

    /// <summary>
    /// Calculate topological ordering for execution sequence using Kahn's algorithm
    /// </summary>
    public List<TaskNode> CalculateTopologicalOrder(ExecutionGraph graph)
    {
        var inDegree = new Dictionary<string, int>();
        var result = new List<TaskNode>();
        var queue = new Queue<TaskNode>();

        // Initialize in-degree for all nodes
        foreach (var node in graph.Nodes.Values)
        {
            inDegree[node.TaskId] = graph.GetIncomingEdges(node.TaskId).Count();

            if (inDegree[node.TaskId] == 0)
            {
                queue.Enqueue(node);
            }
        }

        // Process nodes with no incoming edges
        while (queue.Count > 0)
        {
            var currentNode = queue.Dequeue();
            result.Add(currentNode);

            // Reduce in-degree for all dependent nodes
            var outgoingEdges = graph.GetOutgoingEdges(currentNode.TaskId);
            foreach (var edge in outgoingEdges)
            {
                inDegree[edge.ToNode.TaskId]--;

                if (inDegree[edge.ToNode.TaskId] == 0)
                {
                    queue.Enqueue(edge.ToNode);
                }
            }
        }

        if (result.Count != graph.Nodes.Count)
        {
            throw new CircularDependencyException("Unable to calculate topological order due to circular dependencies");
        }

        return result;
    }
}