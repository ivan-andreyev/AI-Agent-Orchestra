using System.Text.Json.Serialization;

namespace Orchestra.Core.Models.Workflow;

/// <summary>
/// Метаданные workflow
/// </summary>
/// <param name="Author">Автор workflow</param>
/// <param name="Description">Описание workflow</param>
/// <param name="Version">Версия workflow</param>
/// <param name="CreatedAt">Дата создания</param>
/// <param name="Tags">Теги для категоризации</param>
public record WorkflowMetadata(
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("tags")] List<string> Tags
);

/// <summary>
/// Определение переменной workflow
/// </summary>
/// <param name="Name">Имя переменной</param>
/// <param name="Type">Тип переменной в виде строки</param>
/// <param name="DefaultValue">Значение по умолчанию</param>
/// <param name="IsRequired">Обязательна ли переменная</param>
/// <param name="Description">Описание переменной</param>
public record VariableDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("defaultValue")] object? DefaultValue = null,
    [property: JsonPropertyName("isRequired")] bool IsRequired = false,
    [property: JsonPropertyName("description")] string? Description = null
);

/// <summary>
/// Определение workflow для выполнения
/// </summary>
/// <param name="Id">Уникальный идентификатор workflow</param>
/// <param name="Name">Название workflow</param>
/// <param name="Steps">Список шагов workflow</param>
/// <param name="Variables">Определения переменных workflow</param>
/// <param name="Metadata">Метаданные workflow</param>
public record WorkflowDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("steps")] List<WorkflowStep> Steps,
    [property: JsonPropertyName("variables")] Dictionary<string, VariableDefinition> Variables,
    [property: JsonPropertyName("metadata")] WorkflowMetadata Metadata
);

/// <summary>
/// Тип шага workflow
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkflowStepType
{
    /// <summary>
    /// Обычная задача
    /// </summary>
    [JsonPropertyName("Task")]
    Task,

    /// <summary>
    /// Условный переход
    /// </summary>
    [JsonPropertyName("Condition")]
    Condition,

    /// <summary>
    /// Цикл
    /// </summary>
    [JsonPropertyName("Loop")]
    Loop,

    /// <summary>
    /// Параллельное выполнение
    /// </summary>
    [JsonPropertyName("Parallel")]
    Parallel,

    /// <summary>
    /// Начальный шаг
    /// </summary>
    [JsonPropertyName("Start")]
    Start,

    /// <summary>
    /// Завершающий шаг
    /// </summary>
    [JsonPropertyName("End")]
    End
}

/// <summary>
/// Условная логика для шагов workflow
/// </summary>
/// <param name="Expression">Выражение для оценки</param>
/// <param name="TruePath">Путь выполнения при истинном условии</param>
/// <param name="FalsePath">Путь выполнения при ложном условии</param>
public record ConditionalLogic(
    [property: JsonPropertyName("expression")] string Expression,
    [property: JsonPropertyName("truePath")] List<string>? TruePath = null,
    [property: JsonPropertyName("falsePath")] List<string>? FalsePath = null
);


/// <summary>
/// Шаг workflow
/// </summary>
/// <param name="Id">Уникальный идентификатор шага</param>
/// <param name="Type">Тип шага</param>
/// <param name="Command">Команда для выполнения</param>
/// <param name="Parameters">Параметры шага</param>
/// <param name="DependsOn">Идентификаторы шагов, от которых зависит данный шаг</param>
/// <param name="Condition">Условная логика для выполнения шага</param>
/// <param name="RetryPolicy">Политика повторных попыток</param>
/// <param name="LoopDefinition">Определение цикла для шагов типа Loop</param>
/// <param name="NestedSteps">Вложенные шаги для выполнения в цикле (только для Loop шагов)</param>
public record WorkflowStep(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] WorkflowStepType Type,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("parameters")] Dictionary<string, object> Parameters,
    [property: JsonPropertyName("dependsOn")] List<string> DependsOn,
    [property: JsonPropertyName("condition")] ConditionalLogic? Condition = null,
    [property: JsonPropertyName("retryPolicy")] RetryPolicy? RetryPolicy = null,
    [property: JsonPropertyName("loopDefinition")] LoopDefinition? LoopDefinition = null,
    [property: JsonPropertyName("nestedSteps")] List<WorkflowStep>? NestedSteps = null
);