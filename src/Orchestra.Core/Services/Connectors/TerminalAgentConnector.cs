using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Models;

namespace Orchestra.Core.Services.Connectors;

/// <summary>
/// Коннектор для подключения к агентам через терминал (Unix Domain Sockets / Named Pipes)
/// </summary>
/// <remarks>
/// <para>
/// TerminalAgentConnector обеспечивает подключение к внешним агентам (Claude Code, Cursor)
/// через их терминальный интерфейс. Поддерживает:
/// - Unix Domain Sockets (Linux, macOS, Windows 10+)
/// - Named Pipes (Windows legacy)
/// </para>
/// <para>
/// Lifecycle:
/// 1. ConnectAsync() - устанавливает соединение
/// 2. SendCommandAsync() - отправляет команды в stdin агента
/// 3. ReadOutputAsync() - читает stdout/stderr агента
/// 4. DisconnectAsync() - закрывает соединение
/// 5. Dispose() - освобождает ресурсы
/// </para>
/// <para>
/// <b>DI Registration:</b>
/// <code>
/// services.AddTransient&lt;TerminalAgentConnector&gt;();
/// </code>
/// </para>
/// </remarks>
public class TerminalAgentConnector : IAgentConnector
{
    private readonly ILogger<TerminalAgentConnector> _logger;
    private readonly IAgentOutputBuffer _outputBuffer;
    private readonly TerminalConnectorOptions _options;

    // Connection state
    private string? _agentId;
    private ConnectionStatus _status;
    private Stream? _connectionStream;
    private Task? _outputReaderTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private DateTime _lastActivityAt;

    // Disposal state
    private bool _disposed;

    /// <summary>
    /// Создает новый экземпляр TerminalAgentConnector
    /// </summary>
    /// <param name="logger">Логгер для диагностики</param>
    /// <param name="outputBuffer">Буфер для вывода агента</param>
    /// <param name="options">Настройки терминального коннектора</param>
    public TerminalAgentConnector(
        ILogger<TerminalAgentConnector> logger,
        IAgentOutputBuffer outputBuffer,
        IOptions<TerminalConnectorOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _outputBuffer = outputBuffer ?? throw new ArgumentNullException(nameof(outputBuffer));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _status = ConnectionStatus.Disconnected;
        _lastActivityAt = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public string ConnectorType => "terminal";

    /// <inheritdoc />
    public string? AgentId => _agentId;

    /// <inheritdoc />
    public ConnectionStatus Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                var oldStatus = _status;
                _status = value;
                OnStatusChanged(oldStatus, value);
            }
        }
    }

    /// <inheritdoc />
    public bool IsConnected => _status == ConnectionStatus.Connected;

    /// <inheritdoc />
    public DateTime LastActivityAt => _lastActivityAt;

    /// <inheritdoc />
    public event EventHandler<ConnectionStatusChangedEventArgs>? StatusChanged;

    /// <inheritdoc />
    public async Task<ConnectionResult> ConnectAsync(
        string agentId,
        AgentConnectionParams connectionParams,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentNullException(nameof(agentId));
        }

        if (connectionParams == null)
        {
            throw new ArgumentNullException(nameof(connectionParams));
        }

        if (IsConnected)
        {
            throw new InvalidOperationException($"Already connected to agent '{_agentId}'");
        }

        // Validate connection parameters for terminal connector
        var validationErrors = connectionParams.Validate("terminal");
        if (validationErrors.Count > 0)
        {
            var errorMessage = string.Join(", ", validationErrors);
            _logger.LogError("Invalid connection parameters: {Errors}", errorMessage);
            return ConnectionResult.CreateFailure(errorMessage);
        }

        try
        {
            // Update status to Connecting
            Status = ConnectionStatus.Connecting;
            _logger.LogInformation("Connecting to agent '{AgentId}'...", agentId);

            // Determine preferred connection method
            var connectionMethod = GetPreferredConnectionMethod(connectionParams);

            if (connectionMethod == null)
            {
                var errorMsg = "No valid connection method available for current platform. " +
                              $"Platform supports Unix sockets: {SupportsUnixDomainSockets()}, " +
                              $"Options: UseUnixSockets={_options.UseUnixSockets}, UseNamedPipes={_options.UseNamedPipes}";
                _logger.LogError(errorMsg);
                Status = ConnectionStatus.Disconnected;
                return ConnectionResult.CreateFailure(errorMsg);
            }

            Stream connectionStream;

            // Connect using selected method
            if (connectionMethod == "unix")
            {
                // Unix Domain Socket connection
                var socketPath = connectionParams.SocketPath ?? _options.DefaultSocketPath;
                if (string.IsNullOrWhiteSpace(socketPath))
                {
                    var errorMsg = "Unix socket path not provided in connection params or options";
                    _logger.LogError(errorMsg);
                    Status = ConnectionStatus.Disconnected;
                    return ConnectionResult.CreateFailure(errorMsg);
                }

                _logger.LogDebug("Using Unix Domain Socket connection: {SocketPath}", socketPath);
                var socket = await ConnectUnixDomainSocketAsync(socketPath, cancellationToken);
                connectionStream = new NetworkStream(socket, ownsSocket: true);
            }
            else if (connectionMethod == "namedpipe")
            {
                // Named Pipe connection
                var pipeName = connectionParams.PipeName ?? _options.DefaultPipeName;
                if (string.IsNullOrWhiteSpace(pipeName))
                {
                    var errorMsg = "Named pipe name not provided in connection params or options";
                    _logger.LogError(errorMsg);
                    Status = ConnectionStatus.Disconnected;
                    return ConnectionResult.CreateFailure(errorMsg);
                }

                _logger.LogDebug("Using Named Pipe connection: {PipeName}", pipeName);
                connectionStream = await ConnectWindowsNamedPipeAsync(pipeName, cancellationToken);
            }
            else
            {
                // Should never happen due to GetPreferredConnectionMethod validation
                var errorMsg = $"Unknown connection method: {connectionMethod}";
                _logger.LogError(errorMsg);
                Status = ConnectionStatus.Disconnected;
                return ConnectionResult.CreateFailure(errorMsg);
            }

            // Store connection state
            _connectionStream = connectionStream;
            _agentId = agentId;
            UpdateLastActivity();

            // Start background output reader
            _cancellationTokenSource = new CancellationTokenSource();
            _outputReaderTask = Task.Run(
                () => ReadOutputLoopAsync(connectionStream, _cancellationTokenSource.Token),
                _cancellationTokenSource.Token);

            // Update status to Connected
            Status = ConnectionStatus.Connected;
            UpdateLastActivity();

            _logger.LogInformation(
                "Successfully connected to agent '{AgentId}' using {Method}",
                agentId,
                connectionMethod);

            // Create success result with session ID
            var sessionId = $"{agentId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            return ConnectionResult.CreateSuccess(
                sessionId,
                new Dictionary<string, object>
                {
                    { "connectionMethod", connectionMethod },
                    { "agentId", agentId },
                    { "connectedAt", DateTime.UtcNow }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to agent '{AgentId}'", agentId);
            Status = ConnectionStatus.Error;
            UpdateLastActivity();

            // Clean up partial connection resources
            if (_cancellationTokenSource != null)
            {
                try
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        _cancellationTokenSource.Cancel();
                    }
                    _cancellationTokenSource.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
                _cancellationTokenSource = null;
            }

            if (_outputReaderTask != null && !_outputReaderTask.IsCompleted)
            {
                try
                {
                    // Give the task a brief moment to complete after cancellation
                    _outputReaderTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException)
                {
                    // Task may have thrown, ignore during cleanup
                }
                _outputReaderTask = null;
            }

            if (_connectionStream != null)
            {
                try
                {
                    _connectionStream.Dispose();
                }
                catch (Exception disposeEx)
                {
                    _logger.LogWarning(disposeEx, "Error disposing connection stream during cleanup");
                }
                _connectionStream = null;
            }

            // Clear agent ID
            _agentId = null;

            return ConnectionResult.CreateFailure(
                $"Connection failed: {ex.Message}",
                new Dictionary<string, object>
                {
                    { "exceptionType", ex.GetType().Name },
                    { "agentId", agentId },
                    { "timestamp", DateTime.UtcNow }
                });
        }
    }

    /// <inheritdoc />
    public async Task<CommandResult> SendCommandAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to any agent. Call ConnectAsync first.");
        }

        if (_connectionStream == null)
        {
            throw new InvalidOperationException("Connection stream is null. Connection may have been closed.");
        }

        try
        {
            _logger.LogDebug("Sending command to agent '{AgentId}': {Command}", _agentId, command);

            // Write command to stream with UTF8 encoding
            var commandBytes = System.Text.Encoding.UTF8.GetBytes(command + "\n");
            await _connectionStream.WriteAsync(commandBytes, 0, commandBytes.Length, cancellationToken);

            // Flush stream to ensure command is sent immediately
            await _connectionStream.FlushAsync(cancellationToken);

            // Update last activity timestamp
            UpdateLastActivity();

            _logger.LogDebug("Command sent successfully to agent '{AgentId}'", _agentId);

            return CommandResult.CreateSuccess(command);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error sending command to agent '{AgentId}'", _agentId);

            // Update status to error
            Status = ConnectionStatus.Error;
            UpdateLastActivity();

            return CommandResult.CreateFailure(
                command,
                $"IO error sending command: {ex.Message}");
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Stream disposed while sending command to agent '{AgentId}'", _agentId);

            // Update status to disconnected
            Status = ConnectionStatus.Disconnected;
            UpdateLastActivity();

            return CommandResult.CreateFailure(
                command,
                $"Stream disposed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending command to agent '{AgentId}'", _agentId);

            // Update status to error
            Status = ConnectionStatus.Error;
            UpdateLastActivity();

            return CommandResult.CreateFailure(
                command,
                $"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ReadOutputAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to any agent. Call ConnectAsync first.");
        }

        _logger.LogDebug("Starting output streaming for agent '{AgentId}'", _agentId);

        // Stream lines from output buffer
        await foreach (var line in _outputBuffer.GetLinesAsync(regexFilter: null, cancellationToken))
        {
            // Update last activity on each read
            UpdateLastActivity();

            _logger.LogTrace("Yielding output line for agent '{AgentId}': {Line}", _agentId, line);

            yield return line;
        }

        _logger.LogDebug("Output streaming completed for agent '{AgentId}'", _agentId);
    }

    /// <inheritdoc />
    public async Task<DisconnectionResult> DisconnectAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (Status == ConnectionStatus.Disconnected)
        {
            throw new InvalidOperationException("Not connected to any agent.");
        }

        // Allow disconnect from: Connected, Connecting, Error, or Disconnecting states
        // This provides idempotency and allows cleanup from any active state

        var disconnectingAgentId = _agentId ?? "unknown";
        var wasError = Status == ConnectionStatus.Error;
        var disconnectReason = wasError
            ? "Connection error"
            : "Normal disconnection";

        _logger.LogInformation(
            "Disconnecting from agent '{AgentId}' (Reason: {Reason})",
            disconnectingAgentId,
            disconnectReason);

        try
        {
            // Update status to Disconnecting
            Status = ConnectionStatus.Disconnecting;

            // Cancel background output reader task
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogDebug("Cancelling background output reader task for agent '{AgentId}'", disconnectingAgentId);
                _cancellationTokenSource.Cancel();
            }

            // Wait for output reader task to complete with timeout
            if (_outputReaderTask != null && !_outputReaderTask.IsCompleted)
            {
                _logger.LogDebug("Waiting for output reader task to complete for agent '{AgentId}'", disconnectingAgentId);

                var waitTask = _outputReaderTask;
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                var completedTask = await Task.WhenAny(waitTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning(
                        "Output reader task did not complete within timeout (5 seconds) for agent '{AgentId}'",
                        disconnectingAgentId);
                }
                else
                {
                    _logger.LogDebug("Output reader task completed for agent '{AgentId}'", disconnectingAgentId);
                }
            }

            // Close and dispose connection stream
            if (_connectionStream != null)
            {
                _logger.LogDebug("Closing connection stream for agent '{AgentId}'", disconnectingAgentId);

                try
                {
                    _connectionStream.Close();
                    _connectionStream.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing connection stream for agent '{AgentId}'", disconnectingAgentId);
                }

                _connectionStream = null;
            }

            // Dispose cancellation token source
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            // Clear output reader task reference
            _outputReaderTask = null;

            // Update status to Disconnected
            Status = ConnectionStatus.Disconnected;

            // Clear agent ID
            var finalAgentId = _agentId;
            _agentId = null;

            UpdateLastActivity();

            _logger.LogInformation("Successfully disconnected from agent '{AgentId}'", finalAgentId);

            var reason = wasError
                ? DisconnectionReason.Error
                : DisconnectionReason.UserRequested;

            return DisconnectionResult.CreateSuccess(
                reason,
                new Dictionary<string, object>
                {
                    { "agentId", finalAgentId ?? "unknown" },
                    { "disconnectedAt", DateTime.UtcNow },
                    { "disconnectReason", disconnectReason }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnection from agent '{AgentId}'", disconnectingAgentId);

            // Ensure status is set to disconnected even on error
            Status = ConnectionStatus.Disconnected;
            _agentId = null;
            UpdateLastActivity();

            return DisconnectionResult.CreateFailure(
                $"Disconnection completed with errors: {ex.Message}",
                new Dictionary<string, object>
                {
                    { "agentId", disconnectingAgentId },
                    { "exceptionType", ex.GetType().Name },
                    { "disconnectedAt", DateTime.UtcNow }
                });
        }
    }

    /// <summary>
    /// Вызывает событие изменения статуса подключения
    /// </summary>
    /// <param name="oldStatus">Предыдущий статус</param>
    /// <param name="newStatus">Новый статус</param>
    private void OnStatusChanged(ConnectionStatus oldStatus, ConnectionStatus newStatus)
    {
        _logger.LogInformation(
            "Connection status changed for agent '{AgentId}': {OldStatus} -> {NewStatus}",
            _agentId ?? "null",
            oldStatus,
            newStatus);

        StatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
        {
            AgentId = _agentId ?? string.Empty,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Обновляет время последней активности
    /// </summary>
    private void UpdateLastActivity()
    {
        _lastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Подключается к агенту через Windows Named Pipe
    /// </summary>
    /// <param name="pipeName">Имя именованного канала (без префикса \\.\pipe\)</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Поток подключенного именованного канала</returns>
    /// <exception cref="ArgumentException">Если имя канала невалидно</exception>
    /// <exception cref="TimeoutException">Если подключение не удалось в течение таймаута</exception>
    /// <exception cref="IOException">Если произошла ошибка ввода-вывода при подключении</exception>
    /// <remarks>
    /// Используется для подключения к агентам на Windows, особенно для legacy систем,
    /// где Unix Domain Sockets недоступны. Создает NamedPipeClientStream и подключается
    /// к указанному каналу на локальной машине.
    ///
    /// Timeout: Определяется через TerminalConnectorOptions.ConnectionTimeoutMs
    /// Direction: InOut (двунаправленный)
    /// Options: Asynchronous (асинхронный режим)
    /// </remarks>
    private async Task<Stream> ConnectWindowsNamedPipeAsync(
        string pipeName,
        CancellationToken cancellationToken)
    {
        if (!IsValidWindowsPipeName(pipeName))
        {
            throw new ArgumentException(
                $"Invalid pipe name: '{pipeName}'. Pipe name must be non-empty, " +
                $"not contain backslashes, and be less than 256 characters.",
                nameof(pipeName));
        }

        _logger.LogDebug("Connecting to Windows Named Pipe: {PipeName}", pipeName);

        // Create named pipe client stream
        var pipeClient = new NamedPipeClientStream(
            ".",                        // local machine
            pipeName,                   // pipe name
            PipeDirection.InOut,        // bidirectional
            PipeOptions.Asynchronous);  // async mode

        try
        {
            // Connect with configured timeout
            await pipeClient.ConnectAsync(_options.ConnectionTimeoutMs, cancellationToken);

            _logger.LogInformation(
                "Successfully connected to Windows Named Pipe '{PipeName}'",
                pipeName);

            return pipeClient;
        }
        catch (Exception ex)
        {
            // Dispose pipe on any error
            pipeClient.Dispose();

            // Log appropriate error message based on exception type
            var errorType = ex switch
            {
                TimeoutException => "Timeout",
                IOException => "IO error",
                _ => "Unexpected error"
            };

            _logger.LogError(
                ex,
                "{ErrorType} connecting to Named Pipe '{PipeName}'",
                errorType,
                pipeName);

            throw;
        }
    }

    /// <summary>
    /// Проверяет валидность имени Windows Named Pipe
    /// </summary>
    /// <param name="pipeName">Имя именованного канала для проверки</param>
    /// <returns>True если имя валидно, иначе false</returns>
    /// <remarks>
    /// Правила валидации:
    /// - Имя не должно быть пустым или состоять только из пробелов
    /// - Имя не должно содержать обратные слеши (\)
    /// - Имя должно быть короче 256 символов
    ///
    /// Примеры валидных имен:
    /// - "orchestra_agent_pipe"
    /// - "agent-123"
    /// - "claude_code_session"
    ///
    /// Примеры невалидных имен:
    /// - "" (пустое)
    /// - "pipe\\name" (содержит \)
    /// - [строка длиной > 255 символов]
    /// </remarks>
    private bool IsValidWindowsPipeName(string pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            return false;
        }

        if (pipeName.Contains('\\'))
        {
            return false;
        }

        if (pipeName.Length >= 256)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Подключается к агенту через Unix Domain Socket
    /// </summary>
    /// <param name="socketPath">Путь к файлу Unix Domain Socket</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Подключенный сокет</returns>
    /// <exception cref="ArgumentException">Если путь к сокету невалиден</exception>
    /// <exception cref="SocketException">Если произошла ошибка при подключении сокета</exception>
    /// <exception cref="IOException">Если произошла ошибка ввода-вывода</exception>
    /// <remarks>
    /// Используется для подключения к агентам через Unix Domain Sockets на Linux/macOS
    /// и Windows 10+ (build 17063 и выше). Создает Socket с AddressFamily.Unix и подключается
    /// к указанному пути файла сокета.
    ///
    /// Timeout: Определяется через TerminalConnectorOptions.ConnectionTimeoutMs
    /// Socket Options: NoDelay = true, ReceiveTimeout/SendTimeout = ConnectionTimeoutMs
    /// </remarks>
    private async Task<Socket> ConnectUnixDomainSocketAsync(
        string socketPath,
        CancellationToken cancellationToken)
    {
        if (!IsValidUnixSocketPath(socketPath))
        {
            throw new ArgumentException(
                $"Invalid Unix socket path: '{socketPath}'. Path must start with /, " +
                $"be less than 108 characters, and parent directory must exist.",
                nameof(socketPath));
        }

        _logger.LogDebug("Connecting to Unix Domain Socket: {SocketPath}", socketPath);

        // Create Unix Domain Socket
        var socket = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified);

        try
        {
            // Create endpoint
            var endpoint = new UnixDomainSocketEndPoint(socketPath);

            // Connect with timeout handling
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.ConnectionTimeoutMs);

            await socket.ConnectAsync(endpoint, cts.Token);

            // Configure socket options
            socket.NoDelay = true;
            socket.ReceiveTimeout = _options.ConnectionTimeoutMs;
            socket.SendTimeout = _options.ConnectionTimeoutMs;

            _logger.LogInformation(
                "Successfully connected to Unix Domain Socket '{SocketPath}'",
                socketPath);

            return socket;
        }
        catch (Exception ex)
        {
            // Dispose socket on any error
            socket.Dispose();

            // Log appropriate error message based on exception type
            var errorType = ex switch
            {
                OperationCanceledException => "Timeout or cancellation",
                SocketException => "Socket error",
                IOException => "IO error",
                _ => "Unexpected error"
            };

            _logger.LogError(
                ex,
                "{ErrorType} connecting to Unix Domain Socket '{SocketPath}'",
                errorType,
                socketPath);

            throw;
        }
    }

    /// <summary>
    /// Проверяет валидность пути к Unix Domain Socket
    /// </summary>
    /// <param name="socketPath">Путь к файлу сокета для проверки</param>
    /// <returns>True если путь валиден, иначе false</returns>
    /// <remarks>
    /// Правила валидации:
    /// - Путь не должен быть пустым или состоять только из пробелов
    /// - Путь должен быть абсолютным (начинаться с / на Unix, или быть полным путем на Windows)
    /// - Путь должен быть короче 108 символов (ограничение Unix Domain Sockets)
    /// - Родительская директория должна существовать
    ///
    /// Примеры валидных путей:
    /// - "/tmp/orchestra_agent.sock" (Unix/Linux/macOS)
    /// - "/var/run/agent.socket" (Unix/Linux)
    /// - "/home/user/.orchestra/agent.sock" (Unix/Linux/macOS)
    /// - "C:\Temp\socket.sock" (Windows 10+)
    ///
    /// Примеры невалидных путей:
    /// - "" (пустое)
    /// - "relative/path.sock" (не абсолютный путь)
    /// - [строка длиной >= 108 символов]
    /// - "/nonexistent/directory/socket.sock" (родительская директория не существует)
    /// </remarks>
    private bool IsValidUnixSocketPath(string socketPath)
    {
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            return false;
        }

        // Check if path is absolute (Unix-style or Windows-style)
        if (!Path.IsPathFullyQualified(socketPath))
        {
            return false;
        }

        if (socketPath.Length >= 108)
        {
            return false;
        }

        // Check if parent directory exists
        var parentDirectory = Path.GetDirectoryName(socketPath);
        if (string.IsNullOrEmpty(parentDirectory) || !Directory.Exists(parentDirectory))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Определяет предпочтительный метод подключения для текущей платформы
    /// </summary>
    /// <param name="connectionParams">Параметры подключения с указанными путями/именами</param>
    /// <returns>
    /// Тип метода подключения:
    /// - "unix" для Unix Domain Sockets
    /// - "namedpipe" для Windows Named Pipes
    /// - null если нет доступного метода
    /// </returns>
    /// <remarks>
    /// Алгоритм выбора:
    /// 1. Если указан SocketPath и платформа поддерживает Unix Sockets → "unix"
    /// 2. Если указан PipeName и это Windows → "namedpipe"
    /// 3. Если UseUnixSockets=true и платформа поддерживает → "unix" (с DefaultSocketPath)
    /// 4. Если UseNamedPipes=true и Windows → "namedpipe" (с DefaultPipeName)
    /// 5. Иначе → null
    ///
    /// Поддержка Unix Domain Sockets:
    /// - Linux: всегда
    /// - macOS: всегда
    /// - Windows: начиная с Windows 10 build 17063 (проверяется через OperatingSystem.IsWindowsVersionAtLeast)
    /// </remarks>
    private string? GetPreferredConnectionMethod(AgentConnectionParams connectionParams)
    {
        // Priority 1: Unix Domain Socket if path specified and platform supports it
        if (!string.IsNullOrWhiteSpace(connectionParams.SocketPath))
        {
            if (SupportsUnixDomainSockets() && _options.UseUnixSockets)
            {
                return "unix";
            }
        }

        // Priority 2: Named Pipe if name specified and on Windows
        if (!string.IsNullOrWhiteSpace(connectionParams.PipeName))
        {
            if (OperatingSystem.IsWindows() && _options.UseNamedPipes)
            {
                return "namedpipe";
            }
        }

        // Priority 3: Unix Domain Socket with default path (if enabled and platform supports)
        if (_options.UseUnixSockets && SupportsUnixDomainSockets())
        {
            if (!string.IsNullOrWhiteSpace(_options.DefaultSocketPath))
            {
                return "unix";
            }
        }

        // Priority 4: Named Pipe with default name (if enabled and on Windows)
        if (_options.UseNamedPipes && OperatingSystem.IsWindows())
        {
            if (!string.IsNullOrWhiteSpace(_options.DefaultPipeName))
            {
                return "namedpipe";
            }
        }

        // No available connection method
        return null;
    }

    /// <summary>
    /// Проверяет поддерживает ли текущая платформа Unix Domain Sockets
    /// </summary>
    /// <returns>True если Unix Domain Sockets поддерживаются</returns>
    /// <remarks>
    /// Unix Domain Sockets поддерживаются на:
    /// - Linux (все версии)
    /// - macOS (все версии)
    /// - Windows 10 build 17063 и выше (осень 2018, версия 1803)
    ///
    /// Windows версии проверяются через OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17063).
    /// </remarks>
    private static bool SupportsUnixDomainSockets()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return true;
        }

        if (OperatingSystem.IsWindows())
        {
            // Windows 10 build 17063 (версия 1803, апрель 2018) добавила поддержку Unix Domain Sockets
            return OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17063);
        }

        return false;
    }

    /// <summary>
    /// Читает вывод агента в фоновом режиме
    /// </summary>
    /// <param name="stream">Поток для чтения вывода</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Task для фонового чтения</returns>
    /// <remarks>
    /// Этот метод работает в фоновом режиме, непрерывно читая строки из потока
    /// и передавая их в IAgentOutputBuffer через AppendLineAsync.
    /// Обновляет LastActivityAt при каждом чтении строки.
    /// Завершается при получении сигнала отмены или окончании потока.
    /// </remarks>
    private async Task ReadOutputLoopAsync(Stream stream, CancellationToken cancellationToken)
    {
        _logger.LogDebug("ReadOutputLoopAsync started for agent '{AgentId}'", _agentId);

        try
        {
            // Create StreamReader with UTF8 encoding and leave stream open
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Read line from stream
                    var line = await reader.ReadLineAsync(cancellationToken);

                    // Check if stream ended (null = end of stream)
                    if (line == null)
                    {
                        _logger.LogInformation("Stream ended for agent '{AgentId}' (ReadLineAsync returned null)", _agentId);

                        // Update status to disconnected
                        Status = ConnectionStatus.Disconnected;
                        break;
                    }

                    // Append line to output buffer
                    await _outputBuffer.AppendLineAsync(line, cancellationToken);

                    // Update last activity timestamp
                    UpdateLastActivity();

                    _logger.LogTrace("Read line from agent '{AgentId}': {Line}", _agentId, line);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation requested - exit loop gracefully
                    _logger.LogDebug("ReadOutputLoopAsync cancelled for agent '{AgentId}'", _agentId);
                    break;
                }
                catch (IOException ex)
                {
                    _logger.LogError(ex, "IO error reading from agent '{AgentId}'", _agentId);

                    // Update status to error
                    Status = ConnectionStatus.Error;
                    break;
                }
                catch (ObjectDisposedException ex)
                {
                    _logger.LogWarning(ex, "Stream disposed while reading from agent '{AgentId}'", _agentId);

                    // Update status to disconnected
                    Status = ConnectionStatus.Disconnected;
                    break;
                }
            }

            _logger.LogInformation("ReadOutputLoopAsync completed for agent '{AgentId}'", _agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ReadOutputLoopAsync for agent '{AgentId}'", _agentId);

            // Update status to error
            Status = ConnectionStatus.Error;
        }
        finally
        {
            UpdateLastActivity();
        }
    }

    /// <summary>
    /// Проверяет, был ли объект disposed
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TerminalAgentConnector));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Освобождает ресурсы коннектора
    /// </summary>
    /// <param name="disposing">True если вызвано из Dispose(), false если из финализатора</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _logger.LogDebug("Disposing TerminalAgentConnector for agent '{AgentId}'", _agentId);

            // Cancel background tasks
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            // Wait for output reader task to complete
            if (_outputReaderTask != null && !_outputReaderTask.IsCompleted)
            {
                try
                {
                    _outputReaderTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException ex)
                {
                    _logger.LogWarning(ex, "Exception while waiting for output reader task to complete");
                }

                _outputReaderTask = null;
            }

            // Close connection stream
            if (_connectionStream != null)
            {
                _connectionStream.Dispose();
                _connectionStream = null;
            }

            // Update status
            if (_status != ConnectionStatus.Disconnected)
            {
                Status = ConnectionStatus.Disconnected;
            }
        }

        _disposed = true;
    }
}
