namespace Orchestra.Core.Services;

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