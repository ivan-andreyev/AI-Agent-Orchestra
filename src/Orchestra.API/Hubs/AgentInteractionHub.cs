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

        // Cleanup active sessions for this connection
        if (Context.Items.TryGetValue("AgentId", out var agentIdObj) && agentIdObj is string agentId)
        {
            try
            {
                _logger.LogInformation("Cleaning up session for agent {AgentId} on connection {ConnectionId}",
                    agentId, Context.ConnectionId);

                await _sessionManager.DisconnectSessionAsync(agentId, Context.ConnectionAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup session for agent {AgentId} on disconnect", agentId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Подключается к указанному агенту и создает новую сессию.
    /// </summary>
    /// <param name="request">Запрос на подключение с параметрами агента и коннектора</param>
    /// <returns>Ответ с идентификатором сессии или информацией об ошибке</returns>
    /// <remarks>
    /// NOTE: Uses 2-parameter CreateSessionAsync (agentId, connectionParams).
    /// Creates SignalR group "agent_session_{agentId}" for broadcasting.
    /// Stores agentId in Context.Items for cleanup on disconnect.
    /// </remarks>
    public async Task<ConnectToAgentResponse> ConnectToAgent(ConnectToAgentRequest request)
    {
        try
        {
            _logger.LogInformation("Connecting to agent {AgentId} via {ConnectorType} from connection {ConnectionId}",
                request.AgentId, request.ConnectorType, Context.ConnectionId);

            // Validate request
            if (string.IsNullOrWhiteSpace(request.AgentId))
            {
                return new ConnectToAgentResponse(
                    string.Empty,
                    false,
                    "AgentId is required");
            }

            if (string.IsNullOrWhiteSpace(request.ConnectorType))
            {
                return new ConnectToAgentResponse(
                    string.Empty,
                    false,
                    "ConnectorType is required");
            }

            // Create connection parameters from request
            var connectionParams = new Orchestra.Core.Models.AgentConnectionParams
            {
                ConnectorType = request.ConnectorType,
                Metadata = request.ConnectionParams ?? new Dictionary<string, string>(),
                ProcessId = ParseIntParameter(request.ConnectionParams, "ProcessId"),
                PipeName = GetStringParameter(request.ConnectionParams, "PipeName"),
                SocketPath = GetStringParameter(request.ConnectionParams, "SocketPath"),
                ApiEndpoint = GetStringParameter(request.ConnectionParams, "ApiEndpoint"),
                AuthToken = GetStringParameter(request.ConnectionParams, "AuthToken"),
                WorkingDirectory = GetStringParameter(request.ConnectionParams, "WorkingDirectory"),
                ConnectionTimeoutSeconds = ParseIntParameter(request.ConnectionParams, "ConnectionTimeoutSeconds") ?? 30
            };

            // Create session via manager (NOTE: uses 2 params: agentId, connectionParams)
            var session = await _sessionManager.CreateSessionAsync(
                request.AgentId,
                connectionParams,
                Context.ConnectionAborted);

            // Add connection to SignalR group for broadcasting
            var groupName = $"agent_session_{request.AgentId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            _logger.LogInformation("Added connection {ConnectionId} to group {GroupName}",
                Context.ConnectionId, groupName);

            // Store session info in Context.Items for cleanup on disconnect
            Context.Items["AgentId"] = request.AgentId;

            _logger.LogInformation("Successfully connected to agent {AgentId}, session created at {CreatedAt}",
                request.AgentId, session.CreatedAt);

            return new ConnectToAgentResponse(
                request.AgentId, // Using AgentId as SessionId per IAgentSessionManager API
                true,
                null);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "Invalid parameters for connecting to agent {AgentId}", request.AgentId);
            return new ConnectToAgentResponse(
                string.Empty,
                false,
                $"Invalid parameters: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Session already exists for agent {AgentId}", request.AgentId);
            return new ConnectToAgentResponse(
                string.Empty,
                false,
                $"Session already exists: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to agent {AgentId}", request.AgentId);
            return new ConnectToAgentResponse(
                string.Empty,
                false,
                $"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Отключается от агента и закрывает сессию.
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии для отключения (AgentId)</param>
    /// <returns>true если сессия успешно закрыта, false если сессия не найдена</returns>
    /// <remarks>
    /// NOTE: sessionId is the AgentId per IAgentSessionManager API.
    /// Removes connection from SignalR group and notifies other group members.
    /// </remarks>
    public async Task<bool> DisconnectFromAgent(string sessionId)
    {
        try
        {
            _logger.LogInformation("Disconnecting from session {SessionId} on connection {ConnectionId}",
                sessionId, Context.ConnectionId);

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                _logger.LogWarning("Cannot disconnect: SessionId is empty");
                return false;
            }

            // Close session via manager (sessionId is AgentId)
            var closed = await _sessionManager.DisconnectSessionAsync(sessionId, Context.ConnectionAborted);

            if (!closed)
            {
                _logger.LogWarning("Session {SessionId} not found or already closed", sessionId);
                return false;
            }

            // Remove from SignalR group
            var groupName = $"agent_session_{sessionId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            _logger.LogInformation("Removed connection {ConnectionId} from group {GroupName}",
                Context.ConnectionId, groupName);

            // Notify group members about session closure
            await Clients.Group(groupName).SendAsync("SessionClosed", sessionId);

            _logger.LogInformation("Notified group {GroupName} about session closure", groupName);

            // Clear from Context.Items
            Context.Items.Remove("AgentId");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect from session {SessionId}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Отправляет команду указанному агенту через активную сессию.
    /// </summary>
    /// <param name="request">Запрос на отправку команды с идентификатором сессии и командой</param>
    /// <returns>true если команда успешно отправлена, false если сессия не найдена или команда не отправлена</returns>
    /// <remarks>
    /// NOTE: sessionId is the AgentId per IAgentSessionManager API.
    /// Sends command via IAgentConnector and broadcasts notification to SignalR group.
    /// </remarks>
    public async Task<bool> SendCommand(SendCommandRequest request)
    {
        try
        {
            _logger.LogInformation("Sending command to session {SessionId} from connection {ConnectionId}",
                request.SessionId, Context.ConnectionId);

            // Validate request
            if (string.IsNullOrWhiteSpace(request.SessionId))
            {
                _logger.LogWarning("Cannot send command: SessionId is empty");
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Command))
            {
                _logger.LogWarning("Cannot send command: Command is empty");
                return false;
            }

            // Get session from manager (sessionId is AgentId)
            var session = _sessionManager.GetSessionAsync(request.SessionId);

            if (session == null)
            {
                _logger.LogWarning("Session {SessionId} not found", request.SessionId);
                return false;
            }

            // Send command via connector
            var result = await session.Connector.SendCommandAsync(request.Command, Context.ConnectionAborted);

            if (!result.Success)
            {
                _logger.LogError("Failed to send command to session {SessionId}: {ErrorMessage}",
                    request.SessionId, result.ErrorMessage);
                return false;
            }

            _logger.LogInformation("Command sent successfully to session {SessionId}", request.SessionId);

            // Broadcast notification to group members
            var groupName = $"agent_session_{request.SessionId}";
            var notification = new CommandSentNotification(
                request.SessionId,
                request.Command,
                true,
                DateTime.UtcNow);

            await Clients.Group(groupName).SendAsync("CommandSent", notification, Context.ConnectionAborted);

            _logger.LogDebug("Broadcast command notification to group {GroupName}", groupName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to session {SessionId}", request.SessionId);
            return false;
        }
    }

    /// <summary>
    /// Извлекает целочисленный параметр из словаря параметров подключения.
    /// </summary>
    /// <param name="parameters">Словарь параметров</param>
    /// <param name="key">Ключ параметра</param>
    /// <returns>Значение параметра или null, если параметр не найден или не может быть преобразован</returns>
    private static int? ParseIntParameter(Dictionary<string, string>? parameters, string key)
    {
        return parameters != null &&
               parameters.TryGetValue(key, out var value) &&
               int.TryParse(value, out var result)
            ? result
            : null;
    }

    /// <summary>
    /// Извлекает строковый параметр из словаря параметров подключения.
    /// </summary>
    /// <param name="parameters">Словарь параметров</param>
    /// <param name="key">Ключ параметра</param>
    /// <returns>Значение параметра или null, если параметр не найден</returns>
    private static string? GetStringParameter(Dictionary<string, string>? parameters, string key)
    {
        return parameters != null && parameters.TryGetValue(key, out var value)
            ? value
            : null;
    }
}
