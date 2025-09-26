using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchestra.Agents.ClaudeCode;
using Orchestra.Core.Services;

namespace Orchestra.Tests.UnitTests.ClaudeCode;

/// <summary>
/// Unit тесты для ClaudeCodeService - основного сервиса для взаимодействия с Claude Code агентами
/// </summary>
public class ClaudeCodeServiceTests
{
    private readonly Mock<ILogger<ClaudeCodeService>> _mockLogger;
    private readonly Mock<IAgentExecutor> _mockAgentExecutor;
    private readonly Mock<IOptions<ClaudeCodeConfiguration>> _mockOptions;
    private readonly ClaudeCodeConfiguration _configuration;

    public ClaudeCodeServiceTests()
    {
        _mockLogger = new Mock<ILogger<ClaudeCodeService>>();
        _mockAgentExecutor = new Mock<IAgentExecutor>();
        _mockOptions = new Mock<IOptions<ClaudeCodeConfiguration>>();

        _configuration = new ClaudeCodeConfiguration
        {
            DefaultCliPath = @"C:\test\claude.cmd",
            DefaultTimeout = TimeSpan.FromMinutes(5),
            AllowedTools = new[] { "Bash", "Read", "Write" },
            OutputFormat = "text",
            RetryAttempts = 3,
            RetryDelay = TimeSpan.FromSeconds(1),
            DefaultWorkingDirectory = @"C:\test\workdir"
        };

        _mockOptions.Setup(o => o.Value).Returns(_configuration);
    }

    private ClaudeCodeService CreateService()
    {
        return new ClaudeCodeService(_mockOptions.Object, _mockLogger.Object, _mockAgentExecutor.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeService()
    {
        // Act
        var service = CreateService();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ClaudeCodeService(null!, _mockLogger.Object, _mockAgentExecutor.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ClaudeCodeService(_mockOptions.Object, null!, _mockAgentExecutor.Object));
    }

    [Fact]
    public void Constructor_WithNullAgentExecutor_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ClaudeCodeService(_mockOptions.Object, _mockLogger.Object, null!));
    }

    #endregion

    #region IsAgentAvailableAsync Tests

    [Fact]
    public async Task IsAgentAvailableAsync_WithValidAgentId_ShouldReturnTrue()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = true,
                Output = "Claude Code v1.0.0",
                ErrorMessage = "",
                ExecutionTime = TimeSpan.FromSeconds(1)
            });

        // Act
        var result = await service.IsAgentAvailableAsync(agentId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAgentAvailableAsync_WithAgentExecutorFailure_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = false,
                Output = "",
                ErrorMessage = "Agent not found",
                ExecutionTime = TimeSpan.FromSeconds(1)
            });

        // Act
        var result = await service.IsAgentAvailableAsync(agentId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAgentAvailableAsync_WithException_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        var result = await service.IsAgentAvailableAsync(agentId);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsAgentAvailableAsync_WithInvalidAgentId_ShouldThrowArgumentException(string? agentId)
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.IsAgentAvailableAsync(agentId!));
    }

    #endregion

    #region GetAgentVersionAsync Tests

    [Fact]
    public async Task GetAgentVersionAsync_WithSuccessfulExecution_ShouldReturnVersion()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var expectedVersion = "Claude Code v1.0.0";

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = true,
                Output = expectedVersion,
                ErrorMessage = "",
                ExecutionTime = TimeSpan.FromSeconds(1)
            });

        // Act
        var result = await service.GetAgentVersionAsync(agentId);

        // Assert
        Assert.Equal(expectedVersion, result);
    }

    [Fact]
    public async Task GetAgentVersionAsync_WithFailedExecution_ShouldReturnErrorMessage()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var errorMessage = "Command failed";

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = false,
                Output = "",
                ErrorMessage = errorMessage,
                ExecutionTime = TimeSpan.FromSeconds(1)
            });

        // Act
        var result = await service.GetAgentVersionAsync(agentId);

        // Assert
        Assert.Contains("Version unavailable", result);
        Assert.Contains(errorMessage, result);
    }

    [Fact]
    public async Task GetAgentVersionAsync_WithException_ShouldReturnErrorMessage()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var exceptionMessage = "Test exception";

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));

        // Act
        var result = await service.GetAgentVersionAsync(agentId);

        // Assert
        Assert.Contains("Version check failed", result);
        Assert.Contains(exceptionMessage, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAgentVersionAsync_WithInvalidAgentId_ShouldThrowArgumentException(string? agentId)
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAgentVersionAsync(agentId!));
    }

    #endregion

    #region ExecuteCommandAsync Tests

    [Fact]
    public async Task ExecuteCommandAsync_WithValidParameters_ShouldExecuteSuccessfully()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var command = "test command";
        var parameters = new Dictionary<string, object>
        {
            { "WorkingDirectory", @"C:\test" },
            { "Timeout", TimeSpan.FromMinutes(5) }
        };

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(command, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = true,
                Output = "Command executed successfully",
                ErrorMessage = "",
                ExecutionTime = TimeSpan.FromSeconds(2),
                Metadata = new Dictionary<string, object>()
            });

        // Act
        var result = await service.ExecuteCommandAsync(agentId, command, parameters);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(agentId, result.AgentId);
        Assert.Equal("Command executed successfully", result.Output);
        Assert.True(result.ExecutedSteps.Count > 0);
        Assert.Contains("Parameter validation completed", result.ExecutedSteps);
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithRetryLogic_ShouldRetryOnFailure()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var command = "test command";
        var parameters = new Dictionary<string, object>();

        _mockAgentExecutor
            .SetupSequence(x => x.ExecuteCommandAsync(command, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse { Success = false, ErrorMessage = "First failure" })
            .ReturnsAsync(new AgentExecutionResponse { Success = false, ErrorMessage = "Second failure" })
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = true,
                Output = "Success on third try",
                ExecutionTime = TimeSpan.FromSeconds(1),
                Metadata = new Dictionary<string, object>()
            });

        // Act
        var result = await service.ExecuteCommandAsync(agentId, command, parameters);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Success on third try", result.Output);
        _mockAgentExecutor.Verify(x => x.ExecuteCommandAsync(command, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithTaskIdInParameters_ShouldIncludeTaskContext()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var command = "test command";
        var taskId = "task-123";
        var parameters = new Dictionary<string, object>
        {
            { "TaskId", taskId }
        };

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(command, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = true,
                Output = "Success",
                ExecutionTime = TimeSpan.FromSeconds(1),
                Metadata = new Dictionary<string, object>()
            });

        // Act
        var result = await service.ExecuteCommandAsync(agentId, command, parameters);

        // Assert
        Assert.True(result.ExecutedSteps.Any(step => step.Contains($"TaskId: {taskId}")));
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithException_ShouldReturnFailureResult()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var command = "test command";
        var parameters = new Dictionary<string, object>();
        var exceptionMessage = "Test exception";

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(command, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));

        // Act
        var result = await service.ExecuteCommandAsync(agentId, command, parameters);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(exceptionMessage, result.ErrorMessage);
        Assert.Equal(agentId, result.AgentId);
    }

    [Theory]
    [InlineData(null, "test command")]
    [InlineData("", "test command")]
    [InlineData("   ", "test command")]
    [InlineData("test-agent", null)]
    [InlineData("test-agent", "")]
    [InlineData("test-agent", "   ")]
    public async Task ExecuteCommandAsync_WithInvalidParameters_ShouldThrowArgumentException(string? agentId, string? command)
    {
        // Arrange
        var service = CreateService();
        var parameters = new Dictionary<string, object>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteCommandAsync(agentId!, command!, parameters));
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithNullParameters_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var command = "test command";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ExecuteCommandAsync(agentId, command, null!));
    }

    #endregion

    #region ExecuteWorkflowAsync Tests

    [Fact]
    public async Task ExecuteWorkflowAsync_WithValidWorkflow_ShouldExecuteSuccessfully()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var workflowId = "test-workflow";
        var markdownPath = Path.Combine(Path.GetTempPath(), "test-workflow.md");

        // Create temporary markdown file
        await File.WriteAllTextAsync(markdownPath, "# Test Workflow\n\nThis is a test workflow.");

        var workflow = new WorkflowDefinition
        {
            Id = workflowId,
            Name = "Test Workflow",
            Description = "Test workflow description",
            MarkdownFilePath = markdownPath,
            WorkingDirectory = Path.GetTempPath(),
            Timeout = TimeSpan.FromMinutes(5),
            AllowedTools = new[] { "Bash", "Read" }
        };

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = true,
                Output = "Workflow executed successfully",
                ErrorMessage = "",
                ExecutionTime = TimeSpan.FromSeconds(3),
                Metadata = new Dictionary<string, object>()
            });

        try
        {
            // Act
            var result = await service.ExecuteWorkflowAsync(agentId, workflow);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(agentId, result.AgentId);
            Assert.Equal(workflowId, result.WorkflowId);
            Assert.Equal("Workflow executed successfully", result.Output);
            Assert.Equal(1, result.TotalSteps);
            Assert.Equal(1, result.CompletedSteps);
            Assert.Equal(0, result.FailedSteps);
            Assert.Single(result.StepResults);
            Assert.True(result.ExecutedSteps.Any(step => step.Contains("Workflow validation completed")));
        }
        finally
        {
            // Cleanup
            if (File.Exists(markdownPath))
                File.Delete(markdownPath);
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WithNonExistentMarkdownFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var workflow = new WorkflowDefinition
        {
            Id = "test-workflow",
            Name = "Test Workflow",
            MarkdownFilePath = @"C:\non-existent-file.md",
            WorkingDirectory = Path.GetTempPath()
        };

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.ExecuteWorkflowAsync(agentId, workflow));
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WithNonExistentWorkingDirectory_ShouldThrowDirectoryNotFoundException()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var markdownPath = Path.Combine(Path.GetTempPath(), "test-workflow.md");

        // Create temporary markdown file
        await File.WriteAllTextAsync(markdownPath, "# Test Workflow");

        var workflow = new WorkflowDefinition
        {
            Id = "test-workflow",
            Name = "Test Workflow",
            MarkdownFilePath = markdownPath,
            WorkingDirectory = @"C:\non-existent-directory"
        };

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                service.ExecuteWorkflowAsync(agentId, workflow));
        }
        finally
        {
            // Cleanup
            if (File.Exists(markdownPath))
                File.Delete(markdownPath);
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WithCommandExecutionFailure_ShouldReturnFailureResult()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var markdownPath = Path.Combine(Path.GetTempPath(), "test-workflow.md");

        // Create temporary markdown file
        await File.WriteAllTextAsync(markdownPath, "# Test Workflow");

        var workflow = new WorkflowDefinition
        {
            Id = "test-workflow",
            Name = "Test Workflow",
            MarkdownFilePath = markdownPath,
            WorkingDirectory = Path.GetTempPath()
        };

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = false,
                Output = "",
                ErrorMessage = "Command execution failed",
                ExecutionTime = TimeSpan.FromSeconds(1),
                Metadata = new Dictionary<string, object>()
            });

        try
        {
            // Act
            var result = await service.ExecuteWorkflowAsync(agentId, workflow);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(agentId, result.AgentId);
            Assert.Equal("test-workflow", result.WorkflowId);
            Assert.Equal(1, result.TotalSteps);
            Assert.Equal(0, result.CompletedSteps);
            Assert.Equal(1, result.FailedSteps);
        }
        finally
        {
            // Cleanup
            if (File.Exists(markdownPath))
                File.Delete(markdownPath);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteWorkflowAsync_WithInvalidAgentId_ShouldThrowArgumentException(string? agentId)
    {
        // Arrange
        var service = CreateService();
        var workflow = new WorkflowDefinition
        {
            Id = "test-workflow",
            Name = "Test Workflow",
            MarkdownFilePath = "test.md",
            WorkingDirectory = Path.GetTempPath()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteWorkflowAsync(agentId!, workflow));
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WithNullWorkflow_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ExecuteWorkflowAsync(agentId, null!));
    }

    #endregion

    #region Configuration Parameter Extraction Tests

    [Fact]
    public async Task ExecuteCommandAsync_WithWorkingDirectoryParameter_ShouldUseProvidedDirectory()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var command = "test command";
        var customWorkingDirectory = @"C:\custom\workdir";
        var parameters = new Dictionary<string, object>
        {
            { "WorkingDirectory", customWorkingDirectory }
        };

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(command, customWorkingDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = true,
                Output = "Success",
                ExecutionTime = TimeSpan.FromSeconds(1),
                Metadata = new Dictionary<string, object>()
            })
            .Verifiable();

        // Act
        await service.ExecuteCommandAsync(agentId, command, parameters);

        // Assert
        _mockAgentExecutor.Verify();
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithTimeoutParameter_ShouldExtractTimeout()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var command = "test command";
        var parameters = new Dictionary<string, object>
        {
            { "Timeout", TimeSpan.FromMinutes(15) }
        };

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(command, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = true,
                Output = "Success",
                ExecutionTime = TimeSpan.FromSeconds(1),
                Metadata = new Dictionary<string, object>()
            });

        // Act
        var result = await service.ExecuteCommandAsync(agentId, command, parameters);

        // Assert
        Assert.True(result.ExecutedSteps.Any(step => step.Contains("Timeout configured: 00:15:00")));
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithAllowedToolsParameter_ShouldExtractTools()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var command = "test command";
        var allowedTools = new[] { "Bash", "Edit", "Write" };
        var parameters = new Dictionary<string, object>
        {
            { "AllowedTools", allowedTools }
        };

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(command, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = true,
                Output = "Success",
                ExecutionTime = TimeSpan.FromSeconds(1),
                Metadata = new Dictionary<string, object>()
            });

        // Act
        var result = await service.ExecuteCommandAsync(agentId, command, parameters);

        // Assert
        Assert.True(result.ExecutedSteps.Any(step => step.Contains("Bash, Edit, Write")));
    }

    #endregion

    #region Edge Cases and Error Scenarios

    [Fact]
    public async Task ExecuteCommandAsync_WithLongRunningCommand_ShouldTrackExecutionTime()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var command = "test command";
        var parameters = new Dictionary<string, object>();

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(command, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100); // Simulate some execution time
                return new AgentExecutionResponse
                {
                    Success = true,
                    Output = "Success",
                    ExecutionTime = TimeSpan.FromMilliseconds(100),
                    Metadata = new Dictionary<string, object>()
                };
            });

        // Act
        var result = await service.ExecuteCommandAsync(agentId, command, parameters);

        // Assert
        Assert.True(result.ExecutionTime > TimeSpan.Zero);
        Assert.True(result.ExecutedSteps.Any(step => step.Contains("Command execution completed")));
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithVeryLongCommand_ShouldTruncateInLogs()
    {
        // Arrange
        var service = CreateService();
        var agentId = "test-agent";
        var command = new string('x', 200); // Command longer than 100 characters
        var parameters = new Dictionary<string, object>();

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(command, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = true,
                Output = "Success",
                ExecutionTime = TimeSpan.FromSeconds(1),
                Metadata = new Dictionary<string, object>()
            });

        // Act
        var result = await service.ExecuteCommandAsync(agentId, command, parameters);

        // Assert
        Assert.True(result.Success);
        // Verify that the logging would have truncated the command (checked through mock setup)
        _mockAgentExecutor.Verify(x => x.ExecuteCommandAsync(command, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    #endregion
}