namespace Orchestra.Core.Models.Workflow.Markdown
{
    /// <summary>
    /// Представляет переменную в markdown workflow'е
    /// </summary>
    public class MarkdownWorkflowVariable
    {
        /// <summary>Имя переменной</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Тип переменной</summary>
        public MarkdownVariableType Type { get; set; } = MarkdownVariableType.String;

        /// <summary>Значение по умолчанию</summary>
        public object? DefaultValue { get; set; }

        /// <summary>Обязательная ли переменная</summary>
        public bool Required { get; set; } = false;

        /// <summary>Описание переменной</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Ограничения для значения</summary>
        public string Validation { get; set; } = string.Empty;

        /// <summary>Возможные значения (для enum типов)</summary>
        public List<string> AllowedValues { get; set; } = new();
    }
}