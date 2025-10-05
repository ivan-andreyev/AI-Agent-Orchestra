using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;

namespace Orchestra.Core.Services;

/// <summary>
/// Исполнитель команд для Shell агентов через системную оболочку
/// </summary>
/// <remarks>
/// <para>
/// Выполняет команды через PowerShell (Windows) или bash (Linux/macOS)
/// с поддержкой валидации команд, управления переменными окружения
/// и безопасного выполнения.
/// </para>
/// <para>
/// <b>Регистрация в DI:</b>
/// <code>
/// services.Configure&lt;ShellAgentConfiguration&gt;(configuration.GetSection("ShellAgent"));
/// services.AddSingleton&lt;ShellAgentExecutor&gt;();
/// services.AddSingleton&lt;IAgentExecutor&gt;(provider =>
///     provider.GetRequiredService&lt;ShellAgentExecutor&gt;());
/// </code>
/// </para>
/// </remarks>
public class ShellAgentExecutor : BaseAgentExecutor<ShellAgentExecutor>
{
    private readonly ShellAgentConfiguration _configuration;

    /// <inheritdoc />
    public override string AgentType => "shell";

    /// <summary>
    /// Инициализирует новый экземпляр ShellAgentExecutor
    /// </summary>
    /// <param name="configuration">Конфигурация Shell агента</param>
    /// <param name="logger">Логгер для отслеживания операций</param>
    /// <exception cref="ArgumentNullException">Если любой из обязательных параметров равен null</exception>
    public ShellAgentExecutor(
        IOptions<ShellAgentConfiguration> configuration,
        ILogger<ShellAgentExecutor> logger)
        : base(logger, configuration?.Value)
    {
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));

        // Валидируем что конфигурация правильного типа
        if (_configuration.AgentType != AgentType)
        {
            throw new ArgumentException(
                $"Invalid configuration type: expected '{AgentType}', got '{_configuration.AgentType}'",
                nameof(configuration));
        }
    }

    /// <summary>
    /// Выполняет специфичную для Shell логику выполнения команды
    /// </summary>
    /// <param name="command">Команда для выполнения (уже провалидирована)</param>
    /// <param name="workingDirectory">Рабочая директория (уже провалидирована и существует)</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат выполнения команды</returns>
    protected override async Task<AgentExecutionResponse> ExecuteCommandCoreAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Executing Shell command: {Command} in {WorkingDirectory}", command, workingDirectory);

        // Validate command against whitelist/blacklist
        if (!IsCommandAllowed(command))
        {
            var errorMessage = "Command is not allowed by security policy";
            Logger.LogWarning("Command blocked: {Command}", command);
            return new AgentExecutionResponse
            {
                Success = false,
                Output = string.Empty,
                ErrorMessage = errorMessage,
                ExecutionTime = TimeSpan.Zero,
                Metadata = new Dictionary<string, object>
                {
                    { "BlockReason", "Security policy violation" },
                    { "WorkingDirectory", workingDirectory },
                    { "AgentType", AgentType }
                }
            };
        }

        var startTime = DateTime.UtcNow;
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _configuration.ShellExecutablePath,
                Arguments = $"{_configuration.ShellArguments} \"{EscapeCommand(command)}\"",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Add custom environment variables
            foreach (var envVar in _configuration.EnvironmentVariables)
            {
                processStartInfo.Environment[envVar.Key] = envVar.Value;
            }

            using var process = new Process { StartInfo = processStartInfo };

            // Setup output capture
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            if (_configuration.CaptureStdError)
            {
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };
            }

            Logger.LogDebug("Starting process: {FileName} {Arguments}", processStartInfo.FileName, processStartInfo.Arguments);
            process.Start();
            process.BeginOutputReadLine();
            if (_configuration.CaptureStdError)
            {
                process.BeginErrorReadLine();
            }

            // Wait for completion with timeout
            var timeout = (int)_configuration.DefaultTimeout.TotalMilliseconds;
            var completed = await Task.Run(() => process.WaitForExit(timeout), cancellationToken);

            if (!completed)
            {
                if (_configuration.KillOnTimeout)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        Logger.LogWarning("Process killed due to timeout: {Command}", command);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to kill process after timeout");
                    }
                }

                return new AgentExecutionResponse
                {
                    Success = false,
                    Output = outputBuilder.ToString(),
                    ErrorMessage = $"Command execution timed out after {_configuration.DefaultTimeout.TotalSeconds} seconds",
                    ExecutionTime = DateTime.UtcNow - startTime,
                    Metadata = new Dictionary<string, object>
                    {
                        { "TimeoutOccurred", true },
                        { "WorkingDirectory", workingDirectory },
                        { "AgentType", AgentType }
                    }
                };
            }

            var executionTime = DateTime.UtcNow - startTime;
            var output = TruncateOutput(outputBuilder.ToString());
            var errorOutput = errorBuilder.ToString();
            var success = process.ExitCode == 0;

            Logger.LogInformation(
                "Shell command completed with exit code {ExitCode} in {ExecutionTime}ms",
                process.ExitCode,
                executionTime.TotalMilliseconds);

            return new AgentExecutionResponse
            {
                Success = success,
                Output = output,
                ErrorMessage = success ? null : (!string.IsNullOrEmpty(errorOutput) ? errorOutput : "Command failed with non-zero exit code"),
                ExecutionTime = executionTime,
                Metadata = new Dictionary<string, object>
                {
                    { "ExitCode", process.ExitCode },
                    { "WorkingDirectory", workingDirectory },
                    { "AgentType", AgentType },
                    { "ShellExecutable", _configuration.ShellExecutablePath },
                    { "OutputTruncated", outputBuilder.Length > _configuration.MaxOutputLength }
                }
            };
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - startTime;
            Logger.LogError(ex, "Shell command execution failed: {Command}", command);

            return new AgentExecutionResponse
            {
                Success = false,
                Output = outputBuilder.ToString(),
                ErrorMessage = $"Execution failed: {ex.Message}",
                ExecutionTime = executionTime,
                Metadata = new Dictionary<string, object>
                {
                    { "ExceptionType", ex.GetType().Name },
                    { "WorkingDirectory", workingDirectory },
                    { "AgentType", AgentType }
                }
            };
        }
    }

    /// <summary>
    /// Validates command against allowed/blocked lists
    /// </summary>
    private bool IsCommandAllowed(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        // Extract the first word (command name) from the command
        var commandName = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(commandName))
        {
            return false;
        }

        // Check blocked commands first (blacklist takes precedence)
        if (_configuration.BlockedCommands != null && _configuration.BlockedCommands.Length > 0)
        {
            if (_configuration.BlockedCommands.Any(blocked =>
                commandName.Equals(blocked, StringComparison.OrdinalIgnoreCase)))
            {
                Logger.LogWarning("Command '{CommandName}' is in blocked list", commandName);
                return false;
            }
        }

        // If whitelist is defined, command must be in it
        if (_configuration.AllowedCommands != null && _configuration.AllowedCommands.Length > 0)
        {
            var allowed = _configuration.AllowedCommands.Any(allowedCmd =>
                commandName.Equals(allowedCmd, StringComparison.OrdinalIgnoreCase));

            if (!allowed)
            {
                Logger.LogWarning("Command '{CommandName}' is not in allowed list", commandName);
            }

            return allowed;
        }

        // No whitelist defined, command is allowed (unless blacklisted)
        return true;
    }

    /// <summary>
    /// Escapes command for safe shell execution
    /// </summary>
    private string EscapeCommand(string command)
    {
        // Escape double quotes for PowerShell/bash
        return command.Replace("\"", "\\\"");
    }

    /// <summary>
    /// Truncates output to configured maximum length
    /// </summary>
    private string TruncateOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return output;
        }

        if (output.Length <= _configuration.MaxOutputLength)
        {
            return output;
        }

        var truncated = output.Substring(0, _configuration.MaxOutputLength);
        var truncationMessage = $"\n\n[Output truncated at {_configuration.MaxOutputLength} characters]";

        Logger.LogDebug("Output truncated from {OriginalLength} to {MaxLength} characters",
            output.Length, _configuration.MaxOutputLength);

        return truncated + truncationMessage;
    }
}
