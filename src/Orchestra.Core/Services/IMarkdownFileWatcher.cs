using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orchestra.Core.Services
{
    /// <summary>
    /// Интерфейс для отслеживания изменений в markdown workflow файлах
    /// </summary>
    public interface IMarkdownFileWatcher : IDisposable
    {
        /// <summary>
        /// Событие изменения markdown workflow файла
        /// </summary>
        event EventHandler<MarkdownFileChangedEventArgs> MarkdownFileChanged;

        /// <summary>
        /// Событие удаления markdown workflow файла
        /// </summary>
        event EventHandler<MarkdownFileDeletedEventArgs> MarkdownFileDeleted;

        /// <summary>
        /// Событие создания нового markdown workflow файла
        /// </summary>
        event EventHandler<MarkdownFileCreatedEventArgs> MarkdownFileCreated;

        /// <summary>
        /// Начать отслеживание указанной директории
        /// </summary>
        /// <param name="directoryPath">Путь к директории с markdown workflow файлами</param>
        /// <param name="includeSubdirectories">Включать подпапки в отслеживание</param>
        Task StartWatchingAsync(string directoryPath, bool includeSubdirectories = true);

        /// <summary>
        /// Остановить отслеживание
        /// </summary>
        Task StopWatchingAsync();

        /// <summary>
        /// Добавить файл в список отслеживаемых
        /// </summary>
        /// <param name="filePath">Путь к markdown файлу</param>
        Task AddFileToWatchAsync(string filePath);

        /// <summary>
        /// Удалить файл из отслеживания
        /// </summary>
        /// <param name="filePath">Путь к markdown файлу</param>
        Task RemoveFileFromWatchAsync(string filePath);

        /// <summary>
        /// Получить список отслеживаемых файлов
        /// </summary>
        Task<IReadOnlyList<string>> GetWatchedFilesAsync();

        /// <summary>
        /// Проверить статус отслеживания
        /// </summary>
        bool IsWatching { get; }

        /// <summary>
        /// Текущая отслеживаемая директория
        /// </summary>
        string WatchedDirectory { get; }
    }
}