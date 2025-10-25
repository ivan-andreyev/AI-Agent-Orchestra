namespace Orchestra.Core.Services.Connectors;

/// <summary>
/// Интерфейс буфера для вывода агента
/// </summary>
/// <remarks>
/// <para>
/// IAgentOutputBuffer предоставляет потокобезопасный циркулярный буфер
/// для хранения и чтения вывода внешних агентов.
/// </para>
/// <para>
/// <b>NOTE:</b> Это stub-версия интерфейса для Task 1.2A.
/// Полная реализация будет в Phase 1.4 (Output Buffer Implementation).
/// </para>
/// </remarks>
public interface IAgentOutputBuffer
{
    /// <summary>
    /// Добавляет строку в буфер
    /// </summary>
    /// <param name="line">Строка для добавления</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    Task AppendLineAsync(string line, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает последние N строк из буфера
    /// </summary>
    /// <param name="count">Количество строк (по умолчанию 100)</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Список строк</returns>
    Task<IReadOnlyList<string>> GetLastLinesAsync(int count = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает строки из буфера в виде асинхронного потока
    /// </summary>
    /// <param name="regexFilter">Опциональный regex фильтр для строк</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Асинхронный поток строк</returns>
    IAsyncEnumerable<string> GetLinesAsync(string? regexFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Очищает буфер
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Количество строк в буфере
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Событие добавления новой строки в буфер
    /// </summary>
    event EventHandler<OutputLineAddedEventArgs>? LineAdded;
}

/// <summary>
/// Аргументы события добавления строки в буфер
/// </summary>
public class OutputLineAddedEventArgs : EventArgs
{
    /// <summary>
    /// Добавленная строка
    /// </summary>
    public string Line { get; init; } = string.Empty;

    /// <summary>
    /// Время добавления
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
