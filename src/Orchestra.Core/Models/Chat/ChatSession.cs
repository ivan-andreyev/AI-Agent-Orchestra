using System;
using System.Collections.Generic;

namespace Orchestra.Core.Models.Chat
{
    /// <summary>
    /// Модель сессии чата для координатора
    /// </summary>
    public class ChatSession
    {
        /// <summary>
        /// Уникальный идентификатор сессии
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Идентификатор пользователя (nullable для анонимных пользователей)
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Идентификатор инстанса координатора
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Название сессии
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Время создания сессии
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Время последнего сообщения в сессии
        /// </summary>
        public DateTime LastMessageAt { get; set; }

        /// <summary>
        /// Коллекция сообщений в сессии
        /// </summary>
        public List<ChatMessage> Messages { get; set; } = new();
    }
}