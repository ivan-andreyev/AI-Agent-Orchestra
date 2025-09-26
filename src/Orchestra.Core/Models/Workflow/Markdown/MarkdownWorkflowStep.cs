namespace Orchestra.Core.Models.Workflow.Markdown
{
    /// <summary>
    /// Представляет шаг выполнения в markdown workflow'е
    /// </summary>
    public class MarkdownWorkflowStep
    {
        /// <summary>Идентификатор шага</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Название шага</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Тип шага</summary>
        public MarkdownStepType Type { get; set; } = MarkdownStepType.Task;

        /// <summary>Команда для выполнения</summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>Параметры выполнения</summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>Зависимости от других шагов</summary>
        public List<string> DependsOn { get; set; } = new();

        /// <summary>Условия выполнения</summary>
        public string Condition { get; set; } = string.Empty;

        /// <summary>Таймаут выполнения в секундах</summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>Можно ли повторить при ошибке</summary>
        public bool Retryable { get; set; } = true;

        /// <summary>Порядок выполнения</summary>
        public int Order { get; set; }
    }
}