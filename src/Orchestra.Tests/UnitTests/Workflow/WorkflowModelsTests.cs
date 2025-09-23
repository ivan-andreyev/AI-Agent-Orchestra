using Orchestra.Core.Models.Workflow;

namespace Orchestra.Tests.UnitTests.Workflow;

/// <summary>
/// Unit tests for Workflow model classes
/// </summary>
public class WorkflowModelsTests
{
    [Fact]
    public void WorkflowDefinition_WithValidData_ShouldCreateCorrectly()
    {
        // Arrange
        var id = "test-workflow";
        var name = "Test Workflow";
        var steps = new List<WorkflowStep> { CreateValidWorkflowStep() };
        var variables = new Dictionary<string, VariableDefinition>();
        var metadata = CreateValidMetadata();

        // Act
        var workflow = new WorkflowDefinition(id, name, steps, variables, metadata);

        // Assert
        Assert.Equal(id, workflow.Id);
        Assert.Equal(name, workflow.Name);
        Assert.Equal(steps, workflow.Steps);
        Assert.Equal(variables, workflow.Variables);
        Assert.Equal(metadata, workflow.Metadata);
    }

    [Fact]
    public void WorkflowStep_WithValidData_ShouldCreateCorrectly()
    {
        // Arrange
        var id = "step-1";
        var type = WorkflowStepType.Task;
        var command = "test-command";
        var parameters = new Dictionary<string, object> { ["param1"] = "value1" };
        var dependsOn = new List<string> { "step-0" };

        // Act
        var step = new WorkflowStep(id, type, command, parameters, dependsOn);

        // Assert
        Assert.Equal(id, step.Id);
        Assert.Equal(type, step.Type);
        Assert.Equal(command, step.Command);
        Assert.Equal(parameters, step.Parameters);
        Assert.Equal(dependsOn, step.DependsOn);
        Assert.Null(step.Condition);
        Assert.Null(step.RetryPolicy);
    }

    [Fact]
    public void WorkflowStep_WithConditionalLogic_ShouldCreateCorrectly()
    {
        // Arrange
        var condition = new ConditionalLogic("x > 5", new List<string> { "step-true" }, new List<string> { "step-false" });
        var step = new WorkflowStep(
            "step-1",
            WorkflowStepType.Condition,
            "evaluate",
            new Dictionary<string, object>(),
            new List<string>(),
            condition
        );

        // Assert
        Assert.NotNull(step.Condition);
        Assert.Equal("x > 5", step.Condition.Expression);
        Assert.Contains("step-true", step.Condition.TruePath!);
        Assert.Contains("step-false", step.Condition.FalsePath!);
    }

    [Fact]
    public void WorkflowStep_WithRetryPolicy_ShouldCreateCorrectly()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(3);
        var step = new WorkflowStep(
            "step-1",
            WorkflowStepType.Task,
            "retry-command",
            new Dictionary<string, object>(),
            new List<string>(),
            null,
            retryPolicy
        );

        // Assert
        Assert.NotNull(step.RetryPolicy);
        Assert.Equal(3, step.RetryPolicy.MaxRetryCount);
        Assert.Equal(TimeSpan.FromSeconds(1), step.RetryPolicy.BaseDelay);
        Assert.Equal(2.0, step.RetryPolicy.BackoffMultiplier);
    }

    [Fact]
    public void WorkflowExecutionResult_WithValidData_ShouldCreateCorrectly()
    {
        // Arrange
        var executionId = "exec-1";
        var status = WorkflowStatus.Completed;
        var outputVariables = new Dictionary<string, object> { ["result"] = "success" };
        var stepResults = new List<WorkflowStepResult>();

        // Act
        var result = new WorkflowExecutionResult(executionId, status, outputVariables, stepResults);

        // Assert
        Assert.Equal(executionId, result.ExecutionId);
        Assert.Equal(status, result.Status);
        Assert.Equal(outputVariables, result.OutputVariables);
        Assert.Equal(stepResults, result.StepResults);
        Assert.Null(result.Error);
    }

    [Fact]
    public void WorkflowExecutionResult_WithError_ShouldCreateCorrectly()
    {
        // Arrange
        var executionId = "exec-1";
        var status = WorkflowStatus.Failed;
        var error = new InvalidOperationException("Test error");

        // Act
        var result = new WorkflowExecutionResult(
            executionId,
            status,
            new Dictionary<string, object>(),
            new List<WorkflowStepResult>(),
            error
        );

        // Assert
        Assert.Equal(executionId, result.ExecutionId);
        Assert.Equal(status, result.Status);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void WorkflowStepResult_WithValidData_ShouldCreateCorrectly()
    {
        // Arrange
        var stepId = "step-1";
        var status = WorkflowStatus.Completed;
        var output = new Dictionary<string, object> { ["result"] = 42 };
        var duration = TimeSpan.FromSeconds(2);

        // Act
        var stepResult = new WorkflowStepResult(stepId, status, output, null, duration);

        // Assert
        Assert.Equal(stepId, stepResult.StepId);
        Assert.Equal(status, stepResult.Status);
        Assert.Equal(output, stepResult.Output);
        Assert.Null(stepResult.Error);
        Assert.Equal(duration, stepResult.Duration);
    }

    [Fact]
    public void WorkflowContext_WithValidData_ShouldCreateCorrectly()
    {
        // Arrange
        var variables = new Dictionary<string, object> { ["var1"] = "value1" };
        var executionId = "exec-1";
        var cancellationToken = CancellationToken.None;

        // Act
        var context = new WorkflowContext(variables, executionId, cancellationToken);

        // Assert
        Assert.Equal(variables, context.Variables);
        Assert.Equal(executionId, context.ExecutionId);
        Assert.Equal(cancellationToken, context.CancellationToken);
    }

    [Fact]
    public void VariableDefinition_WithValidData_ShouldCreateCorrectly()
    {
        // Arrange
        var name = "testVar";
        var type = "string";
        var defaultValue = "default";
        var isRequired = true;
        var description = "Test variable";

        // Act
        var variable = new VariableDefinition(name, type, defaultValue, isRequired, description);

        // Assert
        Assert.Equal(name, variable.Name);
        Assert.Equal(type, variable.Type);
        Assert.Equal(defaultValue, variable.DefaultValue);
        Assert.Equal(isRequired, variable.IsRequired);
        Assert.Equal(description, variable.Description);
    }

    [Fact]
    public void WorkflowMetadata_WithValidData_ShouldCreateCorrectly()
    {
        // Arrange
        var author = "Test Author";
        var description = "Test Description";
        var version = "1.0.0";
        var createdAt = DateTime.UtcNow;
        var tags = new List<string> { "tag1", "tag2" };

        // Act
        var metadata = new WorkflowMetadata(author, description, version, createdAt, tags);

        // Assert
        Assert.Equal(author, metadata.Author);
        Assert.Equal(description, metadata.Description);
        Assert.Equal(version, metadata.Version);
        Assert.Equal(createdAt, metadata.CreatedAt);
        Assert.Equal(tags, metadata.Tags);
    }

    [Theory]
    [InlineData(WorkflowStepType.Task)]
    [InlineData(WorkflowStepType.Condition)]
    [InlineData(WorkflowStepType.Loop)]
    [InlineData(WorkflowStepType.Parallel)]
    [InlineData(WorkflowStepType.Start)]
    [InlineData(WorkflowStepType.End)]
    public void WorkflowStepType_AllEnumValues_ShouldBeValid(WorkflowStepType stepType)
    {
        // Act & Assert
        Assert.True(Enum.IsDefined(typeof(WorkflowStepType), stepType));
    }

    [Theory]
    [InlineData(WorkflowStatus.Pending)]
    [InlineData(WorkflowStatus.Running)]
    [InlineData(WorkflowStatus.Paused)]
    [InlineData(WorkflowStatus.Completed)]
    [InlineData(WorkflowStatus.Failed)]
    public void WorkflowStatus_AllEnumValues_ShouldBeValid(WorkflowStatus status)
    {
        // Act & Assert
        Assert.True(Enum.IsDefined(typeof(WorkflowStatus), status));
    }

    [Fact]
    public void RecordEquality_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var parameters = new Dictionary<string, object>();
        var dependsOn = new List<string>();

        var step1 = new WorkflowStep("step-1", WorkflowStepType.Task, "test-command", parameters, dependsOn);
        var step2 = new WorkflowStep("step-1", WorkflowStepType.Task, "test-command", parameters, dependsOn);

        // Act & Assert
        Assert.Equal(step1, step2);
        Assert.True(step1 == step2);
        Assert.False(step1 != step2);
    }

    [Fact]
    public void RecordEquality_WithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var step1 = CreateValidWorkflowStep();
        var step2 = step1 with { Id = "different-id" };

        // Act & Assert
        Assert.NotEqual(step1, step2);
        Assert.False(step1 == step2);
        Assert.True(step1 != step2);
    }

    private static WorkflowStep CreateValidWorkflowStep()
    {
        return new WorkflowStep(
            "step-1",
            WorkflowStepType.Task,
            "test-command",
            new Dictionary<string, object>(),
            new List<string>()
        );
    }

    private static WorkflowMetadata CreateValidMetadata()
    {
        return new WorkflowMetadata(
            "Test Author",
            "Test Description",
            "1.0.0",
            DateTime.UtcNow,
            new List<string> { "test" }
        );
    }
}