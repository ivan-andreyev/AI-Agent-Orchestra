using System.Text.Json;
using System.Text.RegularExpressions;
using Orchestra.Core.Models.Workflow;
using Orchestra.Core.Models.Workflow.Markdown;

namespace Orchestra.Core.Services;

/// <summary>
/// Преобразователь markdown workflow в стандартные WorkflowDefinition
/// </summary>
public class MarkdownToWorkflowConverter : IMarkdownToWorkflowConverter
{
    private static readonly Regex VariableRegex = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);
    private readonly Dictionary<string, WorkflowDefinition> _conversionCache = new();

    /// <summary>
    /// Преобразование markdown workflow в WorkflowDefinition
    /// </summary>
    public async Task<MarkdownToWorkflowConversionResult> ConvertAsync(MarkdownWorkflow markdownWorkflow, WorkflowConversionOptions? options = null)
    {
        options ??= new WorkflowConversionOptions();

        try
        {
            // Проверка кэша
            if (_conversionCache.TryGetValue(markdownWorkflow.FileHash, out var cachedWorkflow))
            {
                return new MarkdownToWorkflowConversionResult(
                    IsSuccess: true,
                    WorkflowDefinition: cachedWorkflow,
                    SourceMarkdownWorkflow: markdownWorkflow,
                    ConvertedAt: DateTime.UtcNow
                );
            }

            // Фаза 1: Предварительная валидация
            if (options.StrictValidation)
            {
                var validation = await ValidateConversionAsync(markdownWorkflow);
                if (!validation.CanConvert)
                {
                    var errors = string.Join("; ", validation.BlockingErrors);
                    return new MarkdownToWorkflowConversionResult(
                        IsSuccess: false,
                        ErrorMessage: $"Валидация не пройдена: {errors}",
                        SourceMarkdownWorkflow: markdownWorkflow,
                        ConvertedAt: DateTime.UtcNow
                    );
                }
            }

            // Фаза 2: Создание базового WorkflowDefinition
            var workflowDefinition = await CreateWorkflowDefinitionAsync(markdownWorkflow, options);

            // Кэширование результата
            _conversionCache[markdownWorkflow.FileHash] = workflowDefinition;

            return new MarkdownToWorkflowConversionResult(
                IsSuccess: true,
                WorkflowDefinition: workflowDefinition,
                SourceMarkdownWorkflow: markdownWorkflow,
                ConvertedAt: DateTime.UtcNow
            );
        }
        catch (WorkflowConversionException ex)
        {
            return new MarkdownToWorkflowConversionResult(
                IsSuccess: false,
                ErrorMessage: ex.GetDetailedMessage(),
                SourceMarkdownWorkflow: markdownWorkflow,
                ConvertedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            return new MarkdownToWorkflowConversionResult(
                IsSuccess: false,
                ErrorMessage: $"Неожиданная ошибка: {ex.Message}",
                SourceMarkdownWorkflow: markdownWorkflow,
                ConvertedAt: DateTime.UtcNow
            );
        }
    }

    /// <summary>
    /// Преобразование с расширенной валидацией структуры
    /// </summary>
    public async Task<MarkdownToWorkflowConversionResult> ConvertWithValidationAsync(MarkdownWorkflow markdownWorkflow, WorkflowConversionOptions? options = null)
    {
        options ??= new WorkflowConversionOptions();
        options.StrictValidation = true;
        options.IncludeWarnings = true;

        var result = await ConvertAsync(markdownWorkflow, options);

        // Дополнительная валидация результата
        if (result.IsSuccess && result.WorkflowDefinition != null)
        {
            await PerformExtendedValidationAsync(result.WorkflowDefinition);
        }

        return result;
    }

    /// <summary>
    /// Проверка возможности преобразования markdown workflow
    /// </summary>
    public async Task<WorkflowConversionValidation> ValidateConversionAsync(MarkdownWorkflow markdownWorkflow)
    {
        var validation = new WorkflowConversionValidation { CanConvert = true };

        try
        {
            // Проверка обязательных полей
            if (string.IsNullOrEmpty(markdownWorkflow.Name))
            {
                validation.AddBlockingError("Workflow должен иметь название");
            }

            if (!markdownWorkflow.Steps.Any())
            {
                validation.AddBlockingError("Workflow должен содержать хотя бы один шаг");
            }

            // Проверка структуры шагов
            foreach (var step in markdownWorkflow.Steps)
            {
                if (string.IsNullOrEmpty(step.Title))
                {
                    validation.AddBlockingError($"Шаг {step.Id} не имеет названия");
                }

                if (step.Type.ToLowerInvariant() == "task" && string.IsNullOrEmpty(step.Command))
                {
                    validation.AddWarning($"Шаг '{step.Title}' типа Task не имеет команды");
                }
            }

            // Проверка переменных
            var variableNames = new HashSet<string>();
            foreach (var variable in markdownWorkflow.Variables.Values)
            {
                if (string.IsNullOrEmpty(variable.Name))
                {
                    validation.AddBlockingError("Обнаружена переменная без имени");
                }
                else if (!variableNames.Add(variable.Name))
                {
                    validation.AddBlockingError($"Дублирующееся имя переменной: {variable.Name}");
                }
            }

            // Проверка зависимостей
            var stepIds = markdownWorkflow.Steps.Select(s => s.Id).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
            foreach (var step in markdownWorkflow.Steps)
            {
                foreach (var dependency in step.DependsOn)
                {
                    if (!stepIds.Contains(dependency))
                    {
                        validation.AddWarning($"Шаг '{step.Title}' зависит от неопределённого шага: {dependency}");
                    }
                }
            }

            // Проверка совместимости команд
            await ValidateCommandCompatibilityAsync(markdownWorkflow, validation);
        }
        catch (Exception ex)
        {
            validation.AddBlockingError($"Ошибка валидации: {ex.Message}");
        }

        return validation;
    }

    /// <summary>
    /// Оценка сложности преобразования для планирования ресурсов
    /// </summary>
    public async Task<ConversionComplexityMetrics> EstimateConversionComplexityAsync(MarkdownWorkflow markdownWorkflow)
    {
        var metrics = new ConversionComplexityMetrics
        {
            StepCount = markdownWorkflow.Steps.Count,
            VariableCount = markdownWorkflow.Variables.Count,
            DependencyCount = markdownWorkflow.Steps.Sum(s => s.DependsOn.Count),
            CommandTypeVariety = markdownWorkflow.Steps.Select(s => s.Type).Distinct().Count()
        };

        // Расчёт глубины вложенности
        metrics.MaxNestingDepth = CalculateMaxNestingDepth(markdownWorkflow.Steps);

        // Базовый рейтинг сложности
        var complexityFactors = new List<int>
        {
            Math.Min(metrics.StepCount / 10, 3),           // 0-3 за количество шагов
            Math.Min(metrics.VariableCount / 5, 2),       // 0-2 за переменные
            Math.Min(metrics.DependencyCount / 8, 2),     // 0-2 за зависимости
            Math.Min(metrics.MaxNestingDepth, 2),         // 0-2 за вложенность
            Math.Min(metrics.CommandTypeVariety / 3, 1)   // 0-1 за разнообразие команд
        };

        metrics.ComplexityRating = Math.Min(complexityFactors.Sum(), 10);

        // Оценки производительности
        metrics.EstimatedConversionTimeMs = CalculateEstimatedTime(metrics);
        metrics.EstimatedMemoryUsage = CalculateEstimatedMemory(metrics);

        // Факторы сложности
        if (metrics.StepCount > 50)
            metrics.ComplexityFactors.Add("Большое количество шагов");
        if (metrics.DependencyCount > 20)
            metrics.ComplexityFactors.Add("Сложная структура зависимостей");
        if (metrics.MaxNestingDepth > 3)
            metrics.ComplexityFactors.Add("Глубокая вложенность");

        // Рекомендации по оптимизации
        if (metrics.ComplexityRating > 7)
        {
            metrics.OptimizationRecommendations.Add("Рассмотрите разбиение на несколько workflow");
            metrics.OptimizationRecommendations.Add("Упростите структуру зависимостей");
        }

        return metrics;
    }

    // Приватные вспомогательные методы
    private async Task<WorkflowDefinition> CreateWorkflowDefinitionAsync(MarkdownWorkflow markdownWorkflow, WorkflowConversionOptions options)
    {
        // Создание метаданных workflow
        var metadata = new WorkflowMetadata(
            Author: markdownWorkflow.Metadata.Author,
            Description: markdownWorkflow.Metadata.Description ?? string.Empty,
            Version: markdownWorkflow.Metadata.Version,
            CreatedAt: markdownWorkflow.Metadata.CreatedAt ?? markdownWorkflow.ParsedAt,
            Tags: markdownWorkflow.Metadata.Tags
        );

        // Преобразование переменных markdown в VariableDefinition
        var variables = new Dictionary<string, VariableDefinition>();
        foreach (var markdownVar in markdownWorkflow.Variables.Values)
        {
            variables[markdownVar.Name] = new VariableDefinition(
                Name: markdownVar.Name,
                Type: markdownVar.Type,
                DefaultValue: markdownVar.DefaultValue,
                IsRequired: markdownVar.IsRequired,
                Description: markdownVar.Description
            );
        }

        // Преобразование шагов
        var steps = new List<WorkflowStep>();
        foreach (var markdownStep in markdownWorkflow.Steps)
        {
            var workflowStep = ConvertStep(markdownStep, options);
            steps.Add(workflowStep);
        }

        var workflowDefinition = new WorkflowDefinition(
            Id: markdownWorkflow.Id,
            Name: markdownWorkflow.Name,
            Steps: steps,
            Variables: variables,
            Metadata: metadata
        );

        return workflowDefinition;
    }

    private WorkflowStep ConvertStep(Orchestra.Core.Models.Workflow.MarkdownWorkflowStep markdownStep, WorkflowConversionOptions options)
    {
        var stepId = string.IsNullOrEmpty(markdownStep.Id) && options.GenerateStepIds
            ? $"{options.StepIdPrefix}{markdownStep.Title.Replace(" ", "_").ToLowerInvariant()}"
            : markdownStep.Id;

        var stepType = ConvertStepType(markdownStep.Type);
        var command = markdownStep.Command;
        var parameters = markdownStep.Parameters.ToDictionary(kv => kv.Key, kv => (object)kv.Value);

        // Обработка подстановки переменных
        if (options.ProcessVariableSubstitution)
        {
            command = ProcessVariableSubstitution(command);
            parameters = ProcessParameterSubstitution(parameters);
        }

        var workflowStep = new WorkflowStep(
            Id: stepId,
            Type: stepType,
            Command: command,
            Parameters: parameters,
            DependsOn: markdownStep.DependsOn,
            Condition: null, // Будет добавлено в следующих версиях
            RetryPolicy: null, // Будет добавлено в следующих версиях
            LoopDefinition: null, // Будет добавлено в следующих версиях
            NestedSteps: null // Будет добавлено в следующих версиях
        );

        return workflowStep;
    }

    private WorkflowStepType ConvertStepType(string markdownType)
    {
        return markdownType.ToLowerInvariant() switch
        {
            "task" => WorkflowStepType.Task,
            "condition" => WorkflowStepType.Condition,
            "loop" => WorkflowStepType.Loop,
            "parallel" => WorkflowStepType.Parallel,
            "start" => WorkflowStepType.Start,
            "end" => WorkflowStepType.End,
            _ => WorkflowStepType.Task
        };
    }

    private string ProcessVariableSubstitution(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return VariableRegex.Replace(input, match => $"${{variables.{match.Groups[1].Value}}}");
    }

    private Dictionary<string, object> ProcessParameterSubstitution(Dictionary<string, object> parameters)
    {
        var result = new Dictionary<string, object>();
        foreach (var (key, value) in parameters)
        {
            if (value is string stringValue)
            {
                result[key] = ProcessVariableSubstitution(stringValue);
            }
            else
            {
                result[key] = value;
            }
        }
        return result;
    }

    private async Task PerformExtendedValidationAsync(WorkflowDefinition workflowDefinition)
    {
        // Проверка циклических зависимостей
        if (HasCircularDependencies(workflowDefinition))
        {
            throw new WorkflowConversionException("Обнаружены циклические зависимости в workflow", ConversionPhase.ResultValidation);
        }

        // Проверка доступности команд
        foreach (var step in workflowDefinition.Steps)
        {
            if (string.IsNullOrEmpty(step.Command) && step.Type == WorkflowStepType.Task)
            {
                // Это предупреждение, а не ошибка
            }
        }
    }

    private bool HasCircularDependencies(WorkflowDefinition workflowDefinition)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var dependencies = workflowDefinition.Steps.ToDictionary(s => s.Id, s => s.DependsOn.ToArray());

        foreach (var step in workflowDefinition.Steps)
        {
            if (!visited.Contains(step.Id))
            {
                if (HasCircularDependenciesRecursive(step.Id, dependencies, visited, recursionStack))
                    return true;
            }
        }

        return false;
    }

    private bool HasCircularDependenciesRecursive(string stepId, Dictionary<string, string[]> dependencies, HashSet<string> visited, HashSet<string> recursionStack)
    {
        visited.Add(stepId);
        recursionStack.Add(stepId);

        if (dependencies.TryGetValue(stepId, out var stepDependencies))
        {
            foreach (var dependency in stepDependencies)
            {
                if (!visited.Contains(dependency))
                {
                    if (HasCircularDependenciesRecursive(dependency, dependencies, visited, recursionStack))
                        return true;
                }
                else if (recursionStack.Contains(dependency))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(stepId);
        return false;
    }

    private async Task ValidateCommandCompatibilityAsync(MarkdownWorkflow markdownWorkflow, WorkflowConversionValidation validation)
    {
        var supportedCommands = MarkdownWorkflowConstants.SupportedCommandTypes;
        var unsupportedCommands = new List<string>();

        foreach (var step in markdownWorkflow.Steps)
        {
            if (!string.IsNullOrEmpty(step.Command))
            {
                var commandType = step.Command.Split(' ').FirstOrDefault()?.ToLowerInvariant();
                if (commandType != null && !supportedCommands.Contains(commandType))
                {
                    unsupportedCommands.Add(commandType);
                }
            }
        }

        foreach (var unsupportedCommand in unsupportedCommands.Distinct())
        {
            validation.AddUnsupportedFeature($"Команда типа '{unsupportedCommand}' может не поддерживаться");
        }

        // Установка совместимости
        validation.Compatibility.SupportedCommandTypes.AddRange(supportedCommands);
        validation.Compatibility.UnsupportedCommandTypes.AddRange(unsupportedCommands.Distinct());
    }

    private int CalculateMaxNestingDepth(List<Orchestra.Core.Models.Workflow.MarkdownWorkflowStep> steps)
    {
        // Упрощенный расчёт глубины вложенности на основе зависимостей
        var maxDepth = 0;
        var stepDependencies = steps.ToDictionary(s => s.Id, s => s.DependsOn);

        foreach (var step in steps)
        {
            var depth = CalculateStepDepth(step.Id, stepDependencies, new HashSet<string>());
            maxDepth = Math.Max(maxDepth, depth);
        }

        return maxDepth;
    }

    private int CalculateStepDepth(string stepId, Dictionary<string, List<string>> dependencies, HashSet<string> visited)
    {
        if (visited.Contains(stepId) || !dependencies.TryGetValue(stepId, out var stepDeps))
            return 0;

        visited.Add(stepId);
        var maxDepth = 0;

        foreach (var dependency in stepDeps)
        {
            var depth = CalculateStepDepth(dependency, dependencies, visited);
            maxDepth = Math.Max(maxDepth, depth);
        }

        visited.Remove(stepId);
        return maxDepth + 1;
    }

    private int CalculateEstimatedTime(ConversionComplexityMetrics metrics)
    {
        // Базовое время: 10мс + время на обработку каждого компонента
        var baseTime = 10;
        var stepTime = metrics.StepCount * 2;
        var variableTime = metrics.VariableCount * 1;
        var dependencyTime = metrics.DependencyCount * 3;
        var nestingTime = metrics.MaxNestingDepth * 5;

        return baseTime + stepTime + variableTime + dependencyTime + nestingTime;
    }

    private long CalculateEstimatedMemory(ConversionComplexityMetrics metrics)
    {
        // Базовое потребление: 1KB + память на каждый компонент
        var baseMemory = 1024L;
        var stepMemory = metrics.StepCount * 500L;      // ~500 байт на шаг
        var variableMemory = metrics.VariableCount * 200L; // ~200 байт на переменную
        var dependencyMemory = metrics.DependencyCount * 100L; // ~100 байт на зависимость

        return baseMemory + stepMemory + variableMemory + dependencyMemory;
    }
}