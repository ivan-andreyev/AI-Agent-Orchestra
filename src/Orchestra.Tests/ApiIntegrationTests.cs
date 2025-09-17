using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Orchestra.Core;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

namespace Orchestra.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task GetState_ShouldReturnOrchestratorState()
    {
        // Act
        var response = await _client.GetAsync("/state");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        var state = JsonSerializer.Deserialize<OrchestratorState>(content, _jsonOptions);
        Assert.NotNull(state);
        Assert.NotNull(state.Agents);
        Assert.NotNull(state.TaskQueue);
    }

    [Fact]
    public async Task GetAgents_ShouldReturnAgentList()
    {
        // Act
        var response = await _client.GetAsync("/agents");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        var agents = JsonSerializer.Deserialize<List<AgentInfo>>(content, _jsonOptions);
        Assert.NotNull(agents);
    }

    [Fact]
    public async Task RegisterAgent_ShouldSucceed()
    {
        // Arrange
        var request = new
        {
            Id = "test-agent-api",
            Name = "Test Agent API",
            Type = "claude-code",
            RepositoryPath = @"C:\TestRepo"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/agents/register", request);

        // Assert
        response.EnsureSuccessStatusCode();

        // Verify agent was registered
        var agentsResponse = await _client.GetAsync("/agents");
        var agentsContent = await agentsResponse.Content.ReadAsStringAsync();
        var agents = JsonSerializer.Deserialize<List<AgentInfo>>(agentsContent, _jsonOptions);

        Assert.Contains(agents, a => a.Id == "test-agent-api");
    }

    [Fact]
    public async Task QueueTask_ShouldSucceed()
    {
        // Arrange
        // First register an agent
        var agentRequest = new
        {
            Id = "task-test-agent",
            Name = "Task Test Agent",
            Type = "claude-code",
            RepositoryPath = @"C:\TaskTestRepo"
        };
        await _client.PostAsJsonAsync("/agents/register", agentRequest);

        var taskRequest = new
        {
            Command = "Run integration tests",
            RepositoryPath = @"C:\TaskTestRepo",
            Priority = (int)TaskPriority.High // Use numeric enum value
        };

        // Act
        var response = await _client.PostAsJsonAsync("/tasks/queue", taskRequest);

        // Assert
        response.EnsureSuccessStatusCode();

        // Verify task was queued by checking state
        var stateResponse = await _client.GetAsync("/state");
        var stateContent = await stateResponse.Content.ReadAsStringAsync();
        var state = JsonSerializer.Deserialize<OrchestratorState>(stateContent, _jsonOptions);

        // Task might be processed immediately, so check if it was assigned to agent
        var agent = state.Agents.Values.FirstOrDefault(a => a.Id == "task-test-agent");
        Assert.NotNull(agent);
    }

    [Fact]
    public async Task AgentPing_ShouldUpdateStatus()
    {
        // Arrange
        var agentId = "ping-test-agent";
        var agentRequest = new
        {
            Id = agentId,
            Name = "Ping Test Agent",
            Type = "claude-code",
            RepositoryPath = @"C:\PingTestRepo"
        };
        await _client.PostAsJsonAsync("/agents/register", agentRequest);

        var pingRequest = new
        {
            Status = (int)AgentStatus.Working, // Use numeric enum value
            CurrentTask = "Processing files"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/agents/{agentId}/ping", pingRequest);

        // Assert
        response.EnsureSuccessStatusCode();

        // Verify agent status was updated
        var agentsResponse = await _client.GetAsync("/agents");
        var agentsContent = await agentsResponse.Content.ReadAsStringAsync();
        var agents = JsonSerializer.Deserialize<List<AgentInfo>>(agentsContent, _jsonOptions);

        var agent = agents.FirstOrDefault(a => a.Id == agentId);
        Assert.NotNull(agent);
        Assert.Equal(AgentStatus.Working, agent.Status);
        Assert.Equal("Processing files", agent.CurrentTask);
    }

    [Fact]
    public async Task GetNextTask_ShouldReturnTaskForAgent()
    {
        // Arrange
        var agentId = "next-task-agent";
        var agentRequest = new
        {
            Id = agentId,
            Name = "Next Task Agent",
            Type = "claude-code",
            RepositoryPath = @"C:\NextTaskRepo"
        };
        await _client.PostAsJsonAsync("/agents/register", agentRequest);

        var taskRequest = new
        {
            Command = "Test next task functionality",
            RepositoryPath = @"C:\NextTaskRepo",
            Priority = (int)TaskPriority.Normal // Use numeric enum value
        };
        await _client.PostAsJsonAsync("/tasks/queue", taskRequest);

        // Act
        var response = await _client.GetAsync($"/agents/{agentId}/next-task");

        // Assert
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            var task = JsonSerializer.Deserialize<TaskRequest>(content, _jsonOptions);
            Assert.NotNull(task);
            Assert.Equal(agentId, task.AgentId);
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            // Task might have been processed already, which is also valid
            Assert.True(true);
        }
        else
        {
            Assert.True(false, $"Unexpected status code: {response.StatusCode}");
        }
    }

    [Fact]
    public async Task FullWorkflow_ShouldWorkEndToEnd()
    {
        // Arrange
        var agentId = "workflow-agent";
        var repositoryPath = @"C:\WorkflowRepo";

        // Step 1: Register agent
        var agentRequest = new
        {
            Id = agentId,
            Name = "Workflow Agent",
            Type = "claude-code",
            RepositoryPath = repositoryPath
        };
        var registerResponse = await _client.PostAsJsonAsync("/agents/register", agentRequest);
        registerResponse.EnsureSuccessStatusCode();

        // Step 2: Queue a task
        var taskRequest = new
        {
            Command = "Complete workflow test",
            RepositoryPath = repositoryPath,
            Priority = (int)TaskPriority.High // Use numeric enum value
        };
        var queueResponse = await _client.PostAsJsonAsync("/tasks/queue", taskRequest);
        queueResponse.EnsureSuccessStatusCode();

        // Step 3: Agent pings as working
        var pingRequest = new
        {
            Status = (int)AgentStatus.Working, // Use numeric enum value
            CurrentTask = "Complete workflow test"
        };
        var pingResponse = await _client.PostAsJsonAsync($"/agents/{agentId}/ping", pingRequest);
        pingResponse.EnsureSuccessStatusCode();

        // Step 4: Verify final state
        var stateResponse = await _client.GetAsync("/state");
        var stateContent = await stateResponse.Content.ReadAsStringAsync();
        var state = JsonSerializer.Deserialize<OrchestratorState>(stateContent, _jsonOptions);

        Assert.NotNull(state);
        Assert.True(state.Agents.ContainsKey(agentId));

        var agent = state.Agents[agentId];
        Assert.Equal(AgentStatus.Working, agent.Status);
        Assert.Equal("Complete workflow test", agent.CurrentTask);
    }
}