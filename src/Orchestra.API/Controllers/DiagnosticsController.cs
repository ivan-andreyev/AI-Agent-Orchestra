using MediatR;
using Microsoft.AspNetCore.Mvc;
using Orchestra.Core.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Orchestra.API.Controllers;

/// <summary>
/// Диагностический контроллер для отладки ProcessDiscoveryService и процессов Claude Code
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly IProcessDiscoveryService _processDiscovery;
    private readonly ILogger<DiagnosticsController> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Инициализирует новый экземпляр DiagnosticsController
    /// </summary>
    /// <param name="processDiscovery">Сервис для обнаружения процессов</param>
    /// <param name="logger">Логгер</param>
    /// <param name="mediator">Медиатор для отправки команд</param>
    public DiagnosticsController(
        IProcessDiscoveryService processDiscovery,
        ILogger<DiagnosticsController> logger,
        IMediator mediator)
    {
        _processDiscovery = processDiscovery ?? throw new ArgumentNullException(nameof(processDiscovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Получить детальную информацию обо всех обнаруженных процессах Claude Code
    /// </summary>
    /// <returns>Список процессов с полными деталями включая command-line и environment</returns>
    [HttpGet("processes")]
    public async Task<ActionResult<ProcessDiagnosticsResponse>> GetProcesses()
    {
        try
        {
            _logger.LogInformation("DiagnosticsController: GetProcesses called");

            var discoveredProcesses = await _processDiscovery.DiscoverClaudeProcessesAsync();

            _logger.LogInformation("Discovered {Count} Claude processes", discoveredProcesses.Count);

            var detailedProcesses = new List<DetailedProcessInfo>();

            foreach (var proc in discoveredProcesses)
            {
                try
                {
                    var process = Process.GetProcessById(proc.ProcessId);

                    var commandLine = GetCommandLine(process);
                    var environmentVars = GetEnvironmentVariables(process);

                    var detailedInfo = new DetailedProcessInfo(
                        ProcessId: proc.ProcessId,
                        ProcessName: process.ProcessName,
                        SessionId: proc.SessionId,
                        WorkingDirectory: proc.WorkingDirectory,
                        SocketPath: proc.SocketPath,
                        StartTime: proc.StartTime,
                        CommandLine: commandLine,
                        EnvironmentVariables: environmentVars,
                        IsRunning: !process.HasExited
                    );

                    detailedProcesses.Add(detailedInfo);

                    process.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get details for process {ProcessId}", proc.ProcessId);

                    // Add partial info even if we can't get full details
                    var partialInfo = new DetailedProcessInfo(
                        ProcessId: proc.ProcessId,
                        ProcessName: $"<unavailable: {ex.Message}>",
                        SessionId: proc.SessionId,
                        WorkingDirectory: proc.WorkingDirectory,
                        SocketPath: proc.SocketPath,
                        StartTime: proc.StartTime,
                        CommandLine: null,
                        EnvironmentVariables: null,
                        IsRunning: false
                    );

                    detailedProcesses.Add(partialInfo);
                }
            }

            var response = new ProcessDiagnosticsResponse(
                TotalProcessesFound: detailedProcesses.Count,
                ProcessesWithSessionId: detailedProcesses.Count(p => !string.IsNullOrEmpty(p.SessionId)),
                ProcessesWithoutSessionId: detailedProcesses.Count(p => string.IsNullOrEmpty(p.SessionId)),
                Timestamp: DateTime.UtcNow,
                Platform: RuntimeInformation.OSDescription,
                Processes: detailedProcesses
            );

            _logger.LogInformation("Returning diagnostics for {Count} processes", detailedProcesses.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get process diagnostics");
            return StatusCode(500, new { Message = "Failed to retrieve process diagnostics", Error = ex.Message });
        }
    }

    /// <summary>
    /// Очистить кэш обнаруженных процессов
    /// </summary>
    /// <returns>Результат операции</returns>
    [HttpPost("cache/clear")]
    public ActionResult ClearCache()
    {
        try
        {
            _logger.LogInformation("DiagnosticsController: ClearCache called");

            _processDiscovery.ClearCache();

            return Ok(new { Message = "Process cache cleared successfully", Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear process cache");
            return StatusCode(500, new { Message = "Failed to clear cache", Error = ex.Message });
        }
    }

    /// <summary>
    /// Получить информацию о конкретном SessionId
    /// </summary>
    /// <param name="id">SessionId для поиска</param>
    /// <returns>Детальная информация о процессе с данным SessionId</returns>
    [HttpGet("sessionid/{id}")]
    public async Task<ActionResult<SessionIdDiagnosticsResponse>> GetSessionIdInfo(string id)
    {
        try
        {
            _logger.LogInformation("DiagnosticsController: GetSessionIdInfo called for {SessionId}", id);

            var processId = await _processDiscovery.GetProcessIdForSessionAsync(id);
            var socketPath = await _processDiscovery.GetSocketPathForSessionAsync(id);
            var connectionParams = await _processDiscovery.GetConnectionParamsForAgentAsync(id);

            var response = new SessionIdDiagnosticsResponse(
                SessionId: id,
                ProcessId: processId,
                SocketPath: socketPath,
                ConnectionParams: connectionParams,
                Found: processId.HasValue || !string.IsNullOrEmpty(socketPath),
                Timestamp: DateTime.UtcNow
            );

            if (response.Found)
            {
                _logger.LogInformation("SessionId {SessionId} found: ProcessId={ProcessId}, SocketPath={SocketPath}",
                    id, processId, socketPath);
            }
            else
            {
                _logger.LogWarning("SessionId {SessionId} not found in any discovered process", id);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SessionId diagnostics for {SessionId}", id);
            return StatusCode(500, new { Message = "Failed to retrieve SessionId diagnostics", Error = ex.Message });
        }
    }

    /// <summary>
    /// Получить командную строку процесса
    /// </summary>
    private string? GetCommandLine(Process process)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: используем WMI
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}");
                using var results = searcher.Get();

                foreach (var result in results)
                {
                    return result["CommandLine"]?.ToString();
                }
            }
            else
            {
                // Unix/Linux: читаем /proc/{pid}/cmdline
                var cmdlinePath = $"/proc/{process.Id}/cmdline";
                if (System.IO.File.Exists(cmdlinePath))
                {
                    var cmdline = System.IO.File.ReadAllText(cmdlinePath);
                    return cmdline.Replace('\0', ' ').Trim();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to get command line for process {ProcessId}", process.Id);
        }

        return null;
    }

    /// <summary>
    /// Получить переменные окружения процесса
    /// </summary>
    private Dictionary<string, string>? GetEnvironmentVariables(Process process)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: WMI не предоставляет environment variables напрямую
                // Возвращаем null для Windows (требуется более сложный подход)
                return null;
            }
            else
            {
                // Unix/Linux: читаем /proc/{pid}/environ
                var environPath = $"/proc/{process.Id}/environ";
                if (System.IO.File.Exists(environPath))
                {
                    var environData = System.IO.File.ReadAllText(environPath);
                    var vars = environData.Split('\0', StringSplitOptions.RemoveEmptyEntries);

                    var result = new Dictionary<string, string>();
                    foreach (var variable in vars)
                    {
                        var parts = variable.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            result[parts[0]] = parts[1];
                        }
                    }

                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to get environment variables for process {ProcessId}", process.Id);
        }

        return null;
    }

    /// <summary>
    /// Синхронизирует SessionId для всех агентов с обнаруженными процессами Claude Code
    /// </summary>
    /// <returns>Результат синхронизации с количеством обновленных агентов</returns>
    [HttpPost("sync-session-ids")]
    public async Task<ActionResult<SessionIdSyncResponse>> SyncSessionIds()
    {
        try
        {
            _logger.LogInformation("DiagnosticsController: SyncSessionIds called");

            // Get all discovered Claude Code processes
            var discoveredProcesses = await _processDiscovery.DiscoverClaudeProcessesAsync();

            _logger.LogInformation("Found {Count} Claude Code processes for syncing", discoveredProcesses.Count);

            if (!discoveredProcesses.Any())
            {
                return Ok(new SessionIdSyncResponse(
                    TotalAgents: 0,
                    SyncedAgents: 0,
                    SkippedAgents: 0,
                    Errors: 0,
                    Timestamp: DateTime.UtcNow,
                    Details: new List<string> { "No Claude Code processes found" }));
            }

            // Get all agents
            var getAllAgentsQuery = new Orchestra.Core.Queries.Agents.GetAllAgentsQuery
            {
                ActiveOnly = true
            };
            var allAgents = await _mediator.Send(getAllAgentsQuery);

            _logger.LogInformation("Found {Count} registered agents", allAgents.Count);

            int syncedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;
            var details = new List<string>();

            // Match and update agents
            foreach (var agent in allAgents)
            {
                // Skip if SessionId already populated
                if (!string.IsNullOrWhiteSpace(agent.SessionId))
                {
                    skippedCount++;
                    details.Add($"Agent {agent.Id} already has SessionId: {agent.SessionId}");
                    continue;
                }

                // Try to match with discovered process
                var matchedProcess = TryMatchAgentWithProcess(agent, discoveredProcesses);

                if (matchedProcess != null)
                {
                    var updateCommand = new Orchestra.Core.Commands.Agents.UpdateAgentSessionIdCommand
                    {
                        AgentId = agent.Id,
                        SessionId = matchedProcess.SessionId
                    };

                    var result = await _mediator.Send(updateCommand);

                    if (result.Success)
                    {
                        syncedCount++;
                        details.Add($"Synced {agent.Id}: SessionId = {matchedProcess.SessionId}");
                        _logger.LogInformation("Synced SessionId for agent {AgentId}: {SessionId}",
                            agent.Id, matchedProcess.SessionId);
                    }
                    else
                    {
                        errorCount++;
                        details.Add($"Failed to update {agent.Id}: {result.ErrorMessage}");
                        _logger.LogWarning("Failed to update SessionId for agent {AgentId}: {Error}",
                            agent.Id, result.ErrorMessage);
                    }
                }
                else
                {
                    skippedCount++;
                    details.Add($"No matching process found for agent {agent.Id}");
                }
            }

            var response = new SessionIdSyncResponse(
                TotalAgents: allAgents.Count,
                SyncedAgents: syncedCount,
                SkippedAgents: skippedCount,
                Errors: errorCount,
                Timestamp: DateTime.UtcNow,
                Details: details);

            _logger.LogInformation("SessionId sync completed: {SyncedCount}/{TotalAgents} agents updated",
                syncedCount, allAgents.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync SessionIds: {Error}", ex.Message);
            return StatusCode(500, new SessionIdSyncResponse(
                TotalAgents: 0,
                SyncedAgents: 0,
                SkippedAgents: 0,
                Errors: 1,
                Timestamp: DateTime.UtcNow,
                Details: new List<string> { $"Error: {ex.Message}" }));
        }
    }

    /// <summary>
    /// Пытается сопоставить агента с обнаруженным процессом
    /// </summary>
    private Orchestra.Core.Services.ClaudeProcessInfo? TryMatchAgentWithProcess(
        Orchestra.Core.Data.Entities.Agent agent,
        List<Orchestra.Core.Services.ClaudeProcessInfo> discoveredProcesses)
    {
        // Try to extract ProcessId from agent's ConfigurationJson
        if (!string.IsNullOrWhiteSpace(agent.ConfigurationJson))
        {
            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(agent.ConfigurationJson);

                if (config != null && config.TryGetValue("ProcessId", out var processIdElement))
                {
                    var processId = processIdElement.GetInt32();

                    // Match by ProcessId
                    var matchedByPid = discoveredProcesses.FirstOrDefault(p => p.ProcessId == processId);
                    if (matchedByPid != null)
                    {
                        _logger.LogDebug("Matched agent {AgentId} with process PID {ProcessId}",
                            agent.Id, processId);
                        return matchedByPid;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to parse ConfigurationJson for agent {AgentId}", agent.Id);
            }
        }

        // Fallback: try to match by repository path
        if (!string.IsNullOrWhiteSpace(agent.RepositoryPath))
        {
            var matchedByPath = discoveredProcesses.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.WorkingDirectory) &&
                (p.WorkingDirectory.Equals(agent.RepositoryPath, StringComparison.OrdinalIgnoreCase) ||
                 agent.RepositoryPath.StartsWith(p.WorkingDirectory, StringComparison.OrdinalIgnoreCase)));

            if (matchedByPath != null)
            {
                _logger.LogDebug("Matched agent {AgentId} with process by repository path: {Path}",
                    agent.Id, agent.RepositoryPath);
                return matchedByPath;
            }
        }

        return null;
    }

    /// <summary>
    /// Получить текущие метрики системы в JSON формате
    /// </summary>
    /// <returns>Текущие метрики мониторинга</returns>
    [HttpGet("metrics")]
    public ActionResult<MetricsResponse> GetMetrics()
    {
        try
        {
            _logger.LogInformation("DiagnosticsController: GetMetrics called");

            var response = new MetricsResponse(
                Status: "metrics endpoint available",
                Timestamp: DateTime.UtcNow,
                MetricsEndpoint: "/metrics",
                Message: "Prometheus metrics are available at the /metrics endpoint"
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metrics information");
            return StatusCode(500, new { Message = "Failed to retrieve metrics information", Error = ex.Message });
        }
    }
}

#region Response Models

/// <summary>
/// Ответ с диагностикой процессов
/// </summary>
public record ProcessDiagnosticsResponse(
    int TotalProcessesFound,
    int ProcessesWithSessionId,
    int ProcessesWithoutSessionId,
    DateTime Timestamp,
    string Platform,
    List<DetailedProcessInfo> Processes);

/// <summary>
/// Детальная информация о процессе
/// </summary>
public record DetailedProcessInfo(
    int ProcessId,
    string ProcessName,
    string? SessionId,
    string? WorkingDirectory,
    string? SocketPath,
    DateTime StartTime,
    string? CommandLine,
    Dictionary<string, string>? EnvironmentVariables,
    bool IsRunning);

/// <summary>
/// Ответ с диагностикой SessionId
/// </summary>
public record SessionIdDiagnosticsResponse(
    string SessionId,
    int? ProcessId,
    string? SocketPath,
    Orchestra.Core.Models.AgentConnectionParams? ConnectionParams,
    bool Found,
    DateTime Timestamp);

/// <summary>
/// Результат синхронизации SessionId
/// </summary>
public record SessionIdSyncResponse(
    int TotalAgents,
    int SyncedAgents,
    int SkippedAgents,
    int Errors,
    DateTime Timestamp,
    List<string> Details);

/// <summary>
/// Ответ с информацией о метриках
/// </summary>
public record MetricsResponse(
    string Status,
    DateTime Timestamp,
    string MetricsEndpoint,
    string Message);

#endregion
