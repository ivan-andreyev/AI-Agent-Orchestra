namespace Orchestra.Core.Models.Chat
{
    /// <summary>
    /// Типы сообщений в чате координатора
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// Сообщение от пользователя
        /// </summary>
        User = 0,

        /// <summary>
        /// Системное сообщение
        /// </summary>
        System = 1,

        /// <summary>
        /// Сообщение от агента
        /// </summary>
        Agent = 2
    }
}