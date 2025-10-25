using Orchestra.Core.Models;

namespace Orchestra.Core.Services.Connectors;

/// <summary>
/// Представляет активную сессию подключения к внешнему агенту.
/// </summary>
/// <remarks>
/// Этот класс инкапсулирует всю информацию о сессии агента,
/// включая коннектор, буфер вывода и метаданные.
/// Не является потокобезопасным - управление синхронизацией
/// должно осуществляться на уровне AgentSessionManager.
/// </remarks>
public class AgentSession
{
    /// <summary>
    /// Уникальный идентификатор агента.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Коннектор для взаимодействия с агентом.
    /// </summary>
    public required IAgentConnector Connector { get; init; }

    /// <summary>
    /// Буфер для хранения вывода агента.
    /// </summary>
    public required IAgentOutputBuffer OutputBuffer { get; init; }

    /// <summary>
    /// Время создания сессии (UTC).
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время последней активности в сессии (UTC).
    /// </summary>
    /// <remarks>
    /// Обновляется при каждом обращении к сессии через GetSessionAsync.
    /// Используется для автоматической очистки неактивных сессий.
    /// </remarks>
    public DateTime LastActivityAt { get; set; }

    /// <summary>
    /// Текущий статус подключения к агенту.
    /// </summary>
    /// <remarks>
    /// Это свойство является удобным доступом к статусу коннектора.
    /// </remarks>
    public ConnectionStatus Status => Connector.Status;

    /// <summary>
    /// Дополнительные метаданные сессии (key-value пары).
    /// </summary>
    /// <remarks>
    /// Используется для хранения произвольной информации о сессии,
    /// такой как тип агента, версия, конфигурация и т.д.
    /// </remarks>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Параметры подключения, использованные для создания сессии.
    /// </summary>
    public required AgentConnectionParams ConnectionParams { get; init; }

    /// <summary>
    /// Возвращает строковое представление сессии для отладки.
    /// </summary>
    public override string ToString()
    {
        return $"AgentSession[AgentId={AgentId}, Status={Status}, CreatedAt={CreatedAt:yyyy-MM-dd HH:mm:ss}, LastActivity={LastActivityAt:yyyy-MM-dd HH:mm:ss}]";
    }
}
