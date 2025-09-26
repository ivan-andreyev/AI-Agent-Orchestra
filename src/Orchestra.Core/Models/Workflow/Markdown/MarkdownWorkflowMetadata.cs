namespace Orchestra.Core.Models.Workflow.Markdown
{
    /// <summary>
    /// Метаданные markdown workflow'а
    /// </summary>
    public class MarkdownWorkflowMetadata
    {
        /// <summary>Название workflow'а</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Описание workflow'а</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Автор workflow'а</summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>Версия workflow'а</summary>
        public string Version { get; set; } = "1.0";

        /// <summary>Теги для категоризации</summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>Дата создания</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Дата последнего изменения</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Приоритет выполнения</summary>
        public WorkflowPriority Priority { get; set; } = WorkflowPriority.Normal;

        /// <summary>Статус workflow'а</summary>
        public MarkdownWorkflowStatus Status { get; set; } = MarkdownWorkflowStatus.Draft;
    }
}