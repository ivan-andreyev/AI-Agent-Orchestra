using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orchestra.Agents.ClaudeCode;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.Integration;

/// <summary>
/// Integration tests for AgentConfigurationRegistry with multiple agent types
/// </summary>
public class AgentConfigurationRegistryIntegrationTests
{
    [Fact]
    public void Register_MultipleAgentTypes_ShouldStoreAll()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);

        var claudeConfig = new ClaudeCodeConfiguration
        {
            DefaultCliPath = "claude.cmd"
        };

        var shellConfig = new ShellAgentConfiguration
        {
            ShellExecutablePath = "powershell.exe"
        };

        // ACT
        registry.Register(claudeConfig);
        registry.Register(shellConfig);

        // ASSERT
        var retrievedClaude = registry.Get("claude-code");
        var retrievedShell = registry.Get("shell");

        Assert.NotNull(retrievedClaude);
        Assert.NotNull(retrievedShell);
        Assert.Equal("claude-code", retrievedClaude.AgentType);
        Assert.Equal("shell", retrievedShell.AgentType);
    }

    [Fact]
    public void Get_CaseInsensitiveAgentType_ShouldReturnConfiguration()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
        var config = new ClaudeCodeConfiguration { DefaultCliPath = "claude.cmd" };
        registry.Register(config);

        // ACT
        var result1 = registry.Get("claude-code");
        var result2 = registry.Get("CLAUDE-CODE");
        var result3 = registry.Get("Claude-Code");

        // ASSERT
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        Assert.Same(result1, result2);
        Assert.Same(result1, result3);
    }

    [Fact]
    public void GetAll_WithMultipleConfigurations_ShouldReturnAllRegistered()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);

        registry.Register(new ClaudeCodeConfiguration { DefaultCliPath = "claude.cmd" });
        registry.Register(new ShellAgentConfiguration { ShellExecutablePath = "powershell.exe" });

        // ACT
        var all = registry.GetAll();

        // ASSERT
        Assert.Equal(2, all.Count);
        Assert.Contains(all, c => c.AgentType == "claude-code");
        Assert.Contains(all, c => c.AgentType == "shell");
    }

    [Fact]
    public void Register_DuplicateAgentType_ShouldThrowInvalidOperationException()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
        var config1 = new ClaudeCodeConfiguration { DefaultCliPath = "claude1.cmd" };
        var config2 = new ClaudeCodeConfiguration { DefaultCliPath = "claude2.cmd" };

        registry.Register(config1);

        // ACT & ASSERT
        var exception = Assert.Throws<InvalidOperationException>(() => registry.Register(config2));
        Assert.Contains("already registered", exception.Message);
        Assert.Contains("claude-code", exception.Message);
    }

    [Fact]
    public void Get_UnregisteredAgentType_ShouldReturnNull()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);

        // ACT
        var result = registry.Get("non-existent-agent");

        // ASSERT
        Assert.Null(result);
    }

    [Fact]
    public void IsRegistered_RegisteredAgentType_ShouldReturnTrue()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
        registry.Register(new ClaudeCodeConfiguration { DefaultCliPath = "claude.cmd" });

        // ACT
        var result = registry.IsRegistered("claude-code");

        // ASSERT
        Assert.True(result);
    }

    [Fact]
    public void IsRegistered_UnregisteredAgentType_ShouldReturnFalse()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);

        // ACT
        var result = registry.IsRegistered("non-existent");

        // ASSERT
        Assert.False(result);
    }

    [Fact]
    public void IsRegistered_CaseInsensitive_ShouldReturnTrue()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
        registry.Register(new ClaudeCodeConfiguration { DefaultCliPath = "claude.cmd" });

        // ACT
        var result1 = registry.IsRegistered("claude-code");
        var result2 = registry.IsRegistered("CLAUDE-CODE");
        var result3 = registry.IsRegistered("Claude-Code");

        // ASSERT
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
    }

    [Fact]
    public void GetRegisteredTypes_WithMultipleConfigurations_ShouldReturnAllTypes()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
        registry.Register(new ClaudeCodeConfiguration { DefaultCliPath = "claude.cmd" });
        registry.Register(new ShellAgentConfiguration { ShellExecutablePath = "powershell.exe" });

        // ACT
        var types = registry.GetRegisteredTypes();

        // ASSERT
        Assert.Equal(2, types.Count);
        Assert.Contains("claude-code", types);
        Assert.Contains("shell", types);
    }

    [Fact]
    public void IntegrationWithValidator_ValidConfigurations_ShouldPassValidation()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
        var validator = new AgentConfigurationValidator(NullLogger<AgentConfigurationValidator>.Instance);

        // Use actual existing executables for validation to pass
        var claudeConfig = new ClaudeCodeConfiguration { DefaultCliPath = "C:\\Windows\\System32\\cmd.exe" };
        var shellConfig = new ShellAgentConfiguration { ShellExecutablePath = "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe" };

        registry.Register(claudeConfig);
        registry.Register(shellConfig);

        // ACT
        var validationResults = validator.ValidateAll(registry);

        // ASSERT - ValidateAll returns all configs with their error lists (empty when valid)
        Assert.Equal(2, validationResults.Count);
        Assert.Empty(validationResults["claude-code"]); // No errors
        Assert.Empty(validationResults["shell"]); // No errors
    }

    [Fact]
    public void IntegrationWithFactory_RegisterAndRetrieve_ShouldWork()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);
        var validator = new AgentConfigurationValidator(NullLogger<AgentConfigurationValidator>.Instance);
        var factory = new AgentConfigurationFactory(
            registry,
            validator,
            Array.Empty<IAgentConfiguration>(),
            NullLogger<AgentConfigurationFactory>.Instance);

        // Use actual existing executable for validation to pass
        var config = new ClaudeCodeConfiguration { DefaultCliPath = "C:\\Windows\\System32\\cmd.exe" };

        // ACT
        factory.RegisterConfiguration(config);
        var retrieved = factory.GetConfiguration("claude-code");

        // ASSERT
        Assert.NotNull(retrieved);
        Assert.Equal("claude-code", retrieved.AgentType);
        Assert.IsType<ClaudeCodeConfiguration>(retrieved);
    }

    [Fact]
    public void Register_NullConfiguration_ShouldThrowArgumentNullException()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);

        // ACT & ASSERT
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public void Get_NullOrEmptyAgentType_ShouldReturnNull()
    {
        // ARRANGE
        var registry = new AgentConfigurationRegistry(NullLogger<AgentConfigurationRegistry>.Instance);

        // ACT & ASSERT
        Assert.Null(registry.Get(null!));
        Assert.Null(registry.Get(""));
        Assert.Null(registry.Get("   "));
    }
}
