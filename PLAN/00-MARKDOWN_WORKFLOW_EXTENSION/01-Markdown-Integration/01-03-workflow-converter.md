# Markdown to Workflow Converter - Technical Specification

**Status**: [ ] In Progress
**Parent Task**: [01-Markdown-Integration.md](../01-Markdown-Integration.md)
**Priority**: CRITICAL
**Complexity**: 30 minutes
**Type**: Complex Implementation Task

## Overview

This document provides a comprehensive technical specification for implementing a markdown to JSON WorkflowDefinition converter that transforms parsed `MarkdownWorkflowDocument` objects into standard `WorkflowDefinition` objects compatible with the existing `IWorkflowEngine` infrastructure.

## Architecture Requirements

### System Dependencies
- [x] Markdown workflow data models available (01-01-markdown-models.md) ✅ COMPLETE
- [x] Markdown parser implementation ready (01-02-markdown-parser.md) ✅ COMPLETE
- [x] Existing WorkflowDefinition models accessible ✅ COMPLETE
- [x] Orchestra.Core.Models.Workflow namespace available ✅ COMPLETE

### Implementation Deliverables
- [ ] `IMarkdownToWorkflowConverter.cs` - Converter interface definition (in Orchestra.Core.Services)
- [ ] `MarkdownToWorkflowConverter.cs` - Main converter implementation (in Orchestra.Core.Services)
- [ ] `WorkflowConversionOptions.cs` - Conversion configuration settings (in Orchestra.Core.Services)
- [ ] `WorkflowConversionValidation.cs` - Validation result model (in Orchestra.Core.Services)
- [ ] `WorkflowConversionException.cs` - Specialized exception handling (in Orchestra.Core.Services)
- [ ] `MarkdownToWorkflowConverterTests.cs` - Comprehensive unit test suite (in Orchestra.Tests.Core.Services)

## Technical Implementation Guide

### Component Architecture

The markdown to workflow converter consists of six main components designed for extensibility, performance, and maintainability:

### 1. Core Converter Interface (IMarkdownToWorkflowConverter)

**Purpose**: Defines the contract for converting markdown workflows to standard workflow definitions using existing system models.

**Key Methods**:
- `ConvertAsync()` - Convert MarkdownWorkflow to WorkflowDefinition
- `ConvertWithValidationAsync()` - Convert with structural validation
- `ValidateConversionAsync()` - Pre-validate conversion feasibility

**Implementation**:
```csharp
// Файл: IMarkdownToWorkflowConverter.cs
namespace Orchestra.Core.Services
{
    /// <summary>
    /// Интерфейс для преобразования markdown workflow в стандартные WorkflowDefinition
    /// </summary>
    public interface IMarkdownToWorkflowConverter
    {
        /// <summary>
        /// Преобразование markdown workflow в WorkflowDefinition
        /// </summary>
        /// <param name="markdownWorkflow">Исходный markdown workflow</param>
        /// <param name="options">Настройки преобразования</param>
        /// <returns>Результат преобразования с валидацией</returns>
        Task<MarkdownToWorkflowConversionResult> ConvertAsync(MarkdownWorkflow markdownWorkflow, WorkflowConversionOptions? options = null);

        /// <summary>
        /// Преобразование с расширенной валидацией структуры
        /// </summary>
        /// <param name="markdownWorkflow">Исходный markdown workflow</param>
        /// <param name="options">Настройки преобразования</param>
        /// <returns>Результат преобразования с детальной валидацией</returns>
        Task<MarkdownToWorkflowConversionResult> ConvertWithValidationAsync(MarkdownWorkflow markdownWorkflow, WorkflowConversionOptions? options = null);

        /// <summary>
        /// Проверка возможности преобразования markdown workflow
        /// </summary>
        /// <param name="markdownWorkflow">Markdown workflow для проверки</param>
        /// <returns>Результат валидации без преобразования</returns>
        Task<WorkflowConversionValidation> ValidateConversionAsync(MarkdownWorkflow markdownWorkflow);
    }
}
```

### 2. Conversion Configuration (WorkflowConversionOptions)

**Purpose**: Configurable options for conversion behavior, validation rules, and output formatting.

**Key Properties**:
- `StrictValidation` - Enable/disable strict structure validation
- `PreserveMetadata` - Include markdown metadata in workflow
- `VariableSubstitution` - Process {{variable}} placeholders
- `DependencyResolution` - Resolve step dependencies automatically
- `GenerateStepIds` - Auto-generate missing step identifiers

**Configuration Model**:
```csharp
// Файл: WorkflowConversionOptions.cs
namespace Orchestra.Core.Services
{
    /// <summary>
    /// Настройки преобразования markdown workflow в WorkflowDefinition
    /// </summary>
    public class WorkflowConversionOptions
    {
        /// <summary>Строгая валидация структуры</summary>
        public bool StrictValidation { get; set; } = true;

        /// <summary>Сохранять метаданные markdown в workflow</summary>
        public bool PreserveMetadata { get; set; } = true;

        /// <summary>Обрабатывать подстановку переменных {{variable}}</summary>
        public bool ProcessVariableSubstitution { get; set; } = true;

        /// <summary>Автоматически разрешать зависимости между шагами</summary>
        public bool ResolveDependencies { get; set; } = true;

        /// <summary>Генерировать идентификаторы для шагов без ID</summary>
        public bool GenerateStepIds { get; set; } = true;

        /// <summary>Валидировать команды шагов на существование</summary>
        public bool ValidateCommands { get; set; } = false;

        /// <summary>Максимальное время выполнения преобразования (секунды)</summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>Базовый путь для разрешения относительных ссылок</summary>
        public string BasePath { get; set; } = string.Empty;

        /// <summary>Префикс для генерируемых идентификаторов шагов</summary>
        public string StepIdPrefix { get; set; } = "step_";

        /// <summary>Включать предупреждения в результат преобразования</summary>
        public bool IncludeWarnings { get; set; } = true;

        /// <summary>Формат даты для преобразования дат в строки</summary>
        public string DateFormat { get; set; } = "yyyy-MM-ddTHH:mm:ssZ";
    }
}
```

### 3. Conversion Result Model (WorkflowConversionResult)

**Purpose**: Comprehensive conversion result with detailed feedback, performance metrics, and validation information.

**Features**:
- Successful conversion result with WorkflowDefinition
- Error and warning collection with context
- Performance metrics (conversion time, complexity)
- Structural analysis (steps converted, variables processed)
- Conversion success indicators

**Result Model**: Uses existing `MarkdownToWorkflowConversionResult` from Orchestra.Core.Models.Workflow namespace.

The existing model already provides:
- `IsSuccess` - Success indicator
- `WorkflowDefinition` - Result definition
- `SourceMarkdownWorkflow` - Source workflow reference
- `ErrorMessage` - Error information
- `ConvertedAt` - Conversion timestamp

Additional validation data will be included in the `ErrorMessage` field or via extended result handling.
```

### 4. Conversion Validation (WorkflowConversionValidation)

**Purpose**: Pre-conversion validation to identify potential issues without full processing.

**Features**:
- Structural validation of markdown document
- Variable dependency checking
- Command validity assessment
- Compatibility analysis with existing workflow engine

**Validation Model**:
```csharp
// Файл: WorkflowConversionValidation.cs
namespace Orchestra.Core.Services
{
    /// <summary>
    /// Результат валидации возможности преобразования markdown workflow
    /// </summary>
    public class WorkflowConversionValidation
    {
        /// <summary>Можно ли преобразовать документ</summary>
        public bool CanConvert { get; set; }

        /// <summary>Блокирующие ошибки</summary>
        public List<string> BlockingErrors { get; set; } = new();

        /// <summary>Предупреждения о потенциальных проблемах</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>Неподдерживаемые функции</summary>
        public List<string> UnsupportedFeatures { get; set; } = new();

        /// <summary>Рекомендации по исправлению</summary>
        public List<string> Recommendations { get; set; } = new();

        /// <summary>Совместимость с существующим WorkflowEngine</summary>
        public WorkflowEngineCompatibility Compatibility { get; set; } = new();

        /// <summary>Добавить блокирующую ошибку</summary>
        public void AddBlockingError(string error)
        {
            BlockingErrors.Add(error);
            CanConvert = false;
        }

        /// <summary>Добавить предупреждение</summary>
        public void AddWarning(string warning) => Warnings.Add(warning);

        /// <summary>Добавить неподдерживаемую функцию</summary>
        public void AddUnsupportedFeature(string feature) => UnsupportedFeatures.Add(feature);

        /// <summary>Добавить рекомендацию</summary>
        public void AddRecommendation(string recommendation) => Recommendations.Add(recommendation);
    }

    /// <summary>
    /// Анализ совместимости с WorkflowEngine
    /// </summary>
    public class WorkflowEngineCompatibility
    {
        /// <summary>Совместимость версии</summary>
        public bool VersionCompatible { get; set; } = true;

        /// <summary>Поддерживаемые типы команд</summary>
        public List<string> SupportedCommandTypes { get; set; } = new();

        /// <summary>Неподдерживаемые типы команд</summary>
        public List<string> UnsupportedCommandTypes { get; set; } = new();

        /// <summary>Ограничения по сложности</summary>
        public string ComplexityLimitations { get; set; } = string.Empty;
    }
}
```

### 5. Conversion Complexity Metrics (ConversionComplexityMetrics)

**Purpose**: Performance analysis and resource planning for conversion operations.

**Metrics Model**:
```csharp
// Файл: ConversionComplexityMetrics.cs
namespace Orchestra.Core.Services
{
    /// <summary>
    /// Метрики сложности преобразования workflow
    /// </summary>
    public class ConversionComplexityMetrics
    {
        /// <summary>Общий рейтинг сложности (1-10)</summary>
        public int ComplexityRating { get; set; }

        /// <summary>Количество шагов для преобразования</summary>
        public int StepCount { get; set; }

        /// <summary>Количество переменных</summary>
        public int VariableCount { get; set; }

        /// <summary>Количество зависимостей между шагами</summary>
        public int DependencyCount { get; set; }

        /// <summary>Максимальная глубина вложенности</summary>
        public int MaxNestingDepth { get; set; }

        /// <summary>Количество различных типов команд</summary>
        public int CommandTypeVariety { get; set; }

        /// <summary>Предполагаемое время преобразования (миллисекунды)</summary>
        public int EstimatedConversionTimeMs { get; set; }

        /// <summary>Требуемые ресурсы памяти (байты)</summary>
        public long EstimatedMemoryUsage { get; set; }

        /// <summary>Факторы, влияющие на сложность</summary>
        public List<string> ComplexityFactors { get; set; } = new();

        /// <summary>Рекомендации по оптимизации</summary>
        public List<string> OptimizationRecommendations { get; set; } = new();
    }
}
```

### 6. Exception Handling (WorkflowConversionException)

**Purpose**: Specialized exception for detailed error reporting with conversion context.

**Features**:
- Document context preservation
- Step-level error tracking
- Detailed error message formatting
- Support for nested conversion errors

**Exception Class**:
```csharp
// Файл: WorkflowConversionException.cs
namespace Orchestra.Core.Services
{
    /// <summary>
    /// Исключение при преобразовании markdown workflow в WorkflowDefinition
    /// </summary>
    public class WorkflowConversionException : Exception
    {
        /// <summary>Идентификатор исходного документа</summary>
        public Guid? SourceDocumentId { get; }

        /// <summary>Путь к исходному файлу</summary>
        public string? SourceFilePath { get; }

        /// <summary>Идентификатор шага, где произошла ошибка</summary>
        public string? StepId { get; }

        /// <summary>Название переменной, связанной с ошибкой</summary>
        public string? VariableName { get; }

        /// <summary>Фаза преобразования, где произошла ошибка</summary>
        public ConversionPhase ConversionPhase { get; }

        /// <summary>Дополнительные детали ошибки</summary>
        public Dictionary<string, object> ErrorDetails { get; } = new();

        public WorkflowConversionException(string message, ConversionPhase phase = ConversionPhase.Unknown) : base(message)
        {
            ConversionPhase = phase;
        }

        public WorkflowConversionException(string message, Exception innerException, ConversionPhase phase = ConversionPhase.Unknown) : base(message, innerException)
        {
            ConversionPhase = phase;
        }

        public WorkflowConversionException(string message, Guid sourceDocumentId, string? sourceFilePath = null, string? stepId = null, string? variableName = null, ConversionPhase phase = ConversionPhase.Unknown)
            : base(message)
        {
            SourceDocumentId = sourceDocumentId;
            SourceFilePath = sourceFilePath;
            StepId = stepId;
            VariableName = variableName;
            ConversionPhase = phase;
        }

        /// <summary>Форматированное сообщение об ошибке с деталями</summary>
        public string GetDetailedMessage()
        {
            var details = new List<string> { Message };

            if (SourceDocumentId.HasValue)
                details.Add($"Документ: {SourceDocumentId}");

            if (!string.IsNullOrEmpty(SourceFilePath))
                details.Add($"Файл: {SourceFilePath}");

            if (!string.IsNullOrEmpty(StepId))
                details.Add($"Шаг: {StepId}");

            if (!string.IsNullOrEmpty(VariableName))
                details.Add($"Переменная: {VariableName}");

            details.Add($"Фаза: {ConversionPhase}");

            if (ErrorDetails.Any())
            {
                var errorDetailsList = ErrorDetails.Select(kv => $"{kv.Key}={kv.Value}");
                details.Add($"Детали: {string.Join(", ", errorDetailsList)}");
            }

            return string.Join(" | ", details);
        }
    }

    /// <summary>
    /// Фазы процесса преобразования
    /// </summary>
    public enum ConversionPhase
    {
        /// <summary>Неизвестная фаза</summary>
        Unknown = 0,

        /// <summary>Предварительная валидация</summary>
        PreValidation = 1,

        /// <summary>Обработка метаданных</summary>
        MetadataProcessing = 2,

        /// <summary>Обработка переменных</summary>
        VariableProcessing = 3,

        /// <summary>Преобразование шагов</summary>
        StepConversion = 4,

        /// <summary>Разрешение зависимостей</summary>
        DependencyResolution = 5,

        /// <summary>Валидация результата</summary>
        ResultValidation = 6,

        /// <summary>Финализация</summary>
        Finalization = 7
    }
}
```

### 7. Main Converter Implementation (MarkdownToWorkflowConverter)

**Purpose**: Core conversion engine that transforms markdown workflow documents into standard WorkflowDefinition objects.

**Key Features**:
- Asynchronous processing for complex conversions
- Multi-phase conversion with error recovery
- Variable substitution and dependency resolution
- Performance optimization with result caching
- Comprehensive error handling and validation
- Integration with existing WorkflowDefinition structure

**Converter Implementation**:
```csharp
// Файл: MarkdownToWorkflowConverter.cs
using System.Text.Json;
using System.Text.RegularExpressions;
using Orchestra.Core.Models.Workflow;

namespace Orchestra.Core.Services
{
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
        public async Task<WorkflowConversionResult> ConvertWithValidationAsync(MarkdownWorkflowDocument markdownDocument, WorkflowConversionOptions? options = null)
        {
            options ??= new WorkflowConversionOptions();
            options.StrictValidation = true;
            options.IncludeWarnings = true;

            var result = await ConvertAsync(markdownDocument, options);

            // Дополнительная валидация результата
            if (result.IsSuccess && result.WorkflowDefinition != null)
            {
                await PerformExtendedValidationAsync(result.WorkflowDefinition, result);
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
        public async Task<ConversionComplexityMetrics> EstimateConversionComplexityAsync(MarkdownWorkflowDocument markdownDocument)
        {
            var metrics = new ConversionComplexityMetrics
            {
                StepCount = markdownDocument.Steps.Count,
                VariableCount = markdownDocument.Variables.Count,
                DependencyCount = markdownDocument.Steps.Sum(s => s.DependsOn.Count),
                CommandTypeVariety = markdownDocument.Steps.Select(s => s.Type).Distinct().Count()
            };

            // Расчёт глубины вложенности
            metrics.MaxNestingDepth = CalculateMaxNestingDepth(markdownDocument.Steps);

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

        // Метод больше не требуется - переменные обрабатываются в CreateWorkflowDefinitionAsync

        private async Task ConvertStepsAsync(MarkdownWorkflowDocument markdownDocument, WorkflowDefinition workflowDefinition, WorkflowConversionOptions options, WorkflowConversionResult result)
        {
            foreach (var markdownStep in markdownDocument.Steps.OrderBy(s => s.Order))
            {
                try
                {
                    var workflowStep = ConvertStep(markdownStep, options);
                    workflowDefinition.Steps.Add(workflowStep);
                    result.StepsConverted++;

                    // Обработка зависимостей
                    if (markdownStep.DependsOn.Any())
                    {
                        workflowDefinition.Dependencies[workflowStep.Id] = markdownStep.DependsOn.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    throw new WorkflowConversionException(
                        $"Ошибка преобразования шага '{markdownStep.Name}': {ex.Message}",
                        markdownDocument.Id,
                        markdownDocument.FilePath,
                        stepId: markdownStep.Id,
                        phase: ConversionPhase.StepConversion);
                }
            }
        }

        private WorkflowStep ConvertStep(MarkdownWorkflowStep markdownStep, WorkflowConversionOptions options)
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

        private object ConvertVariableValue(MarkdownWorkflowVariable variable)
        {
            if (variable.DefaultValue == null)
            {
                return variable.Required ? throw new ArgumentException($"Обязательная переменная '{variable.Name}' не имеет значения") : string.Empty;
            }

            return variable.Type switch
            {
                MarkdownVariableType.Number => Convert.ToDouble(variable.DefaultValue),
                MarkdownVariableType.Boolean => Convert.ToBoolean(variable.DefaultValue),
                MarkdownVariableType.DateTime => Convert.ToDateTime(variable.DefaultValue),
                MarkdownVariableType.StringArray => variable.DefaultValue is string[] arr ? arr : new[] { variable.DefaultValue.ToString() ?? string.Empty },
                MarkdownVariableType.Json => JsonSerializer.Deserialize<object>(variable.DefaultValue.ToString() ?? "{}"),
                _ => variable.DefaultValue.ToString() ?? string.Empty
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

        private async Task ResolveDependenciesAsync(WorkflowDefinition workflowDefinition, WorkflowConversionResult result)
        {
            var stepIds = workflowDefinition.Steps.Select(s => s.Id).ToHashSet();

            foreach (var (stepId, dependencies) in workflowDefinition.Dependencies.ToList())
            {
                var resolvedDependencies = new List<string>();

                foreach (var dependency in dependencies)
                {
                    // Попытка разрешить зависимость по ID или порядковому номеру
                    if (stepIds.Contains(dependency))
                    {
                        resolvedDependencies.Add(dependency);
                    }
                    else if (int.TryParse(dependency, out var order))
                    {
                        var stepByOrder = workflowDefinition.Steps.FirstOrDefault(s => s.Order == order);
                        if (stepByOrder != null)
                        {
                            resolvedDependencies.Add(stepByOrder.Id);
                        }
                        else
                        {
                            result.AddWarning($"Не удалось разрешить зависимость по порядку {order} для шага {stepId}");
                        }
                    }
                    else
                    {
                        result.AddWarning($"Не удалось разрешить зависимость '{dependency}' для шага {stepId}");
                    }
                }

                workflowDefinition.Dependencies[stepId] = resolvedDependencies.ToArray();
                result.DependenciesResolved += resolvedDependencies.Count;
            }
        }

        private async Task ValidateResultAsync(WorkflowDefinition workflowDefinition, WorkflowConversionOptions options, WorkflowConversionResult result)
        {
            // Проверка обязательных полей
            if (string.IsNullOrEmpty(workflowDefinition.Name))
                result.AddError("WorkflowDefinition должен иметь название");

            if (!workflowDefinition.Steps.Any())
                result.AddError("WorkflowDefinition должен содержать хотя бы один шаг");

            // Проверка уникальности ID шагов
            var stepIds = workflowDefinition.Steps.Select(s => s.Id).ToList();
            var duplicateIds = stepIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var duplicateId in duplicateIds)
            {
                result.AddError($"Дублирующийся ID шага: {duplicateId}");
            }

            // Проверка корректности зависимостей
            foreach (var (stepId, dependencies) in workflowDefinition.Dependencies)
            {
                foreach (var dependency in dependencies)
                {
                    if (!stepIds.Contains(dependency))
                    {
                        result.AddError($"Шаг {stepId} зависит от несуществующего шага: {dependency}");
                    }
                }
            }
        }

        private async Task PerformExtendedValidationAsync(WorkflowDefinition workflowDefinition, WorkflowConversionResult result)
        {
            // Проверка циклических зависимостей
            if (HasCircularDependencies(workflowDefinition))
            {
                result.AddError("Обнаружены циклические зависимости в workflow");
            }

            // Проверка доступности команд
            foreach (var step in workflowDefinition.Steps)
            {
                if (string.IsNullOrEmpty(step.Command) && step.Type == WorkflowStepType.Task)
                {
                    result.AddWarning($"Шаг '{step.Name}' типа Task не имеет команды");
                }
            }
        }

        private bool HasCircularDependencies(WorkflowDefinition workflowDefinition)
        {
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var step in workflowDefinition.Steps)
            {
                if (!visited.Contains(step.Id))
                {
                    if (HasCircularDependenciesRecursive(step.Id, workflowDefinition.Dependencies, visited, recursionStack))
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

        private void ValidateRequiredSections(MarkdownWorkflowDocument document, WorkflowConversionValidation validation)
        {
            if (!document.Steps.Any())
            {
                validation.AddBlockingError("Документ должен содержать хотя бы один шаг в секции Steps");
            }

            if (string.IsNullOrEmpty(document.Metadata.Title))
            {
                validation.AddWarning("Рекомендуется указать название workflow в заголовке документа");
            }
        }

        private void ValidateStepsStructure(MarkdownWorkflowDocument document, WorkflowConversionValidation validation)
        {
            foreach (var step in document.Steps)
            {
                if (string.IsNullOrEmpty(step.Name))
                {
                    validation.AddBlockingError($"Шаг {step.Order} не имеет названия");
                }

                if (step.Type == MarkdownStepType.Task && string.IsNullOrEmpty(step.Command))
                {
                    validation.AddWarning($"Шаг '{step.Name}' типа Task не имеет команды");
                }
            }
        }

        private void ValidateVariablesStructure(MarkdownWorkflowDocument document, WorkflowConversionValidation validation)
        {
            var variableNames = new HashSet<string>();

            foreach (var variable in document.Variables)
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
        }

        private void ValidateDependencies(MarkdownWorkflowDocument document, WorkflowConversionValidation validation)
        {
            var stepIds = document.Steps.Select(s => s.Id).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
            var stepOrders = document.Steps.Select(s => s.Order.ToString()).ToHashSet();

            foreach (var step in document.Steps)
            {
                foreach (var dependency in step.DependsOn)
                {
                    if (!stepIds.Contains(dependency) && !stepOrders.Contains(dependency))
                    {
                        validation.AddWarning($"Шаг '{step.Name}' зависит от неопределённого шага: {dependency}");
                    }
                }
            }
        }

        private async Task ValidateCommandCompatibilityAsync(MarkdownWorkflowDocument document, WorkflowConversionValidation validation)
        {
            var supportedCommands = MarkdownWorkflowConstants.SupportedCommandTypes;
            var unsupportedCommands = new List<string>();

            foreach (var step in document.Steps)
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
        }

        private int CalculateMaxNestingDepth(List<MarkdownWorkflowStep> steps)
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
}
```

### 8. Unit Test Suite (MarkdownToWorkflowConverterTests)

**Purpose**: Comprehensive test coverage for all converter functionality with focus on integration with existing models.

**Test Categories**:
- Positive conversion scenarios (complete workflows)
- Negative scenarios (error handling and validation)
- Edge cases (empty workflows, complex dependencies)
- Integration tests (WorkflowEngine compatibility)

**Test Implementation**:
```csharp
// Файл: MarkdownToWorkflowConverterTests.cs
namespace Orchestra.Tests.Core.Services
{
    public class MarkdownToWorkflowConverterTests
    {
        private readonly IMarkdownToWorkflowConverter _converter;

        public MarkdownToWorkflowConverterTests()
        {
            _converter = new MarkdownToWorkflowConverter();
        }

        [Fact]
        public async Task ConvertAsync_ValidWorkflow_ReturnsSuccessfulResult()
        {
            // Arrange
            var markdownWorkflow = CreateValidMarkdownWorkflow();

            // Act
            var result = await _converter.ConvertAsync(markdownWorkflow);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.WorkflowDefinition);
            Assert.Equal(markdownWorkflow.Name, result.WorkflowDefinition.Name);
            Assert.Equal(markdownWorkflow.Steps.Count, result.WorkflowDefinition.Steps.Count);
            Assert.Equal(markdownWorkflow.Variables.Count, result.WorkflowDefinition.Variables.Count);
        }

        [Fact]
        public async Task ConvertAsync_DocumentWithDependencies_ResolvesDependenciesCorrectly()
        {
            // Arrange
            var markdownDocument = CreateDocumentWithDependencies();

            // Act
            var result = await _converter.ConvertAsync(markdownDocument);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.DependenciesResolved > 0);
            Assert.NotNull(result.WorkflowDefinition);
            Assert.True(result.WorkflowDefinition.Dependencies.Any());
        }

        [Fact]
        public async Task ConvertWithValidationAsync_InvalidDocument_ReturnsErrors()
        {
            // Arrange
            var invalidDocument = CreateInvalidMarkdownDocument();

            // Act
            var result = await _converter.ConvertWithValidationAsync(invalidDocument);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public async Task ValidateConversionAsync_ValidDocument_ReturnsValidationSuccess()
        {
            // Arrange
            var markdownDocument = CreateValidMarkdownDocument();

            // Act
            var validation = await _converter.ValidateConversionAsync(markdownDocument);

            // Assert
            Assert.True(validation.CanConvert);
            Assert.Empty(validation.BlockingErrors);
        }

        [Fact]
        public async Task ValidateConversionAsync_CircularDependencies_ReturnsValidationErrors()
        {
            // Arrange
            var documentWithCircularDeps = CreateDocumentWithCircularDependencies();

            // Act
            var validation = await _converter.ValidateConversionAsync(documentWithCircularDeps);

            // Assert
            Assert.False(validation.CanConvert);
            Assert.Contains(validation.BlockingErrors, e => e.Contains("циклические") || e.Contains("circular"));
        }

        [Fact]
        public async Task EstimateConversionComplexityAsync_ComplexDocument_ReturnsAccurateMetrics()
        {
            // Arrange
            var complexDocument = CreateComplexMarkdownDocument();

            // Act
            var metrics = await _converter.EstimateConversionComplexityAsync(complexDocument);

            // Assert
            Assert.True(metrics.ComplexityRating > 0);
            Assert.Equal(complexDocument.Steps.Count, metrics.StepCount);
            Assert.Equal(complexDocument.Variables.Count, metrics.VariableCount);
            Assert.True(metrics.EstimatedConversionTimeMs > 0);
        }

        [Theory]
        [InlineData(10, 5, 8)]
        [InlineData(50, 20, 30)]
        [InlineData(100, 50, 80)]
        public async Task ConvertAsync_LargeDocuments_HandlesEfficiently(int stepCount, int variableCount, int dependencyCount)
        {
            // Arrange
            var largeDocument = CreateLargeDocument(stepCount, variableCount, dependencyCount);

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _converter.ConvertAsync(largeDocument);
            stopwatch.Stop();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(stopwatch.ElapsedMilliseconds < 1000); // Should complete within 1 second
            Assert.Equal(stepCount, result.StepsConverted);
            Assert.Equal(variableCount, result.VariablesProcessed);
        }

        [Fact]
        public async Task ConvertAsync_WithVariableSubstitution_ProcessesVariablesCorrectly()
        {
            // Arrange
            var documentWithVariables = CreateDocumentWithVariableSubstitution();
            var options = new WorkflowConversionOptions { ProcessVariableSubstitution = true };

            // Act
            var result = await _converter.ConvertAsync(documentWithVariables, options);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.WorkflowDefinition);
            var stepWithVariable = result.WorkflowDefinition.Steps.First();
            Assert.Contains("${variables.", stepWithVariable.Command);
        }

        [Fact]
        public async Task ConvertAsync_CachingEnabled_UsesCachedResults()
        {
            // Arrange
            var markdownDocument = CreateValidMarkdownDocument();

            // Act
            var firstResult = await _converter.ConvertAsync(markdownDocument);
            var secondResult = await _converter.ConvertAsync(markdownDocument);

            // Assert
            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);
            Assert.Equal(firstResult.WorkflowDefinition?.Id, secondResult.WorkflowDefinition?.Id);
        }

        [Fact]
        public async Task ConvertAsync_StrictValidationDisabled_ConvertsWithWarnings()
        {
            // Arrange
            var documentWithWarnings = CreateDocumentWithWarnings();
            var options = new WorkflowConversionOptions { StrictValidation = false };

            // Act
            var result = await _converter.ConvertAsync(documentWithWarnings, options);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotEmpty(result.Warnings);
        }

        [Fact]
        public async Task ConvertAsync_MetadataPreservation_PreservesOriginalMetadata()
        {
            // Arrange
            var markdownDocument = CreateDocumentWithRichMetadata();
            var options = new WorkflowConversionOptions { PreserveMetadata = true };

            // Act
            var result = await _converter.ConvertAsync(markdownDocument, options);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.WorkflowDefinition);
            Assert.Contains("OriginalFormat", result.WorkflowDefinition.Metadata.Keys);
            Assert.Equal("Markdown", result.WorkflowDefinition.Metadata["OriginalFormat"]);
        }

        // Вспомогательные методы для создания тестовых данных
        private MarkdownWorkflow CreateValidMarkdownWorkflow()
        {
            var metadata = new MarkdownWorkflowMetadata(
                Author: "Test Author",
                Version: "1.0",
                Tags: new List<string> { "test" },
                Description: "Test workflow description",
                CreatedAt: DateTime.UtcNow
            );

            var variables = new Dictionary<string, MarkdownWorkflowVariable>
            {
                ["projectPath"] = new MarkdownWorkflowVariable("projectPath", "string", true, null, "Path to project"),
                ["buildConfig"] = new MarkdownWorkflowVariable("buildConfig", "string", false, "Release", "Build configuration")
            };

            var steps = new List<MarkdownWorkflowStep>
            {
                new("step1", "Build", "task", "dotnet build", new Dictionary<string, string>(), new List<string>(), "Build the project"),
                new("step2", "Test", "task", "dotnet test", new Dictionary<string, string>(), new List<string> { "step1" }, "Run tests")
            };

            return new MarkdownWorkflow(
                Id: "test-workflow-id",
                Name: "Test Workflow",
                SourceFilePath: "test-workflow.md",
                Metadata: metadata,
                Variables: variables,
                Steps: steps,
                ParsedAt: DateTime.UtcNow,
                FileHash: "test-hash"
            );
        }

        private MarkdownWorkflowDocument CreateDocumentWithDependencies()
        {
            var document = CreateValidMarkdownDocument();
            document.Steps.Add(new MarkdownWorkflowStep
            {
                Id = "step3",
                Name = "Deploy",
                Order = 3,
                Type = MarkdownStepType.Task,
                Command = "dotnet publish",
                DependsOn = new List<string> { "step1", "step2" }
            });
            return document;
        }

        private MarkdownWorkflowDocument CreateInvalidMarkdownDocument()
        {
            return new MarkdownWorkflowDocument
            {
                Id = Guid.NewGuid(),
                Metadata = new MarkdownWorkflowMetadata { Title = "" }, // Пустое название
                Steps = new List<MarkdownWorkflowStep>() // Нет шагов
            };
        }

        private MarkdownWorkflowDocument CreateDocumentWithCircularDependencies()
        {
            return new MarkdownWorkflowDocument
            {
                Id = Guid.NewGuid(),
                Metadata = new MarkdownWorkflowMetadata { Title = "Circular Dependencies Test" },
                Steps = new List<MarkdownWorkflowStep>
                {
                    new() { Id = "step1", Name = "Step 1", Order = 1, DependsOn = new List<string> { "step2" } },
                    new() { Id = "step2", Name = "Step 2", Order = 2, DependsOn = new List<string> { "step1" } }
                }
            };
        }

        private MarkdownWorkflowDocument CreateComplexMarkdownDocument()
        {
            var document = CreateValidMarkdownDocument();

            // Добавляем больше шагов и переменных для увеличения сложности
            for (int i = 3; i <= 10; i++)
            {
                document.Steps.Add(new MarkdownWorkflowStep
                {
                    Id = $"step{i}",
                    Name = $"Step {i}",
                    Order = i,
                    Type = MarkdownStepType.Task,
                    Command = $"echo 'Step {i}'",
                    DependsOn = new List<string> { $"step{i-1}" }
                });
            }

            for (int i = 3; i <= 8; i++)
            {
                document.Variables.Add(new MarkdownWorkflowVariable
                {
                    Name = $"var{i}",
                    Type = MarkdownVariableType.String,
                    DefaultValue = $"value{i}"
                });
            }

            return document;
        }

        private MarkdownWorkflowDocument CreateLargeDocument(int stepCount, int variableCount, int dependencyCount)
        {
            var document = new MarkdownWorkflowDocument
            {
                Id = Guid.NewGuid(),
                Metadata = new MarkdownWorkflowMetadata { Title = "Large Test Workflow" },
                Variables = new List<MarkdownWorkflowVariable>(),
                Steps = new List<MarkdownWorkflowStep>()
            };

            // Создание переменных
            for (int i = 1; i <= variableCount; i++)
            {
                document.Variables.Add(new MarkdownWorkflowVariable
                {
                    Name = $"variable{i}",
                    Type = MarkdownVariableType.String,
                    DefaultValue = $"value{i}"
                });
            }

            // Создание шагов
            for (int i = 1; i <= stepCount; i++)
            {
                var step = new MarkdownWorkflowStep
                {
                    Id = $"step{i}",
                    Name = $"Step {i}",
                    Order = i,
                    Type = MarkdownStepType.Task,
                    Command = $"echo 'Step {i}'"
                };

                // Добавление зависимостей
                if (i > 1 && dependencyCount > 0)
                {
                    var maxDeps = Math.Min(dependencyCount / stepCount + 1, i - 1);
                    for (int j = 1; j <= maxDeps; j++)
                    {
                        step.DependsOn.Add($"step{i - j}");
                    }
                }

                document.Steps.Add(step);
            }

            return document;
        }

        private MarkdownWorkflowDocument CreateDocumentWithVariableSubstitution()
        {
            var document = CreateValidMarkdownDocument();
            document.Steps.First().Command = "dotnet build {{projectPath}} --configuration {{buildConfig}}";
            return document;
        }

        private MarkdownWorkflowDocument CreateDocumentWithWarnings()
        {
            var document = CreateValidMarkdownDocument();
            document.Steps.Add(new MarkdownWorkflowStep
            {
                Id = "incomplete-step",
                Name = "Incomplete Step",
                Order = 3,
                Type = MarkdownStepType.Task
                // Намеренно отсутствует Command - должно вызвать предупреждение
            });
            return document;
        }

        private MarkdownWorkflowDocument CreateDocumentWithRichMetadata()
        {
            var document = CreateValidMarkdownDocument();
            document.Metadata.Tags.AddRange(new[] { "test", "ci/cd", "automation" });
            document.Metadata.Priority = WorkflowPriority.High;
            document.FilePath = "/path/to/workflow.md";
            return document;
        }
    }
}
```

## Implementation Requirements

### Functional Requirements
- [ ] IMarkdownToWorkflowConverter interface using existing MarkdownWorkflow input model
- [ ] MarkdownToWorkflowConverter correctly converts to existing WorkflowDefinition structure
- [ ] Variable mapping from MarkdownWorkflowVariable to VariableDefinition
- [ ] Step conversion from MarkdownWorkflowStep to WorkflowStep with proper type mapping
- [ ] Comprehensive validation with detailed error reporting using existing result models

### Technical Requirements
- [ ] **CRITICAL: Full integration with existing Orchestra.Core.Models.Workflow models**
- [ ] Use existing MarkdownToWorkflowConversionResult for output
- [ ] Correct namespace Orchestra.Core.Services (not Workflow.Markdown)
- [ ] Variable substitution using {{variable}} syntax
- [ ] Dependency resolution between workflow steps

### Performance Requirements
- [ ] Convert documents with 100 steps in < 500ms
- [ ] Handle documents up to 10MB efficiently
- [ ] Memory usage optimization for large workflows
- [ ] Result caching for repeated conversions

### Code Quality Requirements
- [ ] Unit test coverage >= 85%
- [ ] XML documentation in Russian for all public methods
- [ ] SOLID principles adherence
- [ ] Exception handling with detailed context

## Integration with System Components

### Downstream Dependencies (Components that depend on this converter)
- `01-05-workflow-engine-extension.md` - WorkflowEngine extension uses conversion results
- `01-06-mediator-commands.md` - Commands use converter for processing
- Existing WorkflowEngine infrastructure

### Upstream Dependencies (Components this converter relies on)
- `01-01-markdown-models.md` - Data models for input ✅ COMPLETE
- `01-02-markdown-parser.md` - Parser provides MarkdownWorkflowDocument ✅ COMPLETE
- Existing WorkflowDefinition models ✅ COMPLETE

## Testing Strategy

### Positive Test Scenarios
1. Convert complete workflows with all components
2. Handle complex dependency structures
3. Process variable substitution correctly
4. Preserve metadata during conversion
5. Cache results for performance

### Negative Test Scenarios
1. Invalid document structures
2. Circular dependencies
3. Missing required components
4. Conversion timeouts
5. Memory limitations

### Edge Case Testing
1. Empty documents
2. Very large workflows (1000+ steps)
3. Complex nested dependencies
4. Unicode and special characters
5. Malformed variable references

## Usage Examples

### Basic Conversion
```csharp
var converter = new MarkdownToWorkflowConverter();
var result = await converter.ConvertAsync(markdownDocument);
if (result.IsSuccess)
{
    var workflowDefinition = result.WorkflowDefinition;
    // Use with existing WorkflowEngine
}
```

### Conversion with Options
```csharp
var options = new WorkflowConversionOptions
{
    StrictValidation = true,
    ProcessVariableSubstitution = true,
    ResolveDependencies = true
};
var result = await converter.ConvertWithValidationAsync(markdownDocument, options);
```

### Pre-validation
```csharp
var validation = await converter.ValidateConversionAsync(markdownDocument);
if (!validation.CanConvert)
{
    foreach (var error in validation.BlockingErrors)
        Console.WriteLine($"Blocking Error: {error}");
}
```

### Complexity Analysis
```csharp
var metrics = await converter.EstimateConversionComplexityAsync(markdownDocument);
Console.WriteLine($"Complexity Rating: {metrics.ComplexityRating}/10");
Console.WriteLine($"Estimated Time: {metrics.EstimatedConversionTimeMs}ms");
```

---

**STATUS**: [ ] Ready for Implementation
**ESTIMATED IMPLEMENTATION TIME**: 30 minutes
**PRIORITY**: CRITICAL - Required for Phase 1 completion

**NEXT STEPS**:
1. Implement all components following the technical specification
2. Create comprehensive unit tests with 85%+ coverage
3. Validate integration with existing WorkflowDefinition models
4. Performance testing with large workflow documents
5. Prepare for integration with WorkflowEngine extension