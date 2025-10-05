namespace Orchestra.Core.Services;

/// <summary>
/// Конфигурация для агента Shell Executor с настройками командной оболочки
/// </summary>
/// <remarks>
/// <para>
/// Shell Executor позволяет выполнять команды через системную оболочку
/// (PowerShell на Windows, bash на Linux/macOS) для автоматизации задач.
/// </para>
/// <para>
/// <b>Регистрация в DI:</b>
/// <code>
/// services.Configure&lt;ShellAgentConfiguration&gt;(configuration.GetSection("ShellAgent"));
/// services.AddSingleton&lt;IAgentConfiguration&gt;(provider =>
///     provider.GetRequiredService&lt;IOptions&lt;ShellAgentConfiguration&gt;&gt;().Value);
/// </code>
/// </para>
/// </remarks>
public class ShellAgentConfiguration : BaseAgentConfiguration
{
    /// <inheritdoc />
    public override string AgentType => "shell";

    /// <inheritdoc />
    public override int MaxConcurrentExecutions { get; set; } = 5;

    /// <inheritdoc />
    public override TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public override int RetryAttempts { get; set; } = 2;

    /// <inheritdoc />
    public override TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Путь к исполняемому файлу оболочки
    /// </summary>
    /// <value>
    /// По умолчанию:
    /// <list type="bullet">
    /// <item><description>Windows: "powershell.exe"</description></item>
    /// <item><description>Linux/macOS: "/bin/bash"</description></item>
    /// </list>
    /// </value>
    public string ShellExecutablePath { get; set; } =
        OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/bash";

    /// <summary>
    /// Аргументы командной строки для запуска оболочки
    /// </summary>
    /// <value>
    /// По умолчанию:
    /// <list type="bullet">
    /// <item><description>Windows PowerShell: "-NoProfile -ExecutionPolicy Bypass -Command"</description></item>
    /// <item><description>Linux/macOS bash: "-c"</description></item>
    /// </list>
    /// </value>
    public string ShellArguments { get; set; } =
        OperatingSystem.IsWindows() ? "-NoProfile -ExecutionPolicy Bypass -Command" : "-c";

    /// <summary>
    /// Рабочая директория по умолчанию для выполнения команд
    /// </summary>
    public string? DefaultWorkingDirectory { get; set; }

    /// <summary>
    /// Переменные окружения, добавляемые при выполнении команд
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Разрешенные команды (белый список). Null означает разрешены все команды.
    /// </summary>
    /// <remarks>
    /// Используется для безопасности - ограничивает выполняемые команды.
    /// Пример: new[] { "git", "npm", "dotnet", "ls", "dir" }
    /// </remarks>
    public string[]? AllowedCommands { get; set; }

    /// <summary>
    /// Запрещенные команды (черный список)
    /// </summary>
    /// <remarks>
    /// Команды, которые нельзя выполнять из соображений безопасности.
    /// Пример: new[] { "rm", "del", "format", "shutdown" }
    /// </remarks>
    public string[] BlockedCommands { get; set; } =
        OperatingSystem.IsWindows()
            ? new[] { "format", "shutdown", "del", "rmdir" }
            : new[] { "rm", "sudo", "shutdown", "reboot" };

    /// <summary>
    /// Максимальная длина вывода команды в символах
    /// </summary>
    public int MaxOutputLength { get; set; } = 100_000;

    /// <summary>
    /// Включить расширенное логирование для отладки
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// Захватывать stderr как часть вывода
    /// </summary>
    public bool CaptureStdError { get; set; } = true;

    /// <summary>
    /// Убивать процесс при таймауте
    /// </summary>
    public bool KillOnTimeout { get; set; } = true;

    /// <summary>
    /// Валидирует специфичные для Shell агента свойства
    /// </summary>
    /// <param name="errors">Список для добавления ошибок валидации</param>
    protected override void ValidateSpecificProperties(List<string> errors)
    {
        // Валидация ShellExecutablePath
        if (string.IsNullOrWhiteSpace(ShellExecutablePath))
        {
            errors.Add("ShellExecutablePath cannot be empty");
        }

        // Валидация MaxOutputLength
        if (MaxOutputLength < 1000 || MaxOutputLength > 10_000_000)
        {
            errors.Add($"MaxOutputLength must be between 1,000 and 10,000,000, got {MaxOutputLength}");
        }

        // Валидация DefaultWorkingDirectory
        if (!string.IsNullOrWhiteSpace(DefaultWorkingDirectory) && !Directory.Exists(DefaultWorkingDirectory))
        {
            errors.Add($"DefaultWorkingDirectory does not exist: {DefaultWorkingDirectory}");
        }

        // Валидация AllowedCommands и BlockedCommands
        if (AllowedCommands != null && BlockedCommands != null)
        {
            var intersection = AllowedCommands.Intersect(BlockedCommands, StringComparer.OrdinalIgnoreCase).ToArray();
            if (intersection.Length > 0)
            {
                errors.Add($"Commands cannot be both allowed and blocked: {string.Join(", ", intersection)}");
            }
        }
    }
}
