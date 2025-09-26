namespace Orchestra.Core.Models.Workflow.Markdown
{
    /// <summary>
    /// Представляет markdown документ workflow'а
    /// </summary>
    public class MarkdownWorkflowDocument
    {
        /// <summary>Уникальный идентификатор документа</summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Путь к markdown файлу</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Исходное содержимое markdown</summary>
        public string RawContent { get; set; } = string.Empty;

        /// <summary>Метаданные workflow'а</summary>
        public MarkdownWorkflowMetadata Metadata { get; set; } = new();

        /// <summary>Список секций документа</summary>
        public List<MarkdownWorkflowSection> Sections { get; set; } = new();

        /// <summary>Переменные workflow'а</summary>
        public List<MarkdownWorkflowVariable> Variables { get; set; } = new();

        /// <summary>Шаги выполнения</summary>
        public List<MarkdownWorkflowStep> Steps { get; set; } = new();

        /// <summary>Дата парсинга</summary>
        public DateTime ParsedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Хеш содержимого для кэширования</summary>
        public string ContentHash { get; set; } = string.Empty;
    }
}