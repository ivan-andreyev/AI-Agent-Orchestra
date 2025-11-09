using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Queries.Sessions;

/// <summary>
/// Запрос для получения сессии агента по SessionId
/// </summary>
public record GetAgentSessionQuery(string SessionId) : IQuery<AgentSession?>;
