using Microsoft.Extensions.Logging;

namespace Orchestra.Core.Services;

/// <summary>
/// Реестр всех зарегистрированных исполнителей агентов для маршрутизации задач
/// </summary>
/// <remarks>
/// <para>
/// AgentExecutorRegistry служит центральной точкой доступа к различным типам агентов,
/// обеспечивая маршрутизацию задач к правильному исполнителю на основе типа агента.
/// </para>
/// <para>
/// <b>Функциональность:</b>
/// <list type="bullet">
/// <item><description>Регистрация исполнителей агентов по типу</description></item>
/// <item><description>Получение исполнителя по строковому идентификатору типа</description></item>
/// <item><description>Перечисление всех доступных типов агентов</description></item>
/// <item><description>Проверка наличия исполнителя для заданного типа</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Регистрация в DI:</b>
/// <code>
/// services.AddSingleton&lt;IAgentExecutorRegistry, AgentExecutorRegistry&gt;();
/// services.AddSingleton&lt;IAgentExecutor, ClaudeCodeExecutor&gt;();
/// services.AddSingleton&lt;IAgentExecutor, ShellExecutor&gt;();
/// </code>
/// Реестр автоматически обнаружит все IAgentExecutor из DI контейнера.
/// </para>
/// </remarks>
/// <example>
/// Использование в TaskExecutionJob:
/// <code>
/// var executor = _registry.GetExecutor(task.AgentType);
/// if (executor == null)
/// {
///     throw new InvalidOperationException($"No executor found for agent type: {task.AgentType}");
/// }
/// var result = await executor.ExecuteCommandAsync(task.Command, task.WorkingDirectory);
/// </code>
/// </example>
public interface IAgentExecutorRegistry
{
    /// <summary>
    /// Получает исполнителя агента по типу
    /// </summary>
    /// <param name="agentType">Тип агента (например, "claude-code", "shell")</param>
    /// <returns>Исполнитель агента или null, если тип не зарегистрирован</returns>
    IAgentExecutor? GetExecutor(string agentType);

    /// <summary>
    /// Получает все зарегистрированные типы агентов
    /// </summary>
    /// <returns>Коллекция типов агентов</returns>
    IReadOnlyCollection<string> GetRegisteredTypes();

    /// <summary>
    /// Проверяет, зарегистрирован ли исполнитель для заданного типа
    /// </summary>
    /// <param name="agentType">Тип агента для проверки</param>
    /// <returns>true если исполнитель зарегистрирован, иначе false</returns>
    bool IsRegistered(string agentType);

    /// <summary>
    /// Получает все зарегистрированные исполнители
    /// </summary>
    /// <returns>Коллекция всех исполнителей агентов</returns>
    IReadOnlyCollection<IAgentExecutor> GetAllExecutors();
}

/// <summary>
/// Реализация реестра исполнителей агентов
/// </summary>
public class AgentExecutorRegistry : IAgentExecutorRegistry
{
    private readonly Dictionary<string, IAgentExecutor> _executors;
    private readonly ILogger<AgentExecutorRegistry> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр AgentExecutorRegistry
    /// </summary>
    /// <param name="executors">
    /// Все зарегистрированные исполнители агентов из DI контейнера.
    /// Реестр автоматически индексирует их по AgentType.
    /// </param>
    /// <param name="logger">Логгер для отслеживания операций реестра</param>
    /// <exception cref="ArgumentNullException">Если executors или logger равны null</exception>
    /// <exception cref="InvalidOperationException">
    /// Если найдены дубликаты типов агентов
    /// </exception>
    public AgentExecutorRegistry(
        IEnumerable<IAgentExecutor> executors,
        ILogger<AgentExecutorRegistry> logger)
    {
        if (executors == null)
        {
            throw new ArgumentNullException(nameof(executors));
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Индексируем исполнителей по типу
        _executors = new Dictionary<string, IAgentExecutor>(StringComparer.OrdinalIgnoreCase);

        foreach (var executor in executors)
        {
            var agentType = executor.AgentType;

            if (string.IsNullOrWhiteSpace(agentType))
            {
                _logger.LogWarning(
                    "Skipping executor {ExecutorType} with empty AgentType",
                    executor.GetType().Name);
                continue;
            }

            if (_executors.ContainsKey(agentType))
            {
                throw new InvalidOperationException(
                    $"Duplicate agent type '{agentType}' detected. " +
                    $"Existing: {_executors[agentType].GetType().Name}, " +
                    $"New: {executor.GetType().Name}");
            }

            _executors[agentType] = executor;

            _logger.LogInformation(
                "Registered agent executor: Type='{AgentType}', Implementation={ImplementationType}",
                agentType,
                executor.GetType().Name);
        }

        _logger.LogInformation(
            "AgentExecutorRegistry initialized with {Count} executor(s): {AgentTypes}",
            _executors.Count,
            string.Join(", ", _executors.Keys));
    }

    /// <inheritdoc />
    public IAgentExecutor? GetExecutor(string agentType)
    {
        if (string.IsNullOrWhiteSpace(agentType))
        {
            _logger.LogWarning("GetExecutor called with empty agentType");
            return null;
        }

        if (_executors.TryGetValue(agentType, out var executor))
        {
            _logger.LogDebug("Executor found for agent type '{AgentType}'", agentType);
            return executor;
        }

        _logger.LogWarning(
            "No executor found for agent type '{AgentType}'. Available types: {AvailableTypes}",
            agentType,
            string.Join(", ", _executors.Keys));

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetRegisteredTypes()
    {
        return _executors.Keys.ToList();
    }

    /// <inheritdoc />
    public bool IsRegistered(string agentType)
    {
        if (string.IsNullOrWhiteSpace(agentType))
        {
            return false;
        }

        return _executors.ContainsKey(agentType);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IAgentExecutor> GetAllExecutors()
    {
        return _executors.Values.ToList();
    }
}
