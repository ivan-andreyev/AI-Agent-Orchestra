using Microsoft.EntityFrameworkCore;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Models;

namespace Orchestra.API.Services;

/// <summary>
/// Репозиторий для работы с задачами через Entity Framework
/// </summary>
public class TaskRepository
{
    private readonly OrchestraDbContext _context;

    public TaskRepository(OrchestraDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Добавить задачу в очередь
    /// </summary>
    public async Task<string> QueueTaskAsync(string command, string repositoryPath, Orchestra.Core.Models.TaskPriority priority)
    {
        try
        {
            var taskId = Guid.NewGuid().ToString();

            var task = new TaskRecord
            {
                Id = taskId,
                Command = command,
                RepositoryPath = repositoryPath,
                Priority = ConvertToEntityTaskPriority(priority),
                Status = Core.Data.Entities.TaskStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RetryCount = 0,
                WorkflowStep = 0
            };

            // Попытаемся найти и привязать к репозиторию
            var repository = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Path == repositoryPath);

            if (repository != null)
            {
                task.RepositoryId = repository.Id;
            }

            await _context.Tasks.AddAsync(task);
            await _context.SaveChangesAsync();

            return taskId;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Получить следующую задачу для агента
    /// </summary>
    public async Task<TaskRequest?> GetNextTaskForAgentAsync(string agentId)
    {
        try
        {
            // Сначала получаем агента
            var agent = await _context.Agents
                .FirstOrDefaultAsync(a => a.Id == agentId && !a.IsDeleted);

            if (agent == null)
            {
                return null;
            }

            // Ищем незначенные задачи для репозитория агента, упорядоченные по приоритету и времени создания
            var task = await _context.Tasks
                .Where(t => t.Status == Core.Data.Entities.TaskStatus.Pending &&
                           t.RepositoryPath == agent.RepositoryPath)
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (task == null)
            {
                return null;
            }

            // Назначаем задачу агенту
            task.AgentId = agentId;
            task.Status = Core.Data.Entities.TaskStatus.Assigned;
            task.StartedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return ConvertToTaskRequest(task);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Получить все задачи в очереди
    /// </summary>
    public async Task<Queue<TaskRequest>> GetTaskQueueAsync()
    {
        var tasks = await _context.Tasks
            .Where(t => t.Status != Core.Data.Entities.TaskStatus.Completed &&
                       t.Status != Core.Data.Entities.TaskStatus.Cancelled &&
                       t.Status != Core.Data.Entities.TaskStatus.Failed)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();

        var queue = new Queue<TaskRequest>();
        foreach (var task in tasks)
        {
            queue.Enqueue(ConvertToTaskRequest(task));
        }

        return queue;
    }

    /// <summary>
    /// Получить задачи для конкретного репозитория
    /// </summary>
    public async Task<List<TaskRequest>> GetTasksByRepositoryAsync(string repositoryPath)
    {
        var tasks = await _context.Tasks
            .Where(t => t.RepositoryPath == repositoryPath)
            .OrderByDescending(t => t.CreatedAt)
            .Take(100) // Ограничиваем последними 100 задачами
            .ToListAsync();

        return tasks.Select(ConvertToTaskRequest).ToList();
    }

    /// <summary>
    /// Обновить статус задачи
    /// </summary>
    public async Task<bool> UpdateTaskStatusAsync(string taskId, Orchestra.Core.Models.TaskStatus status, string? result = null, string? errorMessage = null)
    {
        try
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                return false;
            }

            task.Status = ConvertToEntityTaskStatus(status);
            task.UpdatedAt = DateTime.UtcNow;

            if (status == Orchestra.Core.Models.TaskStatus.Completed || status == Orchestra.Core.Models.TaskStatus.Failed)
            {
                task.CompletedAt = DateTime.UtcNow;

                if (task.StartedAt.HasValue)
                {
                    task.ExecutionDuration = DateTime.UtcNow - task.StartedAt.Value;
                }
            }

            if (!string.IsNullOrEmpty(result))
            {
                task.Result = result;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                task.ErrorMessage = errorMessage;
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Конвертировать Entity TaskRecord в Web TaskRequest
    /// </summary>
    private static TaskRequest ConvertToTaskRequest(TaskRecord task)
    {
        return new TaskRequest(
            Id: task.Id,
            AgentId: task.AgentId ?? string.Empty,
            Command: task.Command,
            RepositoryPath: task.RepositoryPath,
            CreatedAt: task.CreatedAt,
            Priority: ConvertToWebTaskPriority(task.Priority),
            Status: ConvertToWebTaskStatus(task.Status),
            StartedAt: task.StartedAt,
            CompletedAt: task.CompletedAt
        );
    }

    /// <summary>
    /// Конвертировать Web TaskPriority в Entity TaskPriority
    /// </summary>
    private static Core.Data.Entities.TaskPriority ConvertToEntityTaskPriority(Orchestra.Core.Models.TaskPriority priority)
    {
        return priority switch
        {
            Orchestra.Core.Models.TaskPriority.Low => Core.Data.Entities.TaskPriority.Low,
            Orchestra.Core.Models.TaskPriority.Normal => Core.Data.Entities.TaskPriority.Normal,
            Orchestra.Core.Models.TaskPriority.High => Core.Data.Entities.TaskPriority.High,
            Orchestra.Core.Models.TaskPriority.Critical => Core.Data.Entities.TaskPriority.Critical,
            _ => Core.Data.Entities.TaskPriority.Normal
        };
    }

    /// <summary>
    /// Конвертировать Entity TaskPriority в Web TaskPriority
    /// </summary>
    private static Orchestra.Core.Models.TaskPriority ConvertToWebTaskPriority(Core.Data.Entities.TaskPriority priority)
    {
        return priority switch
        {
            Core.Data.Entities.TaskPriority.Low => Orchestra.Core.Models.TaskPriority.Low,
            Core.Data.Entities.TaskPriority.Normal => Orchestra.Core.Models.TaskPriority.Normal,
            Core.Data.Entities.TaskPriority.High => Orchestra.Core.Models.TaskPriority.High,
            Core.Data.Entities.TaskPriority.Critical => Orchestra.Core.Models.TaskPriority.Critical,
            _ => Orchestra.Core.Models.TaskPriority.Normal
        };
    }

    /// <summary>
    /// Конвертировать Web TaskStatus в Entity TaskStatus
    /// </summary>
    private static Core.Data.Entities.TaskStatus ConvertToEntityTaskStatus(Orchestra.Core.Models.TaskStatus status)
    {
        return status switch
        {
            Orchestra.Core.Models.TaskStatus.Pending => Core.Data.Entities.TaskStatus.Pending,
            Orchestra.Core.Models.TaskStatus.Assigned => Core.Data.Entities.TaskStatus.Assigned,
            Orchestra.Core.Models.TaskStatus.InProgress => Core.Data.Entities.TaskStatus.InProgress,
            Orchestra.Core.Models.TaskStatus.Completed => Core.Data.Entities.TaskStatus.Completed,
            Orchestra.Core.Models.TaskStatus.Failed => Core.Data.Entities.TaskStatus.Failed,
            Orchestra.Core.Models.TaskStatus.Cancelled => Core.Data.Entities.TaskStatus.Cancelled,
            _ => Core.Data.Entities.TaskStatus.Pending
        };
    }

    /// <summary>
    /// Конвертировать Entity TaskStatus в Web TaskStatus
    /// </summary>
    private static Orchestra.Core.Models.TaskStatus ConvertToWebTaskStatus(Core.Data.Entities.TaskStatus status)
    {
        return status switch
        {
            Core.Data.Entities.TaskStatus.Pending => Orchestra.Core.Models.TaskStatus.Pending,
            Core.Data.Entities.TaskStatus.Assigned => Orchestra.Core.Models.TaskStatus.Assigned,
            Core.Data.Entities.TaskStatus.InProgress => Orchestra.Core.Models.TaskStatus.InProgress,
            Core.Data.Entities.TaskStatus.Completed => Orchestra.Core.Models.TaskStatus.Completed,
            Core.Data.Entities.TaskStatus.Failed => Orchestra.Core.Models.TaskStatus.Failed,
            Core.Data.Entities.TaskStatus.Cancelled => Orchestra.Core.Models.TaskStatus.Cancelled,
            _ => Orchestra.Core.Models.TaskStatus.Pending
        };
    }
}