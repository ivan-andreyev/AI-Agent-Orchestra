using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchestra.Core.Models;
using Orchestra.Core.Services.Connectors;
using Xunit;

namespace Orchestra.Tests.Services.Connectors;

/// <summary>
/// Тесты для TerminalAgentConnector
/// </summary>
/// <remarks>
/// Тестируют базовую функциональность TerminalAgentConnector:
/// - Создание экземпляра
/// - Свойства интерфейса IAgentConnector
/// - Disposal pattern
/// - Validation логику
///
/// NOTE: Полная функциональность (ConnectAsync, SendCommandAsync, ReadOutputAsync)
/// будет протестирована после реализации Task 1.2B-1.2D.
/// </remarks>
public class TerminalAgentConnectorTests : IDisposable
{
    private readonly Mock<ILogger<TerminalAgentConnector>> _loggerMock;
    private readonly Mock<IAgentOutputBuffer> _outputBufferMock;
    private readonly Mock<IOptions<TerminalConnectorOptions>> _optionsMock;
    private readonly TerminalAgentConnector _connector;

    public TerminalAgentConnectorTests()
    {
        _loggerMock = new Mock<ILogger<TerminalAgentConnector>>();
        _outputBufferMock = new Mock<IAgentOutputBuffer>();
        _optionsMock = new Mock<IOptions<TerminalConnectorOptions>>();
        _optionsMock.Setup(x => x.Value).Returns(new TerminalConnectorOptions());
        _connector = new TerminalAgentConnector(
            _loggerMock.Object,
            _outputBufferMock.Object,
            _optionsMock.Object);
    }

    public void Dispose()
    {
        _connector?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act & Assert
        Assert.NotNull(_connector);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TerminalAgentConnector(null!, _outputBufferMock.Object, _optionsMock.Object));

        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullOutputBuffer_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TerminalAgentConnector(_loggerMock.Object, null!, _optionsMock.Object));

        Assert.Equal("outputBuffer", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TerminalAgentConnector(_loggerMock.Object, _outputBufferMock.Object, null!));

        Assert.Equal("options", exception.ParamName);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void ConnectorType_ReturnsTerminal()
    {
        // Act
        var connectorType = _connector.ConnectorType;

        // Assert
        Assert.Equal("terminal", connectorType);
    }

    [Fact]
    public void AgentId_WhenNotConnected_ReturnsNull()
    {
        // Act
        var agentId = _connector.AgentId;

        // Assert
        Assert.Null(agentId);
    }

    [Fact]
    public void Status_InitiallyDisconnected()
    {
        // Act
        var status = _connector.Status;

        // Assert
        Assert.Equal(ConnectionStatus.Disconnected, status);
    }

    [Fact]
    public void IsConnected_InitiallyFalse()
    {
        // Act
        var isConnected = _connector.IsConnected;

        // Assert
        Assert.False(isConnected);
    }

    [Fact]
    public void LastActivityAt_InitiallyRecentTime()
    {
        // Act
        var lastActivity = _connector.LastActivityAt;

        // Assert
        var timeDifference = Math.Abs((DateTime.UtcNow - lastActivity).TotalSeconds);
        Assert.True(timeDifference < 5, $"LastActivityAt should be within 5 seconds of now, but difference was {timeDifference} seconds");
    }

    #endregion

    #region ConnectAsync Tests

    [Fact]
    public async Task ConnectAsync_WithNullAgentId_ThrowsArgumentNullException()
    {
        // Arrange
        var connectionParams = CreateValidTerminalConnectionParams();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _connector.ConnectAsync(null!, connectionParams));

        Assert.Equal("agentId", exception.ParamName);
    }

    [Fact]
    public async Task ConnectAsync_WithEmptyAgentId_ThrowsArgumentNullException()
    {
        // Arrange
        var connectionParams = CreateValidTerminalConnectionParams();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _connector.ConnectAsync("", connectionParams));
    }

    [Fact]
    public async Task ConnectAsync_WithNullConnectionParams_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _connector.ConnectAsync("test-agent", null!));

        Assert.Equal("connectionParams", exception.ParamName);
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidConnectionParams_ReturnsFailure()
    {
        // Arrange
        var invalidParams = new AgentConnectionParams
        {
            ConnectorType = "terminal"
            // Missing required parameters: PipeName, SocketPath, ProcessId
        };

        // Act
        var result = await _connector.ConnectAsync("test-agent", invalidParams);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Terminal connector requires", result.ErrorMessage);
    }

    [Fact]
    public async Task ConnectAsync_WithMismatchedConnectorType_ReturnsFailure()
    {
        // Arrange
        var invalidParams = new AgentConnectionParams
        {
            ConnectorType = "api", // Mismatch: should be "terminal"
            SocketPath = "/tmp/test.sock"
        };

        // Act
        var result = await _connector.ConnectAsync("test-agent", invalidParams);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("ConnectorType mismatch", result.ErrorMessage);
    }

    [Fact]
    public async Task ConnectAsync_NotYetImplemented_ReturnsFailure()
    {
        // Arrange
        var connectionParams = CreateValidTerminalConnectionParams();

        // Act
        var result = await _connector.ConnectAsync("test-agent", connectionParams);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Not implemented", result.ErrorMessage);
        Assert.Contains("Task 1.2B", result.ErrorMessage);
    }

    #endregion

    #region SendCommandAsync Tests

    [Fact]
    public async Task SendCommandAsync_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _connector.SendCommandAsync(null!));

        Assert.Equal("command", exception.ParamName);
    }

    [Fact]
    public async Task SendCommandAsync_WithEmptyCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _connector.SendCommandAsync(""));
    }

    [Fact]
    public async Task SendCommandAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _connector.SendCommandAsync("test command"));

        Assert.Contains("not connected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region ReadOutputAsync Tests

    [Fact]
    public async Task ReadOutputAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var line in _connector.ReadOutputAsync(cancellationTokenSource.Token))
            {
                // Should not reach here
            }
        });

        Assert.Contains("not connected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region DisconnectAsync Tests

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _connector.DisconnectAsync());

        Assert.Contains("not connected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Act & Assert - should not throw
        _connector.Dispose();
        _connector.Dispose(); // Second call should be safe
    }

    [Fact]
    public async Task ConnectAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var connectionParams = CreateValidTerminalConnectionParams();
        _connector.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _connector.ConnectAsync("test-agent", connectionParams));
    }

    [Fact]
    public async Task SendCommandAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        _connector.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _connector.SendCommandAsync("test command"));
    }

    [Fact]
    public async Task ReadOutputAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        _connector.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await foreach (var line in _connector.ReadOutputAsync())
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task DisconnectAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        _connector.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _connector.DisconnectAsync());
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Создает валидные параметры подключения для terminal connector
    /// </summary>
    private static AgentConnectionParams CreateValidTerminalConnectionParams()
    {
        return new AgentConnectionParams
        {
            ConnectorType = "terminal",
            SocketPath = "/tmp/orchestra_test.sock"
        };
    }

    #endregion
}
