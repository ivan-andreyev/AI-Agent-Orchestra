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
    /// Извлекает Session ID из .claude/projects/{ProjectPath}/{SessionId}.jsonl файлов
    /// </summary>
    private string? ExtractSessionId(string commandLine, Process process)
    {
        // Сначала пробуем старую стратегию: UUID в command-line (может быть полезно для других случаев)
        var uuidPattern = @"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}";
        var match = Regex.Match(commandLine, uuidPattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Value;
        }

        // Новая стратегия: извлекаем SessionId из .claude/projects структуры
        try
        {
            var workingDirectory = GetProcessWorkingDirectory(process);
            if (string.IsNullOrEmpty(workingDirectory))
            {
                return null;
            }

            return ExtractSessionIdFromClaudeProjects(workingDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to extract SessionId from .claude/projects for process {ProcessId}", process.Id);
            return null;
        }
    }

    /// <summary>
    /// Извлекает SessionId из .claude/projects/{ProjectPath}/{SessionId}.jsonl
    /// </summary>
    private string? ExtractSessionIdFromClaudeProjects(string projectPath)
    {
        try
        {
            // Получаем путь к .claude/projects
            var claudeProjectsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude",
                "projects");

            if (!Directory.Exists(claudeProjectsPath))
            {
                return null;
            }

            // Кодируем projectPath: C:\Users\... → C--Users-...
            var encodedProjectPath = projectPath
                .Replace(":", "--")
                .Replace(Path.DirectorySeparatorChar, '-')
                .Replace(Path.AltDirectorySeparatorChar, '-');

            var projectClaudeDir = Path.Combine(claudeProjectsPath, encodedProjectPath);

            if (!Directory.Exists(projectClaudeDir))
            {
                return null;
            }

            // Ищем .jsonl файлы
            var jsonlFiles = Directory.GetFiles(projectClaudeDir, "*.jsonl");
            if (jsonlFiles.Length == 0)
            {
                return null;
            }

            // Берём самый свежий файл по времени модификации
            var latestFile = jsonlFiles
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestFile == null)
            {
                return null;
            }

            // SessionId = имя файла без расширения
            var sessionId = Path.GetFileNameWithoutExtension(latestFile.Name);

            // Проверяем, что это валидный UUID
            var uuidPattern = @"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$";
            if (Regex.IsMatch(sessionId, uuidPattern, RegexOptions.IgnoreCase))
            {
                return sessionId;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to extract SessionId from .claude/projects for path {ProjectPath}", projectPath);
            return null;
        }
    }

    /// <summary>
    /// Получает Current Working Directory (CWD) процесса через поиск в .claude/projects
    /// Упрощённая версия: возвращает проект с самым свежим .jsonl файлом
    /// </summary>
    private string? GetProcessWorkingDirectory(Process process)
    {
        try
        {
            // Стратегия: находим САМЫЙ СВЕЖИЙ .jsonl файл во ВСЕХ проектах
            // Это работает если у нас одна активная сессия Claude Code
            var claudeProjectsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude",
                "projects");

            if (!Directory.Exists(claudeProjectsPath))
            {
                return null;
            }

            // Получаем все проектные директории
            var projectDirs = Directory.GetDirectories(claudeProjectsPath);

            FileInfo? latestJsonlFile = null;
            string? latestProjectDir = null;

            foreach (var projectDir in projectDirs)
            {
                var jsonlFiles = Directory.GetFiles(projectDir, "*.jsonl");
                if (jsonlFiles.Length == 0)
                {
                    continue;
                }

                foreach (var jsonlFile in jsonlFiles)
                {
                    var fileInfo = new FileInfo(jsonlFile);
                    if (latestJsonlFile == null || fileInfo.LastWriteTimeUtc > latestJsonlFile.LastWriteTimeUtc)
                    {
                        latestJsonlFile = fileInfo;
                        latestProjectDir = projectDir;
                    }
                }
            }

            if (latestProjectDir != null)
            {
                // Декодируем путь из имени директории
                var dirName = Path.GetFileName(latestProjectDir);
                return DecodeClaudeProjectPath(dirName);
            }

            // Fallback: Unix/Linux /proc/{pid}/cwd
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var cwdPath = $"/proc/{process.Id}/cwd";
                if (Directory.Exists(cwdPath))
                {
                    var target = Directory.ResolveLinkTarget(cwdPath, false);
                    if (target != null)
                    {
                        return target.FullName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to get working directory for process {ProcessId}", process.Id);
        }

        return null;
    }

    /// <summary>
    /// Декодирует путь проекта из имени директории .claude/projects
    /// Пример: C--Users-mrred-RiderProjects-AI-Agent-Orchestra → C:\Users\mrred\RiderProjects\AI-Agent-Orchestra
    /// </summary>
    private string DecodeClaudeProjectPath(string encodedPath)
    {
        // Обработка Windows путей: C--Users-...
        if (encodedPath.Length >= 3 && char.IsLetter(encodedPath[0]) && encodedPath.Substring(1, 2) == "--")
        {
            // C--Users-mrred → C:\Users\mrred
            var driveLetter = encodedPath[0];
            var restPath = encodedPath.Substring(3).Replace('-', '\\');
            return $"{driveLetter}:\\{restPath}";
        }

        // Unix пути начинаются с --
        if (encodedPath.StartsWith("--"))
        {
            return "/" + encodedPath.Substring(2).Replace('-', '/');
        }

        // Fallback: просто замена дефисов
        return encodedPath.Replace('-', Path.DirectorySeparatorChar);
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
