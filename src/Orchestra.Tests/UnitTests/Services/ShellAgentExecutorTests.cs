using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestra.Agents.ClaudeCode;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests.Services;

/// <summary>
/// Unit tests for ShellAgentExecutor to verify shell command execution
/// </summary>
public class ShellAgentExecutorTests
{
    private readonly ShellAgentConfiguration _defaultConfiguration;
    private readonly ShellAgentExecutor _executor;
    private readonly string _testWorkingDirectory;

    public ShellAgentExecutorTests()
    {
        _testWorkingDirectory = Directory.GetCurrentDirectory();

        _defaultConfiguration = new ShellAgentConfiguration
        {
            ShellExecutablePath = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/bash",
            ShellArguments = OperatingSystem.IsWindows() ? "-NoProfile -ExecutionPolicy Bypass -Command" : "-c",
            DefaultWorkingDirectory = _testWorkingDirectory,
            AllowedCommands = null, // Allow all commands by default
            BlockedCommands = new string[] { }, // Block nothing by default
            MaxOutputLength = 10_000,
            DefaultTimeout = TimeSpan.FromSeconds(30),
            CaptureStdError = true,
            KillOnTimeout = true
        };

        _executor = new ShellAgentExecutor(
            Options.Create(_defaultConfiguration),
            NullLogger<ShellAgentExecutor>.Instance);
    }

    #region Basic Execution Tests

    [Fact]
    public async Task ExecuteCommandAsync_SimpleEchoCommand_ShouldReturnSuccess()
    {
        // ARRANGE
        var command = OperatingSystem.IsWindows() ? "echo 'Hello World'" : "echo Hello World";

        // ACT
        var result = await _executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.True(result.Success);
        Assert.Contains("Hello World", result.Output);
        Assert.Null(result.ErrorMessage);
        Assert.True(result.ExecutionTime.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ListDirectoryCommand_ShouldReturnSuccess()
    {
        // ARRANGE
        var command = OperatingSystem.IsWindows() ? "dir" : "ls";

        // ACT
        var result = await _executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.True(result.Success);
        Assert.NotEmpty(result.Output);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteCommandAsync_PrintWorkingDirectory_ShouldReturnCorrectPath()
    {
        // ARRANGE
        var command = OperatingSystem.IsWindows() ? "pwd" : "pwd";

        // ACT
        var result = await _executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.True(result.Success);
        Assert.Contains(_testWorkingDirectory, result.Output, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Command Validation Tests

    [Fact]
    public async Task ExecuteCommandAsync_BlockedCommand_ShouldReturnFailure()
    {
        // ARRANGE
        var blockedConfig = new ShellAgentConfiguration
        {
            BlockedCommands = new[] { "echo", "ls", "dir" }
        };
        var executor = new ShellAgentExecutor(
            Options.Create(blockedConfig),
            NullLogger<ShellAgentExecutor>.Instance);

        var command = "echo test";

        // ACT
        var result = await executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.False(result.Success);
        Assert.Contains("not allowed", result.ErrorMessage);
        Assert.True(result.ExecutionTime.TotalMilliseconds < 100); // Should be near zero
    }

    [Fact]
    public async Task ExecuteCommandAsync_AllowedCommandInWhitelist_ShouldReturnSuccess()
    {
        // ARRANGE
        var whitelistConfig = new ShellAgentConfiguration
        {
            AllowedCommands = new[] { "echo" }
        };
        var executor = new ShellAgentExecutor(
            Options.Create(whitelistConfig),
            NullLogger<ShellAgentExecutor>.Instance);

        var command = OperatingSystem.IsWindows() ? "echo 'test'" : "echo test";

        // ACT
        var result = await executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteCommandAsync_NotAllowedCommandInWhitelist_ShouldReturnFailure()
    {
        // ARRANGE
        var whitelistConfig = new ShellAgentConfiguration
        {
            AllowedCommands = new[] { "echo" }
        };
        var executor = new ShellAgentExecutor(
            Options.Create(whitelistConfig),
            NullLogger<ShellAgentExecutor>.Instance);

        var command = OperatingSystem.IsWindows() ? "dir" : "ls";

        // ACT
        var result = await executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.False(result.Success);
        Assert.Contains("not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteCommandAsync_BlockedCommandTakesPrecedenceOverWhitelist_ShouldReturnFailure()
    {
        // ARRANGE
        var config = new ShellAgentConfiguration
        {
            AllowedCommands = new[] { "echo" },
            BlockedCommands = new[] { "echo" }
        };
        var executor = new ShellAgentExecutor(
            Options.Create(config),
            NullLogger<ShellAgentExecutor>.Instance);

        var command = "echo test";

        // ACT
        var result = await executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.False(result.Success);
        Assert.Contains("not allowed", result.ErrorMessage);
    }

    #endregion

    #region Environment Variables Tests

    [Fact]
    public async Task ExecuteCommandAsync_WithCustomEnvironmentVariable_ShouldUseVariable()
    {
        // ARRANGE
        var config = new ShellAgentConfiguration
        {
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "TEST_VAR", "CustomValue123" }
            }
        };
        var executor = new ShellAgentExecutor(
            Options.Create(config),
            NullLogger<ShellAgentExecutor>.Instance);

        var command = OperatingSystem.IsWindows()
            ? "$env:TEST_VAR"
            : "echo $TEST_VAR";

        // ACT
        var result = await executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.True(result.Success);
        Assert.Contains("CustomValue123", result.Output);
    }

    #endregion

    #region Output Truncation Tests

    [Fact]
    public async Task ExecuteCommandAsync_LargeOutput_ShouldTruncate()
    {
        // ARRANGE
        var config = new ShellAgentConfiguration
        {
            MaxOutputLength = 100
        };
        var executor = new ShellAgentExecutor(
            Options.Create(config),
            NullLogger<ShellAgentExecutor>.Instance);

        // Generate large output
        var command = OperatingSystem.IsWindows()
            ? "1..50 | ForEach-Object { 'Line ' + $_ }"
            : "for i in {1..50}; do echo Line $i; done";

        // ACT
        var result = await executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.True(result.Success);
        Assert.True(result.Output.Length <= 150); // Max + truncation message
        Assert.Contains("truncated", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.True((bool)result.Metadata["OutputTruncated"]);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteCommandAsync_InvalidCommand_ShouldReturnFailure()
    {
        // ARRANGE
        var command = "this-is-not-a-valid-command-xyz123";

        // ACT
        var result = await _executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteCommandAsync_CommandWithNonZeroExitCode_ShouldReturnFailure()
    {
        // ARRANGE
        var command = OperatingSystem.IsWindows()
            ? "exit 1"
            : "exit 1";

        // ACT
        var result = await _executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.False(result.Success);
        Assert.Equal(1, (int)result.Metadata["ExitCode"]);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task ExecuteCommandAsync_Success_ShouldIncludeMetadata()
    {
        // ARRANGE
        var command = "echo test";

        // ACT
        var result = await _executor.ExecuteCommandAsync(command, _testWorkingDirectory);

        // ASSERT
        Assert.Contains("ExitCode", result.Metadata.Keys);
        Assert.Contains("WorkingDirectory", result.Metadata.Keys);
        Assert.Contains("AgentType", result.Metadata.Keys);
        Assert.Contains("ShellExecutable", result.Metadata.Keys);
        Assert.Equal("shell", result.Metadata["AgentType"]);
        Assert.Equal(_testWorkingDirectory, result.Metadata["WorkingDirectory"]);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_NullConfiguration_ShouldThrowArgumentNullException()
    {
        // ARRANGE & ACT & ASSERT
        Assert.Throws<ArgumentNullException>(() =>
            new ShellAgentExecutor(
                null!,
                NullLogger<ShellAgentExecutor>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ShouldThrowArgumentNullException()
    {
        // ARRANGE & ACT & ASSERT
        Assert.Throws<ArgumentNullException>(() =>
            new ShellAgentExecutor(
                Options.Create(_defaultConfiguration),
                null!));
    }

    #endregion

    #region AgentType Tests

    [Fact]
    public void AgentType_ShouldReturnShell()
    {
        // ARRANGE & ACT
        var agentType = _executor.AgentType;

        // ASSERT
        Assert.Equal("shell", agentType);
    }

    #endregion
}
