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

    /// <summary>
    /// Инициализирует новый экземпляр DiagnosticsController
    /// </summary>
    /// <param name="processDiscovery">Сервис для обнаружения процессов</param>
    /// <param name="logger">Логгер</param>
    public DiagnosticsController(
        IProcessDiscoveryService processDiscovery,
        ILogger<DiagnosticsController> logger)
    {
        _processDiscovery = processDiscovery ?? throw new ArgumentNullException(nameof(processDiscovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

#endregion
