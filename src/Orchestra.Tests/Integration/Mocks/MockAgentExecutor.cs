using Orchestra.Core.Services;
using System.Text.RegularExpressions;

namespace Orchestra.Tests.Integration.Mocks;

/// <summary>
/// Mock implementation of IAgentExecutor for integration testing
/// Simulates agent command execution without requiring real Claude CLI
/// </summary>
public class MockAgentExecutor : IAgentExecutor
{
    private readonly Dictionary<string, Func<string, string, Task<AgentExecutionResponse>>> _commandHandlers;
    private readonly Dictionary<string, AgentBehavior> _agentBehaviors;
    private readonly Dictionary<string, string> _repositoryToAgentMapping; // path -> agentId
    private readonly Random _random = new();

    public string AgentType => "Mock Test Agent";

    public MockAgentExecutor()
    {
        _commandHandlers = new Dictionary<string, Func<string, string, Task<AgentExecutionResponse>>>();
        _agentBehaviors = new Dictionary<string, AgentBehavior>();
        _repositoryToAgentMapping = new Dictionary<string, string>();

        // Register default command handlers
        RegisterDefaultHandlers();
    }

    /// <summary>
    /// Executes a command with simulated agent behavior
    /// </summary>
    public async Task<AgentExecutionResponse> ExecuteCommandAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Debug logging
        Console.WriteLine($"[MockAgentExecutor] ExecuteCommandAsync called - Command: {command}, WorkingDir: {workingDirectory}");
        Console.WriteLine($"[MockAgentExecutor] Repository mappings: {string.Join(", ", _repositoryToAgentMapping.Select(kv => $"{kv.Key} -> {kv.Value}"))}");

        // Check if this is a test agent with specific behavior
        string? agentId = null;
        AgentBehavior behavior = AgentBehavior.Normal;

        if (_repositoryToAgentMapping.TryGetValue(workingDirectory, out agentId) &&
            _agentBehaviors.TryGetValue(agentId, out behavior))
        {
            Console.WriteLine($"[MockAgentExecutor] Found agent {agentId} with behavior {behavior} for directory {workingDirectory}");

            // Apply agent-specific behavior
            switch (behavior)
            {
                case AgentBehavior.Error:
                    Console.WriteLine($"[MockAgentExecutor] Returning error response for agent {agentId}");
                    return new AgentExecutionResponse
                    {
                        Success = false,
                        ErrorMessage = $"Agent {agentId} is in error state - simulated failure",
                        Output = "",
                        ExecutionTime = DateTime.UtcNow - startTime,
                        Metadata = new Dictionary<string, object>
                        {
                            ["WorkingDirectory"] = workingDirectory,
                            ["AgentBehavior"] = behavior.ToString(),
                            ["AgentId"] = agentId
                        }
                    };

                case AgentBehavior.Timeout:
                    // Simulate timeout by waiting indefinitely
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    break;

                case AgentBehavior.Slow:
                    // Slow execution - wait longer
                    await Task.Delay(_random.Next(1000, 3000), cancellationToken);
                    break;
            }
        }
        else
        {
            Console.WriteLine($"[MockAgentExecutor] No specific behavior found for directory {workingDirectory}, using Normal behavior");
        }

        // Simulate normal execution delay (realistic but fast for tests)
        var executionDelay = _random.Next(10, 100); // 10-100ms
        await Task.Delay(executionDelay, cancellationToken);

        try
        {
            // Check for specific command handlers
            foreach (var handler in _commandHandlers)
            {
                if (command.Contains(handler.Key, StringComparison.OrdinalIgnoreCase))
                {
                    var response = await handler.Value(command, workingDirectory);
                    response.ExecutionTime = DateTime.UtcNow - startTime;
                    return response;
                }
            }

            // Default behavior for unknown commands
            return new AgentExecutionResponse
            {
                Success = true,
                Output = $"Mock executed: {command}",
                ExecutionTime = DateTime.UtcNow - startTime,
                Metadata = new Dictionary<string, object>
                {
                    ["WorkingDirectory"] = workingDirectory,
                    ["MockExecution"] = true
                }
            };
        }
        catch (OperationCanceledException)
        {
            return new AgentExecutionResponse
            {
                Success = false,
                ErrorMessage = "Command execution was cancelled",
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Registers a custom command handler for specific commands
    /// </summary>
    public void RegisterCommandHandler(string commandPattern, Func<string, string, Task<AgentExecutionResponse>> handler)
    {
        _commandHandlers[commandPattern] = handler;
    }

    /// <summary>
    /// Simulates agent behavior (normal, slow, error, timeout)
    /// </summary>
    public void SetAgentBehavior(string agentId, AgentBehavior behavior)
    {
        _agentBehaviors[agentId] = behavior;
    }

    /// <summary>
    /// Registers mapping between repository path and agent ID for behavior handling
    /// </summary>
    public void RegisterAgent(string agentId, string repositoryPath)
    {
        _repositoryToAgentMapping[repositoryPath] = agentId;
    }

    /// <summary>
    /// Simulates agent failure for specific agent
    /// </summary>
    public void SimulateAgentFailure(string agentId)
    {
        SetAgentBehavior(agentId, AgentBehavior.Error);
    }

    /// <summary>
    /// Simulates agent timeout for specific agent
    /// </summary>
    public void SimulateAgentTimeout(string agentId)
    {
        SetAgentBehavior(agentId, AgentBehavior.Timeout);
    }

    private void RegisterDefaultHandlers()
    {
        // Echo command handler
        RegisterCommandHandler("echo", async (command, workDir) =>
        {
            var echoMatch = Regex.Match(command, @"echo\s+['""]?(.+?)['""]?$", RegexOptions.IgnoreCase);
            var output = echoMatch.Success ? echoMatch.Groups[1].Value : "echo test";

            return new AgentExecutionResponse
            {
                Success = true,
                Output = output,
                Metadata = new Dictionary<string, object>
                {
                    ["CommandType"] = "echo",
                    ["WorkingDirectory"] = workDir
                }
            };
        });

        // File operations
        RegisterCommandHandler("ls", async (command, workDir) =>
        {
            return new AgentExecutionResponse
            {
                Success = true,
                Output = "file1.txt\nfile2.md\nsubdir/",
                Metadata = new Dictionary<string, object> { ["CommandType"] = "list" }
            };
        });

        RegisterCommandHandler("pwd", async (command, workDir) =>
        {
            return new AgentExecutionResponse
            {
                Success = true,
                Output = workDir,
                Metadata = new Dictionary<string, object> { ["CommandType"] = "pwd" }
            };
        });

        // Git operations
        RegisterCommandHandler("git", async (command, workDir) =>
        {
            if (command.Contains("status"))
            {
                return new AgentExecutionResponse
                {
                    Success = true,
                    Output = "On branch main\nNothing to commit, working tree clean",
                    Metadata = new Dictionary<string, object> { ["CommandType"] = "git-status" }
                };
            }

            return new AgentExecutionResponse
            {
                Success = true,
                Output = "Git command executed successfully",
                Metadata = new Dictionary<string, object> { ["CommandType"] = "git" }
            };
        });

        // Simulate task failure for testing error handling
        RegisterCommandHandler("fail", async (command, workDir) =>
        {
            return new AgentExecutionResponse
            {
                Success = false,
                ErrorMessage = "Simulated command failure for testing",
                Output = "",
                Metadata = new Dictionary<string, object> { ["CommandType"] = "failure-test" }
            };
        });

        // Simulate long-running task
        RegisterCommandHandler("slow", async (command, workDir) =>
        {
            await Task.Delay(2000); // 2 second delay
            return new AgentExecutionResponse
            {
                Success = true,
                Output = "Slow command completed",
                Metadata = new Dictionary<string, object>
                {
                    ["CommandType"] = "slow",
                    ["Duration"] = "2000ms"
                }
            };
        });
    }
}

/// <summary>
/// Defines different agent behavior patterns for testing
/// </summary>
public enum AgentBehavior
{
    Normal,      // Standard execution
    Slow,        // Delayed execution
    Error,       // Always fails
    Timeout,     // Never completes
    Intermittent // Sometimes fails
}