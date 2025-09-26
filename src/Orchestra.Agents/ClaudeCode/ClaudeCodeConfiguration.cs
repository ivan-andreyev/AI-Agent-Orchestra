namespace Orchestra.Agents.ClaudeCode;

/// <summary>
/// Конфигурация для агента Claude Code с настройками CLI и поведения
/// </summary>
public class ClaudeCodeConfiguration
{
    /// <summary>
    /// Путь к исполняемому файлу Claude Code CLI
    /// </summary>
    public string DefaultCliPath { get; set; } = @"C:\Users\mrred\AppData\Roaming\npm\claude.cmd";

    /// <summary>
    /// Таймаут по умолчанию для выполнения команд
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Разрешенные инструменты для Claude Code агентов
    /// </summary>
    public string[] AllowedTools { get; set; } = { "Bash", "Read", "Write", "Edit", "Glob", "Grep" };

    /// <summary>
    /// Формат выходных данных Claude Code CLI
    /// </summary>
    public string OutputFormat { get; set; } = "text";

    /// <summary>
    /// Максимальная длина команды в символах
    /// </summary>
    public int MaxCommandLength { get; set; } = 5000;

    /// <summary>
    /// Включить расширенное логирование для отладки
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// Рабочая директория по умолчанию, если не указана
    /// </summary>
    public string? DefaultWorkingDirectory { get; set; }

    /// <summary>
    /// Дополнительные параметры командной строки для Claude CLI
    /// </summary>
    public Dictionary<string, string> AdditionalCliParameters { get; set; } = new();

    /// <summary>
    /// Количество попыток повтора при сбоях выполнения
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Задержка между попытками повтора
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Максимальное количество одновременных выполнений команд
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = 3;

    /// <summary>
    /// Таймаут для выполнения workflow'ов
    /// </summary>
    public TimeSpan WorkflowTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Включить предварительный прогрев агентов при запуске
    /// </summary>
    public bool WarmupEnabled { get; set; } = true;

    /// <summary>
    /// Задержка в миллисекундах перед прогревом агентов
    /// </summary>
    public int WarmupDelayMs { get; set; } = 5000;

    /// <summary>
    /// Интервал для проверки состояния агентов
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Таймаут для очистки процессов Claude Code CLI
    /// </summary>
    public TimeSpan ProcessCleanupTimeout { get; set; } = TimeSpan.FromSeconds(30);
}