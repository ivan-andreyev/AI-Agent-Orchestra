using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Models;
using Orchestra.Core.Models.Migration;
using System.Text.Json;

namespace Orchestra.Core.Services;

/// <summary>
/// Сервис миграции данных из JSON-файлов состояния оркестратора в базу данных SQLite
/// Реализует алгоритм безопасной миграции с валидацией данных и проверкой целостности
/// </summary>
public class DataMigrationService : IDataMigrationService
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<DataMigrationService> _logger;

    public DataMigrationService(OrchestraDbContext context, ILogger<DataMigrationService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<DataMigrationResult> MigrateFromJsonAsync(string stateFilePath, CancellationToken cancellationToken = default)
    {
        var result = new DataMigrationResult
        {
            StartTime = DateTime.UtcNow
        };

        _logger.LogInformation("Starting data migration from {StateFilePath}, Migration ID: {MigrationId}",
            stateFilePath, result.MigrationId);

        try
        {
            // STEP 1: Validate migration prerequisites
            var validationResult = await ValidateMigrationReadinessAsync(stateFilePath, cancellationToken);
            if (!validationResult.IsReady)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = string.Join("; ", validationResult.Issues);
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // STEP 2: Create database backup
            try
            {
                result.BackupFilePath = await CreateDatabaseBackupAsync(cancellationToken);
                _logger.LogInformation("Database backup created: {BackupPath}", result.BackupFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create database backup, continuing with migration");
                result.Warnings.Add($"Database backup failed: {ex.Message}");
            }

            // STEP 3: Parse legacy data format
            var legacyState = await ParseLegacyStateAsync(stateFilePath, cancellationToken);
            if (legacyState == null)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = "Failed to parse legacy state file";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // STEP 4: Execute migration transaction
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // STEP 4.1: Migrate repositories first (no dependencies)
                result.MigratedRepositoriesCount = await MigrateRepositoriesAsync(legacyState.Repositories, cancellationToken);
                _logger.LogInformation("Migrated {Count} repositories", result.MigratedRepositoriesCount);

                // STEP 4.2: Migrate agents with repository references
                result.MigratedAgentsCount = await MigrateAgentsAsync(legacyState.Agents, cancellationToken);
                _logger.LogInformation("Migrated {Count} agents", result.MigratedAgentsCount);

                // STEP 4.3: Migrate tasks with agent and repository references
                result.MigratedTasksCount = await MigrateTasksAsync(legacyState.TaskQueue, cancellationToken);
                _logger.LogInformation("Migrated {Count} tasks", result.MigratedTasksCount);

                // STEP 4.4: Migrate user preferences and configurations
                result.MigratedUserPreferencesCount = await MigrateUserPreferencesAsync(legacyState.Settings, cancellationToken);
                _logger.LogInformation("Migrated {Count} user preferences", result.MigratedUserPreferencesCount);

                // STEP 5: Commit transaction if all validations pass
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Migration transaction committed successfully");

                result.IsSuccessful = true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Migration transaction failed, rolling back");

                result.IsSuccessful = false;
                result.ErrorMessage = $"Migration transaction failed: {ex.Message}";
            }

            // STEP 6: Backup original JSON file with timestamp
            if (result.IsSuccessful)
            {
                await BackupOriginalStateFileAsync(stateFilePath, result.MigrationId);
            }

            result.EndTime = DateTime.UtcNow;
            _logger.LogInformation("Data migration completed. Success: {IsSuccessful}, Duration: {Duration}ms",
                result.IsSuccessful, result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during data migration");

            result.IsSuccessful = false;
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            result.EndTime = DateTime.UtcNow;

            return result;
        }
    }

    /// <inheritdoc />
    public async Task<MigrationValidationResult> ValidateMigrationReadinessAsync(string stateFilePath, CancellationToken cancellationToken = default)
    {
        var result = new MigrationValidationResult();

        _logger.LogInformation("Validating migration readiness for {StateFilePath}", stateFilePath);

        // CHECK: State file exists and is readable
        if (!File.Exists(stateFilePath))
        {
            result.Issues.Add($"State file does not exist: {stateFilePath}");
            return result;
        }

        try
        {
            var fileInfo = new FileInfo(stateFilePath);
            result.StateFileSize = fileInfo.Length;

            if (result.StateFileSize == 0)
            {
                result.Issues.Add("State file is empty");
                return result;
            }

            if (result.StateFileSize > 100 * 1024 * 1024) // 100MB limit
            {
                result.Warnings.Add($"State file is very large ({result.StateFileSize / 1024 / 1024}MB), migration may take long time");
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Cannot access state file: {ex.Message}");
            return result;
        }

        // VERIFY: Database is accessible and schema is current
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                result.Issues.Add("Cannot connect to database");
                return result;
            }

            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                result.Issues.Add($"Database has pending migrations: {string.Join(", ", pendingMigrations)}");
                return result;
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Database validation failed: {ex.Message}");
            return result;
        }

        // ENSURE: No active tasks are running during migration
        try
        {
            var activeTasks = await _context.Tasks
                .Where(t => t.Status == Models.TaskStatus.InProgress || t.Status == Models.TaskStatus.Assigned)
                .CountAsync(cancellationToken);

            if (activeTasks > 0)
            {
                result.Warnings.Add($"There are {activeTasks} active tasks that will be migrated");
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Could not check active tasks: {ex.Message}");
        }

        // PARSE: Preview legacy data for validation
        try
        {
            var legacyState = await ParseLegacyStateAsync(stateFilePath, cancellationToken);
            if (legacyState != null)
            {
                result.SourceAgentsCount = legacyState.Agents.Count;
                result.SourceTasksCount = legacyState.TaskQueue.Count;
                result.SourceRepositoriesCount = legacyState.Repositories.Count;

                // Validate data structure
                if (result.SourceAgentsCount == 0 && result.SourceTasksCount == 0 && result.SourceRepositoriesCount == 0)
                {
                    result.Warnings.Add("State file appears to be empty or have no meaningful data");
                }

                // Check for duplicate IDs
                var duplicateAgentIds = legacyState.Agents.GroupBy(a => a.Key).Where(g => g.Count() > 1).Select(g => g.Key);
                if (duplicateAgentIds.Any())
                {
                    result.Issues.Add($"Duplicate agent IDs found: {string.Join(", ", duplicateAgentIds)}");
                }

                var duplicateTaskIds = legacyState.TaskQueue.GroupBy(t => t.Id).Where(g => g.Count() > 1).Select(g => g.Key);
                if (duplicateTaskIds.Any())
                {
                    result.Issues.Add($"Duplicate task IDs found: {string.Join(", ", duplicateTaskIds)}");
                }
            }
            else
            {
                result.Issues.Add("Failed to parse legacy state file structure");
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"State file parsing failed: {ex.Message}");
        }

        result.IsReady = !result.Issues.Any();

        _logger.LogInformation("Migration validation completed. Ready: {IsReady}, Issues: {IssueCount}, Warnings: {WarningCount}",
            result.IsReady, result.Issues.Count, result.Warnings.Count);

        return result;
    }

    /// <inheritdoc />
    public async Task<string> CreateDatabaseBackupAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating database backup");

        var connectionString = _context.Database.GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not available");
        }

        // Extract database file path from SQLite connection string
        var dataSourceStart = connectionString.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase);
        if (dataSourceStart == -1)
        {
            throw new InvalidOperationException("Cannot extract database file path from connection string");
        }

        var dataSourceValue = connectionString.Substring(dataSourceStart + "Data Source=".Length);
        var semicolonIndex = dataSourceValue.IndexOf(';');
        var dbFilePath = semicolonIndex > -1 ? dataSourceValue.Substring(0, semicolonIndex) : dataSourceValue;

        if (!File.Exists(dbFilePath))
        {
            throw new FileNotFoundException($"Database file not found: {dbFilePath}");
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"{Path.GetFileNameWithoutExtension(dbFilePath)}_backup_{timestamp}.db";
        var backupFilePath = Path.Combine(Path.GetDirectoryName(dbFilePath) ?? ".", backupFileName);

        await using var sourceStream = File.OpenRead(dbFilePath);
        await using var backupStream = File.Create(backupFilePath);
        await sourceStream.CopyToAsync(backupStream, cancellationToken);

        _logger.LogInformation("Database backup created: {BackupFilePath}", backupFilePath);
        return backupFilePath;
    }

    /// <inheritdoc />
    public async Task<DataIntegrityResult> VerifyDataIntegrityAsync(DataMigrationResult migrationResult, CancellationToken cancellationToken = default)
    {
        var result = new DataIntegrityResult();

        _logger.LogInformation("Verifying data integrity for migration {MigrationId}", migrationResult.MigrationId);

        try
        {
            // Count migrated records
            result.DatabaseAgentsCount = await _context.Agents.CountAsync(cancellationToken);
            result.DatabaseTasksCount = await _context.Tasks.CountAsync(cancellationToken);
            result.DatabaseRepositoriesCount = await _context.Repositories.CountAsync(cancellationToken);

            // Verify counts match
            if (result.DatabaseAgentsCount < migrationResult.MigratedAgentsCount)
            {
                result.IntegrityErrors.Add($"Agent count mismatch: expected {migrationResult.MigratedAgentsCount}, found {result.DatabaseAgentsCount}");
            }

            if (result.DatabaseTasksCount < migrationResult.MigratedTasksCount)
            {
                result.IntegrityErrors.Add($"Task count mismatch: expected {migrationResult.MigratedTasksCount}, found {result.DatabaseTasksCount}");
            }

            if (result.DatabaseRepositoriesCount < migrationResult.MigratedRepositoriesCount)
            {
                result.IntegrityErrors.Add($"Repository count mismatch: expected {migrationResult.MigratedRepositoriesCount}, found {result.DatabaseRepositoriesCount}");
            }

            // Check foreign key relationships
            var agentsWithInvalidRepo = await _context.Agents
                .Where(a => a.RepositoryId != null && a.Repository == null)
                .CountAsync(cancellationToken);

            if (agentsWithInvalidRepo > 0)
            {
                result.IntegrityErrors.Add($"Found {agentsWithInvalidRepo} agents with invalid repository references");
            }

            var tasksWithInvalidAgent = await _context.Tasks
                .Where(t => t.AgentId != null && t.Agent == null)
                .CountAsync(cancellationToken);

            if (tasksWithInvalidAgent > 0)
            {
                result.IntegrityErrors.Add($"Found {tasksWithInvalidAgent} tasks with invalid agent references");
            }

            var tasksWithInvalidRepo = await _context.Tasks
                .Where(t => t.RepositoryId != null && t.Repository == null)
                .CountAsync(cancellationToken);

            if (tasksWithInvalidRepo > 0)
            {
                result.IntegrityErrors.Add($"Found {tasksWithInvalidRepo} tasks with invalid repository references");
            }

            result.ValidatedForeignKeysCount = result.DatabaseAgentsCount + result.DatabaseTasksCount;
            result.IsIntegrityValid = !result.IntegrityErrors.Any();

            _logger.LogInformation("Data integrity verification completed. Valid: {IsValid}, Errors: {ErrorCount}",
                result.IsIntegrityValid, result.IntegrityErrors.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data integrity verification failed");
            result.IntegrityErrors.Add($"Integrity check failed: {ex.Message}");
            result.IsIntegrityValid = false;
            return result;
        }
    }

    #region Private Methods

    /// <summary>
    /// Парсит legacy JSON файл состояния в объектную модель
    /// </summary>
    private async Task<LegacyOrchestratorState?> ParseLegacyStateAsync(string stateFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var jsonText = await File.ReadAllTextAsync(stateFilePath, cancellationToken);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            var legacyState = JsonSerializer.Deserialize<LegacyOrchestratorState>(jsonText, options);

            _logger.LogInformation("Parsed legacy state: {AgentCount} agents, {TaskCount} tasks, {RepoCount} repositories",
                legacyState?.Agents.Count ?? 0, legacyState?.TaskQueue.Count ?? 0, legacyState?.Repositories.Count ?? 0);

            return legacyState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse legacy state file: {StateFilePath}", stateFilePath);
            return null;
        }
    }

    /// <summary>
    /// Мигрирует репозитории в базу данных
    /// </summary>
    private async Task<int> MigrateRepositoriesAsync(Dictionary<string, LegacyRepository> legacyRepositories, CancellationToken cancellationToken)
    {
        var migratedCount = 0;

        foreach (var kvp in legacyRepositories)
        {
            var legacyRepo = kvp.Value;

            // Check if repository already exists
            var existingRepo = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Path == legacyRepo.Path, cancellationToken);

            if (existingRepo != null)
            {
                _logger.LogDebug("Repository already exists: {Path}", legacyRepo.Path);
                continue;
            }

            var repository = new Repository
            {
                Id = string.IsNullOrEmpty(legacyRepo.Id) ? Guid.NewGuid().ToString() : legacyRepo.Id,
                Name = string.IsNullOrEmpty(legacyRepo.Name) ? Path.GetFileName(legacyRepo.Path) : legacyRepo.Name,
                Path = legacyRepo.Path,
                Description = legacyRepo.Description,
                Type = LegacyStatusMapping.MapRepositoryType(legacyRepo.Type),
                IsActive = legacyRepo.IsActive,
                CreatedAt = legacyRepo.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = legacyRepo.UpdatedAt ?? DateTime.UtcNow,
                LastAccessedAt = legacyRepo.LastAccessedAt,
                DefaultBranch = legacyRepo.DefaultBranch,
                AllowedOperations = legacyRepo.AllowedOperations ?? new List<string>(),
                TotalTasks = legacyRepo.TotalTasks,
                SuccessfulTasks = legacyRepo.SuccessfulTasks,
                FailedTasks = legacyRepo.FailedTasks,
                TotalExecutionTime = TimeSpan.Zero
            };

            if (legacyRepo.Settings != null)
            {
                repository.SettingsJson = JsonSerializer.Serialize(legacyRepo.Settings);
            }

            await _context.Repositories.AddAsync(repository, cancellationToken);
            migratedCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return migratedCount;
    }

    /// <summary>
    /// Мигрирует агентов в базу данных
    /// </summary>
    private async Task<int> MigrateAgentsAsync(Dictionary<string, LegacyAgent> legacyAgents, CancellationToken cancellationToken)
    {
        var migratedCount = 0;

        foreach (var kvp in legacyAgents)
        {
            var legacyAgent = kvp.Value;

            // Check if agent already exists
            var existingAgent = await _context.Agents
                .FirstOrDefaultAsync(a => a.Id == legacyAgent.Id, cancellationToken);

            if (existingAgent != null)
            {
                _logger.LogDebug("Agent already exists: {AgentId}", legacyAgent.Id);
                continue;
            }

            // Find repository by path
            var repository = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Path == legacyAgent.RepositoryPath, cancellationToken);

            var agent = new Agent
            {
                Id = legacyAgent.Id,
                Name = legacyAgent.Name,
                Type = legacyAgent.Type,
                RepositoryPath = legacyAgent.RepositoryPath,
                RepositoryId = repository?.Id,
                Status = LegacyStatusMapping.MapAgentStatus(legacyAgent.Status),
                LastPing = legacyAgent.LastPing,
                CurrentTask = legacyAgent.CurrentTask,
                SessionId = legacyAgent.SessionId ?? Guid.NewGuid().ToString(),
                CreatedAt = legacyAgent.LastActiveTime ?? DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                MaxConcurrentTasks = 1,
                HealthCheckInterval = TimeSpan.FromMinutes(1),
                TotalExecutionTime = TimeSpan.Zero,
                TotalTasksCompleted = legacyAgent.TasksCompleted
            };

            await _context.Agents.AddAsync(agent, cancellationToken);
            migratedCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return migratedCount;
    }

    /// <summary>
    /// Мигрирует задачи в базу данных
    /// </summary>
    private async Task<int> MigrateTasksAsync(List<LegacyTask> legacyTasks, CancellationToken cancellationToken)
    {
        var migratedCount = 0;

        foreach (var legacyTask in legacyTasks)
        {
            // Check if task already exists
            var existingTask = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == legacyTask.Id, cancellationToken);

            if (existingTask != null)
            {
                _logger.LogDebug("Task already exists: {TaskId}", legacyTask.Id);
                continue;
            }

            // Find agent and repository
            var agent = !string.IsNullOrEmpty(legacyTask.AgentId)
                ? await _context.Agents.FirstOrDefaultAsync(a => a.Id == legacyTask.AgentId, cancellationToken)
                : null;

            var repository = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Path == legacyTask.RepositoryPath, cancellationToken);

            var task = new TaskRecord
            {
                Id = legacyTask.Id,
                AgentId = legacyTask.AgentId,
                Command = legacyTask.Command,
                RepositoryPath = legacyTask.RepositoryPath,
                RepositoryId = repository?.Id,
                Priority = LegacyStatusMapping.MapTaskPriority(legacyTask.Priority),
                Status = LegacyStatusMapping.MapTaskStatus(legacyTask.Status),
                CreatedAt = legacyTask.CreatedAt,
                StartedAt = legacyTask.StartedAt,
                CompletedAt = legacyTask.CompletedAt,
                UpdatedAt = DateTime.UtcNow,
                Result = legacyTask.Result,
                ErrorMessage = legacyTask.ErrorMessage,
                RetryCount = legacyTask.RetryCount,
                CorrelationId = legacyTask.CorrelationId,
                WorkflowId = legacyTask.WorkflowId,
                WorkflowStep = legacyTask.WorkflowStep
            };

            // Calculate execution duration if both start and completion times are available
            if (task.StartedAt.HasValue && task.CompletedAt.HasValue)
            {
                task.ExecutionDuration = task.CompletedAt.Value - task.StartedAt.Value;
            }

            await _context.Tasks.AddAsync(task, cancellationToken);
            migratedCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return migratedCount;
    }

    /// <summary>
    /// Мигрирует пользовательские настройки
    /// </summary>
    private async Task<int> MigrateUserPreferencesAsync(Dictionary<string, object>? legacySettings, CancellationToken cancellationToken)
    {
        if (legacySettings == null || !legacySettings.Any())
        {
            return 0;
        }

        var migratedCount = 0;
        const string defaultUserId = "system";

        foreach (var kvp in legacySettings)
        {
            // Check if preference already exists
            var existingPreference = await _context.UserPreferences
                .FirstOrDefaultAsync(p => p.UserId == defaultUserId && p.Key == kvp.Key, cancellationToken);

            if (existingPreference != null)
            {
                _logger.LogDebug("User preference already exists: {Key}", kvp.Key);
                continue;
            }

            var preference = new UserPreference
            {
                Id = Guid.NewGuid().ToString(),
                UserId = defaultUserId,
                Key = kvp.Key,
                Value = JsonSerializer.Serialize(kvp.Value),
                Type = Orchestra.Core.Data.Entities.PreferenceType.UI,
                Category = "Migration",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.UserPreferences.AddAsync(preference, cancellationToken);
            migratedCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return migratedCount;
    }

    /// <summary>
    /// Создает резервную копию оригинального JSON файла с идентификатором миграции
    /// </summary>
    private async Task BackupOriginalStateFileAsync(string stateFilePath, string migrationId)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"{Path.GetFileNameWithoutExtension(stateFilePath)}_migrated_{timestamp}_{migrationId[..8]}.json";
            var backupFilePath = Path.Combine(Path.GetDirectoryName(stateFilePath) ?? ".", backupFileName);

            File.Copy(stateFilePath, backupFilePath);

            _logger.LogInformation("Original state file backed up: {BackupFilePath}", backupFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to backup original state file");
        }
    }

    #endregion
}