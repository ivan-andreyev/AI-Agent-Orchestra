using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Queries.Sessions;

/// <summary>
/// Обработчик запроса получения сессии агента
/// </summary>
public class GetAgentSessionQueryHandler : IRequestHandler<GetAgentSessionQuery, AgentSession?>
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<GetAgentSessionQueryHandler> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр GetAgentSessionQueryHandler
    /// </summary>
    /// <param name="context">Контекст базы данных</param>
    /// <param name="logger">Логгер</param>
    public GetAgentSessionQueryHandler(
        OrchestraDbContext context,
        ILogger<GetAgentSessionQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AgentSession?> Handle(GetAgentSessionQuery request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            _logger.LogWarning("GetAgentSessionQuery called with empty SessionId");
            return null;
        }

        _logger.LogDebug("Retrieving session {SessionId}", request.SessionId);

        try
        {
            var session = await _context.AgentSessions
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

            if (session == null)
            {
                _logger.LogDebug("Session {SessionId} not found", request.SessionId);
            }
            else
            {
                _logger.LogDebug("Session {SessionId} found. Status: {Status}, Agent: {AgentId}",
                    request.SessionId, session.Status, session.AgentId);
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve session {SessionId}: {Error}",
                request.SessionId, ex.Message);
            throw;
        }
    }
}
