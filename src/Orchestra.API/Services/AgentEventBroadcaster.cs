using Microsoft.AspNetCore.SignalR;
using Orchestra.API.Hubs;
using Orchestra.Core.Services.Connectors;

namespace Orchestra.API.Services;

/// <summary>
/// Фоновый сервис для трансляции событий AgentSessionManager в SignalR Hub.
/// </summary>
/// <remarks>
/// <para>
/// AgentEventBroadcaster подписывается на события IAgentSessionManager
/// и транслирует их всем подключенным SignalR клиентам через IHubContext.
/// </para>
/// <para>
/// Запускается как Hosted Service при старте приложения и работает в фоновом режиме.
/// </para>
/// </remarks>
public class AgentEventBroadcaster : IHostedService, IDisposable
{
    private readonly IAgentSessionManager _sessionManager;
    private readonly IHubContext<AgentInteractionHub> _hubContext;
    private readonly ILogger<AgentEventBroadcaster> _logger;
    private bool _disposed;

    /// <summary>
    /// Инициализирует новый экземпляр AgentEventBroadcaster.
    /// </summary>
    /// <param name="sessionManager">Менеджер сессий агентов для подписки на события</param>
    /// <param name="hubContext">Контекст SignalR hub для трансляции событий</param>
    /// <param name="logger">Логгер для диагностики</param>
    public AgentEventBroadcaster(
        IAgentSessionManager sessionManager,
        IHubContext<AgentInteractionHub> hubContext,
        ILogger<AgentEventBroadcaster> logger)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AgentEventBroadcaster starting - subscribing to session manager events");

        // Подписываемся на события сессий
        _sessionManager.SessionCreated += OnSessionCreated;
        _sessionManager.SessionDisconnected += OnSessionDisconnected;
        _sessionManager.SessionError += OnSessionError;

        _logger.LogInformation("AgentEventBroadcaster started successfully");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AgentEventBroadcaster stopping - unsubscribing from session manager events");

        // Отписываемся от событий
        _sessionManager.SessionCreated -= OnSessionCreated;
        _sessionManager.SessionDisconnected -= OnSessionDisconnected;
        _sessionManager.SessionError -= OnSessionError;

        _logger.LogInformation("AgentEventBroadcaster stopped successfully");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Обрабатывает событие создания сессии.
    /// </summary>
    private async void OnSessionCreated(object? sender, SessionCreatedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Broadcasting SessionCreated event for agent {AgentId}", e.AgentId);

            var groupName = $"agent_session_{e.AgentId}";

            // Транслируем событие в SignalR группу
            await _hubContext.Clients.Group(groupName).SendAsync("SessionCreated", new
            {
                e.AgentId,
                e.CreatedAt,
                Status = e.Status.ToString()
            });

            _logger.LogDebug("SessionCreated event broadcast successfully for agent {AgentId}", e.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast SessionCreated event for agent {AgentId}", e.AgentId);
        }
    }

    /// <summary>
    /// Обрабатывает событие отключения сессии.
    /// </summary>
    private async void OnSessionDisconnected(object? sender, SessionDisconnectedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Broadcasting SessionDisconnected event for agent {AgentId}", e.AgentId);

            var groupName = $"agent_session_{e.AgentId}";

            // Транслируем событие в SignalR группу
            await _hubContext.Clients.Group(groupName).SendAsync("SessionDisconnected", new
            {
                e.AgentId,
                e.DisconnectedAt,
                e.Reason
            });

            _logger.LogDebug("SessionDisconnected event broadcast successfully for agent {AgentId}", e.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast SessionDisconnected event for agent {AgentId}", e.AgentId);
        }
    }

    /// <summary>
    /// Обрабатывает событие ошибки сессии.
    /// </summary>
    private async void OnSessionError(object? sender, SessionErrorEventArgs e)
    {
        try
        {
            _logger.LogWarning("Broadcasting SessionError event for agent {AgentId}: {ErrorMessage}",
                e.AgentId, e.Error.Message);

            var groupName = $"agent_session_{e.AgentId}";

            // Транслируем событие в SignalR группу
            await _hubContext.Clients.Group(groupName).SendAsync("SessionError", new
            {
                e.AgentId,
                e.ErrorAt,
                ErrorMessage = e.Error.Message,
                e.Context
            });

            _logger.LogDebug("SessionError event broadcast successfully for agent {AgentId}", e.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast SessionError event for agent {AgentId}", e.AgentId);
        }
    }

    /// <summary>
    /// Освобождает ресурсы AgentEventBroadcaster.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Disposing AgentEventBroadcaster");

        // Отписываемся от событий
        _sessionManager.SessionCreated -= OnSessionCreated;
        _sessionManager.SessionDisconnected -= OnSessionDisconnected;
        _sessionManager.SessionError -= OnSessionError;

        _disposed = true;
    }
}
