using Orchestra.Core.Models.Workflow.Markdown;
using System.Text.RegularExpressions;

namespace Orchestra.Tests.UnitTests;

/// <summary>
/// Тесты для констант и моделей markdown workflow системы
/// </summary>
public class MarkdownWorkflowConstantsTests
{
    #region MarkdownWorkflowConstants Tests

    [Fact]
    public void Constants_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(".md", MarkdownWorkflowConstants.FileExtension);
        Assert.Equal(10 * 1024 * 1024, MarkdownWorkflowConstants.MaxFileSize);
        Assert.Equal(5, MarkdownWorkflowConstants.MaxNestingDepth);
        Assert.Equal(30, MarkdownWorkflowConstants.DefaultParsingTimeout);
    }

    [Fact]
    public void Constants_ShouldHaveValidRegexPatterns()
    {
        // Assert - verify regex patterns compile without exceptions
        var variableRegex = new Regex(MarkdownWorkflowConstants.VariablePattern);
        var workflowLinkRegex = new Regex(MarkdownWorkflowConstants.WorkflowLinkPattern);

        Assert.NotNull(variableRegex);
        Assert.NotNull(workflowLinkRegex);
    }

    [Fact]
    public void Constants_RequiredSections_ShouldContainSteps()
    {
        // Assert
        Assert.Contains("Steps", MarkdownWorkflowConstants.RequiredSections);
    }

    [Fact]
    public void Constants_SupportedCommandTypes_ShouldBeValid()
    {
        // Assert
        Assert.Contains("dotnet", MarkdownWorkflowConstants.SupportedCommandTypes);
        Assert.Contains("git", MarkdownWorkflowConstants.SupportedCommandTypes);
        Assert.Contains("powershell", MarkdownWorkflowConstants.SupportedCommandTypes);
        Assert.Contains("bash", MarkdownWorkflowConstants.SupportedCommandTypes);
        Assert.Contains("custom", MarkdownWorkflowConstants.SupportedCommandTypes);
    }

    #endregion

    #region MarkdownSectionType Enum Tests

    [Fact]
    public void SectionType_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)MarkdownSectionType.Metadata);
        Assert.Equal(1, (int)MarkdownSectionType.Variables);
        Assert.Equal(2, (int)MarkdownSectionType.Steps);
        Assert.Equal(3, (int)MarkdownSectionType.Description);
        Assert.Equal(4, (int)MarkdownSectionType.Notes);
        Assert.Equal(99, (int)MarkdownSectionType.Unknown);
    }

    [Fact]
    public void SectionType_ShouldHaveUniqueValues()
    {
        // Arrange
        var allValues = Enum.GetValues<MarkdownSectionType>();
        var distinctValues = allValues.Distinct();

        // Assert
        Assert.Equal(allValues.Length, distinctValues.Count());
    }

    #endregion

    #region MarkdownStepType Enum Tests

    [Fact]
    public void StepType_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)MarkdownStepType.Task);
        Assert.Equal(1, (int)MarkdownStepType.Condition);
        Assert.Equal(2, (int)MarkdownStepType.Loop);
        Assert.Equal(3, (int)MarkdownStepType.Parallel);
        Assert.Equal(4, (int)MarkdownStepType.Delay);
        Assert.Equal(5, (int)MarkdownStepType.SubWorkflow);
    }

    [Fact]
    public void StepType_ShouldHaveUniqueValues()
    {
        // Arrange
        var allValues = Enum.GetValues<MarkdownStepType>();
        var distinctValues = allValues.Distinct();

        // Assert
        Assert.Equal(allValues.Length, distinctValues.Count());
    }

    #endregion

    #region MarkdownVariableType Enum Tests

    [Fact]
    public void VariableType_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)MarkdownVariableType.String);
        Assert.Equal(1, (int)MarkdownVariableType.Number);
        Assert.Equal(2, (int)MarkdownVariableType.Boolean);
        Assert.Equal(3, (int)MarkdownVariableType.DateTime);
        Assert.Equal(4, (int)MarkdownVariableType.FilePath);
        Assert.Equal(5, (int)MarkdownVariableType.Url);
        Assert.Equal(6, (int)MarkdownVariableType.Json);
        Assert.Equal(7, (int)MarkdownVariableType.StringArray);
    }

    [Theory]
    [InlineData(MarkdownVariableType.String)]
    [InlineData(MarkdownVariableType.Number)]
    [InlineData(MarkdownVariableType.Boolean)]
    [InlineData(MarkdownVariableType.DateTime)]
    [InlineData(MarkdownVariableType.FilePath)]
    [InlineData(MarkdownVariableType.Url)]
    [InlineData(MarkdownVariableType.Json)]
    [InlineData(MarkdownVariableType.StringArray)]
    public void VariableType_ShouldBeValidEnumValue(MarkdownVariableType variableType)
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(MarkdownVariableType), variableType));
    }

    [Fact]
    public void VariableType_ShouldHaveUniqueValues()
    {
        // Arrange
        var allValues = Enum.GetValues<MarkdownVariableType>();
        var distinctValues = allValues.Distinct();

        // Assert
        Assert.Equal(allValues.Length, distinctValues.Count());
    }

    #endregion

    #region MarkdownWorkflowStatus Enum Tests

    [Fact]
    public void WorkflowStatus_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)MarkdownWorkflowStatus.Draft);
        Assert.Equal(1, (int)MarkdownWorkflowStatus.Ready);
        Assert.Equal(2, (int)MarkdownWorkflowStatus.Running);
        Assert.Equal(3, (int)MarkdownWorkflowStatus.Completed);
        Assert.Equal(4, (int)MarkdownWorkflowStatus.Failed);
        Assert.Equal(5, (int)MarkdownWorkflowStatus.Paused);
        Assert.Equal(6, (int)MarkdownWorkflowStatus.Cancelled);
    }

    [Fact]
    public void WorkflowStatus_ShouldHaveUniqueValues()
    {
        // Arrange
        var allValues = Enum.GetValues<MarkdownWorkflowStatus>();
        var distinctValues = allValues.Distinct();

        // Assert
        Assert.Equal(allValues.Length, distinctValues.Count());
    }

    #endregion

    #region WorkflowPriority Enum Tests

    [Fact]
    public void WorkflowPriority_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)WorkflowPriority.Low);
        Assert.Equal(1, (int)WorkflowPriority.Normal);
        Assert.Equal(2, (int)WorkflowPriority.High);
        Assert.Equal(3, (int)WorkflowPriority.Critical);
    }

    [Fact]
    public void WorkflowPriority_ShouldHaveUniqueValues()
    {
        // Arrange
        var allValues = Enum.GetValues<WorkflowPriority>();
        var distinctValues = allValues.Distinct();

        // Assert
        Assert.Equal(allValues.Length, distinctValues.Count());
    }

    #endregion

    #region Regex Pattern Tests

    [Theory]
    [InlineData("{{projectPath}}", "projectPath")]
    [InlineData("{{buildConfig}}", "buildConfig")]
    [InlineData("Path is {{projectPath}} and config is {{buildConfig}}", "projectPath")]
    public void VariablePattern_ShouldMatchCorrectly(string input, string expectedVariable)
    {
        // Arrange
        var regex = new Regex(MarkdownWorkflowConstants.VariablePattern);

        // Act
        var match = regex.Match(input);

        // Assert
        Assert.True(match.Success);
        Assert.Equal(expectedVariable, match.Groups[1].Value);
    }

    [Fact]
    public void VariablePattern_ShouldFindMultipleMatches()
    {
        // Arrange
        var input = "Build {{projectPath}} with {{buildConfig}} configuration for {{targetPlatform}}";
        var regex = new Regex(MarkdownWorkflowConstants.VariablePattern);

        // Act
        var matches = regex.Matches(input);

        // Assert
        Assert.Equal(3, matches.Count);
        Assert.Equal("projectPath", matches[0].Groups[1].Value);
        Assert.Equal("buildConfig", matches[1].Groups[1].Value);
        Assert.Equal("targetPlatform", matches[2].Groups[1].Value);
    }

    [Theory]
    [InlineData("[Test Workflow](test.md)", "Test Workflow", "test.md")]
    [InlineData("[Build Process](workflows/build.md)", "Build Process", "workflows/build.md")]
    public void WorkflowLinkPattern_ShouldMatchCorrectly(string input, string expectedTitle, string expectedPath)
    {
        // Arrange
        var regex = new Regex(MarkdownWorkflowConstants.WorkflowLinkPattern);

        // Act
        var match = regex.Match(input);

        // Assert
        Assert.True(match.Success);
        Assert.Equal(expectedTitle, match.Groups[1].Value);
        Assert.Equal(expectedPath, match.Groups[2].Value);
    }

    #endregion

    #region Constants Validation Tests

    [Fact]
    public void FileExtension_ShouldBeValidMarkdownExtension()
    {
        // Assert
        Assert.Equal(".md", MarkdownWorkflowConstants.FileExtension);
        Assert.StartsWith(".", MarkdownWorkflowConstants.FileExtension);
        Assert.Equal(MarkdownWorkflowConstants.FileExtension.ToLowerInvariant(), MarkdownWorkflowConstants.FileExtension);
    }

    [Fact]
    public void NumericConstants_ShouldHavePositiveValues()
    {
        // Assert
        Assert.True(MarkdownWorkflowConstants.MaxFileSize > 0);
        Assert.True(MarkdownWorkflowConstants.MaxNestingDepth > 0);
        Assert.True(MarkdownWorkflowConstants.DefaultParsingTimeout > 0);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void EnumsAndConstants_ShouldSupportTypicalWorkflowScenario()
    {
        // Arrange
        var workflow = new MarkdownWorkflowDocument
        {
            FilePath = "test.md"
        };

        var section = new MarkdownWorkflowSection
        {
            Type = MarkdownSectionType.Steps
        };

        var step = new MarkdownWorkflowStep
        {
            Type = MarkdownStepType.Task
        };

        var variable = new MarkdownWorkflowVariable
        {
            Type = MarkdownVariableType.String
        };

        // Test enum values independently
        var status = MarkdownWorkflowStatus.Ready;
        var priority = WorkflowPriority.Normal;

        // Act & Assert
        Assert.Equal("test.md", workflow.FilePath);
        Assert.Equal(MarkdownWorkflowStatus.Ready, status);
        Assert.Equal(WorkflowPriority.Normal, priority);
        Assert.Equal(MarkdownSectionType.Steps, section.Type);
        Assert.Equal(MarkdownStepType.Task, step.Type);
        Assert.Equal(MarkdownVariableType.String, variable.Type);
    }

    [Fact]
    public void Enums_ShouldBeConsistentAcrossTypes()
    {
        // Assert - Verify that all enums have valid values
        var sectionTypes = Enum.GetValues<MarkdownSectionType>();
        var stepTypes = Enum.GetValues<MarkdownStepType>();
        var variableTypes = Enum.GetValues<MarkdownVariableType>();
        var workflowStatuses = Enum.GetValues<MarkdownWorkflowStatus>();
        var priorities = Enum.GetValues<WorkflowPriority>();

        // All enums should have at least one value
        Assert.True(sectionTypes.Length > 0);
        Assert.True(stepTypes.Length > 0);
        Assert.True(variableTypes.Length > 0);
        Assert.True(workflowStatuses.Length > 0);
        Assert.True(priorities.Length > 0);
    }

    #endregion
}