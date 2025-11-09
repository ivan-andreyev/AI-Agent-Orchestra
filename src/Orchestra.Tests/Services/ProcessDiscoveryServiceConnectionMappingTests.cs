using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Models;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.Services;

/// <summary>
/// Tests for ProcessDiscoveryService.GetConnectionParamsForAgentAsync mapping logic
/// </summary>
public class ProcessDiscoveryServiceConnectionMappingTests
{
    private readonly Mock<ILogger<ProcessDiscoveryService>> _mockLogger;
    private readonly IMemoryCache _cache;

    public ProcessDiscoveryServiceConnectionMappingTests()
    {
        _mockLogger = new Mock<ILogger<ProcessDiscoveryService>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task GetConnectionParamsForAgentAsync_WithValidSessionId_ReturnsConnectionParams()
    {
        // Arrange
        var service = new ProcessDiscoveryService(_mockLogger.Object, _cache);
        var sessionId = "39064667-61eb-4fa6-a460-d0ab90d5f877"; // Valid UUID from real session

        // Mock discovered processes (simulate cache)
        var processes = new List<ClaudeProcessInfo>
        {
            new ClaudeProcessInfo(
                ProcessId: 63040,
                SessionId: sessionId,
                WorkingDirectory: "C:\\Users\\mrred\\RiderProjects\\AI-Agent-Orchestra",
                SocketPath: "claude_63040",
                StartTime: DateTime.UtcNow.AddMinutes(-5))
        };

        _cache.Set("ClaudeProcesses", processes, TimeSpan.FromMinutes(2));

        // Act
        var result = await service.GetConnectionParamsForAgentAsync(sessionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(63040, result.ProcessId);
        Assert.Equal("claude_63040", result.PipeName);
        Assert.Equal("claude_63040", result.SocketPath);
    }

    [Fact]
    public async Task GetConnectionParamsForAgentAsync_WithInvalidAgentId_ReturnsNull()
    {
        // Arrange
        var service = new ProcessDiscoveryService(_mockLogger.Object, _cache);
        var invalidAgentId = "agent-68f0cc71"; // Agent ID from DB, not SessionId

        // Mock empty cache (no processes discovered)
        _cache.Set("ClaudeProcesses", new List<ClaudeProcessInfo>(), TimeSpan.FromMinutes(2));

        // Act
        var result = await service.GetConnectionParamsForAgentAsync(invalidAgentId);

        // Assert
        Assert.Null(result); // Should return null because no process found with this SessionId
    }

    [Fact]
    public async Task GetConnectionParamsForAgentAsync_WithEmptyAgentId_ReturnsNull()
    {
        // Arrange
        var service = new ProcessDiscoveryService(_mockLogger.Object, _cache);

        // Act
        var result = await service.GetConnectionParamsForAgentAsync("");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetConnectionParamsForAgentAsync_WithNullAgentId_ReturnsNull()
    {
        // Arrange
        var service = new ProcessDiscoveryService(_mockLogger.Object, _cache);

        // Act
        var result = await service.GetConnectionParamsForAgentAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetConnectionParamsForAgentAsync_WithMultipleProcessesSameSession_ReturnsFirst()
    {
        // Arrange
        var service = new ProcessDiscoveryService(_mockLogger.Object, _cache);
        var sessionId = "0d27eb88-b39c-4683-80a7-0d3ff2bd59d9";

        // Mock multiple processes with same SessionId (real scenario for Elly2)
        var processes = new List<ClaudeProcessInfo>
        {
            new ClaudeProcessInfo(
                ProcessId: 40656,
                SessionId: sessionId,
                WorkingDirectory: "C:\\Users\\mrred\\RiderProjects\\Elly2",
                SocketPath: "claude_40656",
                StartTime: DateTime.UtcNow.AddMinutes(-10)),
            new ClaudeProcessInfo(
                ProcessId: 33400,
                SessionId: sessionId,
                WorkingDirectory: "C:\\Users\\mrred\\RiderProjects\\Elly2",
                SocketPath: "claude_33400",
                StartTime: DateTime.UtcNow.AddMinutes(-5))
        };

        _cache.Set("ClaudeProcesses", processes, TimeSpan.FromMinutes(2));

        // Act
        var result = await service.GetConnectionParamsForAgentAsync(sessionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(40656, result.ProcessId); // Should return first process
        Assert.Equal("claude_40656", result.PipeName);
    }
}
