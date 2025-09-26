namespace Orchestra.Core.Services;

/// <summary>
/// Интерфейс для выполнения команд через агентов
/// </summary>
public interface IAgentExecutor
{
    /// <summary>
    /// Выполняет команду через агента и возвращает результат
    /// </summary>
    /// <param name="command">Команда для выполнения</param>
    /// <param name="workingDirectory">Рабочая директория для выполнения</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат выполнения команды агентом</returns>
    Task<AgentExecutionResponse> ExecuteCommandAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает тип/название агента для логирования и идентификации
    /// </summary>
    string AgentType { get; }
}

/// <summary>
/// Результат выполнения команды агентом
/// </summary>
public class AgentExecutionResponse
{
    /// <summary>
    /// Успешность выполнения команды
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Выходные данные от агента
    /// </summary>
    public string Output { get; set; } = "";

    /// <summary>
    /// Сообщение об ошибке при неуспешном выполнении
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Время, затраченное на выполнение
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Дополнительные метаданные от агента
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}