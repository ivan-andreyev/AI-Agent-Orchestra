using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Commands.Agents;

/// <summary>
/// Команда для регистрации нового агента в системе
/// </summary>
public class RegisterAgentCommand : ICommand<RegisterAgentResult>
{
    /// <summary>
    /// Уникальный идентификатор агента
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Название агента
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Тип агента
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Путь к репозиторию
    /// </summary>
    public string RepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор сессии агента
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Максимальное количество одновременных задач
    /// </summary>
    public int MaxConcurrentTasks { get; set; } = 1;

    /// <summary>
    /// JSON конфигурация агента
    /// </summary>
    public string? ConfigurationJson { get; set; }
}

/// <summary>
/// Результат регистрации агента
/// </summary>
public class RegisterAgentResult
{
    /// <summary>
    /// Успешность регистрации
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Зарегистрированный агент
    /// </summary>
    public Agent? Agent { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Был ли агент обновлен (true) или создан новый (false)
    /// </summary>
    public bool WasUpdated { get; set; }
}