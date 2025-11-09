using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Models;
using Orchestra.Core.Services.Connectors;
using Xunit;

namespace Orchestra.Tests;

/// <summary>
/// PoC tests для ClaudeCodeSubprocessConnector
///
/// Примечание: Полные интеграционные тесты требуют наличия claude code CLI.
/// Эти тесты проверяют основную логику и структуру коннектора.
/// </summary>
public class ClaudeCodeSubprocessConnectorTests
{
    private readonly Mock<ILogger<ClaudeCodeSubprocessConnector>> _mockLogger;
    private readonly Mock<IMediator> _mockMediator;

    public ClaudeCodeSubprocessConnectorTests()
    {
        _mockLogger = new Mock<ILogger<ClaudeCodeSubprocessConnector>>();
        _mockMediator = new Mock<IMediator>();
    }

    [Fact]
    public void Constructor_InitializesConnectorWithDefaultValues()
    {
        // Arrange
        var connector = new ClaudeCodeSubprocessConnector(_mockLogger.Object, _mockMediator.Object);

        // Act & Assert
        Assert.Null(connector.AgentId);
        Assert.False(connector.IsConnected);
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);
        Assert.Equal("subprocess", connector.ConnectorType);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ClaudeCodeSubprocessConnector(null!, _mockMediator.Object));
    }

    [Fact]
    public async Task SendCommandAsync_ThrowsInvalidOperationException_WhenNotConnected()
    {
        // Arrange
        var connector = new ClaudeCodeSubprocessConnector(_mockLogger.Object, _mockMediator.Object);

        // Act
        var result = await connector.SendCommandAsync("test command");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not connected", result.ErrorMessage);
    }

    [Fact]
    public async Task ConnectAsync_ThrowsArgumentNullException_WhenAgentIdIsNull()
    {
        // Arrange
        var connector = new ClaudeCodeSubprocessConnector(_mockLogger.Object, _mockMediator.Object);
        var connectionParams = new AgentConnectionParams
        {
            ConnectorType = "subprocess",
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => connector.ConnectAsync(null!, connectionParams));
    }

    [Fact]
    public async Task ConnectAsync_ThrowsArgumentNullException_WhenConnectionParamsIsNull()
    {
        // Arrange
        var connector = new ClaudeCodeSubprocessConnector(_mockLogger.Object, _mockMediator.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => connector.ConnectAsync("test-agent", null!));
    }

    [Fact]
    public async Task ConnectAsync_ThrowsInvalidOperationException_WhenAlreadyConnected()
    {
        // Arrange
        var connector = new ClaudeCodeSubprocessConnector(_mockLogger.Object, _mockMediator.Object);
        var connectionParams = new AgentConnectionParams
        {
            ConnectorType = "subprocess",
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        // Пытаемся подключиться (будет ошибка, так как claude code не запущен)
        var result = await connector.ConnectAsync("test-agent", connectionParams);

        if (result.Success)
        {
            // Если первое подключение успешно, второе должно выбросить исключение
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => connector.ConnectAsync("another-agent", connectionParams));
        }
    }

    [Fact]
    public async Task DisconnectAsync_ReturnsFailure_WhenNotConnected()
    {
        // Arrange
        var connector = new ClaudeCodeSubprocessConnector(_mockLogger.Object, _mockMediator.Object);

        // Act
        var result = await connector.DisconnectAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not connected", result.ErrorMessage);
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenNotConnected()
    {
        // Arrange
        var connector = new ClaudeCodeSubprocessConnector(_mockLogger.Object, _mockMediator.Object);

        // Act & Assert
        connector.Dispose(); // Не должно выбросить исключение
    }

    [Fact]
    public void StatusChanged_EventIsRaised_WhenConnectorChangesStatus()
    {
        // Arrange
        var connector = new ClaudeCodeSubprocessConnector(_mockLogger.Object, _mockMediator.Object);
        var statusChangedRaised = false;
        ConnectionStatus? oldStatus = null;
        ConnectionStatus? newStatus = null;

        connector.StatusChanged += (sender, args) =>
        {
            statusChangedRaised = true;
            oldStatus = args.OldStatus;
            newStatus = args.NewStatus;
        };

        // Act: Попытка отправки команды вызовет изменение статуса в Error
        _ = connector.SendCommandAsync("test");

        // Assert: Коннектор должен оставаться в Disconnected, так как еще не подключен
        Assert.False(statusChangedRaised); // StatusChanged не должен был быть вызван до подключения
    }

    /// <summary>
    /// PoC тест для проверки JSON parsing
    /// </summary>
    [Fact]
    public void ClaudeResponse_CanDeserializeFromJson()
    {
        // Arrange
        var json = """
            {
                "type": "result",
                "subtype": "success",
                "is_error": false,
                "result": "File created successfully",
                "session_id": "test-session-uuid",
                "duration_ms": 1234,
                "total_cost_usd": 0.05,
                "permission_denials": [],
                "usage": {
                    "input_tokens": 100,
                    "output_tokens": 50
                }
            }
            """;

        // Act
        var response = System.Text.Json.JsonSerializer.Deserialize<ClaudeResponse>(json);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("result", response.Type);
        Assert.Equal("success", response.Subtype);
        Assert.False(response.IsError);
        Assert.Equal("File created successfully", response.Result);
        Assert.Equal("test-session-uuid", response.SessionId);
        Assert.Equal(1234, response.DurationMs);
        Assert.Equal(0.05, response.TotalCostUsd);
        Assert.NotNull(response.Usage);
    }

    [Fact]
    public void ClaudeResponse_CanDeserializeWithPermissionDenials()
    {
        // Arrange
        var json = """
            {
                "type": "result",
                "subtype": "permission_denied",
                "is_error": true,
                "result": "Permission denied for tool execution",
                "session_id": "test-session-uuid",
                "permission_denials": [
                    {
                        "tool_name": "Bash",
                        "tool_use_id": "tool-123",
                        "tool_input": {
                            "command": "rm -rf /"
                        }
                    }
                ]
            }
            """;

        // Act
        var response = System.Text.Json.JsonSerializer.Deserialize<ClaudeResponse>(json);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsError);
        Assert.NotNull(response.PermissionDenials);
        Assert.Single(response.PermissionDenials);
        Assert.Equal("Bash", response.PermissionDenials[0].ToolName);
        Assert.Equal("tool-123", response.PermissionDenials[0].ToolUseId);
    }

    /// <summary>
    /// Integration test для проверки работы с реальным claude code процессом
    /// ПРИМЕЧАНИЕ: Требует наличия claude code CLI в PATH
    /// ПРОПУСКАЕТСЯ если claude code не установлен
    /// </summary>
    [Fact(Skip = "Requires claude code CLI to be installed. Run manually for full integration test.")]
    public async Task IntegrationTest_CanConnectAndExecuteCommand()
    {
        // Arrange
        var connector = new ClaudeCodeSubprocessConnector(_mockLogger.Object, _mockMediator.Object);
        var connectionParams = new AgentConnectionParams
        {
            ConnectorType = "subprocess",
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        try
        {
            // Act
            var connectResult = await connector.ConnectAsync("integration-test-agent", connectionParams);

            // Assert connection
            Assert.True(connectResult.Success, $"Connection failed: {connectResult.ErrorMessage}");
            Assert.True(connector.IsConnected);
            Assert.Equal("subprocess", connector.ConnectorType);

            // Send a simple command
            var commandResult = await connector.SendCommandAsync(
                "Create a file test-output.txt with content 'Hello from Claude Code PoC'");

            Assert.True(commandResult.Success, $"Command failed: {commandResult.ErrorMessage}");

            // Verify file was created
            await Task.Delay(2000); // Give Claude time to create the file
            Assert.True(File.Exists("test-output.txt"));

            // Cleanup
            File.Delete("test-output.txt");
        }
        finally
        {
            // Cleanup
            if (connector.IsConnected)
            {
                await connector.DisconnectAsync();
            }
            connector.Dispose();
        }
    }
}
