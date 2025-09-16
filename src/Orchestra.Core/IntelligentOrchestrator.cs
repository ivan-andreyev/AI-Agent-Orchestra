using Microsoft.Extensions.Logging;

namespace Orchestra.Core;

public class IntelligentOrchestrator
{
    private readonly SimpleOrchestrator _baseOrchestrator;
    private readonly ILogger<IntelligentOrchestrator> _logger;
    private readonly Dictionary<string, RepositoryContext> _repositories = new();

    public IntelligentOrchestrator(SimpleOrchestrator baseOrchestrator, ILogger<IntelligentOrchestrator> logger)
    {
        _baseOrchestrator = baseOrchestrator;
        _logger = logger;
    }

    public void RegisterRepository(string path, string projectType, List<string> technologies)
    {
        var context = new RepositoryContext(path, projectType, technologies, DateTime.Now);
        _repositories[path] = context;
        _logger.LogInformation("Registered repository: {Path} [{ProjectType}]", path, projectType);
    }

    public void QueueIntelligentTask(string description, string repositoryPath, TaskPriority priority = TaskPriority.Normal)
    {
        // Анализируем задачу и выбираем лучшего агента
        var bestAgent = FindBestAgentForTask(description, repositoryPath);

        if (bestAgent != null)
        {
            var command = GenerateCommandFromDescription(description, repositoryPath);
            _baseOrchestrator.QueueTask(command, repositoryPath, priority);

            _logger.LogInformation("Queued intelligent task for agent {AgentId}: {Description}", bestAgent.Id, description);
        }
        else
        {
            _logger.LogWarning("No suitable agent found for task: {Description}", description);
        }
    }

    public void AnalyzeAndOptimize()
    {
        var allAgents = _baseOrchestrator.GetAllAgents();
        var state = _baseOrchestrator.GetCurrentState();

        // Анализ загрузки агентов
        AnalyzeAgentWorkload(allAgents);

        // Анализ очереди задач
        AnalyzeTaskQueue(state.TaskQueue);

        // Оптимизация распределения
        OptimizeTaskDistribution();
    }

    private AgentInfo? FindBestAgentForTask(string description, string repositoryPath)
    {
        var availableAgents = _baseOrchestrator.GetAllAgents()
            .Where(a => a.Status == AgentStatus.Idle)
            .ToList();

        if (!availableAgents.Any())
            return null;

        // Приоритет агентам из того же репозитория
        var repoAgents = availableAgents.Where(a => a.RepositoryPath == repositoryPath).ToList();
        if (repoAgents.Any())
        {
            return SelectBestAgentBySpecialization(repoAgents, description);
        }

        // Иначе выбираем из всех доступных
        return SelectBestAgentBySpecialization(availableAgents, description);
    }

    private AgentInfo? SelectBestAgentBySpecialization(List<AgentInfo> agents, string description)
    {
        // Простая эвристика на основе типа задачи
        var taskType = ClassifyTaskType(description);

        var specializedAgents = agents.Where(a => IsAgentSuitableForTask(a, taskType)).ToList();

        return specializedAgents.Any()
            ? specializedAgents.OrderBy(a => a.LastPing).First()
            : agents.OrderBy(a => a.LastPing).First();
    }

    private TaskType ClassifyTaskType(string description)
    {
        var lowerDesc = description.ToLowerInvariant();

        if (lowerDesc.Contains("test") || lowerDesc.Contains("unit") || lowerDesc.Contains("spec"))
            return TaskType.Testing;

        if (lowerDesc.Contains("refactor") || lowerDesc.Contains("clean") || lowerDesc.Contains("optimize"))
            return TaskType.Refactoring;

        if (lowerDesc.Contains("bug") || lowerDesc.Contains("fix") || lowerDesc.Contains("error"))
            return TaskType.BugFix;

        if (lowerDesc.Contains("feature") || lowerDesc.Contains("implement") || lowerDesc.Contains("add"))
            return TaskType.FeatureDevelopment;

        if (lowerDesc.Contains("doc") || lowerDesc.Contains("comment") || lowerDesc.Contains("readme"))
            return TaskType.Documentation;

        return TaskType.General;
    }

    private bool IsAgentSuitableForTask(AgentInfo agent, TaskType taskType)
    {
        // В будущем здесь будет более сложная логика на основе истории и специализации агента
        // Пока простая проверка по типу агента
        return agent.Type switch
        {
            "claude-code" => true, // Claude Code может делать всё
            "github-copilot" => taskType == TaskType.FeatureDevelopment || taskType == TaskType.BugFix,
            "test-agent" => taskType == TaskType.Testing,
            "refactor-agent" => taskType == TaskType.Refactoring,
            _ => taskType == TaskType.General
        };
    }

    private string GenerateCommandFromDescription(string description, string repositoryPath)
    {
        // Простое преобразование описания в команду
        // В будущем здесь может быть LLM для генерации команд

        var taskType = ClassifyTaskType(description);

        return taskType switch
        {
            TaskType.Testing => $"Run tests and analyze results: {description}",
            TaskType.BugFix => $"Investigate and fix: {description}",
            TaskType.FeatureDevelopment => $"Implement feature: {description}",
            TaskType.Refactoring => $"Refactor code: {description}",
            TaskType.Documentation => $"Update documentation: {description}",
            _ => $"Execute task: {description}"
        };
    }

    private void AnalyzeAgentWorkload(List<AgentInfo> agents)
    {
        var workingAgents = agents.Count(a => a.Status == AgentStatus.Working);
        var idleAgents = agents.Count(a => a.Status == AgentStatus.Idle);
        var errorAgents = agents.Count(a => a.Status == AgentStatus.Error);

        _logger.LogInformation("Agent workload analysis: Working={Working}, Idle={Idle}, Error={Error}",
            workingAgents, idleAgents, errorAgents);

        // Если много агентов в ошибке, логируем предупреждение
        if (errorAgents > agents.Count * 0.3)
        {
            _logger.LogWarning("High number of agents in error state: {ErrorCount}/{TotalCount}",
                errorAgents, agents.Count);
        }
    }

    private void AnalyzeTaskQueue(Queue<TaskRequest> taskQueue)
    {
        if (taskQueue.Count > 10)
        {
            _logger.LogWarning("Task queue is getting large: {QueueSize} tasks pending", taskQueue.Count);
        }

        var oldTasks = taskQueue.Where(t => DateTime.Now - t.CreatedAt > TimeSpan.FromMinutes(30)).ToList();
        if (oldTasks.Any())
        {
            _logger.LogWarning("Found {OldTaskCount} tasks older than 30 minutes", oldTasks.Count);
        }
    }

    private void OptimizeTaskDistribution()
    {
        // Будущая логика оптимизации:
        // - Перебалансировка нагрузки
        // - Приоритизация критических задач
        // - Автоматическое масштабирование

        _logger.LogDebug("Task distribution optimization completed");
    }
}

public record RepositoryContext(
    string Path,
    string ProjectType,
    List<string> Technologies,
    DateTime LastAnalyzed
);

public enum TaskType
{
    General,
    FeatureDevelopment,
    BugFix,
    Testing,
    Refactoring,
    Documentation
}