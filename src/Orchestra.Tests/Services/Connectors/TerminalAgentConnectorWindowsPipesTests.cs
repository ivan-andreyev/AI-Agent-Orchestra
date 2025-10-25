using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchestra.Core.Models;
using Orchestra.Core.Services.Connectors;
using Xunit;
using System.IO.Pipes;
using System.Reflection;

namespace Orchestra.Tests.Services.Connectors;

/// <summary>
/// Тесты для Windows Named Pipes функциональности в TerminalAgentConnector
/// </summary>
/// <remarks>
/// Тестирует специфичную для Windows функциональность:
/// - ConnectWindowsNamedPipeAsync method
/// - IsValidWindowsPipeName validation
/// - Pipe connection error handling
/// - Timeout handling
/// - Resource cleanup
///
/// NOTE: Эти тесты фокусируются на Task 1.2B.1 (Windows Named Pipes Implementation)
/// </remarks>
public class TerminalAgentConnectorWindowsPipesTests : IDisposable
{
    private readonly Mock<ILogger<TerminalAgentConnector>> _loggerMock;
    private readonly Mock<IAgentOutputBuffer> _outputBufferMock;
    private readonly Mock<IOptions<TerminalConnectorOptions>> _optionsMock;
    private readonly TerminalAgentConnector _connector;

    public TerminalAgentConnectorWindowsPipesTests()
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

    #region IsValidWindowsPipeName Tests

    [Theory]
    [InlineData("orchestra_agent_pipe", true)]
    [InlineData("agent-123", true)]
    [InlineData("claude_code_session", true)]
    [InlineData("MyPipe", true)]
    [InlineData("pipe.name.with.dots", true)]
    [InlineData("pipe-name-with-dashes", true)]
    [InlineData("pipe_name_with_underscores", true)]
    [InlineData("PipeWithMixedCase123", true)]
    public void IsValidWindowsPipeName_WithValidNames_ReturnsTrue(string pipeName, bool expected)
    {
        // Act
        var isValid = InvokeIsValidWindowsPipeName(pipeName);

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsValidWindowsPipeName_WithEmptyOrNullNames_ReturnsFalse(string pipeName, bool expected)
    {
        // Act
        var isValid = InvokeIsValidWindowsPipeName(pipeName);

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Theory]
    [InlineData("pipe\\name", false)]
    [InlineData("\\pipe", false)]
    [InlineData("pipe\\", false)]
    [InlineData("\\\\pipe\\name", false)]
    public void IsValidWindowsPipeName_WithBackslashes_ReturnsFalse(string pipeName, bool expected)
    {
        // Act
        var isValid = InvokeIsValidWindowsPipeName(pipeName);

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Fact]
    public void IsValidWindowsPipeName_WithTooLongName_ReturnsFalse()
    {
        // Arrange - create a string of exactly 256 characters
        var pipeName = new string('a', 256);

        // Act
        var isValid = InvokeIsValidWindowsPipeName(pipeName);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValidWindowsPipeName_WithMaxValidLength_ReturnsTrue()
    {
        // Arrange - create a string of 255 characters (max valid)
        var pipeName = new string('a', 255);

        // Act
        var isValid = InvokeIsValidWindowsPipeName(pipeName);

        // Assert
        Assert.True(isValid);
    }

    #endregion

    #region ConnectWindowsNamedPipeAsync Tests

    [Fact]
    public async Task ConnectWindowsNamedPipeAsync_WithInvalidPipeName_ThrowsArgumentException()
    {
        // Arrange
        var invalidPipeName = "pipe\\with\\backslashes";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await InvokeConnectWindowsNamedPipeAsync(invalidPipeName));

        Assert.Contains("Invalid pipe name", exception.Message);
        Assert.Contains(invalidPipeName, exception.Message);
        Assert.Equal("pipeName", exception.ParamName);
    }

    [Fact]
    public async Task ConnectWindowsNamedPipeAsync_WithEmptyPipeName_ThrowsArgumentException()
    {
        // Arrange
        var emptyPipeName = "";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await InvokeConnectWindowsNamedPipeAsync(emptyPipeName));

        Assert.Contains("Invalid pipe name", exception.Message);
        Assert.Equal("pipeName", exception.ParamName);
    }

    [Fact]
    public async Task ConnectWindowsNamedPipeAsync_WithTooLongPipeName_ThrowsArgumentException()
    {
        // Arrange
        var tooLongPipeName = new string('a', 256);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await InvokeConnectWindowsNamedPipeAsync(tooLongPipeName));

        Assert.Contains("Invalid pipe name", exception.Message);
        Assert.Contains("less than 256 characters", exception.Message);
    }

    [Fact]
    public async Task ConnectWindowsNamedPipeAsync_WithNonExistentPipe_ThrowsTimeoutOrCancellationException()
    {
        // Arrange
        var nonExistentPipeName = "orchestra_test_nonexistent_pipe_" + Guid.NewGuid().ToString("N");

        // Act & Assert - expect either timeout or cancellation (depending on timing)
        // We use a shorter timeout in the test to avoid long waits
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await InvokeConnectWindowsNamedPipeAsync(nonExistentPipeName, cts.Token);
        });

        // Verify logger was called with error (either Timeout or IO error)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ConnectWindowsNamedPipeAsync_Cancellation_PropagatesCancellation()
    {
        // Arrange
        var pipeName = "orchestra_test_cancellation_" + Guid.NewGuid().ToString("N");
        var cts = new CancellationTokenSource();

        // Cancel immediately
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await InvokeConnectWindowsNamedPipeAsync(pipeName, cts.Token));
    }

    [Fact]
    public void ConnectWindowsNamedPipeAsync_LogsDebugOnConnectionAttempt()
    {
        // Arrange
        var pipeName = "test_pipe_logging";

        // Act - start connection (will fail/timeout, but we check logging)
        var task = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                await InvokeConnectWindowsNamedPipeAsync(pipeName, cts.Token);
            }
            catch
            {
                // Expected to fail - we're just checking logging
            }
        });

        task.Wait(TimeSpan.FromSeconds(5));

        // Assert - verify debug log was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Connecting to Windows Named Pipe")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region TerminalConnectorOptions Tests

    [Fact]
    public void TerminalConnectorOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new TerminalConnectorOptions();

        // Assert
        Assert.True(options.UseUnixSockets);
        Assert.True(options.UseNamedPipes);
        Assert.Equal(30000, options.ConnectionTimeoutMs);
        Assert.Null(options.DefaultSocketPath);
        Assert.Null(options.DefaultPipeName);
    }

    [Fact]
    public void TerminalConnectorOptions_Validate_WithValidSettings_ReturnsNoErrors()
    {
        // Arrange
        var options = new TerminalConnectorOptions
        {
            UseUnixSockets = true,
            UseNamedPipes = true,
            ConnectionTimeoutMs = 30000,
            DefaultSocketPath = "/tmp/orchestra.sock",
            DefaultPipeName = "orchestra_pipe"
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void TerminalConnectorOptions_Validate_WithBothMethodsDisabled_ReturnsError()
    {
        // Arrange
        var options = new TerminalConnectorOptions
        {
            UseUnixSockets = false,
            UseNamedPipes = false
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("At least one connection method must be enabled"));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(999)]
    [InlineData(300001)]
    [InlineData(500000)]
    public void TerminalConnectorOptions_Validate_WithInvalidTimeout_ReturnsError(int timeoutMs)
    {
        // Arrange
        var options = new TerminalConnectorOptions
        {
            ConnectionTimeoutMs = timeoutMs
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("ConnectionTimeoutMs must be between 1000 and 300000"));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(30000)]
    [InlineData(60000)]
    [InlineData(300000)]
    public void TerminalConnectorOptions_Validate_WithValidTimeout_ReturnsNoTimeoutError(int timeoutMs)
    {
        // Arrange
        var options = new TerminalConnectorOptions
        {
            ConnectionTimeoutMs = timeoutMs
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.DoesNotContain(errors, e => e.Contains("ConnectionTimeoutMs"));
    }

    [Fact]
    public void TerminalConnectorOptions_Validate_WithBackslashInPipeName_ReturnsError()
    {
        // Arrange
        var options = new TerminalConnectorOptions
        {
            DefaultPipeName = "pipe\\with\\backslash"
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("DefaultPipeName cannot contain backslashes"));
    }

    [Fact]
    public void TerminalConnectorOptions_Validate_WithTooLongPipeName_ReturnsError()
    {
        // Arrange
        var options = new TerminalConnectorOptions
        {
            DefaultPipeName = new string('a', 256)
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("DefaultPipeName must be less than 256 characters"));
    }

    #endregion

    #region Helper Methods - Reflection-based access to private methods

    /// <summary>
    /// Вызывает приватный метод IsValidWindowsPipeName через рефлексию
    /// </summary>
    private bool InvokeIsValidWindowsPipeName(string pipeName)
    {
        var method = typeof(TerminalAgentConnector).GetMethod(
            "IsValidWindowsPipeName",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        var result = method.Invoke(_connector, new object[] { pipeName });
        return (bool)result!;
    }

    /// <summary>
    /// Вызывает приватный метод ConnectWindowsNamedPipeAsync через рефлексию
    /// </summary>
    private async Task<Stream> InvokeConnectWindowsNamedPipeAsync(
        string pipeName,
        CancellationToken cancellationToken = default)
    {
        var method = typeof(TerminalAgentConnector).GetMethod(
            "ConnectWindowsNamedPipeAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        var task = (Task<Stream>)method.Invoke(_connector, new object[] { pipeName, cancellationToken })!;
        return await task;
    }

    #endregion
}
