namespace Orchestra.Core.Models.Workflow.Markdown
{
    /// <summary>
    /// Константы для работы с markdown workflow'ами
    /// </summary>
    public static class MarkdownWorkflowConstants
    {
        /// <summary>Расширение файлов markdown workflow</summary>
        public const string FileExtension = ".md";

        /// <summary>Максимальный размер файла в байтах (10MB)</summary>
        public const long MaxFileSize = 10 * 1024 * 1024;

        /// <summary>Максимальная глубина вложенности</summary>
        public const int MaxNestingDepth = 5;

        /// <summary>Таймаут парсинга по умолчанию (сек)</summary>
        public const int DefaultParsingTimeout = 30;

        /// <summary>Паттерн для поиска переменных {{variable}}</summary>
        public const string VariablePattern = @"\{\{(\w+)\}\}";

        /// <summary>Паттерн для поиска ссылок на другие workflow</summary>
        public const string WorkflowLinkPattern = @"\[([^\]]+)\]\(([^)]+\.md)\)";

        /// <summary>Секции обязательные для workflow</summary>
        public static readonly string[] RequiredSections = { "Steps" };

        /// <summary>Поддерживаемые типы команд</summary>
        public static readonly string[] SupportedCommandTypes =
        {
            "dotnet", "git", "powershell", "bash", "custom"
        };
    }
}