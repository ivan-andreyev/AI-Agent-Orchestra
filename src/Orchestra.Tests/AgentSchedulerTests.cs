using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core;

namespace Orchestra.Tests;

public class AgentSchedulerTests
{
    private Mock<ILogger<AgentScheduler>> CreateMockLogger()
    {
        return new Mock<ILogger<AgentScheduler>>();
    }

    private AgentConfiguration CreateTestConfiguration()
    {
        return new AgentConfiguration
        {
            PingIntervalSeconds = 1, // Fast for testing
            Agents = new List<ConfiguredAgent>
            {
                new("test-agent-1", "Test Agent 1", "claude-code", @"C:\Repo1", true),
                new("test-agent-2", "Test Agent 2", "claude-code", @"C:\Repo2", true),
                new("disabled-agent", "Disabled Agent", "claude-code", @"C:\Repo3", false)
            }
        };
    }

    [Fact]
    public void AgentConfiguration_LoadFromFile_ShouldCreateDefaultWhenFileNotExists()
    {
        // Arrange
        var nonExistentPath = "non-existent-config.json";

        // Act
        var config = AgentConfiguration.LoadFromFile(nonExistentPath);

        // Assert
        Assert.NotNull(config);
        Assert.True(config.PingIntervalSeconds > 0);
        Assert.NotEmpty(config.Agents);
    }

    [Fact]
    public void ConfiguredAgent_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var agent = new ConfiguredAgent("id", "name", "type", "path", true);

        // Assert
        Assert.Equal("id", agent.Id);
        Assert.Equal("name", agent.Name);
        Assert.Equal("type", agent.Type);
        Assert.Equal("path", agent.RepositoryPath);
        Assert.True(agent.Enabled);
    }

    [Fact]
    public void AgentConfiguration_ShouldFilterEnabledAgents()
    {
        // Arrange
        var config = CreateTestConfiguration();

        // Act
        var enabledAgents = config.Agents.Where(a => a.Enabled).ToList();

        // Assert
        Assert.Equal(2, enabledAgents.Count);
        Assert.DoesNotContain(enabledAgents, a => a.Id == "disabled-agent");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(60)]
    public void AgentConfiguration_ShouldSupportDifferentPingIntervals(int intervalSeconds)
    {
        // Arrange & Act
        var config = new AgentConfiguration
        {
            PingIntervalSeconds = intervalSeconds,
            Agents = new List<ConfiguredAgent>()
        };

        // Assert
        Assert.Equal(intervalSeconds, config.PingIntervalSeconds);
    }

    [Fact]
    public void AgentConfiguration_ShouldHandleEmptyAgentsList()
    {
        // Arrange & Act
        var config = new AgentConfiguration
        {
            PingIntervalSeconds = 30,
            Agents = new List<ConfiguredAgent>()
        };

        // Assert
        Assert.NotNull(config.Agents);
        Assert.Empty(config.Agents);
    }

    [Fact]
    public void AgentConfiguration_ShouldSupportMultipleAgentTypes()
    {
        // Arrange
        var agents = new List<ConfiguredAgent>
        {
            new("claude-1", "Claude Agent", "claude-code", @"C:\Repo1", true),
            new("copilot-1", "Copilot Agent", "github-copilot", @"C:\Repo2", true),
            new("custom-1", "Custom Agent", "custom-ai", @"C:\Repo3", true)
        };

        // Act
        var config = new AgentConfiguration
        {
            PingIntervalSeconds = 30,
            Agents = agents
        };

        // Assert
        var agentTypes = config.Agents.Select(a => a.Type).Distinct().ToList();
        Assert.Equal(3, agentTypes.Count);
        Assert.Contains("claude-code", agentTypes);
        Assert.Contains("github-copilot", agentTypes);
        Assert.Contains("custom-ai", agentTypes);
    }

    [Fact]
    public void AgentConfiguration_ShouldSupportDifferentRepositoryPaths()
    {
        // Arrange
        var agents = new List<ConfiguredAgent>
        {
            new("agent-1", "Agent 1", "claude-code", @"C:\Projects\Frontend", true),
            new("agent-2", "Agent 2", "claude-code", @"C:\Projects\Backend", true),
            new("agent-3", "Agent 3", "claude-code", @"D:\Repositories\Utils", true)
        };

        // Act
        var config = new AgentConfiguration
        {
            PingIntervalSeconds = 30,
            Agents = agents
        };

        // Assert
        Assert.Equal(3, config.Agents.Count);
        Assert.All(config.Agents, agent => Assert.False(string.IsNullOrEmpty(agent.RepositoryPath)));

        var paths = config.Agents.Select(a => a.RepositoryPath).ToList();
        Assert.Contains(@"C:\Projects\Frontend", paths);
        Assert.Contains(@"C:\Projects\Backend", paths);
        Assert.Contains(@"D:\Repositories\Utils", paths);
    }

    [Fact]
    public void ConfiguredAgent_ShouldBeImmutable()
    {
        // Arrange
        var originalAgent = new ConfiguredAgent("id", "name", "type", "path", true);

        // Act & Assert
        // Records are immutable by default, this test verifies the concept
        Assert.Equal("id", originalAgent.Id);
        Assert.Equal("name", originalAgent.Name);
        Assert.Equal("type", originalAgent.Type);
        Assert.Equal("path", originalAgent.RepositoryPath);
        Assert.True(originalAgent.Enabled);

        // You cannot modify properties of a record directly
        // This would not compile: originalAgent.Id = "new-id";
    }

    [Fact]
    public void AgentConfiguration_ShouldValidateAgentIds()
    {
        // Arrange
        var config = CreateTestConfiguration();

        // Act
        var agentIds = config.Agents.Select(a => a.Id).ToList();

        // Assert
        Assert.Equal(agentIds.Count, agentIds.Distinct().Count()); // All IDs should be unique
        Assert.All(agentIds, id => Assert.False(string.IsNullOrWhiteSpace(id)));
    }
}