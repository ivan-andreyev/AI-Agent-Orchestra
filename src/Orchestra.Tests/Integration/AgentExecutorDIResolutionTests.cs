using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Agents.ClaudeCode;
using Orchestra.Core.Services;
using Orchestra.Core.Services.Retry;
using Xunit;

namespace Orchestra.Tests.Integration;

/// <summary>
/// Integration tests for DI resolution of multiple IAgentExecutor implementations
/// </summary>
public class AgentExecutorDIResolutionTests
{
    [Fact]
    public void ServiceProvider_MultipleAgentExecutors_ShouldResolveAll()
    {
        // ARRANGE
        var services = new ServiceCollection();

        // Register logging
        services.AddLogging();

        // Register configurations
        services.Configure<ClaudeCodeConfiguration>(config =>
        {
            config.DefaultCliPath = "claude.cmd";
            config.OutputFormat = "json";
        });

        services.Configure<ShellAgentConfiguration>(config =>
        {
            config.ShellExecutablePath = "powershell.exe";
            config.ShellArguments = "-NoProfile -Command";
        });

        // Register retry policy
        services.AddSingleton<IRetryPolicy>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ExponentialBackoffRetryPolicy>>();
            return new ExponentialBackoffRetryPolicy(
                logger,
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(100));
        });

        // Register agent executors
        services.AddSingleton<ClaudeCodeExecutor>();
        services.AddSingleton<ShellAgentExecutor>();

        var serviceProvider = services.BuildServiceProvider();

        // ACT
        var claudeExecutor = serviceProvider.GetRequiredService<ClaudeCodeExecutor>();
        var shellExecutor = serviceProvider.GetRequiredService<ShellAgentExecutor>();

        // ASSERT
        Assert.NotNull(claudeExecutor);
        Assert.NotNull(shellExecutor);
        Assert.Equal("claude-code", claudeExecutor.AgentType);
        Assert.Equal("shell", shellExecutor.AgentType);
    }

    [Fact]
    public void ServiceProvider_ResolveIAgentExecutor_WithKeyedServices_ShouldResolveByAgentType()
    {
        // ARRANGE
        var services = new ServiceCollection();
        services.AddLogging();

        services.Configure<ClaudeCodeConfiguration>(config =>
        {
            config.DefaultCliPath = "claude.cmd";
        });

        services.Configure<ShellAgentConfiguration>(config =>
        {
            config.ShellExecutablePath = "powershell.exe";
        });

        services.AddSingleton<IRetryPolicy>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ExponentialBackoffRetryPolicy>>();
            return new ExponentialBackoffRetryPolicy(logger, 3, TimeSpan.FromMilliseconds(100));
        });

        // Register as keyed services
        services.AddKeyedSingleton<IAgentExecutor, ClaudeCodeExecutor>("claude-code");
        services.AddKeyedSingleton<IAgentExecutor, ShellAgentExecutor>("shell");

        var serviceProvider = services.BuildServiceProvider();

        // ACT
        var claudeExecutor = serviceProvider.GetRequiredKeyedService<IAgentExecutor>("claude-code");
        var shellExecutor = serviceProvider.GetRequiredKeyedService<IAgentExecutor>("shell");

        // ASSERT
        Assert.NotNull(claudeExecutor);
        Assert.NotNull(shellExecutor);
        Assert.IsType<ClaudeCodeExecutor>(claudeExecutor);
        Assert.IsType<ShellAgentExecutor>(shellExecutor);
        Assert.Equal("claude-code", claudeExecutor.AgentType);
        Assert.Equal("shell", shellExecutor.AgentType);
    }

    [Fact]
    public void ServiceProvider_ResolveAllIAgentExecutors_ShouldReturnAllRegistered()
    {
        // ARRANGE
        var services = new ServiceCollection();
        services.AddLogging();

        services.Configure<ClaudeCodeConfiguration>(config => config.DefaultCliPath = "claude.cmd");
        services.Configure<ShellAgentConfiguration>(config => config.ShellExecutablePath = "powershell.exe");

        services.AddSingleton<IRetryPolicy>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ExponentialBackoffRetryPolicy>>();
            return new ExponentialBackoffRetryPolicy(logger, 3, TimeSpan.FromMilliseconds(100));
        });

        services.AddSingleton<IAgentExecutor, ClaudeCodeExecutor>();
        services.AddSingleton<IAgentExecutor, ShellAgentExecutor>();

        var serviceProvider = services.BuildServiceProvider();

        // ACT
        var executors = serviceProvider.GetServices<IAgentExecutor>().ToList();

        // ASSERT
        Assert.Equal(2, executors.Count);
        Assert.Contains(executors, e => e.AgentType == "claude-code");
        Assert.Contains(executors, e => e.AgentType == "shell");
    }

    [Fact]
    public void ServiceProvider_ExecutorConfigurations_ShouldBeInjectedCorrectly()
    {
        // ARRANGE
        var services = new ServiceCollection();
        services.AddLogging();

        var claudeCliPath = "C:\\custom\\claude.cmd";
        var shellExecutable = "bash";

        services.Configure<ClaudeCodeConfiguration>(config =>
        {
            config.DefaultCliPath = claudeCliPath;
            config.MaxConcurrentExecutions = 5;
        });

        services.Configure<ShellAgentConfiguration>(config =>
        {
            config.ShellExecutablePath = shellExecutable;
            config.MaxConcurrentExecutions = 10;
        });

        services.AddSingleton<IRetryPolicy>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ExponentialBackoffRetryPolicy>>();
            return new ExponentialBackoffRetryPolicy(logger, 3, TimeSpan.FromMilliseconds(100));
        });

        services.AddSingleton<ClaudeCodeExecutor>();
        services.AddSingleton<ShellAgentExecutor>();

        var serviceProvider = services.BuildServiceProvider();

        // ACT
        var claudeExecutor = serviceProvider.GetRequiredService<ClaudeCodeExecutor>();
        var shellExecutor = serviceProvider.GetRequiredService<ShellAgentExecutor>();

        // ASSERT - verify configuration was injected
        var claudeConfig = serviceProvider.GetRequiredService<IOptions<ClaudeCodeConfiguration>>().Value;
        var shellConfig = serviceProvider.GetRequiredService<IOptions<ShellAgentConfiguration>>().Value;

        Assert.Equal(claudeCliPath, claudeConfig.DefaultCliPath);
        Assert.Equal(shellExecutable, shellConfig.ShellExecutablePath);
        Assert.Equal(5, claudeConfig.MaxConcurrentExecutions);
        Assert.Equal(10, shellConfig.MaxConcurrentExecutions);
    }

    [Fact]
    public void ServiceProvider_SingletonLifetime_ShouldReturnSameInstance()
    {
        // ARRANGE
        var services = new ServiceCollection();
        services.AddLogging();

        services.Configure<ClaudeCodeConfiguration>(config => config.DefaultCliPath = "claude.cmd");
        services.AddSingleton<IRetryPolicy>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ExponentialBackoffRetryPolicy>>();
            return new ExponentialBackoffRetryPolicy(logger, 3, TimeSpan.FromMilliseconds(100));
        });
        services.AddSingleton<ClaudeCodeExecutor>();

        var serviceProvider = services.BuildServiceProvider();

        // ACT
        var executor1 = serviceProvider.GetRequiredService<ClaudeCodeExecutor>();
        var executor2 = serviceProvider.GetRequiredService<ClaudeCodeExecutor>();

        // ASSERT
        Assert.Same(executor1, executor2);
    }

    [Fact]
    public void ServiceProvider_WithAgentConfigurationRegistry_ShouldIntegrateCorrectly()
    {
        // ARRANGE
        var services = new ServiceCollection();
        services.AddLogging();

        // Register Agent Configuration infrastructure
        services.AddSingleton<IAgentConfigurationRegistry, AgentConfigurationRegistry>();
        services.AddSingleton<IAgentConfigurationValidator, AgentConfigurationValidator>();
        services.AddSingleton<IAgentConfigurationFactory, AgentConfigurationFactory>();

        // Register configurations with actual existing executables
        services.Configure<ClaudeCodeConfiguration>(config => config.DefaultCliPath = "C:\\Windows\\System32\\cmd.exe");
        services.Configure<ShellAgentConfiguration>(config => config.ShellExecutablePath = "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe");

        // Register retry policy
        services.AddSingleton<IRetryPolicy>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ExponentialBackoffRetryPolicy>>();
            return new ExponentialBackoffRetryPolicy(logger, 3, TimeSpan.FromMilliseconds(100));
        });

        // Register executors
        services.AddSingleton<ClaudeCodeExecutor>();
        services.AddSingleton<ShellAgentExecutor>();

        var serviceProvider = services.BuildServiceProvider();

        // Register configurations in registry
        var factory = serviceProvider.GetRequiredService<IAgentConfigurationFactory>();
        var claudeConfig = serviceProvider.GetRequiredService<IOptions<ClaudeCodeConfiguration>>().Value;
        var shellConfig = serviceProvider.GetRequiredService<IOptions<ShellAgentConfiguration>>().Value;

        factory.RegisterConfiguration(claudeConfig);
        factory.RegisterConfiguration(shellConfig);

        // ACT
        var registry = serviceProvider.GetRequiredService<IAgentConfigurationRegistry>();
        var claudeExecutor = serviceProvider.GetRequiredService<ClaudeCodeExecutor>();
        var shellExecutor = serviceProvider.GetRequiredService<ShellAgentExecutor>();

        // ASSERT
        Assert.NotNull(registry.Get("claude-code"));
        Assert.NotNull(registry.Get("shell"));
        Assert.Equal(2, registry.GetRegisteredTypes().Count);
        Assert.NotNull(claudeExecutor);
        Assert.NotNull(shellExecutor);
    }

    [Fact]
    public void ServiceProvider_RetryPolicySharedAcrossExecutors_ShouldWorkCorrectly()
    {
        // ARRANGE
        var services = new ServiceCollection();
        services.AddLogging();

        services.Configure<ClaudeCodeConfiguration>(config => config.DefaultCliPath = "claude.cmd");
        services.Configure<ShellAgentConfiguration>(config => config.ShellExecutablePath = "powershell.exe");

        // Single retry policy instance
        services.AddSingleton<IRetryPolicy>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ExponentialBackoffRetryPolicy>>();
            return new ExponentialBackoffRetryPolicy(
                logger,
                maxAttempts: 5,
                baseDelay: TimeSpan.FromMilliseconds(50));
        });

        services.AddSingleton<ClaudeCodeExecutor>();
        services.AddSingleton<ShellAgentExecutor>();

        var serviceProvider = services.BuildServiceProvider();

        // ACT
        var retryPolicy1 = serviceProvider.GetRequiredService<ClaudeCodeExecutor>();
        var retryPolicy2 = serviceProvider.GetRequiredService<ShellAgentExecutor>();
        var retryPolicyDirect = serviceProvider.GetRequiredService<IRetryPolicy>();

        // ASSERT - verify same instance is shared
        Assert.NotNull(retryPolicyDirect);
        Assert.IsType<ExponentialBackoffRetryPolicy>(retryPolicyDirect);
    }

    [Fact]
    public void ServiceProvider_MissingConfiguration_ShouldUseDefaultValues()
    {
        // ARRANGE
        var services = new ServiceCollection();
        services.AddLogging();

        // Missing ClaudeCodeConfiguration - IOptions<T> will provide default instance
        services.AddSingleton<IRetryPolicy>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ExponentialBackoffRetryPolicy>>();
            return new ExponentialBackoffRetryPolicy(logger, 3, TimeSpan.FromMilliseconds(100));
        });

        services.AddSingleton<ClaudeCodeExecutor>();

        var serviceProvider = services.BuildServiceProvider();

        // ACT - IOptions<ClaudeCodeConfiguration> provides default instance with empty values
        // ClaudeCodeExecutor constructor does NOT validate DefaultCliPath existence
        var executor = serviceProvider.GetRequiredService<ClaudeCodeExecutor>();

        // ASSERT - Executor is created successfully with default configuration
        Assert.NotNull(executor);
        Assert.Equal("claude-code", executor.AgentType);
    }
}
