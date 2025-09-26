using Microsoft.AspNetCore.Mvc;
using Orchestra.Core.Services;

namespace Orchestra.API.Controllers;

/// <summary>
/// Контроллер для управления миграцией данных из JSON файлов в базу данных SQLite
/// Обеспечивает API для выполнения, мониторинга и валидации миграции данных оркестратора
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DataMigrationController : ControllerBase
{
    private readonly IDataMigrationService _migrationService;
    private readonly ILogger<DataMigrationController> _logger;

    public DataMigrationController(
        IDataMigrationService migrationService,
        ILogger<DataMigrationController> logger)
    {
        _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Проверить готовность системы к миграции данных
    /// </summary>
    /// <param name="stateFilePath">Путь к файлу orchestrator-state.json</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат проверки готовности к миграции</returns>
    [HttpPost("validate")]
    public async Task<ActionResult<MigrationValidationResult>> ValidateMigrationReadiness(
        [FromBody] string stateFilePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Validating migration readiness for file: {StateFilePath}", stateFilePath);

            if (string.IsNullOrWhiteSpace(stateFilePath))
            {
                return BadRequest("State file path is required");
            }

            var result = await _migrationService.ValidateMigrationReadinessAsync(stateFilePath, cancellationToken);

            if (result.IsReady)
            {
                _logger.LogInformation("Migration validation successful: {AgentCount} agents, {TaskCount} tasks, {RepoCount} repositories",
                    result.SourceAgentsCount, result.SourceTasksCount, result.SourceRepositoriesCount);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Migration validation failed: {IssueCount} issues found", result.Issues.Count);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during migration validation");
            return StatusCode(500, $"Migration validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Выполнить миграцию данных из JSON файла в базу данных
    /// </summary>
    /// <param name="request">Параметры миграции</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат миграции с подробной статистикой</returns>
    [HttpPost("migrate")]
    public async Task<ActionResult<DataMigrationResult>> MigrateFromJson(
        [FromBody] MigrationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting data migration from file: {StateFilePath}", request.StateFilePath);

            if (string.IsNullOrWhiteSpace(request.StateFilePath))
            {
                return BadRequest("State file path is required");
            }

            // Validate migration readiness first if requested
            if (request.ValidateFirst)
            {
                var validationResult = await _migrationService.ValidateMigrationReadinessAsync(request.StateFilePath, cancellationToken);
                if (!validationResult.IsReady)
                {
                    _logger.LogWarning("Migration pre-validation failed: {IssueCount} issues", validationResult.Issues.Count);
                    return BadRequest(new
                    {
                        Message = "Migration pre-validation failed",
                        ValidationResult = validationResult
                    });
                }
            }

            var result = await _migrationService.MigrateFromJsonAsync(request.StateFilePath, cancellationToken);

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Migration completed successfully: {AgentCount} agents, {TaskCount} tasks, {RepoCount} repositories migrated in {Duration}ms",
                    result.MigratedAgentsCount, result.MigratedTasksCount, result.MigratedRepositoriesCount, result.Duration.TotalMilliseconds);

                // Verify data integrity if requested
                if (request.VerifyIntegrity)
                {
                    var integrityResult = await _migrationService.VerifyDataIntegrityAsync(result, cancellationToken);
                    if (!integrityResult.IsIntegrityValid)
                    {
                        _logger.LogWarning("Data integrity verification failed after migration");
                        return Ok(new
                        {
                            MigrationResult = result,
                            IntegrityResult = integrityResult,
                            Warning = "Migration completed but data integrity issues were found"
                        });
                    }
                }

                return Ok(result);
            }
            else
            {
                _logger.LogError("Migration failed: {ErrorMessage}", result.ErrorMessage);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during data migration");
            return StatusCode(500, $"Migration failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Создать резервную копию текущей базы данных
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Путь к созданной резервной копии</returns>
    [HttpPost("backup")]
    public async Task<ActionResult<string>> CreateDatabaseBackup(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating database backup");

            var backupPath = await _migrationService.CreateDatabaseBackupAsync(cancellationToken);

            _logger.LogInformation("Database backup created: {BackupPath}", backupPath);
            return Ok(backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating database backup");
            return StatusCode(500, $"Backup creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Проверить целостность данных в базе данных
    /// </summary>
    /// <param name="migrationId">Идентификатор миграции для проверки (опционально)</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат проверки целостности данных</returns>
    [HttpPost("verify-integrity")]
    public async Task<ActionResult<DataIntegrityResult>> VerifyDataIntegrity(
        [FromBody] string? migrationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Verifying data integrity for migration: {MigrationId}", migrationId ?? "current");

            // Create a dummy migration result for verification
            var migrationResult = new DataMigrationResult
            {
                MigrationId = migrationId ?? "manual-verification",
                IsSuccessful = true,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow,
                MigratedAgentsCount = 0, // Will be counted during verification
                MigratedTasksCount = 0,
                MigratedRepositoriesCount = 0,
                MigratedUserPreferencesCount = 0
            };

            var result = await _migrationService.VerifyDataIntegrityAsync(migrationResult, cancellationToken);

            if (result.IsIntegrityValid)
            {
                _logger.LogInformation("Data integrity verification passed: {AgentCount} agents, {TaskCount} tasks, {RepoCount} repositories",
                    result.DatabaseAgentsCount, result.DatabaseTasksCount, result.DatabaseRepositoriesCount);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Data integrity verification failed: {ErrorCount} errors found", result.IntegrityErrors.Count);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data integrity verification");
            return StatusCode(500, $"Integrity verification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Получить текущий статус миграции (заглушка для будущего расширения)
    /// </summary>
    /// <returns>Информация о статусе миграции</returns>
    [HttpGet("status")]
    public ActionResult<object> GetMigrationStatus()
    {
        try
        {
            // TODO: Implement migration status tracking for long-running operations
            var status = new
            {
                IsRunning = false,
                LastMigration = (DateTime?)null,
                DatabaseVersion = "1.0",
                MigrationHistory = new List<object>() // TODO: Track migration history
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migration status");
            return StatusCode(500, $"Status retrieval failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Запрос на выполнение миграции данных
/// </summary>
public class MigrationRequest
{
    /// <summary>
    /// Путь к файлу orchestrator-state.json
    /// </summary>
    public string StateFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Выполнить предварительную валидацию перед миграцией
    /// </summary>
    public bool ValidateFirst { get; set; } = true;

    /// <summary>
    /// Проверить целостность данных после миграции
    /// </summary>
    public bool VerifyIntegrity { get; set; } = true;

    /// <summary>
    /// Принудительно выполнить миграцию даже при наличии предупреждений
    /// </summary>
    public bool ForceOverride { get; set; } = false;
}