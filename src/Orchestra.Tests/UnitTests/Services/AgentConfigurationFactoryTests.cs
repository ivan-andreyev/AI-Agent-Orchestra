using Microsoft.Extensions.Logging.Abstractions;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests.Services;

/// <summary>
/// Unit tests for AgentConfigurationFactory to verify configuration management and composition
/// </summary>
public class AgentConfigurationFactoryTests
{
    private readonly AgentConfigurationRegistry _registry;
    private readonly AgentConfigurationValidator _validator;

    public AgentConfigurationFactoryTests()
    {
        _registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
        _validator = new AgentConfigurationValidator(NullLogger<AgentConfigurationValidator>.Instance);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidParameters_ShouldInitializeSuccessfully()
    {
        // ARRANGE
        var configurations = new List<IAgentConfiguration>
        {
            new TestAgentConfiguration("agent1", hasErrors: false),
            new TestAgentConfiguration("agent2", hasErrors: false)
        };

        // ACT
        var factory = new AgentConfigurationFactory(
            _registry,
            _validator,
            configurations,
            NullLogger<AgentConfigurationFactory>.Instance);

        // ASSERT
        Assert.NotNull(factory);
        Assert.Equal(2, factory.GetRegisteredTypes().Count);
    }

    [Fact]
    public void Constructor_NullRegistry_ShouldThrowArgumentNullException()
    {
        // ARRANGE
        var configurations = new List<IAgentConfiguration>();

        // ACT & ASSERT
        Assert.Throws<ArgumentNullException>(() => new AgentConfigurationFactory(
            null!,
            _validator,
            configurations,
            NullLogger<AgentConfigurationFactory>.Instance));
    }

    [Fact]
    public void Constructor_NullValidator_ShouldThrowArgumentNullException()
    {
        // ARRANGE
        var configurations = new List<IAgentConfiguration>();

        // ACT & ASSERT
        Assert.Throws<ArgumentNullException>(() => new AgentConfigurationFactory(
            _registry,
            null!,
            configurations,
            NullLogger<AgentConfigurationFactory>.Instance));
    }

    [Fact]
    public void Constructor_NullConfigurations_ShouldThrowArgumentNullException()
    {
        // ACT & ASSERT
        Assert.Throws<ArgumentNullException>(() => new AgentConfigurationFactory(
            _registry,
            _validator,
            null!,
            NullLogger<AgentConfigurationFactory>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ShouldThrowArgumentNullException()
    {
        // ARRANGE
        var configurations = new List<IAgentConfiguration>();

        // ACT & ASSERT
        Assert.Throws<ArgumentNullException>(() => new AgentConfigurationFactory(
            _registry,
            _validator,
            configurations,
            null!));
    }

    [Fact]
    public void Constructor_ConfigurationWithValidationErrors_ShouldThrowArgumentException()
    {
        // ARRANGE
        var configurations = new List<IAgentConfiguration>
        {
            new TestAgentConfiguration("valid-agent", hasErrors: false),
            new TestAgentConfiguration("invalid-agent", hasErrors: true)
        };

        // ACT & ASSERT
        var exception = Assert.Throws<ArgumentException>(() => new AgentConfigurationFactory(
            _registry,
            _validator,
            configurations,
            NullLogger<AgentConfigurationFactory>.Instance));

        Assert.Contains("validation failed", exception.Message);
        Assert.Contains("invalid-agent", exception.Message);
    }

    #endregion

    #region RegisterConfiguration Tests

    [Fact]
    public void RegisterConfiguration_ValidConfiguration_ShouldRegisterSuccessfully()
    {
        // ARRANGE
        var factory = CreateFactory();
        var configuration = new TestAgentConfiguration("new-agent", hasErrors: false);

        // ACT
        factory.RegisterConfiguration(configuration);

        // ASSERT
        Assert.True(factory.IsRegistered("new-agent"));
        var retrieved = factory.GetConfiguration("new-agent");
        Assert.NotNull(retrieved);
        Assert.Equal("new-agent", retrieved.AgentType);
    }

    [Fact]
    public void RegisterConfiguration_NullConfiguration_ShouldThrowArgumentNullException()
    {
        // ARRANGE
        var factory = CreateFactory();

        // ACT & ASSERT
        Assert.Throws<ArgumentNullException>(() => factory.RegisterConfiguration(null!));
    }

    [Fact]
    public void RegisterConfiguration_ConfigurationWithValidationErrors_ShouldThrowArgumentException()
    {
        // ARRANGE
        var factory = CreateFactory();
        var configuration = new TestAgentConfiguration("invalid-agent", hasErrors: true);

        // ACT & ASSERT
        var exception = Assert.Throws<ArgumentException>(() => factory.RegisterConfiguration(configuration));
        Assert.Contains("validation failed", exception.Message);
        Assert.Contains("invalid-agent", exception.Message);
    }

    [Fact]
    public void RegisterConfiguration_DuplicateAgentType_ShouldThrowInvalidOperationException()
    {
        // ARRANGE
        var factory = CreateFactory();
        var configuration1 = new TestAgentConfiguration("duplicate-agent", hasErrors: false);
        var configuration2 = new TestAgentConfiguration("duplicate-agent", hasErrors: false);
        factory.RegisterConfiguration(configuration1);

        // ACT & ASSERT
        Assert.Throws<InvalidOperationException>(() => factory.RegisterConfiguration(configuration2));
    }

    #endregion

    #region GetConfiguration Tests

    [Fact]
    public void GetConfiguration_ExistingAgentType_ShouldReturnConfiguration()
    {
        // ARRANGE
        var configurations = new List<IAgentConfiguration>
        {
            new TestAgentConfiguration("existing-agent", hasErrors: false)
        };
        var factory = new AgentConfigurationFactory(
            _registry,
            _validator,
            configurations,
            NullLogger<AgentConfigurationFactory>.Instance);

        // ACT
        var result = factory.GetConfiguration("existing-agent");

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal("existing-agent", result.AgentType);
    }

    [Fact]
    public void GetConfiguration_NonExistingAgentType_ShouldReturnNull()
    {
        // ARRANGE
        var factory = CreateFactory();

        // ACT
        var result = factory.GetConfiguration("non-existing");

        // ASSERT
        Assert.Null(result);
    }

    #endregion

    #region GetRegisteredTypes Tests

    [Fact]
    public void GetRegisteredTypes_EmptyFactory_ShouldReturnEmptyCollection()
    {
        // ARRANGE
        var factory = CreateFactory();

        // ACT
        var result = factory.GetRegisteredTypes();

        // ASSERT
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetRegisteredTypes_MultipleRegistrations_ShouldReturnAllTypes()
    {
        // ARRANGE
        var configurations = new List<IAgentConfiguration>
        {
            new TestAgentConfiguration("agent1", hasErrors: false),
            new TestAgentConfiguration("agent2", hasErrors: false),
            new TestAgentConfiguration("agent3", hasErrors: false)
        };
        var factory = new AgentConfigurationFactory(
            _registry,
            _validator,
            configurations,
            NullLogger<AgentConfigurationFactory>.Instance);

        // ACT
        var result = factory.GetRegisteredTypes();

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains("agent1", result);
        Assert.Contains("agent2", result);
        Assert.Contains("agent3", result);
    }

    #endregion

    #region IsRegistered Tests

    [Fact]
    public void IsRegistered_ExistingAgentType_ShouldReturnTrue()
    {
        // ARRANGE
        var configurations = new List<IAgentConfiguration>
        {
            new TestAgentConfiguration("registered-agent", hasErrors: false)
        };
        var factory = new AgentConfigurationFactory(
            _registry,
            _validator,
            configurations,
            NullLogger<AgentConfigurationFactory>.Instance);

        // ACT
        var result = factory.IsRegistered("registered-agent");

        // ASSERT
        Assert.True(result);
    }

    [Fact]
    public void IsRegistered_NonExistingAgentType_ShouldReturnFalse()
    {
        // ARRANGE
        var factory = CreateFactory();

        // ACT
        var result = factory.IsRegistered("non-existing");

        // ASSERT
        Assert.False(result);
    }

    #endregion

    #region ValidateAll Tests

    [Fact]
    public void ValidateAll_AllValidConfigurations_ShouldReturnEmptyErrorLists()
    {
        // ARRANGE
        var configurations = new List<IAgentConfiguration>
        {
            new TestAgentConfiguration("agent1", hasErrors: false),
            new TestAgentConfiguration("agent2", hasErrors: false)
        };
        var factory = new AgentConfigurationFactory(
            _registry,
            _validator,
            configurations,
            NullLogger<AgentConfigurationFactory>.Instance);

        // ACT
        var result = factory.ValidateAll();

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result.Values, errors => Assert.Empty(errors));
    }

    #endregion

    #region Test Helper Methods

    private AgentConfigurationFactory CreateFactory()
    {
        return new AgentConfigurationFactory(
            _registry,
            _validator,
            new List<IAgentConfiguration>(),
            NullLogger<AgentConfigurationFactory>.Instance);
    }

    #endregion

    #region Test Helper Classes

    /// <summary>
    /// Test configuration for unit testing purposes
    /// </summary>
    private class TestAgentConfiguration : IAgentConfiguration
    {
        private readonly bool _hasErrors;

        public string AgentType { get; }
        public int MaxConcurrentExecutions { get; set; } = 3;
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(10);
        public int RetryAttempts { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

        public TestAgentConfiguration(string agentType, bool hasErrors)
        {
            AgentType = agentType;
            _hasErrors = hasErrors;
        }

        public IReadOnlyList<string> Validate()
        {
            if (_hasErrors)
            {
                return new List<string> { "Error 1", "Error 2" };
            }

            return new List<string>();
        }
    }

    #endregion
}
