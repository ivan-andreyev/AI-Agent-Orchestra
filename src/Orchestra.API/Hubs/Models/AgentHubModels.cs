namespace Orchestra.API.Hubs.Models;

/// <summary>
/// Запрос на подключение к агенту через указанный коннектор.
/// </summary>
/// <param name="AgentId">Идентификатор агента для подключения</param>
/// <param name="ConnectorType">Тип коннектора (например, "terminal", "ssh")</param>
/// <param name="ConnectionParams">Параметры подключения для коннектора</param>
public record ConnectToAgentRequest(
    string AgentId,
    string ConnectorType,
    Dictionary<string, string> ConnectionParams);

/// <summary>
/// Ответ на запрос подключения к агенту.
/// </summary>
/// <param name="SessionId">Идентификатор созданного сеанса</param>
/// <param name="Success">Признак успешного подключения</param>
/// <param name="ErrorMessage">Сообщение об ошибке (если подключение неудачно)</param>
public record ConnectToAgentResponse(
    string SessionId,
    bool Success,
    string? ErrorMessage);

/// <summary>
/// Запрос на отправку команды агенту в активном сеансе.
/// </summary>
/// <param name="SessionId">Идентификатор сеанса</param>
/// <param name="Command">Команда для выполнения агентом</param>
public record SendCommandRequest(
    string SessionId,
    string Command);

/// <summary>
/// Уведомление об отправке команды агенту.
/// </summary>
/// <param name="SessionId">Идентификатор сеанса</param>
/// <param name="Command">Отправленная команда</param>
/// <param name="Success">Признак успешной отправки</param>
/// <param name="Timestamp">Временная метка отправки</param>
public record CommandSentNotification(
    string SessionId,
    string Command,
    bool Success,
    DateTime Timestamp);

/// <summary>
/// Информация об активной сессии агента.
/// </summary>
/// <param name="SessionId">Идентификатор сессии (AgentId)</param>
/// <param name="AgentId">Идентификатор агента</param>
/// <param name="Status">Текущий статус подключения</param>
/// <param name="ConnectorType">Тип используемого коннектора</param>
/// <param name="CreatedAt">Время создания сессии (UTC)</param>
/// <param name="LastActivityAt">Время последней активности (UTC)</param>
/// <param name="OutputLineCount">Количество строк в буфере вывода</param>
public record SessionInfo(
    string SessionId,
    string AgentId,
    string Status,
    string ConnectorType,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    int OutputLineCount);
