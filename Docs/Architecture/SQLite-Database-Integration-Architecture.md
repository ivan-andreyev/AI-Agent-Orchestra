# SQLite Database Integration Architecture

## Architecture Overview

This document defines the comprehensive architecture for implementing SQLite database integration using Entity Framework Core to replace file-based storage with persistent, queryable data management for agent configurations, task history, user preferences, and orchestration state. This foundation supports all other features while maintaining the lightweight, embedded database approach suitable for desktop deployment.

## System Context Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                AI Agent Orchestra - SQLite Database Integration             │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                      Application Layer                              │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │   │
│  │  │ Web Interface   │  │ Hangfire Jobs   │  │ SignalR Hubs    │     │   │
│  │  │ - UI Components │  │ - Background    │  │ - Real-time     │     │   │
│  │  │ - User Actions  │  │   Processing    │  │   Communication │     │   │
│  │  │ - Data Display  │  │ - Scheduling    │  │ - Live Updates  │     │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘     │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                   │                                         │
│                                   ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                        Service Layer                                │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │   │
│  │  │ Agent           │  │ Task            │  │ Repository      │     │   │
│  │  │ Management      │  │ Orchestration   │  │ Management      │     │   │
│  │  │ Service         │  │ Service         │  │ Service         │     │   │
│  │  │ - Agent CRUD    │  │ - Task Queue    │  │ - Repo Config   │     │   │
│  │  │ - Health Checks │  │ - Execution     │  │ - Path Mgmt     │     │   │
│  │  │ - Performance   │  │ - Analytics     │  │ - Analytics     │     │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘     │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │   │
│  │  │ User Preference │  │ Analytics       │  │ Audit & Logging │     │   │
│  │  │ Service         │  │ Service         │  │ Service         │     │   │
│  │  │ - Settings Mgmt │  │ - Performance   │  │ - Change Track  │     │   │
│  │  │ - UI Config     │  │ - Reporting     │  │ - Compliance    │     │   │
│  │  │ - Personalization│ │ - Trends       │  │ - Security      │     │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘     │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                   │                                         │
│                                   ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                     Data Access Layer                               │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │   │
│  │  │ Repository      │  │ Unit of Work    │  │ Specification   │     │   │
│  │  │ Pattern         │  │ Pattern         │  │ Pattern         │     │   │
│  │  │ - Generic CRUD  │  │ - Transaction   │  │ - Query Logic   │     │   │
│  │  │ - Specialized   │  │   Management    │  │ - Reusable      │     │   │
│  │  │   Operations    │  │ - Change        │  │   Criteria      │     │   │
│  │  │ - Caching       │  │   Tracking      │  │ - Composable    │     │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘     │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │   │
│  │  │ Entity          │  │ Query           │  │ Migration       │     │   │
│  │  │ Configuration   │  │ Optimization    │  │ Management      │     │   │
│  │  │ - Mappings      │  │ - Indexing      │  │ - Schema        │     │   │
│  │  │ - Relationships │  │ - Performance   │  │   Versioning    │     │   │
│  │  │ - Constraints   │  │ - Caching       │  │ - Data Safety   │     │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘     │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                   │                                         │
│                                   ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    Entity Framework Core                            │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │   │
│  │  │ DbContext       │  │ Change Tracking │  │ Query           │     │   │
│  │  │ - Connection    │  │ - Entity State  │  │ Translation     │     │   │
│  │  │   Management    │  │ - Dirty Flag    │  │ - SQL           │     │   │
│  │  │ - Transaction   │  │ - Validation    │  │   Generation    │     │   │
│  │  │   Control       │  │ - Concurrency   │  │ - Optimization  │     │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘     │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                   │                                         │
│                                   ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                        SQLite Database                              │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │   │
│  │  │ Core Tables     │  │ Logging Tables  │  │ Cache Tables    │     │   │
│  │  │ - Agents        │  │ - Audit Logs    │  │ - Query Cache   │     │   │
│  │  │ - Tasks         │  │ - Performance   │  │ - Computed      │     │   │
│  │  │ - Repositories  │  │   Metrics       │  │   Results       │     │   │
│  │  │ - Users         │  │ - Error Logs    │  │ - Session Data  │     │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘     │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │   │
│  │  │ Index           │  │ Triggers        │  │ Views           │     │   │
│  │  │ Management      │  │ - Audit Trail   │  │ - Aggregated    │     │   │
│  │  │ - Performance   │  │ - Data          │  │   Data          │     │   │
│  │  │ - Query         │  │   Validation    │  │ - Reporting     │     │   │
│  │  │   Optimization  │  │ - Cleanup       │  │ - Analytics     │     │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘     │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                   │                                         │
│                                   ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                      File System Storage                            │   │
│  │                                                                     │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │   │
│  │  │ Database File   │  │ Backup Files    │  │ Transaction     │     │   │
│  │  │ - orchestra.db  │  │ - Automated     │  │ Logs            │     │   │
│  │  │ - WAL Journal   │  │   Backups       │  │ - WAL Files     │     │   │
│  │  │ - Shared Memory │  │ - Manual        │  │ - Rollback      │     │   │
│  │  │ - Temp Files    │  │   Snapshots     │  │   Recovery      │     │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘     │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Database Schema Architecture

### 1. Entity Relationship Model

```sql
-- Core domain entities with comprehensive relationships
CREATE TABLE Agents (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL,
    RepositoryPath TEXT NOT NULL,
    Status INTEGER NOT NULL DEFAULT 0, -- AgentStatus enum
    LastPing DATETIME NOT NULL,
    CurrentTask TEXT NULL,

    -- Configuration
    ConfigurationJson TEXT NULL,
    MaxConcurrentTasks INTEGER NOT NULL DEFAULT 1,
    HealthCheckInterval TEXT NOT NULL DEFAULT '00:01:00', -- TimeSpan

    -- Performance Metrics
    TotalTasksCompleted INTEGER NOT NULL DEFAULT 0,
    TotalTasksFailed INTEGER NOT NULL DEFAULT 0,
    TotalExecutionTime TEXT NOT NULL DEFAULT '00:00:00', -- TimeSpan
    AverageExecutionTime REAL NOT NULL DEFAULT 0,
    SuccessRate REAL COMPUTED AS (
        CASE
            WHEN (TotalTasksCompleted + TotalTasksFailed) = 0 THEN 0
            ELSE CAST(TotalTasksCompleted AS REAL) / (TotalTasksCompleted + TotalTasksFailed)
        END
    ),

    -- Audit fields
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy TEXT NOT NULL DEFAULT 'system',
    UpdatedBy TEXT NOT NULL DEFAULT 'system',
    IsDeleted BOOLEAN NOT NULL DEFAULT 0,
    DeletedAt DATETIME NULL,
    DeletedBy TEXT NULL,

    -- Relationships
    RepositoryId TEXT NULL,

    FOREIGN KEY (RepositoryId) REFERENCES Repositories(Id) ON DELETE SET NULL
);

CREATE TABLE Tasks (
    Id TEXT PRIMARY KEY,
    Command TEXT NOT NULL CHECK(length(Command) <= 2000),
    AgentId TEXT NULL,
    RepositoryPath TEXT NOT NULL,
    Priority INTEGER NOT NULL DEFAULT 1, -- TaskPriority enum
    Status INTEGER NOT NULL DEFAULT 0, -- TaskStatus enum

    -- Execution tracking
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    StartedAt DATETIME NULL,
    CompletedAt DATETIME NULL,
    ExecutionDuration TEXT NULL, -- TimeSpan

    -- Results and output
    Result TEXT NULL,
    ErrorMessage TEXT NULL,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    CorrelationId TEXT NULL,

    -- Workflow support
    WorkflowId TEXT NULL,
    ParentTaskId TEXT NULL,
    WorkflowStep INTEGER NOT NULL DEFAULT 0,

    -- Hangfire integration
    HangfireJobId TEXT NULL,
    QueueName TEXT NULL,

    -- Progress tracking
    ProgressPercentage INTEGER NOT NULL DEFAULT 0,
    ProgressMessage TEXT NULL,
    LastProgressUpdate DATETIME NULL,

    -- Audit fields
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy TEXT NOT NULL DEFAULT 'system',
    UpdatedBy TEXT NOT NULL DEFAULT 'system',

    -- Relationships
    RepositoryId TEXT NULL,

    FOREIGN KEY (AgentId) REFERENCES Agents(Id) ON DELETE SET NULL,
    FOREIGN KEY (RepositoryId) REFERENCES Repositories(Id) ON DELETE SET NULL,
    FOREIGN KEY (ParentTaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
    FOREIGN KEY (WorkflowId) REFERENCES WorkflowDefinitions(Id) ON DELETE SET NULL
);

CREATE TABLE Repositories (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Path TEXT NOT NULL UNIQUE,
    Description TEXT NULL,
    Type INTEGER NOT NULL DEFAULT 0, -- RepositoryType enum
    IsActive BOOLEAN NOT NULL DEFAULT 1,

    -- Configuration
    SettingsJson TEXT NULL,
    DefaultBranch TEXT NULL,
    AllowedOperations TEXT NULL, -- Comma-separated list

    -- Access control
    AccessLevel INTEGER NOT NULL DEFAULT 0, -- AccessLevel enum
    OwnerUserId TEXT NULL,

    -- Statistics
    TotalTasks INTEGER NOT NULL DEFAULT 0,
    SuccessfulTasks INTEGER NOT NULL DEFAULT 0,
    FailedTasks INTEGER NOT NULL DEFAULT 0,
    TotalExecutionTime TEXT NOT NULL DEFAULT '00:00:00',
    AverageTaskDuration REAL NOT NULL DEFAULT 0,
    LastTaskExecutedAt DATETIME NULL,

    -- Audit fields
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastAccessedAt DATETIME NULL,
    CreatedBy TEXT NOT NULL DEFAULT 'system',
    UpdatedBy TEXT NOT NULL DEFAULT 'system',

    -- Performance tracking
    PerformanceRating REAL NOT NULL DEFAULT 0,
    ReliabilityScore REAL NOT NULL DEFAULT 0
);

CREATE TABLE UserPreferences (
    Id TEXT PRIMARY KEY,
    UserId TEXT NOT NULL DEFAULT 'default',
    Key TEXT NOT NULL,
    Value TEXT NOT NULL,
    Type INTEGER NOT NULL DEFAULT 0, -- PreferenceType enum
    Category TEXT NULL,

    -- Scope
    RepositoryId TEXT NULL, -- Repository-specific preferences
    AgentId TEXT NULL, -- Agent-specific preferences

    -- Metadata
    Description TEXT NULL,
    IsEncrypted BOOLEAN NOT NULL DEFAULT 0,
    IsReadOnly BOOLEAN NOT NULL DEFAULT 0,

    -- Audit fields
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy TEXT NOT NULL DEFAULT 'system',
    UpdatedBy TEXT NOT NULL DEFAULT 'system',

    -- Relationships
    FOREIGN KEY (RepositoryId) REFERENCES Repositories(Id) ON DELETE CASCADE,
    FOREIGN KEY (AgentId) REFERENCES Agents(Id) ON DELETE CASCADE,

    -- Constraints
    UNIQUE(UserId, Key, RepositoryId, AgentId)
);
```

### 2. Advanced Tables for Analytics and Monitoring

```sql
-- Performance metrics with time-series data
CREATE TABLE PerformanceMetrics (
    Id TEXT PRIMARY KEY,
    EntityType TEXT NOT NULL, -- 'Agent', 'Task', 'Repository', 'System'
    EntityId TEXT NULL,
    MetricName TEXT NOT NULL,
    MetricValue REAL NOT NULL,
    MetricUnit TEXT NULL,

    -- Time series
    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PeriodType TEXT NOT NULL DEFAULT 'Instant', -- 'Instant', 'Hourly', 'Daily', 'Weekly'
    PeriodStart DATETIME NULL,
    PeriodEnd DATETIME NULL,

    -- Metadata
    Tags TEXT NULL, -- JSON array of tags
    Properties TEXT NULL, -- JSON object with additional properties

    -- Aggregation support
    SampleCount INTEGER NOT NULL DEFAULT 1,
    MinValue REAL NULL,
    MaxValue REAL NULL,
    StandardDeviation REAL NULL,

    -- Audit
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy TEXT NOT NULL DEFAULT 'system'
);

-- Comprehensive audit log
CREATE TABLE AuditLogs (
    Id TEXT PRIMARY KEY,
    EntityType TEXT NOT NULL,
    EntityId TEXT NULL,
    Action TEXT NOT NULL, -- 'Create', 'Update', 'Delete', 'Execute', etc.

    -- Change tracking
    OldValues TEXT NULL, -- JSON of previous values
    NewValues TEXT NULL, -- JSON of new values
    ChangedProperties TEXT NULL, -- JSON array of changed property names

    -- Context
    UserId TEXT NULL,
    SessionId TEXT NULL,
    IpAddress TEXT NULL,
    UserAgent TEXT NULL,

    -- Request details
    RequestId TEXT NULL,
    CorrelationId TEXT NULL,
    HttpMethod TEXT NULL,
    RequestPath TEXT NULL,

    -- Timing
    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Duration INTEGER NULL, -- milliseconds

    -- Result
    Success BOOLEAN NOT NULL DEFAULT 1,
    ErrorMessage TEXT NULL,
    ErrorCode TEXT NULL,

    -- Security
    SecurityContext TEXT NULL, -- JSON with security-related context
    RiskLevel INTEGER NOT NULL DEFAULT 0, -- 0=Low, 1=Medium, 2=High, 3=Critical

    -- Metadata
    Tags TEXT NULL,
    Properties TEXT NULL
);

-- Chat messages with rich metadata
CREATE TABLE ChatMessages (
    Id TEXT PRIMARY KEY,
    SessionId TEXT NOT NULL,
    AgentId TEXT NOT NULL,
    UserId TEXT NULL,

    -- Message content
    MessageType INTEGER NOT NULL, -- MessageType enum
    Content TEXT NOT NULL,
    FormattedContent TEXT NULL, -- HTML formatted content

    -- Attachments and media
    Attachments TEXT NULL, -- JSON array of attachment info
    HasFiles BOOLEAN NOT NULL DEFAULT 0,
    FileCount INTEGER NOT NULL DEFAULT 0,
    TotalFileSize INTEGER NOT NULL DEFAULT 0,

    -- Conversation context
    ThreadId TEXT NULL,
    ParentMessageId TEXT NULL,
    IsEdited BOOLEAN NOT NULL DEFAULT 0,
    EditHistory TEXT NULL, -- JSON array of edit timestamps and reasons

    -- Delivery tracking
    DeliveryStatus INTEGER NOT NULL DEFAULT 0, -- DeliveryStatus enum
    DeliveredAt DATETIME NULL,
    ReadAt DATETIME NULL,
    AcknowledgedAt DATETIME NULL,

    -- Processing metadata
    ProcessingTime INTEGER NULL, -- milliseconds
    TokenCount INTEGER NULL,
    ModelVersion TEXT NULL,

    -- Sentiment and analysis
    SentimentScore REAL NULL,
    Language TEXT NULL,
    Topics TEXT NULL, -- JSON array of detected topics

    -- Performance tracking
    ResponseTime INTEGER NULL, -- milliseconds for agent responses
    QueueTime INTEGER NULL, -- milliseconds spent in queue

    -- Relationships
    LinkedTaskId TEXT NULL,
    WorkflowExecutionId TEXT NULL,

    -- Audit
    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CreatedBy TEXT NOT NULL DEFAULT 'system',

    FOREIGN KEY (AgentId) REFERENCES Agents(Id) ON DELETE CASCADE,
    FOREIGN KEY (LinkedTaskId) REFERENCES Tasks(Id) ON DELETE SET NULL,
    FOREIGN KEY (ParentMessageId) REFERENCES ChatMessages(Id) ON DELETE SET NULL
);

-- Workflow definitions and executions
CREATE TABLE WorkflowDefinitions (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Version TEXT NOT NULL DEFAULT '1.0.0',

    -- Workflow content
    Definition TEXT NOT NULL, -- JSON workflow definition
    InputSchema TEXT NULL, -- JSON schema for input validation
    OutputSchema TEXT NULL, -- JSON schema for output validation

    -- Metadata
    Category TEXT NULL,
    Tags TEXT NULL, -- JSON array
    Author TEXT NULL,
    IsPublic BOOLEAN NOT NULL DEFAULT 0,
    IsActive BOOLEAN NOT NULL DEFAULT 1,

    -- Usage statistics
    ExecutionCount INTEGER NOT NULL DEFAULT 0,
    SuccessCount INTEGER NOT NULL DEFAULT 0,
    FailureCount INTEGER NOT NULL DEFAULT 0,
    AverageExecutionTime REAL NOT NULL DEFAULT 0,
    LastExecutedAt DATETIME NULL,

    -- Version control
    PreviousVersionId TEXT NULL,
    ChangeLog TEXT NULL,

    -- Audit
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy TEXT NOT NULL DEFAULT 'system',
    UpdatedBy TEXT NOT NULL DEFAULT 'system',

    FOREIGN KEY (PreviousVersionId) REFERENCES WorkflowDefinitions(Id) ON DELETE SET NULL
);

CREATE TABLE WorkflowExecutions (
    Id TEXT PRIMARY KEY,
    WorkflowDefinitionId TEXT NOT NULL,
    WorkflowVersion TEXT NOT NULL,

    -- Execution context
    InputParameters TEXT NULL, -- JSON
    OutputResults TEXT NULL, -- JSON
    ExecutionPlan TEXT NULL, -- JSON execution plan

    -- Status tracking
    Status INTEGER NOT NULL DEFAULT 0, -- WorkflowExecutionStatus enum
    CurrentStage TEXT NULL,
    CompletedStages TEXT NULL, -- JSON array
    FailedStages TEXT NULL, -- JSON array

    -- Timing
    StartedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CompletedAt DATETIME NULL,
    EstimatedDuration INTEGER NULL, -- milliseconds
    ActualDuration INTEGER NULL, -- milliseconds

    -- Error handling
    ErrorMessage TEXT NULL,
    ErrorStage TEXT NULL,
    RetryCount INTEGER NOT NULL DEFAULT 0,

    -- Progress tracking
    ProgressPercentage INTEGER NOT NULL DEFAULT 0,
    ProgressMessage TEXT NULL,
    LastProgressUpdate DATETIME NULL,

    -- Resource usage
    PeakMemoryUsage INTEGER NULL,
    CpuTime INTEGER NULL, -- milliseconds
    IoOperations INTEGER NULL,

    -- Relationships
    TriggerUserId TEXT NULL,
    TriggerType TEXT NULL, -- 'Manual', 'Scheduled', 'Event', 'API'
    ParentExecutionId TEXT NULL,

    -- Audit
    CreatedBy TEXT NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (WorkflowDefinitionId) REFERENCES WorkflowDefinitions(Id) ON DELETE CASCADE,
    FOREIGN KEY (ParentExecutionId) REFERENCES WorkflowExecutions(Id) ON DELETE SET NULL
);
```

### 3. Optimized Indexes for Performance

```sql
-- Agent performance indexes
CREATE INDEX IX_Agents_Status ON Agents(Status) WHERE IsDeleted = 0;
CREATE INDEX IX_Agents_Repository ON Agents(RepositoryPath) WHERE IsDeleted = 0;
CREATE INDEX IX_Agents_Performance ON Agents(Status, RepositoryPath, AverageExecutionTime) WHERE IsDeleted = 0;
CREATE INDEX IX_Agents_Health ON Agents(LastPing, Status) WHERE IsDeleted = 0;

-- Task execution indexes
CREATE INDEX IX_Tasks_Status ON Tasks(Status, Priority, CreatedAt);
CREATE INDEX IX_Tasks_Agent ON Tasks(AgentId, Status) WHERE AgentId IS NOT NULL;
CREATE INDEX IX_Tasks_Repository ON Tasks(RepositoryId, Status) WHERE RepositoryId IS NOT NULL;
CREATE INDEX IX_Tasks_Workflow ON Tasks(WorkflowId, WorkflowStep) WHERE WorkflowId IS NOT NULL;
CREATE INDEX IX_Tasks_Execution_Timeline ON Tasks(StartedAt, CompletedAt) WHERE StartedAt IS NOT NULL;
CREATE INDEX IX_Tasks_Correlation ON Tasks(CorrelationId) WHERE CorrelationId IS NOT NULL;
CREATE INDEX IX_Tasks_Hangfire ON Tasks(HangfireJobId) WHERE HangfireJobId IS NOT NULL;

-- Repository analytics indexes
CREATE INDEX IX_Repositories_Active ON Repositories(IsActive, PerformanceRating);
CREATE INDEX IX_Repositories_Usage ON Repositories(LastTaskExecutedAt, TotalTasks);
CREATE INDEX IX_Repositories_Path ON Repositories(Path) WHERE IsActive = 1;

-- User preferences indexes
CREATE INDEX IX_UserPreferences_User ON UserPreferences(UserId, Category);
CREATE INDEX IX_UserPreferences_Repository ON UserPreferences(RepositoryId, Key) WHERE RepositoryId IS NOT NULL;
CREATE INDEX IX_UserPreferences_Agent ON UserPreferences(AgentId, Key) WHERE AgentId IS NOT NULL;
CREATE UNIQUE INDEX IX_UserPreferences_Unique ON UserPreferences(UserId, Key, COALESCE(RepositoryId, ''), COALESCE(AgentId, ''));

-- Performance metrics indexes for time-series queries
CREATE INDEX IX_PerformanceMetrics_Entity ON PerformanceMetrics(EntityType, EntityId, MetricName, Timestamp);
CREATE INDEX IX_PerformanceMetrics_TimeSeries ON PerformanceMetrics(MetricName, Timestamp, EntityType);
CREATE INDEX IX_PerformanceMetrics_Aggregation ON PerformanceMetrics(EntityType, MetricName, PeriodType, PeriodStart, PeriodEnd);

-- Audit log indexes
CREATE INDEX IX_AuditLogs_Entity ON AuditLogs(EntityType, EntityId, Timestamp);
CREATE INDEX IX_AuditLogs_User ON AuditLogs(UserId, Timestamp) WHERE UserId IS NOT NULL;
CREATE INDEX IX_AuditLogs_Session ON AuditLogs(SessionId, Timestamp) WHERE SessionId IS NOT NULL;
CREATE INDEX IX_AuditLogs_Risk ON AuditLogs(RiskLevel, Timestamp) WHERE RiskLevel > 0;
CREATE INDEX IX_AuditLogs_Correlation ON AuditLogs(CorrelationId) WHERE CorrelationId IS NOT NULL;

-- Chat message indexes
CREATE INDEX IX_ChatMessages_Session ON ChatMessages(SessionId, Timestamp);
CREATE INDEX IX_ChatMessages_Agent ON ChatMessages(AgentId, Timestamp);
CREATE INDEX IX_ChatMessages_Thread ON ChatMessages(ThreadId, Timestamp) WHERE ThreadId IS NOT NULL;
CREATE INDEX IX_ChatMessages_Task ON ChatMessages(LinkedTaskId) WHERE LinkedTaskId IS NOT NULL;
CREATE INDEX IX_ChatMessages_Delivery ON ChatMessages(DeliveryStatus, Timestamp);

-- Workflow execution indexes
CREATE INDEX IX_WorkflowExecutions_Definition ON WorkflowExecutions(WorkflowDefinitionId, StartedAt);
CREATE INDEX IX_WorkflowExecutions_Status ON WorkflowExecutions(Status, StartedAt);
CREATE INDEX IX_WorkflowExecutions_User ON WorkflowExecutions(TriggerUserId, StartedAt) WHERE TriggerUserId IS NOT NULL;
CREATE INDEX IX_WorkflowExecutions_Parent ON WorkflowExecutions(ParentExecutionId) WHERE ParentExecutionId IS NOT NULL;
```

## Entity Framework Core Configuration

### 1. DbContext Implementation

```csharp
public class OrchestraDbContext : DbContext
{
    private readonly IConfiguration _configuration;
    private readonly IUserContextService _userContext;
    private readonly IAuditService _auditService;

    public OrchestraDbContext(
        DbContextOptions<OrchestraDbContext> options,
        IConfiguration configuration,
        IUserContextService userContext,
        IAuditService auditService) : base(options)
    {
        _configuration = configuration;
        _userContext = userContext;
        _auditService = auditService;
    }

    // Core domain entities
    public DbSet<Agent> Agents { get; set; }
    public DbSet<TaskRecord> Tasks { get; set; }
    public DbSet<Repository> Repositories { get; set; }
    public DbSet<UserPreference> UserPreferences { get; set; }

    // Analytics and monitoring
    public DbSet<PerformanceMetric> PerformanceMetrics { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    // Communication
    public DbSet<ChatMessage> ChatMessages { get; set; }

    // Workflow management
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
    public DbSet<WorkflowExecution> WorkflowExecutions { get; set; }

    // Template system
    public DbSet<TaskTemplate> TaskTemplates { get; set; }
    public DbSet<TemplateCategory> TemplateCategories { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=orchestra.db";

            optionsBuilder.UseSqlite(connectionString, options =>
            {
                options.CommandTimeout(30);
            })
            .EnableSensitiveDataLogging(_configuration.GetValue<bool>("Database:EnableSensitiveDataLogging"))
            .EnableDetailedErrors(_configuration.GetValue<bool>("Database:EnableDetailedErrors"))
            .LogTo(Console.WriteLine, LogLevel.Information, DbContextLoggerOptions.SingleLine);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrchestraDbContext).Assembly);

        // Configure global query filters
        ConfigureGlobalQueryFilters(modelBuilder);

        // Configure value converters
        ConfigureValueConverters(modelBuilder);

        // Configure computed columns
        ConfigureComputedColumns(modelBuilder);

        // Configure triggers (if supported)
        ConfigureTriggers(modelBuilder);
    }

    private void ConfigureGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        // Soft delete filter for agents
        modelBuilder.Entity<Agent>().HasQueryFilter(e => !e.IsDeleted);

        // Multi-tenancy filter for user preferences (if needed)
        modelBuilder.Entity<UserPreference>().HasQueryFilter(e =>
            e.UserId == _userContext.CurrentUserId || e.UserId == "default");
    }

    private void ConfigureValueConverters(ModelBuilder modelBuilder)
    {
        // TimeSpan to string conversion
        var timeSpanConverter = new ValueConverter<TimeSpan, string>(
            v => v.ToString("c"),
            v => TimeSpan.Parse(v));

        modelBuilder.Entity<Agent>()
            .Property(e => e.HealthCheckInterval)
            .HasConversion(timeSpanConverter);

        modelBuilder.Entity<Agent>()
            .Property(e => e.TotalExecutionTime)
            .HasConversion(timeSpanConverter);

        modelBuilder.Entity<TaskRecord>()
            .Property(e => e.ExecutionDuration)
            .HasConversion(timeSpanConverter);

        // Enum to integer conversion
        modelBuilder.Entity<Agent>()
            .Property(e => e.Status)
            .HasConversion<int>();

        modelBuilder.Entity<TaskRecord>()
            .Property(e => e.Status)
            .HasConversion<int>();

        modelBuilder.Entity<TaskRecord>()
            .Property(e => e.Priority)
            .HasConversion<int>();

        // JSON serialization for complex objects
        var stringListConverter = new ValueConverter<List<string>, string>(
            v => string.Join(',', v),
            v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

        modelBuilder.Entity<Repository>()
            .Property(e => e.AllowedOperations)
            .HasConversion(stringListConverter);
    }

    private void ConfigureComputedColumns(ModelBuilder modelBuilder)
    {
        // Success rate computed column for agents
        modelBuilder.Entity<Agent>()
            .Property(e => e.SuccessRate)
            .HasComputedColumnSql(@"
                CASE
                    WHEN (TotalTasksCompleted + TotalTasksFailed) = 0 THEN 0.0
                    ELSE CAST(TotalTasksCompleted AS REAL) / (TotalTasksCompleted + TotalTasksFailed)
                END");

        // Task duration computed column
        modelBuilder.Entity<TaskRecord>()
            .Property(e => e.ComputedDuration)
            .HasComputedColumnSql(@"
                CASE
                    WHEN StartedAt IS NOT NULL AND CompletedAt IS NOT NULL
                    THEN (julianday(CompletedAt) - julianday(StartedAt)) * 86400000
                    ELSE NULL
                END");
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update audit fields
        UpdateAuditFields();

        // Capture changes for auditing
        var auditEntries = await _auditService.CaptureChangesAsync(ChangeTracker);

        var result = await base.SaveChangesAsync(cancellationToken);

        // Save audit entries after successful save
        await _auditService.SaveAuditEntriesAsync(auditEntries);

        return result;
    }

    private void UpdateAuditFields()
    {
        var currentUser = _userContext.CurrentUserId ?? "system";
        var currentTime = DateTime.UtcNow;

        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditable &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (IAuditable)entry.Entity;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = currentTime;
                entity.CreatedBy = currentUser;
            }

            entity.UpdatedAt = currentTime;
            entity.UpdatedBy = currentUser;
        }
    }
}
```

### 2. Entity Configuration Classes

```csharp
public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents");

        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(128);

        // Required properties
        builder.Property(e => e.Name).IsRequired().HasMaxLength(255);
        builder.Property(e => e.Type).IsRequired().HasMaxLength(100);
        builder.Property(e => e.RepositoryPath).IsRequired().HasMaxLength(500);

        // Indexes
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_Agents_Status");
        builder.HasIndex(e => e.RepositoryPath).HasDatabaseName("IX_Agents_Repository");
        builder.HasIndex(e => new { e.Status, e.RepositoryPath }).HasDatabaseName("IX_Agents_Performance");
        builder.HasIndex(e => new { e.LastPing, e.Status }).HasDatabaseName("IX_Agents_Health");

        // Relationships
        builder.HasOne(e => e.Repository)
               .WithMany(r => r.Agents)
               .HasForeignKey(e => e.RepositoryId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.AssignedTasks)
               .WithOne(t => t.Agent)
               .HasForeignKey(t => t.AgentId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.PerformanceMetrics)
               .WithOne()
               .HasForeignKey("EntityId")
               .HasPrincipalKey(e => e.Id)
               .OnDelete(DeleteBehavior.Cascade);

        // Value conversions
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.HealthCheckInterval).HasConversion<string>();
        builder.Property(e => e.TotalExecutionTime).HasConversion<string>();

        // Computed columns
        builder.Property(e => e.SuccessRate)
               .HasComputedColumnSql(@"
                   CASE
                       WHEN (TotalTasksCompleted + TotalTasksFailed) = 0 THEN 0.0
                       ELSE CAST(TotalTasksCompleted AS REAL) / (TotalTasksCompleted + TotalTasksFailed)
                   END");

        // Soft delete configuration
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Check constraints
        builder.HasCheckConstraint("CK_Agents_MaxConcurrentTasks", "MaxConcurrentTasks > 0 AND MaxConcurrentTasks <= 10");
        builder.HasCheckConstraint("CK_Agents_TotalTasks", "TotalTasksCompleted >= 0 AND TotalTasksFailed >= 0");
    }
}

public class TaskRecordConfiguration : IEntityTypeConfiguration<TaskRecord>
{
    public void Configure(EntityTypeBuilder<TaskRecord> builder)
    {
        builder.ToTable("Tasks");

        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(128);

        // Required properties
        builder.Property(e => e.Command).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.RepositoryPath).IsRequired().HasMaxLength(500);

        // Indexes for performance
        builder.HasIndex(e => new { e.Status, e.Priority, e.CreatedAt }).HasDatabaseName("IX_Tasks_Status_Priority");
        builder.HasIndex(e => new { e.AgentId, e.Status }).HasDatabaseName("IX_Tasks_Agent_Status");
        builder.HasIndex(e => e.CorrelationId).HasDatabaseName("IX_Tasks_Correlation");
        builder.HasIndex(e => e.HangfireJobId).HasDatabaseName("IX_Tasks_Hangfire");
        builder.HasIndex(e => new { e.WorkflowId, e.WorkflowStep }).HasDatabaseName("IX_Tasks_Workflow");

        // Relationships
        builder.HasOne(e => e.Agent)
               .WithMany(a => a.AssignedTasks)
               .HasForeignKey(e => e.AgentId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Repository)
               .WithMany(r => r.Tasks)
               .HasForeignKey(e => e.RepositoryId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ParentTask)
               .WithMany(t => t.ChildTasks)
               .HasForeignKey(e => e.ParentTaskId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Workflow)
               .WithMany(w => w.Tasks)
               .HasForeignKey(e => e.WorkflowId)
               .OnDelete(DeleteBehavior.SetNull);

        // Value conversions
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Priority).HasConversion<int>();
        builder.Property(e => e.ExecutionDuration).HasConversion<string>();

        // Check constraints
        builder.HasCheckConstraint("CK_Tasks_RetryCount", "RetryCount >= 0 AND RetryCount <= 10");
        builder.HasCheckConstraint("CK_Tasks_Progress", "ProgressPercentage >= 0 AND ProgressPercentage <= 100");
        builder.HasCheckConstraint("CK_Tasks_WorkflowStep", "WorkflowStep >= 0");
        builder.HasCheckConstraint("CK_Tasks_Timing", @"
            (StartedAt IS NULL OR StartedAt >= CreatedAt) AND
            (CompletedAt IS NULL OR (StartedAt IS NOT NULL AND CompletedAt >= StartedAt))");
    }
}

public class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.ToTable("Repositories");

        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(128);

        // Unique constraints
        builder.HasIndex(e => e.Path).IsUnique().HasDatabaseName("IX_Repositories_Path_Unique");

        // Required properties
        builder.Property(e => e.Name).IsRequired().HasMaxLength(255);
        builder.Property(e => e.Path).IsRequired().HasMaxLength(500);

        // Performance indexes
        builder.HasIndex(e => new { e.IsActive, e.PerformanceRating }).HasDatabaseName("IX_Repositories_Active_Performance");
        builder.HasIndex(e => new { e.LastTaskExecutedAt, e.TotalTasks }).HasDatabaseName("IX_Repositories_Usage");

        // Relationships
        builder.HasMany(e => e.Agents)
               .WithOne(a => a.Repository)
               .HasForeignKey(a => a.RepositoryId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.Tasks)
               .WithOne(t => t.Repository)
               .HasForeignKey(t => t.RepositoryId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.UserPreferences)
               .WithOne(p => p.Repository)
               .HasForeignKey(p => p.RepositoryId)
               .OnDelete(DeleteBehavior.Cascade);

        // Value conversions
        builder.Property(e => e.Type).HasConversion<int>();
        builder.Property(e => e.AccessLevel).HasConversion<int>();
        builder.Property(e => e.TotalExecutionTime).HasConversion<string>();

        // JSON serialization for complex types
        builder.Property(e => e.AllowedOperations)
               .HasConversion(
                   v => string.Join(',', v),
                   v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

        // Check constraints
        builder.HasCheckConstraint("CK_Repositories_Statistics", @"
            TotalTasks >= 0 AND
            SuccessfulTasks >= 0 AND
            FailedTasks >= 0 AND
            SuccessfulTasks + FailedTasks <= TotalTasks");

        builder.HasCheckConstraint("CK_Repositories_Ratings", @"
            PerformanceRating >= 0 AND PerformanceRating <= 10 AND
            ReliabilityScore >= 0 AND ReliabilityScore <= 1");
    }
}
```

## Repository Pattern Implementation

### 1. Generic Repository with Advanced Features

```csharp
public interface IGenericRepository<T> where T : class
{
    // Basic CRUD operations
    Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<T?> GetByIdAsync(object id, params string[] includeProperties);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    // Advanced querying
    Task<PagedResult<T>> GetPagedAsync<TKey>(
        Expression<Func<T, bool>>? predicate = null,
        Expression<Func<T, TKey>>? orderBy = null,
        bool ascending = true,
        int pageNumber = 1,
        int pageSize = 20,
        params string[] includeProperties);

    Task<IEnumerable<T>> FindWithSpecificationAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

    // Aggregation operations
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default);
    Task<double> AverageAsync(Expression<Func<T, double>> selector, CancellationToken cancellationToken = default);

    // Modification operations
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(object id, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    // Bulk operations
    Task<int> BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task<int> BulkUpdateAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateExpression, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    // Caching operations
    Task<T?> GetFromCacheAsync(string cacheKey, Func<Task<T?>> factory, TimeSpan? expiration = null);
    Task InvalidateCacheAsync(string cacheKey);
    Task InvalidateCachePatternAsync(string pattern);
}

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly OrchestraDbContext _context;
    protected readonly DbSet<T> _dbSet;
    protected readonly IMemoryCache _cache;
    protected readonly ILogger<GenericRepository<T>> _logger;
    protected readonly IQueryOptimizer _queryOptimizer;

    public GenericRepository(
        OrchestraDbContext context,
        IMemoryCache cache,
        ILogger<GenericRepository<T>> logger,
        IQueryOptimizer queryOptimizer)
    {
        _context = context;
        _dbSet = context.Set<T>();
        _cache = cache;
        _logger = logger;
        _queryOptimizer = queryOptimizer;
    }

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey($"{typeof(T).Name}_{id}");

        // Try cache first
        if (_cache.TryGetValue(cacheKey, out T? cachedEntity))
        {
            _logger.LogDebug("Retrieved {EntityType} with ID {Id} from cache", typeof(T).Name, id);
            return cachedEntity;
        }

        // Query database
        var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);

        if (entity != null)
        {
            // Cache the result
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                SlidingExpiration = TimeSpan.FromMinutes(5),
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(cacheKey, entity, cacheOptions);
            _logger.LogDebug("Cached {EntityType} with ID {Id}", typeof(T).Name, id);
        }

        return entity;
    }

    public virtual async Task<PagedResult<T>> GetPagedAsync<TKey>(
        Expression<Func<T, bool>>? predicate = null,
        Expression<Func<T, TKey>>? orderBy = null,
        bool ascending = true,
        int pageNumber = 1,
        int pageSize = 20,
        params string[] includeProperties)
    {
        var query = _dbSet.AsQueryable();

        // Apply includes
        foreach (var includeProperty in includeProperties)
        {
            query = query.Include(includeProperty);
        }

        // Apply filter
        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync();

        // Apply ordering
        if (orderBy != null)
        {
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
        }

        // Apply paging
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }

    public virtual async Task<IEnumerable<T>> FindWithSpecificationAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        // Apply specification criteria
        query = specification.Apply(query);

        return await query.ToListAsync(cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = await _dbSet.AddAsync(entity, cancellationToken);

        // Invalidate related cache entries
        InvalidateEntityCache(entity);

        return entry.Entity;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);

        // Invalidate related cache entries
        InvalidateEntityCache(entity);

        await Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        // Check if entity supports soft delete
        if (entity is ISoftDeletable softDeletable)
        {
            softDeletable.IsDeleted = true;
            softDeletable.DeletedAt = DateTime.UtcNow;
            softDeletable.DeletedBy = GetCurrentUserId();
            _dbSet.Update(entity);
        }
        else
        {
            _dbSet.Remove(entity);
        }

        // Invalidate related cache entries
        InvalidateEntityCache(entity);

        await Task.CompletedTask;
    }

    // Bulk operations with performance optimization
    public virtual async Task<int> BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var entitiesList = entities.ToList();

        if (!entitiesList.Any())
            return 0;

        // Use batch insert for better performance
        await _dbSet.AddRangeAsync(entitiesList, cancellationToken);

        // Invalidate relevant cache patterns
        InvalidateCachePattern($"{typeof(T).Name}_*");

        return entitiesList.Count;
    }

    public virtual async Task<int> BulkUpdateAsync(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, T>> updateExpression,
        CancellationToken cancellationToken = default)
    {
        // For EF Core 6+, use ExecuteUpdateAsync
        var affectedRows = await _dbSet
            .Where(predicate)
            .ExecuteUpdateAsync(updateExpression, cancellationToken);

        // Invalidate cache
        InvalidateCachePattern($"{typeof(T).Name}_*");

        return affectedRows;
    }

    // Caching helpers
    private string GenerateCacheKey(string key)
    {
        return $"OrchestraDB:{key}";
    }

    private void InvalidateEntityCache(T entity)
    {
        if (entity is IIdentifiable identifiable)
        {
            var cacheKey = GenerateCacheKey($"{typeof(T).Name}_{identifiable.Id}");
            _cache.Remove(cacheKey);
        }

        // Also invalidate any list caches
        InvalidateCachePattern($"{typeof(T).Name}_List_*");
    }

    private void InvalidateCachePattern(string pattern)
    {
        // Implementation depends on cache provider
        // For MemoryCache, we'd need to track keys or use a different approach
        _logger.LogDebug("Invalidated cache pattern: {Pattern}", pattern);
    }

    private string GetCurrentUserId()
    {
        // Get current user from context
        return "system"; // Placeholder
    }
}
```

### 2. Specialized Repository Implementations

```csharp
public interface IAgentRepository : IGenericRepository<Agent>
{
    // Agent-specific operations
    Task<IEnumerable<Agent>> GetAvailableAgentsAsync(string? repositoryPath = null, CancellationToken cancellationToken = default);
    Task<Agent?> GetBestAvailableAgentAsync(string repositoryPath, AgentSelectionCriteria criteria, CancellationToken cancellationToken = default);
    Task<IEnumerable<Agent>> GetAgentsByStatusAsync(AgentStatus status, CancellationToken cancellationToken = default);
    Task<AgentStatistics> GetAgentStatisticsAsync(string agentId, TimeSpan? period = null, CancellationToken cancellationToken = default);
    Task UpdateAgentStatusAsync(string agentId, AgentStatus status, string? currentTask = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Agent>> GetStaleAgentsAsync(TimeSpan threshold, CancellationToken cancellationToken = default);
    Task UpdateAgentPerformanceMetricsAsync(string agentId, AgentPerformanceMetrics metrics, CancellationToken cancellationToken = default);
    Task<Dictionary<string, AgentLoadInfo>> GetAgentLoadDistributionAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Agent>> GetAgentsRequiringMaintenanceAsync(CancellationToken cancellationToken = default);
}

public class AgentRepository : GenericRepository<Agent>, IAgentRepository
{
    private readonly IAgentSelectionStrategy _selectionStrategy;
    private readonly IPerformanceCalculator _performanceCalculator;

    public AgentRepository(
        OrchestraDbContext context,
        IMemoryCache cache,
        ILogger<AgentRepository> logger,
        IQueryOptimizer queryOptimizer,
        IAgentSelectionStrategy selectionStrategy,
        IPerformanceCalculator performanceCalculator)
        : base(context, cache, logger, queryOptimizer)
    {
        _selectionStrategy = selectionStrategy;
        _performanceCalculator = performanceCalculator;
    }

    public async Task<IEnumerable<Agent>> GetAvailableAgentsAsync(string? repositoryPath = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey($"AvailableAgents_{repositoryPath ?? "All"}");

        return await GetFromCacheAsync(cacheKey, async () =>
        {
            var query = _dbSet
                .Where(a => a.Status == AgentStatus.Idle && !a.IsDeleted)
                .Include(a => a.PerformanceMetrics.Where(pm => pm.Timestamp >= DateTime.UtcNow.AddHours(-24)));

            if (!string.IsNullOrEmpty(repositoryPath))
            {
                query = query.Where(a => a.RepositoryPath == repositoryPath || a.RepositoryPath == "*");
            }

            var agents = await query
                .OrderBy(a => a.TotalTasksCompleted) // Load balancing
                .ThenByDescending(a => a.SuccessRate)
                .ToListAsync(cancellationToken);

            return agents.AsEnumerable();
        }, TimeSpan.FromMinutes(2));
    }

    public async Task<Agent?> GetBestAvailableAgentAsync(string repositoryPath, AgentSelectionCriteria criteria, CancellationToken cancellationToken = default)
    {
        var availableAgents = await GetAvailableAgentsAsync(repositoryPath, cancellationToken);

        if (!availableAgents.Any())
        {
            return null;
        }

        // Use strategy pattern for agent selection
        return await _selectionStrategy.SelectBestAgentAsync(availableAgents, criteria);
    }

    public async Task<AgentStatistics> GetAgentStatisticsAsync(string agentId, TimeSpan? period = null, CancellationToken cancellationToken = default)
    {
        var periodDays = period?.Days ?? 30;
        var cacheKey = GenerateCacheKey($"AgentStats_{agentId}_{periodDays}");

        return await GetFromCacheAsync(cacheKey, async () =>
        {
            var cutoffDate = DateTime.UtcNow.Subtract(period ?? TimeSpan.FromDays(30));

            var agent = await _dbSet
                .Include(a => a.AssignedTasks.Where(t => t.CreatedAt >= cutoffDate))
                .Include(a => a.PerformanceMetrics.Where(pm => pm.Timestamp >= cutoffDate))
                .FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);

            if (agent == null)
            {
                throw new EntityNotFoundException($"Agent {agentId} not found");
            }

            return await _performanceCalculator.CalculateAgentStatisticsAsync(agent, period);
        }, TimeSpan.FromMinutes(10));
    }

    public async Task UpdateAgentStatusAsync(string agentId, AgentStatus status, string? currentTask = null, CancellationToken cancellationToken = default)
    {
        var updateExpression = status switch
        {
            AgentStatus.Idle => (Expression<Func<Agent, Agent>>)(a => new Agent
            {
                Status = status,
                CurrentTask = null,
                LastPing = DateTime.UtcNow
            }),
            AgentStatus.Busy => (Expression<Func<Agent, Agent>>)(a => new Agent
            {
                Status = status,
                CurrentTask = currentTask,
                LastPing = DateTime.UtcNow
            }),
            _ => (Expression<Func<Agent, Agent>>)(a => new Agent
            {
                Status = status,
                LastPing = DateTime.UtcNow
            })
        };

        await BulkUpdateAsync(
            a => a.Id == agentId,
            updateExpression,
            cancellationToken);

        // Invalidate cache
        InvalidateCacheAsync(GenerateCacheKey($"Agent_{agentId}"));
        InvalidateCachePattern("AvailableAgents_*");
    }

    public async Task<Dictionary<string, AgentLoadInfo>> GetAgentLoadDistributionAsync(CancellationToken cancellationToken = default)
    {
        var agents = await _dbSet
            .Include(a => a.AssignedTasks.Where(t => t.Status == TaskStatus.InProgress || t.Status == TaskStatus.Assigned))
            .Where(a => !a.IsDeleted)
            .ToListAsync(cancellationToken);

        return agents.ToDictionary(
            a => a.Id,
            a => new AgentLoadInfo
            {
                AgentId = a.Id,
                AgentName = a.Name,
                Status = a.Status,
                CurrentTasks = a.AssignedTasks.Count,
                MaxConcurrentTasks = a.MaxConcurrentTasks,
                LoadPercentage = (double)a.AssignedTasks.Count / a.MaxConcurrentTasks,
                AverageExecutionTime = a.AverageExecutionTime,
                SuccessRate = a.SuccessRate
            });
    }
}

public interface ITaskRepository : IGenericRepository<TaskRecord>
{
    // Task-specific operations
    Task<IEnumerable<TaskRecord>> GetPendingTasksAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<TaskRecord>> GetTasksByAgentAsync(string agentId, TaskStatus? status = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<TaskRecord>> GetTasksByRepositoryAsync(string repositoryId, TimeSpan? period = null, CancellationToken cancellationToken = default);
    Task<TaskAnalytics> GetTaskAnalyticsAsync(TimeSpan period, string? agentId = null, string? repositoryId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<TaskRecord>> GetFailedTasksAsync(TimeSpan? period = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<TaskRecord>> GetLongRunningTasksAsync(TimeSpan threshold, CancellationToken cancellationToken = default);
    Task UpdateTaskProgressAsync(string taskId, int progressPercentage, string? progressMessage = null, CancellationToken cancellationToken = default);
    Task CompleteTaskAsync(string taskId, TaskStatus finalStatus, string? result = null, string? errorMessage = null, Dictionary<string, object>? metrics = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<TaskRecord>> GetTasksByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<WorkflowTaskTree> GetWorkflowTaskTreeAsync(string workflowId, CancellationToken cancellationToken = default);
}

public class TaskRepository : GenericRepository<TaskRecord>, ITaskRepository
{
    private readonly ITaskAnalyticsCalculator _analyticsCalculator;
    private readonly ITaskMetricsCollector _metricsCollector;

    public TaskRepository(
        OrchestraDbContext context,
        IMemoryCache cache,
        ILogger<TaskRepository> logger,
        IQueryOptimizer queryOptimizer,
        ITaskAnalyticsCalculator analyticsCalculator,
        ITaskMetricsCollector metricsCollector)
        : base(context, cache, logger, queryOptimizer)
    {
        _analyticsCalculator = analyticsCalculator;
        _metricsCollector = metricsCollector;
    }

    public async Task<IEnumerable<TaskRecord>> GetPendingTasksAsync(CancellationToken cancellationToken = default)
    {
        return await FindAsync(
            t => t.Status == TaskStatus.Pending || t.Status == TaskStatus.Assigned,
            cancellationToken);
    }

    public async Task<TaskAnalytics> GetTaskAnalyticsAsync(
        TimeSpan period,
        string? agentId = null,
        string? repositoryId = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey($"TaskAnalytics_{period.Days}_{agentId ?? "All"}_{repositoryId ?? "All"}");

        return await GetFromCacheAsync(cacheKey, async () =>
        {
            var fromDate = DateTime.UtcNow.Subtract(period);

            var query = _dbSet.Where(t => t.CreatedAt >= fromDate);

            if (!string.IsNullOrEmpty(agentId))
            {
                query = query.Where(t => t.AgentId == agentId);
            }

            if (!string.IsNullOrEmpty(repositoryId))
            {
                query = query.Where(t => t.RepositoryId == repositoryId);
            }

            return await _analyticsCalculator.CalculateTaskAnalyticsAsync(query, period);
        }, TimeSpan.FromMinutes(15));
    }

    public async Task UpdateTaskProgressAsync(
        string taskId,
        int progressPercentage,
        string? progressMessage = null,
        CancellationToken cancellationToken = default)
    {
        await BulkUpdateAsync(
            t => t.Id == taskId,
            t => new TaskRecord
            {
                ProgressPercentage = progressPercentage,
                ProgressMessage = progressMessage,
                LastProgressUpdate = DateTime.UtcNow
            },
            cancellationToken);

        // Collect progress metrics
        await _metricsCollector.RecordTaskProgressAsync(taskId, progressPercentage, progressMessage);
    }

    public async Task CompleteTaskAsync(
        string taskId,
        TaskStatus finalStatus,
        string? result = null,
        string? errorMessage = null,
        Dictionary<string, object>? metrics = null,
        CancellationToken cancellationToken = default)
    {
        var task = await GetByIdAsync(taskId, cancellationToken);
        if (task == null)
        {
            throw new EntityNotFoundException($"Task {taskId} not found");
        }

        // Calculate execution duration
        var executionDuration = task.StartedAt.HasValue
            ? DateTime.UtcNow - task.StartedAt.Value
            : TimeSpan.Zero;

        await BulkUpdateAsync(
            t => t.Id == taskId,
            t => new TaskRecord
            {
                Status = finalStatus,
                CompletedAt = DateTime.UtcNow,
                ExecutionDuration = executionDuration,
                Result = result,
                ErrorMessage = errorMessage,
                ProgressPercentage = 100
            },
            cancellationToken);

        // Update agent performance metrics
        if (!string.IsNullOrEmpty(task.AgentId))
        {
            await UpdateAgentPerformanceAsync(task.AgentId, finalStatus, executionDuration);
        }

        // Collect completion metrics
        await _metricsCollector.RecordTaskCompletionAsync(taskId, finalStatus, executionDuration, metrics);
    }

    private async Task UpdateAgentPerformanceAsync(string agentId, TaskStatus taskStatus, TimeSpan executionDuration)
    {
        var isSuccess = taskStatus == TaskStatus.Completed;

        await BulkUpdateAsync(
            a => a.Id == agentId,
            a => new Agent
            {
                TotalTasksCompleted = isSuccess ? a.TotalTasksCompleted + 1 : a.TotalTasksCompleted,
                TotalTasksFailed = !isSuccess ? a.TotalTasksFailed + 1 : a.TotalTasksFailed,
                TotalExecutionTime = a.TotalExecutionTime + executionDuration,
                AverageExecutionTime = (a.TotalExecutionTime.TotalMilliseconds + executionDuration.TotalMilliseconds) /
                                     (a.TotalTasksCompleted + a.TotalTasksFailed + 1)
            });
    }
}
```

## Migration and Data Management

### 1. Database Migration Service

```csharp
public interface IDatabaseMigrationService
{
    Task EnsureDatabaseCreatedAsync();
    Task MigrateFromLegacyDataAsync(string legacyDataPath);
    Task CreateBackupAsync(string backupPath);
    Task RestoreBackupAsync(string backupPath);
    Task<MigrationStatus> GetMigrationStatusAsync();
    Task ValidateDataIntegrityAsync();
}

public class DatabaseMigrationService : IDatabaseMigrationService
{
    private readonly OrchestraDbContext _context;
    private readonly ILegacyDataMigrator _legacyMigrator;
    private readonly IDataIntegrityValidator _integrityValidator;
    private readonly IBackupService _backupService;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public async Task EnsureDatabaseCreatedAsync()
    {
        try
        {
            _logger.LogInformation("Checking database migration status");

            // Check if database exists
            var canConnect = await _context.Database.CanConnectAsync();
            if (!canConnect)
            {
                _logger.LogInformation("Database does not exist, creating new database");
                await _context.Database.EnsureCreatedAsync();
            }

            // Apply pending migrations
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
                    pendingMigrations.Count(), string.Join(", ", pendingMigrations));

                await _context.Database.MigrateAsync();
                _logger.LogInformation("Database migrations completed successfully");
            }
            else
            {
                _logger.LogInformation("Database is up to date");
            }

            // Seed default data if needed
            await SeedDefaultDataAsync();

            // Validate database integrity
            await ValidateDataIntegrityAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure database is created and migrated");
            throw new DatabaseMigrationException("Database migration failed", ex);
        }
    }

    public async Task MigrateFromLegacyDataAsync(string legacyDataPath)
    {
        _logger.LogInformation("Starting legacy data migration from {Path}", legacyDataPath);

        try
        {
            // Create backup before migration
            var backupPath = Path.Combine(Path.GetDirectoryName(legacyDataPath)!,
                $"backup_before_migration_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");
            await CreateBackupAsync(backupPath);

            // Perform migration
            var migrationResult = await _legacyMigrator.MigrateAsync(legacyDataPath);

            _logger.LogInformation("Legacy data migration completed - Migrated {AgentCount} agents, {TaskCount} tasks, {RepoCount} repositories",
                migrationResult.AgentCount, migrationResult.TaskCount, migrationResult.RepositoryCount);

            // Validate migrated data
            await ValidateDataIntegrityAsync();

            // Archive legacy data
            await ArchiveLegacyDataAsync(legacyDataPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Legacy data migration failed");
            throw new DataMigrationException("Failed to migrate legacy data", ex);
        }
    }

    private async Task SeedDefaultDataAsync()
    {
        // Check if data already exists
        if (await _context.UserPreferences.AnyAsync())
        {
            return; // Data already seeded
        }

        _logger.LogInformation("Seeding default data");

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Seed default user preferences
            var defaultPreferences = new[]
            {
                new UserPreference
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = "default",
                    Key = "ui.theme",
                    Value = "dark",
                    Type = PreferenceType.UI,
                    Category = "Appearance",
                    Description = "Default UI theme"
                },
                new UserPreference
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = "default",
                    Key = "ui.pageSize",
                    Value = "50",
                    Type = PreferenceType.UI,
                    Category = "Display",
                    Description = "Default page size for lists"
                },
                new UserPreference
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = "default",
                    Key = "system.autoRefresh",
                    Value = "true",
                    Type = PreferenceType.Behavior,
                    Category = "System",
                    Description = "Auto-refresh data in UI"
                },
                new UserPreference
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = "default",
                    Key = "notifications.enabled",
                    Value = "true",
                    Type = PreferenceType.Behavior,
                    Category = "Notifications",
                    Description = "Enable system notifications"
                }
            };

            await _context.UserPreferences.AddRangeAsync(defaultPreferences);

            // Seed default task templates
            var defaultTemplates = await CreateDefaultTaskTemplatesAsync();
            await _context.TaskTemplates.AddRangeAsync(defaultTemplates);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Default data seeded successfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to seed default data");
            throw;
        }
    }

    private async Task<List<TaskTemplate>> CreateDefaultTaskTemplatesAsync()
    {
        return new List<TaskTemplate>
        {
            new TaskTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Code Review",
                Description = "Comprehensive code review template",
                Category = "Development",
                Version = "1.0.0",
                IsPublic = true,
                Definition = JsonSerializer.Serialize(new
                {
                    steps = new[]
                    {
                        new { command = "git diff HEAD~1", description = "Show recent changes" },
                        new { command = "git log --oneline -10", description = "Show recent commits" },
                        new { command = "find . -name '*.cs' -exec grep -l 'TODO\\|FIXME\\|HACK' {} \\;", description = "Find code issues" }
                    },
                    parameters = new
                    {
                        branch = new { type = "string", default = "main", required = true },
                        depth = new { type = "number", default = 1, required = false }
                    }
                }),
                Tags = new List<string> { "code-review", "git", "quality" },
                CreatedBy = "system"
            },
            new TaskTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Build and Test",
                Description = "Standard build and test pipeline",
                Category = "CI/CD",
                Version = "1.0.0",
                IsPublic = true,
                Definition = JsonSerializer.Serialize(new
                {
                    steps = new[]
                    {
                        new { command = "dotnet restore", description = "Restore dependencies" },
                        new { command = "dotnet build", description = "Build project" },
                        new { command = "dotnet test", description = "Run tests" }
                    },
                    parameters = new
                    {
                        configuration = new { type = "string", default = "Release", required = false },
                        verbosity = new { type = "string", default = "minimal", required = false }
                    }
                }),
                Tags = new List<string> { "build", "test", "dotnet" },
                CreatedBy = "system"
            }
        };
    }

    public async Task ValidateDataIntegrityAsync()
    {
        _logger.LogInformation("Validating database integrity");

        var validationResults = await _integrityValidator.ValidateAsync();

        if (validationResults.HasErrors)
        {
            var errors = string.Join(", ", validationResults.Errors);
            _logger.LogError("Data integrity validation failed: {Errors}", errors);
            throw new DataIntegrityException($"Data integrity validation failed: {errors}");
        }

        _logger.LogInformation("Database integrity validation completed successfully");
    }
}
```

This comprehensive architecture provides a robust, scalable, and well-organized foundation for the AI Agent Orchestra's data persistence layer. The SQLite database integration maintains the lightweight characteristics suitable for desktop deployment while providing enterprise-grade features for data management, analytics, and system monitoring.