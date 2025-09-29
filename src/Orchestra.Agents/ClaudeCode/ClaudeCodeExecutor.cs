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

        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"Working directory not found: {workingDirectory}");
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

        var processStartInfo = new ProcessStartInfo
        {
            FileName = _configuration.DefaultCliPath,
            Arguments = PrepareCliArguments(command, workingDirectory),
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(_configuration.DefaultTimeout);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Process timed out, killing Claude Code CLI process");
                process.Kill();
                throw;
            }


            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();
            var success = process.ExitCode == 0;

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

        // Добавляем основную команду
        args.Append($"--command \"{EscapeCliArgument(command)}\"");

        // Добавляем рабочую директорию
        args.Append($" --working-directory \"{EscapeCliArgument(workingDirectory)}\"");

        // Добавляем формат вывода
        args.Append($" --output-format {_configuration.OutputFormat}");

        // Добавляем дополнительные параметры из конфигурации
        foreach (var param in _configuration.AdditionalCliParameters)
        {
            args.Append($" --{param.Key} \"{EscapeCliArgument(param.Value)}\"");
        }

        return args.ToString();
    }

    private static string EscapeCliArgument(string argument)
    {
        return argument.Replace("\"", "\\\"").Replace("\\", "\\\\");
    }

    #endregion

    private class AgentExecutionResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string? ErrorMessage { get; set; }
    }
}