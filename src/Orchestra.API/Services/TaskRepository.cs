using Microsoft.EntityFrameworkCore;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Models;
using TaskPriority = Orchestra.Core.Models.TaskPriority;
using TaskStatus = Orchestra.Core.Models.TaskStatus;

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
                Priority = priority,
                Status = TaskStatus.Pending,
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
                .Where(t => t.Status == TaskStatus.Pending &&
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
            task.Status = TaskStatus.Assigned;
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
            .Where(t => t.Status != TaskStatus.Completed &&
                       t.Status != TaskStatus.Cancelled &&
                       t.Status != TaskStatus.Failed)
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

            task.Status = status;
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
            Priority: task.Priority,
            Status: task.Status,
            StartedAt: task.StartedAt,
            CompletedAt: task.CompletedAt
        );
    }

}