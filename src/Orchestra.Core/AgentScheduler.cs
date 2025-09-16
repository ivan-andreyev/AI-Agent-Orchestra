using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Orchestra.Core;

public class AgentScheduler : BackgroundService
{
    private readonly SimpleOrchestrator _orchestrator;
    private readonly ILogger<AgentScheduler> _logger;
    private readonly AgentConfiguration _config;
    private readonly TimeSpan _pingInterval;

    public AgentScheduler(
        SimpleOrchestrator orchestrator,
        ILogger<AgentScheduler> logger,
        AgentConfiguration config)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _config = config;
        _pingInterval = TimeSpan.FromSeconds(config.PingIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent Scheduler started");

        // Регистрируем всех агентов при запуске
        RegisterConfiguredAgents();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PingAllAgents();
                await ProcessTaskQueue();

                _logger.LogDebug("Scheduler cycle completed");
                await Task.Delay(_pingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduler cycle");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Agent Scheduler stopped");
    }

    private void RegisterConfiguredAgents()
    {
        foreach (var agent in _config.Agents.Where(a => a.Enabled))
        {
            try
            {
                _orchestrator.RegisterAgent(agent.Id, agent.Name, agent.Type, agent.RepositoryPath);
                _logger.LogInformation("Registered agent: {AgentName}", agent.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register agent: {AgentName}", agent.Name);
            }
        }
    }

    private async Task PingAllAgents()
    {
        var pingTasks = _config.Agents
            .Where(a => a.Enabled)
            .Select(PingAgent)
            .ToArray();

        await Task.WhenAll(pingTasks);
    }

    private async Task PingAgent(ConfiguredAgent agent)
    {
        try
        {
            // Проверяем доступность репозитория
            var status = await CheckAgentStatus(agent);

            _orchestrator.UpdateAgentStatus(agent.Id, status);

            _logger.LogDebug("Pinged agent {AgentName} with status {Status}", agent.Name, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ping agent: {AgentName}", agent.Name);
            _orchestrator.UpdateAgentStatus(agent.Id, AgentStatus.Error);
        }
    }

    private async Task<AgentStatus> CheckAgentStatus(ConfiguredAgent agent)
    {
        // Простая проверка доступности директории
        if (!Directory.Exists(agent.RepositoryPath))
        {
            return AgentStatus.Error;
        }

        // TODO: Здесь можно добавить более сложную логику проверки
        // например, проверка запущенных процессов, доступности порта и т.д.

        return AgentStatus.Idle;
    }

    private async Task ProcessTaskQueue()
    {
        // Простая логика обработки очереди задач
        var allAgents = _orchestrator.GetAllAgents();
        var idleAgents = allAgents.Where(a => a.Status == AgentStatus.Idle).ToList();

        foreach (var agent in idleAgents)
        {
            var task = _orchestrator.GetNextTaskForAgent(agent.Id);
            if (task != null)
            {
                _logger.LogInformation("Assigning task {TaskId} to agent {AgentName}", task.Id, agent.Name);

                // TODO: Здесь должна быть логика отправки команды агенту
                // Пока просто обновляем статус
                _orchestrator.UpdateAgentStatus(agent.Id, AgentStatus.Working, task.Command);
            }
        }
    }
}

public class AgentConfiguration
{
    public int PingIntervalSeconds { get; set; } = 30;
    public List<ConfiguredAgent> Agents { get; set; } = new();

    public static AgentConfiguration LoadFromFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<AgentConfiguration>(json) ?? new AgentConfiguration();
            }
            catch
            {
                // Если не удалось загрузить, создаем новую конфигурацию
            }
        }

        // Создаем пример конфигурации
        var config = new AgentConfiguration
        {
            PingIntervalSeconds = 30,
            Agents = new List<ConfiguredAgent>
            {
                new("claude-1", "Claude Agent 1", "claude-code", @"C:\Users\mrred\RiderProjects\Galactic-Idlers", true),
                new("claude-2", "Claude Agent 2", "claude-code", @"C:\Users\mrred\RiderProjects\AI-Agent-Orchestra", true)
            }
        };

        // Сохраняем пример конфигурации
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Игнорируем ошибки сохранения
        }

        return config;
    }
}

public record ConfiguredAgent(
    string Id,
    string Name,
    string Type,
    string RepositoryPath,
    bool Enabled
);