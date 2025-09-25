using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace Orchestra.Core.Services;

/// <summary>
/// Реализация сервиса для управления путями к репозиториям с поддержкой конфигурации и валидации
/// </summary>
public class RepositoryPathService : IRepositoryPathService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RepositoryPathService> _logger;
    private string? _activeRepositoryPath;

    public RepositoryPathService(IConfiguration configuration, ILogger<RepositoryPathService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Получает путь к репозиторию для выполнения команд
    /// </summary>
    public string GetRepositoryPath()
    {
        try
        {
            // 1. Приоритет: Активно установленный путь
            if (!string.IsNullOrWhiteSpace(_activeRepositoryPath) && ValidateRepositoryPath(_activeRepositoryPath))
            {
                _logger.LogDebug("Using active repository path: {Path}", _activeRepositoryPath);
                return _activeRepositoryPath;
            }

            // 2. Путь из конфигурации
            var configPath = _configuration["AgentSettings:RepositoryPath"];
            if (!string.IsNullOrWhiteSpace(configPath) && ValidateRepositoryPath(configPath))
            {
                _logger.LogDebug("Using configuration repository path: {Path}", configPath);
                return configPath;
            }

            // 3. Fallback: текущая директория
            var defaultPath = GetDefaultRepositoryPath();
            _logger.LogWarning("No valid repository path found in configuration, using default: {Path}", defaultPath);
            return defaultPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repository path, falling back to default");
            return GetDefaultRepositoryPath();
        }
    }

    /// <summary>
    /// Получает путь по умолчанию
    /// </summary>
    public string GetDefaultRepositoryPath()
    {
        try
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            _logger.LogDebug("Default repository path: {Path}", currentDirectory);
            return currentDirectory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current directory, using root directory");
            return Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        }
    }

    /// <summary>
    /// Проверяет корректность пути к репозиторию
    /// </summary>
    public bool ValidateRepositoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogDebug("Repository path validation failed: path is null or empty");
            return false;
        }

        try
        {
            // Проверяем существование директории
            if (!Directory.Exists(path))
            {
                _logger.LogDebug("Repository path validation failed: directory does not exist - {Path}", path);
                return false;
            }

            // Проверяем права доступа на чтение/запись
            var testFilePath = Path.Combine(path, $".test_access_{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(testFilePath, "test");
                File.Delete(testFilePath);
            }
            catch
            {
                _logger.LogDebug("Repository path validation failed: no write access - {Path}", path);
                return false;
            }

            _logger.LogDebug("Repository path validation passed: {Path}", path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Repository path validation failed with exception - {Path}", path);
            return false;
        }
    }

    /// <summary>
    /// Получает все доступные пути к репозиториям
    /// </summary>
    public Dictionary<string, string> GetAvailableRepositories()
    {
        var repositories = new Dictionary<string, string>();

        try
        {
            // Добавляем путь из конфигурации
            var configPath = _configuration["AgentSettings:RepositoryPath"];
            if (!string.IsNullOrWhiteSpace(configPath) && ValidateRepositoryPath(configPath))
            {
                repositories["Configuration"] = configPath;
            }

            // Добавляем текущую директорию
            var currentPath = GetDefaultRepositoryPath();
            if (ValidateRepositoryPath(currentPath))
            {
                repositories["Current"] = currentPath;
            }

            // Добавляем дополнительные пути из конфигурации
            var additionalPaths = _configuration.GetSection("AgentSettings:AdditionalRepositoryPaths");
            if (additionalPaths.Exists())
            {
                var index = 0;
                foreach (var pathConfig in additionalPaths.GetChildren())
                {
                    var path = pathConfig.Value;
                    if (!string.IsNullOrWhiteSpace(path) && ValidateRepositoryPath(path))
                    {
                        repositories[$"Additional_{index++}"] = path;
                    }
                }
            }

            _logger.LogInformation("Found {Count} available repositories", repositories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available repositories");
        }

        return repositories;
    }

    /// <summary>
    /// Устанавливает путь к репозиторию в качестве активного
    /// </summary>
    public bool SetActiveRepositoryPath(string path)
    {
        try
        {
            if (!ValidateRepositoryPath(path))
            {
                _logger.LogWarning("Cannot set invalid repository path as active: {Path}", path);
                return false;
            }

            var previousPath = _activeRepositoryPath;
            _activeRepositoryPath = path;

            _logger.LogInformation("Active repository path changed from {PreviousPath} to {NewPath}",
                previousPath ?? "[none]", path);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active repository path: {Path}", path);
            return false;
        }
    }
}