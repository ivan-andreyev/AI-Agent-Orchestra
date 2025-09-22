using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Orchestra.API.Services;

/// <summary>
/// Service for executing commands through Claude Code CLI agent
/// </summary>
public class ClaudeAgentExecutor : IAgentExecutor
{
    private readonly ILogger<ClaudeAgentExecutor> _logger;

    public string AgentType => "Claude Code CLI";

    public ClaudeAgentExecutor(ILogger<ClaudeAgentExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes a command using Claude Code CLI and returns the response
    /// </summary>
    /// <param name="command">Command to execute</param>
    /// <param name="workingDirectory">Working directory for the agent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent response</returns>
    public async Task<AgentExecutionResponse> ExecuteCommandAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var processId = Guid.NewGuid().ToString()[..8];

        _logger.LogInformation("Starting Claude agent execution - ProcessId: {ProcessId}, Command: {Command}, WorkingDir: {WorkingDirectory}",
            processId, command, workingDirectory);

        try
        {
            // Validate inputs
            ValidateInputs(command, workingDirectory);

            // Execute Claude CLI process
            var result = await ExecuteClaudeCliAsync(command, workingDirectory, processId, cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            _logger.LogInformation("Claude agent execution completed - ProcessId: {ProcessId}, Success: {Success}, Duration: {Duration}ms",
                processId, result.Success, duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Claude agent execution failed - ProcessId: {ProcessId}, Duration: {Duration}ms",
                processId, duration.TotalMilliseconds);

            return new AgentExecutionResponse
            {
                Success = false,
                Output = "",
                ErrorMessage = $"Agent execution failed: {ex.Message}",
                ExecutionTime = duration
            };
        }
    }

    private static void ValidateInputs(string command, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command cannot be null or empty", nameof(command));
        }

        if (command.Length > 5000)
        {
            throw new ArgumentException($"Command exceeds maximum length of 5000 characters: {command.Length}", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("Working directory cannot be null or empty", nameof(workingDirectory));
        }

        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"Working directory does not exist: {workingDirectory}");
        }
    }

    private async Task<AgentExecutionResponse> ExecuteClaudeCliAsync(
        string command,
        string workingDirectory,
        string processId,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        // Create process start info
        var processInfo = new ProcessStartInfo
        {
            FileName = @"C:\Users\mrred\AppData\Roaming\npm\claude.cmd",
            Arguments = $"-p --allowed-tools \"Bash Read Write Edit Glob Grep\" --output-format text",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = processInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        // Set up output handlers
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                _logger.LogWarning("Claude CLI stderr - ProcessId: {ProcessId}: {Error}", processId, e.Data);
            }
        };

        _logger.LogDebug("Starting Claude CLI process - ProcessId: {ProcessId}, Args: {Args}, WorkingDir: {WorkingDir}",
            processId, processInfo.Arguments, workingDirectory);

        // Start process
        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start Claude CLI process");
        }

        try
        {
            // Begin async output reading
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Send command to stdin
            await process.StandardInput.WriteLineAsync(command);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            // Wait for process completion with timeout
            var processCompleted = await WaitForProcessAsync(process, cancellationToken);

            var executionTime = DateTime.UtcNow - startTime;
            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            if (!processCompleted)
            {
                _logger.LogWarning("Claude CLI process timed out - ProcessId: {ProcessId}", processId);

                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to kill timed out Claude CLI process - ProcessId: {ProcessId}", processId);
                }

                return new AgentExecutionResponse
                {
                    Success = false,
                    Output = output,
                    ErrorMessage = "Agent execution timed out",
                    ExecutionTime = executionTime
                };
            }

            var exitCode = process.ExitCode;
            var success = exitCode == 0 && string.IsNullOrEmpty(error);

            _logger.LogDebug("Claude CLI process completed - ProcessId: {ProcessId}, ExitCode: {ExitCode}, OutputLength: {OutputLength}",
                processId, exitCode, output.Length);

            return new AgentExecutionResponse
            {
                Success = success,
                Output = output,
                ErrorMessage = success ? null : error,
                ExecutionTime = executionTime,
                Metadata = new Dictionary<string, object> { { "ExitCode", exitCode } }
            };
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up Claude CLI process - ProcessId: {ProcessId}", processId);
            }
        }
    }

    private static async Task<bool> WaitForProcessAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

/// <summary>
/// Fallback simulation agent executor for testing when Claude CLI is not available
/// </summary>
public class SimulationAgentExecutor : IAgentExecutor
{
    private readonly ILogger<SimulationAgentExecutor> _logger;

    public string AgentType => "Simulation";

    public SimulationAgentExecutor(ILogger<SimulationAgentExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentExecutionResponse> ExecuteCommandAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Simulating agent execution - Command: {Command}, WorkingDir: {WorkingDirectory}",
            command, workingDirectory);

        // Simulate processing delay
        await Task.Delay(Random.Shared.Next(500, 2000), cancellationToken);

        return new AgentExecutionResponse
        {
            Success = true,
            Output = $"[SIMULATION] Command executed successfully: {command}",
            ExecutionTime = TimeSpan.FromSeconds(Random.Shared.Next(1, 5)),
            Metadata = new Dictionary<string, object> { { "IsSimulation", true } }
        };
    }
}