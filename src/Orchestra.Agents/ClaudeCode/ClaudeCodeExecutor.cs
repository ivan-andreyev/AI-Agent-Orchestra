using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Services;
using System.Diagnostics;
using System.Text;

namespace Orchestra.Agents.ClaudeCode;

/// <summary>
/// Исполнитель команд для агентов Claude Code через CLI интерфейс
/// </summary>
public class ClaudeCodeExecutor : IAgentExecutor
{
    private readonly ClaudeCodeConfiguration _configuration;
    private readonly ILogger<ClaudeCodeExecutor> _logger;
    private static readonly SemaphoreSlim _executionSemaphore = new(3, 3); // Ограничение параллельных выполнений

    /// <inheritdoc />
    public string AgentType => "Claude Code";

    /// <summary>
    /// Инициализирует новый экземпляр ClaudeCodeExecutor
    /// </summary>
    /// <param name="configuration">Конфигурация Claude Code агента</param>
    /// <param name="logger">Логгер для отслеживания операций</param>
    /// <exception cref="ArgumentNullException">Если любой из обязательных параметров равен null</exception>
    public ClaudeCodeExecutor(
        IOptions<ClaudeCodeConfiguration> configuration,
        ILogger<ClaudeCodeExecutor> logger)
    {
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AgentExecutionResponse> ExecuteCommandAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        // Валидация входных параметров
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command cannot be null or empty", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = _configuration.DefaultWorkingDirectory ?? Environment.CurrentDirectory;
        }

        // Ensure working directory exists - create if needed
        if (!Directory.Exists(workingDirectory))
        {
            _logger.LogInformation("Working directory does not exist, creating: {WorkingDirectory}", workingDirectory);
            try
            {
                Directory.CreateDirectory(workingDirectory);
            }
            catch (Exception ex)
            {
                throw new DirectoryNotFoundException($"Working directory not found and could not be created: {workingDirectory}", ex);
            }
        }

        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Executing Claude Code command in directory {WorkingDirectory}: {Command}",
            workingDirectory, command.Length > 100 ? command.Substring(0, 100) + "..." : command);

        // Ограничиваем количество параллельных выполнений
        await _executionSemaphore.WaitAsync(cancellationToken);

        try
        {
            // Пытаемся найти активную сессию Claude Code через HTTP API
            var httpResult = await TryExecuteViaHttpApi(command, workingDirectory, cancellationToken);
            if (httpResult != null)
            {
                var httpExecutionTime = DateTime.UtcNow - startTime;
                _logger.LogInformation("Command executed via HTTP API in {ExecutionTime}ms",
                    httpExecutionTime.TotalMilliseconds);

                return new AgentExecutionResponse
                {
                    Success = httpResult.Success,
                    Output = httpResult.Output,
                    ErrorMessage = httpResult.ErrorMessage,
                    ExecutionTime = httpExecutionTime,
                    Metadata = new Dictionary<string, object>
                    {
                        { "ExecutionMethod", "HTTP API" },
                        { "WorkingDirectory", workingDirectory },
                        { "AgentType", AgentType }
                    }
                };
            }

            // Fallback на CLI выполнение
            var cliResult = await ExecuteViaCli(command, workingDirectory, cancellationToken);
            var executionTime = DateTime.UtcNow - startTime;

            _logger.LogInformation("Command executed via CLI in {ExecutionTime}ms",
                executionTime.TotalMilliseconds);

            return new AgentExecutionResponse
            {
                Success = cliResult.Success,
                Output = cliResult.Output,
                ErrorMessage = cliResult.ErrorMessage,
                ExecutionTime = executionTime,
                Metadata = new Dictionary<string, object>
                {
                    { "ExecutionMethod", "CLI" },
                    { "WorkingDirectory", workingDirectory },
                    { "AgentType", AgentType },
                    { "CliPath", _configuration.DefaultCliPath }
                }
            };
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Failed to execute Claude Code command: {Command}", command);

            return new AgentExecutionResponse
            {
                Success = false,
                Output = "",
                ErrorMessage = $"Execution failed: {ex.Message}",
                ExecutionTime = executionTime,
                Metadata = new Dictionary<string, object>
                {
                    { "ExecutionMethod", "Failed" },
                    { "Exception", ex.GetType().Name },
                    { "WorkingDirectory", workingDirectory },
                    { "AgentType", AgentType }
                }
            };
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    #region HTTP API выполнение

    private async Task<AgentExecutionResult?> TryExecuteViaHttpApi(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = _configuration.DefaultTimeout;

            // Попробуем найти активную сессию Claude Code на стандартных портах
            var claudePorts = new[] { 3001, 3000, 8080, 55001 };

            foreach (var port in claudePorts)
            {
                try
                {
                    var baseUrl = $"http://localhost:{port}";

                    // Проверяем доступность API
                    var healthResponse = await httpClient.GetAsync($"{baseUrl}/health", cancellationToken);
                    if (!healthResponse.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    _logger.LogDebug("Found Claude Code API on port {Port}", port);

                    // Выполняем команду через API
                    var requestBody = new
                    {
                        command = command,
                        workingDirectory = workingDirectory,
                        timeout = (int)_configuration.DefaultTimeout.TotalMilliseconds
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync($"{baseUrl}/execute", content, cancellationToken);
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        // Парсим ответ
                        var result = System.Text.Json.JsonSerializer.Deserialize<ApiExecutionResponse>(responseContent);
                        return new AgentExecutionResult
                        {
                            Success = result?.Success ?? false,
                            Output = result?.Output ?? responseContent,
                            ErrorMessage = result?.ErrorMessage
                        };
                    }
                    else
                    {
                        _logger.LogWarning("HTTP API execution failed on port {Port}: {StatusCode} - {Response}",
                            port, response.StatusCode, responseContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Port {Port} not available: {Error}", port, ex.Message);
                    continue;
                }
            }

            return null; // Не найден активный API
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP API execution failed, falling back to CLI");
            return null;
        }
    }

    private class ApiExecutionResponse
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string? ErrorMessage { get; set; }
    }

    #endregion

    #region CLI выполнение

    private async Task<AgentExecutionResult> ExecuteViaCli(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_configuration.DefaultCliPath))
        {
            _logger.LogError("Claude Code CLI not found at: {CliPath}", _configuration.DefaultCliPath);
            return new AgentExecutionResult
            {
                Success = false,
                Output = "",
                ErrorMessage = $"Claude Code CLI not found at: {_configuration.DefaultCliPath}"
            };
        }

        var arguments = PrepareCliArguments(command, workingDirectory);

        // Detailed logging for debugging
        _logger.LogInformation("=== Claude CLI Execution Details ===");
        _logger.LogInformation("CLI Path: {CliPath}", _configuration.DefaultCliPath);
        _logger.LogInformation("Working Directory: {WorkingDirectory}", workingDirectory);
        _logger.LogInformation("Directory Exists: {DirectoryExists}", Directory.Exists(workingDirectory));
        _logger.LogInformation("Arguments Length: {ArgumentsLength} chars", arguments.Length);
        _logger.LogInformation("Original Command: {Command}", command);
        _logger.LogInformation("Full Arguments: {Arguments}", arguments);
        _logger.LogInformation("Full Command Line: {FullCommandLine}", $"\"{_configuration.DefaultCliPath}\" {arguments}");
        _logger.LogInformation("====================================");

        // Set WorkingDirectory to ensure Claude CLI operates in the correct directory
        // Note: Originally avoided setting WorkingDirectory for .cmd files due to temp directory issues,
        // but setting it is necessary for file operations to work in the expected location
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _configuration.DefaultCliPath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory, // Set working directory for CLI execution
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true, // Redirect stdin to close it immediately
            CreateNoWindow = true
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        using var process = new Process { StartInfo = processStartInfo };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();

            // Close stdin immediately to prevent interactive prompts
            process.StandardInput.Close();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                // Use timeout-based cancellation instead of external cancellationToken
                // to prevent premature cancellation by test framework or Hangfire
                using var timeoutCts = new CancellationTokenSource(_configuration.DefaultTimeout);
                await process.WaitForExitAsync(timeoutCts.Token);

                // Wait for async event handlers to finish processing output
                // OutputDataReceived/ErrorDataReceived may still be processing data
                await Task.Delay(100);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Process timed out after {Timeout}, killing Claude Code CLI process", _configuration.DefaultTimeout);
                try
                {
                    if (!process.HasExited)
                    {
                        // Kill entire process tree to prevent zombie child processes (node.js from claude.cmd)
                        // Claude CLI spawns node.js processes that must be terminated
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception killEx)
                {
                    _logger.LogWarning(killEx, "Failed to kill process tree after timeout");
                }
                throw new TimeoutException($"Claude CLI process timed out after {_configuration.DefaultTimeout}");
            }


            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();
            var success = process.ExitCode == 0;

            // Always log output and errors for debugging
            _logger.LogInformation("=== Claude CLI Result ===");
            _logger.LogInformation("Exit Code: {ExitCode}", process.ExitCode);
            _logger.LogInformation("Output Length: {OutputLength} chars", output.Length);

            // CRITICAL: Log FULL output without truncation for debugging empty file issue
            if (!string.IsNullOrEmpty(output))
            {
                _logger.LogInformation("STDOUT (FULL): {Output}", output);
            }
            else
            {
                _logger.LogWarning("STDOUT is EMPTY");
            }

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("STDERR (FULL): {Error}", error);
            }

            // Log files in working directory after execution WITH CONTENTS
            try
            {
                if (Directory.Exists(workingDirectory))
                {
                    var files = Directory.GetFiles(workingDirectory, "*", SearchOption.AllDirectories);
                    _logger.LogInformation("Files in working directory after execution: {FileCount}", files.Length);
                    foreach (var file in files.Take(10))  // Log first 10 files
                    {
                        var relativePath = Path.GetRelativePath(workingDirectory, file);
                        var fileInfo = new FileInfo(file);

                        // CRITICAL: Log file contents to debug empty file issue
                        try
                        {
                            var fileContent = File.ReadAllText(file);
                            var contentPreview = fileContent.Length > 100 ? fileContent.Substring(0, 100) + "..." : fileContent;
                            _logger.LogInformation("  File: {FileName}, Size: {Size} bytes, Content: \"{Content}\"",
                                relativePath, fileInfo.Length, contentPreview);
                        }
                        catch (Exception readEx)
                        {
                            _logger.LogWarning("  File: {FileName}, Size: {Size} bytes, Content: <Could not read: {Error}>",
                                relativePath, fileInfo.Length, readEx.Message);
                        }
                    }
                    if (files.Length > 10)
                    {
                        _logger.LogInformation("  ... and {MoreFiles} more files", files.Length - 10);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not list files in working directory: {Error}", ex.Message);
            }

            _logger.LogInformation("========================");

            if (!success && _configuration.EnableVerboseLogging)
            {
                _logger.LogWarning("Claude Code CLI exited with code {ExitCode}. Error: {Error}",
                    process.ExitCode, error);
            }

            return new AgentExecutionResult
            {
                Success = success,
                Output = output,
                ErrorMessage = success ? null : error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Claude Code CLI process");

            return new AgentExecutionResult
            {
                Success = false,
                Output = outputBuilder.ToString(),
                ErrorMessage = $"Process execution failed: {ex.Message}"
            };
        }
    }

    private string PrepareCliArguments(string command, string workingDirectory)
    {
        var args = new StringBuilder();

        // IMPORTANT: Do NOT use --print flag as it prevents actual file operations
        // --print only shows what Claude would do, without executing
        // For real E2E tests we need actual file operations

        // Add output format for structured responses
        args.Append($"--output-format {_configuration.OutputFormat}");

        // CRITICAL: Skip permission checks for automated execution
        // This allows Claude to create/edit files without interactive prompts
        args.Append(" --dangerously-skip-permissions");

        // Add working directory as additional directory for tool access
        args.Append($" --add-dir \"{EscapeCliArgument(workingDirectory)}\"");

        // Add additional parameters from configuration
        foreach (var param in _configuration.AdditionalCliParameters)
        {
            args.Append($" --{param.Key} \"{EscapeCliArgument(param.Value)}\"");
        }

        // Add the command/prompt as the last argument (without --command flag)
        // Use -- separator to prevent --add-dir from consuming the prompt argument
        args.Append($" -- \"{EscapeCliArgument(command)}\"");

        return args.ToString();
    }

    private static string EscapeCliArgument(string argument)
    {
        // For Windows paths and command strings, we only need to escape internal quotes
        // Don't double backslashes as they're part of the path/command
        return argument.Replace("\"", "\\\"");
    }

    #endregion

    private class AgentExecutionResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string? ErrorMessage { get; set; }
    }
}