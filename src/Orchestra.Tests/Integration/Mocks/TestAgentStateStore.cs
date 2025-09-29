using Orchestra.Core.Abstractions;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Services;

namespace Orchestra.Tests.Integration.Mocks;

/// <summary>
/// Тестовая реализация хранилища состояния агентов.
/// Наследуется от InMemoryAgentStateStore и добавляет дополнительные возможности для тестирования.
/// Автоматически изолируется между тестами благодаря Scoped lifecycle.
/// </summary>
public class TestAgentStateStore : InMemoryAgentStateStore
{
    private readonly MockAgentExecutor _mockExecutor;

    public TestAgentStateStore(MockAgentExecutor mockExecutor)
    {
        _mockExecutor = mockExecutor ?? throw new ArgumentNullException(nameof(mockExecutor));
    }

    public override async Task<bool> RegisterAgentAsync(AgentInfo agent)
    {
        var result = await base.RegisterAgentAsync(agent);

        if (result && agent != null)
        {
            // Автоматически регистрируем агента в MockAgentExecutor для правильной работы
            _mockExecutor.RegisterAgent(agent.Id, agent.RepositoryPath);
        }

        return result;
    }

    /// <summary>
    /// Создает тестового агента с указанным поведением
    /// </summary>
    /// <param name="agentId">ID агента</param>
    /// <param name="repositoryPath">Путь к репозиторию</param>
    /// <param name="agentType">Тип агента</param>
    /// <param name="behavior">Поведение агента для тестирования</param>
    /// <returns>True если агент успешно создан</returns>
    public async Task<bool> CreateTestAgentAsync(string agentId, string repositoryPath, string agentType = "mock-test", AgentBehavior behavior = AgentBehavior.Normal)
    {
        var agent = new AgentInfo(
            agentId,
            $"Test Agent {agentId}",
            agentType,
            repositoryPath,
            AgentStatus.Idle,
            DateTime.UtcNow
        );

        var result = await RegisterAgentAsync(agent);

        if (result)
        {
            // Настраиваем поведение агента в MockAgentExecutor
            _mockExecutor.SetAgentBehavior(agentId, behavior);
        }

        return result;
    }

    /// <summary>
    /// Симулирует отказ агента
    /// </summary>
    /// <param name="agentId">ID агента</param>
    public async Task SimulateAgentFailureAsync(string agentId)
    {
        _mockExecutor.SimulateAgentFailure(agentId);
        await UpdateAgentStatusAsync(agentId, AgentStatus.Error);
    }

    /// <summary>
    /// Симулирует таймаут агента
    /// </summary>
    /// <param name="agentId">ID агента</param>
    public async Task SimulateAgentTimeoutAsync(string agentId)
    {
        _mockExecutor.SimulateAgentTimeout(agentId);
        await UpdateAgentStatusAsync(agentId, AgentStatus.Offline);
    }

    /// <summary>
    /// Сброс всех агентов к нормальному поведению
    /// </summary>
    public async Task ResetAllAgentBehaviorsAsync()
    {
        var allAgents = await GetAllAgentsAsync();

        foreach (var agent in allAgents)
        {
            _mockExecutor.SetAgentBehavior(agent.Id, AgentBehavior.Normal);
            await UpdateAgentStatusAsync(agent.Id, AgentStatus.Idle);
        }
    }
}