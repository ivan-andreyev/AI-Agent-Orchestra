using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchestra.Core.Models;
using Orchestra.Core.Services.Connectors;
using Xunit;

namespace Orchestra.Tests.Services.Connectors;

/// <summary>
/// Тесты для DisconnectAsync и полного lifecycle TerminalAgentConnector
/// </summary>
/// <remarks>
/// Тестируют:
/// - DisconnectAsync functionality (~10 tests)
/// - Full lifecycle: Connect → Send → Read → Disconnect (~5 tests)
/// - Error handling edge cases (~5 tests)
/// - Resource cleanup verification (~3 tests)
/// </remarks>
public class TerminalAgentConnectorDisconnectionTests : IDisposable
{
    private readonly Mock<ILogger<TerminalAgentConnector>> _loggerMock;
    private readonly Mock<IOptions<TerminalConnectorOptions>> _optionsMock;
    private readonly TerminalConnectorOptions _options;

    public TerminalAgentConnectorDisconnectionTests()
    {
        _loggerMock = new Mock<ILogger<TerminalAgentConnector>>();
        _optionsMock = new Mock<IOptions<TerminalConnectorOptions>>();

        _options = new TerminalConnectorOptions
        {
            UseUnixSockets = true,
            UseNamedPipes = true,
            DefaultSocketPath = "/tmp/test_disconnect.sock",
            DefaultPipeName = "test_disconnect_pipe",
            ConnectionTimeoutMs = 5000
        };

        _optionsMock.Setup(x => x.Value).Returns(_options);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    #region DisconnectAsync Tests

    [Fact]
    public async Task DisconnectAsync_WhenConnected_SetsStatusToDisconnecting()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        ConnectionStatus? statusDuringDisconnect = null;

        connector.StatusChanged += (sender, args) =>
        {
            if (args.NewStatus == ConnectionStatus.Disconnecting)
            {
                statusDuringDisconnect = args.NewStatus;
            }
        };

        // Act
        var result = await connector.DisconnectAsync();

        // Assert
        Assert.NotNull(statusDuringDisconnect);
        Assert.Equal(ConnectionStatus.Disconnecting, statusDuringDisconnect.Value);
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);
        Assert.True(result.Success);

        connector.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_ReturnsSuccess()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Act
        var result = await connector.DisconnectAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(DisconnectionReason.UserRequested, result.Reason);

        connector.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_ClearsAgentId()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Verify agent ID set
        Assert.NotNull(connector.AgentId);

        // Act
        var result = await connector.DisconnectAsync();

        // Assert
        Assert.Null(connector.AgentId);
        Assert.True(result.Success);

        connector.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_UpdatesStatusToDisconnected()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        Assert.Equal(ConnectionStatus.Connected, connector.Status);

        // Act
        var result = await connector.DisconnectAsync();

        // Assert
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);
        Assert.True(result.Success);

        connector.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_FiresStatusChangedEvent()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        var statusChangedFired = false;
        ConnectionStatus? newStatus = null;

        connector.StatusChanged += (sender, args) =>
        {
            if (args.NewStatus == ConnectionStatus.Disconnected)
            {
                statusChangedFired = true;
                newStatus = args.NewStatus;
            }
        };

        // Act
        var result = await connector.DisconnectAsync();

        // Assert
        Assert.True(statusChangedFired);
        Assert.Equal(ConnectionStatus.Disconnected, newStatus);
        Assert.True(result.Success);

        connector.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_IncludesMetadataInResult()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Act
        var result = await connector.DisconnectAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata.ContainsKey("agentId"));
        Assert.True(result.Metadata.ContainsKey("disconnectedAt"));
        Assert.True(result.Metadata.ContainsKey("disconnectReason"));

        connector.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_AfterErrorState_ReturnsSuccessWithErrorReason()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Simulate error state by closing the pipe while reader is active
        serverPipe.Dispose();
        await Task.Delay(200); // Wait for error to be detected

        // Act
        var result = await connector.DisconnectAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(DisconnectionReason.Error, result.Reason);

        connector.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_WaitsForBackgroundTaskCompletion()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        using var clientPipe = new AnonymousPipeClientStream(PipeDirection.Out, serverPipe.ClientSafePipeHandle);

        await SetConnectedState(connector, serverPipe);

        // Start writing output to keep reader busy
        var writerTask = Task.Run(async () =>
        {
            using var writer = new StreamWriter(clientPipe, Encoding.UTF8, leaveOpen: true);
            for (int i = 0; i < 100; i++)
            {
                await writer.WriteLineAsync($"line {i}");
                await writer.FlushAsync();
                await Task.Delay(10);
            }
        });

        // Act
        var disconnectTask = connector.DisconnectAsync();
        var completed = await Task.WhenAny(disconnectTask, Task.Delay(10000));

        // Assert - disconnect should complete within reasonable time (not hang)
        Assert.Equal(disconnectTask, completed);
        var result = await disconnectTask;
        Assert.True(result.Success);

        connector.Dispose();
        await writerTask;
    }

    [Fact]
    public async Task DisconnectAsync_WithTimeoutOnBackgroundTask_CompletesSuccessfully()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Act - disconnect immediately (background task still running)
        var result = await connector.DisconnectAsync();

        // Assert - should complete successfully even if background task not fully stopped
        Assert.True(result.Success);
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);

        connector.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_MultipleCalls_ThrowsInvalidOperationException()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Act - first disconnect
        var result1 = await connector.DisconnectAsync();
        Assert.True(result1.Success);

        // Assert - second disconnect should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() => connector.DisconnectAsync());

        connector.Dispose();
    }

    #endregion

    #region Full Lifecycle Integration Tests

    [Fact]
    public async Task FullLifecycle_ConnectSendReadDisconnect_WorksEndToEnd()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        using var clientPipe = new AnonymousPipeClientStream(PipeDirection.Out, serverPipe.ClientSafePipeHandle);

        // Act - Connect
        await SetConnectedState(connector, serverPipe);
        Assert.Equal(ConnectionStatus.Connected, connector.Status);
        Assert.NotNull(connector.AgentId);

        // Act - Send Command (simulate by writing to buffer directly)
        await outputBuffer.AppendLineAsync("response to command");

        // Act - Read Output
        var lines = await outputBuffer.GetLastLinesAsync(10);
        Assert.Single(lines);
        Assert.Equal("response to command", lines[0]);

        // Act - Disconnect
        var disconnectResult = await connector.DisconnectAsync();

        // Assert
        Assert.True(disconnectResult.Success);
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);
        Assert.Null(connector.AgentId);
        Assert.False(connector.IsConnected);

        connector.Dispose();
    }

    [Fact]
    public async Task FullLifecycle_ConnectDisconnectReconnect_NotSupported()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe1 = new AnonymousPipeServerStream(PipeDirection.In);
        using var serverPipe2 = new AnonymousPipeServerStream(PipeDirection.In);

        // Act - First connection
        await SetConnectedState(connector, serverPipe1);
        Assert.Equal(ConnectionStatus.Connected, connector.Status);

        // Act - Disconnect
        var disconnectResult = await connector.DisconnectAsync();
        Assert.True(disconnectResult.Success);
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);

        // Act - Try to reconnect (should throw as connector is single-use)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connector.ConnectAsync("test-agent-2", new AgentConnectionParams
            {
                ConnectorType = "terminal",
                SocketPath = "/tmp/test2.sock"
            }));

        connector.Dispose();
    }

    [Fact]
    public async Task FullLifecycle_StatusTransitions_AllFiredCorrectly()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);

        var statusChanges = new List<(ConnectionStatus Old, ConnectionStatus New)>();
        connector.StatusChanged += (sender, args) =>
        {
            statusChanges.Add((args.OldStatus, args.NewStatus));
        };

        // Act - Connect
        await SetConnectedState(connector, serverPipe);

        // Act - Disconnect
        var result = await connector.DisconnectAsync();

        // Assert - verify all status transitions
        Assert.Contains(statusChanges, sc =>
            sc.Old == ConnectionStatus.Disconnected && sc.New == ConnectionStatus.Connected);
        Assert.Contains(statusChanges, sc =>
            sc.Old == ConnectionStatus.Connected && sc.New == ConnectionStatus.Disconnecting);
        Assert.Contains(statusChanges, sc =>
            sc.Old == ConnectionStatus.Disconnecting && sc.New == ConnectionStatus.Disconnected);

        connector.Dispose();
    }

    [Fact]
    public async Task FullLifecycle_DisconnectCleansUpResources_NoLeaks()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Act - Disconnect
        var result = await connector.DisconnectAsync();

        // Assert - verify all resources cleaned up
        Assert.True(result.Success);
        Assert.Null(connector.AgentId);
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);
        Assert.False(connector.IsConnected);

        // Dispose should not throw (resources already cleaned)
        connector.Dispose();
    }

    [Fact]
    public async Task FullLifecycle_DisposeDuringConnection_CleansUpGracefully()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        Assert.Equal(ConnectionStatus.Connected, connector.Status);

        // Act - Dispose without explicit disconnect
        connector.Dispose();

        // Assert - status should be disconnected
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);
    }

    #endregion

    #region Error Handling Edge Cases

    [Fact]
    public async Task ErrorHandling_DisconnectDuringStreamWrite_HandlesGracefully()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        using var clientPipe = new AnonymousPipeClientStream(PipeDirection.Out, serverPipe.ClientSafePipeHandle);

        await SetConnectedState(connector, serverPipe);

        // Start writing output
        var writerTask = Task.Run(async () =>
        {
            using var writer = new StreamWriter(clientPipe, Encoding.UTF8, leaveOpen: true);
            for (int i = 0; i < 1000; i++)
            {
                try
                {
                    await writer.WriteLineAsync($"line {i}");
                    await writer.FlushAsync();
                    await Task.Delay(5);
                }
                catch
                {
                    break; // Pipe closed, exit gracefully
                }
            }
        });

        // Act - Disconnect while writing
        await Task.Delay(50); // Let some writes happen
        var result = await connector.DisconnectAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);

        connector.Dispose();
        await writerTask;
    }

    [Fact]
    public async Task ErrorHandling_StreamDisposedBeforeDisconnect_ReturnsSuccess()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Act - Dispose stream before disconnect
        serverPipe.Dispose();
        await Task.Delay(200); // Wait for error detection

        var result = await connector.DisconnectAsync();

        // Assert - should handle gracefully (stream already disposed)
        Assert.True(result.Success);

        connector.Dispose();
    }

    [Fact]
    public async Task ErrorHandling_CancellationDuringDisconnect_CompletesSuccessfully()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Act - Disconnect with cancellation token
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel after 100ms

        var result = await connector.DisconnectAsync(cts.Token);

        // Assert - should complete successfully despite cancellation
        Assert.True(result.Success);
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);

        connector.Dispose();
    }

    [Fact]
    public async Task ErrorHandling_DisconnectWithActiveReader_StopsReaderCleanly()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        using var clientPipe = new AnonymousPipeClientStream(PipeDirection.Out, serverPipe.ClientSafePipeHandle);

        await SetConnectedState(connector, serverPipe);

        // Start background reader consuming output
        var readerActive = true;
        var readerTask = Task.Run(async () =>
        {
            while (readerActive)
            {
                var lines = await outputBuffer.GetLastLinesAsync(10);
                await Task.Delay(10);
            }
        });

        // Act - Disconnect while reader active
        var result = await connector.DisconnectAsync();
        readerActive = false;

        // Assert
        Assert.True(result.Success);
        await readerTask;

        connector.Dispose();
    }

    [Fact]
    public async Task ErrorHandling_MultipleDisposeCalls_HandledGracefully()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Act - Disconnect
        var result = await connector.DisconnectAsync();
        Assert.True(result.Success);

        // Act - Multiple dispose calls
        connector.Dispose();
        connector.Dispose(); // Should not throw
        connector.Dispose(); // Should not throw

        // Assert - no exceptions thrown
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);
    }

    #endregion

    #region Resource Cleanup Verification

    [Fact]
    public async Task ResourceCleanup_AfterDisconnect_AllResourcesDisposed()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Act - Disconnect
        var result = await connector.DisconnectAsync();

        // Assert - verify state cleaned
        Assert.True(result.Success);
        Assert.Null(connector.AgentId);
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);
        Assert.False(connector.IsConnected);

        // Further operations should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connector.SendCommandAsync("test"));

        connector.Dispose();
    }

    [Fact]
    public async Task ResourceCleanup_DisposeWithoutDisconnect_CleansUpAllResources()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Act - Dispose without explicit disconnect
        connector.Dispose();

        // Assert - all operations should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(() => connector.DisconnectAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => connector.SendCommandAsync("test"));

        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);
    }

    [Fact]
    public async Task ResourceCleanup_DisconnectThenDispose_NoDoubleDisposal()
    {
        // Arrange
        var outputBuffer = new TestAgentOutputBuffer();
        var connector = new TerminalAgentConnector(_loggerMock.Object, outputBuffer, _optionsMock.Object);

        using var serverPipe = new AnonymousPipeServerStream(PipeDirection.In);
        await SetConnectedState(connector, serverPipe);

        // Act - Disconnect then Dispose
        var result = await connector.DisconnectAsync();
        Assert.True(result.Success);

        connector.Dispose(); // Should not throw (no double disposal)

        // Assert - verify final state
        Assert.Equal(ConnectionStatus.Disconnected, connector.Status);
        Assert.Null(connector.AgentId);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Sets connector to connected state using reflection (simulates successful connection)
    /// </summary>
    private async Task SetConnectedState(TerminalAgentConnector connector, Stream stream)
    {
        // Use reflection to set internal state for testing
        var type = typeof(TerminalAgentConnector);

        // Set _agentId
        var agentIdField = type.GetField("_agentId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        agentIdField?.SetValue(connector, "test-agent-123");

        // Set _connectionStream
        var streamField = type.GetField("_connectionStream", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        streamField?.SetValue(connector, stream);

        // Set _cancellationTokenSource
        var ctsField = type.GetField("_cancellationTokenSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cts = new CancellationTokenSource();
        ctsField?.SetValue(connector, cts);

        // Start background output reader
        var outputReaderTaskField = type.GetField("_outputReaderTask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var readMethod = type.GetMethod("ReadOutputLoopAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var readerTask = Task.Run(() => readMethod?.Invoke(connector, new object[] { stream, cts.Token }));
        outputReaderTaskField?.SetValue(connector, readerTask);

        // Set Status to Connected (will trigger event)
        var statusProperty = type.GetProperty("Status");
        statusProperty?.SetValue(connector, ConnectionStatus.Connected);

        await Task.Delay(50); // Give background task time to start
    }

    /// <summary>
    /// Test implementation of IAgentOutputBuffer
    /// </summary>
    private class TestAgentOutputBuffer : IAgentOutputBuffer
    {
        private readonly List<string> _lines = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        public int Count => _lines.Count;

        public event EventHandler<OutputLineAddedEventArgs>? LineAdded;

        public async Task AppendLineAsync(string line, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                _lines.Add(line);
                LineAdded?.Invoke(this, new OutputLineAddedEventArgs
                {
                    Line = line,
                    Timestamp = DateTime.UtcNow
                });
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IReadOnlyList<string>> GetLastLinesAsync(int count = 100, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var skip = Math.Max(0, _lines.Count - count);
                return _lines.Skip(skip).Take(count).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async IAsyncEnumerable<string> GetLinesAsync(string? regexFilter = null, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
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

        public async Task ClearAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
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

    #endregion
}
