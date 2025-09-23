using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orchestra.Core.Models.Chat;

namespace Orchestra.Core.Services
{
    /// <summary>
    /// Интерфейс для управления контекстом чата координатора
    /// </summary>
    public interface IChatContextService
    {
        /// <summary>
        /// Получить существующую сессию или создать новую для пользователя и инстанса
        /// </summary>
        /// <param name="userId">Идентификатор пользователя (null для анонимных)</param>
        /// <param name="instanceId">Идентификатор инстанса координатора</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Сессия чата</returns>
        Task<ChatSession> GetOrCreateSessionAsync(string? userId, string instanceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Сохранить сообщение в сессии чата
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <param name="author">Автор сообщения</param>
        /// <param name="content">Содержимое сообщения</param>
        /// <param name="messageType">Тип сообщения</param>
        /// <param name="metadata">Дополнительные метаданные в формате JSON</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Созданное сообщение</returns>
        Task<ChatMessage> SaveMessageAsync(Guid sessionId, string author, string content, MessageType messageType, string? metadata = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Получить историю сообщений для сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <param name="limit">Максимальное количество сообщений (null для всех)</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Список сообщений в хронологическом порядке</returns>
        Task<List<ChatMessage>> GetSessionHistoryAsync(Guid sessionId, int? limit = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Получить список сессий пользователя
        /// </summary>
        /// <param name="userId">Идентификатор пользователя</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Список сессий пользователя</returns>
        Task<List<ChatSession>> GetUserSessionsAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Проверить существование сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>True если сессия существует</returns>
        Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Обновить название сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <param name="title">Новое название сессии</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        Task UpdateSessionTitleAsync(Guid sessionId, string title, CancellationToken cancellationToken = default);
    }
}