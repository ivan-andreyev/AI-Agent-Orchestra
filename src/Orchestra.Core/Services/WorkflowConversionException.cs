namespace Orchestra.Core.Services;

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