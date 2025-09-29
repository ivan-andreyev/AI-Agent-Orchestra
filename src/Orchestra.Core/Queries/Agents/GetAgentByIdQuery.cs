using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Queries.Agents;

/// <summary>
/// Запрос для получения агента по идентификатору
/// </summary>
public class GetAgentByIdQuery : IQuery<Agent?>
{
    /// <summary>
    /// Идентификатор агента
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Включать ли связанные данные
    /// </summary>
    public bool IncludeRelated { get; set; } = false;

    /// <summary>
    /// Инициализирует новый запрос получения агента
    /// </summary>
    /// <param name="agentId">Идентификатор агента</param>
    /// <param name="includeRelated">Включать ли связанные данные</param>
    public GetAgentByIdQuery(string agentId, bool includeRelated = false)
    {
        AgentId = agentId ?? throw new ArgumentNullException(nameof(agentId));
        IncludeRelated = includeRelated;
    }
}

/// <summary>
/// Запрос для получения всех агентов
/// </summary>
public class GetAllAgentsQuery : IQuery<List<Agent>>
{
    /// <summary>
    /// Фильтр по репозиторию
    /// </summary>
    public string? RepositoryPath { get; set; }

    /// <summary>
    /// Фильтр по типу агента
    /// </summary>
    public string? AgentType { get; set; }

    /// <summary>
    /// Фильтр по статусу
    /// </summary>
    public Orchestra.Core.Data.Entities.AgentStatus? Status { get; set; }

    /// <summary>
    /// Включать ли только активные агенты
    /// </summary>
    public bool ActiveOnly { get; set; } = true;

    /// <summary>
    /// Включать ли связанные данные
    /// </summary>
    public bool IncludeRelated { get; set; } = false;
}

/// <summary>
/// Запрос для получения агентов по репозиторию
/// </summary>
public class GetAgentsByRepositoryQuery : IQuery<List<Agent>>
{
    /// <summary>
    /// Путь к репозиторию
    /// </summary>
    public string RepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Включать ли связанные данные
    /// </summary>
    public bool IncludeRelated { get; set; } = false;

    /// <summary>
    /// Инициализирует новый запрос получения агентов репозитория
    /// </summary>
    /// <param name="repositoryPath">Путь к репозиторию</param>
    /// <param name="includeRelated">Включать ли связанные данные</param>
    public GetAgentsByRepositoryQuery(string repositoryPath, bool includeRelated = false)
    {
        RepositoryPath = repositoryPath ?? throw new ArgumentNullException(nameof(repositoryPath));
        IncludeRelated = includeRelated;
    }
}