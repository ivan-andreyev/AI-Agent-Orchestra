using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchestra.Core.Models;
using Orchestra.Core.Services.Connectors;
using Xunit;

namespace Orchestra.Tests.Services.Connectors;

/// <summary>
/// Тесты для Task 1.2C: Command Sending и Output Reading в TerminalAgentConnector
/// </summary>
/// <remarks>
/// Тестируют функциональность отправки команд и чтения вывода:
/// - SendCommandAsync: отправка команд через stream
/// - ReadOutputLoopAsync: фоновое чтение из stream
/// - ReadOutputAsync: стриминг вывода из буфера
/// - Интеграцию между command sending и output reading
/// </remarks>
public class TerminalAgentConnectorCommandTests : IDisposable
{
    private readonly Mock<ILogger<TerminalAgentConnector>> _loggerMock;
    private readonly Mock<IAgentOutputBuffer> _outputBufferMock;
    private readonly Mock<IOptions<TerminalConnectorOptions>> _optionsMock;
    private readonly TerminalConnectorOptions _options;

    public TerminalAgentConnectorCommandTests()
    {
        _loggerMock = new Mock<ILogger<TerminalAgentConnector>>();
        _outputBufferMock = new Mock<IAgentOutputBuffer>();
        _optionsMock = new Mock<IOptions<TerminalConnectorOptions>>();

        _options = new TerminalConnectorOptions
        {
            UseUnixSockets = true,
            UseNamedPipes = true,
            DefaultSocketPath = "/tmp/test_agent.sock",
            DefaultPipeName = "test_agent_pipe",
            ConnectionTimeoutMs = 5000
        };

        _optionsMock.Setup(x => x.Value).Returns(_options);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    #region SendCommandAsync Tests

    [Fact]
    public async Task SendCommandAsync_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var connector = new TerminalAgentConnector(_loggerMock.Object, _outputBufferMock.Object, _optionsMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => connector.SendCommandAsync(null!));

        connector.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_WithEmptyCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var connector = new TerminalAgentConnector(_loggerMock.Object, _outputBufferMock.Object, _optionsMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => connector.SendCommandAsync(""));

        connector.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_WithWhitespaceCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var connector = new TerminalAgentConnector(_loggerMock.Object, _outputBufferMock.Object, _optionsMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => connector.SendCommandAsync("   "));

        connector.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var connector = new TerminalAgentConnector(_loggerMock.Object, _outputBufferMock.Object, _optionsMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connector.SendCommandAsync("test command"));

        Assert.Contains("Not connected", exception.Message);

        connector.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_WithValidCommand_WritesToStream()
    {
        // Arrange
        using var memoryStream = new MemoryStream();
        var connector = await CreateConnectedConnectorWithStream(memoryStream);

        var command = "test command";

        // Act
        var result = await connector.SendCommandAsync(command);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(command, result.Command);

        // Verify command written to stream with newline
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream, Encoding.UTF8);
        var writtenCommand = await reader.ReadLineAsync();
        Assert.Equal(command, writtenCommand);

        connector.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_WithValidCommand_FlushesStream()
    {
        // Arrange
        var streamMock = new Mock<Stream>();
        streamMock.Setup(s => s.CanWrite).Returns(true);
        streamMock.Setup(s => s.CanRead).Returns(true);

        var connector = await CreateConnectedConnectorWithStream(streamMock.Object);

        // Act
        var result = await connector.SendCommandAsync("test command");

        // Assert
        Assert.True(result.Success);

        // Verify FlushAsync was called
        streamMock.Verify(s => s.FlushAsync(It.IsAny<CancellationToken>()), Times.Once);

        connector.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_WithUTF8Characters_EncodesCorrectly()
    {
        // Arrange
        using var memoryStream = new MemoryStream();
        var connector = await CreateConnectedConnectorWithStream(memoryStream);

        var command = "test 测试 тест 🚀";

        // Act
        var result = await connector.SendCommandAsync(command);

        // Assert
        Assert.True(result.Success);

        // Verify UTF8 encoding
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream, Encoding.UTF8);
        var writtenCommand = await reader.ReadLineAsync();
        Assert.Equal(command, writtenCommand);

        connector.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_WithMultipleCommands_WritesAllCommands()
    {
        // Arrange
        using var memoryStream = new MemoryStream();
        var connector = await CreateConnectedConnectorWithStream(memoryStream);

        var commands = new[] { "command1", "command2", "command3" };

        // Act
        foreach (var command in commands)
        {
            var result = await connector.SendCommandAsync(command);
            Assert.True(result.Success);
        }

        // Assert
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream, Encoding.UTF8);

        foreach (var expectedCommand in commands)
        {
            var writtenCommand = await reader.ReadLineAsync();
            Assert.Equal(expectedCommand, writtenCommand);
        }

        connector.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_WithCancellationToken_PassesTokenToStream()
    {
        // Arrange
        var streamMock = new Mock<Stream>();
        streamMock.Setup(s => s.CanWrite).Returns(true);
        streamMock.Setup(s => s.CanRead).Returns(true);

        CancellationToken capturedToken = default;

        // Setup WriteAsync to capture the cancellation token
        streamMock.Setup(s => s.WriteAsync(
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns<byte[], int, int, CancellationToken>((buffer, offset, count, ct) =>
            {
                capturedToken = ct;
                return Task.CompletedTask;
            });

        streamMock.Setup(s => s.FlushAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var connector = await CreateConnectedConnectorWithStream(streamMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var result = await connector.SendCommandAsync("test", cts.Token);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(cts.Token, capturedToken); // Verify token was passed through

        connector.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_WhenIOExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var streamMock = new Mock<Stream>();
        streamMock.Setup(s => s.CanWrite).Returns(true);
        streamMock.Setup(s => s.CanRead).Returns(true);
        streamMock.Setup(s => s.WriteAsync(
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Stream error"));

        var connector = await CreateConnectedConnectorWithStream(streamMock.Object);

        // Act
        var result = await connector.SendCommandAsync("test command");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("IO error", result.ErrorMessage);

        connector.Dispose();
    }

    [Fact]
    public async Task SendCommandAsync_WhenStreamDisposed_ReturnsFailureResult()
    {
        // Arrange
        var streamMock = new Mock<Stream>();
        streamMock.Setup(s => s.CanWrite).Returns(true);
        streamMock.Setup(s => s.CanRead).Returns(true);
        streamMock.Setup(s => s.WriteAsync(
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ObjectDisposedException("Stream"));

        var connector = await CreateConnectedConnectorWithStream(streamMock.Object);

        // Act
        var result = await connector.SendCommandAsync("test command");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Stream disposed", result.ErrorMessage);

        connector.Dispose();
    }

    #endregion

    #region ReadOutputAsync Tests

    [Fact]
    public async Task ReadOutputAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var connector = new TerminalAgentConnector(_loggerMock.Object, _outputBufferMock.Object, _optionsMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in connector.ReadOutputAsync())
            {
                // Should throw before any iteration
            }
        });

        connector.Dispose();
    }

    [Fact]
    public async Task ReadOutputAsync_WithBufferedLines_ReturnsLines()
    {
        // Arrange
        var lines = new[] { "line1", "line2", "line3" };
        var asyncEnumerable = CreateAsyncEnumerable(lines);

        _outputBufferMock
            .Setup(b => b.GetLinesAsync(null, It.IsAny<CancellationToken>()))
            .Returns(asyncEnumerable);

        using var memoryStream = new MemoryStream();
        var connector = await CreateConnectedConnectorWithStream(memoryStream);

        // Act
        var result = new List<string>();
        await foreach (var line in connector.ReadOutputAsync())
        {
            result.Add(line);
        }

        // Assert
        Assert.Equal(lines.Length, result.Count);
        Assert.Equal(lines, result);

        connector.Dispose();
    }

    [Fact]
    public async Task ReadOutputAsync_WithEmptyBuffer_ReturnsNoLines()
    {
        // Arrange
        var asyncEnumerable = CreateAsyncEnumerable(Array.Empty<string>());

        _outputBufferMock
            .Setup(b => b.GetLinesAsync(null, It.IsAny<CancellationToken>()))
            .Returns(asyncEnumerable);

        using var memoryStream = new MemoryStream();
        var connector = await CreateConnectedConnectorWithStream(memoryStream);

        // Act
        var result = new List<string>();
        await foreach (var line in connector.ReadOutputAsync())
        {
            result.Add(line);
        }

        // Assert
        Assert.Empty(result);

        connector.Dispose();
    }

    [Fact]
    public async Task ReadOutputAsync_WithCancellation_StopsReading()
    {
        // Arrange
        var lines = new[] { "line1", "line2", "line3", "line4", "line5" };

        // Create cancellation-aware async enumerable
        async IAsyncEnumerable<string> CreateCancellableEnumerable([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var line in lines)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(1, ct);
                yield return line;
            }
        }

        _outputBufferMock
            .Setup(b => b.GetLinesAsync(null, It.IsAny<CancellationToken>()))
            .Returns<string?, CancellationToken>((filter, ct) => CreateCancellableEnumerable(ct));

        using var memoryStream = new MemoryStream();
        var connector = await CreateConnectedConnectorWithStream(memoryStream);

        using var cts = new CancellationTokenSource();

        // Act
        var result = new List<string>();
        try
        {
            await foreach (var line in connector.ReadOutputAsync(cts.Token))
            {
                result.Add(line);

                // Cancel after reading 2 lines
                if (result.Count == 2)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        // Assert
        Assert.Equal(2, result.Count);

        connector.Dispose();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Создает connected connector с указанным stream для тестирования
    /// </summary>
    private async Task<TerminalAgentConnector> CreateConnectedConnectorWithStream(Stream stream)
    {
        var connector = new TerminalAgentConnector(_loggerMock.Object, _outputBufferMock.Object, _optionsMock.Object);

        // Use reflection to set connection state for testing
        var connectorType = typeof(TerminalAgentConnector);

        var statusField = connectorType.GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(connector, ConnectionStatus.Connected);

        var agentIdField = connectorType.GetField("_agentId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        agentIdField?.SetValue(connector, "test-agent");

        var streamField = connectorType.GetField("_connectionStream", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        streamField?.SetValue(connector, stream);

        await Task.CompletedTask;
        return connector;
    }

    /// <summary>
    /// Создает асинхронный enumerable из массива строк
    /// </summary>
    private async IAsyncEnumerable<string> CreateAsyncEnumerable(string[] items)
    {
        foreach (var item in items)
        {
            await Task.Delay(1); // Simulate async operation
            yield return item;
        }
    }

    #endregion
}
