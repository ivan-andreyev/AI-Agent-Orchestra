using Orchestra.Core.Models.Workflow;

namespace Orchestra.Core.Services;

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

    /// <summary>
    /// Оценка сложности преобразования для планирования ресурсов
    /// </summary>
    /// <param name="markdownWorkflow">Markdown workflow для анализа</param>
    /// <returns>Метрики сложности преобразования</returns>
    Task<ConversionComplexityMetrics> EstimateConversionComplexityAsync(MarkdownWorkflow markdownWorkflow);
}