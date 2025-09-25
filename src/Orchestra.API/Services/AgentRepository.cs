using Microsoft.EntityFrameworkCore;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Web.Models;

namespace Orchestra.API.Services;

/// <summary>
/// Репозиторий для работы с агентами через Entity Framework
/// </summary>
public class AgentRepository
{
    private readonly OrchestraDbContext _context;

    public AgentRepository(OrchestraDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Получить всех активных агентов
    /// </summary>
    public async Task<List<AgentInfo>> GetAllActiveAgentsAsync()
    {
        var agents = await _context.Agents
            .Include(a => a.Repository)
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return agents.Select(ConvertToAgentInfo).ToList();
    }

    /// <summary>
    /// Получить агентов по пути репозитория
    /// </summary>
    public async Task<List<AgentInfo>> GetAgentsByRepositoryPathAsync(string repositoryPath)
    {
        var agents = await _context.Agents
            .Include(a => a.Repository)
            .Where(a => !a.IsDeleted && a.RepositoryPath == repositoryPath)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return agents.Select(ConvertToAgentInfo).ToList();
    }

    /// <summary>
    /// Зарегистрировать нового агента
    /// </summary>
    public async Task<bool> RegisterAgentAsync(string id, string name, string type, string repositoryPath)
    {
        try
        {
            // Проверяем, существует ли уже агент с таким ID
            var existingAgent = await _context.Agents
                .FirstOrDefaultAsync(a => a.Id == id);

            if (existingAgent != null && !existingAgent.IsDeleted)
            {
                // Агент уже существует, обновляем его данные
                existingAgent.Name = name;
                existingAgent.Type = type;
                existingAgent.RepositoryPath = repositoryPath;
                existingAgent.Status = Core.Data.Entities.AgentStatus.Idle;
                existingAgent.LastPing = DateTime.UtcNow;
                existingAgent.UpdatedAt = DateTime.UtcNow;
            }
            else if (existingAgent != null && existingAgent.IsDeleted)
            {
                // Восстанавливаем удаленного агента
                existingAgent.IsDeleted = false;
                existingAgent.Name = name;
                existingAgent.Type = type;
                existingAgent.RepositoryPath = repositoryPath;
                existingAgent.Status = Core.Data.Entities.AgentStatus.Idle;
                existingAgent.LastPing = DateTime.UtcNow;
                existingAgent.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Создаем нового агента
                var agent = new Agent
                {
                    Id = id,
                    Name = name,
                    Type = type,
                    RepositoryPath = repositoryPath,
                    Status = Core.Data.Entities.AgentStatus.Idle,
                    LastPing = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false,
                    MaxConcurrentTasks = 1,
                    HealthCheckInterval = TimeSpan.FromMinutes(1),
                    TotalExecutionTime = TimeSpan.Zero,
                    SessionId = Guid.NewGuid().ToString()
                };

                // Попытаемся найти и привязать к репозиторию
                var repository = await _context.Repositories
                    .FirstOrDefaultAsync(r => r.Path == repositoryPath);

                if (repository != null)
                {
                    agent.RepositoryId = repository.Id;
                }

                await _context.Agents.AddAsync(agent);
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
    /// Обновить статус агента
    /// </summary>
    public async Task<bool> UpdateAgentStatusAsync(string agentId, Web.Models.AgentStatus status, string? currentTask = null)
    {
        try
        {
            var agent = await _context.Agents
                .FirstOrDefaultAsync(a => a.Id == agentId && !a.IsDeleted);

            if (agent == null)
                return false;

            agent.Status = ConvertToEntityAgentStatus(status);
            agent.LastPing = DateTime.UtcNow;
            agent.UpdatedAt = DateTime.UtcNow;
            agent.CurrentTask = currentTask;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Получить репозитории с информацией об агентах
    /// </summary>
    public async Task<Dictionary<string, RepositoryInfo>> GetRepositoriesWithAgentsAsync()
    {
        var repositories = await _context.Repositories
            .Include(r => r.Agents.Where(a => !a.IsDeleted))
            .Where(r => r.IsActive)
            .ToListAsync();

        var result = new Dictionary<string, RepositoryInfo>();

        foreach (var repo in repositories)
        {
            var agents = repo.Agents.Select(ConvertToAgentInfo).ToList();

            var repoInfo = new RepositoryInfo(
                Name: repo.Name,
                Path: repo.Path,
                Agents: agents,
                IdleCount: agents.Count(a => a.Status == Web.Models.AgentStatus.Idle),
                WorkingCount: agents.Count(a => a.Status == Web.Models.AgentStatus.Working),
                ErrorCount: agents.Count(a => a.Status == Web.Models.AgentStatus.Error),
                OfflineCount: agents.Count(a => a.Status == Web.Models.AgentStatus.Offline),
                LastUpdate: DateTime.UtcNow
            );

            result[repo.Name] = repoInfo;
        }

        return result;
    }

    /// <summary>
    /// Конвертировать Entity Agent в Web AgentInfo
    /// </summary>
    private static AgentInfo ConvertToAgentInfo(Agent agent)
    {
        return new AgentInfo(
            Id: agent.Id,
            Name: agent.Name,
            Type: agent.Type,
            RepositoryPath: agent.RepositoryPath,
            Status: ConvertToWebAgentStatus(agent.Status),
            LastPing: agent.LastPing,
            CurrentTask: agent.CurrentTask,
            SessionId: agent.SessionId,
            Repository: agent.Repository?.Name,
            TaskStartTime: null // Можно будет добавить позже из TaskRecord
        );
    }

    /// <summary>
    /// Конвертировать Web AgentStatus в Entity AgentStatus
    /// </summary>
    private static Core.Data.Entities.AgentStatus ConvertToEntityAgentStatus(Web.Models.AgentStatus status)
    {
        return status switch
        {
            Web.Models.AgentStatus.Idle => Core.Data.Entities.AgentStatus.Idle,
            Web.Models.AgentStatus.Working => Core.Data.Entities.AgentStatus.Busy,
            Web.Models.AgentStatus.Error => Core.Data.Entities.AgentStatus.Error,
            Web.Models.AgentStatus.Offline => Core.Data.Entities.AgentStatus.Offline,
            _ => Core.Data.Entities.AgentStatus.Offline
        };
    }

    /// <summary>
    /// Конвертировать Entity AgentStatus в Web AgentStatus
    /// </summary>
    private static Web.Models.AgentStatus ConvertToWebAgentStatus(Core.Data.Entities.AgentStatus status)
    {
        return status switch
        {
            Core.Data.Entities.AgentStatus.Idle => Web.Models.AgentStatus.Idle,
            Core.Data.Entities.AgentStatus.Busy => Web.Models.AgentStatus.Working,
            Core.Data.Entities.AgentStatus.Error => Web.Models.AgentStatus.Error,
            Core.Data.Entities.AgentStatus.Offline => Web.Models.AgentStatus.Offline,
            Core.Data.Entities.AgentStatus.Unknown => Web.Models.AgentStatus.Offline,
            _ => Web.Models.AgentStatus.Offline
        };
    }
}