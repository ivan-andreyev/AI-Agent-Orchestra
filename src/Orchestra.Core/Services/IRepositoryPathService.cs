using System;

namespace Orchestra.Core.Services;

/// <summary>
/// Сервис для управления путями к репозиториям
/// </summary>
public interface IRepositoryPathService
{
    /// <summary>
    /// Получает путь к репозиторию для выполнения команд
    /// </summary>
    /// <returns>Путь к репозиторию</returns>
    string GetRepositoryPath();

    /// <summary>
    /// Получает путь по умолчанию
    /// </summary>
    /// <returns>Путь по умолчанию</returns>
    string GetDefaultRepositoryPath();

    /// <summary>
    /// Проверяет корректность пути к репозиторию
    /// </summary>
    /// <param name="path">Путь для проверки</param>
    /// <returns>True, если путь корректен</returns>
    bool ValidateRepositoryPath(string path);

    /// <summary>
    /// Получает все доступные пути к репозиториям
    /// </summary>
    /// <returns>Словарь с именами и путями репозиториев</returns>
    Dictionary<string, string> GetAvailableRepositories();

    /// <summary>
    /// Устанавливает путь к репозиторию в качестве активного
    /// </summary>
    /// <param name="path">Путь к репозиторию</param>
    /// <returns>True, если путь успешно установлен</returns>
    bool SetActiveRepositoryPath(string path);
}