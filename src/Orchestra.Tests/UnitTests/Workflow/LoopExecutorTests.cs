using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Models.Workflow;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests.Workflow;

/// <summary>
/// Тесты для LoopExecutor - исполнителя циклов в workflow
/// </summary>
public class LoopExecutorTests
{
    private readonly Mock<ILogger<LoopExecutor>> _mockLogger;
    private readonly Mock<IExpressionEvaluator> _mockExpressionEvaluator;
    private readonly LoopExecutor _loopExecutor;

    public LoopExecutorTests()
    {
        _mockLogger = new Mock<ILogger<LoopExecutor>>();
        _mockExpressionEvaluator = new Mock<IExpressionEvaluator>();
        _loopExecutor = new LoopExecutor(_mockLogger.Object, _mockExpressionEvaluator.Object);
    }

    [Fact]
    public async Task ExecuteLoopAsync_ForEachLoop_IteratesOverCollection()
    {
        // Arrange
        var loopDefinition = new LoopDefinition(
            Type: LoopType.ForEach,
            Collection: "$items",
            IteratorVariable: "item",
            IndexVariable: "index"
        );

        var nestedSteps = new List<WorkflowStep>
        {
            new("step1", WorkflowStepType.Task, "process_item", new Dictionary<string, object>(), new List<string>())
        };

        var context = new WorkflowContext(
            new Dictionary<string, object>
            {
                ["items"] = new[] { "item1", "item2", "item3" }
            },
            "test-execution",
            CancellationToken.None
        );

        var stepExecutorCallCount = 0;
        Task<List<WorkflowStepResult>> StepExecutor(List<WorkflowStep> steps, WorkflowContext ctx)
        {
            stepExecutorCallCount++;
            var results = steps.Select(s => new WorkflowStepResult(
                s.Id,
                WorkflowStatus.Completed,
                new Dictionary<string, object> { ["processed"] = true }
            )).ToList();
            return Task.FromResult(results);
        }

        // Act
        var result = await _loopExecutor.ExecuteLoopAsync(loopDefinition, nestedSteps, context, StepExecutor);

        // Assert
        Assert.Equal(LoopType.ForEach, result.LoopType);
        Assert.Equal(LoopExecutionStatus.Completed, result.Status);
        Assert.Equal(3, result.TotalIterations);
        Assert.Equal(3, result.SuccessfulIterations);
        Assert.Equal(0, result.FailedIterations);
        Assert.Equal(3, stepExecutorCallCount);
        Assert.Equal(3, result.IterationResults.Count);
    }

    [Fact]
    public async Task ExecuteLoopAsync_WhileLoop_ExecutesWhileConditionTrue()
    {
        // Arrange
        var loopDefinition = new LoopDefinition(
            Type: LoopType.While,
            Condition: "$counter < 3",
            MaxIterations: 10
        );

        var nestedSteps = new List<WorkflowStep>
        {
            new("step1", WorkflowStepType.Task, "increment", new Dictionary<string, object>(), new List<string>())
        };

        var context = new WorkflowContext(
            new Dictionary<string, object>
            {
                ["counter"] = 0
            },
            "test-execution",
            CancellationToken.None
        );

        var iterationCount = 0;
        _mockExpressionEvaluator
            .Setup(e => e.EvaluateAsync(It.IsAny<string>(), It.IsAny<WorkflowExecutionContext>()))
            .ReturnsAsync(() => iterationCount < 3); // Выполняется 3 раза

        Task<List<WorkflowStepResult>> StepExecutor(List<WorkflowStep> steps, WorkflowContext ctx)
        {
            iterationCount++;
            var results = steps.Select(s => new WorkflowStepResult(
                s.Id,
                WorkflowStatus.Completed,
                new Dictionary<string, object> { ["iteration"] = iterationCount }
            )).ToList();
            return Task.FromResult(results);
        }

        // Act
        var result = await _loopExecutor.ExecuteLoopAsync(loopDefinition, nestedSteps, context, StepExecutor);

        // Assert
        Assert.Equal(LoopType.While, result.LoopType);
        Assert.Equal(LoopExecutionStatus.Completed, result.Status);
        Assert.Equal(3, result.TotalIterations);
        Assert.Equal(3, result.SuccessfulIterations);
        Assert.Equal(0, result.FailedIterations);
    }

    [Fact]
    public async Task ExecuteLoopAsync_RetryLoop_RetriesUntilSuccess()
    {
        // Arrange
        var loopDefinition = new LoopDefinition(
            Type: LoopType.Retry,
            MaxIterations: 5
        );

        var nestedSteps = new List<WorkflowStep>
        {
            new("step1", WorkflowStepType.Task, "unreliable_task", new Dictionary<string, object>(), new List<string>())
        };

        var context = new WorkflowContext(
            new Dictionary<string, object>(),
            "test-execution",
            CancellationToken.None
        );

        var attemptCount = 0;
        Task<List<WorkflowStepResult>> StepExecutor(List<WorkflowStep> steps, WorkflowContext ctx)
        {
            attemptCount++;
            var status = attemptCount < 3 ? WorkflowStatus.Failed : WorkflowStatus.Completed; // Успех на 3-й попытке
            var results = steps.Select(s => new WorkflowStepResult(
                s.Id,
                status,
                new Dictionary<string, object> { ["attempt"] = attemptCount },
                status == WorkflowStatus.Failed ? new Exception($"Failed attempt {attemptCount}") : null
            )).ToList();
            return Task.FromResult(results);
        }

        // Act
        var result = await _loopExecutor.ExecuteLoopAsync(loopDefinition, nestedSteps, context, StepExecutor);

        // Assert
        Assert.Equal(LoopType.Retry, result.LoopType);
        Assert.Equal(LoopExecutionStatus.Completed, result.Status);
        Assert.Equal(3, result.TotalIterations);
        Assert.Equal(1, result.SuccessfulIterations);
        Assert.Equal(2, result.FailedIterations);
    }

    [Fact]
    public async Task ExecuteLoopAsync_InfiniteLoopProtection_StopsAtMaxIterations()
    {
        // Arrange
        var loopDefinition = new LoopDefinition(
            Type: LoopType.While,
            Condition: "true", // Всегда истинно
            MaxIterations: 5
        );

        var nestedSteps = new List<WorkflowStep>
        {
            new("step1", WorkflowStepType.Task, "endless_task", new Dictionary<string, object>(), new List<string>())
        };

        var context = new WorkflowContext(
            new Dictionary<string, object>(),
            "test-execution",
            CancellationToken.None
        );

        _mockExpressionEvaluator
            .Setup(e => e.EvaluateAsync(It.IsAny<string>(), It.IsAny<WorkflowExecutionContext>()))
            .ReturnsAsync(true); // Всегда возвращает true

        var stepExecutorCallCount = 0;
        Task<List<WorkflowStepResult>> StepExecutor(List<WorkflowStep> steps, WorkflowContext ctx)
        {
            stepExecutorCallCount++;
            var results = steps.Select(s => new WorkflowStepResult(
                s.Id,
                WorkflowStatus.Completed,
                new Dictionary<string, object> { ["iteration"] = stepExecutorCallCount }
            )).ToList();
            return Task.FromResult(results);
        }

        // Act
        var result = await _loopExecutor.ExecuteLoopAsync(loopDefinition, nestedSteps, context, StepExecutor);

        // Assert
        Assert.Equal(LoopType.While, result.LoopType);
        Assert.Equal(LoopExecutionStatus.MaxIterationsReached, result.Status);
        Assert.Equal(5, result.TotalIterations);
        Assert.Equal(5, result.SuccessfulIterations);
        Assert.Equal(0, result.FailedIterations);
        Assert.Equal(5, stepExecutorCallCount);
    }

    [Fact]
    public async Task ExecuteLoopAsync_EmptyCollection_CompletesImmediately()
    {
        // Arrange
        var loopDefinition = new LoopDefinition(
            Type: LoopType.ForEach,
            Collection: "$items"
        );

        var nestedSteps = new List<WorkflowStep>
        {
            new("step1", WorkflowStepType.Task, "process_item", new Dictionary<string, object>(), new List<string>())
        };

        var context = new WorkflowContext(
            new Dictionary<string, object>
            {
                ["items"] = new string[0] // Пустая коллекция
            },
            "test-execution",
            CancellationToken.None
        );

        var stepExecutorCallCount = 0;
        Task<List<WorkflowStepResult>> StepExecutor(List<WorkflowStep> steps, WorkflowContext ctx)
        {
            stepExecutorCallCount++;
            return Task.FromResult(new List<WorkflowStepResult>());
        }

        // Act
        var result = await _loopExecutor.ExecuteLoopAsync(loopDefinition, nestedSteps, context, StepExecutor);

        // Assert
        Assert.Equal(LoopType.ForEach, result.LoopType);
        Assert.Equal(LoopExecutionStatus.Completed, result.Status);
        Assert.Equal(0, result.TotalIterations);
        Assert.Equal(0, stepExecutorCallCount);
        Assert.Empty(result.IterationResults);
    }

    [Fact]
    public async Task ExecuteLoopAsync_MissingCollection_CompletesWithoutIterations()
    {
        // Arrange
        var loopDefinition = new LoopDefinition(
            Type: LoopType.ForEach,
            Collection: "$missing_items"
        );

        var nestedSteps = new List<WorkflowStep>
        {
            new("step1", WorkflowStepType.Task, "process_item", new Dictionary<string, object>(), new List<string>())
        };

        var context = new WorkflowContext(
            new Dictionary<string, object>(), // Нет переменной missing_items
            "test-execution",
            CancellationToken.None
        );

        var stepExecutorCallCount = 0;
        Task<List<WorkflowStepResult>> StepExecutor(List<WorkflowStep> steps, WorkflowContext ctx)
        {
            stepExecutorCallCount++;
            return Task.FromResult(new List<WorkflowStepResult>());
        }

        // Act
        var result = await _loopExecutor.ExecuteLoopAsync(loopDefinition, nestedSteps, context, StepExecutor);

        // Assert
        Assert.Equal(LoopType.ForEach, result.LoopType);
        Assert.Equal(LoopExecutionStatus.Completed, result.Status);
        Assert.Equal(0, result.TotalIterations);
        Assert.Equal(0, stepExecutorCallCount);
        Assert.Empty(result.IterationResults);
    }

    [Fact]
    public async Task ExecuteLoopAsync_ThrowsForNullArguments()
    {
        // Arrange
        var loopDefinition = new LoopDefinition(LoopType.ForEach);
        var steps = new List<WorkflowStep>();
        var context = new WorkflowContext(new Dictionary<string, object>(), "test", CancellationToken.None);
        Task<List<WorkflowStepResult>> stepExecutor(List<WorkflowStep> s, WorkflowContext c) => Task.FromResult(new List<WorkflowStepResult>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _loopExecutor.ExecuteLoopAsync(null!, steps, context, stepExecutor));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _loopExecutor.ExecuteLoopAsync(loopDefinition, null!, context, stepExecutor));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _loopExecutor.ExecuteLoopAsync(loopDefinition, steps, null!, stepExecutor));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _loopExecutor.ExecuteLoopAsync(loopDefinition, steps, context, null!));
    }

    [Fact]
    public async Task ExecuteLoopAsync_ForEachRequiresCollection()
    {
        // Arrange
        var loopDefinition = new LoopDefinition(
            Type: LoopType.ForEach,
            Collection: null // Явно указываем null
        );

        var nestedSteps = new List<WorkflowStep>
        {
            new("step1", WorkflowStepType.Task, "task", new Dictionary<string, object>(), new List<string>())
        };

        var context = new WorkflowContext(
            new Dictionary<string, object>(),
            "test-execution",
            CancellationToken.None
        );

        Task<List<WorkflowStepResult>> StepExecutor(List<WorkflowStep> steps, WorkflowContext ctx)
        {
            return Task.FromResult(new List<WorkflowStepResult>());
        }

        // Act
        var result = await _loopExecutor.ExecuteLoopAsync(loopDefinition, nestedSteps, context, StepExecutor);

        // Assert
        Assert.Equal(LoopType.ForEach, result.LoopType);
        Assert.Equal(LoopExecutionStatus.Failed, result.Status);
        Assert.NotNull(result.Error);
        Assert.IsType<ArgumentException>(result.Error);
        Assert.Contains("ForEach цикл требует указания коллекции", result.Error.Message);
    }

    [Fact]
    public async Task ExecuteLoopAsync_WhileRequiresCondition()
    {
        // Arrange
        var loopDefinition = new LoopDefinition(
            Type: LoopType.While,
            Condition: null // Явно указываем null
        );

        var nestedSteps = new List<WorkflowStep>
        {
            new("step1", WorkflowStepType.Task, "task", new Dictionary<string, object>(), new List<string>())
        };

        var context = new WorkflowContext(
            new Dictionary<string, object>(),
            "test-execution",
            CancellationToken.None
        );

        Task<List<WorkflowStepResult>> StepExecutor(List<WorkflowStep> steps, WorkflowContext ctx)
        {
            return Task.FromResult(new List<WorkflowStepResult>());
        }

        // Act
        var result = await _loopExecutor.ExecuteLoopAsync(loopDefinition, nestedSteps, context, StepExecutor);

        // Assert
        Assert.Equal(LoopType.While, result.LoopType);
        Assert.Equal(LoopExecutionStatus.Failed, result.Status);
        Assert.NotNull(result.Error);
        Assert.IsType<ArgumentException>(result.Error);
        Assert.Contains("While цикл требует указания условия", result.Error.Message);
    }

    [Fact]
    public async Task ExecuteLoopAsync_UnsupportedLoopType_ThrowsNotSupportedException()
    {
        // Arrange
        var loopDefinition = new LoopDefinition((LoopType)999); // Несуществующий тип

        var nestedSteps = new List<WorkflowStep>
        {
            new("step1", WorkflowStepType.Task, "task", new Dictionary<string, object>(), new List<string>())
        };

        var context = new WorkflowContext(
            new Dictionary<string, object>(),
            "test-execution",
            CancellationToken.None
        );

        Task<List<WorkflowStepResult>> StepExecutor(List<WorkflowStep> steps, WorkflowContext ctx)
        {
            return Task.FromResult(new List<WorkflowStepResult>());
        }

        // Act
        var result = await _loopExecutor.ExecuteLoopAsync(loopDefinition, nestedSteps, context, StepExecutor);

        // Assert
        Assert.Equal((LoopType)999, result.LoopType);
        Assert.Equal(LoopExecutionStatus.Failed, result.Status);
        Assert.NotNull(result.Error);
        Assert.IsType<NotSupportedException>(result.Error);
        Assert.Contains("не поддерживается", result.Error.Message);
    }
}