using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Interfaces;
using Orchestra.Core.Models;
using Orchestra.Core.Services;

namespace Orchestra.Agents.ClaudeCode;

/// <summary>
/// Исполнитель рабочих процессов Claude Code для обработки markdown-based workflow
/// </summary>
public class ClaudeCodeWorkflowExecutor : IWorkflowExecutor
{
    private readonly IAgentExecutor _agentExecutor;
    private readonly ClaudeCodeConfiguration _configuration;
    private readonly ILogger<ClaudeCodeWorkflowExecutor> _logger;

    public ClaudeCodeWorkflowExecutor(
        IAgentExecutor agentExecutor,
        IOptions<ClaudeCodeConfiguration> configuration,
        ILogger<ClaudeCodeWorkflowExecutor> logger)
    {
        _agentExecutor = agentExecutor ?? throw new ArgumentNullException(nameof(agentExecutor));
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Выполнить рабочий процесс по определению
    /// </summary>
    /// <param name="workflowDefinition">Определение рабочего процесса</param>
    /// <param name="parameters">Параметры выполнения</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат выполнения рабочего процесса</returns>
    public async Task<Orchestra.Core.Models.WorkflowExecutionResult> ExecuteWorkflowAsync(
        Orchestra.Core.Models.WorkflowDefinition workflowDefinition,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinition);
        parameters ??= new Dictionary<string, object>();

        _logger.LogInformation("Starting workflow execution: {WorkflowId}", workflowDefinition.Id);

        var startTime = DateTime.UtcNow;
        var stepResults = new List<Orchestra.Core.Models.WorkflowStepResult>();

        try
        {
            // Обрабатываем шаги в порядке, определенном Order
            var orderedSteps = workflowDefinition.Steps.OrderBy(s => s.Order).ToList();

            foreach (var step in orderedSteps)
            {
                // Проверяем условия выполнения шага
                if (!ShouldExecuteStep(step, parameters))
                {
                    _logger.LogDebug("Skipping workflow step due to conditions: {StepId}", step.Id);
                    continue;
                }

                _logger.LogDebug("Executing workflow step: {StepId}", step.Id);

                var stepStartTime = DateTime.UtcNow;
                var stepResult = await ExecuteWorkflowStepAsync(step, parameters, cancellationToken, stepStartTime);
                var stepDuration = DateTime.UtcNow - stepStartTime;

                stepResult = stepResult with { Duration = stepDuration };
                stepResults.Add(stepResult);

                _logger.LogDebug("Workflow step completed: {StepId}, Success: {Success}",
                    step.Id, stepResult.IsSuccess);

                if (!stepResult.IsSuccess && step.IsRequired)
                {
                    _logger.LogWarning("Required workflow step failed: {StepId}, Error: {Error}",
                        step.Id, stepResult.ErrorMessage);

                    return new Orchestra.Core.Models.WorkflowExecutionResult
                    {
                        IsSuccess = false,
                        WorkflowId = workflowDefinition.Id,
                        ExecutionId = Guid.NewGuid().ToString(),
                        StartTime = startTime,
                        EndTime = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime,
                        StepResults = stepResults,
                        ErrorMessage = $"Required step '{step.Id}' failed: {stepResult.ErrorMessage}"
                    };
                }

                // Обновляем параметры с результатами шага для следующих шагов
                if (stepResult.IsSuccess && !string.IsNullOrEmpty(stepResult.Output))
                {
                    parameters[$"step_{step.Id}_output"] = stepResult.Output;
                }
            }

            var totalDuration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Workflow execution completed: {WorkflowId}, Duration: {Duration}",
                workflowDefinition.Id, totalDuration);

            return new Orchestra.Core.Models.WorkflowExecutionResult
            {
                IsSuccess = true,
                WorkflowId = workflowDefinition.Id,
                ExecutionId = Guid.NewGuid().ToString(),
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                Duration = totalDuration,
                StepResults = stepResults,
                Output = string.Join("\n", stepResults.Where(r => r.IsSuccess).Select(r => r.Output))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow execution failed: {WorkflowId}", workflowDefinition.Id);

            return new Orchestra.Core.Models.WorkflowExecutionResult
            {
                IsSuccess = false,
                WorkflowId = workflowDefinition.Id,
                ExecutionId = Guid.NewGuid().ToString(),
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime,
                StepResults = stepResults,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Выполнить отдельный шаг рабочего процесса
    /// </summary>
    private async Task<Orchestra.Core.Models.WorkflowStepResult> ExecuteWorkflowStepAsync(
        Orchestra.Core.Models.WorkflowStep step,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken,
        DateTime stepStartTime)
    {
        try
        {
            // Подготавливаем команду с подстановкой параметров
            var command = SubstituteParameters(step.Command, parameters);

            _logger.LogDebug("Executing step command: {Command}", command);

            // Выполняем команду через агент
            var executionResult = await _agentExecutor.ExecuteCommandAsync(
                command,
                parameters.GetValueOrDefault("workingDirectory", Environment.CurrentDirectory).ToString()!,
                cancellationToken);

            var endTime = DateTime.UtcNow;

            return new Orchestra.Core.Models.WorkflowStepResult
            {
                StepId = step.Id,
                IsSuccess = executionResult.Success,
                Output = executionResult.Output,
                ErrorMessage = executionResult.ErrorMessage ?? string.Empty,
                StartTime = stepStartTime,
                EndTime = endTime
            };
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            _logger.LogError(ex, "Error executing workflow step: {StepId}", step.Id);

            return new Orchestra.Core.Models.WorkflowStepResult
            {
                StepId = step.Id,
                IsSuccess = false,
                Output = string.Empty,
                ErrorMessage = ex.Message,
                StartTime = stepStartTime,
                EndTime = endTime
            };
        }
    }

    /// <summary>
    /// Проверяет, следует ли выполнить шаг на основе условий
    /// </summary>
    private static bool ShouldExecuteStep(Orchestra.Core.Models.WorkflowStep step, Dictionary<string, object> parameters)
    {
        if (step.Conditions.Count == 0)
        {
            return true;
        }

        foreach (var condition in step.Conditions)
        {
            var parameterValue = parameters.GetValueOrDefault(condition.Key);
            var expectedValue = condition.Value;

            // Простая проверка равенства
            if (!AreValuesEqual(parameterValue, expectedValue))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Сравнивает значения для условий
    /// </summary>
    private static bool AreValuesEqual(object? actual, object? expected)
    {
        if (actual == null && expected == null)
        {
            return true;
        }

        if (actual == null || expected == null)
        {
            return false;
        }

        // Конвертируем оба значения в строки для сравнения
        var actualString = actual.ToString();
        var expectedString = expected.ToString();

        return string.Equals(actualString, expectedString, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Подставляет параметры в команду
    /// </summary>
    private static string SubstituteParameters(string command, Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(command) || parameters.Count == 0)
        {
            return command;
        }

        var result = command;
        foreach (var parameter in parameters)
        {
            var placeholder = $"{{{parameter.Key}}}";
            var value = parameter.Value?.ToString() ?? string.Empty;
            result = result.Replace(placeholder, value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}