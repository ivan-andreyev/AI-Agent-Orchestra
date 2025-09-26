namespace Orchestra.Core.Models.Workflow.Markdown
{
    /// <summary>
    /// Представляет секцию markdown документа
    /// </summary>
    public class MarkdownWorkflowSection
    {
        /// <summary>Тип секции</summary>
        public MarkdownSectionType Type { get; set; }

        /// <summary>Заголовок секции</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Содержимое секции</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Порядок в документе</summary>
        public int Order { get; set; }

        /// <summary>Уровень заголовка (H1, H2, H3...)</summary>
        public int HeaderLevel { get; set; } = 1;

        /// <summary>Дополнительные атрибуты секции</summary>
        public Dictionary<string, string> Attributes { get; set; } = new();
    }
}