namespace Orchestra.Core.Services;

/// <summary>
/// Базовый абстрактный класс для конфигураций агентов с общей валидацией
/// </summary>
/// <remarks>
/// <para>
/// Предоставляет общую функциональность для всех конфигураций агентов,
/// устраняя дублирование кода валидации (DRY principle).
/// </para>
/// <para>
/// <b>Использование:</b>
/// <code>
/// public class MyAgentConfiguration : BaseAgentConfiguration
/// {
///     public override string AgentType => "my-agent";
///
///     // Специфичные для агента свойства
///     public string CustomProperty { get; set; }
///
///     protected override void ValidateSpecificProperties(List&lt;string&gt; errors)
///     {
///         if (string.IsNullOrEmpty(CustomProperty))
///         {
///             errors.Add("CustomProperty cannot be empty");
///         }
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class BaseAgentConfiguration : IAgentConfiguration
{
    /// <inheritdoc />
    public abstract string AgentType { get; }

    /// <inheritdoc />
    public virtual int MaxConcurrentExecutions { get; set; } = 3;

    /// <inheritdoc />
    public virtual TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    public virtual int RetryAttempts { get; set; } = 3;

    /// <inheritdoc />
    public virtual TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public virtual IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        // Валидация общих свойств
        ValidateCommonProperties(errors);

        // Позволяем производным классам добавить специфичную валидацию
        ValidateSpecificProperties(errors);

        return errors;
    }

    /// <summary>
    /// Валидирует специфичные для агента свойства
    /// </summary>
    /// <param name="errors">Список для добавления ошибок валидации</param>
    /// <remarks>
    /// Переопределите этот метод в производных классах для добавления
    /// специфичной валидации. Не вызывайте base.ValidateSpecificProperties().
    /// </remarks>
    protected virtual void ValidateSpecificProperties(List<string> errors)
    {
        // По умолчанию нет специфичной валидации
    }

    /// <summary>
    /// Валидирует общие свойства конфигурации агента
    /// </summary>
    /// <param name="errors">Список для добавления ошибок валидации</param>
    private void ValidateCommonProperties(List<string> errors)
    {
        // Валидация MaxConcurrentExecutions
        if (MaxConcurrentExecutions < 1 || MaxConcurrentExecutions > 100)
        {
            errors.Add($"MaxConcurrentExecutions must be between 1 and 100, got {MaxConcurrentExecutions}");
        }

        // Валидация DefaultTimeout
        if (DefaultTimeout < TimeSpan.FromSeconds(10))
        {
            errors.Add($"DefaultTimeout must be at least 10 seconds, got {DefaultTimeout.TotalSeconds} seconds");
        }

        if (DefaultTimeout > TimeSpan.FromHours(1))
        {
            errors.Add($"DefaultTimeout should not exceed 1 hour, got {DefaultTimeout.TotalHours} hours");
        }

        // Валидация RetryAttempts
        if (RetryAttempts < 0 || RetryAttempts > 10)
        {
            errors.Add($"RetryAttempts must be between 0 and 10, got {RetryAttempts}");
        }

        // Валидация RetryDelay
        if (RetryDelay < TimeSpan.FromMilliseconds(100))
        {
            errors.Add($"RetryDelay must be at least 100 milliseconds, got {RetryDelay.TotalMilliseconds} ms");
        }

        if (RetryDelay > TimeSpan.FromSeconds(30))
        {
            errors.Add($"RetryDelay should not exceed 30 seconds, got {RetryDelay.TotalSeconds} seconds");
        }
    }
}
