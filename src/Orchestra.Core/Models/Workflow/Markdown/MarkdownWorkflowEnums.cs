namespace Orchestra.Core.Models.Workflow.Markdown
{
    /// <summary>
    /// Статус markdown workflow'а
    /// </summary>
    public enum MarkdownWorkflowStatus
    {
        /// <summary>Черновик</summary>
        Draft = 0,

        /// <summary>Готов к выполнению</summary>
        Ready = 1,

        /// <summary>Выполняется</summary>
        Running = 2,

        /// <summary>Завершён успешно</summary>
        Completed = 3,

        /// <summary>Завершён с ошибкой</summary>
        Failed = 4,

        /// <summary>Приостановлен</summary>
        Paused = 5,

        /// <summary>Отменён</summary>
        Cancelled = 6
    }

    /// <summary>
    /// Тип секции markdown документа
    /// </summary>
    public enum MarkdownSectionType
    {
        /// <summary>Метаданные</summary>
        Metadata = 0,

        /// <summary>Переменные</summary>
        Variables = 1,

        /// <summary>Шаги выполнения</summary>
        Steps = 2,

        /// <summary>Описание</summary>
        Description = 3,

        /// <summary>Примечания</summary>
        Notes = 4,

        /// <summary>Неизвестный тип</summary>
        Unknown = 99
    }

    /// <summary>
    /// Тип шага выполнения
    /// </summary>
    public enum MarkdownStepType
    {
        /// <summary>Выполнение задачи</summary>
        Task = 0,

        /// <summary>Условное выполнение</summary>
        Condition = 1,

        /// <summary>Цикл</summary>
        Loop = 2,

        /// <summary>Параллельное выполнение</summary>
        Parallel = 3,

        /// <summary>Задержка</summary>
        Delay = 4,

        /// <summary>Вызов другого workflow</summary>
        SubWorkflow = 5
    }

    /// <summary>
    /// Тип переменной workflow'а
    /// </summary>
    public enum MarkdownVariableType
    {
        /// <summary>Строка</summary>
        String = 0,

        /// <summary>Число</summary>
        Number = 1,

        /// <summary>Логическое значение</summary>
        Boolean = 2,

        /// <summary>Дата</summary>
        DateTime = 3,

        /// <summary>Путь к файлу</summary>
        FilePath = 4,

        /// <summary>URL</summary>
        Url = 5,

        /// <summary>JSON объект</summary>
        Json = 6,

        /// <summary>Массив строк</summary>
        StringArray = 7
    }

    /// <summary>
    /// Приоритет выполнения workflow'а
    /// </summary>
    public enum WorkflowPriority
    {
        /// <summary>Низкий</summary>
        Low = 0,

        /// <summary>Обычный</summary>
        Normal = 1,

        /// <summary>Высокий</summary>
        High = 2,

        /// <summary>Критический</summary>
        Critical = 3
    }
}