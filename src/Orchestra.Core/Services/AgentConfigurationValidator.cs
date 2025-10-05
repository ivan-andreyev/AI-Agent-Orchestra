using Microsoft.Extensions.Logging;

namespace Orchestra.Core.Services;

/// <summary>
/// Интерфейс валидатора конфигураций агентов
/// </summary>
/// <remarks>
/// Отвечает только за валидацию конфигураций (Single Responsibility Principle).
/// Хранение вынесено в IAgentConfigurationRegistry.
/// </remarks>
public interface IAgentConfigurationValidator
{
    /// <summary>
    /// Валидирует конфигурацию агента
    /// </summary>
    /// <param name="configuration">Конфигурация для валидации</param>
    /// <returns>Список ошибок валидации. Пустой список означает успешную валидацию.</returns>
    IReadOnlyList<string> Validate(IAgentConfiguration configuration);

    /// <summary>
    /// Валидирует все конфигурации из реестра
    /// </summary>
    /// <param name="registry">Реестр с конфигурациями для валидации</param>
    /// <returns>
    /// Dictionary где ключ - тип агента, значение - список ошибок валидации.
    /// Пустой список ошибок означает успешную валидацию конфигурации.
    /// </returns>
    IReadOnlyDictionary<string, IReadOnlyList<string>> ValidateAll(IAgentConfigurationRegistry registry);
}

/// <summary>
/// Реализация валидатора конфигураций агентов
/// </summary>
public class AgentConfigurationValidator : IAgentConfigurationValidator
{
    private readonly ILogger<AgentConfigurationValidator> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр AgentConfigurationValidator
    /// </summary>
    /// <param name="logger">Логгер для отслеживания операций</param>
    public AgentConfigurationValidator(ILogger<AgentConfigurationValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Validate(IAgentConfiguration configuration)
    {
        if (configuration == null)
        {
            _logger.LogError("Validation called with null configuration");
            return new List<string> { "Configuration cannot be null" };
        }

        var errors = configuration.Validate();

        if (errors.Count > 0)
        {
            _logger.LogWarning(
                "Configuration validation failed for '{AgentType}': {Errors}",
                configuration.AgentType,
                string.Join("; ", errors));
        }
        else
        {
            _logger.LogDebug(
                "Configuration validation successful for '{AgentType}'",
                configuration.AgentType);
        }

        return errors;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidateAll(IAgentConfigurationRegistry registry)
    {
        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        var results = new Dictionary<string, IReadOnlyList<string>>();
        var allConfigurations = registry.GetAll();

        _logger.LogInformation(
            "Validating {Count} agent configuration(s)",
            allConfigurations.Count);

        foreach (var configuration in allConfigurations)
        {
            var errors = Validate(configuration);
            results[configuration.AgentType] = errors;
        }

        var failedCount = results.Count(r => r.Value.Count > 0);
        if (failedCount > 0)
        {
            _logger.LogWarning(
                "Validation completed: {FailedCount} configuration(s) failed out of {TotalCount}",
                failedCount,
                results.Count);
        }
        else
        {
            _logger.LogInformation(
                "All {Count} configuration(s) validated successfully",
                results.Count);
        }

        return results;
    }
}
