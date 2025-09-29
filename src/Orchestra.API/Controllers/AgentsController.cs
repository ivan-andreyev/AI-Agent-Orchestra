using MediatR;
using Microsoft.AspNetCore.Mvc;
using Orchestra.Core.Commands.Agents;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Queries.Agents;

namespace Orchestra.API.Controllers;

/// <summary>
/// API контроллер для управления агентами через MediatR
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AgentsController> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр AgentsController
    /// </summary>
    /// <param name="mediator">Медиатор для выполнения команд и запросов</param>
    /// <param name="logger">Логгер</param>
    public AgentsController(IMediator mediator, ILogger<AgentsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Получить всех агентов
    /// </summary>
    /// <param name="repositoryPath">Фильтр по пути репозитория</param>
    /// <param name="agentType">Фильтр по типу агента</param>
    /// <param name="status">Фильтр по статусу</param>
    /// <param name="includeRelated">Включать связанные данные</param>
    /// <returns>Список агентов</returns>
    [HttpGet]
    public async Task<ActionResult<List<Agent>>> GetAllAgents(
        [FromQuery] string? repositoryPath = null,
        [FromQuery] string? agentType = null,
        [FromQuery] Orchestra.Core.Data.Entities.AgentStatus? status = null,
        [FromQuery] bool includeRelated = false)
    {
        try
        {
            var query = new GetAllAgentsQuery
            {
                RepositoryPath = repositoryPath,
                AgentType = agentType,
                Status = status,
                IncludeRelated = includeRelated,
                ActiveOnly = true
            };

            var agents = await _mediator.Send(query);
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all agents");
            return StatusCode(500, new { Message = "Failed to retrieve agents", Error = ex.Message });
        }
    }

    /// <summary>
    /// Получить агента по идентификатору
    /// </summary>
    /// <param name="id">Идентификатор агента</param>
    /// <param name="includeRelated">Включать связанные данные</param>
    /// <returns>Агент или 404</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<Agent>> GetAgentById(string id, [FromQuery] bool includeRelated = false)
    {
        try
        {
            var query = new GetAgentByIdQuery(id, includeRelated);
            var agent = await _mediator.Send(query);

            if (agent == null)
            {
                return NotFound(new { Message = $"Agent {id} not found" });
            }

            return Ok(agent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent {AgentId}", id);
            return StatusCode(500, new { Message = "Failed to retrieve agent", Error = ex.Message });
        }
    }

    /// <summary>
    /// Получить агентов по репозиторию
    /// </summary>
    /// <param name="repositoryPath">Путь к репозиторию</param>
    /// <param name="includeRelated">Включать связанные данные</param>
    /// <returns>Список агентов репозитория</returns>
    [HttpGet("by-repository")]
    public async Task<ActionResult<List<Agent>>> GetAgentsByRepository(
        [FromQuery] string repositoryPath,
        [FromQuery] bool includeRelated = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(repositoryPath))
            {
                return BadRequest(new { Message = "Repository path is required" });
            }

            var query = new GetAgentsByRepositoryQuery(repositoryPath, includeRelated);
            var agents = await _mediator.Send(query);

            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agents for repository {RepositoryPath}", repositoryPath);
            return StatusCode(500, new { Message = "Failed to retrieve agents for repository", Error = ex.Message });
        }
    }

    /// <summary>
    /// Зарегистрировать нового агента
    /// </summary>
    /// <param name="request">Данные для регистрации агента</param>
    /// <returns>Результат регистрации</returns>
    [HttpPost("register")]
    public async Task<ActionResult<RegisterAgentResult>> RegisterAgent([FromBody] RegisterAgentRequest request)
    {
        try
        {
            var command = new RegisterAgentCommand
            {
                Id = request.Id,
                Name = request.Name,
                Type = request.Type,
                RepositoryPath = request.RepositoryPath,
                SessionId = request.SessionId,
                MaxConcurrentTasks = request.MaxConcurrentTasks,
                ConfigurationJson = request.ConfigurationJson
            };

            var result = await _mediator.Send(command);

            if (result.Success)
            {
                _logger.LogInformation("Agent {AgentId} registered successfully", request.Id);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Failed to register agent {AgentId}: {Error}", request.Id, result.ErrorMessage);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent {AgentId}", request.Id);
            return StatusCode(500, new { Message = "Agent registration failed", Error = ex.Message });
        }
    }

    /// <summary>
    /// Обновить статус агента
    /// </summary>
    /// <param name="id">Идентификатор агента</param>
    /// <param name="request">Данные для обновления статуса</param>
    /// <returns>Результат обновления</returns>
    [HttpPut("{id}/status")]
    public async Task<ActionResult<UpdateAgentStatusResult>> UpdateAgentStatus(string id, [FromBody] UpdateAgentStatusRequest request)
    {
        try
        {
            var command = new UpdateAgentStatusCommand
            {
                AgentId = id,
                Status = request.Status,
                CurrentTask = request.CurrentTask,
                LastPing = request.LastPing ?? DateTime.UtcNow,
                StatusMessage = request.StatusMessage
            };

            var result = await _mediator.Send(command);

            if (result.Success)
            {
                _logger.LogDebug("Agent {AgentId} status updated to {Status}", id, request.Status);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Failed to update agent {AgentId} status: {Error}", id, result.ErrorMessage);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update agent {AgentId} status", id);
            return StatusCode(500, new { Message = "Status update failed", Error = ex.Message });
        }
    }

    /// <summary>
    /// Ping агента (обновление времени последней активности)
    /// </summary>
    /// <param name="id">Идентификатор агента</param>
    /// <param name="request">Данные ping запроса</param>
    /// <returns>Результат ping</returns>
    [HttpPost("{id}/ping")]
    public async Task<ActionResult> PingAgent(string id, [FromBody] PingAgentRequest request)
    {
        try
        {
            var command = new UpdateAgentStatusCommand
            {
                AgentId = id,
                Status = request.Status,
                CurrentTask = request.CurrentTask,
                LastPing = DateTime.UtcNow,
                StatusMessage = request.StatusMessage
            };

            var result = await _mediator.Send(command);

            if (result.Success)
            {
                return Ok(new { Message = "Agent ping received", AgentId = id, Timestamp = DateTime.UtcNow });
            }
            else
            {
                return BadRequest(new { Message = "Ping failed", Error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ping agent {AgentId}", id);
            return StatusCode(500, new { Message = "Ping failed", Error = ex.Message });
        }
    }

    /// <summary>
    /// Автоматическое обнаружение агентов
    /// </summary>
    /// <param name="request">Параметры обнаружения</param>
    /// <returns>Результат обнаружения</returns>
    [HttpPost("discover")]
    public async Task<ActionResult> DiscoverAgents([FromBody] DiscoverAgentsRequest? request = null)
    {
        try
        {
            // Для автоматического обнаружения используем логику из существующего контроллера
            // но с использованием новых MediatR команд

            var repositoryPath = request?.RepositoryPath ?? Environment.CurrentDirectory;

            // Получаем существующих агентов для репозитория
            var query = new GetAgentsByRepositoryQuery(repositoryPath);
            var existingAgents = await _mediator.Send(query);

            if (!existingAgents.Any())
            {
                // Создаем агента автоматически
                var agentId = $"claude-discovery-{Guid.NewGuid().ToString()[..8]}";
                var registerCommand = new RegisterAgentCommand
                {
                    Id = agentId,
                    Name = "Claude Code Agent (Auto-discovered)",
                    Type = "claude-code",
                    RepositoryPath = repositoryPath,
                    MaxConcurrentTasks = 1
                };

                var result = await _mediator.Send(registerCommand);

                if (result.Success)
                {
                    return Ok(new
                    {
                        Message = "New agent discovered and registered",
                        AgentsFound = 1,
                        AgentId = agentId,
                        RepositoryPath = repositoryPath
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Message = "No agents found and failed to create new agent",
                        AgentsFound = 0,
                        Error = result.ErrorMessage,
                        RepositoryPath = repositoryPath
                    });
                }
            }

            return Ok(new
            {
                Message = "Existing agents discovered",
                AgentsFound = existingAgents.Count,
                Agents = existingAgents.Select(a => new { a.Id, a.Name, a.Status, a.RepositoryPath }).ToList(),
                RepositoryPath = repositoryPath
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent discovery failed");
            return StatusCode(500, new
            {
                Message = "Agent discovery failed",
                Error = ex.Message
            });
        }
    }
}

#region Request/Response Models

/// <summary>
/// Запрос на регистрацию агента
/// </summary>
public record RegisterAgentRequest(
    string Id,
    string Name,
    string Type,
    string RepositoryPath,
    string? SessionId = null,
    int MaxConcurrentTasks = 1,
    string? ConfigurationJson = null);

/// <summary>
/// Запрос на обновление статуса агента
/// </summary>
public record UpdateAgentStatusRequest(
    AgentStatus Status,
    string? CurrentTask = null,
    DateTime? LastPing = null,
    string? StatusMessage = null);

/// <summary>
/// Запрос ping агента
/// </summary>
public record PingAgentRequest(
    AgentStatus Status,
    string? CurrentTask = null,
    string? StatusMessage = null);

/// <summary>
/// Запрос на обнаружение агентов
/// </summary>
public record DiscoverAgentsRequest(string? RepositoryPath = null);

#endregion