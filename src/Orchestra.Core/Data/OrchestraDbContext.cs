using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Orchestra.Core.Data.Entities;

namespace Orchestra.Core.Data;

/// <summary>
/// Основной контекст базы данных для AI Agent Orchestra
/// </summary>
public class OrchestraDbContext : DbContext
{
    /// <summary>
    /// Инициализирует новый экземпляр OrchestraDbContext
    /// </summary>
    /// <param name="options">Опции конфигурации DbContext</param>
    public OrchestraDbContext(DbContextOptions<OrchestraDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Агенты в системе оркестрации
    /// </summary>
    public DbSet<Agent> Agents { get; set; }

    /// <summary>
    /// Записи о задачах в системе
    /// </summary>
    public DbSet<TaskRecord> Tasks { get; set; }

    /// <summary>
    /// Репозитории проектов
    /// </summary>
    public DbSet<Repository> Repositories { get; set; }

    /// <summary>
    /// Пользовательские настройки
    /// </summary>
    public DbSet<UserPreference> UserPreferences { get; set; }

    /// <summary>
    /// Логи оркестрации
    /// </summary>
    public DbSet<OrchestrationLog> OrchestrationLogs { get; set; }

    /// <summary>
    /// Шаблоны задач
    /// </summary>
    public DbSet<TaskTemplate> TaskTemplates { get; set; }

    /// <summary>
    /// Определения рабочих процессов
    /// </summary>
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }

    /// <summary>
    /// Метрики производительности агентов
    /// </summary>
    public DbSet<PerformanceMetric> PerformanceMetrics { get; set; }

    /// <summary>
    /// Конфигурирует модель данных при создании
    /// </summary>
    /// <param name="modelBuilder">Построитель модели EF Core</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Agent Configuration
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.RepositoryPath);
            entity.HasIndex(e => new { e.Status, e.RepositoryPath });
            entity.HasIndex(e => e.SessionId);

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RepositoryPath).HasMaxLength(500).IsRequired();
            entity.Property(e => e.CurrentTask).HasMaxLength(128);
            entity.Property(e => e.SessionId).HasMaxLength(128);
            entity.Property(e => e.RepositoryId).HasMaxLength(128);

            entity.HasOne(e => e.Repository)
                  .WithMany(r => r.Agents)
                  .HasForeignKey(e => e.RepositoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(e => !e.IsDeleted);

            // Configure enum conversion
            entity.Property(e => e.Status)
                  .HasConversion<int>();

            // Configure TimeSpan properties
            entity.Property(e => e.HealthCheckInterval)
                  .HasConversion(
                      v => v.Ticks,
                      v => new TimeSpan(v));

            entity.Property(e => e.TotalExecutionTime)
                  .HasConversion(
                      v => v.Ticks,
                      v => new TimeSpan(v));
        });

        // TaskRecord Configuration
        modelBuilder.Entity<TaskRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Status, e.Priority });
            entity.HasIndex(e => new { e.AgentId, e.Status });
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.WorkflowId);

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Command).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.RepositoryPath).HasMaxLength(500).IsRequired();
            entity.Property(e => e.AgentId).HasMaxLength(128);
            entity.Property(e => e.RepositoryId).HasMaxLength(128);
            entity.Property(e => e.CorrelationId).HasMaxLength(128);
            entity.Property(e => e.WorkflowId).HasMaxLength(128);
            entity.Property(e => e.ParentTaskId).HasMaxLength(128);

            entity.HasOne(e => e.Agent)
                  .WithMany(a => a.AssignedTasks)
                  .HasForeignKey(e => e.AgentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Repository)
                  .WithMany(r => r.Tasks)
                  .HasForeignKey(e => e.RepositoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Workflow)
                  .WithMany(w => w.Tasks)
                  .HasForeignKey(e => e.WorkflowId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ParentTask)
                  .WithMany(t => t.ChildTasks)
                  .HasForeignKey(e => e.ParentTaskId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Configure enum conversions
            entity.Property(e => e.Status)
                  .HasConversion<int>();

            entity.Property(e => e.Priority)
                  .HasConversion<int>();

            // Configure TimeSpan properties
            entity.Property(e => e.ExecutionDuration)
                  .HasConversion(
                      v => v.HasValue ? v.Value.Ticks : (long?)null,
                      v => v.HasValue ? new TimeSpan(v.Value) : null);
        });

        // Repository Configuration
        modelBuilder.Entity<Repository>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Path).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.Type);

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Path).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.DefaultBranch).HasMaxLength(100);

            // Configure enum conversion
            entity.Property(e => e.Type)
                  .HasConversion<int>();

            // Configure TimeSpan properties
            entity.Property(e => e.TotalExecutionTime)
                  .HasConversion(
                      v => v.Ticks,
                      v => new TimeSpan(v));
        });

        // UserPreference Configuration
        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.Key, e.RepositoryId }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.Type });
            entity.HasIndex(e => e.Category);

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Key).HasMaxLength(255).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.RepositoryId).HasMaxLength(128);
            entity.Property(e => e.Category).HasMaxLength(100);

            entity.HasOne(e => e.Repository)
                  .WithMany(r => r.UserPreferences)
                  .HasForeignKey(e => e.RepositoryId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Configure enum conversion
            entity.Property(e => e.Type)
                  .HasConversion<int>();
        });

        // PerformanceMetric Configuration
        modelBuilder.Entity<PerformanceMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.MetricName);
            entity.HasIndex(e => e.MeasuredAt);
            entity.HasIndex(e => new { e.AgentId, e.MetricName, e.MeasuredAt });

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.AgentId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.MetricName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasOne(e => e.Agent)
                  .WithMany(a => a.PerformanceMetrics)
                  .HasForeignKey(e => e.AgentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // OrchestrationLog Configuration
        modelBuilder.Entity<OrchestrationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Level);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.AgentId, e.CreatedAt });
            entity.HasIndex(e => new { e.TaskId, e.CreatedAt });

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AgentId).HasMaxLength(128);
            entity.Property(e => e.TaskId).HasMaxLength(128);
            entity.Property(e => e.RepositoryId).HasMaxLength(128);

            entity.HasOne(e => e.Agent)
                  .WithMany()
                  .HasForeignKey(e => e.AgentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Task)
                  .WithMany()
                  .HasForeignKey(e => e.TaskId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Repository)
                  .WithMany()
                  .HasForeignKey(e => e.RepositoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Configure enum conversion
            entity.Property(e => e.Level)
                  .HasConversion<int>();
        });

        // TaskTemplate Configuration
        modelBuilder.Entity<TaskTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CommandTemplate).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(100);

            // Configure enum conversion
            entity.Property(e => e.DefaultPriority)
                  .HasConversion<int>();
        });

        // WorkflowDefinition Configuration
        modelBuilder.Entity<WorkflowDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.Name, e.Version });

            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
        });
    }

    /// <summary>
    /// Асинхронно сохраняет изменения с автоматическим обновлением временных меток
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Количество измененных записей</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Синхронно сохраняет изменения с автоматическим обновлением временных меток
    /// </summary>
    /// <returns>Количество измененных записей</returns>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// Обновляет временные метки для сущностей, реализующих ITimestamped
    /// </summary>
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is ITimestamped &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified))
            .ToList();

        if (!entries.Any())
        {
            return;
        }

        var utcNow = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            var entity = (ITimestamped)entry.Entity;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = utcNow;
            }

            entity.UpdatedAt = utcNow;
        }
    }
}