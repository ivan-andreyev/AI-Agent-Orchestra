using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Commands.Agents;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Queries.Agents;

namespace Orchestra.Core.Services;

/// <summary>
/// Сервис для мониторинга состояния агентов
/// </summary>
public class AgentHealthCheckService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentHealthCheckService> _logger;
    private readonly AgentHealthCheckOptions _options;

    /// <summary>
    /// Инициализирует новый экземпляр AgentHealthCheckService
    /// </summary>
    /// <param name="scopeFactory">Фабрика для создания scope</param>
    /// <param name="logger">Логгер</param>
    /// <param name="options">Настройки health check'а</param>
    public AgentHealthCheckService(
        IServiceScopeFactory scopeFactory,
        ILogger<AgentHealthCheckService> logger,
        IOptions<AgentHealthCheckOptions> options)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent Health Check Service started with interval {Interval}",
            _options.CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthCheck(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during agent health check");
            }

            await Task.Delay(_options.CheckInterval, stoppingToken);
        }

        _logger.LogInformation("Agent Health Check Service stopped");
    }

    private async Task PerformHealthCheck(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        _logger.LogDebug("Starting agent health check cycle");

        try
        {
            // Получаем всех активных агентов
            var getAllAgentsQuery = new GetAllAgentsQuery
            {
                ActiveOnly = true,
                IncludeRelated = false
            };

            var agents = await mediator.Send(getAllAgentsQuery, cancellationToken);

            if (!agents.Any())
            {
                _logger.LogDebug("No agents found for health check");
                return;
            }

            _logger.LogDebug("Checking health of {Count} agents", agents.Count);

            var healthCheckTasks = agents.Select(agent => CheckAgentHealth(mediator, agent, cancellationToken));
            await Task.WhenAll(healthCheckTasks);

            _logger.LogDebug("Agent health check cycle completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform agent health check");
        }
    }

    private async Task CheckAgentHealth(IMediator mediator, Agent agent, CancellationToken cancellationToken)
    {
        try
        {
            var timeSinceLastPing = DateTime.UtcNow - agent.LastPing;
            var isHealthy = timeSinceLastPing <= _options.AgentTimeout;

            var expectedStatus = DetermineExpectedStatus(agent, timeSinceLastPing);

            if (agent.Status != expectedStatus)
            {
                _logger.LogInformation("Agent {AgentId} status change: {OldStatus} -> {NewStatus} (last ping: {LastPing})",
                    agent.Id, agent.Status, expectedStatus, agent.LastPing);

                var updateCommand = new UpdateAgentStatusCommand
                {
                    AgentId = agent.Id,
                    Status = expectedStatus,
                    StatusMessage = isHealthy ? null : $"No ping for {timeSinceLastPing:c}"
                };

                var result = await mediator.Send(updateCommand, cancellationToken);

                if (!result.Success)
                {
                    _logger.LogWarning("Failed to update agent {AgentId} status: {Error}",
                        agent.Id, result.ErrorMessage);
                }
            }
            else if (!isHealthy && _options.EnableVerboseLogging)
            {
                _logger.LogDebug("Agent {AgentId} remains unhealthy - last ping: {LastPing} ({TimeSince} ago)",
                    agent.Id, agent.LastPing, timeSinceLastPing);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check health of agent {AgentId}", agent.Id);
        }
    }

    private AgentStatus DetermineExpectedStatus(Agent agent, TimeSpan timeSinceLastPing)
    {
        // Если агент не отвечает больше таймаута, помечаем как Offline
        if (timeSinceLastPing > _options.AgentTimeout)
        {
            return AgentStatus.Offline;
        }

        // Если агент недавно отвечал и был в статусе Offline, возвращаем в Idle
        if (agent.Status == AgentStatus.Offline && timeSinceLastPing <= _options.RecoveryTimeout)
        {
            return AgentStatus.Idle;
        }

        // Если агент в статусе Error и уже прошло время восстановления, переводим в Idle
        if (agent.Status == AgentStatus.Error && timeSinceLastPing <= _options.RecoveryTimeout)
        {
            return AgentStatus.Idle;
        }

        // В остальных случаях оставляем текущий статус
        return agent.Status;
    }
}