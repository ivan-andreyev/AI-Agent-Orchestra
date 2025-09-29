using Orchestra.Tests.Integration.Mocks;
using Xunit;

namespace Orchestra.Tests.UnitTests;

/// <summary>
/// Unit tests for MockAgentExecutor to verify mock functionality works correctly
/// </summary>
public class MockAgentExecutorTests
{
    [Fact]
    public async Task ExecuteCommandAsync_EchoCommand_ShouldReturnExpectedOutput()
    {
        // ARRANGE
        var mockExecutor = new MockAgentExecutor();
        var command = "echo 'Hello World'";
        var workingDirectory = @"C:\TestDir";

        // ACT
        var result = await mockExecutor.ExecuteCommandAsync(command, workingDirectory);

        // ASSERT
        Assert.True(result.Success);
        Assert.Equal("Hello World", result.Output);
        Assert.True(result.ExecutionTime.TotalMilliseconds > 0);
        Assert.Contains("WorkingDirectory", result.Metadata.Keys);
        Assert.Equal(workingDirectory, result.Metadata["WorkingDirectory"]);
    }

    [Fact]
    public async Task ExecuteCommandAsync_FailCommand_ShouldReturnFailure()
    {
        // ARRANGE
        var mockExecutor = new MockAgentExecutor();
        var command = "fail 'Simulated failure'";
        var workingDirectory = @"C:\TestDir";

        // ACT
        var result = await mockExecutor.ExecuteCommandAsync(command, workingDirectory);

        // ASSERT
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("failure", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteCommandAsync_SlowCommand_ShouldTakeTime()
    {
        // ARRANGE
        var mockExecutor = new MockAgentExecutor();
        var command = "slow 'Long running task'";
        var workingDirectory = @"C:\TestDir";

        // ACT
        var startTime = DateTime.UtcNow;
        var result = await mockExecutor.ExecuteCommandAsync(command, workingDirectory);
        var elapsed = DateTime.UtcNow - startTime;

        // ASSERT
        Assert.True(result.Success);
        Assert.Contains("Slow command completed", result.Output);
        Assert.True(elapsed.TotalMilliseconds >= 1000); // Should take at least 1 second
    }

    [Theory]
    [InlineData("ls", "file1.txt")]
    [InlineData("pwd", "TestDir")]
    [InlineData("git status", "working tree clean")]
    public async Task ExecuteCommandAsync_VariousCommands_ShouldReturnAppropriateOutput(string command, string expectedSubstring)
    {
        // ARRANGE
        var mockExecutor = new MockAgentExecutor();
        var workingDirectory = @"C:\TestDir";

        // ACT
        var result = await mockExecutor.ExecuteCommandAsync(command, workingDirectory);

        // ASSERT
        Assert.True(result.Success);
        Assert.Contains(expectedSubstring, result.Output, StringComparison.OrdinalIgnoreCase);
    }
}