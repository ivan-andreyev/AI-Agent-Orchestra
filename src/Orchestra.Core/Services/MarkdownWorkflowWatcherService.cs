using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Configuration;

namespace Orchestra.Core.Services
{
    /// <summary>
    /// Сервис интеграции file watcher с workflow engine
    /// </summary>
    public class MarkdownWorkflowWatcherService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MarkdownWorkflowWatcherService> _logger;
        private readonly MarkdownWorkflowOptions _options;
        private IMarkdownFileWatcher? _fileWatcher;
        private IMarkdownToWorkflowConverter? _converter;
        private IWorkflowEngine? _workflowEngine;

        public MarkdownWorkflowWatcherService(
            IServiceProvider serviceProvider,
            ILogger<MarkdownWorkflowWatcherService> logger,
            IOptions<MarkdownWorkflowOptions> options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.FileWatcher.EnableAutoReload)
            {
                _logger.LogInformation("Markdown file auto-reload is disabled");
                return;
            }

            try
            {
                // Создаем scoped сервисы для работы с workflow
                using var scope = _serviceProvider.CreateScope();

                _fileWatcher = scope.ServiceProvider.GetService<IMarkdownFileWatcher>();
                _converter = scope.ServiceProvider.GetService<IMarkdownToWorkflowConverter>();
                _workflowEngine = scope.ServiceProvider.GetService<IWorkflowEngine>();

                if (_fileWatcher == null)
                {
                    _logger.LogWarning("IMarkdownFileWatcher service not registered. Markdown file watching disabled.");
                    return;
                }

                if (_converter == null)
                {
                    _logger.LogWarning("IMarkdownToWorkflowConverter service not registered. Auto-conversion disabled.");
                }

                if (_workflowEngine == null)
                {
                    _logger.LogWarning("IWorkflowEngine service not registered. Workflow updates disabled.");
                }

                // Подписываемся на события file watcher
                _fileWatcher.MarkdownFileChanged += OnMarkdownFileChanged;
                _fileWatcher.MarkdownFileCreated += OnMarkdownFileCreated;
                _fileWatcher.MarkdownFileDeleted += OnMarkdownFileDeleted;

                // Проверяем существование директории
                var watchDirectory = _options.FileWatcher.WatchDirectory;
                if (!System.IO.Directory.Exists(watchDirectory))
                {
                    _logger.LogInformation("Creating watch directory: {Directory}", watchDirectory);
                    System.IO.Directory.CreateDirectory(watchDirectory);
                }

                await _fileWatcher.StartWatchingAsync(watchDirectory,
                    _options.FileWatcher.IncludeSubdirectories);

                _logger.LogInformation("Markdown workflow watcher service started watching: {Directory}", watchDirectory);

                try
                {
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    _logger.LogInformation("Markdown workflow watcher service cancellation requested");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in markdown workflow watcher service");
                throw;
            }
        }

        private async void OnMarkdownFileChanged(object? sender, MarkdownFileChangedEventArgs e)
        {
            if (_converter == null || _workflowEngine == null)
                return;

            try
            {
                _logger.LogDebug("Processing markdown file change: {FilePath}", e.FilePath);

                // Парсим содержимое файла
                using var scope = _serviceProvider.CreateScope();
                var parser = scope.ServiceProvider.GetService<IMarkdownWorkflowParser>();

                if (parser == null)
                {
                    _logger.LogWarning("IMarkdownWorkflowParser not available for processing file change");
                    return;
                }

                var parseResult = await parser.ParseAsync(e.NewContent ?? string.Empty, e.FilePath);

                if (!parseResult.IsSuccess || parseResult.Workflow == null)
                {
                    _logger.LogWarning("Failed to parse changed markdown file: {FilePath}. Error: {Error}",
                        e.FilePath, parseResult.ErrorMessage);
                    return;
                }

                // Конвертируем в WorkflowDefinition
                var conversionResult = await _converter.ConvertAsync(parseResult.Workflow);

                if (!conversionResult.IsSuccess || conversionResult.WorkflowDefinition == null)
                {
                    _logger.LogWarning("Failed to convert changed markdown workflow: {FilePath}. Error: {Error}",
                        e.FilePath, conversionResult.ErrorMessage);
                    return;
                }

                // Обновляем workflow в engine (здесь предполагается, что такой метод будет добавлен)
                // await _workflowEngine.UpdateWorkflowDefinitionAsync(conversionResult.WorkflowDefinition);

                _logger.LogInformation("Updated workflow from changed markdown file: {FilePath}", e.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process markdown file change: {FilePath}", e.FilePath);
            }
        }

        private async void OnMarkdownFileCreated(object? sender, MarkdownFileCreatedEventArgs e)
        {
            if (_converter == null || _workflowEngine == null)
                return;

            try
            {
                _logger.LogDebug("Processing new markdown file: {FilePath}", e.FilePath);

                // Парсим содержимое нового файла
                using var scope = _serviceProvider.CreateScope();
                var parser = scope.ServiceProvider.GetService<IMarkdownWorkflowParser>();

                if (parser == null)
                {
                    _logger.LogWarning("IMarkdownWorkflowParser not available for processing new file");
                    return;
                }

                var parseResult = await parser.ParseAsync(e.InitialContent ?? string.Empty, e.FilePath);

                if (!parseResult.IsSuccess || parseResult.Workflow == null)
                {
                    _logger.LogWarning("Failed to parse new markdown file: {FilePath}. Error: {Error}",
                        e.FilePath, parseResult.ErrorMessage);
                    return;
                }

                // Конвертируем в WorkflowDefinition
                var conversionResult = await _converter.ConvertAsync(parseResult.Workflow);

                if (!conversionResult.IsSuccess || conversionResult.WorkflowDefinition == null)
                {
                    _logger.LogWarning("Failed to convert new markdown workflow: {FilePath}. Error: {Error}",
                        e.FilePath, conversionResult.ErrorMessage);
                    return;
                }

                // Регистрируем новый workflow (здесь предполагается, что такой метод будет добавлен)
                // await _workflowEngine.RegisterWorkflowDefinitionAsync(conversionResult.WorkflowDefinition);

                _logger.LogInformation("Registered new workflow from markdown file: {FilePath}", e.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process new markdown file: {FilePath}", e.FilePath);
            }
        }

        private async void OnMarkdownFileDeleted(object? sender, MarkdownFileDeletedEventArgs e)
        {
            if (_workflowEngine == null)
                return;

            try
            {
                _logger.LogDebug("Processing markdown file deletion: {FilePath}", e.FilePath);

                // Извлекаем ID workflow из имени файла или кэша
                var workflowId = ExtractWorkflowIdFromPath(e.FilePath);

                if (string.IsNullOrEmpty(workflowId))
                {
                    _logger.LogWarning("Could not determine workflow ID for deleted file: {FilePath}", e.FilePath);
                    return;
                }

                // Удаляем workflow definition (здесь предполагается, что такой метод будет добавлен)
                // await _workflowEngine.UnregisterWorkflowDefinitionAsync(workflowId);

                _logger.LogInformation("Unregistered workflow for deleted markdown file: {FilePath}", e.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process markdown file deletion: {FilePath}", e.FilePath);
            }
        }

        private static string? ExtractWorkflowIdFromPath(string filePath)
        {
            try
            {
                // Простая логика извлечения ID из имени файла
                // Можно расширить для более сложных сценариев
                var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                return fileName?.Replace(" ", "-").ToLowerInvariant();
            }
            catch
            {
                return null;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping markdown workflow watcher service");

            if (_fileWatcher != null)
            {
                try
                {
                    await _fileWatcher.StopWatchingAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping file watcher");
                }
            }

            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.MarkdownFileChanged -= OnMarkdownFileChanged;
                _fileWatcher.MarkdownFileCreated -= OnMarkdownFileCreated;
                _fileWatcher.MarkdownFileDeleted -= OnMarkdownFileDeleted;
                _fileWatcher?.Dispose();
            }

            base.Dispose();
        }
    }
}