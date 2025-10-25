using Microsoft.AspNetCore.SignalR;
using Orchestra.Core.Services.Connectors;
using Orchestra.API.Hubs.Models;
using MediatR;

namespace Orchestra.API.Hubs;

/// <summary>
/// SignalR hub для взаимодействия с внешними агентами в реальном времени.
/// Обеспечивает управление сеансами, отправку команд и потоковую передачу вывода.
/// </summary>
public class AgentInteractionHub : Hub
{
    private readonly IAgentSessionManager _sessionManager;
    private readonly ILogger<AgentInteractionHub> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Инициализирует новый экземпляр AgentInteractionHub с необходимыми зависимостями.
    /// </summary>
    /// <param name="sessionManager">Менеджер сеансов агентов для управления подключениями</param>
    /// <param name="logger">Логгер для диагностики и мониторинга</param>
    /// <param name="mediator">Медиатор для обработки команд и событий</param>
    public AgentInteractionHub(
        IAgentSessionManager sessionManager,
        ILogger<AgentInteractionHub> logger,
        IMediator mediator)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Обрабатывает подключение клиента к hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to AgentInteractionHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Обрабатывает отключение клиента от hub.
    /// </summary>
    /// <param name="exception">Исключение, вызвавшее отключение (если есть)</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from AgentInteractionHub: {ConnectionId}. Reason: {Exception}",
            Context.ConnectionId, exception?.Message ?? "Normal disconnect");

        await base.OnDisconnectedAsync(exception);
    }
}
