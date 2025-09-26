using System;
using System.IO;

namespace Orchestra.Core.Services
{
    /// <summary>
    /// Аргументы события изменения markdown файла
    /// </summary>
    public class MarkdownFileChangedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public string FileName { get; }
        public ChangeType ChangeType { get; }
        public DateTime ChangedAt { get; }
        public string? PreviousContent { get; }
        public string? NewContent { get; }

        public MarkdownFileChangedEventArgs(string filePath, ChangeType changeType,
            string? previousContent = null, string? newContent = null)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            ChangeType = changeType;
            ChangedAt = DateTime.UtcNow;
            PreviousContent = previousContent;
            NewContent = newContent;
        }
    }

    /// <summary>
    /// Аргументы события удаления markdown файла
    /// </summary>
    public class MarkdownFileDeletedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public string FileName { get; }
        public DateTime DeletedAt { get; }

        public MarkdownFileDeletedEventArgs(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            DeletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Аргументы события создания markdown файла
    /// </summary>
    public class MarkdownFileCreatedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public string FileName { get; }
        public DateTime CreatedAt { get; }
        public string? InitialContent { get; }

        public MarkdownFileCreatedEventArgs(string filePath, string? initialContent = null)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            CreatedAt = DateTime.UtcNow;
            InitialContent = initialContent;
        }
    }

    /// <summary>
    /// Типы изменений файлов
    /// </summary>
    public enum ChangeType
    {
        Modified,
        Renamed,
        AttributeChanged
    }
}