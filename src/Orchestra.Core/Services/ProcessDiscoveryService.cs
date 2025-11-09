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

        // Find all processes with matching SessionId
        var matchingProcesses = processes.Where(p => p.SessionId == sessionId).ToList();

        if (matchingProcesses.Count == 0)
        {
            return null;
        }

        // If multiple processes have same SessionId, pick the most recently started one
        if (matchingProcesses.Count > 1)
        {
            _logger.LogWarning(
                "Multiple processes found for SessionId {SessionId}: {ProcessIds}. Selecting most recent.",
                sessionId,
                string.Join(", ", matchingProcesses.Select(p => p.ProcessId)));

            return matchingProcesses.OrderByDescending(p => p.StartTime).First().ProcessId;
        }

        return matchingProcesses[0].ProcessId;
    }

    /// <inheritdoc />
    public async Task<string?> GetSocketPathForSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var processes = await DiscoverClaudeProcessesAsync();

        // Find all processes with matching SessionId
        var matchingProcesses = processes.Where(p => p.SessionId == sessionId).ToList();

        if (matchingProcesses.Count == 0)
        {
            return null;
        }

        // If multiple processes have same SessionId, pick the most recently started one
        if (matchingProcesses.Count > 1)
        {
            _logger.LogWarning(
                "Multiple processes found for SessionId {SessionId}: {ProcessIds}. Selecting most recent for SocketPath.",
                sessionId,
                string.Join(", ", matchingProcesses.Select(p => p.ProcessId)));

            return matchingProcesses.OrderByDescending(p => p.StartTime).First().SocketPath;
        }

        return matchingProcesses[0].SocketPath;
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
            var workingDirectory = GetProcessWorkingDirectory(process);
            var sessionId = ExtractSessionId(commandLine, process);
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

            // НОВАЯ СТРАТЕГИЯ: Вместо кодирования projectPath, проходим по всем директориям в .claude/projects
            // и декодируем их имена, затем сравниваем с projectPath
            // Это избегает проблем с обратным кодированием

            var projectDirs = Directory.GetDirectories(claudeProjectsPath);

            foreach (var projectDir in projectDirs)
            {
                var encodedDirName = Path.GetFileName(projectDir);
                var decodedPath = DecodeClaudeProjectPath(encodedDirName);

                // Сравниваем decoded path с projectPath (case-insensitive для Windows)
                if (string.Equals(decodedPath, projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Нашли соответствующую директорию! Извлекаем SessionId

                    // Ищем .jsonl файлы
                    var jsonlFiles = Directory.GetFiles(projectDir, "*.jsonl");
                    if (jsonlFiles.Length == 0)
                    {
                        continue;
                    }

                    // Берём самый свежий файл по времени модификации
                    var latestFile = jsonlFiles
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(fi => fi.LastWriteTimeUtc)
                        .FirstOrDefault();

                    if (latestFile == null)
                    {
                        continue;
                    }

                    // SessionId = имя файла без расширения
                    var sessionId = Path.GetFileNameWithoutExtension(latestFile.Name);

                    // Проверяем, что это валидный UUID
                    var uuidPattern = @"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$";
                    if (Regex.IsMatch(sessionId, uuidPattern, RegexOptions.IgnoreCase))
                    {
                        _logger.LogTrace(
                            "Matched project path {ProjectPath} to .claude/projects dir {EncodedDirName}, extracted SessionId: {SessionId}",
                            projectPath,
                            encodedDirName,
                            sessionId);

                        return sessionId;
                    }
                }
            }

            // Не нашли соответствующую директорию
            _logger.LogTrace("No matching .claude/projects directory found for path {ProjectPath}", projectPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to extract SessionId from .claude/projects for path {ProjectPath}", projectPath);
            return null;
        }
    }

    /// <summary>
    /// Получает Current Working Directory (CWD) процесса используя WMI (Windows) или /proc (Unix)
    /// </summary>
    private string? GetProcessWorkingDirectory(Process process)
    {
        try
        {
            // СТРАТЕГИЯ 1: Получаем реальный CWD из процесса через WMI (Windows) или /proc (Unix)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: используем WMI для получения environment variables
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT * FROM Win32_Process WHERE ProcessId = {process.Id}");
                using var results = searcher.Get();

                foreach (var obj in results)
                {
                    using var mo = (System.Management.ManagementObject)obj;

                    // Пытаемся вызвать метод GetOwner для проверки доступа
                    // Если нет доступа, WMI не даст получить environment variables

                    // Вместо environment variables используем ExecutablePath как подсказку
                    // Но для node.exe это будет "C:\Program Files\nodejs\node.exe", что не помогает

                    // АЛЬТЕРНАТИВНЫЙ ПОДХОД: Используем CommandLine для поиска рабочей директории
                    var commandLine = mo["CommandLine"]?.ToString();
                    if (!string.IsNullOrEmpty(commandLine))
                    {
                        // Для Claude Code процессов парсим command line
                        // Ожидаем: "C:\Program Files\nodejs\node.exe" C:\Users\...\cli.js [--resume]
                        // К сожалению, command line НЕ содержит рабочую директорию напрямую

                        // Поэтому используем СТРАТЕГИЮ 2: Matching by process start time
                        _logger.LogTrace("Process {ProcessId} CommandLine: {CommandLine}", process.Id, commandLine);
                    }
                }
            }
            else
            {
                // Unix/Linux: используем /proc/{pid}/cwd
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

            // СТРАТЕГИЯ 2: Matching by process start time with .jsonl creation/modification time
            // Находим .jsonl файлы, которые были созданы/модифицированы близко ко времени старта процесса
            var processStartTime = process.StartTime;

            var claudeProjectsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude",
                "projects");

            if (!Directory.Exists(claudeProjectsPath))
            {
                return null;
            }

            var projectDirs = Directory.GetDirectories(claudeProjectsPath);

            // Ищем .jsonl файлы, у которых LastWriteTime близок к processStartTime
            // Допускаем погрешность в 5 минут (процесс мог стартовать раньше/позже записи в .jsonl)
            var timeTolerance = TimeSpan.FromMinutes(5);

            FileInfo? bestMatchFile = null;
            string? bestMatchProjectDir = null;
            TimeSpan bestMatchDifference = TimeSpan.MaxValue;

            foreach (var projectDir in projectDirs)
            {
                var jsonlFiles = Directory.GetFiles(projectDir, "*.jsonl");
                foreach (var jsonlFile in jsonlFiles)
                {
                    var fileInfo = new FileInfo(jsonlFile);
                    var timeDifference = (fileInfo.LastWriteTime - processStartTime).Duration();

                    // Проверяем CreationTime и LastWriteTime
                    var creationTimeDiff = (fileInfo.CreationTime - processStartTime).Duration();
                    var minDifference = timeDifference < creationTimeDiff ? timeDifference : creationTimeDiff;

                    if (minDifference < bestMatchDifference && minDifference < timeTolerance)
                    {
                        bestMatchDifference = minDifference;
                        bestMatchFile = fileInfo;
                        bestMatchProjectDir = projectDir;
                    }
                }
            }

            if (bestMatchProjectDir != null)
            {
                var dirName = Path.GetFileName(bestMatchProjectDir);
                var decodedPath = DecodeClaudeProjectPath(dirName);

                _logger.LogTrace(
                    "Matched process {ProcessId} (started {StartTime}) to project {ProjectDir} (time diff: {TimeDiff}ms)",
                    process.Id,
                    processStartTime,
                    decodedPath,
                    bestMatchDifference.TotalMilliseconds);

                return decodedPath;
            }

            // СТРАТЕГИЯ 3 (fallback): Возвращаем проект с самым свежим .jsonl файлом
            // Это работает если у нас одна активная сессия
            _logger.LogTrace("Could not match process {ProcessId} by start time, using latest .jsonl fallback", process.Id);

            FileInfo? latestJsonlFile = null;
            string? latestProjectDir = null;

            foreach (var projectDir in projectDirs)
            {
                var jsonlFiles = Directory.GetFiles(projectDir, "*.jsonl");
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
                var dirName = Path.GetFileName(latestProjectDir);
                return DecodeClaudeProjectPath(dirName);
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
    /// ПРОБЛЕМА: Не может надёжно различить дефисы-разделители от дефисов в названиях директорий.
    /// РЕШЕНИЕ: Пробуем несколько вариантов декодирования, начиная с наиболее вероятных.
    /// </summary>
    private string DecodeClaudeProjectPath(string encodedPath)
    {
        // Обработка Windows путей: C--Users-...
        if (encodedPath.Length >= 3 && char.IsLetter(encodedPath[0]) && encodedPath.Substring(1, 2) == "--")
        {
            var driveLetter = encodedPath[0];
            var pathPart = encodedPath.Substring(3); // Убираем "C--"

            // Стратегия: пробуем разные варианты декодирования
            // Обычно названия проектов (с дефисами) находятся в конце пути

            var segments = pathPart.Split('-');

            // Вариант 1: Пробуем сгруппировать последние 3 сегмента (например: AI-Agent-Orchestra)
            if (segments.Length >= 4)
            {
                var decoded = TryDecodeWithLastNSegmentsGrouped(driveLetter, segments, 3);
                if (decoded != null)
                {
                    return decoded;
                }
            }

            // Вариант 2: Пробуем сгруппировать последние 2 сегмента (например: AI-Agent)
            if (segments.Length >= 3)
            {
                var decoded = TryDecodeWithLastNSegmentsGrouped(driveLetter, segments, 2);
                if (decoded != null)
                {
                    return decoded;
                }
            }

            // Вариант 3: Пробуем без группировки (для простых путей без дефисов в названии)
            if (segments.Length >= 2)
            {
                var decoded = TryDecodeWithLastNSegmentsGrouped(driveLetter, segments, 1);
                if (decoded != null)
                {
                    return decoded;
                }
            }

            // Вариант 4 (fallback): заменяем все дефисы на разделители
            var restPath = pathPart.Replace('-', '\\');
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
    /// Пробует декодировать путь, группируя последние N сегментов вместе (для поддержки дефисов в названиях проектов)
    /// </summary>
    private string? TryDecodeWithLastNSegmentsGrouped(char driveLetter, string[] segments, int lastNToGroup)
    {
        if (segments.Length < lastNToGroup + 1)
        {
            return null;
        }

        // Группируем последние N сегментов
        var normalSegments = segments.Take(segments.Length - lastNToGroup);
        var groupedSegment = string.Join("-", segments.Skip(segments.Length - lastNToGroup));

        var pathParts = normalSegments.Append(groupedSegment);
        var decodedPath = $"{driveLetter}:\\" + string.Join("\\", pathParts);

        // Проверяем что путь выглядит реалистично:
        // Должны существовать хотя бы первые 4 уровня (например: C:\Users\mrred\RiderProjects)
        // (сам projectPath может не существовать, если это старая сессия)
        try
        {
            var pathSegments = decodedPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Для Windows путей: C: \ Users \ mrred \ RiderProjects должны существовать
            if (pathSegments.Length >= 4)
            {
                // Проверяем 4 уровня: C:\Users\mrred\RiderProjects
                var checkPath = $"{pathSegments[0]}\\{pathSegments[1]}\\{pathSegments[2]}\\{pathSegments[3]}";
                if (Directory.Exists(checkPath))
                {
                    return decodedPath;
                }
            }
            else if (pathSegments.Length == 3)
            {
                // Fallback для коротких путей: проверяем 3 уровня
                var checkPath = $"{pathSegments[0]}\\{pathSegments[1]}\\{pathSegments[2]}";
                if (Directory.Exists(checkPath))
                {
                    return decodedPath;
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
            // Windows Named Pipe (without \\.\pipe\ prefix - NamedPipeClientStream adds it automatically)
            return $"claude_{process.Id}";
        }
        else
        {
            // Unix Domain Socket
            return $"/tmp/claude_{process.Id}.sock";
        }
    }
}
