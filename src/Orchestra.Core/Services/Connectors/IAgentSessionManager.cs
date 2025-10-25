using Orchestra.Core.Models;

namespace Orchestra.Core.Services.Connectors;

/// <summary>
/// Управляет жизненным циклом сессий подключения к внешним агентам.
/// </summary>
/// <remarks>
/// Этот интерфейс определяет контракт для управления сессиями агентов,
/// включая создание, получение, отключение и мониторинг активных сессий.
/// Все операции являются потокобезопасными.
/// </remarks>
public interface IAgentSessionManager
{
    /// <summary>
    /// Создает новую сессию агента и подключается к нему.
    /// </summary>
    /// <param name="agentId">Уникальный идентификатор агента.</param>
    /// <param name="connectionParams">Параметры подключения к агенту.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданная сессия агента.</returns>
    /// <exception cref="ArgumentNullException">Если agentId или connectionParams равны null.</exception>
    /// <exception cref="InvalidOperationException">Если сессия для данного агента уже существует.</exception>
    /// <exception cref="ConnectionException">Если не удалось подключиться к агенту.</exception>
    Task<AgentSession> CreateSessionAsync(
        string agentId,
        AgentConnectionParams connectionParams,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает существующую сессию агента по идентификатору.
    /// </summary>
    /// <param name="agentId">Уникальный идентификатор агента.</param>
    /// <returns>Сессия агента, если найдена; иначе null.</returns>
    /// <remarks>
    /// Этот метод обновляет время последней активности сессии (LastActivityAt).
    /// </remarks>
    AgentSession? GetSessionAsync(string agentId);

    /// <summary>
    /// Отключает и удаляет сессию агента.
    /// </summary>
    /// <param name="agentId">Уникальный идентификатор агента.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>true, если сессия была успешно отключена; false, если сессия не найдена.</returns>
    Task<bool> DisconnectSessionAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает все активные сессии агентов.
    /// </summary>
    /// <returns>Коллекция всех активных сессий (read-only).</returns>
    Task<IReadOnlyCollection<AgentSession>> GetAllSessionsAsync();

    /// <summary>
    /// Событие, возникающее при создании новой сессии агента.
    /// </summary>
    event EventHandler<SessionCreatedEventArgs>? SessionCreated;

    /// <summary>
    /// Событие, возникающее при отключении сессии агента.
    /// </summary>
    event EventHandler<SessionDisconnectedEventArgs>? SessionDisconnected;

    /// <summary>
    /// Событие, возникающее при ошибке в сессии агента.
    /// </summary>
    event EventHandler<SessionErrorEventArgs>? SessionError;
}

/// <summary>
/// Аргументы события создания сессии.
/// </summary>
public class SessionCreatedEventArgs : EventArgs
{
    /// <summary>
    /// Уникальный идентификатор агента.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Время создания сессии (UTC).
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Статус подключения к агенту.
    /// </summary>
    public required ConnectionStatus Status { get; init; }
}

/// <summary>
/// Аргументы события отключения сессии.
/// </summary>
public class SessionDisconnectedEventArgs : EventArgs
{
    /// <summary>
    /// Уникальный идентификатор агента.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Время отключения сессии (UTC).
    /// </summary>
    public required DateTime DisconnectedAt { get; init; }

    /// <summary>
    /// Причина отключения (опционально).
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Аргументы события ошибки сессии.
/// </summary>
public class SessionErrorEventArgs : EventArgs
{
    /// <summary>
    /// Уникальный идентификатор агента.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Время возникновения ошибки (UTC).
    /// </summary>
    public required DateTime ErrorAt { get; init; }

    /// <summary>
    /// Исключение, вызвавшее ошибку.
    /// </summary>
    public required Exception Error { get; init; }

    /// <summary>
    /// Контекст ошибки (операция, в которой произошла ошибка).
    /// </summary>
    public string? Context { get; init; }
}
