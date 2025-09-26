using System.Text.Json.Serialization;

namespace Orchestra.Core.Models.Workflow;

/// <summary>
/// Метаданные markdown workflow
/// </summary>
/// <param name="Author">Автор workflow</param>
/// <param name="Version">Версия workflow</param>
/// <param name="Tags">Теги для категоризации</param>
/// <param name="Description">Описание workflow (опционально)</param>
/// <param name="CreatedAt">Дата создания (автоматически устанавливается)</param>
public record MarkdownWorkflowMetadata(
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("tags")] List<string> Tags,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("createdAt")] DateTime? CreatedAt = null
);

/// <summary>
/// Определение переменной markdown workflow
/// </summary>
/// <param name="Name">Имя переменной</param>
/// <param name="Type">Тип переменной (string, int, bool, etc.)</param>
/// <param name="IsRequired">Обязательна ли переменная</param>
/// <param name="DefaultValue">Значение по умолчанию</param>
/// <param name="Description">Описание переменной</param>
public record MarkdownWorkflowVariable(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("isRequired")] bool IsRequired,
    [property: JsonPropertyName("defaultValue")] string? DefaultValue = null,
    [property: JsonPropertyName("description")] string? Description = null
);

/// <summary>
/// Шаг markdown workflow
/// </summary>
/// <param name="Id">Уникальный идентификатор шага</param>
/// <param name="Title">Заголовок шага из markdown</param>
/// <param name="Type">Тип шага</param>
/// <param name="Command">Команда для выполнения</param>
/// <param name="Parameters">Параметры шага</param>
/// <param name="DependsOn">Идентификаторы шагов, от которых зависит данный шаг</param>
/// <param name="Description">Описание шага</param>
public record MarkdownWorkflowStep(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("parameters")] Dictionary<string, string> Parameters,
    [property: JsonPropertyName("dependsOn")] List<string> DependsOn,
    [property: JsonPropertyName("description")] string? Description = null
);

/// <summary>
/// Представление markdown workflow после парсинга
/// </summary>
/// <param name="Id">Уникальный идентификатор (генерируется автоматически)</param>
/// <param name="Name">Название workflow из заголовка</param>
/// <param name="SourceFilePath">Путь к исходному markdown файлу</param>
/// <param name="Metadata">Метаданные workflow</param>
/// <param name="Variables">Переменные workflow</param>
/// <param name="Steps">Шаги workflow</param>
/// <param name="ParsedAt">Время парсинга</param>
/// <param name="FileHash">Хеш файла для отслеживания изменений</param>
public record MarkdownWorkflow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("sourceFilePath")] string SourceFilePath,
    [property: JsonPropertyName("metadata")] MarkdownWorkflowMetadata Metadata,
    [property: JsonPropertyName("variables")] Dictionary<string, MarkdownWorkflowVariable> Variables,
    [property: JsonPropertyName("steps")] List<MarkdownWorkflowStep> Steps,
    [property: JsonPropertyName("parsedAt")] DateTime ParsedAt,
    [property: JsonPropertyName("fileHash")] string FileHash
);

/// <summary>
/// Результат парсинга markdown workflow файла
/// </summary>
/// <param name="IsSuccess">Успешность парсинга</param>
/// <param name="Workflow">Распарсенный workflow (если успешно)</param>
/// <param name="ErrorMessage">Сообщение об ошибке (если неуспешно)</param>
/// <param name="Warnings">Предупреждения при парсинге</param>
public record MarkdownWorkflowParseResult(
    [property: JsonPropertyName("isSuccess")] bool IsSuccess,
    [property: JsonPropertyName("workflow")] MarkdownWorkflow? Workflow = null,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null,
    [property: JsonPropertyName("warnings")] List<string>? Warnings = null
);

/// <summary>
/// Результат конвертации markdown workflow в JSON WorkflowDefinition
/// </summary>
/// <param name="IsSuccess">Успешность конвертации</param>
/// <param name="WorkflowDefinition">Результирующий JSON workflow (если успешно)</param>
/// <param name="SourceMarkdownWorkflow">Исходный markdown workflow</param>
/// <param name="ErrorMessage">Сообщение об ошибке (если неуспешно)</param>
/// <param name="ConvertedAt">Время конвертации</param>
public record MarkdownToWorkflowConversionResult(
    [property: JsonPropertyName("isSuccess")] bool IsSuccess,
    [property: JsonPropertyName("workflowDefinition")] WorkflowDefinition? WorkflowDefinition = null,
    [property: JsonPropertyName("sourceMarkdownWorkflow")] MarkdownWorkflow? SourceMarkdownWorkflow = null,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null,
    [property: JsonPropertyName("convertedAt")] DateTime? ConvertedAt = null
);