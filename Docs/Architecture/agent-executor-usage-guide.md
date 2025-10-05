# IAgentExecutor Pattern Usage Guide

## Overview

This guide demonstrates how to implement and use the `IAgentExecutor` pattern for creating new agent types in the Orchestra framework. The pattern provides a uniform interface for executing commands across different AI agent implementations.

## Table of Contents

1. [Creating a New Agent Executor](#creating-a-new-agent-executor)
2. [Implementing Agent Configuration](#implementing-agent-configuration)
3. [Registering in Dependency Injection](#registering-in-dependency-injection)
4. [Usage Patterns](#usage-patterns)
5. [Testing Your Implementation](#testing-your-implementation)

---

## Creating a New Agent Executor

### Step 1: Implement Agent Configuration

Create a configuration class that implements `IAgentConfiguration`:

```csharp
using Orchestra.Core.Services;

namespace Orchestra.Agents.MyAgent;

/// <summary>
/// Конфигурация для MyAgent агента
/// </summary>
public class MyAgentConfiguration : IAgentConfiguration
{
    /// <inheritdoc />
    public string AgentType => "my-agent";

    /// <summary>
    /// Путь к исполняемому файлу агента
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// API ключ для авторизации
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Максимальное количество одновременных выполнений
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = 1;

    /// <summary>
    /// Timeout для операций
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ExecutablePath))
        {
            errors.Add("ExecutablePath is required");
        }
        else if (!File.Exists(ExecutablePath))
        {
            errors.Add($"ExecutablePath does not exist: {ExecutablePath}");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            errors.Add("ApiKey is required");
        }

        if (MaxConcurrentExecutions < 1)
        {
            errors.Add("MaxConcurrentExecutions must be at least 1");
        }

        if (DefaultTimeout <= TimeSpan.Zero)
        {
            errors.Add("DefaultTimeout must be positive");
        }

        return errors;
    }
}
```

### Step 2: Implement Agent Executor

Create an executor class that inherits from `BaseAgentExecutor<T>`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Services;
using Orchestra.Core.Services.Retry;

namespace Orchestra.Agents.MyAgent;

/// <summary>
/// Исполнитель команд для MyAgent агента
/// </summary>
public class MyAgentExecutor : BaseAgentExecutor<MyAgentExecutor>
{
    private readonly MyAgentConfiguration _configuration;
    private readonly IRetryPolicy _retryPolicy;

    /// <inheritdoc />
    public override string AgentType => "my-agent";

    /// <summary>
    /// Инициализирует новый экземпляр MyAgentExecutor
    /// </summary>
    public MyAgentExecutor(
        IOptions<MyAgentConfiguration> configuration,
        IRetryPolicy retryPolicy,
        ILogger<MyAgentExecutor> logger)
        : base(logger, configuration?.Value)
    {
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));

        // Валидация типа конфигурации
        if (_configuration.AgentType != AgentType)
        {
            throw new ArgumentException(
                $"Invalid configuration type: expected '{AgentType}', got '{_configuration.AgentType}'",
                nameof(configuration));
        }
    }

    /// <summary>
    /// Выполняет команду через MyAgent
    /// </summary>
    protected override async Task<AgentExecutionResponse> ExecuteCommandCoreAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Executing MyAgent command: {Command}", command);

        var startTime = DateTime.UtcNow;

        // Выполнение с retry policy
        return await _retryPolicy.ExecuteAsync(async ct =>
        {
            // Ваша логика выполнения команды
            var result = await ExecuteMyAgentCommand(command, workingDirectory, ct);
            var executionTime = DateTime.UtcNow - startTime;

            Logger.LogInformation(
                "MyAgent command executed in {ExecutionTime}ms",
                executionTime.TotalMilliseconds);

            return new AgentExecutionResponse
            {
                Success = result.Success,
                Output = result.Output,
                ErrorMessage = result.ErrorMessage,
                ExecutionTime = executionTime,
                Metadata = new Dictionary<string, object>
                {
                    { "AgentType", AgentType },
                    { "WorkingDirectory", workingDirectory },
                    { "ApiKeyUsed", !string.IsNullOrEmpty(_configuration.ApiKey) }
                }
            };
        }, cancellationToken);
    }

    private async Task<MyAgentResult> ExecuteMyAgentCommand(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        // Реализация специфичной логики MyAgent
        // Например, вызов API, запуск процесса, и т.д.

        throw new NotImplementedException("Implement your agent execution logic here");
    }

    private class MyAgentResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string? ErrorMessage { get; set; }
    }
}
```

---

## Registering in Dependency Injection

### Step 1: Configure in appsettings.json

Add configuration section for your agent:

```json
{
  "MyAgent": {
    "ExecutablePath": "C:\\path\\to\\myagent.exe",
    "ApiKey": "your-api-key-here",
    "MaxConcurrentExecutions": 3,
    "DefaultTimeout": "00:05:00"
  }
}
```

### Step 2: Register in Startup.cs

Register your agent executor and configuration in the DI container:

```csharp
// In Startup.cs ConfigureServices method

// 1. Register configuration
services.Configure<MyAgentConfiguration>(
    configuration.GetSection("MyAgent"));

// 2. Register configuration as IAgentConfiguration (для factory)
services.AddSingleton<IAgentConfiguration>(provider =>
    provider.GetRequiredService<IOptions<MyAgentConfiguration>>().Value);

// 3. Register executor
services.AddSingleton<MyAgentExecutor>();

// 4. Register as IAgentExecutor (если нужно)
services.AddSingleton<IAgentExecutor>(provider =>
    provider.GetRequiredService<MyAgentExecutor>());

// 5. Для множественных агентов - используйте Keyed Services
services.AddKeyedSingleton<IAgentExecutor, MyAgentExecutor>("my-agent");
```

### Step 3: Alternative - Multiple Agent Registration

For registering multiple agent types:

```csharp
// Register all configurations
services.Configure<ClaudeCodeConfiguration>(configuration.GetSection("ClaudeCode"));
services.Configure<MyAgentConfiguration>(configuration.GetSection("MyAgent"));

// Register all executors
services.AddSingleton<ClaudeCodeExecutor>();
services.AddSingleton<MyAgentExecutor>();

// Register as keyed services by agent type
services.AddKeyedSingleton<IAgentExecutor, ClaudeCodeExecutor>("claude-code");
services.AddKeyedSingleton<IAgentExecutor, MyAgentExecutor>("my-agent");

// Register configuration infrastructure
services.AddSingleton<IAgentConfigurationRegistry, AgentConfigurationRegistry>();
services.AddSingleton<IAgentConfigurationValidator, AgentConfigurationValidator>();
services.AddSingleton<IAgentConfigurationFactory, AgentConfigurationFactory>();

// Register all IAgentConfiguration instances for factory
services.AddSingleton<IEnumerable<IAgentConfiguration>>(provider => new[]
{
    provider.GetRequiredService<IOptions<ClaudeCodeConfiguration>>().Value,
    provider.GetRequiredService<IOptions<MyAgentConfiguration>>().Value
});
```

---

## Usage Patterns

### Pattern 1: Direct Executor Usage

```csharp
public class MyService
{
    private readonly MyAgentExecutor _executor;
    private readonly ILogger<MyService> _logger;

    public MyService(
        MyAgentExecutor executor,
        ILogger<MyService> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public async Task ProcessTaskAsync(string command, string workingDir)
    {
        try
        {
            var response = await _executor.ExecuteCommandAsync(
                command,
                workingDir,
                CancellationToken.None);

            if (response.Success)
            {
                _logger.LogInformation("Task completed: {Output}", response.Output);
            }
            else
            {
                _logger.LogError("Task failed: {Error}", response.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execution failed");
        }
    }
}
```

### Pattern 2: Dynamic Agent Resolution (Keyed Services)

```csharp
public class AgentOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IServiceProvider serviceProvider,
        ILogger<AgentOrchestrator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<AgentExecutionResponse> ExecuteWithAgentAsync(
        string agentType,
        string command,
        string workingDirectory)
    {
        // Получаем executor по типу агента
        var executor = _serviceProvider.GetRequiredKeyedService<IAgentExecutor>(agentType);

        _logger.LogInformation(
            "Executing command with agent type: {AgentType}",
            agentType);

        return await executor.ExecuteCommandAsync(
            command,
            workingDirectory,
            CancellationToken.None);
    }
}
```

### Pattern 3: Using Agent Configuration Registry

```csharp
public class ConfigurationManager
{
    private readonly IAgentConfigurationRegistry _registry;
    private readonly IAgentConfigurationValidator _validator;

    public ConfigurationManager(
        IAgentConfigurationRegistry registry,
        IAgentConfigurationValidator validator)
    {
        _registry = registry;
        _validator = validator;
    }

    public void ValidateAllAgents()
    {
        var validationResults = _validator.ValidateAll(_registry);

        foreach (var (agentType, errors) in validationResults)
        {
            if (errors.Count > 0)
            {
                Console.WriteLine($"Agent '{agentType}' validation errors:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
            else
            {
                Console.WriteLine($"Agent '{agentType}': ✓ Valid");
            }
        }
    }

    public IAgentConfiguration? GetAgentConfig(string agentType)
    {
        if (_registry.IsRegistered(agentType))
        {
            return _registry.Get(agentType);
        }

        return null;
    }
}
```

### Pattern 4: Multiple Agents with IEnumerable Resolution

```csharp
public class MultiAgentProcessor
{
    private readonly IEnumerable<IAgentExecutor> _executors;
    private readonly ILogger<MultiAgentProcessor> _logger;

    public MultiAgentProcessor(
        IEnumerable<IAgentExecutor> executors,
        ILogger<MultiAgentProcessor> logger)
    {
        _executors = executors;
        _logger = logger;
    }

    public async Task<Dictionary<string, AgentExecutionResponse>> ExecuteOnAllAgentsAsync(
        string command,
        string workingDirectory)
    {
        var results = new Dictionary<string, AgentExecutionResponse>();

        foreach (var executor in _executors)
        {
            _logger.LogInformation(
                "Executing on agent: {AgentType}",
                executor.AgentType);

            var response = await executor.ExecuteCommandAsync(
                command,
                workingDirectory,
                CancellationToken.None);

            results[executor.AgentType] = response;
        }

        return results;
    }
}
```

---

## Testing Your Implementation

### Unit Tests Example

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestra.Core.Services.Retry;
using Xunit;

namespace Orchestra.Tests.UnitTests.Agents;

public class MyAgentExecutorTests
{
    [Fact]
    public void Constructor_ValidConfiguration_ShouldSucceed()
    {
        // ARRANGE
        var config = new MyAgentConfiguration
        {
            ExecutablePath = "C:\\Windows\\System32\\cmd.exe",
            ApiKey = "test-key"
        };
        var options = Options.Create(config);
        var retryPolicy = new NoRetryPolicy(NullLogger<NoRetryPolicy>.Instance);

        // ACT
        var executor = new MyAgentExecutor(
            options,
            retryPolicy,
            NullLogger<MyAgentExecutor>.Instance);

        // ASSERT
        Assert.NotNull(executor);
        Assert.Equal("my-agent", executor.AgentType);
    }

    [Fact]
    public void Constructor_NullConfiguration_ShouldThrow()
    {
        // ARRANGE
        var retryPolicy = new NoRetryPolicy(NullLogger<NoRetryPolicy>.Instance);

        // ACT & ASSERT
        Assert.Throws<ArgumentNullException>(() =>
            new MyAgentExecutor(
                null!,
                retryPolicy,
                NullLogger<MyAgentExecutor>.Instance));
    }
}
```

### Integration Tests Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Orchestra.Tests.Integration;

public class MyAgentIntegrationTests
{
    [Fact]
    public void ServiceProvider_ShouldResolveMyAgentExecutor()
    {
        // ARRANGE
        var services = new ServiceCollection();
        services.AddLogging();

        services.Configure<MyAgentConfiguration>(config =>
        {
            config.ExecutablePath = "C:\\Windows\\System32\\cmd.exe";
            config.ApiKey = "test-key";
        });

        services.AddSingleton<IRetryPolicy>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<NoRetryPolicy>>();
            return new NoRetryPolicy(logger);
        });

        services.AddSingleton<MyAgentExecutor>();

        var serviceProvider = services.BuildServiceProvider();

        // ACT
        var executor = serviceProvider.GetRequiredService<MyAgentExecutor>();

        // ASSERT
        Assert.NotNull(executor);
        Assert.Equal("my-agent", executor.AgentType);
    }

    [Fact]
    public void ServiceProvider_MultipleAgents_ShouldResolveAll()
    {
        // ARRANGE
        var services = new ServiceCollection();
        services.AddLogging();

        // Configure both agents
        services.Configure<ClaudeCodeConfiguration>(config =>
            config.DefaultCliPath = "C:\\Windows\\System32\\cmd.exe");
        services.Configure<MyAgentConfiguration>(config =>
        {
            config.ExecutablePath = "C:\\Windows\\System32\\cmd.exe";
            config.ApiKey = "test";
        });

        services.AddSingleton<IRetryPolicy, NoRetryPolicy>();
        services.AddSingleton<IAgentExecutor, ClaudeCodeExecutor>();
        services.AddSingleton<IAgentExecutor, MyAgentExecutor>();

        var serviceProvider = services.BuildServiceProvider();

        // ACT
        var executors = serviceProvider.GetServices<IAgentExecutor>().ToList();

        // ASSERT
        Assert.Equal(2, executors.Count);
        Assert.Contains(executors, e => e.AgentType == "claude-code");
        Assert.Contains(executors, e => e.AgentType == "my-agent");
    }
}
```

---

## Best Practices

1. **Configuration Validation**: Always implement comprehensive `Validate()` method in your configuration
2. **Retry Logic**: Use `IRetryPolicy` for resilient execution
3. **Logging**: Log all important operations and errors
4. **Cancellation**: Respect `CancellationToken` for graceful shutdown
5. **Metadata**: Include useful metadata in `AgentExecutionResponse` for debugging
6. **Error Handling**: Catch and log exceptions, return meaningful error messages
7. **Testing**: Write both unit tests (executor logic) and integration tests (DI resolution)
8. **Documentation**: Add XML documentation to all public members

---

## Common Pitfalls

1. **Missing Configuration**: Always check if configuration exists and is valid
2. **File Path Validation**: Validate file paths in `Validate()`, not in constructor
3. **AgentType Mismatch**: Ensure configuration `AgentType` matches executor `AgentType`
4. **Null IOptions**: IOptions<T> is never null, but `.Value` can be default instance
5. **Retry Policy**: Don't implement retry logic manually - use `IRetryPolicy`
6. **Keyed Services**: When using keyed services, ensure keys match `AgentType`
7. **Semaphore Limits**: BaseAgentExecutor already handles concurrency - don't add duplicate limits

---

## References

- [IAgentExecutor.cs](../../src/Orchestra.Core/Services/IAgentExecutor.cs) - Interface definition
- [BaseAgentExecutor.cs](../../src/Orchestra.Core/Services/BaseAgentExecutor.cs) - Base implementation
- [ClaudeCodeExecutor.cs](../../src/Orchestra.Agents/ClaudeCode/ClaudeCodeExecutor.cs) - Reference implementation
- [AgentConfigurationRegistry.cs](../../src/Orchestra.Core/Services/AgentConfigurationRegistry.cs) - Registry implementation
