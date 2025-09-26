namespace Orchestra.Core.Services;

/// <summary>
/// Интерфейс для базовых операций с агентом Claude Code
/// Этот интерфейс определен в Orchestra.Core, чтобы избежать круговых зависимостей
/// </summary>
public interface IClaudeCodeAgentService
{
    /// <summary>
    /// Проверяет доступность агента Claude Code по его идентификатору
    /// </summary>
    /// <param name="agentId">Идентификатор агента (не может быть null или пустым)</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True если агент доступен, иначе false</returns>
    /// <exception cref="ArgumentException">Если agentId пуст или null</exception>
    Task<bool> IsAgentAvailableAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает версию агента Claude Code для совместимости и диагностики
    /// </summary>
    /// <param name="agentId">Идентификатор агента (не может быть null или пустым)</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Строка с информацией о версии агента</returns>
    /// <exception cref="ArgumentException">Если agentId пуст или null</exception>
    Task<string> GetAgentVersionAsync(string agentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Интерфейс для выполнения команд через агента Claude Code
/// </summary>
public interface IClaudeCodeExecutor
{
    /// <summary>
    /// Выполняет команду через агента Claude Code с дополнительными параметрами
    /// </summary>
    /// <param name="agentId">Идентификатор агента (не может быть null или пустым)</param>
    /// <param name="command">Команда для выполнения (не может быть null или пустой)</param>
    /// <param name="parameters">Дополнительные параметры выполнения (не может быть null)</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат выполнения команды с метаданными Claude Code</returns>
    /// <exception cref="ArgumentException">Если agentId или command пусты или null</exception>
    /// <exception cref="ArgumentNullException">Если parameters равно null</exception>
    Task<ClaudeCodeExecutionResult> ExecuteCommandAsync(
        string agentId,
        string command,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Составной интерфейс, объединяющий все функции Claude Code сервиса
/// Определен в Orchestra.Core для предотвращения круговых зависимостей
/// </summary>
public interface IClaudeCodeCoreService : IClaudeCodeAgentService, IClaudeCodeExecutor
{
}

/// <summary>
/// Результат выполнения команды Claude Code с расширенными метаданными
/// </summary>
public class ClaudeCodeExecutionResult : AgentExecutionResponse
{
    /// <summary>
    /// Идентификатор агента, который выполнил команду
    /// </summary>
    public required string AgentId { get; set; }

    /// <summary>
    /// Идентификатор workflow, если команда была частью workflow
    /// </summary>
    public string? WorkflowId { get; set; }

    /// <summary>
    /// Список выполненных шагов в процессе обработки команды
    /// </summary>
    public List<string> ExecutedSteps { get; set; } = [];

    /// <summary>
    /// Метаданные workflow для отслеживания состояния и прогресса
    /// </summary>
    public Dictionary<string, object> WorkflowMetadata { get; set; } = [];
}