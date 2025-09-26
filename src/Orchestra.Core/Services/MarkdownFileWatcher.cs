using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Orchestra.Core.Services
{
    /// <summary>
    /// Реализация отслеживания изменений в markdown workflow файлах
    /// </summary>
    public class MarkdownFileWatcher : IMarkdownFileWatcher
    {
        private readonly ILogger<MarkdownFileWatcher> _logger;
        private readonly IMarkdownWorkflowParser _parser;
        private FileSystemWatcher? _fileWatcher;
        private readonly ConcurrentDictionary<string, string> _fileContents;
        private readonly ConcurrentHashSet<string> _watchedFiles;
        private readonly Timer _debounceTimer;
        private readonly ConcurrentQueue<FileSystemEventArgs> _pendingEvents;
        private bool _disposed;

        // Конфигурация
        private const int DEBOUNCE_INTERVAL_MS = 500; // Задержка для группировки событий
        private const string MARKDOWN_PATTERN = "*.md";

        public event EventHandler<MarkdownFileChangedEventArgs>? MarkdownFileChanged;
        public event EventHandler<MarkdownFileDeletedEventArgs>? MarkdownFileDeleted;
        public event EventHandler<MarkdownFileCreatedEventArgs>? MarkdownFileCreated;

        public bool IsWatching => _fileWatcher?.EnableRaisingEvents == true;
        public string WatchedDirectory { get; private set; } = string.Empty;

        public MarkdownFileWatcher(ILogger<MarkdownFileWatcher> logger,
                                 IMarkdownWorkflowParser parser)
        {
            _logger = logger;
            _parser = parser;
            _fileContents = new ConcurrentDictionary<string, string>();
            _watchedFiles = new ConcurrentHashSet<string>();
            _pendingEvents = new ConcurrentQueue<FileSystemEventArgs>();
            _debounceTimer = new Timer(ProcessPendingEvents, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task StartWatchingAsync(string directoryPath, bool includeSubdirectories = true)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));

            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

            await StopWatchingAsync();

            WatchedDirectory = Path.GetFullPath(directoryPath);

            _fileWatcher = new FileSystemWatcher(WatchedDirectory, MARKDOWN_PATTERN)
            {
                IncludeSubdirectories = includeSubdirectories,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Deleted += OnFileDeleted;
            _fileWatcher.Renamed += OnFileRenamed;
            _fileWatcher.Error += OnError;

            // Загрузить существующие файлы
            await LoadExistingFilesAsync();

            _fileWatcher.EnableRaisingEvents = true;

            _logger.LogInformation("Started watching markdown files in directory: {Directory}", WatchedDirectory);
        }

        public async Task StopWatchingAsync()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            _fileContents.Clear();
            _watchedFiles.Clear();

            // Очистить pending events
            while (_pendingEvents.TryDequeue(out _)) { }

            _logger.LogInformation("Stopped watching markdown files");

            await Task.CompletedTask;
        }

        public async Task AddFileToWatchAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            var fullPath = Path.GetFullPath(filePath);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"File not found: {fullPath}");

            if (!fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("File must be a markdown file (.md)", nameof(filePath));

            if (_watchedFiles.Add(fullPath))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(fullPath);
                    _fileContents.TryAdd(fullPath, content);

                    _logger.LogDebug("Added file to watch list: {FilePath}", fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read content for watched file: {FilePath}", fullPath);
                }
            }
        }

        public async Task RemoveFileFromWatchAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            var fullPath = Path.GetFullPath(filePath);

            if (_watchedFiles.TryRemove(fullPath))
            {
                _fileContents.TryRemove(fullPath, out _);
                _logger.LogDebug("Removed file from watch list: {FilePath}", fullPath);
            }

            await Task.CompletedTask;
        }

        public async Task<IReadOnlyList<string>> GetWatchedFilesAsync()
        {
            await Task.CompletedTask;
            return _watchedFiles.ToList();
        }

        private async Task LoadExistingFilesAsync()
        {
            try
            {
                var markdownFiles = Directory.GetFiles(WatchedDirectory, MARKDOWN_PATTERN,
                    _fileWatcher!.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

                foreach (var file in markdownFiles)
                {
                    await AddFileToWatchAsync(file);
                }

                _logger.LogInformation("Loaded {Count} existing markdown files", markdownFiles.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load existing markdown files from: {Directory}", WatchedDirectory);
            }
        }

        private void OnFileCreated(object? sender, FileSystemEventArgs e)
        {
            _pendingEvents.Enqueue(e);
            RestartDebounceTimer();
        }

        private void OnFileChanged(object? sender, FileSystemEventArgs e)
        {
            _pendingEvents.Enqueue(e);
            RestartDebounceTimer();
        }

        private void OnFileDeleted(object? sender, FileSystemEventArgs e)
        {
            _pendingEvents.Enqueue(e);
            RestartDebounceTimer();
        }

        private void OnFileRenamed(object? sender, RenamedEventArgs e)
        {
            _pendingEvents.Enqueue(e);
            RestartDebounceTimer();
        }

        private void OnError(object? sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "FileSystemWatcher error occurred");
        }

        private void RestartDebounceTimer()
        {
            _debounceTimer.Change(DEBOUNCE_INTERVAL_MS, Timeout.Infinite);
        }

        private async void ProcessPendingEvents(object? state)
        {
            var processedFiles = new HashSet<string>();

            while (_pendingEvents.TryDequeue(out var eventArgs))
            {
                if (processedFiles.Contains(eventArgs.FullPath))
                    continue;

                processedFiles.Add(eventArgs.FullPath);

                try
                {
                    await ProcessFileEventAsync(eventArgs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process file event for: {FilePath}", eventArgs.FullPath);
                }
            }
        }

        private async Task ProcessFileEventAsync(FileSystemEventArgs eventArgs)
        {
            var filePath = eventArgs.FullPath;

            switch (eventArgs.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    await HandleFileCreatedAsync(filePath);
                    break;

                case WatcherChangeTypes.Changed:
                    await HandleFileChangedAsync(filePath);
                    break;

                case WatcherChangeTypes.Deleted:
                    await HandleFileDeletedAsync(filePath);
                    break;

                case WatcherChangeTypes.Renamed when eventArgs is RenamedEventArgs renamedArgs:
                    await HandleFileRenamedAsync(renamedArgs.OldFullPath, renamedArgs.FullPath);
                    break;
            }
        }

        private async Task HandleFileCreatedAsync(string filePath)
        {
            if (!IsMarkdownWorkflowFile(filePath))
                return;

            try
            {
                // Небольшая задержка для завершения записи файла
                await Task.Delay(100);

                var content = await File.ReadAllTextAsync(filePath);

                // Проверить, что это валидный workflow файл
                if (await IsValidWorkflowFileAsync(content))
                {
                    await AddFileToWatchAsync(filePath);

                    MarkdownFileCreated?.Invoke(this, new MarkdownFileCreatedEventArgs(filePath, content));

                    _logger.LogInformation("New markdown workflow file created: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to handle file creation: {FilePath}", filePath);
            }
        }

        private async Task HandleFileChangedAsync(string filePath)
        {
            if (!_watchedFiles.Contains(filePath))
                return;

            try
            {
                // Небольшая задержка для завершения записи файла
                await Task.Delay(100);

                var newContent = await File.ReadAllTextAsync(filePath);
                var previousContent = _fileContents.GetValueOrDefault(filePath);

                if (newContent != previousContent)
                {
                    _fileContents.AddOrUpdate(filePath, newContent, (_, _) => newContent);

                    MarkdownFileChanged?.Invoke(this, new MarkdownFileChangedEventArgs(
                        filePath, ChangeType.Modified, previousContent, newContent));

                    _logger.LogDebug("Markdown workflow file changed: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to handle file change: {FilePath}", filePath);
            }
        }

        private async Task HandleFileDeletedAsync(string filePath)
        {
            if (_watchedFiles.Contains(filePath))
            {
                await RemoveFileFromWatchAsync(filePath);

                MarkdownFileDeleted?.Invoke(this, new MarkdownFileDeletedEventArgs(filePath));

                _logger.LogInformation("Markdown workflow file deleted: {FilePath}", filePath);
            }
        }

        private async Task HandleFileRenamedAsync(string oldPath, string newPath)
        {
            if (_watchedFiles.Contains(oldPath))
            {
                var content = _fileContents.GetValueOrDefault(oldPath);

                await RemoveFileFromWatchAsync(oldPath);
                await AddFileToWatchAsync(newPath);

                MarkdownFileChanged?.Invoke(this, new MarkdownFileChangedEventArgs(
                    newPath, ChangeType.Renamed, content, content));

                _logger.LogInformation("Markdown workflow file renamed: {OldPath} -> {NewPath}", oldPath, newPath);
            }
        }

        private static bool IsMarkdownWorkflowFile(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".md", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> IsValidWorkflowFileAsync(string content)
        {
            try
            {
                var result = await _parser.ParseAsync(content);
                return result.IsSuccess && result.Workflow != null && !string.IsNullOrWhiteSpace(result.Workflow.Name);
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                StopWatchingAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during file watcher shutdown");
            }

            _debounceTimer?.Dispose();
            _fileWatcher?.Dispose();
            _watchedFiles?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}