using System;

namespace Orchestra.Core.Models.Chat
{
    /// <summary>
    /// DTO для запроса создания нового сообщения в чате
    /// </summary>
    /// <param name="SessionId">Идентификатор сессии чата</param>
    /// <param name="Author">Автор сообщения</param>
    /// <param name="Content">Содержимое сообщения</param>
    /// <param name="MessageType">Тип сообщения (пользователь, система, агент)</param>
    /// <param name="Metadata">Дополнительные метаданные в формате JSON (опционально)</param>
    public record CreateMessageRequest(
        Guid SessionId,
        string Author,
        string Content,
        MessageType MessageType,
        string? Metadata = null
    );
}