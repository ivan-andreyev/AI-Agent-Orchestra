using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Models;

namespace Orchestra.Core.Services.Connectors;

/// <summary>
/// Реализация менеджера сессий агентов с потокобезопасным управлением жизненным циклом.
/// </summary>
/// <remarks>
/// AgentSessionManager управляет созданием, хранением и удалением активных сессий подключения к внешним агентам.
/// Использует ConcurrentDictionary для потокобезопасного хранения сессий.
/// Подписывается на события коннекторов для отслеживания изменений статуса.
/// </remarks>
public class AgentSessionManager : IAgentSessionManager, IDisposable
{
    private readonly ConcurrentDictionary<string, AgentSession> _sessions;
    private readonly ConcurrentDictionary<string, EventHandler<ConnectionStatusChangedEventArgs>> _statusHandlers;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentSessionManager> _logger;
    private bool _disposed;

    /// <summary>
    /// Инициализирует новый экземпляр менеджера сессий агентов.
    /// </summary>
    /// <param name="serviceProvider">Провайдер сервисов для создания коннекторов и буферов.</param>
    /// <param name="logger">Логгер для диагностики.</param>
    public AgentSessionManager(
        IServiceProvider serviceProvider,
        ILogger<AgentSessionManager> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessions = new ConcurrentDictionary<string, AgentSession>(StringComparer.OrdinalIgnoreCase);
        _statusHandlers = new ConcurrentDictionary<string, EventHandler<ConnectionStatusChangedEventArgs>>(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("AgentSessionManager initialized");
    }

    /// <inheritdoc/>
    public event EventHandler<SessionCreatedEventArgs>? SessionCreated;

    /// <inheritdoc/>
    public event EventHandler<SessionDisconnectedEventArgs>? SessionDisconnected;

    /// <inheritdoc/>
    public event EventHandler<SessionErrorEventArgs>? SessionError;

    /// <inheritdoc/>
    public async Task<AgentSession> CreateSessionAsync(
        string agentId,
        AgentConnectionParams connectionParams,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentNullException(nameof(agentId));
        }

        if (connectionParams == null)
        {
            throw new ArgumentNullException(nameof(connectionParams));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Creating session for agent {AgentId}", agentId);

        // Проверяем, не существует ли уже сессия для этого агента
        if (_sessions.ContainsKey(agentId))
        {
            _logger.LogWarning("Session for agent {AgentId} already exists", agentId);
            throw new InvalidOperationException($"Session for agent '{agentId}' already exists. Disconnect existing session first.");
        }

        try
        {
            // Создаем коннектор через DI (будет реализовано позже в Task 1.2)
            var connector = CreateConnector(connectionParams);

            // Создаем буфер вывода через DI (будет реализовано позже в Task 1.4)
            var outputBuffer = CreateOutputBuffer();

            var now = DateTime.UtcNow;

            // Создаем сессию
            var session = new AgentSession
            {
                AgentId = agentId,
                Connector = connector,
                OutputBuffer = outputBuffer,
                CreatedAt = now,
                LastActivityAt = now,
                ConnectionParams = connectionParams
            };

            // Подписываемся на события коннектора
            EventHandler<ConnectionStatusChangedEventArgs> statusHandler = (sender, args) => OnConnectorStatusChanged(agentId, args);
            connector.StatusChanged += statusHandler;
            _statusHandlers[agentId] = statusHandler;

            // Подключаемся к агенту
            var connectionResult = await connector.ConnectAsync(agentId, connectionParams, cancellationToken);

            if (!connectionResult.Success)
            {
                _logger.LogError("Failed to connect to agent {AgentId}: {ErrorMessage}", agentId, connectionResult.ErrorMessage);
                throw new InvalidOperationException($"Failed to connect to agent '{agentId}': {connectionResult.ErrorMessage}");
            }

            // Добавляем сессию в словарь
            if (!_sessions.TryAdd(agentId, session))
            {
                // Теоретически невозможно, но на всякий случай
                await connector.DisconnectAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to register session for agent '{agentId}'. Concurrent creation detected.");
            }

            _logger.LogInformation("Session created successfully for agent {AgentId}, Status: {Status}", agentId, session.Status);

            // Генерируем событие создания сессии
            SessionCreated?.Invoke(this, new SessionCreatedEventArgs
            {
                AgentId = agentId,
                CreatedAt = session.CreatedAt,
                Status = session.Status
            });

            return session;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error creating session for agent {AgentId}", agentId);

            // Генерируем событие ошибки
            SessionError?.Invoke(this, new SessionErrorEventArgs
            {
                AgentId = agentId,
                ErrorAt = DateTime.UtcNow,
                Error = ex,
                Context = "CreateSessionAsync"
            });

            throw;
        }
    }

    /// <inheritdoc/>
    public AgentSession? GetSessionAsync(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentNullException(nameof(agentId));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_sessions.TryGetValue(agentId, out var session))
        {
            // Обновляем время последней активности
            session.LastActivityAt = DateTime.UtcNow;
            _logger.LogDebug("Session retrieved for agent {AgentId}, Status: {Status}", agentId, session.Status);
            return session;
        }

        _logger.LogDebug("Session not found for agent {AgentId}", agentId);
        return null;
    }

    /// <inheritdoc/>
    public async Task<bool> DisconnectSessionAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentNullException(nameof(agentId));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Disconnecting session for agent {AgentId}", agentId);

        if (!_sessions.TryRemove(agentId, out var session))
        {
            _logger.LogWarning("Session not found for agent {AgentId}", agentId);
            return false;
        }

        try
        {
            // Отписываемся от событий коннектора
            if (_statusHandlers.TryRemove(agentId, out var statusHandler))
            {
                session.Connector.StatusChanged -= statusHandler;
            }

            // Отключаемся от агента
            var disconnectResult = await session.Connector.DisconnectAsync(cancellationToken);

            if (!disconnectResult.Success)
            {
                _logger.LogWarning("Disconnect returned failure for agent {AgentId}: {ErrorMessage}", agentId, disconnectResult.ErrorMessage);
            }

            // Освобождаем ресурсы коннектора
            if (session.Connector is IDisposable disposableConnector)
            {
                disposableConnector.Dispose();
            }

            _logger.LogInformation("Session disconnected successfully for agent {AgentId}", agentId);

            // Генерируем событие отключения
            SessionDisconnected?.Invoke(this, new SessionDisconnectedEventArgs
            {
                AgentId = agentId,
                DisconnectedAt = DateTime.UtcNow,
                Reason = "Manual disconnect"
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting session for agent {AgentId}", agentId);

            // Генерируем событие ошибки
            SessionError?.Invoke(this, new SessionErrorEventArgs
            {
                AgentId = agentId,
                ErrorAt = DateTime.UtcNow,
                Error = ex,
                Context = "DisconnectSessionAsync"
            });

            throw;
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<AgentSession>> GetAllSessionsAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sessions = _sessions.Values.ToList();
        _logger.LogDebug("Retrieved {Count} active sessions", sessions.Count);
        return Task.FromResult<IReadOnlyCollection<AgentSession>>(sessions);
    }

    /// <summary>
    /// Обрабатывает событие изменения статуса коннектора.
    /// </summary>
    private void OnConnectorStatusChanged(string agentId, ConnectionStatusChangedEventArgs args)
    {
        _logger.LogInformation("Connector status changed for agent {AgentId}: {OldStatus} -> {NewStatus}, Reason: {Reason}",
            agentId, args.OldStatus, args.NewStatus, args.Reason);

        // Если коннектор отключился неожиданно (не по нашей команде), логируем
        if (args.NewStatus == ConnectionStatus.Disconnected && args.Reason != null)
        {
            _logger.LogWarning("Agent {AgentId} disconnected: {Reason}", agentId, args.Reason);
        }
    }

    /// <summary>
    /// Создает коннектор агента на основе параметров подключения.
    /// </summary>
    /// <remarks>
    /// NOTE: Текущая реализация создает IAgentConnector из DI.
    /// В будущем будет добавлена фабрика для создания разных типов коннекторов на основе ConnectorType.
    /// </remarks>
    private IAgentConnector CreateConnector(AgentConnectionParams connectionParams)
    {
        // NOTE: В будущем добавить фабрику для создания разных типов коннекторов
        // на основе connectionParams.ConnectorType (terminal, api, tab-based)

        // Пытаемся получить IAgentConnector из DI
        var connector = _serviceProvider.GetService(typeof(IAgentConnector)) as IAgentConnector;
        if (connector == null)
        {
            throw new InvalidOperationException(
                "IAgentConnector is not registered in DI container. " +
                "Register connector implementation (e.g., TerminalAgentConnector) as services.AddTransient<IAgentConnector, TerminalAgentConnector>() in Startup.cs");
        }

        return connector;
    }

    /// <summary>
    /// Создает буфер вывода агента.
    /// </summary>
    /// <remarks>
    /// NOTE: Текущая реализация требует регистрации IAgentOutputBuffer в DI.
    /// Полная реализация буфера будет в Phase 1.4.
    /// </remarks>
    private IAgentOutputBuffer CreateOutputBuffer()
    {
        var buffer = _serviceProvider.GetService(typeof(IAgentOutputBuffer)) as IAgentOutputBuffer;
        if (buffer == null)
        {
            throw new InvalidOperationException(
                "IAgentOutputBuffer is not registered in DI container. " +
                "Register it in Startup.cs (will be implemented in Phase 1.4)");
        }

        return buffer;
    }

    /// <summary>
    /// Освобождает ресурсы менеджера сессий.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing AgentSessionManager, closing {Count} active sessions", _sessions.Count);

        // Отключаем все активные сессии
        foreach (var kvp in _sessions)
        {
            try
            {
                var session = kvp.Value;

                // Отписываемся от событий
                if (_statusHandlers.TryRemove(kvp.Key, out var statusHandler))
                {
                    session.Connector.StatusChanged -= statusHandler;
                }

                session.Connector.DisconnectAsync(CancellationToken.None).GetAwaiter().GetResult();

                if (session.Connector is IDisposable disposableConnector)
                {
                    disposableConnector.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing session for agent {AgentId}", kvp.Key);
            }
        }

        _sessions.Clear();
        _statusHandlers.Clear();
        _disposed = true;

        _logger.LogInformation("AgentSessionManager disposed");
    }
}
