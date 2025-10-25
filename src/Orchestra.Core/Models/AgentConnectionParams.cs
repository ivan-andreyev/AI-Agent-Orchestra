namespace Orchestra.Core.Models;

/// <summary>
/// Параметры подключения к внешнему агенту
/// </summary>
/// <remarks>
/// Содержит все необходимые данные для установления соединения
/// с внешним агентом через различные типы коннекторов.
/// Разные типы коннекторов могут использовать разные параметры.
/// </remarks>
public class AgentConnectionParams
{
    /// <summary>
    /// Тип коннектора (terminal, api, tab-based)
    /// </summary>
    public string ConnectorType { get; init; } = string.Empty;

    /// <summary>
    /// Идентификатор процесса для подключения (для terminal connector)
    /// </summary>
    public int? ProcessId { get; init; }

    /// <summary>
    /// Имя named pipe для подключения (для terminal connector)
    /// </summary>
    public string? PipeName { get; init; }

    /// <summary>
    /// Путь к Unix domain socket (для terminal connector на Linux/macOS)
    /// </summary>
    public string? SocketPath { get; init; }

    /// <summary>
    /// URL API endpoint (для API-based connectors)
    /// </summary>
    public string? ApiEndpoint { get; init; }

    /// <summary>
    /// Токен аутентификации для API (для API-based connectors)
    /// </summary>
    public string? AuthToken { get; init; }

    /// <summary>
    /// Рабочая директория агента
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Дополнительные метаданные для коннектора
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Timeout для подключения (в секундах)
    /// </summary>
    public int ConnectionTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Валидирует параметры подключения для указанного типа коннектора
    /// </summary>
    /// <param name="connectorType">Тип коннектора для валидации</param>
    /// <returns>Список ошибок валидации (пустой если валидация успешна)</returns>
    public IReadOnlyList<string> Validate(string connectorType)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ConnectorType))
        {
            errors.Add("ConnectorType is required");
        }

        if (ConnectorType != connectorType)
        {
            errors.Add($"ConnectorType mismatch: expected '{connectorType}', got '{ConnectorType}'");
        }

        switch (connectorType.ToLowerInvariant())
        {
            case "terminal":
                ValidateTerminalParams(errors);
                break;

            case "api":
                ValidateApiParams(errors);
                break;

            case "tab-based":
                ValidateTabBasedParams(errors);
                break;

            default:
                errors.Add($"Unknown connector type: {connectorType}");
                break;
        }

        if (ConnectionTimeoutSeconds <= 0 || ConnectionTimeoutSeconds > 300)
        {
            errors.Add($"ConnectionTimeoutSeconds must be between 1 and 300, got {ConnectionTimeoutSeconds}");
        }

        return errors;
    }

    /// <summary>
    /// Валидирует параметры для terminal connector
    /// </summary>
    private void ValidateTerminalParams(List<string> errors)
    {
        // Должен быть указан хотя бы один из параметров: ProcessId, PipeName или SocketPath
        if (ProcessId == null && string.IsNullOrWhiteSpace(PipeName) && string.IsNullOrWhiteSpace(SocketPath))
        {
            errors.Add("Terminal connector requires at least one of: ProcessId, PipeName, or SocketPath");
        }

        if (ProcessId.HasValue && ProcessId.Value <= 0)
        {
            errors.Add($"ProcessId must be positive, got {ProcessId.Value}");
        }

        // Validate socket path format (must start with / on Unix)
        if (!string.IsNullOrWhiteSpace(SocketPath) && !OperatingSystem.IsWindows())
        {
            if (!SocketPath.StartsWith('/'))
            {
                errors.Add($"Unix socket path must be absolute (start with /), got '{SocketPath}'");
            }
        }
    }

    /// <summary>
    /// Валидирует параметры для API connector
    /// </summary>
    private void ValidateApiParams(List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(ApiEndpoint))
        {
            errors.Add("API connector requires ApiEndpoint");
        }
        else if (!Uri.TryCreate(ApiEndpoint, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            errors.Add($"ApiEndpoint must be a valid HTTP/HTTPS URL, got '{ApiEndpoint}'");
        }
    }

    /// <summary>
    /// Валидирует параметры для tab-based connector
    /// </summary>
    private void ValidateTabBasedParams(List<string> errors)
    {
        // Tab-based connector может использовать ProcessId или ApiEndpoint
        if (ProcessId == null && string.IsNullOrWhiteSpace(ApiEndpoint))
        {
            errors.Add("Tab-based connector requires either ProcessId or ApiEndpoint");
        }
    }
}
