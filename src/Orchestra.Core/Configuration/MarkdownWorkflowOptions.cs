namespace Orchestra.Core.Configuration
{
    /// <summary>
    /// Конфигурация для markdown workflow системы
    /// </summary>
    public class MarkdownWorkflowOptions
    {
        public const string SECTION_NAME = "MarkdownWorkflow";

        /// <summary>
        /// Настройки файлового наблюдателя
        /// </summary>
        public FileWatcherOptions FileWatcher { get; set; } = new();
    }

    /// <summary>
    /// Настройки для отслеживания изменений файлов
    /// </summary>
    public class FileWatcherOptions
    {
        /// <summary>
        /// Директория для отслеживания markdown workflow файлов
        /// </summary>
        public string WatchDirectory { get; set; } = "./workflows";

        /// <summary>
        /// Включать подпапки в отслеживание
        /// </summary>
        public bool IncludeSubdirectories { get; set; } = true;

        /// <summary>
        /// Интервал задержки для группировки событий (мс)
        /// </summary>
        public int DebounceIntervalMs { get; set; } = 500;

        /// <summary>
        /// Включить автоматическую перезагрузку workflow при изменениях
        /// </summary>
        public bool EnableAutoReload { get; set; } = true;

        /// <summary>
        /// Максимальное количество файлов для отслеживания
        /// </summary>
        public int MaxWatchedFiles { get; set; } = 1000;

        /// <summary>
        /// Максимальный размер файла для отслеживания (байты)
        /// </summary>
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB
    }
}