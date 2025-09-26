using System.Text.Json;
using Orchestra.Core.Models.Workflow.Markdown;
using Xunit;

namespace Orchestra.Tests.UnitTests;

/// <summary>
/// Тесты для моделей markdown workflow, соответствующих спецификации
/// </summary>
public class MarkdownWorkflowSpecCompliantModelsTests
{
    #region MarkdownWorkflowEnums Tests

    [Fact]
    public void MarkdownWorkflowStatus_Should_Have_Correct_Values()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)MarkdownWorkflowStatus.Draft);
        Assert.Equal(1, (int)MarkdownWorkflowStatus.Ready);
        Assert.Equal(2, (int)MarkdownWorkflowStatus.Running);
        Assert.Equal(3, (int)MarkdownWorkflowStatus.Completed);
        Assert.Equal(4, (int)MarkdownWorkflowStatus.Failed);
        Assert.Equal(5, (int)MarkdownWorkflowStatus.Paused);
        Assert.Equal(6, (int)MarkdownWorkflowStatus.Cancelled);
    }

    [Fact]
    public void MarkdownSectionType_Should_Have_Correct_Values()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)MarkdownSectionType.Metadata);
        Assert.Equal(1, (int)MarkdownSectionType.Variables);
        Assert.Equal(2, (int)MarkdownSectionType.Steps);
        Assert.Equal(3, (int)MarkdownSectionType.Description);
        Assert.Equal(4, (int)MarkdownSectionType.Notes);
        Assert.Equal(99, (int)MarkdownSectionType.Unknown);
    }

    [Fact]
    public void MarkdownStepType_Should_Have_Correct_Values()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)MarkdownStepType.Task);
        Assert.Equal(1, (int)MarkdownStepType.Condition);
        Assert.Equal(2, (int)MarkdownStepType.Loop);
        Assert.Equal(3, (int)MarkdownStepType.Parallel);
        Assert.Equal(4, (int)MarkdownStepType.Delay);
        Assert.Equal(5, (int)MarkdownStepType.SubWorkflow);
    }

    [Fact]
    public void MarkdownVariableType_Should_Have_Correct_Values()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)MarkdownVariableType.String);
        Assert.Equal(1, (int)MarkdownVariableType.Number);
        Assert.Equal(2, (int)MarkdownVariableType.Boolean);
        Assert.Equal(3, (int)MarkdownVariableType.DateTime);
        Assert.Equal(4, (int)MarkdownVariableType.FilePath);
        Assert.Equal(5, (int)MarkdownVariableType.Url);
        Assert.Equal(6, (int)MarkdownVariableType.Json);
        Assert.Equal(7, (int)MarkdownVariableType.StringArray);
    }

    [Fact]
    public void WorkflowPriority_Should_Have_Correct_Values()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)WorkflowPriority.Low);
        Assert.Equal(1, (int)WorkflowPriority.Normal);
        Assert.Equal(2, (int)WorkflowPriority.High);
        Assert.Equal(3, (int)WorkflowPriority.Critical);
    }

    #endregion

    #region MarkdownWorkflowDocument Tests

    [Fact]
    public void MarkdownWorkflowDocument_Constructor_Should_Initialize_With_Defaults()
    {
        // Act
        var document = new MarkdownWorkflowDocument();

        // Assert
        Assert.NotEqual(Guid.Empty, document.Id);
        Assert.Equal(string.Empty, document.FilePath);
        Assert.Equal(string.Empty, document.RawContent);
        Assert.NotNull(document.Metadata);
        Assert.NotNull(document.Sections);
        Assert.Empty(document.Sections);
        Assert.NotNull(document.Variables);
        Assert.Empty(document.Variables);
        Assert.NotNull(document.Steps);
        Assert.Empty(document.Steps);
        Assert.Equal(string.Empty, document.ContentHash);
        Assert.True(document.ParsedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void MarkdownWorkflowDocument_Should_Allow_Property_Setting()
    {
        // Arrange
        var document = new MarkdownWorkflowDocument();
        var metadata = new MarkdownWorkflowMetadata { Title = "Test" };
        var section = new MarkdownWorkflowSection { Title = "Section1" };
        var variable = new MarkdownWorkflowVariable { Name = "var1" };
        var step = new MarkdownWorkflowStep { Name = "step1" };

        // Act
        document.FilePath = "/test/path.md";
        document.RawContent = "# Test Content";
        document.Metadata = metadata;
        document.Sections.Add(section);
        document.Variables.Add(variable);
        document.Steps.Add(step);
        document.ContentHash = "hash123";

        // Assert
        Assert.Equal("/test/path.md", document.FilePath);
        Assert.Equal("# Test Content", document.RawContent);
        Assert.Equal(metadata, document.Metadata);
        Assert.Single(document.Sections);
        Assert.Single(document.Variables);
        Assert.Single(document.Steps);
        Assert.Equal("hash123", document.ContentHash);
    }

    #endregion

    #region MarkdownWorkflowMetadata Tests

    [Fact]
    public void MarkdownWorkflowMetadata_Constructor_Should_Initialize_With_Defaults()
    {
        // Act
        var metadata = new MarkdownWorkflowMetadata();

        // Assert
        Assert.Equal(string.Empty, metadata.Title);
        Assert.Equal(string.Empty, metadata.Description);
        Assert.Equal(string.Empty, metadata.Author);
        Assert.Equal("1.0", metadata.Version);
        Assert.NotNull(metadata.Tags);
        Assert.Empty(metadata.Tags);
        Assert.True(metadata.CreatedAt <= DateTime.UtcNow);
        Assert.True(metadata.UpdatedAt <= DateTime.UtcNow);
        Assert.Equal(WorkflowPriority.Normal, metadata.Priority);
        Assert.Equal(MarkdownWorkflowStatus.Draft, metadata.Status);
    }

    [Fact]
    public void MarkdownWorkflowMetadata_Should_Allow_Property_Setting()
    {
        // Arrange
        var metadata = new MarkdownWorkflowMetadata();
        var now = DateTime.UtcNow;

        // Act
        metadata.Title = "Test Workflow";
        metadata.Description = "Test Description";
        metadata.Author = "Test Author";
        metadata.Version = "2.0";
        metadata.Tags.Add("tag1");
        metadata.CreatedAt = now;
        metadata.UpdatedAt = now;
        metadata.Priority = WorkflowPriority.High;
        metadata.Status = MarkdownWorkflowStatus.Ready;

        // Assert
        Assert.Equal("Test Workflow", metadata.Title);
        Assert.Equal("Test Description", metadata.Description);
        Assert.Equal("Test Author", metadata.Author);
        Assert.Equal("2.0", metadata.Version);
        Assert.Single(metadata.Tags);
        Assert.Equal("tag1", metadata.Tags[0]);
        Assert.Equal(now, metadata.CreatedAt);
        Assert.Equal(now, metadata.UpdatedAt);
        Assert.Equal(WorkflowPriority.High, metadata.Priority);
        Assert.Equal(MarkdownWorkflowStatus.Ready, metadata.Status);
    }

    #endregion

    #region MarkdownWorkflowSection Tests

    [Fact]
    public void MarkdownWorkflowSection_Constructor_Should_Initialize_With_Defaults()
    {
        // Act
        var section = new MarkdownWorkflowSection();

        // Assert
        Assert.Equal(MarkdownSectionType.Metadata, section.Type);
        Assert.Equal(string.Empty, section.Title);
        Assert.Equal(string.Empty, section.Content);
        Assert.Equal(0, section.Order);
        Assert.Equal(1, section.HeaderLevel);
        Assert.NotNull(section.Attributes);
        Assert.Empty(section.Attributes);
    }

    [Fact]
    public void MarkdownWorkflowSection_Should_Allow_Property_Setting()
    {
        // Arrange
        var section = new MarkdownWorkflowSection();

        // Act
        section.Type = MarkdownSectionType.Steps;
        section.Title = "Test Section";
        section.Content = "Section content";
        section.Order = 5;
        section.HeaderLevel = 2;
        section.Attributes["key1"] = "value1";

        // Assert
        Assert.Equal(MarkdownSectionType.Steps, section.Type);
        Assert.Equal("Test Section", section.Title);
        Assert.Equal("Section content", section.Content);
        Assert.Equal(5, section.Order);
        Assert.Equal(2, section.HeaderLevel);
        Assert.Single(section.Attributes);
        Assert.Equal("value1", section.Attributes["key1"]);
    }

    #endregion

    #region MarkdownWorkflowStep Tests

    [Fact]
    public void MarkdownWorkflowStep_Constructor_Should_Initialize_With_Defaults()
    {
        // Act
        var step = new MarkdownWorkflowStep();

        // Assert
        Assert.Equal(string.Empty, step.Id);
        Assert.Equal(string.Empty, step.Name);
        Assert.Equal(MarkdownStepType.Task, step.Type);
        Assert.Equal(string.Empty, step.Command);
        Assert.NotNull(step.Parameters);
        Assert.Empty(step.Parameters);
        Assert.NotNull(step.DependsOn);
        Assert.Empty(step.DependsOn);
        Assert.Equal(string.Empty, step.Condition);
        Assert.Equal(300, step.TimeoutSeconds);
        Assert.True(step.Retryable);
        Assert.Equal(0, step.Order);
    }

    [Fact]
    public void MarkdownWorkflowStep_Should_Allow_Property_Setting()
    {
        // Arrange
        var step = new MarkdownWorkflowStep();

        // Act
        step.Id = "step1";
        step.Name = "Test Step";
        step.Type = MarkdownStepType.Condition;
        step.Command = "dotnet build";
        step.Parameters["arg1"] = "value1";
        step.DependsOn.Add("step0");
        step.Condition = "if success";
        step.TimeoutSeconds = 600;
        step.Retryable = false;
        step.Order = 1;

        // Assert
        Assert.Equal("step1", step.Id);
        Assert.Equal("Test Step", step.Name);
        Assert.Equal(MarkdownStepType.Condition, step.Type);
        Assert.Equal("dotnet build", step.Command);
        Assert.Single(step.Parameters);
        Assert.Equal("value1", step.Parameters["arg1"]);
        Assert.Single(step.DependsOn);
        Assert.Equal("step0", step.DependsOn[0]);
        Assert.Equal("if success", step.Condition);
        Assert.Equal(600, step.TimeoutSeconds);
        Assert.False(step.Retryable);
        Assert.Equal(1, step.Order);
    }

    #endregion

    #region MarkdownWorkflowVariable Tests

    [Fact]
    public void MarkdownWorkflowVariable_Constructor_Should_Initialize_With_Defaults()
    {
        // Act
        var variable = new MarkdownWorkflowVariable();

        // Assert
        Assert.Equal(string.Empty, variable.Name);
        Assert.Equal(MarkdownVariableType.String, variable.Type);
        Assert.Null(variable.DefaultValue);
        Assert.False(variable.Required);
        Assert.Equal(string.Empty, variable.Description);
        Assert.Equal(string.Empty, variable.Validation);
        Assert.NotNull(variable.AllowedValues);
        Assert.Empty(variable.AllowedValues);
    }

    [Fact]
    public void MarkdownWorkflowVariable_Should_Allow_Property_Setting()
    {
        // Arrange
        var variable = new MarkdownWorkflowVariable();

        // Act
        variable.Name = "testVar";
        variable.Type = MarkdownVariableType.Number;
        variable.DefaultValue = 42;
        variable.Required = true;
        variable.Description = "Test variable";
        variable.Validation = "min:0,max:100";
        variable.AllowedValues.Add("value1");

        // Assert
        Assert.Equal("testVar", variable.Name);
        Assert.Equal(MarkdownVariableType.Number, variable.Type);
        Assert.Equal(42, variable.DefaultValue);
        Assert.True(variable.Required);
        Assert.Equal("Test variable", variable.Description);
        Assert.Equal("min:0,max:100", variable.Validation);
        Assert.Single(variable.AllowedValues);
        Assert.Equal("value1", variable.AllowedValues[0]);
    }

    #endregion

    #region MarkdownWorkflowConstants Tests

    [Fact]
    public void MarkdownWorkflowConstants_Should_Have_Correct_Values()
    {
        // Assert
        Assert.Equal(".md", MarkdownWorkflowConstants.FileExtension);
        Assert.Equal(10 * 1024 * 1024, MarkdownWorkflowConstants.MaxFileSize);
        Assert.Equal(5, MarkdownWorkflowConstants.MaxNestingDepth);
        Assert.Equal(30, MarkdownWorkflowConstants.DefaultParsingTimeout);
        Assert.Equal(@"\{\{(\w+)\}\}", MarkdownWorkflowConstants.VariablePattern);
        Assert.Equal(@"\[([^\]]+)\]\(([^)]+\.md)\)", MarkdownWorkflowConstants.WorkflowLinkPattern);
        Assert.Single(MarkdownWorkflowConstants.RequiredSections);
        Assert.Equal("Steps", MarkdownWorkflowConstants.RequiredSections[0]);
        Assert.Equal(5, MarkdownWorkflowConstants.SupportedCommandTypes.Length);
        Assert.Contains("dotnet", MarkdownWorkflowConstants.SupportedCommandTypes);
        Assert.Contains("git", MarkdownWorkflowConstants.SupportedCommandTypes);
        Assert.Contains("powershell", MarkdownWorkflowConstants.SupportedCommandTypes);
        Assert.Contains("bash", MarkdownWorkflowConstants.SupportedCommandTypes);
        Assert.Contains("custom", MarkdownWorkflowConstants.SupportedCommandTypes);
    }

    #endregion

    #region JSON Serialization Tests

    [Fact]
    public void MarkdownWorkflowDocument_Should_Support_JSON_Serialization()
    {
        // Arrange
        var document = new MarkdownWorkflowDocument
        {
            FilePath = "/test/workflow.md",
            RawContent = "# Test Workflow",
            ContentHash = "abc123",
            Metadata = new MarkdownWorkflowMetadata
            {
                Title = "Test",
                Author = "Test Author"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(document);
        var deserialized = JsonSerializer.Deserialize<MarkdownWorkflowDocument>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(document.FilePath, deserialized.FilePath);
        Assert.Equal(document.RawContent, deserialized.RawContent);
        Assert.Equal(document.ContentHash, deserialized.ContentHash);
        Assert.Equal(document.Metadata.Title, deserialized.Metadata.Title);
        Assert.Equal(document.Metadata.Author, deserialized.Metadata.Author);
    }

    [Fact]
    public void MarkdownWorkflowMetadata_Should_Support_JSON_Serialization()
    {
        // Arrange
        var metadata = new MarkdownWorkflowMetadata
        {
            Title = "Test Workflow",
            Author = "Test Author",
            Version = "2.0",
            Priority = WorkflowPriority.High,
            Status = MarkdownWorkflowStatus.Ready
        };
        metadata.Tags.Add("test");

        // Act
        var json = JsonSerializer.Serialize(metadata);
        var deserialized = JsonSerializer.Deserialize<MarkdownWorkflowMetadata>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(metadata.Title, deserialized.Title);
        Assert.Equal(metadata.Author, deserialized.Author);
        Assert.Equal(metadata.Version, deserialized.Version);
        Assert.Equal(metadata.Priority, deserialized.Priority);
        Assert.Equal(metadata.Status, deserialized.Status);
        Assert.Single(deserialized.Tags);
        Assert.Equal("test", deserialized.Tags[0]);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void MarkdownWorkflow_Complete_Document_Should_Work_Together()
    {
        // Arrange
        var document = new MarkdownWorkflowDocument
        {
            FilePath = "/workflows/test.md",
            RawContent = "# Test Workflow\n## Steps\n- Step 1",
            ContentHash = "hash123"
        };

        document.Metadata.Title = "Integration Test";
        document.Metadata.Author = "Test Author";
        document.Metadata.Status = MarkdownWorkflowStatus.Ready;

        var section = new MarkdownWorkflowSection
        {
            Type = MarkdownSectionType.Steps,
            Title = "Steps",
            Content = "- Step 1\n- Step 2",
            Order = 1,
            HeaderLevel = 2
        };

        var variable = new MarkdownWorkflowVariable
        {
            Name = "projectName",
            Type = MarkdownVariableType.String,
            Required = true,
            Description = "Name of the project"
        };

        var step = new MarkdownWorkflowStep
        {
            Id = "step1",
            Name = "Build Project",
            Type = MarkdownStepType.Task,
            Command = "dotnet build",
            TimeoutSeconds = 300,
            Order = 1
        };

        step.Parameters["configuration"] = "Release";
        document.Sections.Add(section);
        document.Variables.Add(variable);
        document.Steps.Add(step);

        // Act & Assert - No exceptions should be thrown
        Assert.NotNull(document);
        Assert.Equal("Integration Test", document.Metadata.Title);
        Assert.Single(document.Sections);
        Assert.Single(document.Variables);
        Assert.Single(document.Steps);
        Assert.Equal(MarkdownSectionType.Steps, document.Sections[0].Type);
        Assert.Equal(MarkdownVariableType.String, document.Variables[0].Type);
        Assert.Equal(MarkdownStepType.Task, document.Steps[0].Type);
    }

    #endregion
}