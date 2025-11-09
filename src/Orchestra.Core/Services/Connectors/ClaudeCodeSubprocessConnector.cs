using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Commands.Sessions;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Models;
using Orchestra.Core.Queries.Sessions;

namespace Orchestra.Core.Services.Connectors;

/// <summary>
/// Коннектор для подключения к Claude Code через subprocess с stdin/stdout коммуникацией.
///
/// Использует официальный Claude Code CLI в headless режиме (--print) для:
/// - Проактивной отправки задач через stdin
/// - Чтения структурированных JSON ответов через stdout
/// - Управления сессиями через --session-id / --resume
/// - Контроля разрешений через --dangerously-skip-permissions
/// </summary>
public class ClaudeCodeSubprocessConnector : IAgentConnector
{
    private readonly ILogger<ClaudeCodeSubprocessConnector> _logger;
    private readonly IMediator _mediator;
    private Process? _claudeProcess;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private string? _sessionId;
    private string? _agentId;
    private string? _databaseSessionId;
    private DateTime _lastActivityAt = DateTime.UtcNow;
    private bool _disposed;

    /// <summary>
    /// Инициализирует новый экземпляр коннектора
    /// </summary>
    public ClaudeCodeSubprocessConnector(
        ILogger<ClaudeCodeSubprocessConnector> logger,
        IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <inheritdoc />
    public string ConnectorType => "subprocess";

    /// <inheritdoc />
    public string? AgentId => _agentId;

    /// <inheritdoc />
    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

    /// <inheritdoc />
    public bool IsConnected => Status == ConnectionStatus.Connected && _claudeProcess?.HasExited == false;

    /// <inheritdoc />
    public DateTime LastActivityAt => _lastActivityAt;

    /// <inheritdoc />
    public event EventHandler<ConnectionStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Подключается к Claude Code процессу в режиме subprocess
    /// </summary>
    public async Task<ConnectionResult> ConnectAsync(
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

        if (IsConnected)
        {
            var error = $"Connector is already connected to agent {_agentId}";
            _logger.LogError(error);
            throw new InvalidOperationException(error);
        }

        try
        {
            ChangeStatus(ConnectionStatus.Connecting);

            _agentId = agentId;
            _sessionId = Guid.NewGuid().ToString();
            var workingDirectory = connectionParams.WorkingDirectory ?? Directory.GetCurrentDirectory();

            // Запускаем claude code в headless режиме
            var processInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = BuildArguments(_sessionId, skipPermissions: true),
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _claudeProcess = new Process { StartInfo = processInfo };

            _logger.LogInformation(
                "Starting Claude Code subprocess for agent {AgentId} in directory {WorkingDirectory} with SessionId {SessionId}",
                agentId,
                workingDirectory,
                _sessionId);

            if (!_claudeProcess.Start())
            {
                throw new InvalidOperationException("Failed to start Claude Code process");
            }

            _stdin = _claudeProcess.StandardInput;
            _stdout = _claudeProcess.StandardOutput;

            _lastActivityAt = DateTime.UtcNow;
            ChangeStatus(ConnectionStatus.Connected);

            // Создание сессии в БД через MediatR
            Data.Entities.AgentSession? sessionEntity = null;
            try
            {
                sessionEntity = await _mediator.Send(
                    new CreateAgentSessionCommand(
                        agentId,
                        _sessionId,
                        workingDirectory,
                        _claudeProcess.Id
                    ),
                    cancellationToken);

                _databaseSessionId = sessionEntity.Id;

                _logger.LogDebug("Created database session {DatabaseSessionId} for subprocess SessionId {SessionId}",
                    _databaseSessionId, _sessionId);
            }
            catch (Exception dbEx)
            {
                _logger.LogWarning(dbEx, "Failed to create database session for {SessionId}, continuing without DB tracking", _sessionId);
                // Продолжаем работу даже если БД недоступна
            }

            var metadata = new Dictionary<string, object>
            {
                { "ProcessId", _claudeProcess.Id },
                { "SessionId", _sessionId },
                { "WorkingDirectory", workingDirectory }
            };

            if (sessionEntity != null)
            {
                metadata.Add("DatabaseSessionId", sessionEntity.Id);
                metadata.Add("MessageCount", sessionEntity.MessageCount);
                metadata.Add("TotalCostUsd", sessionEntity.TotalCostUsd);
            }

            _logger.LogInformation(
                "Successfully connected to Claude Code subprocess. ProcessId: {ProcessId}, SessionId: {SessionId}",
                _claudeProcess.Id,
                _sessionId);

            return ConnectionResult.CreateSuccess(_sessionId, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Claude Code subprocess for agent {AgentId}", agentId);
            ChangeStatus(ConnectionStatus.Error);
            CleanupProcess();
            return ConnectionResult.CreateFailure($"Failed to connect: {ex.Message}");
        }
    }

    /// <summary>
    /// Отправляет задачу в Claude Code через stdin и возвращает результат из stdout
    /// </summary>
    public async Task<CommandResult> SendCommandAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (!IsConnected)
        {
            return CommandResult.CreateFailure(command, "Connector is not connected to agent");
        }

        try
        {
            _logger.LogDebug("Sending command to Claude Code subprocess: {Command}", command);

            // Отправляем prompt через stdin
            await _stdin!.WriteLineAsync(command);
            await _stdin.FlushAsync();

            _lastActivityAt = DateTime.UtcNow;

            _logger.LogDebug("Command sent successfully to Claude Code subprocess");

            return CommandResult.CreateSuccess(
                command,
                new Dictionary<string, object> { { "SessionId", _sessionId ?? "unknown" } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to Claude Code subprocess");
            ChangeStatus(ConnectionStatus.Error);
            return CommandResult.CreateFailure(command, $"Failed to send command: {ex.Message}");
        }
    }

    /// <summary>
    /// Читает вывод из Claude Code в виде асинхронного потока строк
    /// </summary>
    public async IAsyncEnumerable<string> ReadOutputAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Connector is not connected to agent");
        }

        if (_stdout == null)
        {
            throw new InvalidOperationException("Standard output stream is not available");
        }

        string? line;
        while ((line = await _stdout.ReadLineAsync(cancellationToken)) != null)
        {
            _lastActivityAt = DateTime.UtcNow;
            yield return line;
        }
    }

    /// <summary>
    /// Возобновляет приостановленную сессию Claude Code
    /// </summary>
    /// <param name="sessionId">ID сессии для возобновления</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат возобновления сессии</returns>
    public async Task<ConnectionResult> ResumeSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentNullException(nameof(sessionId));
        }

        if (IsConnected)
        {
            var error = "Connector is already connected to an agent. Disconnect first.";
            _logger.LogError(error);
            return ConnectionResult.CreateFailure(error);
        }

        try
        {
            ChangeStatus(ConnectionStatus.Connecting);

            _logger.LogInformation("Resuming Claude Code session {SessionId}", sessionId);

            // Загрузка сессии из БД
            var session = await _mediator.Send(new GetAgentSessionQuery(sessionId), cancellationToken);

            if (session == null)
            {
                var error = $"Session {sessionId} not found in database";
                _logger.LogWarning(error);
                return ConnectionResult.CreateFailure(error);
            }

            if (session.Status == SessionStatus.Closed)
            {
                var error = $"Cannot resume closed session {sessionId}";
                _logger.LogWarning(error);
                return ConnectionResult.CreateFailure(error);
            }

            _agentId = session.AgentId;
            _sessionId = session.SessionId;
            _databaseSessionId = session.Id;

            // Запускаем claude code в headless режиме с --resume флагом
            var processInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = BuildArgumentsForResume(_sessionId),
                WorkingDirectory = session.WorkingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _claudeProcess = new Process { StartInfo = processInfo };

            _logger.LogInformation(
                "Starting Claude Code subprocess for session resume {SessionId} in directory {WorkingDirectory}",
                _sessionId,
                session.WorkingDirectory);

            if (!_claudeProcess.Start())
            {
                throw new InvalidOperationException("Failed to start Claude Code process for resume");
            }

            _stdin = _claudeProcess.StandardInput;
            _stdout = _claudeProcess.StandardOutput;

            _lastActivityAt = DateTime.UtcNow;
            ChangeStatus(ConnectionStatus.Connected);

            // Обновление статуса сессии в Active
            try
            {
                await _mediator.Send(
                    new UpdateSessionStatusCommand(
                        _sessionId,
                        SessionStatus.Active,
                        ProcessId: _claudeProcess.Id
                    ),
                    cancellationToken);

                _logger.LogDebug("Updated session {SessionId} status to Active with ProcessId {ProcessId}",
                    _sessionId, _claudeProcess.Id);
            }
            catch (Exception dbEx)
            {
                _logger.LogWarning(dbEx, "Failed to update session status for {SessionId}, continuing without DB tracking", _sessionId);
            }

            var metadata = new Dictionary<string, object>
            {
                { "ProcessId", _claudeProcess.Id },
                { "SessionId", _sessionId },
                { "DatabaseSessionId", _databaseSessionId },
                { "WorkingDirectory", session.WorkingDirectory },
                { "MessageCount", session.MessageCount },
                { "TotalCostUsd", session.TotalCostUsd }
            };

            _logger.LogInformation(
                "Successfully resumed Claude Code session. ProcessId: {ProcessId}, SessionId: {SessionId}",
                _claudeProcess.Id,
                _sessionId);

            return ConnectionResult.CreateSuccess(_sessionId, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume Claude Code session {SessionId}", sessionId);
            ChangeStatus(ConnectionStatus.Error);
            CleanupProcess();
            return ConnectionResult.CreateFailure($"Failed to resume session: {ex.Message}");
        }
    }

    /// <summary>
    /// Отключается от Claude Code процесса
    /// </summary>
    public async Task<DisconnectionResult> DisconnectAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return DisconnectionResult.CreateFailure("Connector is not connected to agent");
        }

        try
        {
            ChangeStatus(ConnectionStatus.Disconnecting);

            _logger.LogInformation("Disconnecting from Claude Code subprocess. AgentId: {AgentId}, SessionId: {SessionId}",
                _agentId, _sessionId);

            // Закрываем stdin для сигнала завершения
            _stdin?.Close();

            // Ожидаем завершения процесса с таймаутом
            const int timeoutMs = 5000;
            if (!_claudeProcess!.WaitForExit(timeoutMs))
            {
                _logger.LogWarning("Claude Code subprocess did not exit within {TimeoutMs}ms, killing it", timeoutMs);
                _claudeProcess.Kill();
                _claudeProcess.WaitForExit(1000);
            }

            // Обновление статуса сессии в Paused
            if (!string.IsNullOrWhiteSpace(_sessionId))
            {
                try
                {
                    await _mediator.Send(
                        new UpdateSessionStatusCommand(
                            _sessionId,
                            SessionStatus.Paused,
                            ProcessId: null
                        ),
                        cancellationToken);

                    _logger.LogInformation("Updated session {SessionId} status to Paused", _sessionId);
                }
                catch (Exception dbEx)
                {
                    _logger.LogWarning(dbEx, "Failed to update session status to Paused for {SessionId}", _sessionId);
                    // Продолжаем, даже если не удалось обновить БД
                }
            }

            _logger.LogInformation("Successfully disconnected from Claude Code subprocess");

            return DisconnectionResult.CreateSuccess(DisconnectionReason.UserRequested);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect from Claude Code subprocess");
            return DisconnectionResult.CreateFailure($"Error during disconnect: {ex.Message}");
        }
        finally
        {
            ChangeStatus(ConnectionStatus.Disconnected);
            CleanupProcess();
        }
    }

    /// <summary>
    /// Построить аргументы для запуска claude code с новой сессией
    /// </summary>
    private string BuildArguments(string sessionId, bool skipPermissions = false)
    {
        var args = new List<string>
        {
            "code",
            "--print",
            "--session-id", sessionId,
            "--output-format", "json"
        };

        if (skipPermissions)
        {
            args.Add("--dangerously-skip-permissions");
        }

        return string.Join(" ", args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a));
    }

    /// <summary>
    /// Построить аргументы для возобновления существующей сессии claude code
    /// </summary>
    private string BuildArgumentsForResume(string sessionId)
    {
        var args = new List<string>
        {
            "code",
            "--print",
            "--resume", sessionId,
            "--output-format", "json"
        };

        return string.Join(" ", args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a));
    }

    /// <summary>
    /// Изменить статус подключения и вызвать событие
    /// </summary>
    private void ChangeStatus(ConnectionStatus newStatus)
    {
        if (Status != newStatus)
        {
            var oldStatus = Status;
            Status = newStatus;
            _logger.LogDebug("Connection status changed from {OldStatus} to {NewStatus}", oldStatus, newStatus);
            StatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs { OldStatus = oldStatus, NewStatus = newStatus });
        }
    }

    /// <summary>
    /// Очистить ресурсы процесса
    /// </summary>
    private void CleanupProcess()
    {
        if (_claudeProcess != null)
        {
            try
            {
                if (!_claudeProcess.HasExited)
                {
                    _claudeProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error killing Claude Code process");
            }

            _claudeProcess.Dispose();
            _claudeProcess = null;
        }

        _stdin?.Dispose();
        _stdout?.Dispose();
        _stdin = null;
        _stdout = null;
        _agentId = null;
        _sessionId = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (IsConnected)
        {
            try
            {
                DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during dispose");
            }
        }

        CleanupProcess();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~ClaudeCodeSubprocessConnector()
    {
        Dispose();
    }
}

/// <summary>
/// Модель ответа от Claude Code в JSON формате
/// </summary>
public class ClaudeResponse
{
    /// <summary>
    /// Тип ответа (e.g., "result", "thinking", "tool_call")
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Подтип ответа (e.g., "success", "error", "permission_denied")
    /// </summary>
    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    /// <summary>
    /// Ошибка ли это
    /// </summary>
    [JsonPropertyName("is_error")]
    public bool IsError { get; set; }

    /// <summary>
    /// Текстовый результат
    /// </summary>
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    /// <summary>
    /// ID сессии (для переиспользования через --resume)
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    /// <summary>
    /// Запросы на разрешения (для эскалации к человеку)
    /// </summary>
    [JsonPropertyName("permission_denials")]
    public List<PermissionDenial>? PermissionDenials { get; set; }

    /// <summary>
    /// Длительность выполнения в миллисекундах
    /// </summary>
    [JsonPropertyName("duration_ms")]
    public int? DurationMs { get; set; }

    /// <summary>
    /// Стоимость API вызова в USD
    /// </summary>
    [JsonPropertyName("total_cost_usd")]
    public double? TotalCostUsd { get; set; }

    /// <summary>
    /// Статистика использования (tokens, etc.)
    /// </summary>
    [JsonPropertyName("usage")]
    public Dictionary<string, object>? Usage { get; set; }
}

/// <summary>
/// Запрос на разрешение от Claude Code
/// </summary>
public class PermissionDenial
{
    /// <summary>
    /// Имя инструмента (e.g., "Bash", "Write", "Edit")
    /// </summary>
    [JsonPropertyName("tool_name")]
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// ID использования инструмента
    /// </summary>
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; set; }

    /// <summary>
    /// Входные параметры инструмента
    /// </summary>
    [JsonPropertyName("tool_input")]
    public Dictionary<string, object>? ToolInput { get; set; }
}
