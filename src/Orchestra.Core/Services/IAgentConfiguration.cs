using Microsoft.Extensions.Logging;

namespace Orchestra.Core.Services;

/// <summary>
/// Базовый интерфейс для конфигураций всех типов агентов в системе
/// </summary>
/// <remarks>
/// <para>
/// Этот интерфейс определяет единый контракт для конфигурации любых агентов,
/// обеспечивая единообразную валидацию и управление настройками через DI.
/// </para>
/// <para>
/// <b>Реализации конфигураций:</b>
/// <list type="bullet">
/// <item><description>ClaudeCodeConfiguration - Конфигурация для Claude Code CLI агентов</description></item>
/// <item><description>ShellAgentConfiguration - Конфигурация для Shell исполнителей</description></item>
/// <item><description>CustomAgentConfiguration - Расширяемые конфигурации для пользовательских агентов</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Регистрация в DI:</b>
/// <code>
/// services.Configure&lt;ClaudeCodeConfiguration&gt;(configuration.GetSection("ClaudeCode"));
/// services.AddSingleton&lt;IAgentConfiguration&gt;(provider =>
///     provider.GetRequiredService&lt;IOptions&lt;ClaudeCodeConfiguration&gt;&gt;().Value);
/// </code>
/// </para>
/// </remarks>
public interface IAgentConfiguration
{
    /// <summary>
    /// Уникальный идентификатор типа агента, к которому относится эта конфигурация
    /// </summary>
    /// <value>
    /// Строка-идентификатор типа агента (например, "claude-code", "shell", "custom").
    /// Должна соответствовать значению AgentType в IAgentExecutor.
    /// </value>
    /// <remarks>
    /// Используется для маршрутизации конфигурации к правильному исполнителю агента
    /// через AgentConfigurationFactory и AgentExecutorRegistry.
    /// </remarks>
    string AgentType { get; }

    /// <summary>
    /// Максимальное количество одновременных выполнений команд для агента
    /// </summary>
    /// <value>
    /// Целое число от 1 до 100. По умолчанию рекомендуется 3.
    /// </value>
    /// <remarks>
    /// <para>
    /// Используется в BaseAgentExecutor для инициализации SemaphoreSlim.
    /// Ограничивает параллельные выполнения для предотвращения перегрузки агента.
    /// </para>
    /// <para>
    /// Типичные значения:
    /// <list type="bullet">
    /// <item><description>Claude Code: 1-3 (из-за rate limits API)</description></item>
    /// <item><description>Shell Executor: 5-10 (легковесные локальные процессы)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    int MaxConcurrentExecutions { get; }

    /// <summary>
    /// Таймаут по умолчанию для выполнения команд агентом
    /// </summary>
    /// <value>
    /// TimeSpan от 10 секунд до 1 часа. По умолчанию 10 минут.
    /// </value>
    /// <remarks>
    /// Используется агентами для установки максимального времени ожидания
    /// завершения команды. При превышении команда будет отменена.
    /// </remarks>
    TimeSpan DefaultTimeout { get; }

    /// <summary>
    /// Количество попыток повтора при сбоях выполнения
    /// </summary>
    /// <value>
    /// Целое число от 0 до 10. 0 означает отсутствие retry логики.
    /// </value>
    /// <remarks>
    /// <para>
    /// Используется в retry wrapper или базовом executor'е для автоматического
    /// повторения неудачных команд при временных сбоях.
    /// </para>
    /// <para>
    /// Повторяются только транзиентные ошибки:
    /// <list type="bullet">
    /// <item><description>Network timeouts</description></item>
    /// <item><description>HTTP 503 Service Unavailable</description></item>
    /// <item><description>Process termination errors</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    int RetryAttempts { get; }

    /// <summary>
    /// Задержка между попытками повтора
    /// </summary>
    /// <value>
    /// TimeSpan от 100 миллисекунд до 30 секунд. По умолчанию 2 секунды.
    /// </value>
    /// <remarks>
    /// Используется с экспоненциальным backoff: delay * (2 ^ attemptNumber)
    /// </remarks>
    TimeSpan RetryDelay { get; }

    /// <summary>
    /// Валидирует конфигурацию агента и возвращает список ошибок
    /// </summary>
    /// <returns>
    /// Список строк с описаниями ошибок валидации.
    /// Пустой список означает успешную валидацию.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Метод вызывается:
    /// <list type="bullet">
    /// <item><description>При регистрации агента через AgentConfigurationFactory</description></item>
    /// <item><description>При старте приложения для проверки конфигураций</description></item>
    /// <item><description>При обновлении конфигурации в runtime (hot reload)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Типичные валидации:
    /// <list type="bullet">
    /// <item><description>Проверка существования путей к исполняемым файлам</description></item>
    /// <item><description>Проверка допустимости диапазонов значений</description></item>
    /// <item><description>Валидация зависимостей между настройками</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public IReadOnlyList&lt;string&gt; Validate()
    /// {
    ///     var errors = new List&lt;string&gt;();
    ///
    ///     if (MaxConcurrentExecutions &lt; 1 || MaxConcurrentExecutions &gt; 100)
    ///     {
    ///         errors.Add("MaxConcurrentExecutions must be between 1 and 100");
    ///     }
    ///
    ///     if (DefaultTimeout &lt; TimeSpan.FromSeconds(10))
    ///     {
    ///         errors.Add("DefaultTimeout must be at least 10 seconds");
    ///     }
    ///
    ///     return errors;
    /// }
    /// </code>
    /// </example>
    IReadOnlyList<string> Validate();
}

/// <summary>
/// Фабрика для создания и валидации конфигураций агентов по типу
/// </summary>
/// <remarks>
/// <para>
/// Обеспечивает централизованное управление конфигурациями агентов,
/// автоматическую маршрутизацию конфигураций к правильным типам агентов
/// и валидацию настроек при регистрации.
/// </para>
/// <para>
/// <b>Регистрация в DI:</b>
/// <code>
/// services.AddSingleton&lt;IAgentConfigurationFactory, AgentConfigurationFactory&gt;();
/// </code>
/// </para>
/// </remarks>
public interface IAgentConfigurationFactory
{
    /// <summary>
    /// Получает конфигурацию для указанного типа агента
    /// </summary>
    /// <param name="agentType">Тип агента (например, "claude-code", "shell")</param>
    /// <returns>Конфигурация агента или null, если тип не зарегистрирован</returns>
    /// <remarks>
    /// Вызывается перед созданием экземпляра агента для передачи настроек.
    /// </remarks>
    IAgentConfiguration? GetConfiguration(string agentType);

    /// <summary>
    /// Регистрирует конфигурацию для типа агента
    /// </summary>
    /// <param name="configuration">Конфигурация для регистрации</param>
    /// <exception cref="InvalidOperationException">
    /// Если конфигурация для данного типа уже зарегистрирована
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Если валидация конфигурации не прошла (Validate() вернул ошибки)
    /// </exception>
    void RegisterConfiguration(IAgentConfiguration configuration);

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
    /// Валидирует все зарегистрированные конфигурации
    /// </summary>
    /// <returns>
    /// Dictionary где ключ - тип агента, значение - список ошибок валидации.
    /// Пустой список ошибок означает успешную валидацию конфигурации.
    /// </returns>
    /// <remarks>
    /// Вызывается при старте приложения для проверки всех конфигураций.
    /// </remarks>
    IReadOnlyDictionary<string, IReadOnlyList<string>> ValidateAll();
}

/// <summary>
/// Реализация фабрики конфигураций агентов через композицию Registry и Validator
/// </summary>
/// <remarks>
/// Использует композицию для разделения ответственности (SRP):
/// - Registry отвечает за хранение конфигураций
/// - Validator отвечает за валидацию
/// - Factory координирует их работу
/// </remarks>
public class AgentConfigurationFactory : IAgentConfigurationFactory
{
    private readonly IAgentConfigurationRegistry _registry;
    private readonly IAgentConfigurationValidator _validator;
    private readonly ILogger<AgentConfigurationFactory> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр AgentConfigurationFactory
    /// </summary>
    /// <param name="registry">Реестр для хранения конфигураций</param>
    /// <param name="validator">Валидатор конфигураций</param>
    /// <param name="configurations">
    /// Все зарегистрированные конфигурации агентов из DI контейнера.
    /// Фабрика автоматически индексирует их по AgentType.
    /// </param>
    /// <param name="logger">Логгер для отслеживания операций фабрики</param>
    /// <exception cref="ArgumentNullException">
    /// Если registry, validator, configurations или logger равны null
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Если валидация любой конфигурации не прошла
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Если найдены дубликаты типов агентов
    /// </exception>
    public AgentConfigurationFactory(
        IAgentConfigurationRegistry registry,
        IAgentConfigurationValidator validator,
        IEnumerable<IAgentConfiguration> configurations,
        ILogger<AgentConfigurationFactory> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (configurations == null)
        {
            throw new ArgumentNullException(nameof(configurations));
        }

        foreach (var config in configurations)
        {
            RegisterConfiguration(config);
        }

        _logger.LogInformation(
            "AgentConfigurationFactory initialized with {Count} configuration(s): {AgentTypes}",
            _registry.GetRegisteredTypes().Count,
            string.Join(", ", _registry.GetRegisteredTypes()));
    }

    /// <inheritdoc />
    public IAgentConfiguration? GetConfiguration(string agentType)
    {
        return _registry.Get(agentType);
    }

    /// <inheritdoc />
    public void RegisterConfiguration(IAgentConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // Валидируем конфигурацию перед регистрацией
        var validationErrors = _validator.Validate(configuration);
        if (validationErrors.Count > 0)
        {
            var errorMessage = $"Configuration validation failed for '{configuration.AgentType}': " +
                              string.Join("; ", validationErrors);
            _logger.LogError("Configuration validation failed: {ValidationErrors}", errorMessage);
            throw new ArgumentException(errorMessage, nameof(configuration));
        }

        // Регистрируем в реестре
        _registry.Register(configuration);

        _logger.LogDebug(
            "Registered agent configuration via factory: Type='{AgentType}', Implementation={ImplementationType}",
            configuration.AgentType,
            configuration.GetType().Name);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetRegisteredTypes()
    {
        return _registry.GetRegisteredTypes();
    }

    /// <inheritdoc />
    public bool IsRegistered(string agentType)
    {
        return _registry.IsRegistered(agentType);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidateAll()
    {
        return _validator.ValidateAll(_registry);
    }
}
