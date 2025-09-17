using Orchestra.Web.Models;
using System.Text;
using System.Text.Json;

namespace Orchestra.Web.Services;

public class OrchestratorService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public OrchestratorService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }

    public async Task<OrchestratorState?> GetStateAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/state");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<OrchestratorState>(json, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting state: {ex.Message}");
        }
        return null;
    }

    public async Task<List<AgentInfo>?> GetAgentsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/agents");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<AgentInfo>>(json, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting agents: {ex.Message}");
        }
        return new List<AgentInfo>();
    }

    public async Task<bool> QueueTaskAsync(string command, string repositoryPath, TaskPriority priority = TaskPriority.Normal)
    {
        try
        {
            var request = new QueueTaskRequest(command, repositoryPath, priority);
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/tasks/queue", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error queuing task: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RegisterAgentAsync(string id, string name, string type, string repositoryPath)
    {
        try
        {
            var request = new RegisterAgentRequest(id, name, type, repositoryPath);
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/agents/register", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error registering agent: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/state");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}