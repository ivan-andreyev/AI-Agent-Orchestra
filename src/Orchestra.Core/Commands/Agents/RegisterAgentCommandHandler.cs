using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Events.Agents;

namespace Orchestra.Core.Commands.Agents;

/// <summary>
/// Обработчик команды регистрации агента
/// </summary>
public class RegisterAgentCommandHandler : IRequestHandler<RegisterAgentCommand, RegisterAgentResult>
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<RegisterAgentCommandHandler> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Инициализирует новый экземпляр RegisterAgentCommandHandler
    /// </summary>
    /// <param name="context">Контекст базы данных</param>
    /// <param name="logger">Логгер</param>
    /// <param name="mediator">Медиатор для публикации событий</param>
    public RegisterAgentCommandHandler(
        OrchestraDbContext context,
        ILogger<RegisterAgentCommandHandler> logger,
        IMediator mediator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <inheritdoc />
    public async Task<RegisterAgentResult> Handle(RegisterAgentCommand request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // Валидация входных данных
        var validationResult = ValidateRequest(request);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        _logger.LogInformation("Registering agent {AgentId} of type {Type} for repository {RepositoryPath}",
            request.Id, request.Type, request.RepositoryPath);

        try
        {
            // Проверяем существует ли агент
            var existingAgent = await _context.Agents
                .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

            var isUpdate = existingAgent != null;
            var agent = existingAgent ?? new Agent { Id = request.Id };

            // Обновляем свойства агента
            agent.Name = request.Name;
            agent.Type = request.Type;
            agent.RepositoryPath = request.RepositoryPath;
            agent.SessionId = request.SessionId;
            agent.MaxConcurrentTasks = request.MaxConcurrentTasks;
            agent.ConfigurationJson = request.ConfigurationJson;
            agent.Status = AgentStatus.Idle;
            agent.LastPing = DateTime.UtcNow;

            // Обновляем связь с репозиторием
            await UpdateRepositoryRelation(agent, cancellationToken);

            if (!isUpdate)
            {
                _context.Agents.Add(agent);
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Agent {AgentId} {Action} successfully",
                request.Id, isUpdate ? "updated" : "registered");

            // Публикуем событие
            if (isUpdate)
            {
                await _mediator.Publish(new AgentUpdatedEvent(agent), cancellationToken);
            }
            else
            {
                await _mediator.Publish(new AgentRegisteredEvent(agent), cancellationToken);
            }

            return new RegisterAgentResult
            {
                Success = true,
                Agent = agent,
                WasUpdated = isUpdate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent {AgentId}: {Error}",
                request.Id, ex.Message);

            return new RegisterAgentResult
            {
                Success = false,
                ErrorMessage = $"Registration failed: {ex.Message}"
            };
        }
    }

    private RegisterAgentResult ValidateRequest(RegisterAgentCommand request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            errors.Add("Agent ID is required");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Agent Name is required");
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            errors.Add("Agent Type is required");
        }

        if (string.IsNullOrWhiteSpace(request.RepositoryPath))
        {
            errors.Add("Repository Path is required");
        }
        else if (!Directory.Exists(request.RepositoryPath))
        {
            errors.Add($"Repository directory does not exist: {request.RepositoryPath}");
        }

        if (request.MaxConcurrentTasks <= 0)
        {
            errors.Add("MaxConcurrentTasks must be greater than 0");
        }

        if (errors.Any())
        {
            return new RegisterAgentResult
            {
                Success = false,
                ErrorMessage = string.Join("; ", errors)
            };
        }

        return new RegisterAgentResult { Success = true };
    }

    private async Task UpdateRepositoryRelation(Agent agent, CancellationToken cancellationToken)
    {
        try
        {
            // Пытаемся найти существующий репозиторий по пути
            var repository = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Path == agent.RepositoryPath, cancellationToken);

            if (repository != null)
            {
                agent.RepositoryId = repository.Id;
                _logger.LogDebug("Agent {AgentId} linked to existing repository {RepositoryId}",
                    agent.Id, repository.Id);
            }
            else
            {
                // Создаем новый репозиторий
                var repositoryName = Path.GetFileName(agent.RepositoryPath.TrimEnd('\\', '/'));
                if (string.IsNullOrEmpty(repositoryName))
                {
                    repositoryName = agent.RepositoryPath;
                }

                var newRepository = new Repository
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = repositoryName,
                    Path = agent.RepositoryPath,
                    Type = Orchestra.Core.Data.Entities.RepositoryType.Git, // Предполагаем Git по умолчанию
                    IsActive = true,
                    TotalExecutionTime = TimeSpan.Zero
                };

                _context.Repositories.Add(newRepository);
                agent.RepositoryId = newRepository.Id;

                _logger.LogInformation("Created new repository {RepositoryId} for path {Path}",
                    newRepository.Id, agent.RepositoryPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update repository relation for agent {AgentId}",
                agent.Id);
            // Продолжаем без связи с репозиторием
            agent.RepositoryId = null;
        }
    }
}