namespace Orchestra.Core.Models;

/// <summary>
/// Статус подключения к внешнему агенту
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// Отключен от агента
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// Процесс подключения к агенту
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// Успешно подключен к агенту
    /// </summary>
    Connected = 2,

    /// <summary>
    /// Ошибка подключения или выполнения
    /// </summary>
    Error = 3,

    /// <summary>
    /// Процесс отключения от агента
    /// </summary>
    Disconnecting = 4
}
