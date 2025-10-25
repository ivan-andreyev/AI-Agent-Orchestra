namespace Orchestra.Core.Services.Connectors;

/// <summary>
/// Настройки для TerminalAgentConnector
/// </summary>
/// <remarks>
/// <para>
/// Конфигурация для терминального коннектора, определяющая какие методы
/// подключения использовать на текущей платформе.
/// </para>
/// <para>
/// Приоритет методов подключения:
/// 1. Unix Domain Sockets (если поддерживается платформой и UseUnixSockets = true)
/// 2. Named Pipes (Windows fallback если UseNamedPipes = true)
/// </para>
/// <para>
/// <b>Configuration Example (appsettings.json):</b>
/// <code>
/// {
///   "TerminalConnector": {
///     "UseUnixSockets": true,
///     "UseNamedPipes": true,
///     "DefaultSocketPath": "/tmp/orchestra_agent.sock",
///     "DefaultPipeName": "orchestra_agent_pipe",
///     "ConnectionTimeoutMs": 30000
///   }
/// }
/// </code>
/// </para>
/// <para>
/// <b>DI Registration:</b>
/// <code>
/// services.Configure&lt;TerminalConnectorOptions&gt;(
///     builder.Configuration.GetSection("TerminalConnector"));
/// </code>
/// </para>
/// </remarks>
public class TerminalConnectorOptions
{
    /// <summary>
    /// Использовать Unix Domain Sockets для подключения
    /// </summary>
    /// <remarks>
    /// Поддерживается на:
    /// - Linux (все версии)
    /// - macOS (все версии)
    /// - Windows 10 build 17063 и выше
    ///
    /// Default: true (будет использован если платформа поддерживает)
    /// </remarks>
    public bool UseUnixSockets { get; set; } = true;

    /// <summary>
    /// Использовать Windows Named Pipes для подключения
    /// </summary>
    /// <remarks>
    /// Поддерживается только на Windows.
    /// Используется как fallback если Unix Domain Sockets недоступны.
    ///
    /// Default: true (для обратной совместимости с Windows 7/8)
    /// </remarks>
    public bool UseNamedPipes { get; set; } = true;

    /// <summary>
    /// Путь к Unix Domain Socket по умолчанию
    /// </summary>
    /// <remarks>
    /// Используется если AgentConnectionParams.SocketPath не указан.
    /// Должен быть абсолютным путем на Unix системах (начинается с /).
    ///
    /// Рекомендуемые пути:
    /// - Linux: /tmp/orchestra_agent.sock
    /// - macOS: /tmp/orchestra_agent.sock
    /// - Windows: C:\Temp\orchestra_agent.sock
    /// </remarks>
    public string? DefaultSocketPath { get; set; }

    /// <summary>
    /// Имя Named Pipe по умолчанию (Windows)
    /// </summary>
    /// <remarks>
    /// Используется если AgentConnectionParams.PipeName не указан.
    /// Указывается БЕЗ префикса \\.\pipe\ (префикс добавляется автоматически).
    ///
    /// Требования:
    /// - Не может быть пустым
    /// - Не может содержать обратные слеши (\)
    /// - Длина меньше 256 символов
    ///
    /// Default: "orchestra_agent_pipe"
    /// </remarks>
    public string? DefaultPipeName { get; set; }

    /// <summary>
    /// Timeout подключения в миллисекундах
    /// </summary>
    /// <remarks>
    /// Максимальное время ожидания установки соединения.
    ///
    /// Range: 1000-300000 мс (1 сек - 5 мин)
    /// Default: 30000 мс (30 секунд)
    ///
    /// Рекомендации:
    /// - Локальные соединения: 5000-10000 мс
    /// - Сетевые соединения: 30000-60000 мс
    /// </remarks>
    public int ConnectionTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Валидирует настройки конфигурации
    /// </summary>
    /// <returns>Список ошибок валидации (пустой если настройки валидны)</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        // Хотя бы один метод подключения должен быть включен
        if (!UseUnixSockets && !UseNamedPipes)
        {
            errors.Add(
                "At least one connection method must be enabled " +
                "(UseUnixSockets or UseNamedPipes)");
        }

        // Валидация timeout
        if (ConnectionTimeoutMs < 1000 || ConnectionTimeoutMs > 300000)
        {
            errors.Add(
                $"ConnectionTimeoutMs must be between 1000 and 300000, " +
                $"got {ConnectionTimeoutMs}");
        }

        // Валидация DefaultPipeName (если указан)
        if (!string.IsNullOrWhiteSpace(DefaultPipeName))
        {
            if (DefaultPipeName.Contains('\\'))
            {
                errors.Add(
                    $"DefaultPipeName cannot contain backslashes: '{DefaultPipeName}'");
            }

            if (DefaultPipeName.Length >= 256)
            {
                errors.Add(
                    $"DefaultPipeName must be less than 256 characters, " +
                    $"got {DefaultPipeName.Length}");
            }
        }

        // Валидация DefaultSocketPath (если указан и на Unix)
        if (!string.IsNullOrWhiteSpace(DefaultSocketPath) && !OperatingSystem.IsWindows())
        {
            if (!DefaultSocketPath.StartsWith('/'))
            {
                errors.Add(
                    $"DefaultSocketPath must be absolute (start with /), " +
                    $"got '{DefaultSocketPath}'");
            }
        }

        return errors;
    }
}
