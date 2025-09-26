using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchestra.Agents.ClaudeCode;
using Orchestra.Core.Services;

namespace Orchestra.Tests.UnitTests.ClaudeCode;

/// <summary>
/// Базовые unit тесты для ClaudeCodeService для проверки основной функциональности
/// </summary>
public class ClaudeCodeServiceBasicTests
{
    private readonly Mock<ILogger<ClaudeCodeService>> _mockLogger;
    private readonly Mock<IAgentExecutor> _mockAgentExecutor;
    private readonly ClaudeCodeConfiguration _configuration;

    public ClaudeCodeServiceBasicTests()
    {
        _mockLogger = new Mock<ILogger<ClaudeCodeService>>();
        _mockAgentExecutor = new Mock<IAgentExecutor>();

        _configuration = new ClaudeCodeConfiguration
        {
            DefaultCliPath = @"C:\test\claude.cmd",
            DefaultTimeout = TimeSpan.FromMinutes(5),
            AllowedTools = new[] { "Bash", "Read", "Write" },
            RetryAttempts = 2,
            RetryDelay = TimeSpan.FromMilliseconds(100)
        };
    }

    private ClaudeCodeService CreateService()
    {
        var mockOptions = new Mock<IOptions<ClaudeCodeConfiguration>>();
        mockOptions.Setup(o => o.Value).Returns(_configuration);

        return new ClaudeCodeService(mockOptions.Object, _mockLogger.Object, _mockAgentExecutor.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateService()
    {
        // Act
        var service = CreateService();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task IsAgentAvailableAsync_WithSuccessfulResponse_ShouldReturnTrue()
    {
        // Arrange
        var service = CreateService();
        const string agentId = "test-agent";

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
    public async Task ExecuteCommandAsync_WithBasicParameters_ShouldReturnResult()
    {
        // Arrange
        var service = CreateService();
        const string agentId = "test-agent";
        const string command = "test command";
        var parameters = new Dictionary<string, object>();

        _mockAgentExecutor
            .Setup(x => x.ExecuteCommandAsync(command, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResponse
            {
                Success = true,
                Output = "Success",
                ErrorMessage = "",
                ExecutionTime = TimeSpan.FromSeconds(1),
                Metadata = new Dictionary<string, object>()
            });

        // Act
        var result = await service.ExecuteCommandAsync(agentId, command, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(agentId, result.AgentId);
        Assert.Equal("Success", result.Output);
        Assert.True(result.ExecutedSteps.Count > 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsAgentAvailableAsync_WithInvalidAgentId_ShouldThrowArgumentException(string agentId)
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.IsAgentAvailableAsync(agentId));
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithNullParameters_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        const string agentId = "test-agent";
        const string command = "test command";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ExecuteCommandAsync(agentId, command, null!));
    }
}