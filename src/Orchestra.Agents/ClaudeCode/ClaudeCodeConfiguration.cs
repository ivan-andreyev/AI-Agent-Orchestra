using Orchestra.Core.Services;

namespace Orchestra.Agents.ClaudeCode;

/// <summary>
/// Конфигурация для агента Claude Code с настройками CLI и поведения
/// </summary>
public class ClaudeCodeConfiguration : BaseAgentConfiguration
{
    /// <inheritdoc />
    public override string AgentType => "claude-code";
    /// <summary>
    /// Путь к исполняемому файлу Claude Code CLI
    /// </summary>
    public string DefaultCliPath { get; set; } = @"C:\Users\mrred\AppData\Roaming\npm\claude.cmd";

    /// <summary>
    /// Таймаут по умолчанию для выполнения команд
    /// </summary>
    public new TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(10);

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
    public new int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Задержка между попытками повтора
    /// </summary>
    public new TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Максимальное количество одновременных выполнений команд
    /// </summary>
    public new int MaxConcurrentExecutions { get; set; } = 3;

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

    /// <summary>
    /// Валидирует специфичные для Claude Code свойства
    /// </summary>
    /// <param name="errors">Список для добавления ошибок валидации</param>
    protected override void ValidateSpecificProperties(List<string> errors)
    {
        // Валидация DefaultCliPath
        if (string.IsNullOrWhiteSpace(DefaultCliPath))
        {
            errors.Add("DefaultCliPath cannot be empty");
        }
        else if (!File.Exists(DefaultCliPath))
        {
            errors.Add($"DefaultCliPath does not exist: {DefaultCliPath}");
        }

        // Валидация MaxCommandLength
        if (MaxCommandLength < 100 || MaxCommandLength > 1_000_000)
        {
            errors.Add($"MaxCommandLength must be between 100 and 1,000,000, got {MaxCommandLength}");
        }

        // Валидация WorkflowTimeout
        if (WorkflowTimeout < TimeSpan.FromMinutes(1))
        {
            errors.Add($"WorkflowTimeout must be at least 1 minute, got {WorkflowTimeout.TotalMinutes} minutes");
        }

        // Валидация WarmupDelayMs
        if (WarmupDelayMs < 0 || WarmupDelayMs > 60000)
        {
            errors.Add($"WarmupDelayMs must be between 0 and 60000, got {WarmupDelayMs}");
        }

        // Валидация HealthCheckInterval
        if (HealthCheckInterval < TimeSpan.FromMinutes(1))
        {
            errors.Add($"HealthCheckInterval must be at least 1 minute, got {HealthCheckInterval.TotalMinutes} minutes");
        }

        // Валидация ProcessCleanupTimeout
        if (ProcessCleanupTimeout < TimeSpan.FromSeconds(1))
        {
            errors.Add($"ProcessCleanupTimeout must be at least 1 second, got {ProcessCleanupTimeout.TotalSeconds} seconds");
        }
    }
}