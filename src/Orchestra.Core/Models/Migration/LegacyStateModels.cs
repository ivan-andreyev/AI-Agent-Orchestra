using System.Text.Json.Serialization;

namespace Orchestra.Core.Models.Migration;

/// <summary>
/// Модель для десериализации legacy orchestrator-state.json файла
/// Представляет структуру JSON-файла состояния оркестратора до миграции на SQLite
/// </summary>
public class LegacyOrchestratorState
{
    /// <summary>
    /// Коллекция агентов из JSON состояния
    /// </summary>
    [JsonPropertyName("Agents")]
    public Dictionary<string, LegacyAgent> Agents { get; set; } = new();

    /// <summary>
    /// Очередь задач из JSON состояния
    /// </summary>
    [JsonPropertyName("TaskQueue")]
    public List<LegacyTask> TaskQueue { get; set; } = new();

    /// <summary>
    /// Коллекция репозиториев из JSON состояния
    /// </summary>
    [JsonPropertyName("Repositories")]
    public Dictionary<string, LegacyRepository> Repositories { get; set; } = new();

    /// <summary>
    /// Текущее состояние оркестратора
    /// </summary>
    [JsonPropertyName("CurrentState")]
    public string? CurrentState { get; set; }

    /// <summary>
    /// Время последнего обновления состояния
    /// </summary>
    [JsonPropertyName("LastUpdated")]
    public DateTime? LastUpdated { get; set; }

    /// <summary>
    /// Версия формата состояния
    /// </summary>
    [JsonPropertyName("Version")]
    public string? Version { get; set; }

    /// <summary>
    /// Дополнительные настройки из JSON
    /// </summary>
    [JsonPropertyName("Settings")]
    public Dictionary<string, object>? Settings { get; set; }
}

/// <summary>
/// Legacy модель агента из JSON файла состояния
/// </summary>
public class LegacyAgent
{
    /// <summary>
    /// Идентификатор агента
    /// </summary>
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Имя агента
    /// </summary>
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Тип агента
    /// </summary>
    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Путь к репозиторию
    /// </summary>
    [JsonPropertyName("RepositoryPath")]
    public string RepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Статус агента (числовое значение)
    /// </summary>
    [JsonPropertyName("Status")]
    public int Status { get; set; }

    /// <summary>
    /// Время последнего пинга
    /// </summary>
    [JsonPropertyName("LastPing")]
    public DateTime LastPing { get; set; }

    /// <summary>
    /// Текущая задача агента
    /// </summary>
    [JsonPropertyName("CurrentTask")]
    public string? CurrentTask { get; set; }

    /// <summary>
    /// Идентификатор сессии
    /// </summary>
    [JsonPropertyName("SessionId")]
    public string? SessionId { get; set; }

    /// <summary>
    /// Идентификатор текущей задачи
    /// </summary>
    [JsonPropertyName("CurrentTaskId")]
    public string? CurrentTaskId { get; set; }

    /// <summary>
    /// Время последней активности
    /// </summary>
    [JsonPropertyName("LastActiveTime")]
    public DateTime? LastActiveTime { get; set; }

    /// <summary>
    /// Количество выполненных задач
    /// </summary>
    [JsonPropertyName("TasksCompleted")]
    public int TasksCompleted { get; set; }

    /// <summary>
    /// Дополнительные свойства агента
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}

/// <summary>
/// Legacy модель задачи из JSON файла состояния
/// </summary>
public class LegacyTask
{
    /// <summary>
    /// Идентификатор задачи
    /// </summary>
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор агента, выполняющего задачу
    /// </summary>
    [JsonPropertyName("AgentId")]
    public string? AgentId { get; set; }

    /// <summary>
    /// Команда для выполнения
    /// </summary>
    [JsonPropertyName("Command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Путь к репозиторию
    /// </summary>
    [JsonPropertyName("RepositoryPath")]
    public string RepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Приоритет задачи (числовое значение)
    /// </summary>
    [JsonPropertyName("Priority")]
    public int Priority { get; set; }

    /// <summary>
    /// Статус задачи (числовое значение)
    /// </summary>
    [JsonPropertyName("Status")]
    public int Status { get; set; }

    /// <summary>
    /// Время создания задачи
    /// </summary>
    [JsonPropertyName("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Время начала выполнения
    /// </summary>
    [JsonPropertyName("StartedAt")]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Время завершения
    /// </summary>
    [JsonPropertyName("CompletedAt")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Результат выполнения задачи
    /// </summary>
    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Количество попыток выполнения
    /// </summary>
    [JsonPropertyName("RetryCount")]
    public int RetryCount { get; set; }

    /// <summary>
    /// Идентификатор корреляции
    /// </summary>
    [JsonPropertyName("CorrelationId")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Идентификатор рабочего процесса
    /// </summary>
    [JsonPropertyName("WorkflowId")]
    public string? WorkflowId { get; set; }

    /// <summary>
    /// Шаг рабочего процесса
    /// </summary>
    [JsonPropertyName("WorkflowStep")]
    public int WorkflowStep { get; set; }

    /// <summary>
    /// Дополнительные свойства задачи
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}

/// <summary>
/// Legacy модель репозитория из JSON файла состояния
/// </summary>
public class LegacyRepository
{
    /// <summary>
    /// Идентификатор репозитория
    /// </summary>
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Имя репозитория
    /// </summary>
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Путь к репозиторию
    /// </summary>
    [JsonPropertyName("Path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Описание репозитория
    /// </summary>
    [JsonPropertyName("Description")]
    public string? Description { get; set; }

    /// <summary>
    /// Тип репозитория
    /// </summary>
    [JsonPropertyName("Type")]
    public string? Type { get; set; }

    /// <summary>
    /// Активность репозитория
    /// </summary>
    [JsonPropertyName("IsActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Время создания
    /// </summary>
    [JsonPropertyName("CreatedAt")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Время последнего обновления
    /// </summary>
    [JsonPropertyName("UpdatedAt")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Время последнего доступа
    /// </summary>
    [JsonPropertyName("LastAccessedAt")]
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// Ветка по умолчанию
    /// </summary>
    [JsonPropertyName("DefaultBranch")]
    public string? DefaultBranch { get; set; }

    /// <summary>
    /// Настройки репозитория в JSON
    /// </summary>
    [JsonPropertyName("Settings")]
    public Dictionary<string, object>? Settings { get; set; }

    /// <summary>
    /// Разрешенные операции
    /// </summary>
    [JsonPropertyName("AllowedOperations")]
    public List<string>? AllowedOperations { get; set; }

    /// <summary>
    /// Статистика задач
    /// </summary>
    [JsonPropertyName("TotalTasks")]
    public int TotalTasks { get; set; }

    /// <summary>
    /// Успешно выполненные задачи
    /// </summary>
    [JsonPropertyName("SuccessfulTasks")]
    public int SuccessfulTasks { get; set; }

    /// <summary>
    /// Неуспешные задачи
    /// </summary>
    [JsonPropertyName("FailedTasks")]
    public int FailedTasks { get; set; }

    /// <summary>
    /// Дополнительные свойства репозитория
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}

/// <summary>
/// Маппинг статусов агентов из legacy JSON в новые enum значения
/// </summary>
public static class LegacyStatusMapping
{
    /// <summary>
    /// Преобразование legacy статуса агента в новый enum
    /// </summary>
    /// <param name="legacyStatus">Legacy статус (числовое значение)</param>
    /// <returns>Статус агента в новом формате</returns>
    public static Data.Entities.AgentStatus MapAgentStatus(int legacyStatus)
    {
        return legacyStatus switch
        {
            0 => Data.Entities.AgentStatus.Unknown,
            1 => Data.Entities.AgentStatus.Idle,
            2 => Data.Entities.AgentStatus.Busy,
            3 => Data.Entities.AgentStatus.Error,
            4 => Data.Entities.AgentStatus.Offline,
            _ => Data.Entities.AgentStatus.Unknown
        };
    }

    /// <summary>
    /// Преобразование legacy статуса задачи в новый enum
    /// </summary>
    /// <param name="legacyStatus">Legacy статус (числовое значение)</param>
    /// <returns>Статус задачи в новом формате</returns>
    public static TaskStatus MapTaskStatus(int legacyStatus)
    {
        return legacyStatus switch
        {
            0 => TaskStatus.Pending,
            1 => TaskStatus.Assigned,
            2 => TaskStatus.InProgress,
            3 => TaskStatus.Completed,
            4 => TaskStatus.Failed,
            5 => TaskStatus.Cancelled,
            _ => TaskStatus.Pending
        };
    }

    /// <summary>
    /// Преобразование legacy приоритета задачи в новый enum
    /// </summary>
    /// <param name="legacyPriority">Legacy приоритет (числовое значение)</param>
    /// <returns>Приоритет задачи в новом формате</returns>
    public static TaskPriority MapTaskPriority(int legacyPriority)
    {
        return legacyPriority switch
        {
            0 => TaskPriority.Low,
            1 => TaskPriority.Normal,
            2 => TaskPriority.High,
            3 => TaskPriority.Critical,
            _ => TaskPriority.Normal
        };
    }

    /// <summary>
    /// Преобразование legacy типа репозитория в новый enum
    /// </summary>
    /// <param name="legacyType">Legacy тип репозитория</param>
    /// <returns>Тип репозитория в новом формате</returns>
    public static Data.Entities.RepositoryType MapRepositoryType(string? legacyType)
    {
        return legacyType?.ToLowerInvariant() switch
        {
            "git" => Data.Entities.RepositoryType.Git,
            "svn" => Data.Entities.RepositoryType.Svn,
            "local" => Data.Entities.RepositoryType.Local,
            "remote" => Data.Entities.RepositoryType.Remote,
            "network" => Data.Entities.RepositoryType.Remote,
            "cloud" => Data.Entities.RepositoryType.Remote,
            _ => Data.Entities.RepositoryType.Local
        };
    }
}