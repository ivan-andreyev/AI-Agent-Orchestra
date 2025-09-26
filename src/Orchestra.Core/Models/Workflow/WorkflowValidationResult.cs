using System.Text.Json.Serialization;

namespace Orchestra.Core.Models.Workflow;

/// <summary>
/// Результат валидации markdown workflow
/// </summary>
/// <param name="IsValid">Валиден ли workflow</param>
/// <param name="ErrorMessage">Сообщение об ошибке (если невалиден)</param>
/// <param name="ValidationErrors">Список ошибок валидации</param>
/// <param name="ValidationWarnings">Список предупреждений валидации</param>
/// <param name="ValidatedAt">Время выполнения валидации</param>
public record WorkflowValidationResult(
    [property: JsonPropertyName("isValid")] bool IsValid,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null,
    [property: JsonPropertyName("validationErrors")] List<string>? ValidationErrors = null,
    [property: JsonPropertyName("validationWarnings")] List<string>? ValidationWarnings = null,
    [property: JsonPropertyName("validatedAt")] DateTime? ValidatedAt = null
);