using Microsoft.Extensions.Logging;

namespace Orchestra.Core.Services;

/// <summary>
/// Базовый абстрактный класс для реализации IAgentExecutor с общей функциональностью
/// </summary>
/// <typeparam name="TImplementation">Конкретный тип реализации для логирования</typeparam>
/// <remarks>
/// <para>
/// Этот базовый класс предоставляет общую инфраструктуру для всех исполнителей агентов:
/// <list type="bullet">
/// <item><description>Валидацию входных параметров</description></item>
/// <item><description>Ограничение параллельных выполнений через семафор</description></item>
/// <item><description>Стандартизированное логирование</description></item>
/// <item><description>Отслеживание времени выполнения</description></item>
/// <item><description>Обработку cancellation токенов</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Шаблон использования:</b>
/// <code>
/// public class MyAgentExecutor : BaseAgentExecutor&lt;MyAgentExecutor&gt;
/// {
///     public override string AgentType => "my-agent";
///
///     public MyAgentExecutor(ILogger&lt;MyAgentExecutor&gt; logger)
///         : base(logger, maxConcurrentExecutions: 5)
///     {
///     }
///
///     protected override async Task&lt;AgentExecutionResponse&gt; ExecuteCommandCoreAsync(
///         string command, string workingDirectory, CancellationToken cancellationToken)
///     {
///         // Реализация специфичной логики выполнения агента
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class BaseAgentExecutor<TImplementation> : IAgentExecutor
    where TImplementation : class
{
    /// <summary>
    /// Логгер для отслеживания операций агента
    /// </summary>
    protected readonly ILogger<TImplementation> Logger;

    /// <summary>
    /// Конфигурация агента с настройками выполнения
    /// </summary>
    protected readonly IAgentConfiguration? Configuration;

    /// <summary>
    /// Семафор для ограничения количества параллельных выполнений
    /// </summary>
    private readonly SemaphoreSlim _executionSemaphore;

    /// <summary>
    /// Максимальное количество параллельных выполнений для этого типа агента
    /// </summary>
    protected int MaxConcurrentExecutions { get; }

    /// <inheritdoc />
    public abstract string AgentType { get; }

    /// <summary>
    /// Инициализирует новый экземпляр BaseAgentExecutor
    /// </summary>
    /// <param name="logger">Логгер для отслеживания операций</param>
    /// <param name="configuration">
    /// Конфигурация агента (необязательно). Если указана, MaxConcurrentExecutions берется из неё.
    /// </param>
    /// <param name="maxConcurrentExecutions">
    /// Максимальное количество параллельных выполнений (по умолчанию 3).
    /// Используется только если configuration == null или configuration.MaxConcurrentExecutions &lt;= 0.
    /// </param>
    /// <exception cref="ArgumentNullException">Если logger равен null</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Если maxConcurrentExecutions меньше или равно 0 (и не переопределено конфигурацией)
    /// </exception>
    protected BaseAgentExecutor(
        ILogger<TImplementation> logger,
        IAgentConfiguration? configuration = null,
        int maxConcurrentExecutions = 3)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Configuration = configuration;

        // Приоритет: конфигурация > параметр конструктора
        var effectiveMaxConcurrency = configuration?.MaxConcurrentExecutions ?? maxConcurrentExecutions;

        if (effectiveMaxConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrentExecutions),
                effectiveMaxConcurrency,
                "Max concurrent executions must be greater than 0");
        }

        MaxConcurrentExecutions = effectiveMaxConcurrency;
        _executionSemaphore = new SemaphoreSlim(effectiveMaxConcurrency, effectiveMaxConcurrency);

        Logger.LogDebug(
            "[{AgentType}] BaseAgentExecutor initialized with MaxConcurrentExecutions={MaxConcurrency} (from {Source})",
            GetType().Name,
            MaxConcurrentExecutions,
            configuration != null ? "configuration" : "constructor parameter");
    }

    /// <inheritdoc />
    public async Task<AgentExecutionResponse> ExecuteCommandAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        // Валидация входных параметров
        ValidateInputs(command, ref workingDirectory);

        var startTime = DateTime.UtcNow;
        var executionId = Guid.NewGuid().ToString()[..8];

        Logger.LogInformation(
            "[{AgentType}][{ExecutionId}] Starting execution in {WorkingDirectory}. Command length: {CommandLength} chars",
            AgentType,
            executionId,
            workingDirectory,
            command.Length);

        // Ожидаем доступного слота для выполнения
        await _executionSemaphore.WaitAsync(cancellationToken);

        try
        {
            // Выполняем специфичную логику агента
            var response = await ExecuteCommandCoreAsync(command, workingDirectory, cancellationToken);

            // Обогащаем ответ общей информацией
            var executionTime = DateTime.UtcNow - startTime;
            response.ExecutionTime = executionTime;

            // Добавляем стандартные метаданные, если их ещё нет
            if (!response.Metadata.ContainsKey("AgentType"))
            {
                response.Metadata["AgentType"] = AgentType;
            }

            if (!response.Metadata.ContainsKey("ExecutionId"))
            {
                response.Metadata["ExecutionId"] = executionId;
            }

            if (!response.Metadata.ContainsKey("WorkingDirectory"))
            {
                response.Metadata["WorkingDirectory"] = workingDirectory;
            }

            Logger.LogInformation(
                "[{AgentType}][{ExecutionId}] Execution completed. Success: {Success}, Duration: {Duration}ms, Output length: {OutputLength} chars",
                AgentType,
                executionId,
                response.Success,
                executionTime.TotalMilliseconds,
                response.Output.Length);

            if (!response.Success)
            {
                Logger.LogWarning(
                    "[{AgentType}][{ExecutionId}] Execution failed: {ErrorMessage}",
                    AgentType,
                    executionId,
                    response.ErrorMessage ?? "Unknown error");
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            var executionTime = DateTime.UtcNow - startTime;

            Logger.LogWarning(
                "[{AgentType}][{ExecutionId}] Execution cancelled after {Duration}ms",
                AgentType,
                executionId,
                executionTime.TotalMilliseconds);

            return new AgentExecutionResponse
            {
                Success = false,
                ErrorMessage = "Operation was cancelled",
                ExecutionTime = executionTime,
                Metadata = new Dictionary<string, object>
                {
                    { "AgentType", AgentType },
                    { "ExecutionId", executionId },
                    { "WorkingDirectory", workingDirectory },
                    { "Cancelled", true }
                }
            };
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - startTime;

            Logger.LogError(
                ex,
                "[{AgentType}][{ExecutionId}] Execution failed with exception after {Duration}ms: {ErrorMessage}",
                AgentType,
                executionId,
                executionTime.TotalMilliseconds,
                ex.Message);

            return new AgentExecutionResponse
            {
                Success = false,
                ErrorMessage = $"Execution failed: {ex.Message}",
                ExecutionTime = executionTime,
                Metadata = new Dictionary<string, object>
                {
                    { "AgentType", AgentType },
                    { "ExecutionId", executionId },
                    { "WorkingDirectory", workingDirectory },
                    { "ExceptionType", ex.GetType().Name }
                }
            };
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    /// <summary>
    /// Выполняет специфичную для агента логику выполнения команды
    /// </summary>
    /// <param name="command">Команда для выполнения (уже провалидирована)</param>
    /// <param name="workingDirectory">Рабочая директория (уже провалидирована и существует)</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат выполнения команды агентом</returns>
    /// <remarks>
    /// <para>
    /// Этот метод вызывается из ExecuteCommandAsync после всех проверок и инициализации.
    /// Реализация не должна заботиться о:
    /// <list type="bullet">
    /// <item><description>Валидации входных параметров</description></item>
    /// <item><description>Ограничении параллельных выполнений</description></item>
    /// <item><description>Базовом логировании</description></item>
    /// <item><description>Отслеживании времени выполнения</description></item>
    /// <item><description>Обработке общих исключений</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Реализация должна:
    /// <list type="bullet">
    /// <item><description>Выполнить специфичную логику агента</description></item>
    /// <item><description>Заполнить Success, Output и ErrorMessage</description></item>
    /// <item><description>Добавить специфичные метаданные в Metadata</description></item>
    /// <item><description>Корректно обрабатывать cancellationToken</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    protected abstract Task<AgentExecutionResponse> ExecuteCommandCoreAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Валидирует входные параметры команды и рабочей директории
    /// </summary>
    /// <param name="command">Команда для валидации</param>
    /// <param name="workingDirectory">
    /// Рабочая директория для валидации. Будет изменена на текущую директорию,
    /// если передана пустая строка или null.
    /// </param>
    /// <exception cref="ArgumentException">Если command пустая строка или null</exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Если workingDirectory не существует и не может быть создана
    /// </exception>
    protected virtual void ValidateInputs(string command, ref string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command cannot be null or empty", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = Environment.CurrentDirectory;
            Logger.LogDebug(
                "[{AgentType}] Working directory not specified, using current directory: {WorkingDirectory}",
                AgentType,
                workingDirectory);
        }

        // Ensure working directory exists - create if needed
        if (!Directory.Exists(workingDirectory))
        {
            Logger.LogInformation(
                "[{AgentType}] Working directory does not exist, creating: {WorkingDirectory}",
                AgentType,
                workingDirectory);

            try
            {
                Directory.CreateDirectory(workingDirectory);
            }
            catch (Exception ex)
            {
                throw new DirectoryNotFoundException(
                    $"Working directory not found and could not be created: {workingDirectory}",
                    ex);
            }
        }
    }

    /// <summary>
    /// Освобождает ресурсы, используемые executor'ом
    /// </summary>
    public virtual void Dispose()
    {
        _executionSemaphore?.Dispose();
    }
}
