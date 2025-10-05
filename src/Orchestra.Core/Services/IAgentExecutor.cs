namespace Orchestra.Core.Services;

/// <summary>
/// Интерфейс для выполнения команд через AI-агентов
/// </summary>
/// <remarks>
/// <para>
/// Этот интерфейс определяет единый контракт для всех исполнителей AI-агентов в системе.
/// Каждая реализация отвечает за взаимодействие с конкретным типом AI-агента
/// (например, Claude Code, GitHub Copilot, Custom Shell Executor).
/// </para>
/// <para>
/// <b>Паттерны реализации:</b>
/// <list type="bullet">
/// <item><description>Все реализации должны обрабатывать retry logic (через RetryExecutor или собственную логику)</description></item>
/// <item><description>Реализации должны ограничивать параллельные выполнения через SemaphoreSlim</description></item>
/// <item><description>Рекомендуется использовать базовый класс BaseAgentExecutor для общей функциональности</description></item>
/// <item><description>Логирование должно быть детальным для трассировки выполнения</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Регистрация в DI:</b>
/// Используйте keyed services для регистрации нескольких реализаций:
/// <code>
/// services.AddKeyedSingleton&lt;IAgentExecutor, ClaudeCodeExecutor&gt;("claude-code");
/// services.AddKeyedSingleton&lt;IAgentExecutor, ShellExecutor&gt;("shell");
/// </code>
/// </para>
/// </remarks>
/// <example>
/// Пример использования через TaskExecutionJob:
/// <code>
/// var executor = _serviceProvider.GetRequiredKeyedService&lt;IAgentExecutor&gt;(task.AgentType);
/// var result = await executor.ExecuteCommandAsync(task.Command, task.WorkingDirectory, cancellationToken);
/// </code>
/// </example>
public interface IAgentExecutor
{
    /// <summary>
    /// Выполняет команду через AI-агента и возвращает результат выполнения
    /// </summary>
    /// <param name="command">Команда или prompt для выполнения агентом. Не может быть пустой строкой.</param>
    /// <param name="workingDirectory">
    /// Рабочая директория для выполнения команды.
    /// Если директория не существует, поведение зависит от реализации (создание или ошибка).
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены операции. Реализации должны корректно обрабатывать отмену,
    /// останавливая процессы агента и освобождая ресурсы.
    /// </param>
    /// <returns>
    /// Результат выполнения команды агентом, включая успешность, выходные данные,
    /// ошибки и метаданные выполнения.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Выбрасывается, если command пустая строка или null.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Может быть выброшено реализацией, если workingDirectory не существует и не может быть создана.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Выбрасывается при отмене операции через cancellationToken.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Метод должен быть идемпотентным в разумных пределах - повторный вызов
    /// с теми же параметрами должен давать аналогичный результат (с учётом состояния агента).
    /// </para>
    /// <para>
    /// Реализации должны использовать retry logic для обработки временных сбоев
    /// (сетевые проблемы, тайм-ауты, недоступность процесса агента).
    /// </para>
    /// <para>
    /// Для длительных операций (>30 секунд) реализация должна поддерживать
    /// прогрессивное логирование и возможность отмены.
    /// </para>
    /// </remarks>
    Task<AgentExecutionResponse> ExecuteCommandAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает уникальный идентификатор типа агента для логирования, маршрутизации и идентификации
    /// </summary>
    /// <value>
    /// Строка-идентификатор типа агента (например, "claude-code", "github-copilot", "shell").
    /// Должна быть константной для каждой реализации и уникальной в рамках системы.
    /// </value>
    /// <remarks>
    /// <para>
    /// Это значение используется для:
    /// <list type="bullet">
    /// <item><description>Маршрутизации задач к правильному исполнителю через Agent Registry</description></item>
    /// <item><description>Логирования и трассировки выполнения</description></item>
    /// <item><description>Регистрации в DI контейнере как keyed service</description></item>
    /// <item><description>Отображения в UI для выбора типа агента</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Рекомендуется использовать lowercase kebab-case формат: "agent-type-name"
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public string AgentType => "claude-code";
    /// </code>
    /// </example>
    string AgentType { get; }
}

/// <summary>
/// Результат выполнения команды агентом с детальной информацией о выполнении
/// </summary>
/// <remarks>
/// <para>
/// Этот класс инкапсулирует все данные о результате выполнения команды агентом,
/// включая успешность, выходные данные, ошибки и метаданные для анализа и логирования.
/// </para>
/// <para>
/// <b>Использование:</b>
/// <list type="bullet">
/// <item><description>Success указывает на общую успешность операции</description></item>
/// <item><description>Output содержит результат работы агента (код, текст, JSON)</description></item>
/// <item><description>ErrorMessage заполняется только при Success = false</description></item>
/// <item><description>Metadata используется для передачи специфичных для агента данных</description></item>
/// </list>
/// </para>
/// </remarks>
public class AgentExecutionResponse
{
    /// <summary>
    /// Успешность выполнения команды агентом
    /// </summary>
    /// <value>
    /// <c>true</c> если команда выполнена успешно (даже если с предупреждениями),
    /// <c>false</c> если произошла критическая ошибка выполнения.
    /// </value>
    /// <remarks>
    /// Успешность определяется по exit code процесса агента или по анализу ответа API.
    /// Даже при Success = true могут присутствовать предупреждения в Output или Metadata.
    /// </remarks>
    public bool Success { get; set; }

    /// <summary>
    /// Выходные данные от агента после выполнения команды
    /// </summary>
    /// <value>
    /// Строка с результатом работы агента. Формат зависит от типа агента и команды:
    /// <list type="bullet">
    /// <item><description>Для Claude Code: Markdown с описанием действий и результатов</description></item>
    /// <item><description>Для Shell Executor: Plain text с stdout процесса</description></item>
    /// <item><description>Для API-based агентов: JSON response или formatted text</description></item>
    /// </list>
    /// </value>
    /// <remarks>
    /// <para>
    /// Всегда содержит непустую строку даже при Success = false (в этом случае содержит описание проблемы).
    /// Максимальная длина не ограничена, но рекомендуется логировать только первые 1000 символов.
    /// </para>
    /// </remarks>
    public string Output { get; set; } = "";

    /// <summary>
    /// Сообщение об ошибке при неуспешном выполнении команды
    /// </summary>
    /// <value>
    /// <c>null</c> при Success = true,
    /// детальное описание ошибки при Success = false.
    /// </value>
    /// <remarks>
    /// <para>
    /// Содержит human-readable описание ошибки для логирования и отображения пользователю.
    /// Может включать stack trace для системных ошибок, но не должно содержать чувствительной информации.
    /// </para>
    /// <para>
    /// Примеры сообщений:
    /// <list type="bullet">
    /// <item><description>"Process timed out after 600 seconds"</description></item>
    /// <item><description>"Agent returned non-zero exit code: 1"</description></item>
    /// <item><description>"HTTP API call failed with status 500: Internal Server Error"</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Время, фактически затраченное на выполнение команды агентом
    /// </summary>
    /// <value>
    /// TimeSpan от начала вызова ExecuteCommandAsync до получения результата,
    /// включая время retry attempts и ожидания в семафоре.
    /// </value>
    /// <remarks>
    /// <para>
    /// Используется для:
    /// <list type="bullet">
    /// <item><description>Performance monitoring и выявления медленных операций</description></item>
    /// <item><description>SLA tracking для агентов</description></item>
    /// <item><description>Оптимизации timeout настроек</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Типичные значения:
    /// <list type="bullet">
    /// <item><description>Claude Code: 5-30 секунд для простых команд, до 10 минут для сложных</description></item>
    /// <item><description>Shell Executor: 1-5 секунд для быстрых скриптов</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Дополнительные метаданные от агента о выполнении команды
    /// </summary>
    /// <value>
    /// Dictionary с специфичными для агента данными. Ключи должны быть уникальными в рамках типа агента.
    /// </value>
    /// <remarks>
    /// <para>
    /// <b>Стандартные ключи метаданных:</b>
    /// <list type="bullet">
    /// <item><description>"ExecutionMethod": string - Способ выполнения (HTTP, CLI, Mock)</description></item>
    /// <item><description>"WorkingDirectory": string - Фактическая рабочая директория</description></item>
    /// <item><description>"AgentType": string - Тип агента для логирования</description></item>
    /// <item><description>"TotalAttempts": int - Количество попыток выполнения (включая retry)</description></item>
    /// <item><description>"RetrySuccess": bool - Была ли успешна retry логика</description></item>
    /// <item><description>"ProcessId": int - PID процесса агента (если применимо)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Реализации могут добавлять собственные ключи для специфичных данных:
    /// <code>
    /// Metadata["ApiEndpoint"] = "https://api.anthropic.com/v1/claude";
    /// Metadata["TokensUsed"] = 1500;
    /// Metadata["CacheHit"] = true;
    /// </code>
    /// </para>
    /// </remarks>
    /// <example>
    /// Пример метаданных от ClaudeCodeExecutor:
    /// <code>
    /// {
    ///     "ExecutionMethod": "CLI",
    ///     "WorkingDirectory": "C:\\Projects\\MyApp",
    ///     "AgentType": "Claude Code",
    ///     "TotalAttempts": 2,
    ///     "RetrySuccess": true,
    ///     "TotalRetryTime": "00:00:04.250"
    /// }
    /// </code>
    /// </example>
    public Dictionary<string, object> Metadata { get; set; } = new();
}