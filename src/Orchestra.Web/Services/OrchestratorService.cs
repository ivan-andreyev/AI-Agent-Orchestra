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
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<OrchestratorState?> GetStateAsync()
    {
        try
        {
            Console.WriteLine($"Attempting to connect to: {_httpClient.BaseAddress}/state");
            var response = await _httpClient.GetAsync("/state");
            Console.WriteLine($"Response status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Received JSON: {json}");
                return JsonSerializer.Deserialize<OrchestratorState>(json, _jsonOptions);
            }
            else
            {
                Console.WriteLine($"Error response: {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting state: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
            var request = new { Command = command, RepositoryPath = repositoryPath, Priority = (int)priority };
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
            var request = new { Id = id, Name = name, Type = type, RepositoryPath = repositoryPath };
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

    public async Task<Dictionary<string, RepositoryInfo>?> GetRepositoriesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/repositories");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Dictionary<string, RepositoryInfo>>(json, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting repositories: {ex.Message}");
        }
        return new Dictionary<string, RepositoryInfo>();
    }

    public async Task<bool> RefreshAgentsAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("/refresh", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing agents: {ex.Message}");
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

    public async Task<List<AgentHistoryEntry>?> GetAgentHistoryAsync(string sessionId, int maxEntries = 50)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/agents/{sessionId}/history?maxEntries={maxEntries}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<AgentHistoryEntry>>(json, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting agent history: {ex.Message}");
        }
        return new List<AgentHistoryEntry>();
    }
}