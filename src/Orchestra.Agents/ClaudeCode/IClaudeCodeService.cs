using Orchestra.Core.Services;
using ClaudeCodeExecutionResult = Orchestra.Core.Services.ClaudeCodeExecutionResult;

namespace Orchestra.Agents.ClaudeCode;

/// <summary>
/// Интерфейс для базовых операций с агентом Claude Code
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
/// Интерфейс для выполнения workflow через агента Claude Code
/// </summary>
public interface IClaudeCodeWorkflowService
{
    /// <summary>
    /// Выполняет markdown workflow через агента Claude Code
    /// </summary>
    /// <param name="agentId">Идентификатор агента (не может быть null или пустым)</param>
    /// <param name="workflow">Определение workflow для выполнения (не может быть null)</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат выполнения workflow с детализированной информацией</returns>
    /// <exception cref="ArgumentException">Если agentId пуст или null</exception>
    /// <exception cref="ArgumentNullException">Если workflow равно null</exception>
    Task<ClaudeCodeWorkflowResult> ExecuteWorkflowAsync(
        string agentId,
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Составной интерфейс, объединяющий все функции Claude Code сервиса
/// </summary>
public interface IClaudeCodeService : IClaudeCodeAgentService, IClaudeCodeExecutor, IClaudeCodeWorkflowService
{
}


/// <summary>
/// Результат выполнения workflow с детализированной информацией о процессе
/// </summary>
public class ClaudeCodeWorkflowResult : ClaudeCodeExecutionResult
{
    /// <summary>
    /// Общее количество шагов в workflow
    /// </summary>
    public int TotalSteps { get; set; }

    /// <summary>
    /// Количество успешно выполненных шагов
    /// </summary>
    public int CompletedSteps { get; set; }

    /// <summary>
    /// Количество пропущенных шагов
    /// </summary>
    public int SkippedSteps { get; set; }

    /// <summary>
    /// Количество шагов, завершившихся с ошибкой
    /// </summary>
    public int FailedSteps { get; set; }

    /// <summary>
    /// Детализированные результаты каждого шага workflow
    /// </summary>
    public List<WorkflowStepResult> StepResults { get; set; } = [];
}

/// <summary>
/// Результат выполнения отдельного шага workflow
/// </summary>
public class WorkflowStepResult
{
    /// <summary>
    /// Номер шага в workflow
    /// </summary>
    public int StepNumber { get; set; }

    /// <summary>
    /// Наименование шага
    /// </summary>
    public required string StepName { get; set; }

    /// <summary>
    /// Статус выполнения шага
    /// </summary>
    public WorkflowStepStatus Status { get; set; }

    /// <summary>
    /// Выходные данные шага
    /// </summary>
    public string Output { get; set; } = "";

    /// <summary>
    /// Сообщение об ошибке, если шаг завершился неудачно
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Время выполнения шага
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// Статус выполнения шага workflow
/// </summary>
public enum WorkflowStepStatus
{
    /// <summary>
    /// Шаг ожидает выполнения
    /// </summary>
    Pending,

    /// <summary>
    /// Шаг выполняется
    /// </summary>
    Running,

    /// <summary>
    /// Шаг выполнен успешно
    /// </summary>
    Completed,

    /// <summary>
    /// Шаг пропущен
    /// </summary>
    Skipped,

    /// <summary>
    /// Шаг завершился с ошибкой
    /// </summary>
    Failed
}

/// <summary>
/// Определение workflow для выполнения через Claude Code агента
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// Уникальный идентификатор workflow
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Наименование workflow
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Описание workflow
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Путь к файлу markdown workflow
    /// </summary>
    public required string MarkdownFilePath { get; set; }

    /// <summary>
    /// Рабочая директория для выполнения workflow
    /// </summary>
    public required string WorkingDirectory { get; set; }

    /// <summary>
    /// Параметры конфигурации workflow
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = [];

    /// <summary>
    /// Таймаут для выполнения всего workflow
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Разрешенные инструменты для workflow
    /// </summary>
    public string[] AllowedTools { get; set; } = [];
}