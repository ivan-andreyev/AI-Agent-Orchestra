using System.Text.Json;
using System.Text.Json.Serialization;
using Orchestra.Core.Models.Workflow;

namespace Orchestra.Core.Services;

/// <summary>
/// Сервис для сериализации и десериализации Workflow definitions
/// </summary>
public class WorkflowSerializer
{
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Инициализирует новый экземпляр WorkflowSerializer с настройками JSON
    /// </summary>
    public WorkflowSerializer()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(),
                new TimeSpanConverter()
            }
        };
    }

    /// <summary>
    /// Сериализует WorkflowDefinition в JSON строку
    /// </summary>
    /// <param name="workflow">Workflow definition для сериализации</param>
    /// <returns>JSON строка представляющая workflow</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается если workflow null</exception>
    /// <exception cref="JsonException">Выбрасывается при ошибке сериализации</exception>
    public string SerializeWorkflow(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        try
        {
            return JsonSerializer.Serialize(workflow, _jsonOptions);
        }
        catch (JsonException ex)
        {
            throw new JsonException($"Failed to serialize workflow '{workflow.Name}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Десериализует JSON строку в WorkflowDefinition
    /// </summary>
    /// <param name="json">JSON строка для десериализации</param>
    /// <returns>Восстановленный WorkflowDefinition</returns>
    /// <exception cref="ArgumentException">Выбрасывается если JSON строка пуста или null</exception>
    /// <exception cref="JsonException">Выбрасывается при ошибке десериализации</exception>
    public WorkflowDefinition DeserializeWorkflow(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON string cannot be null or empty", nameof(json));
        }

        try
        {
            var workflow = JsonSerializer.Deserialize<WorkflowDefinition>(json, _jsonOptions);
            if (workflow == null)
            {
                throw new JsonException("Deserialization resulted in null workflow");
            }

            return workflow;
        }
        catch (JsonException ex)
        {
            throw new JsonException($"Failed to deserialize workflow JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Валидирует JSON структуру workflow без полной десериализации
    /// </summary>
    /// <param name="json">JSON строка для валидации</param>
    /// <returns>True если JSON валиден, иначе false</returns>
    public bool ValidateWorkflowJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Check if root is an object (not null, array, etc.)
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // Check required properties
            return root.TryGetProperty("id", out _) &&
                   root.TryGetProperty("name", out _) &&
                   root.TryGetProperty("steps", out var stepsProperty) &&
                   stepsProperty.ValueKind == JsonValueKind.Array &&
                   root.TryGetProperty("variables", out var variablesProperty) &&
                   variablesProperty.ValueKind == JsonValueKind.Object &&
                   root.TryGetProperty("metadata", out var metadataProperty) &&
                   metadataProperty.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Асинхронно загружает workflow из файла
    /// </summary>
    /// <param name="filePath">Путь к файлу workflow</param>
    /// <returns>WorkflowDefinition из файла</returns>
    /// <exception cref="FileNotFoundException">Выбрасывается если файл не найден</exception>
    /// <exception cref="JsonException">Выбрасывается при ошибке десериализации</exception>
    public async Task<WorkflowDefinition> LoadWorkflowFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Workflow file not found: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath);
        return DeserializeWorkflow(json);
    }

    /// <summary>
    /// Асинхронно сохраняет workflow в файл
    /// </summary>
    /// <param name="workflow">Workflow для сохранения</param>
    /// <param name="filePath">Путь к файлу для сохранения</param>
    /// <exception cref="ArgumentNullException">Выбрасывается если workflow null</exception>
    /// <exception cref="JsonException">Выбрасывается при ошибке сериализации</exception>
    public async Task SaveWorkflowToFileAsync(WorkflowDefinition workflow, string filePath)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var json = SerializeWorkflow(workflow);
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, json);
    }
}

/// <summary>
/// Custom JSON converter для TimeSpan сериализации в ISO 8601 duration format
/// </summary>
public class TimeSpanConverter : JsonConverter<TimeSpan>
{
    /// <summary>
    /// Читает TimeSpan из JSON в формате HH:mm:ss
    /// </summary>
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
        {
            return TimeSpan.Zero;
        }

        if (TimeSpan.TryParse(value, out var timeSpan))
        {
            return timeSpan;
        }

        throw new JsonException($"Invalid TimeSpan format: {value}. Expected format: HH:mm:ss");
    }

    /// <summary>
    /// Записывает TimeSpan в JSON в формате HH:mm:ss
    /// </summary>
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(@"hh\:mm\:ss"));
    }
}