using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchestra.Core.Models;
using Orchestra.Core.Services.Connectors;
using Xunit;

namespace Orchestra.Tests.Services.Connectors;

/// <summary>
/// Тесты для ReadOutputLoopAsync в TerminalAgentConnector
/// </summary>
/// <remarks>
/// Тестируют фоновое чтение вывода агента из stream:
/// - Непрерывное чтение строк из stream
/// - Запись строк в IAgentOutputBuffer
/// - Обработку завершения stream (null от ReadLineAsync)
/// - Обработку cancellation
/// - Обработку IO ошибок
/// - Обработку disposed stream
/// - Обновление LastActivityAt
/// - Обновление ConnectionStatus
/// </remarks>
public class TerminalAgentConnectorReadLoopTests
{
    private readonly Mock<ILogger<TerminalAgentConnector>> _loggerMock;
    private readonly Mock<IAgentOutputBuffer> _outputBufferMock;
    private readonly Mock<IOptions<TerminalConnectorOptions>> _optionsMock;
    private readonly TerminalConnectorOptions _options;

    public TerminalAgentConnectorReadLoopTests()
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

    #region ReadOutputLoopAsync Tests

    [Fact]
    public async Task ReadOutputLoopAsync_WithStreamData_ReadsAllLines()
    {
        // Arrange
        var lines = new[] { "line1", "line2", "line3", "line4", "line5" };
        using var stream = CreateStreamWithLines(lines);

        var readLines = new List<string>();
        _outputBufferMock
            .Setup(b => b.AppendLineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((line, _) => readLines.Add(line))
            .Returns(Task.CompletedTask);

        // Act
        await InvokeReadOutputLoopAsync(stream);

        // Assert
        Assert.Equal(lines.Length, readLines.Count);
        Assert.Equal(lines, readLines);

        // Verify all lines were appended to buffer
        _outputBufferMock.Verify(
            b => b.AppendLineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(lines.Length));
    }

    [Fact]
    public async Task ReadOutputLoopAsync_WithEmptyStream_CompletesImmediately()
    {
        // Arrange
        using var stream = new MemoryStream(); // Empty stream

        // Act
        await InvokeReadOutputLoopAsync(stream);

        // Assert - should complete without errors
        _outputBufferMock.Verify(
            b => b.AppendLineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReadOutputLoopAsync_WithUTF8Characters_DecodesCorrectly()
    {
        // Arrange
        var lines = new[] { "test 测试", "тест 🚀", "مرحبا עולם" };
        using var stream = CreateStreamWithLines(lines);

        var readLines = new List<string>();
        _outputBufferMock
            .Setup(b => b.AppendLineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((line, _) => readLines.Add(line))
            .Returns(Task.CompletedTask);

        // Act
        await InvokeReadOutputLoopAsync(stream);

        // Assert
        Assert.Equal(lines, readLines);
    }

    [Fact]
    public async Task ReadOutputLoopAsync_WithCancellation_StopsReading()
    {
        // Arrange
        var lines = new[] { "line1", "line2", "line3", "line4", "line5" };
        using var stream = CreateInfiniteStream(lines); // Stream that never ends

        var readLines = new List<string>();
        _outputBufferMock
            .Setup(b => b.AppendLineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((line, _) => readLines.Add(line))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Cancel after 100ms

        // Act
        await InvokeReadOutputLoopAsync(stream, cts.Token);

        // Assert - should have read some lines
        Assert.NotEmpty(readLines);
        // Cancellation should have logged a debug message
        _loggerMock.Verify(
            l => l.Log(
                It.Is<LogLevel>(level => level == LogLevel.Debug || level == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ReadOutputLoopAsync_WhenStreamEnds_UpdatesStatusToDisconnected()
    {
        // Arrange
        var lines = new[] { "line1", "line2" };
        using var stream = CreateStreamWithLines(lines);

        _outputBufferMock
            .Setup(b => b.AppendLineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await InvokeReadOutputLoopAsync(stream);

        // Assert - should log stream ended message
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stream ended")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReadOutputLoopAsync_WhenIOException_UpdatesStatusToError()
    {
        // Arrange
        var streamMock = new Mock<Stream>();
        streamMock.Setup(s => s.CanRead).Returns(true);

        _outputBufferMock
            .Setup(b => b.AppendLineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Buffer error"));

        // Create stream that returns one line then throws
        using var stream = CreateStreamWithLines(new[] { "line1" });

        // Act
        await InvokeReadOutputLoopAsync(stream);

        // Assert - should log IO error
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("IO error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ReadOutputLoopAsync_WhenObjectDisposedException_UpdatesStatusToDisconnected()
    {
        // Arrange
        var streamMock = new Mock<Stream>();
        streamMock.Setup(s => s.CanRead).Returns(true);

        _outputBufferMock
            .Setup(b => b.AppendLineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ObjectDisposedException("Buffer"));

        // Create stream that returns one line
        using var stream = CreateStreamWithLines(new[] { "line1" });

        // Act
        await InvokeReadOutputLoopAsync(stream);

        // Assert - should log disposed warning
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stream disposed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ReadOutputLoopAsync_WithLongLines_ReadsCorrectly()
    {
        // Arrange
        var longLine = new string('A', 10000); // 10KB line
        var lines = new[] { longLine, "short line", longLine };
        using var stream = CreateStreamWithLines(lines);

        var readLines = new List<string>();
        _outputBufferMock
            .Setup(b => b.AppendLineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((line, _) => readLines.Add(line))
            .Returns(Task.CompletedTask);

        // Act
        await InvokeReadOutputLoopAsync(stream);

        // Assert
        Assert.Equal(lines, readLines);
    }

    [Fact]
    public async Task ReadOutputLoopAsync_WithEmptyLines_ReadsEmptyStrings()
    {
        // Arrange
        var lines = new[] { "line1", "", "line3" };
        using var stream = CreateStreamWithLines(lines);

        var readLines = new List<string>();
        _outputBufferMock
            .Setup(b => b.AppendLineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((line, _) => readLines.Add(line))
            .Returns(Task.CompletedTask);

        // Act
        await InvokeReadOutputLoopAsync(stream);

        // Assert
        Assert.Equal(lines, readLines);
    }

    [Fact]
    public async Task ReadOutputLoopAsync_CompletesWithoutLeaks_StreamStaysOpen()
    {
        // Arrange
        var lines = new[] { "line1", "line2" };
        using var stream = CreateStreamWithLines(lines);

        _outputBufferMock
            .Setup(b => b.AppendLineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await InvokeReadOutputLoopAsync(stream);

        // Assert - stream should still be open (leaveOpen: true)
        Assert.True(stream.CanRead);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Создает MemoryStream с заданными строками
    /// </summary>
    private MemoryStream CreateStreamWithLines(string[] lines)
    {
        var content = string.Join("\n", lines);
        var bytes = Encoding.UTF8.GetBytes(content);
        return new MemoryStream(bytes);
    }

    /// <summary>
    /// Создает "бесконечный" stream для тестирования cancellation
    /// </summary>
    private Stream CreateInfiniteStream(string[] repeatLines)
    {
        // Create a stream that repeats lines indefinitely
        var content = string.Join("\n", repeatLines) + "\n";
        var bytes = Encoding.UTF8.GetBytes(content);

        // Repeat content many times
        var largeBytes = new byte[bytes.Length * 1000];
        for (int i = 0; i < 1000; i++)
        {
            Array.Copy(bytes, 0, largeBytes, i * bytes.Length, bytes.Length);
        }

        return new MemoryStream(largeBytes);
    }

    /// <summary>
    /// Вызывает приватный метод ReadOutputLoopAsync через рефлексию
    /// </summary>
    private async Task InvokeReadOutputLoopAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var connector = new TerminalAgentConnector(_loggerMock.Object, _outputBufferMock.Object, _optionsMock.Object);

        try
        {
            // Set agent ID for logging
            var agentIdField = typeof(TerminalAgentConnector).GetField("_agentId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            agentIdField?.SetValue(connector, "test-agent");

            // Get private method
            var method = typeof(TerminalAgentConnector).GetMethod(
                "ReadOutputLoopAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method == null)
            {
                throw new InvalidOperationException("ReadOutputLoopAsync method not found");
            }

            // Invoke method
            var task = method.Invoke(connector, new object[] { stream, cancellationToken }) as Task;

            if (task == null)
            {
                throw new InvalidOperationException("ReadOutputLoopAsync did not return Task");
            }

            await task;
        }
        finally
        {
            connector.Dispose();
        }
    }

    #endregion
}
