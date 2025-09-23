using System.Text.Json;
using Orchestra.Core.Models.Workflow;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests.Workflow;

/// <summary>
/// Unit tests для WorkflowSerializer
/// </summary>
public class WorkflowSerializerTests
{
    private readonly WorkflowSerializer _serializer;
    private readonly WorkflowDefinition _testWorkflow;

    public WorkflowSerializerTests()
    {
        _serializer = new WorkflowSerializer();
        _testWorkflow = CreateTestWorkflow();
    }

    [Fact]
    public void SerializeWorkflow_ValidWorkflow_ReturnsValidJson()
    {
        // Act
        var json = _serializer.SerializeWorkflow(_testWorkflow);

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.True(_serializer.ValidateWorkflowJson(json));
    }

    [Fact]
    public void SerializeWorkflow_NullWorkflow_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _serializer.SerializeWorkflow(null!));
    }

    [Fact]
    public void DeserializeWorkflow_ValidJson_ReturnsWorkflowDefinition()
    {
        // Arrange
        var json = _serializer.SerializeWorkflow(_testWorkflow);

        // Act
        var deserialized = _serializer.DeserializeWorkflow(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(_testWorkflow.Id, deserialized.Id);
        Assert.Equal(_testWorkflow.Name, deserialized.Name);
        Assert.Equal(_testWorkflow.Steps.Count, deserialized.Steps.Count);
        Assert.Equal(_testWorkflow.Variables.Count, deserialized.Variables.Count);
        Assert.Equal(_testWorkflow.Metadata.Author, deserialized.Metadata.Author);
    }

    [Fact]
    public void DeserializeWorkflow_EmptyJson_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _serializer.DeserializeWorkflow(""));
        Assert.Throws<ArgumentException>(() => _serializer.DeserializeWorkflow("   "));
        Assert.Throws<ArgumentException>(() => _serializer.DeserializeWorkflow(null!));
    }

    [Fact]
    public void DeserializeWorkflow_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        Assert.Throws<JsonException>(() => _serializer.DeserializeWorkflow(invalidJson));
    }

    [Fact]
    public void ValidateWorkflowJson_ValidJson_ReturnsTrue()
    {
        // Arrange
        var json = _serializer.SerializeWorkflow(_testWorkflow);

        // Act
        var isValid = _serializer.ValidateWorkflowJson(json);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateWorkflowJson_InvalidJson_ReturnsFalse()
    {
        // Arrange
        var invalidJsons = new[]
        {
            "",
            "null",
            "{ \"id\": \"test\" }", // Missing required properties
            "{ \"name\": \"test\" }", // Missing required properties
            "invalid json",
            "[]" // Array instead of object
        };

        foreach (var invalidJson in invalidJsons)
        {
            // Act
            var isValid = _serializer.ValidateWorkflowJson(invalidJson);

            // Assert
            Assert.False(isValid, $"JSON should be invalid: {invalidJson}");
        }
    }

    [Fact]
    public void SerializationRoundTrip_ComplexWorkflow_PreservesAllData()
    {
        // Arrange
        var complexWorkflow = CreateComplexTestWorkflow();

        // Act
        var json = _serializer.SerializeWorkflow(complexWorkflow);
        var deserialized = _serializer.DeserializeWorkflow(json);

        // Assert
        Assert.Equal(complexWorkflow.Id, deserialized.Id);
        Assert.Equal(complexWorkflow.Name, deserialized.Name);
        Assert.Equal(complexWorkflow.Steps.Count, deserialized.Steps.Count);
        Assert.Equal(complexWorkflow.Variables.Count, deserialized.Variables.Count);

        // Check step with condition
        var conditionalStep = deserialized.Steps.FirstOrDefault(s => s.Condition != null);
        Assert.NotNull(conditionalStep);
        Assert.Equal("$input > 5", conditionalStep.Condition!.Expression);

        // Check step with retry policy
        var retryStep = deserialized.Steps.FirstOrDefault(s => s.RetryPolicy != null);
        Assert.NotNull(retryStep);
        Assert.Equal(3, retryStep.RetryPolicy!.MaxRetryCount);
        Assert.Equal(2.0, retryStep.RetryPolicy.BackoffMultiplier);
    }

    [Fact]
    public async Task LoadWorkflowFromFileAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _serializer.LoadWorkflowFromFileAsync("non-existent-file.json"));
    }

    [Fact]
    public async Task SaveAndLoadWorkflowFromFile_ValidWorkflow_RoundTripSuccessful()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            await _serializer.SaveWorkflowToFileAsync(_testWorkflow, tempFile);
            var loaded = await _serializer.LoadWorkflowFromFileAsync(tempFile);

            // Assert
            Assert.Equal(_testWorkflow.Id, loaded.Id);
            Assert.Equal(_testWorkflow.Name, loaded.Name);
            Assert.Equal(_testWorkflow.Steps.Count, loaded.Steps.Count);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TimeSpanConverter_ValidTimeSpan_SerializesCorrectly()
    {
        // Arrange
        var workflow = CreateWorkflowWithRetryPolicy();

        // Act
        var json = _serializer.SerializeWorkflow(workflow);
        var deserialized = _serializer.DeserializeWorkflow(json);

        // Assert
        var retryStep = deserialized.Steps.First(s => s.RetryPolicy != null);
        Assert.Equal(TimeSpan.FromMinutes(5), retryStep.RetryPolicy!.MaxDelay);
    }

    private static WorkflowDefinition CreateTestWorkflow()
    {
        var metadata = new WorkflowMetadata(
            Author: "Test Author",
            Description: "Test workflow for serialization",
            Version: "1.0.0",
            CreatedAt: new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Tags: new List<string> { "test", "serialization" }
        );

        var variables = new Dictionary<string, VariableDefinition>
        {
            ["input"] = new VariableDefinition("input", "String", "default", true, "Input parameter"),
            ["output"] = new VariableDefinition("output", "String", null, false, "Output result")
        };

        var steps = new List<WorkflowStep>
        {
            new WorkflowStep(
                Id: "step1",
                Type: WorkflowStepType.Task,
                Command: "echo",
                Parameters: new Dictionary<string, object> { ["message"] = "Hello World" },
                DependsOn: new List<string>()
            ),
            new WorkflowStep(
                Id: "step2",
                Type: WorkflowStepType.Task,
                Command: "process",
                Parameters: new Dictionary<string, object> { ["input"] = "$input" },
                DependsOn: new List<string> { "step1" }
            )
        };

        return new WorkflowDefinition(
            Id: "test-workflow",
            Name: "Test Workflow",
            Steps: steps,
            Variables: variables,
            Metadata: metadata
        );
    }

    private static WorkflowDefinition CreateComplexTestWorkflow()
    {
        var metadata = new WorkflowMetadata(
            Author: "Complex Author",
            Description: "Complex workflow with conditions and retries",
            Version: "2.0.0",
            CreatedAt: DateTime.UtcNow,
            Tags: new List<string> { "complex", "test" }
        );

        var variables = new Dictionary<string, VariableDefinition>
        {
            ["input"] = new VariableDefinition("input", "Int32", 0),
            ["result"] = new VariableDefinition("result", "String")
        };

        var conditionalLogic = new ConditionalLogic(
            Expression: "$input > 5",
            TruePath: new List<string> { "high-value-step" },
            FalsePath: new List<string> { "low-value-step" }
        );

        var retryPolicy = new RetryPolicy(
            MaxRetryCount: 3,
            BaseDelay: TimeSpan.FromSeconds(30),
            BackoffMultiplier: 2.0
        );

        var steps = new List<WorkflowStep>
        {
            new WorkflowStep(
                Id: "conditional-step",
                Type: WorkflowStepType.Condition,
                Command: "evaluate",
                Parameters: new Dictionary<string, object>(),
                DependsOn: new List<string>(),
                Condition: conditionalLogic
            ),
            new WorkflowStep(
                Id: "retry-step",
                Type: WorkflowStepType.Task,
                Command: "unreliable-task",
                Parameters: new Dictionary<string, object> { ["attempts"] = 3 },
                DependsOn: new List<string> { "conditional-step" },
                RetryPolicy: retryPolicy
            )
        };

        return new WorkflowDefinition(
            Id: "complex-workflow",
            Name: "Complex Test Workflow",
            Steps: steps,
            Variables: variables,
            Metadata: metadata
        );
    }

    private static WorkflowDefinition CreateWorkflowWithRetryPolicy()
    {
        var metadata = new WorkflowMetadata(
            Author: "TimeSpan Test",
            Description: "Workflow to test TimeSpan serialization",
            Version: "1.0.0",
            CreatedAt: DateTime.UtcNow,
            Tags: new List<string> { "timespan" }
        );

        var retryPolicy = new RetryPolicy(
            MaxRetryCount: 2,
            MaxDelay: TimeSpan.FromMinutes(5),
            BackoffMultiplier: 1.0
        );

        var steps = new List<WorkflowStep>
        {
            new WorkflowStep(
                Id: "timespan-step",
                Type: WorkflowStepType.Task,
                Command: "test-timespan",
                Parameters: new Dictionary<string, object>(),
                DependsOn: new List<string>(),
                RetryPolicy: retryPolicy
            )
        };

        return new WorkflowDefinition(
            Id: "timespan-workflow",
            Name: "TimeSpan Test Workflow",
            Steps: steps,
            Variables: new Dictionary<string, VariableDefinition>(),
            Metadata: metadata
        );
    }
}