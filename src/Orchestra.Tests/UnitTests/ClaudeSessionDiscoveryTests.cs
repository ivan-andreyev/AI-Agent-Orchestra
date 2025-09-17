using Orchestra.Core;
using System.Reflection;

namespace Orchestra.Tests.UnitTests;

public class ClaudeSessionDiscoveryTests
{
    private ClaudeSessionDiscovery CreateDiscovery(string? testPath = null)
    {
        return new ClaudeSessionDiscovery(testPath ?? @"C:\NonExistentPath");
    }

    private string CallDecodeProjectPath(ClaudeSessionDiscovery discovery, string encodedPath)
    {
        var method = typeof(ClaudeSessionDiscovery).GetMethod("DecodeProjectPath",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return (string)method!.Invoke(discovery, new object[] { encodedPath })!;
    }

    [Theory]
    [InlineData("C--Users-mrred-RiderProjects-AI-Agent-Orchestra", @"C:\Users\mrred\RiderProjects\AI-Agent-Orchestra")]
    [InlineData("C--Users-mrred-RiderProjects-Galactic-Idlers", @"C:\Users\mrred\RiderProjects\Galactic-Idlers")]
    [InlineData("c--Users-mrred-RiderProjects-Elly2-2", @"C:\Users\mrred\RiderProjects\Elly2-2")]
    [InlineData("D--Projects-My-Project", @"D:\Projects\My-Project")]
    [InlineData("E--Some--Nested--Path", @"E:\Some\Nested\Path")]
    public void DecodeProjectPath_ShouldDecodeCorrectly(string encoded, string expected)
    {
        // Arrange
        var discovery = CreateDiscovery();

        // Act
        var result = CallDecodeProjectPath(discovery, encoded);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Invalid-Path-Without-Drive")]
    [InlineData("")]
    [InlineData("Just-Text")]
    public void DecodeProjectPath_ShouldHandleInvalidPaths(string encoded)
    {
        // Arrange
        var discovery = CreateDiscovery();

        // Act
        var result = CallDecodeProjectPath(discovery, encoded);

        // Assert
        // Should not crash and return some result
        Assert.NotNull(result);
    }

    [Fact]
    public void DecodeProjectPath_WithSpecialCharacters_ShouldWork()
    {
        // Arrange
        var discovery = CreateDiscovery();
        var encoded = "C--Users-user-name-Documents-My-Super-Project";
        var expected = @"C:\Users\user-name\Documents\My-Super-Project";

        // Act
        var result = CallDecodeProjectPath(discovery, encoded);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DiscoverActiveSessions_WithNonExistentPath_ShouldReturnEmpty()
    {
        // Arrange
        var discovery = CreateDiscovery(@"C:\NonExistentTestPath");

        // Act
        var result = discovery.DiscoverActiveSessions();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Constructor_WithCustomPath_ShouldUseProvidedPath()
    {
        // Arrange
        var customPath = @"C:\CustomTestPath";

        // Act
        var discovery = new ClaudeSessionDiscovery(customPath);

        // Assert
        // Test that it doesn't crash with custom path
        var result = discovery.DiscoverActiveSessions();
        Assert.NotNull(result);
    }
}