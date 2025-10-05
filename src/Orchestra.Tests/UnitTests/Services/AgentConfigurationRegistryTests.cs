using Microsoft.Extensions.Logging.Abstractions;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests.Services;

/// <summary>
/// Unit tests for AgentConfigurationRegistry to verify configuration storage and retrieval
/// </summary>
public class AgentConfigurationRegistryTests
{
    private readonly AgentConfigurationRegistry _registry;

    public AgentConfigurationRegistryTests()
    {
        _registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
    }

    #region Register Tests

    [Fact]
    public void Register_ValidConfiguration_ShouldRegisterSuccessfully()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration("test-agent");

        // ACT
        _registry.Register(configuration);

        // ASSERT
        Assert.True(_registry.IsRegistered("test-agent"));
        var retrieved = _registry.Get("test-agent");
        Assert.NotNull(retrieved);
        Assert.Equal("test-agent", retrieved.AgentType);
    }

    [Fact]
    public void Register_NullConfiguration_ShouldThrowArgumentNullException()
    {
        // ARRANGE
        IAgentConfiguration configuration = null!;

        // ACT & ASSERT
        Assert.Throws<ArgumentNullException>(() => _registry.Register(configuration));
    }

    [Fact]
    public void Register_ConfigurationWithEmptyAgentType_ShouldThrowArgumentException()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration("");

        // ACT & ASSERT
        var exception = Assert.Throws<ArgumentException>(() => _registry.Register(configuration));
        Assert.Contains("empty AgentType", exception.Message);
    }

    [Fact]
    public void Register_DuplicateAgentType_ShouldThrowInvalidOperationException()
    {
        // ARRANGE
        var configuration1 = new TestAgentConfiguration("duplicate-agent");
        var configuration2 = new TestAgentConfiguration("duplicate-agent");
        _registry.Register(configuration1);

        // ACT & ASSERT
        var exception = Assert.Throws<InvalidOperationException>(() => _registry.Register(configuration2));
        Assert.Contains("already registered", exception.Message);
        Assert.Contains("duplicate-agent", exception.Message);
    }

    [Fact]
    public void Register_CaseInsensitiveAgentType_ShouldThrowInvalidOperationException()
    {
        // ARRANGE
        var configuration1 = new TestAgentConfiguration("test-agent");
        var configuration2 = new TestAgentConfiguration("TEST-AGENT");
        _registry.Register(configuration1);

        // ACT & ASSERT
        var exception = Assert.Throws<InvalidOperationException>(() => _registry.Register(configuration2));
        Assert.Contains("already registered", exception.Message);
    }

    #endregion

    #region Get Tests

    [Fact]
    public void Get_ExistingAgentType_ShouldReturnConfiguration()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration("existing-agent");
        _registry.Register(configuration);

        // ACT
        var result = _registry.Get("existing-agent");

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal("existing-agent", result.AgentType);
    }

    [Fact]
    public void Get_CaseInsensitiveAgentType_ShouldReturnConfiguration()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration("case-agent");
        _registry.Register(configuration);

        // ACT
        var result = _registry.Get("CASE-AGENT");

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal("case-agent", result.AgentType);
    }

    [Fact]
    public void Get_NonExistingAgentType_ShouldReturnNull()
    {
        // ARRANGE & ACT
        var result = _registry.Get("non-existing");

        // ASSERT
        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Get_EmptyOrNullAgentType_ShouldReturnNull(string? agentType)
    {
        // ARRANGE & ACT
        var result = _registry.Get(agentType!);

        // ASSERT
        Assert.Null(result);
    }

    #endregion

    #region IsRegistered Tests

    [Fact]
    public void IsRegistered_ExistingAgentType_ShouldReturnTrue()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration("registered-agent");
        _registry.Register(configuration);

        // ACT
        var result = _registry.IsRegistered("registered-agent");

        // ASSERT
        Assert.True(result);
    }

    [Fact]
    public void IsRegistered_CaseInsensitiveAgentType_ShouldReturnTrue()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration("case-agent");
        _registry.Register(configuration);

        // ACT
        var result = _registry.IsRegistered("CASE-AGENT");

        // ASSERT
        Assert.True(result);
    }

    [Fact]
    public void IsRegistered_NonExistingAgentType_ShouldReturnFalse()
    {
        // ARRANGE & ACT
        var result = _registry.IsRegistered("non-existing");

        // ASSERT
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsRegistered_EmptyOrNullAgentType_ShouldReturnFalse(string? agentType)
    {
        // ARRANGE & ACT
        var result = _registry.IsRegistered(agentType!);

        // ASSERT
        Assert.False(result);
    }

    #endregion

    #region GetRegisteredTypes Tests

    [Fact]
    public void GetRegisteredTypes_EmptyRegistry_ShouldReturnEmptyCollection()
    {
        // ARRANGE & ACT
        var result = _registry.GetRegisteredTypes();

        // ASSERT
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetRegisteredTypes_MultipleRegistrations_ShouldReturnAllTypes()
    {
        // ARRANGE
        _registry.Register(new TestAgentConfiguration("agent1"));
        _registry.Register(new TestAgentConfiguration("agent2"));
        _registry.Register(new TestAgentConfiguration("agent3"));

        // ACT
        var result = _registry.GetRegisteredTypes();

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains("agent1", result);
        Assert.Contains("agent2", result);
        Assert.Contains("agent3", result);
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public void GetAll_EmptyRegistry_ShouldReturnEmptyCollection()
    {
        // ARRANGE & ACT
        var result = _registry.GetAll();

        // ASSERT
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetAll_MultipleRegistrations_ShouldReturnAllConfigurations()
    {
        // ARRANGE
        var config1 = new TestAgentConfiguration("agent1");
        var config2 = new TestAgentConfiguration("agent2");
        var config3 = new TestAgentConfiguration("agent3");
        _registry.Register(config1);
        _registry.Register(config2);
        _registry.Register(config3);

        // ACT
        var result = _registry.GetAll();

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains(config1, result);
        Assert.Contains(config2, result);
        Assert.Contains(config3, result);
    }

    #endregion

    #region Test Helper Classes

    /// <summary>
    /// Test configuration for unit testing purposes
    /// </summary>
    private class TestAgentConfiguration : IAgentConfiguration
    {
        public string AgentType { get; }
        public int MaxConcurrentExecutions { get; set; } = 3;
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(10);
        public int RetryAttempts { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

        public TestAgentConfiguration(string agentType)
        {
            AgentType = agentType;
        }

        public IReadOnlyList<string> Validate()
        {
            return new List<string>();
        }
    }

    #endregion
}
