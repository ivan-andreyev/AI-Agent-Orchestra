using Orchestra.Core.Models.Chat;
using System;
using System.Threading.Tasks;

namespace Orchestra.Core.Services
{
    /// <summary>
    /// Сервис для управления сессиями чата по connection ID
    /// Thread-safe альтернатива static dictionary для SignalR хаба
    /// </summary>
    public interface IConnectionSessionService
    {
        /// <summary>
        /// Получить сессию чата для connection ID
        /// </summary>
        /// <param name="connectionId">Идентификатор SignalR подключения</param>
        /// <returns>Сессия чата или null если не найдена</returns>
        Task<ChatSession?> GetSessionAsync(string connectionId);

        /// <summary>
        /// Сохранить сессию для connection ID
        /// </summary>
        /// <param name="connectionId">Идентификатор SignalR подключения</param>
        /// <param name="session">Сессия чата для сохранения</param>
        Task SetSessionAsync(string connectionId, ChatSession session);

        /// <summary>
        /// Удалить сессию для connection ID
        /// </summary>
        /// <param name="connectionId">Идентификатор SignalR подключения</param>
        /// <returns>True если сессия была удалена, false если не найдена</returns>
        Task<bool> RemoveSessionAsync(string connectionId);

        /// <summary>
        /// Проверить существование сессии для connection ID
        /// </summary>
        /// <param name="connectionId">Идентификатор SignalR подключения</param>
        /// <returns>True если сессия существует</returns>
        Task<bool> HasSessionAsync(string connectionId);
    }
}