using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Queries.Agents;

/// <summary>
/// Обработчик запроса получения агента по идентификатору
/// </summary>
public class GetAgentByIdQueryHandler : IRequestHandler<GetAgentByIdQuery, Agent?>
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<GetAgentByIdQueryHandler> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр GetAgentByIdQueryHandler
    /// </summary>
    /// <param name="context">Контекст базы данных</param>
    /// <param name="logger">Логгер</param>
    public GetAgentByIdQueryHandler(
        OrchestraDbContext context,
        ILogger<GetAgentByIdQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Agent?> Handle(GetAgentByIdQuery request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.AgentId))
        {
            return null;
        }

        _logger.LogDebug("Getting agent by ID: {AgentId}", request.AgentId);

        try
        {
            var query = _context.Agents.AsQueryable();

            if (request.IncludeRelated)
            {
                query = query
                    .Include(a => a.Repository)
                    .Include(a => a.AssignedTasks)
                    .Include(a => a.PerformanceMetrics);
            }

            var agent = await query
                .FirstOrDefaultAsync(a => a.Id == request.AgentId, cancellationToken);

            if (agent == null)
            {
                _logger.LogDebug("Agent {AgentId} not found", request.AgentId);
            }
            else
            {
                _logger.LogDebug("Agent {AgentId} found: {Name} ({Type})",
                    request.AgentId, agent.Name, agent.Type);
            }

            return agent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent {AgentId}: {Error}",
                request.AgentId, ex.Message);
            throw;
        }
    }
}

/// <summary>
/// Обработчик запроса получения всех агентов
/// </summary>
public class GetAllAgentsQueryHandler : IRequestHandler<GetAllAgentsQuery, List<Agent>>
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<GetAllAgentsQueryHandler> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр GetAllAgentsQueryHandler
    /// </summary>
    /// <param name="context">Контекст базы данных</param>
    /// <param name="logger">Логгер</param>
    public GetAllAgentsQueryHandler(
        OrchestraDbContext context,
        ILogger<GetAllAgentsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<List<Agent>> Handle(GetAllAgentsQuery request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        _logger.LogDebug("Getting all agents with filters: Repository={Repository}, Type={Type}, Status={Status}",
            request.RepositoryPath, request.AgentType, request.Status);

        try
        {
            var query = _context.Agents.AsQueryable();

            // Применяем фильтры
            if (!string.IsNullOrWhiteSpace(request.RepositoryPath))
            {
                query = query.Where(a => a.RepositoryPath == request.RepositoryPath);
            }

            if (!string.IsNullOrWhiteSpace(request.AgentType))
            {
                query = query.Where(a => a.Type == request.AgentType);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(a => a.Status == request.Status.Value);
            }

            if (request.ActiveOnly)
            {
                query = query.Where(a => !a.IsDeleted);
            }

            if (request.IncludeRelated)
            {
                query = query
                    .Include(a => a.Repository)
                    .Include(a => a.AssignedTasks)
                    .Include(a => a.PerformanceMetrics);
            }

            var agents = await query
                .OrderBy(a => a.Name)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Found {Count} agents", agents.Count);

            return agents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all agents: {Error}", ex.Message);
            throw;
        }
    }
}

/// <summary>
/// Обработчик запроса получения агентов по репозиторию
/// </summary>
public class GetAgentsByRepositoryQueryHandler : IRequestHandler<GetAgentsByRepositoryQuery, List<Agent>>
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<GetAgentsByRepositoryQueryHandler> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр GetAgentsByRepositoryQueryHandler
    /// </summary>
    /// <param name="context">Контекст базы данных</param>
    /// <param name="logger">Логгер</param>
    public GetAgentsByRepositoryQueryHandler(
        OrchestraDbContext context,
        ILogger<GetAgentsByRepositoryQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<List<Agent>> Handle(GetAgentsByRepositoryQuery request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.RepositoryPath))
        {
            return new List<Agent>();
        }

        _logger.LogDebug("Getting agents for repository: {RepositoryPath}", request.RepositoryPath);

        try
        {
            var query = _context.Agents
                .Where(a => a.RepositoryPath == request.RepositoryPath && !a.IsDeleted);

            if (request.IncludeRelated)
            {
                query = query
                    .Include(a => a.Repository)
                    .Include(a => a.AssignedTasks)
                    .Include(a => a.PerformanceMetrics);
            }

            var agents = await query
                .OrderBy(a => a.Name)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Found {Count} agents for repository {RepositoryPath}",
                agents.Count, request.RepositoryPath);

            return agents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agents for repository {RepositoryPath}: {Error}",
                request.RepositoryPath, ex.Message);
            throw;
        }
    }
}