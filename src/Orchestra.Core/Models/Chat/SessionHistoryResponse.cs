using System;
using System.Collections.Generic;

namespace Orchestra.Core.Models.Chat
{
    /// <summary>
    /// DTO для ответа с историей сессии чата
    /// </summary>
    /// <param name="SessionId">Идентификатор сессии чата</param>
    /// <param name="Title">Название сессии</param>
    /// <param name="Messages">Список сообщений в хронологическом порядке</param>
    public record SessionHistoryResponse(
        Guid SessionId,
        string Title,
        List<ChatMessage> Messages
    );
}