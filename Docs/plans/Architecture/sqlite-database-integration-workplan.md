# SQLite Database Integration Work Plan

## Executive Summary

Implement comprehensive SQLite database integration using Entity Framework Core to replace file-based storage with persistent, queryable data management for agent configurations, task history, user preferences, and orchestration state. This foundation supports all other features while maintaining the lightweight, embedded database approach suitable for desktop deployment.

## Current State Analysis

**Existing Data Storage:**
- JSON file persistence (`orchestrator-state.json`)
- In-memory agent and task collections
- Simple serialization for state snapshots
- No relational data modeling or querying capabilities
- Limited data integrity and transaction support

**Data Currently Managed:**
- Agent registrations and status
- Task queue and execution history
- Repository information
- Basic orchestration state

**Limitations:**
- No data relationships or referential integrity
- Limited querying and reporting capabilities
- Manual file handling and concurrency issues
- No data migration or versioning strategy
- Inefficient for complex queries and analytics

## Target Architecture

### 1. Database Schema Design

**Core Entities:**
```
Agents
├── Agent configurations and capabilities
├── Connection status and health metrics
├── Performance statistics
└── Repository assignments

Tasks
├── Task definitions and parameters
├── Execution history and results
├── Dependencies and workflows
└── Performance metrics

Repositories
├── Repository metadata and settings
├── Agent assignments and preferences
├── Access permissions and constraints
└── Performance baselines

UserPreferences
├── UI customization settings
├── Default task templates
├── Notification preferences
└── Dashboard layouts

OrchestrationLogs
├── System events and state changes
├── Performance monitoring data
├── Error logs and diagnostics
└── Audit trails
```

### 2. Entity Framework Core Architecture

**DbContext Structure:**
```csharp
public class OrchestraDbContext : DbContext
{
    public DbSet<Agent> Agents { get; set; }
    public DbSet<TaskRecord> Tasks { get; set; }
    public DbSet<Repository> Repositories { get; set; }
    public DbSet<UserPreference> UserPreferences { get; set; }
    public DbSet<OrchestrationLog> OrchestrationLogs { get; set; }
    public DbSet<TaskTemplate> TaskTemplates { get; set; }
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
    public DbSet<PerformanceMetric> PerformanceMetrics { get; set; }
}
```

**Repository Pattern Implementation:**
- Generic repository for common CRUD operations
- Specialized repositories for complex domain logic
- Unit of Work pattern for transaction management
- Caching layer for frequently accessed data

### 3. Data Access Layer

**Service Architecture:**
```
Data Access Layer
├── OrchestraDbContext (EF Core)
├── GenericRepository<T> (Base CRUD)
├── AgentRepository (Agent-specific operations)
├── TaskRepository (Task querying and analytics)
├── RepositoryService (Repository management)
└── UserPreferenceService (Settings management)

Business Logic Layer
├── AgentManagementService
├── TaskOrchestrationService
├── RepositoryManagementService
├── UserSettingsService
└── AnalyticsService

API Layer
├── AgentsController
├── TasksController
├── RepositoriesController
└── SettingsController
```

## Implementation Phases

### Phase 1: Database Foundation (Estimated: 12-16 hours)

**Tasks:**
1. **Install and Configure Entity Framework**
   - Add EF Core SQLite packages to Orchestra.Core
   - Create OrchestraDbContext with core entities
   - Configure connection strings and database location
   - **Acceptance Criteria**: Database created and accessible via EF Core

2. **Design Core Entity Models**
   - Define Agent, Task, Repository, and UserPreference entities
   - Implement relationships and constraints
   - Add audit fields and soft delete support
   - **Acceptance Criteria**: All entities properly mapped with relationships

3. **Implement Repository Pattern**
   - Create generic repository interface and implementation
   - Implement specialized repositories for each entity
   - Add Unit of Work pattern for transaction management
   - **Acceptance Criteria**: CRUD operations work through repository layer

**Technical Implementation:**

**Entity Models:**
```csharp
public class Agent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string RepositoryPath { get; set; } = string.Empty;
    public AgentStatus Status { get; set; }
    public DateTime LastPing { get; set; }
    public string? CurrentTask { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Configuration
    public string? ConfigurationJson { get; set; }
    public int MaxConcurrentTasks { get; set; } = 1;
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    // Performance Metrics
    public int TotalTasksCompleted { get; set; }
    public int TotalTasksFailed { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public double AverageExecutionTime { get; set; }

    // Navigation Properties
    public Repository? Repository { get; set; }
    public string? RepositoryId { get; set; }
    public ICollection<TaskRecord> AssignedTasks { get; set; } = new List<TaskRecord>();
    public ICollection<PerformanceMetric> PerformanceMetrics { get; set; } = new List<PerformanceMetric>();
}

public class TaskRecord
{
    public string Id { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string? AgentId { get; set; }
    public string RepositoryPath { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; }
    public TaskStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? ExecutionDuration { get; set; }

    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string? CorrelationId { get; set; }

    // Workflow Support
    public string? WorkflowId { get; set; }
    public string? ParentTaskId { get; set; }
    public int WorkflowStep { get; set; }

    // Navigation Properties
    public Agent? Agent { get; set; }
    public Repository? Repository { get; set; }
    public string? RepositoryId { get; set; }
    public WorkflowDefinition? Workflow { get; set; }
    public TaskRecord? ParentTask { get; set; }
    public ICollection<TaskRecord> ChildTasks { get; set; } = new List<TaskRecord>();
}

public class Repository
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RepositoryType Type { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }

    // Configuration
    public string? SettingsJson { get; set; }
    public string? DefaultBranch { get; set; }
    public List<string> AllowedOperations { get; set; } = new();

    // Statistics
    public int TotalTasks { get; set; }
    public int SuccessfulTasks { get; set; }
    public int FailedTasks { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }

    // Navigation Properties
    public ICollection<Agent> Agents { get; set; } = new List<Agent>();
    public ICollection<TaskRecord> Tasks { get; set; } = new List<TaskRecord>();
    public ICollection<UserPreference> UserPreferences { get; set; } = new List<UserPreference>();
}

public class UserPreference
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = "default"; // Support for future multi-user
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public PreferenceType Type { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Repository-specific preferences
    public string? RepositoryId { get; set; }
    public Repository? Repository { get; set; }
}
```

**DbContext Configuration:**
```csharp
public class OrchestraDbContext : DbContext
{
    public OrchestraDbContext(DbContextOptions<OrchestraDbContext> options) : base(options) { }

    public DbSet<Agent> Agents { get; set; }
    public DbSet<TaskRecord> Tasks { get; set; }
    public DbSet<Repository> Repositories { get; set; }
    public DbSet<UserPreference> UserPreferences { get; set; }
    public DbSet<OrchestrationLog> OrchestrationLogs { get; set; }
    public DbSet<TaskTemplate> TaskTemplates { get; set; }
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
    public DbSet<PerformanceMetric> PerformanceMetrics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Agent Configuration
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.RepositoryPath);
            entity.HasIndex(e => new { e.Status, e.RepositoryPath });

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RepositoryPath).HasMaxLength(500).IsRequired();

            entity.HasOne(e => e.Repository)
                  .WithMany(r => r.Agents)
                  .HasForeignKey(e => e.RepositoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Task Configuration
        modelBuilder.Entity<TaskRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Status, e.Priority });
            entity.HasIndex(e => new { e.AgentId, e.Status });

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Command).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.RepositoryPath).HasMaxLength(500).IsRequired();

            entity.HasOne(e => e.Agent)
                  .WithMany(a => a.AssignedTasks)
                  .HasForeignKey(e => e.AgentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Repository)
                  .WithMany(r => r.Tasks)
                  .HasForeignKey(e => e.RepositoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ParentTask)
                  .WithMany(t => t.ChildTasks)
                  .HasForeignKey(e => e.ParentTaskId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Repository Configuration
        modelBuilder.Entity<Repository>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Path).IsUnique();
            entity.HasIndex(e => e.IsActive);

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Path).HasMaxLength(500).IsRequired();

            entity.Property(e => e.AllowedOperations)
                  .HasConversion(
                      v => string.Join(',', v),
                      v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
        });

        // User Preferences Configuration
        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.Key, e.RepositoryId }).IsUnique();

            entity.Property(e => e.Key).HasMaxLength(255).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(128).IsRequired();

            entity.HasOne(e => e.Repository)
                  .WithMany(r => r.UserPreferences)
                  .HasForeignKey(e => e.RepositoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is ITimestamped &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (ITimestamped)entry.Entity;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
            }

            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
```

### Phase 2: Repository Layer Implementation (Estimated: 10-14 hours)

**Tasks:**
1. **Implement Generic Repository Pattern**
   - Create base repository interface and implementation
   - Add async operations and cancellation support
   - Implement specification pattern for complex queries
   - **Acceptance Criteria**: Generic CRUD operations available for all entities

2. **Create Specialized Repositories**
   - AgentRepository with agent-specific queries
   - TaskRepository with analytics and reporting
   - RepositoryService with configuration management
   - **Acceptance Criteria**: Domain-specific operations properly encapsulated

3. **Add Caching and Performance Optimization**
   - Implement memory caching for frequently accessed data
   - Add query optimization and eager loading strategies
   - Create database connection pooling
   - **Acceptance Criteria**: Repository operations perform within acceptable limits

**Repository Implementation:**
```csharp
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
}

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly OrchestraDbContext _context;
    protected readonly DbSet<T> _dbSet;
    protected readonly IMemoryCache _cache;
    protected readonly ILogger _logger;

    public GenericRepository(OrchestraDbContext context, IMemoryCache cache, ILogger logger)
    {
        _context = context;
        _dbSet = context.Set<T>();
        _cache = cache;
        _logger = logger;
    }

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{typeof(T).Name}_{id}";

        if (_cache.TryGetValue(cacheKey, out T? cachedEntity))
        {
            return cachedEntity;
        }

        var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);

        if (entity != null)
        {
            _cache.Set(cacheKey, entity, TimeSpan.FromMinutes(5));
        }

        return entity;
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        InvalidateCache(entity);
        await Task.CompletedTask;
    }

    private void InvalidateCache(T entity)
    {
        if (entity is IIdentifiable identifiable)
        {
            var cacheKey = $"{typeof(T).Name}_{identifiable.Id}";
            _cache.Remove(cacheKey);
        }
    }
}

public interface IAgentRepository : IGenericRepository<Agent>
{
    Task<IEnumerable<Agent>> GetAvailableAgentsAsync(string? repositoryPath = null, CancellationToken cancellationToken = default);
    Task<Agent?> GetBestAvailableAgentAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<IEnumerable<Agent>> GetAgentsByStatusAsync(AgentStatus status, CancellationToken cancellationToken = default);
    Task<AgentStatistics> GetAgentStatisticsAsync(string agentId, CancellationToken cancellationToken = default);
    Task UpdateAgentStatusAsync(string agentId, AgentStatus status, string? currentTask = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Agent>> GetStaleAgentsAsync(TimeSpan threshold, CancellationToken cancellationToken = default);
}

public class AgentRepository : GenericRepository<Agent>, IAgentRepository
{
    public AgentRepository(OrchestraDbContext context, IMemoryCache cache, ILogger<AgentRepository> logger)
        : base(context, cache, logger) { }

    public async Task<IEnumerable<Agent>> GetAvailableAgentsAsync(string? repositoryPath = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(a => a.Status == AgentStatus.Idle && !a.IsDeleted);

        if (!string.IsNullOrEmpty(repositoryPath))
        {
            query = query.Where(a => a.RepositoryPath == repositoryPath);
        }

        return await query
            .OrderBy(a => a.TotalTasksCompleted) // Prefer agents with fewer completed tasks for load balancing
            .ToListAsync(cancellationToken);
    }

    public async Task<Agent?> GetBestAvailableAgentAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        // First, try to find an agent specifically for this repository
        var repositorySpecificAgent = await _dbSet
            .Where(a => a.Status == AgentStatus.Idle &&
                       a.RepositoryPath == repositoryPath &&
                       !a.IsDeleted)
            .OrderBy(a => a.AverageExecutionTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (repositorySpecificAgent != null)
        {
            return repositorySpecificAgent;
        }

        // If no repository-specific agent, find any available agent
        return await _dbSet
            .Where(a => a.Status == AgentStatus.Idle && !a.IsDeleted)
            .OrderBy(a => a.AverageExecutionTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AgentStatistics> GetAgentStatisticsAsync(string agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _dbSet
            .Include(a => a.AssignedTasks)
            .FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);

        if (agent == null)
        {
            throw new ArgumentException($"Agent {agentId} not found");
        }

        var recentTasks = agent.AssignedTasks
            .Where(t => t.CreatedAt >= DateTime.UtcNow.AddDays(-30))
            .ToList();

        return new AgentStatistics
        {
            AgentId = agentId,
            TotalTasksCompleted = agent.TotalTasksCompleted,
            TotalTasksFailed = agent.TotalTasksFailed,
            AverageExecutionTime = agent.AverageExecutionTime,
            SuccessRate = agent.TotalTasksCompleted + agent.TotalTasksFailed > 0
                ? (double)agent.TotalTasksCompleted / (agent.TotalTasksCompleted + agent.TotalTasksFailed)
                : 0,
            RecentTaskCount = recentTasks.Count,
            RecentSuccessRate = recentTasks.Count > 0
                ? (double)recentTasks.Count(t => t.Status == TaskStatus.Completed) / recentTasks.Count
                : 0
        };
    }
}
```

### Phase 3: Data Migration and Service Integration (Estimated: 8-12 hours)

**Tasks:**
1. **Create Data Migration Tools**
   - Migrate existing orchestrator-state.json to database
   - Import historical task data and agent configurations
   - Validate data integrity after migration
   - **Detailed Algorithm for Data Migration**:
     ```
     ALGORITHM: MigrateFromJsonAsync(stateFilePath)
     1. VALIDATE migration prerequisites:
        - CHECK state file exists and is readable
        - VERIFY database is accessible and schema is current
        - ENSURE no active tasks are running during migration
        - CREATE backup of existing database if present
     2. PARSE legacy data format:
        - DESERIALIZE JSON with error handling for malformed data
        - VALIDATE data structure matches expected schema
        - EXTRACT agents, tasks, repositories, and configuration
        - LOG any data inconsistencies found
     3. TRANSFORM data to new schema:
        - MAP legacy agent data to new Agent entity structure
        - CONVERT task queue items to TaskRecord entities
        - PRESERVE relationships and dependencies
        - GENERATE missing IDs using deterministic algorithm
     4. VALIDATE transformed data:
        - CHECK all foreign key relationships are valid
        - VERIFY no duplicate primary keys exist
        - VALIDATE data types and constraints
        - ENSURE referential integrity is maintained
     5. EXECUTE migration transaction:
        - BEGIN database transaction for atomicity
        - INSERT repositories first (no dependencies)
        - INSERT agents with repository references
        - INSERT tasks with agent and repository references
        - INSERT user preferences and configurations
     6. VERIFY migration success:
        - COUNT migrated records vs source records
        - VALIDATE random sample of migrated data
        - CHECK all relationships are properly established
        - RUN integrity checks on final database state
     7. FINALIZE migration:
        - COMMIT transaction if all validations pass
        - BACKUP original JSON file with timestamp
        - LOG migration summary with statistics
        - UPDATE migration status in database
     ```
   - **Data Validation Rules**:
     - Agent IDs must be unique and non-empty
     - Task commands must be under 2000 characters
     - Repository paths must be valid and accessible
     - Timestamps must be valid DateTime values
     - Status enums must map to valid integer values
   - **Rollback Procedures**:
     - IF validation fails → Rollback transaction, restore backup
     - IF data corruption detected → Stop migration, generate error report
     - IF constraint violations → Log specific violations, provide fix suggestions
   - **Acceptance Criteria**: All existing data successfully migrated with comprehensive validation and rollback capability

2. **Update Service Layer**
   - Modify OrchestratorService to use database repositories
   - Update AgentSidebar and TaskQueue to use EF queries
   - Implement database-backed user preferences
   - **Acceptance Criteria**: All existing functionality works with database backend

3. **Add Database Seeding and Initialization**
   - Create default configuration and sample data
   - Implement database schema versioning
   - Add automatic migration on startup
   - **Acceptance Criteria**: Fresh installations create proper database structure

**Migration Service:**
```csharp
public class DataMigrationService
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<DataMigrationService> _logger;

    public async Task MigrateFromJsonAsync(string stateFilePath)
    {
        if (!File.Exists(stateFilePath))
        {
            _logger.LogInformation("No existing state file found, starting with empty database");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(stateFilePath);
            var state = JsonSerializer.Deserialize<OrchestratorState>(json);

            if (state == null)
            {
                _logger.LogWarning("Failed to deserialize state file");
                return;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Migrate repositories first
                await MigrateRepositoriesAsync(state.Repositories);

                // Migrate agents
                await MigrateAgentsAsync(state.Agents.Values);

                // Migrate tasks
                await MigrateTasksAsync(state.TaskQueue);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully migrated {AgentCount} agents and {TaskCount} tasks",
                    state.Agents.Count, state.TaskQueue.Count);

                // Backup original file
                var backupPath = $"{stateFilePath}.backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                File.Move(stateFilePath, backupPath);
                _logger.LogInformation("Original state file backed up to {BackupPath}", backupPath);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate data from JSON file");
            throw;
        }
    }

    private async Task MigrateRepositoriesAsync(Dictionary<string, RepositoryInfo> repositories)
    {
        foreach (var (path, info) in repositories)
        {
            var existingRepo = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Path == path);

            if (existingRepo == null)
            {
                var repository = new Repository
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = info.Name,
                    Path = path,
                    Type = RepositoryType.Git, // Default assumption
                    IsActive = true,
                    TotalTasks = 0,
                    SuccessfulTasks = 0,
                    FailedTasks = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.Repositories.AddAsync(repository);
            }
        }
    }
}
```

### Phase 4: Analytics and Reporting (Estimated: 6-10 hours)

**Tasks:**
1. **Implement Performance Analytics**
   - Task execution time tracking and trending
   - Agent performance comparisons and optimization
   - Repository activity and health metrics
   - **Acceptance Criteria**: Comprehensive analytics available via API and UI

2. **Create Reporting Dashboard**
   - Real-time performance metrics display
   - Historical trend analysis and charts
   - Agent and repository health monitoring
   - **Acceptance Criteria**: Dashboard provides actionable insights

3. **Add Data Export and Backup**
   - Export task history and performance data
   - Automated database backup scheduling
   - Data retention policy implementation
   - **Acceptance Criteria**: Data can be exported and backups are automated

## Technical Specifications

### Database Configuration

**Connection String Management:**
```csharp
public class DatabaseConfiguration
{
    public string ConnectionString { get; set; } = "Data Source=orchestra.db";
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;
    public int CommandTimeout { get; set; } = 30;
    public bool AutoMigrateOnStartup { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
}

// Startup.cs
services.AddDbContext<OrchestraDbContext>(options =>
{
    var config = configuration.GetSection("Database").Get<DatabaseConfiguration>();

    options.UseSqlite(config.ConnectionString, sqliteOptions =>
    {
        sqliteOptions.CommandTimeout(config.CommandTimeout);
    })
    .EnableSensitiveDataLogging(config.EnableSensitiveDataLogging)
    .EnableDetailedErrors(config.EnableDetailedErrors);

    if (isDevelopment)
    {
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});
```

### Service Layer Integration

**Updated OrchestratorService:**
```csharp
public class OrchestratorService
{
    private readonly IAgentRepository _agentRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IRepositoryService _repositoryService;
    private readonly IUnitOfWork _unitOfWork;

    public async Task<bool> QueueTaskAsync(string command, string repositoryPath, TaskPriority priority = TaskPriority.Normal)
    {
        try
        {
            // Find or create repository
            var repository = await _repositoryService.GetOrCreateRepositoryAsync(repositoryPath);

            // Find best available agent
            var agent = await _agentRepository.GetBestAvailableAgentAsync(repositoryPath);

            if (agent == null)
            {
                // Queue task without agent assignment
                var pendingTask = new TaskRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    Command = command,
                    RepositoryPath = repositoryPath,
                    RepositoryId = repository.Id,
                    Priority = priority,
                    Status = TaskStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                await _taskRepository.AddAsync(pendingTask);
                await _unitOfWork.SaveChangesAsync();
                return true;
            }

            // Assign task to agent
            var task = new TaskRecord
            {
                Id = Guid.NewGuid().ToString(),
                Command = command,
                AgentId = agent.Id,
                RepositoryPath = repositoryPath,
                RepositoryId = repository.Id,
                Priority = priority,
                Status = TaskStatus.Assigned,
                CreatedAt = DateTime.UtcNow
            };

            await _taskRepository.AddAsync(task);
            await _agentRepository.UpdateAgentStatusAsync(agent.Id, AgentStatus.Busy, task.Id);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue task: {Command}", command);
            return false;
        }
    }

    public async Task<List<AgentInfo>> GetAllAgentsAsync()
    {
        var agents = await _agentRepository.GetAllAsync();
        return agents.Select(a => new AgentInfo(
            a.Id, a.Name, a.Type, a.RepositoryPath, a.Status, a.LastPing, a.CurrentTask)).ToList();
    }

    public async Task<OrchestratorState> GetCurrentStateAsync()
    {
        var agents = await _agentRepository.GetAllAsync();
        var pendingTasks = await _taskRepository.GetPendingTasksAsync();
        var repositories = await _repositoryService.GetRepositoryInfoAsync();

        return new OrchestratorState(
            agents.ToDictionary(a => a.Id, a => new AgentInfo(a.Id, a.Name, a.Type, a.RepositoryPath, a.Status, a.LastPing, a.CurrentTask)),
            new Queue<TaskRequest>(pendingTasks.Select(t => new TaskRequest(t.Id, t.AgentId, t.Command, t.RepositoryPath, t.CreatedAt, t.Priority, t.Status, t.StartedAt, t.CompletedAt))),
            DateTime.UtcNow,
            repositories
        );
    }
}
```

### Performance Optimization

**Query Optimization:**
```csharp
public class TaskRepository : GenericRepository<TaskRecord>, ITaskRepository
{
    public async Task<IEnumerable<TaskRecord>> GetTasksWithStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? agentId = null,
        string? repositoryId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(t => t.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.CreatedAt <= toDate.Value);

        if (!string.IsNullOrEmpty(agentId))
            query = query.Where(t => t.AgentId == agentId);

        if (!string.IsNullOrEmpty(repositoryId))
            query = query.Where(t => t.RepositoryId == repositoryId);

        return await query
            .Include(t => t.Agent)
            .Include(t => t.Repository)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskAnalytics> GetTaskAnalyticsAsync(
        TimeSpan period,
        CancellationToken cancellationToken = default)
    {
        var fromDate = DateTime.UtcNow.Subtract(period);

        var analytics = await _dbSet
            .Where(t => t.CreatedAt >= fromDate)
            .GroupBy(t => new { t.Status, Date = t.CreatedAt.Date })
            .Select(g => new
            {
                g.Key.Status,
                g.Key.Date,
                Count = g.Count(),
                AverageExecutionTime = g.Average(t => t.ExecutionDuration != null ? t.ExecutionDuration.Value.TotalSeconds : 0)
            })
            .ToListAsync(cancellationToken);

        return new TaskAnalytics
        {
            Period = period,
            TotalTasks = analytics.Sum(a => a.Count),
            CompletedTasks = analytics.Where(a => a.Status == TaskStatus.Completed).Sum(a => a.Count),
            FailedTasks = analytics.Where(a => a.Status == TaskStatus.Failed).Sum(a => a.Count),
            AverageExecutionTime = TimeSpan.FromSeconds(analytics.Average(a => a.AverageExecutionTime)),
            DailyBreakdown = analytics.GroupBy(a => a.Date)
                .ToDictionary(g => g.Key, g => g.ToList())
        };
    }
}
```

## Quality Assurance

### Testing Strategy
- **Unit Tests**: Repository operations, entity validation, migration logic
- **Integration Tests**: Database operations, service layer integration
- **Performance Tests**: Query performance, large dataset handling
- **Data Integrity Tests**: Migration accuracy, constraint validation

### Data Validation

**Entity Validation:**
```csharp
public class AgentValidator : AbstractValidator<Agent>
{
    public AgentValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Type).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RepositoryPath).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.MaxConcurrentTasks).GreaterThan(0).LessThanOrEqualTo(10);
        RuleFor(x => x.HealthCheckInterval).GreaterThan(TimeSpan.Zero);
    }
}

public class TaskRecordValidator : AbstractValidator<TaskRecord>
{
    public TaskRecordValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Command).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.RepositoryPath).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.RetryCount).GreaterThanOrEqualTo(0).LessThanOrEqualTo(10);

        When(x => x.StartedAt.HasValue, () =>
        {
            RuleFor(x => x.StartedAt).GreaterThanOrEqualTo(x => x.CreatedAt);
        });

        When(x => x.CompletedAt.HasValue, () =>
        {
            RuleFor(x => x.CompletedAt).GreaterThanOrEqualTo(x => x.StartedAt);
        });
    }
}
```

## Migration Strategy

### Database Versioning

**Migration Management:**
```csharp
public class DatabaseMigrationService
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public async Task EnsureDatabaseCreatedAsync()
    {
        try
        {
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count());
                await _context.Database.MigrateAsync();
                _logger.LogInformation("Database migrations completed successfully");
            }
            else
            {
                _logger.LogInformation("Database is up to date");
            }

            await SeedDefaultDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure database is created and migrated");
            throw;
        }
    }

    private async Task SeedDefaultDataAsync()
    {
        // Seed default user preferences
        if (!await _context.UserPreferences.AnyAsync())
        {
            var defaultPreferences = new[]
            {
                new UserPreference { Id = Guid.NewGuid().ToString(), UserId = "default", Key = "theme", Value = "dark", Type = PreferenceType.UI },
                new UserPreference { Id = Guid.NewGuid().ToString(), UserId = "default", Key = "pageSize", Value = "50", Type = PreferenceType.UI },
                new UserPreference { Id = Guid.NewGuid().ToString(), UserId = "default", Key = "autoRefresh", Value = "true", Type = PreferenceType.Behavior }
            };

            await _context.UserPreferences.AddRangeAsync(defaultPreferences);
            await _context.SaveChangesAsync();
        }
    }
}
```

### Comprehensive Data Integrity and Rollback Strategy

**Multi-level Backup and Recovery System:**
```csharp
public class ComprehensiveBackupService
{
    private readonly DatabaseConfiguration _config;
    private readonly IDataIntegrityValidator _integrityValidator;
    private readonly ILogger<ComprehensiveBackupService> _logger;

    public async Task<BackupResult> CreateTransactionSafeBackupAsync(BackupType backupType)
    {
        var backupId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;

        try
        {
            // 1. Validate database integrity before backup
            _logger.LogInformation("Starting integrity validation before backup {BackupId}", backupId);
            var integrityResult = await _integrityValidator.ValidateAsync();
            if (!integrityResult.IsValid)
            {
                throw new DataIntegrityException($"Database integrity check failed: {string.Join(", ", integrityResult.Errors)}");
            }

            // 2. Create WAL checkpoint to ensure consistency
            await CreateWalCheckpointAsync();

            // 3. Acquire shared lock to prevent writes during backup
            using var lockScope = await AcquireSharedLockAsync();

            var sourceFile = ExtractDbFileFromConnectionString(_config.ConnectionString);
            var backupPath = GenerateBackupPath(backupType, startTime);

            // 4. Perform atomic backup with verification
            await PerformAtomicBackupAsync(sourceFile, backupPath);

            // 5. Verify backup integrity
            var verificationResult = await VerifyBackupIntegrityAsync(backupPath);
            if (!verificationResult.IsValid)
            {
                File.Delete(backupPath);
                throw new BackupVerificationException($"Backup verification failed: {verificationResult.ErrorMessage}");
            }

            // 6. Update backup registry
            await RegisterBackupAsync(backupId, backupPath, backupType, startTime);

            // 7. Cleanup old backups based on retention policy
            await CleanupOldBackupsAsync(backupType);

            _logger.LogInformation("Backup {BackupId} completed successfully in {Duration}ms",
                backupId, (DateTime.UtcNow - startTime).TotalMilliseconds);

            return new BackupResult
            {
                BackupId = backupId,
                BackupPath = backupPath,
                BackupType = backupType,
                CreatedAt = startTime,
                SizeBytes = new FileInfo(backupPath).Length,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup {BackupId} failed after {Duration}ms",
                backupId, (DateTime.UtcNow - startTime).TotalMilliseconds);

            return new BackupResult
            {
                BackupId = backupId,
                BackupType = backupType,
                CreatedAt = startTime,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<RestoreResult> RestoreFromBackupAsync(string backupId, RestoreOptions options)
    {
        var restoreStartTime = DateTime.UtcNow;
        var tempRestoreId = Guid.NewGuid().ToString();

        try
        {
            // 1. Validate restore preconditions
            var backup = await GetBackupMetadataAsync(backupId);
            if (backup == null)
                throw new BackupNotFoundException($"Backup {backupId} not found");

            if (!File.Exists(backup.BackupPath))
                throw new BackupFileNotFoundException($"Backup file not found: {backup.BackupPath}");

            // 2. Verify backup file integrity
            var backupIntegrity = await VerifyBackupIntegrityAsync(backup.BackupPath);
            if (!backupIntegrity.IsValid)
                throw new BackupCorruptedException($"Backup file is corrupted: {backupIntegrity.ErrorMessage}");

            // 3. Create safety backup of current database
            var currentDbPath = ExtractDbFileFromConnectionString(_config.ConnectionString);
            var safetyBackupPath = $"{currentDbPath}.safety_backup_{tempRestoreId}";
            await File.CopyAsync(currentDbPath, safetyBackupPath);

            _logger.LogInformation("Created safety backup before restore: {SafetyBackupPath}", safetyBackupPath);

            // 4. Stop all active connections and transactions
            await StopAllActiveConnectionsAsync();

            // 5. Perform atomic restore operation
            await PerformAtomicRestoreAsync(backup.BackupPath, currentDbPath, options);

            // 6. Validate restored database integrity
            var restoredIntegrity = await _integrityValidator.ValidateAsync();
            if (!restoredIntegrity.IsValid)
            {
                // Rollback to safety backup
                _logger.LogError("Restored database failed integrity check, rolling back");
                await File.CopyAsync(safetyBackupPath, currentDbPath, overwrite: true);
                throw new RestoreValidationException($"Restored database failed integrity validation: {string.Join(", ", restoredIntegrity.Errors)}");
            }

            // 7. Run post-restore validation tests
            if (options.RunValidationTests)
            {
                var validationResult = await RunPostRestoreValidationAsync();
                if (!validationResult.Success)
                {
                    _logger.LogError("Post-restore validation failed, rolling back");
                    await File.CopyAsync(safetyBackupPath, currentDbPath, overwrite: true);
                    throw new PostRestoreValidationException($"Post-restore validation failed: {validationResult.ErrorMessage}");
                }
            }

            // 8. Archive safety backup (don't delete immediately)
            var archivePath = $"{safetyBackupPath}.archived";
            File.Move(safetyBackupPath, archivePath);

            _logger.LogInformation("Database restore from backup {BackupId} completed successfully in {Duration}ms",
                backupId, (DateTime.UtcNow - restoreStartTime).TotalMilliseconds);

            return new RestoreResult
            {
                RestoreId = tempRestoreId,
                BackupId = backupId,
                RestoredAt = DateTime.UtcNow,
                Success = true,
                SafetyBackupPath = archivePath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database restore from backup {BackupId} failed after {Duration}ms",
                backupId, (DateTime.UtcNow - restoreStartTime).TotalMilliseconds);

            return new RestoreResult
            {
                RestoreId = tempRestoreId,
                BackupId = backupId,
                RestoredAt = DateTime.UtcNow,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task PerformAtomicBackupAsync(string sourceFile, string backupPath)
    {
        // Use SQLite VACUUM INTO for atomic backup
        using var connection = new SQLiteConnection($"Data Source={sourceFile}");
        await connection.OpenAsync();

        // VACUUM INTO creates an atomic backup
        await connection.ExecuteAsync($"VACUUM INTO '{backupPath}'");

        // Verify the backup was created successfully
        if (!File.Exists(backupPath))
            throw new BackupCreationException("Backup file was not created successfully");

        var sourceSize = new FileInfo(sourceFile).Length;
        var backupSize = new FileInfo(backupPath).Length;

        // Basic size validation (backup should be similar size, accounting for VACUUM optimization)
        if (backupSize < sourceSize * 0.5 || backupSize > sourceSize * 1.5)
            throw new BackupValidationException($"Backup size mismatch: source={sourceSize}, backup={backupSize}");
    }

    private async Task<BackupVerificationResult> VerifyBackupIntegrityAsync(string backupPath)
    {
        try
        {
            using var connection = new SQLiteConnection($"Data Source={backupPath}");
            await connection.OpenAsync();

            // Run PRAGMA integrity_check
            var integrityResult = await connection.QuerySingleAsync<string>("PRAGMA integrity_check");
            if (integrityResult != "ok")
                return BackupVerificationResult.Failed($"Integrity check failed: {integrityResult}");

            // Verify critical tables exist
            var tableCount = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Agents', 'Tasks', 'Repositories')");
            if (tableCount < 3)
                return BackupVerificationResult.Failed("Critical tables missing from backup");

            // Sample data verification
            var agentCount = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM Agents WHERE IsDeleted = 0");
            var taskCount = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM Tasks");

            return BackupVerificationResult.Success(new BackupMetrics
            {
                AgentCount = agentCount,
                TaskCount = taskCount,
                IntegrityStatus = "ok"
            });
        }
        catch (Exception ex)
        {
            return BackupVerificationResult.Failed($"Backup verification error: {ex.Message}");
        }
    }
}
```

**Advanced Data Integrity Validation:**
```csharp
public class DataIntegrityValidator : IDataIntegrityValidator
{
    private readonly OrchestraDbContext _context;
    private readonly ILogger<DataIntegrityValidator> _logger;

    public async Task<IntegrityValidationResult> ValidateAsync()
    {
        var validationId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;
        var errors = new List<string>();
        var warnings = new List<string>();

        _logger.LogInformation("Starting comprehensive data integrity validation {ValidationId}", validationId);

        try
        {
            // 1. Database structure validation
            await ValidateDatabaseStructureAsync(errors);

            // 2. Referential integrity validation
            await ValidateReferentialIntegrityAsync(errors);

            // 3. Data consistency validation
            await ValidateDataConsistencyAsync(errors, warnings);

            // 4. Business rule validation
            await ValidateBusinessRulesAsync(errors, warnings);

            // 5. Performance and health checks
            await ValidatePerformanceMetricsAsync(warnings);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Data integrity validation {ValidationId} completed in {Duration}ms with {ErrorCount} errors and {WarningCount} warnings",
                validationId, duration.TotalMilliseconds, errors.Count, warnings.Count);

            return new IntegrityValidationResult
            {
                ValidationId = validationId,
                IsValid = !errors.Any(),
                Errors = errors,
                Warnings = warnings,
                ValidatedAt = DateTime.UtcNow,
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data integrity validation {ValidationId} failed with exception", validationId);
            errors.Add($"Validation failed with exception: {ex.Message}");

            return new IntegrityValidationResult
            {
                ValidationId = validationId,
                IsValid = false,
                Errors = errors,
                Warnings = warnings,
                ValidatedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task ValidateReferentialIntegrityAsync(List<string> errors)
    {
        // Check for orphaned tasks
        var orphanedTasks = await _context.Database.ExecuteSqlInterpolatedAsync($@"
            SELECT COUNT(*) FROM Tasks t
            WHERE t.AgentId IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM Agents a WHERE a.Id = t.AgentId AND a.IsDeleted = 0)");

        if (orphanedTasks > 0)
            errors.Add($"Found {orphanedTasks} tasks with invalid agent references");

        // Check for orphaned user preferences
        var orphanedPreferences = await _context.Database.ExecuteSqlInterpolatedAsync($@"
            SELECT COUNT(*) FROM UserPreferences up
            WHERE up.RepositoryId IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM Repositories r WHERE r.Id = up.RepositoryId)");

        if (orphanedPreferences > 0)
            errors.Add($"Found {orphanedPreferences} user preferences with invalid repository references");

        // Check for circular dependencies in tasks
        var circularDependencies = await DetectCircularTaskDependenciesAsync();
        if (circularDependencies.Any())
            errors.Add($"Found circular dependencies in tasks: {string.Join(", ", circularDependencies)}");
    }

    private async Task ValidateDataConsistencyAsync(List<string> errors, List<string> warnings)
    {
        // Validate agent performance metrics consistency
        var inconsistentAgents = await _context.Agents
            .Where(a => a.TotalTasksCompleted + a.TotalTasksFailed !=
                       a.AssignedTasks.Count(t => t.Status == TaskStatus.Completed || t.Status == TaskStatus.Failed))
            .Select(a => a.Id)
            .ToListAsync();

        if (inconsistentAgents.Any())
            warnings.Add($"Found {inconsistentAgents.Count} agents with inconsistent performance metrics");

        // Validate timestamp consistency
        var invalidTimestamps = await _context.Tasks
            .Where(t => t.StartedAt.HasValue && t.CompletedAt.HasValue && t.StartedAt > t.CompletedAt)
            .Select(t => t.Id)
            .ToListAsync();

        if (invalidTimestamps.Any())
            errors.Add($"Found {invalidTimestamps.Count} tasks with invalid timestamp sequences");

        // Validate enum values are within valid ranges
        var invalidStatuses = await _context.Tasks
            .Where(t => (int)t.Status < 0 || (int)t.Status > 4)
            .Select(t => t.Id)
            .ToListAsync();

        if (invalidStatuses.Any())
            errors.Add($"Found {invalidStatuses.Count} tasks with invalid status values");
    }

    private async Task ValidateBusinessRulesAsync(List<string> errors, List<string> warnings)
    {
        // Validate no agent is assigned to more tasks than its max concurrent limit
        var overloadedAgents = await _context.Agents
            .Where(a => a.AssignedTasks.Count(t => t.Status == TaskStatus.InProgress || t.Status == TaskStatus.Assigned) > a.MaxConcurrentTasks)
            .Select(a => new { a.Id, a.Name, CurrentTasks = a.AssignedTasks.Count(t => t.Status == TaskStatus.InProgress || t.Status == TaskStatus.Assigned), a.MaxConcurrentTasks })
            .ToListAsync();

        foreach (var agent in overloadedAgents)
            warnings.Add($"Agent {agent.Name} ({agent.Id}) has {agent.CurrentTasks} active tasks but max concurrent is {agent.MaxConcurrentTasks}");

        // Validate task execution times are reasonable
        var longRunningTasks = await _context.Tasks
            .Where(t => t.ExecutionDuration.HasValue && t.ExecutionDuration > TimeSpan.FromHours(24))
            .Select(t => new { t.Id, t.Command, t.ExecutionDuration })
            .ToListAsync();

        foreach (var task in longRunningTasks)
            warnings.Add($"Task {task.Id} has unusually long execution time: {task.ExecutionDuration}");
    }
}
```

**Automated Recovery Procedures:**
```csharp
public class AutomatedRecoveryService
{
    public async Task<RecoveryResult> AttemptAutomaticRecoveryAsync(IntegrityValidationResult validationResult)
    {
        var recoveryActions = new List<RecoveryAction>();

        foreach (var error in validationResult.Errors)
        {
            var action = await DetermineRecoveryActionAsync(error);
            if (action != null)
            {
                recoveryActions.Add(action);
            }
        }

        var recoveryResult = new RecoveryResult
        {
            RecoveryId = Guid.NewGuid().ToString(),
            OriginalValidationId = validationResult.ValidationId,
            AttemptedActions = recoveryActions,
            StartedAt = DateTime.UtcNow
        };

        foreach (var action in recoveryActions)
        {
            try
            {
                await ExecuteRecoveryActionAsync(action);
                action.Success = true;
            }
            catch (Exception ex)
            {
                action.Success = false;
                action.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Recovery action {ActionType} failed", action.ActionType);
            }
        }

        // Re-validate after recovery attempts
        var postRecoveryValidation = await _integrityValidator.ValidateAsync();
        recoveryResult.PostRecoveryValidation = postRecoveryValidation;
        recoveryResult.CompletedAt = DateTime.UtcNow;
        recoveryResult.Success = postRecoveryValidation.IsValid;

        return recoveryResult;
    }

    private async Task<RecoveryAction?> DetermineRecoveryActionAsync(string error)
    {
        return error switch
        {
            var e when e.Contains("orphaned tasks") => new RecoveryAction
            {
                ActionType = RecoveryActionType.CleanupOrphanedTasks,
                Description = "Remove tasks with invalid agent references",
                Query = "DELETE FROM Tasks WHERE AgentId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Agents WHERE Id = Tasks.AgentId)"
            },
            var e when e.Contains("orphaned user preferences") => new RecoveryAction
            {
                ActionType = RecoveryActionType.CleanupOrphanedPreferences,
                Description = "Remove user preferences with invalid repository references",
                Query = "DELETE FROM UserPreferences WHERE RepositoryId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Repositories WHERE Id = UserPreferences.RepositoryId)"
            },
            var e when e.Contains("invalid timestamp sequences") => new RecoveryAction
            {
                ActionType = RecoveryActionType.FixTimestamps,
                Description = "Fix tasks with invalid timestamp sequences",
                Query = "UPDATE Tasks SET CompletedAt = StartedAt WHERE StartedAt IS NOT NULL AND CompletedAt IS NOT NULL AND StartedAt > CompletedAt"
            },
            _ => null
        };
    }
}
```
```

## Success Criteria

1. **Data Persistence**: All application data stored reliably in SQLite database
2. **Performance**: Database operations complete within 100ms for typical queries
3. **Migration**: Seamless migration from file-based storage without data loss
4. **Scalability**: Support for 10,000+ tasks and 100+ agents without performance degradation
5. **Reliability**: Database operations have 99.9% success rate with proper error handling
6. **Analytics**: Comprehensive reporting and analytics capabilities available

This work plan establishes a robust, scalable database foundation that supports all other planned features while maintaining the simplicity and deployment characteristics of an embedded SQLite database solution.