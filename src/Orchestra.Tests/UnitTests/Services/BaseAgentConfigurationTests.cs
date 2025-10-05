using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests.Services;

/// <summary>
/// Unit tests for BaseAgentConfiguration to verify common validation logic
/// </summary>
public class BaseAgentConfigurationTests
{
    #region Common Properties Validation Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(200)]
    public void Validate_InvalidMaxConcurrentExecutions_ShouldReturnError(int maxConcurrent)
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration
        {
            MaxConcurrentExecutions = maxConcurrent
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.NotEmpty(result);
        Assert.Contains(result, error => error.Contains("MaxConcurrentExecutions"));
        Assert.Contains(result, error => error.Contains("between 1 and 100"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_ValidMaxConcurrentExecutions_ShouldNotReturnError(int maxConcurrent)
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration
        {
            MaxConcurrentExecutions = maxConcurrent
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.DoesNotContain(result, error => error.Contains("MaxConcurrentExecutions"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    public void Validate_TooShortDefaultTimeout_ShouldReturnError(int seconds)
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration
        {
            DefaultTimeout = TimeSpan.FromSeconds(seconds)
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.NotEmpty(result);
        Assert.Contains(result, error => error.Contains("DefaultTimeout"));
        Assert.Contains(result, error => error.Contains("at least 10 seconds"));
    }

    [Fact]
    public void Validate_TooLongDefaultTimeout_ShouldReturnError()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration
        {
            DefaultTimeout = TimeSpan.FromHours(2)
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.NotEmpty(result);
        Assert.Contains(result, error => error.Contains("DefaultTimeout"));
        Assert.Contains(result, error => error.Contains("should not exceed 1 hour"));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(60)]
    [InlineData(600)]
    [InlineData(3600)]
    public void Validate_ValidDefaultTimeout_ShouldNotReturnError(int seconds)
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration
        {
            DefaultTimeout = TimeSpan.FromSeconds(seconds)
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.DoesNotContain(result, error => error.Contains("DefaultTimeout"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(20)]
    public void Validate_InvalidRetryAttempts_ShouldReturnError(int retryAttempts)
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration
        {
            RetryAttempts = retryAttempts
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.NotEmpty(result);
        Assert.Contains(result, error => error.Contains("RetryAttempts"));
        Assert.Contains(result, error => error.Contains("between 0 and 10"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void Validate_ValidRetryAttempts_ShouldNotReturnError(int retryAttempts)
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration
        {
            RetryAttempts = retryAttempts
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.DoesNotContain(result, error => error.Contains("RetryAttempts"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(99)]
    public void Validate_TooShortRetryDelay_ShouldReturnError(int milliseconds)
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration
        {
            RetryDelay = TimeSpan.FromMilliseconds(milliseconds)
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.NotEmpty(result);
        Assert.Contains(result, error => error.Contains("RetryDelay"));
        Assert.Contains(result, error => error.Contains("at least 100 milliseconds"));
    }

    [Fact]
    public void Validate_TooLongRetryDelay_ShouldReturnError()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration
        {
            RetryDelay = TimeSpan.FromSeconds(35)
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.NotEmpty(result);
        Assert.Contains(result, error => error.Contains("RetryDelay"));
        Assert.Contains(result, error => error.Contains("should not exceed 30 seconds"));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(30000)]
    public void Validate_ValidRetryDelay_ShouldNotReturnError(int milliseconds)
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration
        {
            RetryDelay = TimeSpan.FromMilliseconds(milliseconds)
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.DoesNotContain(result, error => error.Contains("RetryDelay"));
    }

    #endregion

    #region Template Method Pattern Tests

    [Fact]
    public void Validate_DefaultConfiguration_ShouldReturnNoErrors()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration();

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_ConfigurationWithSpecificErrors_ShouldIncludeBothCommonAndSpecificErrors()
    {
        // ARRANGE
        var configuration = new TestAgentConfigurationWithSpecificValidation
        {
            MaxConcurrentExecutions = 0, // Common error
            SpecificProperty = "" // Specific error
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.NotEmpty(result);
        Assert.Contains(result, error => error.Contains("MaxConcurrentExecutions"));
        Assert.Contains(result, error => error.Contains("SpecificProperty"));
    }

    [Fact]
    public void Validate_ConfigurationWithOnlySpecificErrors_ShouldReturnOnlySpecificErrors()
    {
        // ARRANGE
        var configuration = new TestAgentConfigurationWithSpecificValidation
        {
            SpecificProperty = ""
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.Single(result);
        Assert.Contains(result, error => error.Contains("SpecificProperty"));
    }

    [Fact]
    public void Validate_MultipleCommonErrors_ShouldReturnAllErrors()
    {
        // ARRANGE
        var configuration = new TestAgentConfiguration
        {
            MaxConcurrentExecutions = 0,
            DefaultTimeout = TimeSpan.FromSeconds(5),
            RetryAttempts = 15,
            RetryDelay = TimeSpan.FromMilliseconds(50)
        };

        // ACT
        var result = configuration.Validate();

        // ASSERT
        Assert.Equal(4, result.Count);
        Assert.Contains(result, error => error.Contains("MaxConcurrentExecutions"));
        Assert.Contains(result, error => error.Contains("DefaultTimeout"));
        Assert.Contains(result, error => error.Contains("RetryAttempts"));
        Assert.Contains(result, error => error.Contains("RetryDelay"));
    }

    #endregion

    #region Test Helper Classes

    /// <summary>
    /// Test agent configuration for unit testing purposes
    /// </summary>
    private class TestAgentConfiguration : BaseAgentConfiguration
    {
        public override string AgentType => "test-agent";
    }

    /// <summary>
    /// Test agent configuration with specific validation for testing template method pattern
    /// </summary>
    private class TestAgentConfigurationWithSpecificValidation : BaseAgentConfiguration
    {
        public override string AgentType => "test-agent-specific";

        public string SpecificProperty { get; set; } = "valid";

        protected override void ValidateSpecificProperties(List<string> errors)
        {
            if (string.IsNullOrEmpty(SpecificProperty))
            {
                errors.Add("SpecificProperty cannot be empty");
            }
        }
    }

    #endregion
}
