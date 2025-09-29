using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Abstractions;

/// <summary>
/// Абстракция для хранения состояния агентов.
/// Обеспечивает изоляцию состояния и возможность подмены реализации для тестов.
/// </summary>
public interface IAgentStateStore
{
    /// <summary>
    /// Получить агента по ID
    /// </summary>
    /// <param name="id">Идентификатор агента</param>
    /// <returns>Информация об агенте или null если не найден</returns>
    Task<AgentInfo?> GetAgentAsync(string id);

    /// <summary>
    /// Получить всех агентов
    /// </summary>
    /// <returns>Список всех агентов</returns>
    Task<List<AgentInfo>> GetAllAgentsAsync();

    /// <summary>
    /// Зарегистрировать нового агента
    /// </summary>
    /// <param name="agent">Информация об агенте</param>
    /// <returns>True если агент успешно зарегистрирован</returns>
    Task<bool> RegisterAgentAsync(AgentInfo agent);

    /// <summary>
    /// Обновить информацию об агенте
    /// </summary>
    /// <param name="agent">Обновленная информация об агенте</param>
    /// <returns>True если агент успешно обновлен</returns>
    Task<bool> UpdateAgentAsync(AgentInfo agent);

    /// <summary>
    /// Обновить статус агента
    /// </summary>
    /// <param name="agentId">ID агента</param>
    /// <param name="status">Новый статус</param>
    /// <param name="currentTask">Текущая задача (опционально)</param>
    /// <returns>True если статус успешно обновлен</returns>
    Task<bool> UpdateAgentStatusAsync(string agentId, AgentStatus status, string? currentTask = null);

    /// <summary>
    /// Найти доступных агентов для указанного репозитория
    /// </summary>
    /// <param name="repositoryPath">Путь к репозиторию</param>
    /// <returns>Список доступных агентов</returns>
    Task<List<AgentInfo>> FindAvailableAgentsAsync(string repositoryPath);

    /// <summary>
    /// Найти лучшего агента для указанного репозитория (Claude Code)
    /// </summary>
    /// <param name="repositoryPath">Путь к репозиторию</param>
    /// <returns>Информация об агенте или null если не найден</returns>
    Task<AgentInfo?> FindBestAgentAsync(string repositoryPath);

    /// <summary>
    /// Очистить всех агентов (используется для тестов)
    /// </summary>
    Task ClearAllAgentsAsync();

    /// <summary>
    /// Проверить, является ли агент Claude Code агентом
    /// </summary>
    /// <param name="agentId">ID агента</param>
    /// <returns>True если агент является Claude Code агентом</returns>
    Task<bool> IsClaudeCodeAgentAsync(string agentId);

    /// <summary>
    /// Получить всех Claude Code агентов
    /// </summary>
    /// <returns>Список Claude Code агентов</returns>
    Task<List<AgentInfo>> GetClaudeCodeAgentsAsync();
}