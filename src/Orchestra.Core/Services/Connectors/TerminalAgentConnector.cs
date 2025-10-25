using System.IO.Pipes;
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
    public Task<ConnectionResult> ConnectAsync(
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
            return Task.FromResult(ConnectionResult.CreateFailure(errorMessage));
        }

        // NOTE: Connection implementation pending (Task 1.2B: Cross-Platform Socket Connection)
        _logger.LogWarning(
            "ConnectAsync not yet implemented. " +
            "Implementation pending for cross-platform socket connection");

        return Task.FromResult(ConnectionResult.CreateFailure(
            "Not implemented: ConnectAsync stub. Implementation pending in Task 1.2B."));
    }

    /// <inheritdoc />
    public Task<CommandResult> SendCommandAsync(
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

        // NOTE: Command sending implementation pending (Task 1.2C: Command Sending and Output Reading)
        _logger.LogWarning(
            "SendCommandAsync not yet implemented. " +
            "Implementation pending for command sending and output reading");

        return Task.FromResult(CommandResult.CreateFailure(
            command,
            "Not implemented: SendCommandAsync stub. Implementation pending in Task 1.2C."));
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

        // NOTE: Output reading implementation pending (Task 1.2C: Command Sending and Output Reading)
        _logger.LogWarning(
            "ReadOutputAsync not yet implemented. " +
            "Implementation pending for output reading from connected agent");

        // Yield empty enumerable for now
        await Task.CompletedTask;
        yield break;
    }

    /// <inheritdoc />
    public Task<DisconnectionResult> DisconnectAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!IsConnected && Status != ConnectionStatus.Error)
        {
            throw new InvalidOperationException("Not connected to any agent.");
        }

        // NOTE: Disconnection implementation pending (Task 1.2D: Integration and Error Handling)
        _logger.LogWarning(
            "DisconnectAsync not yet implemented. " +
            "Implementation pending for proper disconnection and cleanup");

        return Task.FromResult(DisconnectionResult.CreateFailure(
            "Not implemented: DisconnectAsync stub. Implementation pending in Task 1.2D."));
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
