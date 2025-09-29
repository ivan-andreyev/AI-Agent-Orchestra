using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Services;
using System.Diagnostics;
using System.Text;

namespace Orchestra.Agents.ClaudeCode;

/// <summary>
/// Основной сервис для взаимодействия с агентами Claude Code через CLI
/// </summary>
public class ClaudeCodeService : IClaudeCodeService, IClaudeCodeCoreService
{
    private readonly ClaudeCodeConfiguration _configuration;
    private readonly ILogger<ClaudeCodeService> _logger;
    private readonly IAgentExecutor _agentExecutor;

    /// <summary>
    /// Инициализирует новый экземпляр ClaudeCodeService
    /// </summary>
    /// <param name="configuration">Конфигурация Claude Code агента</param>
    /// <param name="logger">Логгер для отслеживания операций</param>
    /// <param name="agentExecutor">Исполнитель команд агента</param>
    /// <exception cref="ArgumentNullException">Если любой из обязательных параметров равен null</exception>
    public ClaudeCodeService(
        IOptions<ClaudeCodeConfiguration> configuration,
        ILogger<ClaudeCodeService> logger,
        IAgentExecutor agentExecutor)
    {
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentExecutor = agentExecutor ?? throw new ArgumentNullException(nameof(agentExecutor));
    }

    /// <inheritdoc />
    public async Task<bool> IsAgentAvailableAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID cannot be null or empty", nameof(agentId));
        }

        _logger.LogDebug("Checking availability for Claude Code agent: {AgentId}", agentId);

        try
        {
            // Проверяем доступность через прямое выполнение команды версии
            var workingDirectory = _configuration.DefaultWorkingDirectory ?? Environment.CurrentDirectory;
            var versionCommand = "Provide your Claude Code CLI version information.";

            var result = await _agentExecutor.ExecuteCommandAsync(
                versionCommand,
                workingDirectory,
                cancellationToken);

            var isAvailable = result.Success;

            _logger.LogDebug("Claude Code agent {AgentId} availability check result: {IsAvailable}",
                agentId, isAvailable);

            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check availability for Claude Code agent: {AgentId}", agentId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetAgentVersionAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID cannot be null or empty", nameof(agentId));
        }

        _logger.LogDebug("Getting version for Claude Code agent: {AgentId}", agentId);

        try
        {
            // Используем IAgentExecutor для выполнения команды версии
            var workingDirectory = _configuration.DefaultWorkingDirectory ?? Environment.CurrentDirectory;
            var versionCommand = "Provide your Claude Code CLI version information.";

            var result = await _agentExecutor.ExecuteCommandAsync(
                versionCommand,
                workingDirectory,
                cancellationToken);

            if (result.Success)
            {
                _logger.LogDebug("Successfully retrieved version for agent {AgentId}: {Version}",
                    agentId, result.Output);
                return result.Output;
            }

            _logger.LogWarning("Failed to get version for agent {AgentId}: {Error}",
                agentId, result.ErrorMessage);
            return $"Version unavailable: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting version for Claude Code agent: {AgentId}", agentId);
            return $"Version check failed: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public async Task<ClaudeCodeExecutionResult> ExecuteCommandAsync(
        string agentId,
        string command,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        // Валидация входных параметров
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID cannot be null or empty", nameof(agentId));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command cannot be null or empty", nameof(command));
        }

        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        _logger.LogInformation("Executing command for Claude Code agent {AgentId}: {Command}",
            agentId, command.Length > 100 ? command.Substring(0, 100) + "..." : command);

        var startTime = DateTime.UtcNow;
        var executedSteps = new List<string>();

        try
        {
            // Подготовка параметров выполнения
            executedSteps.Add("Parameter validation completed");

            var workingDirectory = ExtractWorkingDirectory(parameters);
            var timeout = ExtractTimeout(parameters);
            var allowedTools = ExtractAllowedTools(parameters);

            executedSteps.Add($"Working directory resolved: {workingDirectory}");
            executedSteps.Add($"Timeout configured: {timeout}");
            executedSteps.Add($"Allowed tools: {string.Join(", ", allowedTools)}");

            // Task management integration is handled by the orchestration layer (TaskExecutionJob)
            // through parameters passed in the execution context, keeping this service focused on Claude Code execution
            if (parameters.TryGetValue("TaskId", out var taskIdObj) && taskIdObj is string taskId)
            {
                executedSteps.Add($"Task execution context - TaskId: {taskId}");
            }

            // Выполнение команды через IAgentExecutor
            var result = await ExecuteWithRetry(command, workingDirectory, cancellationToken);

            var executionTime = DateTime.UtcNow - startTime;
            executedSteps.Add($"Command execution completed in {executionTime.TotalSeconds:F2} seconds");

            return new ClaudeCodeExecutionResult
            {
                AgentId = agentId,
                Success = result.Success,
                Output = result.Output,
                ErrorMessage = result.ErrorMessage,
                ExecutionTime = executionTime,
                ExecutedSteps = executedSteps,
                WorkflowMetadata = ExtractWorkflowMetadata(parameters),
                Metadata = CombineMetadata(result.Metadata, parameters)
            };
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - startTime;
            executedSteps.Add($"Execution failed with exception: {ex.Message}");

            _logger.LogError(ex, "Command execution failed for Claude Code agent {AgentId}", agentId);

            return new ClaudeCodeExecutionResult
            {
                AgentId = agentId,
                Success = false,
                Output = "",
                ErrorMessage = $"Command execution failed: {ex.Message}",
                ExecutionTime = executionTime,
                ExecutedSteps = executedSteps,
                WorkflowMetadata = ExtractWorkflowMetadata(parameters),
                Metadata = new Dictionary<string, object> { { "Exception", ex.GetType().Name } }
            };
        }
    }

    /// <inheritdoc />
    public async Task<ClaudeCodeWorkflowResult> ExecuteWorkflowAsync(
        string agentId,
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID cannot be null or empty", nameof(agentId));
        }

        if (workflow == null)
        {
            throw new ArgumentNullException(nameof(workflow));
        }

        _logger.LogInformation("Executing workflow {WorkflowId} for Claude Code agent {AgentId}",
            workflow.Id, agentId);

        var startTime = DateTime.UtcNow;
        var executedSteps = new List<string>();
        var stepResults = new List<WorkflowStepResult>();

        // Валидация workflow - позволяем исключениям валидации пробрасываться напрямую
        executedSteps.Add("Workflow validation started");
        await ValidateWorkflow(workflow);
        executedSteps.Add("Workflow validation completed");

        try
        {

            // Подготовка команды для выполнения workflow
            var workflowCommand = await PrepareWorkflowCommand(workflow);
            executedSteps.Add("Workflow command prepared");

            // Параметры для выполнения
            var parameters = new Dictionary<string, object>
            {
                { "WorkingDirectory", workflow.WorkingDirectory },
                { "Timeout", workflow.Timeout },
                { "AllowedTools", workflow.AllowedTools },
                { "WorkflowId", workflow.Id },
                { "WorkflowName", workflow.Name }
            };

            // Выполнение workflow
            var result = await ExecuteCommandAsync(agentId, workflowCommand, parameters, cancellationToken);

            var executionTime = DateTime.UtcNow - startTime;
            executedSteps.Add($"Workflow execution completed in {executionTime.TotalSeconds:F2} seconds");

            // Создание результата workflow
            var workflowResult = new ClaudeCodeWorkflowResult
            {
                AgentId = agentId,
                WorkflowId = workflow.Id,
                Success = result.Success,
                Output = result.Output,
                ErrorMessage = result.ErrorMessage,
                ExecutionTime = executionTime,
                ExecutedSteps = executedSteps,
                WorkflowMetadata = result.WorkflowMetadata,
                Metadata = result.Metadata,
                TotalSteps = 1, // Для простого workflow пока считаем как 1 шаг
                CompletedSteps = result.Success ? 1 : 0,
                SkippedSteps = 0,
                FailedSteps = result.Success ? 0 : 1,
                StepResults = CreateStepResults(result, workflow)
            };

            return workflowResult;
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - startTime;
            executedSteps.Add($"Workflow execution failed: {ex.Message}");

            _logger.LogError(ex, "Workflow execution failed for agent {AgentId}, workflow {WorkflowId}",
                agentId, workflow.Id);

            return new ClaudeCodeWorkflowResult
            {
                AgentId = agentId,
                WorkflowId = workflow.Id,
                Success = false,
                Output = "",
                ErrorMessage = $"Workflow execution failed: {ex.Message}",
                ExecutionTime = executionTime,
                ExecutedSteps = executedSteps,
                WorkflowMetadata = new Dictionary<string, object>(),
                Metadata = new Dictionary<string, object> { { "Exception", ex.GetType().Name } },
                TotalSteps = 1,
                CompletedSteps = 0,
                SkippedSteps = 0,
                FailedSteps = 1,
                StepResults = new List<WorkflowStepResult>
                {
                    new WorkflowStepResult
                    {
                        StepNumber = 1,
                        StepName = workflow.Name,
                        Status = WorkflowStepStatus.Failed,
                        Output = "",
                        ErrorMessage = ex.Message,
                        ExecutionTime = executionTime
                    }
                }
            };
        }
    }

    #region Приватные методы

    private async Task<AgentExecutionResponse> ExecuteWithRetry(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var attempts = 0;
        Exception? lastException = null;

        while (attempts <= _configuration.RetryAttempts)
        {
            try
            {
                var result = await _agentExecutor.ExecuteCommandAsync(command, workingDirectory, cancellationToken);

                // Если команда выполнилась успешно или это последняя попытка
                if (result.Success || attempts == _configuration.RetryAttempts)
                {
                    return result;
                }

                _logger.LogWarning("Command execution attempt {Attempt} failed, retrying in {Delay}ms",
                    attempts + 1, _configuration.RetryDelay.TotalMilliseconds);

                await Task.Delay(_configuration.RetryDelay, cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Command execution attempt {Attempt} threw exception", attempts + 1);

                if (attempts == _configuration.RetryAttempts)
                {
                    break;
                }

                await Task.Delay(_configuration.RetryDelay, cancellationToken);
            }

            attempts++;
        }

        // Если все попытки исчерпаны
        if (lastException != null)
        {
            throw lastException;
        }

        return new AgentExecutionResponse
        {
            Success = false,
            Output = "",
            ErrorMessage = "All retry attempts exhausted",
            ExecutionTime = TimeSpan.Zero
        };
    }

    private string ExtractWorkingDirectory(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("WorkingDirectory", out var workingDir) && workingDir is string dir)
        {
            return dir;
        }

        return _configuration.DefaultWorkingDirectory ?? Environment.CurrentDirectory;
    }

    private TimeSpan ExtractTimeout(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Timeout", out var timeoutObj))
        {
            if (timeoutObj is TimeSpan timeout)
            {
                return timeout;
            }

            if (timeoutObj is int timeoutSeconds)
            {
                return TimeSpan.FromSeconds(timeoutSeconds);
            }
        }

        return _configuration.DefaultTimeout;
    }

    private string[] ExtractAllowedTools(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("AllowedTools", out var toolsObj) && toolsObj is string[] tools)
        {
            return tools;
        }

        return _configuration.AllowedTools;
    }

    private Dictionary<string, object> ExtractWorkflowMetadata(Dictionary<string, object> parameters)
    {
        var metadata = new Dictionary<string, object>();

        if (parameters.TryGetValue("WorkflowId", out var workflowId))
        {
            metadata["WorkflowId"] = workflowId;
        }

        if (parameters.TryGetValue("WorkflowName", out var workflowName))
        {
            metadata["WorkflowName"] = workflowName;
        }

        return metadata;
    }

    private Dictionary<string, object> CombineMetadata(
        Dictionary<string, object> existingMetadata,
        Dictionary<string, object> parameters)
    {
        var combined = new Dictionary<string, object>(existingMetadata);

        // Добавляем специфичные для Claude Code метаданные
        combined["AgentType"] = "Claude Code";
        combined["ConfigurationUsed"] = true;

        if (parameters.TryGetValue("WorkflowId", out var workflowId))
        {
            combined["WorkflowId"] = workflowId;
        }

        return combined;
    }

    private async Task ValidateWorkflow(WorkflowDefinition workflow)
    {
        if (!File.Exists(workflow.MarkdownFilePath))
        {
            throw new FileNotFoundException($"Workflow markdown file not found: {workflow.MarkdownFilePath}");
        }

        if (!Directory.Exists(workflow.WorkingDirectory))
        {
            throw new DirectoryNotFoundException($"Workflow working directory not found: {workflow.WorkingDirectory}");
        }

        // Дополнительные проверки workflow
        await Task.CompletedTask; // Placeholder для будущих асинхронных проверок
    }

    private async Task<string> PrepareWorkflowCommand(WorkflowDefinition workflow)
    {
        // Читаем содержимое markdown файла и подготавливаем команду
        var markdownContent = await File.ReadAllTextAsync(workflow.MarkdownFilePath);

        var command = new StringBuilder();
        command.AppendLine($"Execute the following workflow: {workflow.Name}");
        command.AppendLine($"Description: {workflow.Description}");
        command.AppendLine("Workflow content:");
        command.AppendLine(markdownContent);

        return command.ToString();
    }

    private List<WorkflowStepResult> CreateStepResults(ClaudeCodeExecutionResult result, WorkflowDefinition workflow)
    {
        return new List<WorkflowStepResult>
        {
            new WorkflowStepResult
            {
                StepNumber = 1,
                StepName = workflow.Name,
                Status = result.Success ? WorkflowStepStatus.Completed : WorkflowStepStatus.Failed,
                Output = result.Output,
                ErrorMessage = result.ErrorMessage,
                ExecutionTime = result.ExecutionTime
            }
        };
    }

    #endregion
}