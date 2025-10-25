using Orchestra.Core.Models;

namespace Orchestra.Core.Services.Connectors;

/// <summary>
/// Интерфейс для подключения к внешним агентам различных типов
/// </summary>
/// <remarks>
/// <para>
/// IAgentConnector предоставляет абстракцию для взаимодействия с различными
/// типами внешних AI-агентов (Claude Code через терминал, Cursor через API, и т.д.)
/// </para>
/// <para>
/// Lifecycle сессии:
/// 1. ConnectAsync() - установка соединения
/// 2. SendCommandAsync() - отправка команд
/// 3. ReadOutputAsync() - чтение вывода (streaming)
/// 4. DisconnectAsync() - закрытие соединения
/// 5. Dispose() - очистка ресурсов
/// </para>
/// <para>
/// <b>Регистрация в DI:</b>
/// <code>
/// services.AddTransient&lt;IAgentConnector, TerminalAgentConnector&gt;();
/// </code>
/// </para>
/// </remarks>
public interface IAgentConnector : IDisposable
{
    /// <summary>
    /// Тип коннектора (terminal, api, tab-based)
    /// </summary>
    string ConnectorType { get; }

    /// <summary>
    /// Идентификатор подключенного агента
    /// </summary>
    /// <remarks>
    /// Null если коннектор еще не подключен к агенту
    /// </remarks>
    string? AgentId { get; }

    /// <summary>
    /// Текущий статус подключения
    /// </summary>
    ConnectionStatus Status { get; }

    /// <summary>
    /// Признак того, что коннектор в данный момент подключен к агенту
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Время последней активности (отправка команды или получение вывода)
    /// </summary>
    DateTime LastActivityAt { get; }

    /// <summary>
    /// Подключается к внешнему агенту
    /// </summary>
    /// <param name="agentId">Идентификатор агента для подключения</param>
    /// <param name="connectionParams">Параметры подключения</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат подключения</returns>
    /// <exception cref="ArgumentNullException">Если agentId или connectionParams равны null</exception>
    /// <exception cref="InvalidOperationException">Если коннектор уже подключен к агенту</exception>
    /// <exception cref="TimeoutException">Если подключение превысило таймаут</exception>
    Task<ConnectionResult> ConnectAsync(
        string agentId,
        AgentConnectionParams connectionParams,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправляет команду в подключенного агента
    /// </summary>
    /// <param name="command">Команда для отправки</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат отправки команды</returns>
    /// <exception cref="ArgumentNullException">Если command равна null</exception>
    /// <exception cref="InvalidOperationException">Если коннектор не подключен к агенту</exception>
    /// <remarks>
    /// Команда отправляется асинхронно. Результат успешной отправки означает,
    /// что команда была записана в stdin агента, но не означает, что команда
    /// была выполнена агентом.
    /// </remarks>
    Task<CommandResult> SendCommandAsync(
        string command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Читает вывод из подключенного агента в виде асинхронного потока
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Асинхронный поток строк вывода</returns>
    /// <exception cref="InvalidOperationException">Если коннектор не подключен к агенту</exception>
    /// <remarks>
    /// <para>
    /// Метод возвращает IAsyncEnumerable, который будет продолжать выдавать строки
    /// вывода до тех пор, пока агент не закроется или не будет вызван DisconnectAsync().
    /// </para>
    /// <para>
    /// Пример использования:
    /// <code>
    /// await foreach (var line in connector.ReadOutputAsync(cancellationToken))
    /// {
    ///     Console.WriteLine(line);
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    IAsyncEnumerable<string> ReadOutputAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отключается от агента
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат отключения</returns>
    /// <exception cref="InvalidOperationException">Если коннектор не подключен к агенту</exception>
    /// <remarks>
    /// После успешного отключения коннектор можно переиспользовать для
    /// подключения к другому агенту вызовом ConnectAsync().
    /// </remarks>
    Task<DisconnectionResult> DisconnectAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Событие изменения статуса подключения
    /// </summary>
    /// <remarks>
    /// Генерируется при каждом изменении статуса (Connecting, Connected, Error, Disconnecting, Disconnected)
    /// </remarks>
    event EventHandler<ConnectionStatusChangedEventArgs>? StatusChanged;
}
