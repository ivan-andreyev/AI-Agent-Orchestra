using Orchestra.Core.Models;

namespace Orchestra.Core.Services;

/// <summary>
/// Сервис для обнаружения процессов Claude Code и определения параметров подключения
/// </summary>
public interface IProcessDiscoveryService
{
    /// <summary>
    /// Находит ProcessId для указанного Session ID
    /// </summary>
    /// <param name="sessionId">Session ID агента из .jsonl файла</param>
    /// <returns>ProcessId если найден, иначе null</returns>
    Task<int?> GetProcessIdForSessionAsync(string sessionId);

    /// <summary>
    /// Находит путь к Unix Domain Socket для указанного Session ID
    /// </summary>
    /// <param name="sessionId">Session ID агента</param>
    /// <returns>Путь к сокету если найден, иначе null</returns>
    Task<string?> GetSocketPathForSessionAsync(string sessionId);

    /// <summary>
    /// Находит все процессы Claude Code, запущенные в системе
    /// </summary>
    /// <returns>Список процессов с параметрами подключения</returns>
    Task<List<ClaudeProcessInfo>> DiscoverClaudeProcessesAsync();

    /// <summary>
    /// Определяет параметры подключения для указанного агента
    /// </summary>
    /// <param name="agentId">Идентификатор агента (SessionId)</param>
    /// <returns>Параметры подключения или null если процесс не найден</returns>
    Task<AgentConnectionParams?> GetConnectionParamsForAgentAsync(string agentId);

    /// <summary>
    /// Очищает кэш обнаруженных процессов
    /// </summary>
    void ClearCache();
}

/// <summary>
/// Информация о процессе Claude Code
/// </summary>
public record ClaudeProcessInfo(
    int ProcessId,
    string? SessionId,
    string? WorkingDirectory,
    string? SocketPath,
    DateTime StartTime);
