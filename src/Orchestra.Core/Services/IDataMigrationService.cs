using Orchestra.Core.Models;

namespace Orchestra.Core.Services;

/// <summary>
/// Интерфейс сервиса миграции данных из JSON-файлов в базу данных SQLite
/// Обеспечивает перенос исторических данных оркестратора с валидацией и проверкой целостности
/// </summary>
public interface IDataMigrationService
{
    /// <summary>
    /// Выполнить миграцию данных из JSON файла состояния оркестратора в базу данных
    /// </summary>
    /// <param name="stateFilePath">Путь к файлу orchestrator-state.json</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат миграции с подробной статистикой</returns>
    Task<DataMigrationResult> MigrateFromJsonAsync(string stateFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверить, можно ли выполнить миграцию (проверка предварительных условий)
    /// </summary>
    /// <param name="stateFilePath">Путь к файлу состояния</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат проверки готовности к миграции</returns>
    Task<MigrationValidationResult> ValidateMigrationReadinessAsync(string stateFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Создать резервную копию текущей базы данных перед миграцией
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Путь к созданной резервной копии</returns>
    Task<string> CreateDatabaseBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверить целостность данных после миграции
    /// </summary>
    /// <param name="migrationResult">Результат миграции для проверки</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат проверки целостности данных</returns>
    Task<DataIntegrityResult> VerifyDataIntegrityAsync(DataMigrationResult migrationResult, CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат операции миграции данных
/// </summary>
public class DataMigrationResult
{
    /// <summary>
    /// Успешность миграции
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Сообщение об ошибке (если есть)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Время начала миграции
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Время завершения миграции
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Длительность миграции
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Количество мигрированных агентов
    /// </summary>
    public int MigratedAgentsCount { get; set; }

    /// <summary>
    /// Количество мигрированных задач
    /// </summary>
    public int MigratedTasksCount { get; set; }

    /// <summary>
    /// Количество мигрированных репозиториев
    /// </summary>
    public int MigratedRepositoriesCount { get; set; }

    /// <summary>
    /// Количество мигрированных пользовательских настроек
    /// </summary>
    public int MigratedUserPreferencesCount { get; set; }

    /// <summary>
    /// Путь к резервной копии оригинального JSON файла
    /// </summary>
    public string? BackupFilePath { get; set; }

    /// <summary>
    /// Подробные ошибки, возникшие во время миграции
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Идентификатор миграции для отслеживания
    /// </summary>
    public string MigrationId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Результат проверки готовности к миграции
/// </summary>
public class MigrationValidationResult
{
    /// <summary>
    /// Готовность к миграции
    /// </summary>
    public bool IsReady { get; set; }

    /// <summary>
    /// Список проблем, препятствующих миграции
    /// </summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>
    /// Предупреждения, не блокирующие миграцию
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Размер файла состояния в байтах
    /// </summary>
    public long StateFileSize { get; set; }

    /// <summary>
    /// Количество агентов в файле состояния
    /// </summary>
    public int SourceAgentsCount { get; set; }

    /// <summary>
    /// Количество задач в файле состояния
    /// </summary>
    public int SourceTasksCount { get; set; }

    /// <summary>
    /// Количество репозиториев в файле состояния
    /// </summary>
    public int SourceRepositoriesCount { get; set; }
}

/// <summary>
/// Результат проверки целостности данных
/// </summary>
public class DataIntegrityResult
{
    /// <summary>
    /// Успешность проверки целостности
    /// </summary>
    public bool IsIntegrityValid { get; set; }

    /// <summary>
    /// Ошибки целостности данных
    /// </summary>
    public List<string> IntegrityErrors { get; set; } = new();

    /// <summary>
    /// Количество агентов в базе данных после миграции
    /// </summary>
    public int DatabaseAgentsCount { get; set; }

    /// <summary>
    /// Количество задач в базе данных после миграции
    /// </summary>
    public int DatabaseTasksCount { get; set; }

    /// <summary>
    /// Количество репозиториев в базе данных после миграции
    /// </summary>
    public int DatabaseRepositoriesCount { get; set; }

    /// <summary>
    /// Проверенные внешние ключи
    /// </summary>
    public int ValidatedForeignKeysCount { get; set; }

    /// <summary>
    /// Время проверки целостности
    /// </summary>
    public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
}