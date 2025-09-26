namespace Orchestra.Core.Services;

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