using System;

namespace Orchestra.Core.Models.Chat
{
    /// <summary>
    /// Модель сообщения в чате координатора
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Уникальный идентификатор сообщения
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Идентификатор сессии чата (внешний ключ)
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// Автор сообщения
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Содержимое сообщения
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Тип сообщения (пользователь, система, агент)
        /// </summary>
        public MessageType MessageType { get; set; }

        /// <summary>
        /// Время создания сообщения
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Дополнительные метаданные в формате JSON
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Навигационное свойство к сессии чата
        /// </summary>
        public ChatSession Session { get; set; } = null!;
    }
}