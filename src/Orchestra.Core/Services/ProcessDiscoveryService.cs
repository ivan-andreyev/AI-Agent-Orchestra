using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Orchestra.Core.Services;

/// <summary>
/// Сервис для обнаружения запущенных процессов Claude Code
/// </summary>
public class ProcessDiscoveryService : IProcessDiscoveryService
{
    private readonly ILogger<ProcessDiscoveryService> _logger;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(2);
    private const string CacheKey = "ClaudeProcesses";

    public ProcessDiscoveryService(
        ILogger<ProcessDiscoveryService> logger,
        IMemoryCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public async Task<int?> GetProcessIdForSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var processes = await DiscoverClaudeProcessesAsync();
        var process = processes.FirstOrDefault(p => p.SessionId == sessionId);

        return process?.ProcessId;
    }

    /// <inheritdoc />
    public async Task<string?> GetSocketPathForSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var processes = await DiscoverClaudeProcessesAsync();
        var process = processes.FirstOrDefault(p => p.SessionId == sessionId);

        return process?.SocketPath;
    }

    /// <inheritdoc />
    public async Task<List<ClaudeProcessInfo>> DiscoverClaudeProcessesAsync()
    {
        // Check cache first
        if (_cache.TryGetValue<List<ClaudeProcessInfo>>(CacheKey, out var cachedProcesses))
        {
            _logger.LogDebug("Returning {Count} cached Claude processes", cachedProcesses?.Count ?? 0);
            return cachedProcesses ?? new List<ClaudeProcessInfo>();
        }

        _logger.LogInformation("Discovering Claude Code processes...");

        var discoveredProcesses = new List<ClaudeProcessInfo>();

        try
        {
            // Ищем процессы Node.js (Claude Code работает на Node.js)
            var allProcesses = Process.GetProcesses();

            foreach (var process in allProcesses)
            {
                try
                {
                    // Ищем процессы node.exe/node (Claude CLI запускается через Node.js)
                    if (IsClaudeCodeProcess(process))
                    {
                        var processInfo = await ExtractProcessInfoAsync(process);
                        if (processInfo != null)
                        {
                            discoveredProcesses.Add(processInfo);
                            _logger.LogDebug(
                                "Found Claude process: PID={ProcessId}, SessionId={SessionId}, WorkDir={WorkDir}",
                                processInfo.ProcessId,
                                processInfo.SessionId,
                                processInfo.WorkingDirectory);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Access denied or process already exited - skip
                    _logger.LogTrace(ex, "Could not access process {ProcessId}", process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }

            _logger.LogInformation("Discovered {Count} Claude Code processes", discoveredProcesses.Count);

            // Cache results
            _cache.Set(CacheKey, discoveredProcesses, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheExpiration,
                Size = 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover Claude processes");
        }

        return discoveredProcesses;
    }

    /// <inheritdoc />
    public async Task<AgentConnectionParams?> GetConnectionParamsForAgentAsync(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return null;
        }

        _logger.LogDebug("Getting connection params for agent {AgentId}", agentId);

        // Try to find process by SessionId
        var processId = await GetProcessIdForSessionAsync(agentId);
        var socketPath = await GetSocketPathForSessionAsync(agentId);

        if (processId == null && socketPath == null)
        {
            _logger.LogWarning("No process found for agent {AgentId}", agentId);
            return null;
        }

        // Create connection params based on platform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: Use ProcessId or Named Pipe
            return new AgentConnectionParams
            {
                ConnectorType = "terminal",
                ProcessId = processId,
                PipeName = socketPath, // On Windows, SocketPath might be PipeName
                ConnectionTimeoutSeconds = 30
            };
        }
        else
        {
            // Unix/Linux/Mac: Use Unix Domain Socket
            return new AgentConnectionParams
            {
                ConnectorType = "terminal",
                ProcessId = processId,
                SocketPath = socketPath,
                ConnectionTimeoutSeconds = 30
            };
        }
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        _cache.Remove(CacheKey);
        _logger.LogDebug("Process cache cleared");
    }

    /// <summary>
    /// Проверяет, является ли процесс процессом Claude Code
    /// </summary>
    private bool IsClaudeCodeProcess(Process process)
    {
        try
        {
            var processName = process.ProcessName.ToLowerInvariant();

            // Ищем node.exe/node или claude.cmd/claude
            if (processName.Contains("node") || processName.Contains("claude"))
            {
                // Дополнительная проверка по командной строке
                var commandLine = GetCommandLine(process);
                if (!string.IsNullOrEmpty(commandLine))
                {
                    // Claude Code CLI обычно содержит "@anthropic-ai/claude-code" или путь к claude
                    return commandLine.Contains("claude-code") ||
                           commandLine.Contains("@anthropic-ai/claude") ||
                           commandLine.Contains(@"\npm\claude") ||
                           commandLine.Contains("/npm/claude");
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    /// <summary>
    /// Извлекает информацию из процесса Claude Code
    /// </summary>
    private async Task<ClaudeProcessInfo?> ExtractProcessInfoAsync(Process process)
    {
        try
        {
            var commandLine = GetCommandLine(process);
            if (string.IsNullOrEmpty(commandLine))
            {
                return null;
            }

            // Попытка извлечь SessionId из командной строки или переменных окружения
            var sessionId = ExtractSessionId(commandLine, process);
            var workingDirectory = ExtractWorkingDirectory(process);
            var socketPath = ExtractSocketPath(commandLine, process);

            return new ClaudeProcessInfo(
                ProcessId: process.Id,
                SessionId: sessionId,
                WorkingDirectory: workingDirectory,
                SocketPath: socketPath,
                StartTime: process.StartTime);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to extract info from process {ProcessId}", process.Id);
            return null;
        }
    }

    /// <summary>
    /// Получает командную строку процесса
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
                if (File.Exists(cmdlinePath))
                {
                    var cmdline = File.ReadAllText(cmdlinePath);
                    return cmdline.Replace('\0', ' ').Trim();
                }
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    /// <summary>
    /// Извлекает Session ID из командной строки или переменных окружения
    /// </summary>
    private string? ExtractSessionId(string commandLine, Process process)
    {
        // Session ID обычно передается как параметр или находится в рабочей директории
        // Паттерн: UUID вида "83673153-5336-48e5-ae7a-85cdaca2da91"
        var uuidPattern = @"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}";
        var match = Regex.Match(commandLine, uuidPattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Value;
        }

        // Fallback: попробуем из working directory
        try
        {
            var workDir = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(workDir))
            {
                match = Regex.Match(workDir, uuidPattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Value;
                }
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    /// <summary>
    /// Извлекает рабочую директорию процесса
    /// </summary>
    private string? ExtractWorkingDirectory(Process process)
    {
        try
        {
            // Попытка получить working directory
            return process.MainModule?.FileName != null
                ? Path.GetDirectoryName(process.MainModule.FileName)
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Извлекает путь к Unix socket или Named Pipe из командной строки
    /// </summary>
    private string? ExtractSocketPath(string commandLine, Process process)
    {
        // Обычно socket path передается как параметр --socket или находится в temp
        var socketMatch = Regex.Match(commandLine, @"--socket[= ]([^\s]+)", RegexOptions.IgnoreCase);
        if (socketMatch.Success)
        {
            return socketMatch.Groups[1].Value;
        }

        // Fallback: default socket paths
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows Named Pipe
            return $@"\\.\pipe\claude_{process.Id}";
        }
        else
        {
            // Unix Domain Socket
            return $"/tmp/claude_{process.Id}.sock";
        }
    }
}
