using Microsoft.Extensions.Logging.Abstractions;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests.Services;

/// <summary>
/// Unit tests for AgentConfigurationValidator to verify configuration validation logic
/// </summary>
public class AgentConfigurationValidatorTests
{
    private readonly AgentConfigurationValidator _validator;

    public AgentConfigurationValidatorTests()
    {
        _validator = new AgentConfigurationValidator(NullLogger<AgentConfigurationValidator>.Instance);
    }

    #region Validate Tests

    [Fact]
    public void Validate_NullConfiguration_ShouldReturnError()
    {
        // ARRANGE
        IAgentConfiguration configuration = null!;

        // ACT
        var result = _validator.Validate(configuration);

        // ASSERT
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Contains("cannot be null", result[0]);
    }

    [Fact]
    public void Validate_ValidConfiguration_ShouldReturnEmptyList()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration("test-agent", hasErrors: false);

        // ACT
        var result = _validator.Validate(configuration);

        // ASSERT
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_ConfigurationWithErrors_ShouldReturnErrors()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration("test-agent", hasErrors: true);

        // ACT
        var result = _validator.Validate(configuration);

        // ASSERT
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(2, result.Count);
        Assert.Contains("Error 1", result);
        Assert.Contains("Error 2", result);
    }

    #endregion

    #region ValidateAll Tests

    [Fact]
    public void ValidateAll_NullRegistry_ShouldThrowArgumentNullException()
    {
        // ARRANGE
        IAgentConfigurationRegistry registry = null!;

        // ACT & ASSERT
        Assert.Throws<ArgumentNullException>(() => _validator.ValidateAll(registry));
    }

    [Fact]
    public void ValidateAll_EmptyRegistry_ShouldReturnEmptyDictionary()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);

        // ACT
        var result = _validator.ValidateAll(registry);

        // ASSERT
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateAll_AllValidConfigurations_ShouldReturnEmptyErrorLists()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
        registry.Register(new TestAgentConfiguration("agent1", hasErrors: false));
        registry.Register(new TestAgentConfiguration("agent2", hasErrors: false));
        registry.Register(new TestAgentConfiguration("agent3", hasErrors: false));

        // ACT
        var result = _validator.ValidateAll(registry);

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.All(result.Values, errors => Assert.Empty(errors));
    }

    [Fact]
    public void ValidateAll_MixedConfigurations_ShouldReturnCorrectErrors()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
        registry.Register(new TestAgentConfiguration("valid-agent", hasErrors: false));
        registry.Register(new TestAgentConfiguration("invalid-agent", hasErrors: true));
        registry.Register(new TestAgentConfiguration("another-valid", hasErrors: false));

        // ACT
        var result = _validator.ValidateAll(registry);

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        Assert.Empty(result["valid-agent"]);
        Assert.NotEmpty(result["invalid-agent"]);
        Assert.Equal(2, result["invalid-agent"].Count);
        Assert.Empty(result["another-valid"]);
    }

    [Fact]
    public void ValidateAll_AllInvalidConfigurations_ShouldReturnAllErrors()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
        registry.Register(new TestAgentConfiguration("invalid1", hasErrors: true));
        registry.Register(new TestAgentConfiguration("invalid2", hasErrors: true));

        // ACT
        var result = _validator.ValidateAll(registry);

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result.Values, errors => Assert.NotEmpty(errors));
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
