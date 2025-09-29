using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Orchestra.Core.Services;
using Orchestra.Core.Models;
using Orchestra.Core.Data.Entities;

namespace Orchestra.Core;

public class AgentScheduler : BackgroundService
{
    private readonly SimpleOrchestrator _orchestrator;
    private readonly ILogger<AgentScheduler> _logger;
    private readonly AgentConfiguration _config;
    private readonly TimeSpan _pingInterval;
    private readonly MarkdownPlanReader _planReader;
    private readonly IClaudeCodeCoreService? _claudeCodeService;

    public AgentScheduler(
        SimpleOrchestrator orchestrator,
        ILogger<AgentScheduler> logger,
        AgentConfiguration config,
        IClaudeCodeCoreService? claudeCodeService = null)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _config = config;
        _pingInterval = TimeSpan.FromSeconds(config.PingIntervalSeconds);
        _planReader = new MarkdownPlanReader();
        _claudeCodeService = claudeCodeService;
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
                await ProcessMarkdownPlans();

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
            AgentStatus status;

            // Новая логика для Claude Code агентов
            if (agent.Type == "claude-code" && _claudeCodeService != null)
            {
                _logger.LogDebug("Pinging Claude Code agent {AgentName} ({AgentId})", agent.Name, agent.Id);
                var isAvailable = await _claudeCodeService.IsAgentAvailableAsync(agent.Id);
                status = isAvailable ? AgentStatus.Idle : AgentStatus.Offline;

                // Дополнительно получаем версию для диагностики
                if (isAvailable)
                {
                    try
                    {
                        var version = await _claudeCodeService.GetAgentVersionAsync(agent.Id);
                        _logger.LogDebug("Claude Code agent {AgentName} version: {Version}", agent.Name, version);
                    }
                    catch (Exception versionEx)
                    {
                        _logger.LogWarning(versionEx, "Could not get version for Claude Code agent {AgentName}", agent.Name);
                    }
                }
            }
            else
            {
                // Существующая логика для обычных агентов
                status = await CheckAgentStatus(agent);
            }

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

                // Обновляем статус агента на Working
                _orchestrator.UpdateAgentStatus(agent.Id, AgentStatus.Busy, task.Command);

                // Отправляем команду агенту асинхронно
                _ = Task.Run(async () => await ExecuteTaskOnAgent(agent, task));
            }
        }
    }

    /// <summary>
    /// Выполняет задачу на агенте с полной обработкой результата
    /// </summary>
    /// <param name="agent">Информация об агенте</param>
    /// <param name="task">Задача для выполнения</param>
    private async Task ExecuteTaskOnAgent(AgentInfo agent, TaskRequest task)
    {
        try
        {
            _logger.LogInformation("Starting task execution {TaskId} on agent {AgentName}", task.Id, agent.Name);

            // Находим конфигурацию агента для определения типа
            var agentConfig = _config.Agents.FirstOrDefault(a => a.Id == agent.Id);
            if (agentConfig == null)
            {
                _logger.LogError("Agent configuration not found for agent {AgentId}", agent.Id);
                _orchestrator.UpdateAgentStatus(agent.Id, AgentStatus.Error, "Configuration not found");
                return;
            }

            // Выполняем команду в зависимости от типа агента
            if (agentConfig.Type == "claude-code" && _claudeCodeService != null)
            {
                await ExecuteClaudeCodeTask(agent, task, agentConfig);
            }
            else
            {
                // Для других типов агентов используем стандартную логику
                await ExecuteStandardTask(agent, task, agentConfig);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute task {TaskId} on agent {AgentName}", task.Id, agent.Name);
            _orchestrator.UpdateAgentStatus(agent.Id, AgentStatus.Error, $"Task execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Выполняет задачу на агенте Claude Code
    /// </summary>
    private async Task ExecuteClaudeCodeTask(AgentInfo agent, TaskRequest task, ConfiguredAgent agentConfig)
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "repositoryPath", task.RepositoryPath },
                { "taskId", task.Id },
                { "priority", task.Priority.ToString() },
                { "createdAt", task.CreatedAt.ToString("O") }
            };

            _logger.LogDebug("Executing Claude Code command: {Command} on agent {AgentName}", task.Command, agent.Name);

            var result = await _claudeCodeService!.ExecuteCommandAsync(
                agent.Id,
                task.Command,
                parameters
            );

            if (result.Success)
            {
                _logger.LogInformation("Task {TaskId} completed successfully on agent {AgentName}. Output: {Output}",
                    task.Id, agent.Name, result.Output);

                _orchestrator.UpdateAgentStatus(agent.Id, AgentStatus.Idle, null);

                // Здесь можно добавить логику сохранения результата задачи
                _logger.LogDebug("Task execution time: {ExecutionTime}, Workflow ID: {WorkflowId}",
                    result.ExecutionTime, result.WorkflowId);
            }
            else
            {
                _logger.LogError("Task {TaskId} failed on agent {AgentName}. Error: {Error}",
                    task.Id, agent.Name, result.ErrorMessage);

                _orchestrator.UpdateAgentStatus(agent.Id, AgentStatus.Error, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Claude Code task execution for task {TaskId} on agent {AgentName}",
                task.Id, agent.Name);

            _orchestrator.UpdateAgentStatus(agent.Id, AgentStatus.Error, $"Execution exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Выполняет задачу на стандартном агенте (заглушка для будущего расширения)
    /// </summary>
    private async Task ExecuteStandardTask(AgentInfo agent, TaskRequest task, ConfiguredAgent agentConfig)
    {
        _logger.LogWarning("Standard agent execution not implemented yet for agent type {AgentType}. Task {TaskId} on agent {AgentName} will be marked as completed.",
            agentConfig.Type, task.Id, agent.Name);

        // Имитируем выполнение задачи
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Возвращаем агента в состояние Idle
        _orchestrator.UpdateAgentStatus(agent.Id, AgentStatus.Idle, null);
    }

    /// <summary>
    /// Обрабатывает markdown планы работ агентов
    /// </summary>
    private async Task ProcessMarkdownPlans()
    {
        try
        {
            var activePlans = _planReader.GetActivePlans();

            foreach (var plan in activePlans)
            {
                // Проверяем статус агента
                var agent = _orchestrator.GetAgentById(plan.AgentId);
                if (agent == null)
                {
                    _logger.LogWarning("Agent {AgentId} from plan {PlanName} not found", plan.AgentId, plan.PlanName);
                    continue;
                }

                // Обновляем прогресс в markdown если агент работает
                if (agent.Status == AgentStatus.Busy)
                {
                    _planReader.UpdatePlanProgress(plan.AgentId, $"Agent working on: {agent.CurrentTask}");
                    _logger.LogDebug("Updated progress for agent {AgentId}: {Progress}%", plan.AgentId, plan.ProgressPercent);
                }

                // Проверяем застрявших агентов (не обновлялись более 10 минут)
                if (plan.LastPing.HasValue &&
                    DateTime.UtcNow - plan.LastPing.Value > TimeSpan.FromMinutes(10))
                {
                    _logger.LogWarning("Agent {AgentId} appears stuck - last ping {LastPing}",
                        plan.AgentId, plan.LastPing);

                    // Здесь можно добавить логику пинга агента или уведомления
                    _planReader.UpdatePlanProgress(plan.AgentId, "⚠️ Agent appears stuck - coordinator intervention needed");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing markdown plans");
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