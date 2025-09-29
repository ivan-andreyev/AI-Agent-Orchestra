namespace Orchestra.Core.Services;

/// <summary>
/// Настройки для сервиса автоматического обнаружения агентов
/// </summary>
public class AgentDiscoveryOptions
{
    /// <summary>
    /// Интервал между сканированием для обнаружения новых агентов
    /// </summary>
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Задержка перед первым сканированием после запуска
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Таймаут для подключения к агентам
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Порты для поиска Claude Code агентов
    /// </summary>
    public int[] ClaudeCodePorts { get; set; } = { 3001, 3000, 8080, 55001 };

    /// <summary>
    /// Включить сканирование процессов
    /// </summary>
    public bool EnableProcessScanning { get; set; } = true;

    /// <summary>
    /// Имена процессов для сканирования
    /// </summary>
    public string[] ProcessNamesToScan { get; set; } = { "claude", "claude-desktop", "code", "cursor" };

    /// <summary>
    /// Максимальное количество агентов для обнаружения
    /// </summary>
    public int MaxAgentsToDiscover { get; set; } = 10;

    /// <summary>
    /// Включено ли автоматическое обнаружение агентов
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Автоматически регистрировать найденных агентов
    /// </summary>
    public bool AutoRegister { get; set; } = true;

    /// <summary>
    /// Репозитории для сканирования
    /// </summary>
    public string[] ScanRepositories { get; set; } = { "." };

    /// <summary>
    /// Включить сканирование HTTP API для поиска Claude Code
    /// </summary>
    public bool EnableHttpScanning { get; set; } = true;
}