using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchestra.Core.Models;
using Orchestra.Core.Services.Connectors;
using Xunit;

namespace Orchestra.Tests.Services.Connectors;

/// <summary>
/// Интеграционные тесты для полного flow Command Sending и Output Reading
/// </summary>
/// <remarks>
/// Тестируют интеграцию между:
/// - SendCommandAsync → Stream → ReadOutputLoopAsync → OutputBuffer → ReadOutputAsync
/// - Полный цикл: отправка команды, получение вывода, чтение результата
/// - Реалистичные сценарии с использованием реальных stream (pipe-based)
/// </remarks>
public class TerminalAgentConnectorIntegrationFlowTests : IDisposable
{
    private readonly Mock<ILogger<TerminalAgentConnector>> _loggerMock;
    private readonly Mock<IOptions<TerminalConnectorOptions>> _optionsMock;
    private readonly TerminalConnectorOptions _options;

    public TerminalAgentConnectorIntegrationFlowTests()
    {
        _loggerMock = new Mock<ILogger<TerminalAgentConnector>>();
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

    #region Integration Tests

    [Fact]
    public async Task CommandOutputFlow_SendCommandAndReadOutput_WorksEndToEnd()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        // Create bidirectional pipe for testing
        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        using var clientPipe = new AnonymousPipeClientStream(PipeDirection.Out, serverPipe.ClientSafePipeHandle);

        // Simulate agent output in background
        var outputLines = new[] { "output1", "output2", "output3" };
        var outputTask = Task.Run(async () =>
        {
            await Task.Delay(100); // Wait for connector to be ready

            using var writer = new StreamWriter(clientPipe, Encoding.UTF8, leaveOpen: true);
            foreach (var line in outputLines)
            {
                await writer.WriteLineAsync(line);
                await writer.FlushAsync();
                await Task.Delay(50); // Simulate realistic output timing
            }
        });

        // Set up connector with pipe stream
        await SetConnectedState(connector, serverPipe);

        // Start background output reader
        var cts = new CancellationTokenSource();
        var readerTask = Task.Run(() => InvokeReadOutputLoopAsync(connector, serverPipe, cts.Token));

        // Act - wait for output to be buffered
        await Task.Delay(500);
        cts.Cancel();

        await readerTask;
        await outputTask;

        // Assert - verify all output captured
        var bufferedLines = await outputBuffer.GetLastLinesAsync(10);
        Assert.Equal(outputLines.Length, bufferedLines.Count);
        Assert.Equal(outputLines, bufferedLines);

        connector.Dispose();
    }

    [Fact]
    public async Task CommandOutputFlow_MultipleCommands_AllProcessedCorrectly()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var stream = new BidirectionalMemoryStream();
        await SetConnectedState(connector, stream);

        var commands = new[] { "cmd1", "cmd2", "cmd3" };

        // Act - send all commands
        foreach (var command in commands)
        {
            var result = await connector.SendCommandAsync(command);
            Assert.True(result.Success);
        }

        // Assert - verify commands written to stream
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8);

        foreach (var expectedCommand in commands)
        {
            var writtenCommand = await reader.ReadLineAsync();
            Assert.Equal(expectedCommand, writtenCommand);
        }

        connector.Dispose();
    }

    [Fact]
    public async Task CommandOutputFlow_LargeDataTransfer_HandlesCorrectly()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var stream = new BidirectionalMemoryStream();
        await SetConnectedState(connector, stream);

        // Create large command (10KB)
        var largeCommand = new string('A', 10000);

        // Act
        var result = await connector.SendCommandAsync(largeCommand);

        // Assert
        Assert.True(result.Success);

        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var writtenCommand = await reader.ReadLineAsync();
        Assert.Equal(largeCommand, writtenCommand);

        connector.Dispose();
    }

    [Fact]
    public async Task CommandOutputFlow_ConcurrentCommandsAndReads_ThreadSafe()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var stream = new BidirectionalMemoryStream();
        await SetConnectedState(connector, stream);

        var commandCount = 100;
        var commands = Enumerable.Range(1, commandCount).Select(i => $"command_{i}").ToArray();

        // Act - send commands concurrently
        var sendTasks = commands.Select(cmd => connector.SendCommandAsync(cmd)).ToArray();
        var results = await Task.WhenAll(sendTasks);

        // Assert - all commands succeeded
        Assert.All(results, result => Assert.True(result.Success));

        // Verify all commands written (order may vary due to concurrency)
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var writtenCommands = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            writtenCommands.Add(line);
        }

        Assert.Equal(commandCount, writtenCommands.Count);
        Assert.All(commands, cmd => Assert.Contains(cmd, writtenCommands));

        connector.Dispose();
    }

    [Fact]
    public async Task CommandOutputFlow_ErrorRecovery_HandlesGracefully()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var stream = new FaultyStream(); // Stream that fails after N operations
        await SetConnectedState(connector, stream);

        // Act - send commands until stream fails
        var command1Result = await connector.SendCommandAsync("command1");
        var command2Result = await connector.SendCommandAsync("command2");

        stream.ShouldFail = true; // Trigger failure

        var command3Result = await connector.SendCommandAsync("command3");

        // Assert
        Assert.True(command1Result.Success);
        Assert.True(command2Result.Success);
        Assert.False(command3Result.Success); // Should fail gracefully

        connector.Dispose();
    }

    #endregion

    #region Helper Methods and Classes

    /// <summary>
    /// Устанавливает connector в Connected состояние для тестирования
    /// </summary>
    private async Task SetConnectedState(TerminalAgentConnector connector, Stream stream)
    {
        var connectorType = typeof(TerminalAgentConnector);

        var statusField = connectorType.GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        statusField?.SetValue(connector, ConnectionStatus.Connected);

        var agentIdField = connectorType.GetField("_agentId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        agentIdField?.SetValue(connector, "test-agent");

        var streamField = connectorType.GetField("_connectionStream", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        streamField?.SetValue(connector, stream);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Вызывает приватный метод ReadOutputLoopAsync
    /// </summary>
    private async Task InvokeReadOutputLoopAsync(TerminalAgentConnector connector, Stream stream, CancellationToken cancellationToken)
    {
        var method = typeof(TerminalAgentConnector).GetMethod(
            "ReadOutputLoopAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method == null)
        {
            throw new InvalidOperationException("ReadOutputLoopAsync method not found");
        }

        var task = method.Invoke(connector, new object[] { stream, cancellationToken }) as Task;

        if (task != null)
        {
            await task;
        }
    }

    /// <summary>
    /// Тестовая реализация IAgentOutputBuffer для интеграционных тестов
    /// </summary>
    private class TestAgentOutputBuffer : IAgentOutputBuffer
    {
        private readonly List<string> _lines = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        public int Count => _lines.Count;

        public event EventHandler<OutputLineAddedEventArgs>? LineAdded;

        public async Task AppendLineAsync(string line, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _lines.Add(line);
                LineAdded?.Invoke(this, new OutputLineAddedEventArgs { Line = line, Timestamp = DateTime.UtcNow });
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IReadOnlyList<string>> GetLastLinesAsync(int count = 100, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                return _lines.TakeLast(count).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async IAsyncEnumerable<string> GetLinesAsync(string? regexFilter = null, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                foreach (var line in _lines)
                {
                    yield return line;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _lines.Clear();
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <summary>
    /// Двунаправленный MemoryStream для тестирования
    /// </summary>
    private class BidirectionalMemoryStream : MemoryStream
    {
        public BidirectionalMemoryStream() : base()
        {
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
    }

    /// <summary>
    /// Stream который имитирует ошибки
    /// </summary>
    private class FaultyStream : MemoryStream
    {
        public bool ShouldFail { get; set; }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (ShouldFail)
            {
                throw new IOException("Simulated stream failure");
            }

            await base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (ShouldFail)
            {
                throw new IOException("Simulated stream failure");
            }

            await base.FlushAsync(cancellationToken);
        }
    }

    #endregion
}
