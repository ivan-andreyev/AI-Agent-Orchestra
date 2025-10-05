using Microsoft.Extensions.Logging;

namespace Orchestra.Core.Services;

/// <summary>
/// Интерфейс реестра конфигураций агентов
/// </summary>
/// <remarks>
/// Отвечает только за хранение и получение конфигураций (Single Responsibility Principle).
/// Валидация вынесена в IAgentConfigurationValidator.
/// </remarks>
public interface IAgentConfigurationRegistry
{
    /// <summary>
    /// Регистрирует конфигурацию агента
    /// </summary>
    /// <param name="configuration">Конфигурация для регистрации</param>
    /// <exception cref="ArgumentNullException">Если configuration равна null</exception>
    /// <exception cref="ArgumentException">Если AgentType пустой</exception>
    /// <exception cref="InvalidOperationException">Если конфигурация для данного типа уже зарегистрирована</exception>
    void Register(IAgentConfiguration configuration);

    /// <summary>
    /// Получает конфигурацию агента по типу
    /// </summary>
    /// <param name="agentType">Тип агента</param>
    /// <returns>Конфигурация агента или null, если тип не зарегистрирован</returns>
    IAgentConfiguration? Get(string agentType);

    /// <summary>
    /// Получает все зарегистрированные типы агентов
    /// </summary>
    /// <returns>Коллекция типов агентов</returns>
    IReadOnlyCollection<string> GetRegisteredTypes();

    /// <summary>
    /// Проверяет, зарегистрирована ли конфигурация для типа агента
    /// </summary>
    /// <param name="agentType">Тип агента для проверки</param>
    /// <returns>true если конфигурация зарегистрирована, иначе false</returns>
    bool IsRegistered(string agentType);

    /// <summary>
    /// Получает все зарегистрированные конфигурации
    /// </summary>
    /// <returns>Коллекция всех конфигураций</returns>
    IReadOnlyCollection<IAgentConfiguration> GetAll();
}

/// <summary>
/// Реализация реестра конфигураций агентов
/// </summary>
public class AgentConfigurationRegistry : IAgentConfigurationRegistry
{
    private readonly Dictionary<string, IAgentConfiguration> _configurations;
    private readonly ILogger<AgentConfigurationRegistry> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр AgentConfigurationRegistry
    /// </summary>
    /// <param name="logger">Логгер для отслеживания операций</param>
    public AgentConfigurationRegistry(ILogger<AgentConfigurationRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurations = new Dictionary<string, IAgentConfiguration>(StringComparer.OrdinalIgnoreCase);

        _logger.LogDebug("AgentConfigurationRegistry initialized");
    }

    /// <inheritdoc />
    public void Register(IAgentConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var agentType = configuration.AgentType;

        if (string.IsNullOrWhiteSpace(agentType))
        {
            throw new ArgumentException(
                $"Configuration {configuration.GetType().Name} has empty AgentType",
                nameof(configuration));
        }

        if (_configurations.ContainsKey(agentType))
        {
            throw new InvalidOperationException(
                $"Configuration for agent type '{agentType}' is already registered. " +
                $"Existing: {_configurations[agentType].GetType().Name}, " +
                $"New: {configuration.GetType().Name}");
        }

        _configurations[agentType] = configuration;

        _logger.LogInformation(
            "Registered agent configuration: Type='{AgentType}', Implementation={ImplementationType}",
            agentType,
            configuration.GetType().Name);
    }

    /// <inheritdoc />
    public IAgentConfiguration? Get(string agentType)
    {
        if (string.IsNullOrWhiteSpace(agentType))
        {
            _logger.LogWarning("Get called with empty agentType");
            return null;
        }

        if (_configurations.TryGetValue(agentType, out var configuration))
        {
            _logger.LogDebug("Configuration found for agent type '{AgentType}'", agentType);
            return configuration;
        }

        _logger.LogWarning(
            "No configuration found for agent type '{AgentType}'. Available types: {AvailableTypes}",
            agentType,
            string.Join(", ", _configurations.Keys));

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetRegisteredTypes()
    {
        return _configurations.Keys.ToList();
    }

    /// <inheritdoc />
    public bool IsRegistered(string agentType)
    {
        if (string.IsNullOrWhiteSpace(agentType))
        {
            return false;
        }

        return _configurations.ContainsKey(agentType);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IAgentConfiguration> GetAll()
    {
        return _configurations.Values.ToList();
    }
}
