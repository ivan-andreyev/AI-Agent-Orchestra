using System.Text.Json;
using Orchestra.Core.Models.Workflow;

namespace Orchestra.Tests.UnitTests;

/// <summary>
/// Тесты для базовых моделей markdown workflow системы
/// </summary>
public class MarkdownWorkflowModelsTests
{
    #region MarkdownWorkflowMetadata Tests

    [Fact]
    public void MarkdownWorkflowMetadata_ShouldCreateWithRequiredProperties()
    {
        // Arrange
        var author = "Test Author";
        var version = "1.0.0";
        var tags = new List<string> { "test", "workflow" };

        // Act
        var metadata = new MarkdownWorkflowMetadata(author, version, tags);

        // Assert
        Assert.Equal(author, metadata.Author);
        Assert.Equal(version, metadata.Version);
        Assert.Equal(tags, metadata.Tags);
        Assert.Null(metadata.Description);
        Assert.Null(metadata.CreatedAt);
    }

    [Fact]
    public void MarkdownWorkflowMetadata_ShouldCreateWithAllProperties()
    {
        // Arrange
        var author = "Test Author";
        var version = "1.0.0";
        var tags = new List<string> { "test", "workflow" };
        var description = "Test description";
        var createdAt = DateTime.UtcNow;

        // Act
        var metadata = new MarkdownWorkflowMetadata(author, version, tags, description, createdAt);

        // Assert
        Assert.Equal(author, metadata.Author);
        Assert.Equal(version, metadata.Version);
        Assert.Equal(tags, metadata.Tags);
        Assert.Equal(description, metadata.Description);
        Assert.Equal(createdAt, metadata.CreatedAt);
    }

    [Fact]
    public void MarkdownWorkflowMetadata_ShouldSerializeToJson()
    {
        // Arrange
        var metadata = new MarkdownWorkflowMetadata(
            "Test Author",
            "1.0.0",
            new List<string> { "test" },
            "Description"
        );

        // Act
        var json = JsonSerializer.Serialize(metadata);
        var deserialized = JsonSerializer.Deserialize<MarkdownWorkflowMetadata>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(metadata.Author, deserialized.Author);
        Assert.Equal(metadata.Version, deserialized.Version);
        Assert.Equal(metadata.Tags, deserialized.Tags);
        Assert.Equal(metadata.Description, deserialized.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkdownWorkflowMetadata_ShouldHandleEmptyAuthor(string author)
    {
        // Arrange & Act
        var metadata = new MarkdownWorkflowMetadata(author, "1.0", new List<string>());

        // Assert
        Assert.Equal(author, metadata.Author);
    }

    [Fact]
    public void MarkdownWorkflowMetadata_ShouldHandleEmptyTags()
    {
        // Arrange
        var tags = new List<string>();

        // Act
        var metadata = new MarkdownWorkflowMetadata("Author", "1.0", tags);

        // Assert
        Assert.Empty(metadata.Tags);
    }

    #endregion

    #region MarkdownWorkflowVariable Tests

    [Fact]
    public void MarkdownWorkflowVariable_ShouldCreateWithRequiredProperties()
    {
        // Arrange
        var name = "testVariable";
        var type = "string";
        var isRequired = true;

        // Act
        var variable = new MarkdownWorkflowVariable(name, type, isRequired);

        // Assert
        Assert.Equal(name, variable.Name);
        Assert.Equal(type, variable.Type);
        Assert.Equal(isRequired, variable.IsRequired);
        Assert.Null(variable.DefaultValue);
        Assert.Null(variable.Description);
    }

    [Fact]
    public void MarkdownWorkflowVariable_ShouldCreateWithAllProperties()
    {
        // Arrange
        var name = "testVariable";
        var type = "string";
        var isRequired = false;
        var defaultValue = "default";
        var description = "Test variable description";

        // Act
        var variable = new MarkdownWorkflowVariable(name, type, isRequired, defaultValue, description);

        // Assert
        Assert.Equal(name, variable.Name);
        Assert.Equal(type, variable.Type);
        Assert.Equal(isRequired, variable.IsRequired);
        Assert.Equal(defaultValue, variable.DefaultValue);
        Assert.Equal(description, variable.Description);
    }

    [Theory]
    [InlineData("string", true, "default", "Description")]
    [InlineData("int", false, "42", "Number variable")]
    [InlineData("bool", true, "true", "Boolean flag")]
    public void MarkdownWorkflowVariable_ShouldSupportDifferentTypes(string type, bool isRequired, string defaultValue, string description)
    {
        // Arrange & Act
        var variable = new MarkdownWorkflowVariable("test", type, isRequired, defaultValue, description);

        // Assert
        Assert.Equal(type, variable.Type);
        Assert.Equal(isRequired, variable.IsRequired);
        Assert.Equal(defaultValue, variable.DefaultValue);
        Assert.Equal(description, variable.Description);
    }

    [Fact]
    public void MarkdownWorkflowVariable_ShouldSerializeToJson()
    {
        // Arrange
        var variable = new MarkdownWorkflowVariable("testVar", "string", true, "default", "description");

        // Act
        var json = JsonSerializer.Serialize(variable);
        var deserialized = JsonSerializer.Deserialize<MarkdownWorkflowVariable>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(variable.Name, deserialized.Name);
        Assert.Equal(variable.Type, deserialized.Type);
        Assert.Equal(variable.IsRequired, deserialized.IsRequired);
        Assert.Equal(variable.DefaultValue, deserialized.DefaultValue);
        Assert.Equal(variable.Description, deserialized.Description);
    }

    #endregion

    #region MarkdownWorkflowStep Tests

    [Fact]
    public void MarkdownWorkflowStep_ShouldCreateWithRequiredProperties()
    {
        // Arrange
        var id = "step1";
        var title = "Test Step";
        var type = "Task";
        var command = "dotnet build";
        var parameters = new Dictionary<string, string> { { "config", "Release" } };
        var dependsOn = new List<string> { "step0" };

        // Act
        var step = new MarkdownWorkflowStep(id, title, type, command, parameters, dependsOn);

        // Assert
        Assert.Equal(id, step.Id);
        Assert.Equal(title, step.Title);
        Assert.Equal(type, step.Type);
        Assert.Equal(command, step.Command);
        Assert.Equal(parameters, step.Parameters);
        Assert.Equal(dependsOn, step.DependsOn);
        Assert.Null(step.Description);
    }

    [Fact]
    public void MarkdownWorkflowStep_ShouldCreateWithAllProperties()
    {
        // Arrange
        var id = "step1";
        var title = "Test Step";
        var type = "Task";
        var command = "dotnet build";
        var parameters = new Dictionary<string, string> { { "config", "Release" } };
        var dependsOn = new List<string> { "step0" };
        var description = "Test step description";

        // Act
        var step = new MarkdownWorkflowStep(id, title, type, command, parameters, dependsOn, description);

        // Assert
        Assert.Equal(id, step.Id);
        Assert.Equal(title, step.Title);
        Assert.Equal(type, step.Type);
        Assert.Equal(command, step.Command);
        Assert.Equal(parameters, step.Parameters);
        Assert.Equal(dependsOn, step.DependsOn);
        Assert.Equal(description, step.Description);
    }

    [Fact]
    public void MarkdownWorkflowStep_ShouldHandleEmptyParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, string>();
        var dependsOn = new List<string>();

        // Act
        var step = new MarkdownWorkflowStep("step1", "Title", "Task", "command", parameters, dependsOn);

        // Assert
        Assert.Empty(step.Parameters);
        Assert.Empty(step.DependsOn);
    }

    [Fact]
    public void MarkdownWorkflowStep_ShouldHandleMultipleDependencies()
    {
        // Arrange
        var dependsOn = new List<string> { "step1", "step2", "step3" };

        // Act
        var step = new MarkdownWorkflowStep("step4", "Title", "Task", "command", new Dictionary<string, string>(), dependsOn);

        // Assert
        Assert.Equal(3, step.DependsOn.Count);
        Assert.Contains("step1", step.DependsOn);
        Assert.Contains("step2", step.DependsOn);
        Assert.Contains("step3", step.DependsOn);
    }

    [Fact]
    public void MarkdownWorkflowStep_ShouldSerializeToJson()
    {
        // Arrange
        var step = new MarkdownWorkflowStep(
            "step1",
            "Test Step",
            "Task",
            "dotnet build",
            new Dictionary<string, string> { { "config", "Release" } },
            new List<string> { "step0" },
            "Description"
        );

        // Act
        var json = JsonSerializer.Serialize(step);
        var deserialized = JsonSerializer.Deserialize<MarkdownWorkflowStep>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(step.Id, deserialized.Id);
        Assert.Equal(step.Title, deserialized.Title);
        Assert.Equal(step.Type, deserialized.Type);
        Assert.Equal(step.Command, deserialized.Command);
        Assert.Equal(step.Parameters.Count, deserialized.Parameters.Count);
        Assert.Equal(step.DependsOn.Count, deserialized.DependsOn.Count);
        Assert.Equal(step.Description, deserialized.Description);
    }

    #endregion

    #region MarkdownWorkflow Tests

    [Fact]
    public void MarkdownWorkflow_ShouldCreateWithAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var name = "Test Workflow";
        var sourceFilePath = @"C:\test\workflow.md";
        var metadata = new MarkdownWorkflowMetadata("Author", "1.0", new List<string> { "test" });
        var variables = new Dictionary<string, MarkdownWorkflowVariable>
        {
            { "var1", new MarkdownWorkflowVariable("var1", "string", true) }
        };
        var steps = new List<MarkdownWorkflowStep>
        {
            new("step1", "Test Step", "Task", "command", new Dictionary<string, string>(), new List<string>())
        };
        var parsedAt = DateTime.UtcNow;
        var fileHash = "hash123";

        // Act
        var workflow = new MarkdownWorkflow(id, name, sourceFilePath, metadata, variables, steps, parsedAt, fileHash);

        // Assert
        Assert.Equal(id, workflow.Id);
        Assert.Equal(name, workflow.Name);
        Assert.Equal(sourceFilePath, workflow.SourceFilePath);
        Assert.Equal(metadata, workflow.Metadata);
        Assert.Equal(variables, workflow.Variables);
        Assert.Equal(steps, workflow.Steps);
        Assert.Equal(parsedAt, workflow.ParsedAt);
        Assert.Equal(fileHash, workflow.FileHash);
    }

    [Fact]
    public void MarkdownWorkflow_ShouldHandleEmptyCollections()
    {
        // Arrange
        var variables = new Dictionary<string, MarkdownWorkflowVariable>();
        var steps = new List<MarkdownWorkflowStep>();
        var metadata = new MarkdownWorkflowMetadata("Author", "1.0", new List<string>());

        // Act
        var workflow = new MarkdownWorkflow("id", "name", "path", metadata, variables, steps, DateTime.UtcNow, "hash");

        // Assert
        Assert.Empty(workflow.Variables);
        Assert.Empty(workflow.Steps);
    }

    [Fact]
    public void MarkdownWorkflow_ShouldSerializeToJson()
    {
        // Arrange
        var metadata = new MarkdownWorkflowMetadata("Author", "1.0", new List<string> { "test" });
        var variables = new Dictionary<string, MarkdownWorkflowVariable>
        {
            { "var1", new MarkdownWorkflowVariable("var1", "string", true) }
        };
        var steps = new List<MarkdownWorkflowStep>
        {
            new("step1", "Test Step", "Task", "command", new Dictionary<string, string>(), new List<string>())
        };
        var workflow = new MarkdownWorkflow("id", "name", "path", metadata, variables, steps, DateTime.UtcNow, "hash");

        // Act
        var json = JsonSerializer.Serialize(workflow);
        var deserialized = JsonSerializer.Deserialize<MarkdownWorkflow>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(workflow.Id, deserialized.Id);
        Assert.Equal(workflow.Name, deserialized.Name);
        Assert.Equal(workflow.SourceFilePath, deserialized.SourceFilePath);
        Assert.Equal(workflow.Variables.Count, deserialized.Variables.Count);
        Assert.Equal(workflow.Steps.Count, deserialized.Steps.Count);
        Assert.Equal(workflow.FileHash, deserialized.FileHash);
    }

    #endregion

    #region MarkdownWorkflowParseResult Tests

    [Fact]
    public void MarkdownWorkflowParseResult_ShouldCreateSuccessResult()
    {
        // Arrange
        var metadata = new MarkdownWorkflowMetadata("Author", "1.0", new List<string>());
        var workflow = new MarkdownWorkflow("id", "name", "path", metadata, new Dictionary<string, MarkdownWorkflowVariable>(), new List<MarkdownWorkflowStep>(), DateTime.UtcNow, "hash");

        // Act
        var result = new MarkdownWorkflowParseResult(true, workflow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Workflow);
        Assert.Equal(workflow, result.Workflow);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Warnings);
    }

    [Fact]
    public void MarkdownWorkflowParseResult_ShouldCreateErrorResult()
    {
        // Arrange
        var errorMessage = "Parse error occurred";

        // Act
        var result = new MarkdownWorkflowParseResult(false, null, errorMessage);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Workflow);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.Null(result.Warnings);
    }

    [Fact]
    public void MarkdownWorkflowParseResult_ShouldCreateWithWarnings()
    {
        // Arrange
        var metadata = new MarkdownWorkflowMetadata("Author", "1.0", new List<string>());
        var workflow = new MarkdownWorkflow("id", "name", "path", metadata, new Dictionary<string, MarkdownWorkflowVariable>(), new List<MarkdownWorkflowStep>(), DateTime.UtcNow, "hash");
        var warnings = new List<string> { "Warning 1", "Warning 2" };

        // Act
        var result = new MarkdownWorkflowParseResult(true, workflow, null, warnings);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Workflow);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(2, result.Warnings?.Count);
        Assert.Contains("Warning 1", result.Warnings);
        Assert.Contains("Warning 2", result.Warnings);
    }

    [Fact]
    public void MarkdownWorkflowParseResult_ShouldSerializeToJson()
    {
        // Arrange
        var result = new MarkdownWorkflowParseResult(false, null, "Error", new List<string> { "Warning" });

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<MarkdownWorkflowParseResult>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(result.IsSuccess, deserialized.IsSuccess);
        Assert.Equal(result.ErrorMessage, deserialized.ErrorMessage);
        Assert.Equal(result.Warnings?.Count, deserialized.Warnings?.Count);
    }

    #endregion

    #region MarkdownToWorkflowConversionResult Tests

    [Fact]
    public void MarkdownToWorkflowConversionResult_ShouldCreateSuccessResult()
    {
        // Arrange
        var workflowDefinition = new WorkflowDefinition(
            "id",
            "name",
            new List<WorkflowStep>(),
            new Dictionary<string, VariableDefinition>(),
            new WorkflowMetadata("Author", "Description", "1.0", DateTime.UtcNow, new List<string>())
        );
        var metadata = new MarkdownWorkflowMetadata("Author", "1.0", new List<string>());
        var markdownWorkflow = new MarkdownWorkflow("id", "name", "path", metadata, new Dictionary<string, MarkdownWorkflowVariable>(), new List<MarkdownWorkflowStep>(), DateTime.UtcNow, "hash");
        var convertedAt = DateTime.UtcNow;

        // Act
        var result = new MarkdownToWorkflowConversionResult(true, workflowDefinition, markdownWorkflow, null, convertedAt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.WorkflowDefinition);
        Assert.NotNull(result.SourceMarkdownWorkflow);
        Assert.Equal(workflowDefinition, result.WorkflowDefinition);
        Assert.Equal(markdownWorkflow, result.SourceMarkdownWorkflow);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(convertedAt, result.ConvertedAt);
    }

    [Fact]
    public void MarkdownToWorkflowConversionResult_ShouldCreateErrorResult()
    {
        // Arrange
        var errorMessage = "Conversion failed";

        // Act
        var result = new MarkdownToWorkflowConversionResult(false, null, null, errorMessage);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.WorkflowDefinition);
        Assert.Null(result.SourceMarkdownWorkflow);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.Null(result.ConvertedAt);
    }

    [Fact]
    public void MarkdownToWorkflowConversionResult_ShouldSerializeToJson()
    {
        // Arrange
        var result = new MarkdownToWorkflowConversionResult(false, null, null, "Error");

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<MarkdownToWorkflowConversionResult>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(result.IsSuccess, deserialized.IsSuccess);
        Assert.Equal(result.ErrorMessage, deserialized.ErrorMessage);
    }

    #endregion

    #region Record Immutability Tests

    [Fact]
    public void MarkdownWorkflowModels_ShouldBeImmutableRecords()
    {
        // Arrange
        var metadata = new MarkdownWorkflowMetadata("Author", "1.0", new List<string>());
        var variable = new MarkdownWorkflowVariable("var", "string", true);
        var step = new MarkdownWorkflowStep("step", "title", "Task", "command", new Dictionary<string, string>(), new List<string>());

        // Act & Assert
        // Records are immutable by default, this test verifies the concept
        Assert.Equal("Author", metadata.Author);
        Assert.Equal("var", variable.Name);
        Assert.Equal("step", step.Id);

        // These would not compile (records are immutable):
        // metadata.Author = "New Author";
        // variable.Name = "newVar";
        // step.Id = "newStep";
    }

    [Fact]
    public void MarkdownWorkflowModels_ShouldSupportWithExpressions()
    {
        // Arrange
        var originalMetadata = new MarkdownWorkflowMetadata("Author", "1.0", new List<string>());

        // Act
        var modifiedMetadata = originalMetadata with { Author = "New Author" };

        // Assert
        Assert.Equal("Author", originalMetadata.Author);
        Assert.Equal("New Author", modifiedMetadata.Author);
        Assert.Equal(originalMetadata.Version, modifiedMetadata.Version);
        Assert.Equal(originalMetadata.Tags, modifiedMetadata.Tags);
    }

    #endregion

    #region Collection Handling Tests

    [Fact]
    public void MarkdownWorkflowModels_ShouldHandleNullCollections()
    {
        // Arrange & Act
        var metadataWithNullTags = new MarkdownWorkflowMetadata("Author", "1.0", null!);
        var stepWithNullCollections = new MarkdownWorkflowStep("step", "title", "Task", "command", null!, null!);

        // Assert
        Assert.Null(metadataWithNullTags.Tags);
        Assert.Null(stepWithNullCollections.Parameters);
        Assert.Null(stepWithNullCollections.DependsOn);
    }

    [Fact]
    public void MarkdownWorkflow_ShouldPreserveCollectionReferences()
    {
        // Arrange
        var variables = new Dictionary<string, MarkdownWorkflowVariable>
        {
            { "var1", new MarkdownWorkflowVariable("var1", "string", true) }
        };
        var steps = new List<MarkdownWorkflowStep>
        {
            new("step1", "title", "Task", "command", new Dictionary<string, string>(), new List<string>())
        };
        var metadata = new MarkdownWorkflowMetadata("Author", "1.0", new List<string>());

        // Act
        var workflow = new MarkdownWorkflow("id", "name", "path", metadata, variables, steps, DateTime.UtcNow, "hash");

        // Assert
        Assert.Same(variables, workflow.Variables);
        Assert.Same(steps, workflow.Steps);
        Assert.Single(workflow.Variables);
        Assert.Single(workflow.Steps);
    }

    #endregion
}