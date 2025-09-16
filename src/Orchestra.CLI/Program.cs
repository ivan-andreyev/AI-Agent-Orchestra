using Orchestra.Core;
using System.Text.Json;

namespace Orchestra.CLI;

static class JsonHelper
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}

class Program
{
    private static readonly HttpClient _httpClient = new();
    private static string _orchestratorUrl = "http://localhost:5000";

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== AI Agent Orchestra CLI ===");
        Console.WriteLine();

        if (args.Length == 0)
        {
            ShowHelp();
            return;
        }

        try
        {
            await ProcessCommand(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static async Task ProcessCommand(string[] args)
    {
        var command = args[0].ToLower();

        switch (command)
        {
            case "status":
                await ShowStatus();
                break;

            case "agents":
                await ListAgents();
                break;

            case "queue":
                if (args.Length > 1 && args[1] == "add")
                {
                    await QueueTask(args.Skip(2).ToArray());
                }
                else
                {
                    await ShowQueue();
                }
                break;

            case "start":
                await StartOrchestrator();
                break;

            case "ping":
                await PingOrchestrator();
                break;

            case "config":
                await ShowConfig();
                break;

            default:
                ShowHelp();
                break;
        }
    }

    static async Task ShowStatus()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_orchestratorUrl}/state");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var state = JsonSerializer.Deserialize<OrchestratorState>(json, JsonHelper.Options);

                Console.WriteLine("=== Orchestrator Status ===");
                Console.WriteLine($"Last Update: {state?.LastUpdate}");
                Console.WriteLine($"Active Agents: {state?.Agents.Count}");
                Console.WriteLine($"Tasks in Queue: {state?.TaskQueue.Count}");
                Console.WriteLine();

                if (state?.Agents.Any() == true)
                {
                    Console.WriteLine("Agent Status:");
                    foreach (var agent in state.Agents.Values)
                    {
                        var statusColor = agent.Status switch
                        {
                            AgentStatus.Working => ConsoleColor.Yellow,
                            AgentStatus.Idle => ConsoleColor.Green,
                            AgentStatus.Error => ConsoleColor.Red,
                            _ => ConsoleColor.Gray
                        };

                        Console.ForegroundColor = statusColor;
                        Console.Write($"  {agent.Name} [{agent.Status}]");
                        Console.ResetColor();

                        if (!string.IsNullOrEmpty(agent.CurrentTask))
                        {
                            Console.Write($" - {agent.CurrentTask}");
                        }
                        Console.WriteLine();
                    }
                }
            }
            else
            {
                Console.WriteLine("Failed to connect to orchestrator");
            }
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("Orchestrator is not running or not accessible");
        }
    }

    static async Task ListAgents()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_orchestratorUrl}/agents");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var agents = JsonSerializer.Deserialize<List<AgentInfo>>(json, JsonHelper.Options);

                Console.WriteLine("=== Registered Agents ===");
                foreach (var agent in agents ?? new List<AgentInfo>())
                {
                    Console.WriteLine($"ID: {agent.Id}");
                    Console.WriteLine($"Name: {agent.Name}");
                    Console.WriteLine($"Type: {agent.Type}");
                    Console.WriteLine($"Repository: {agent.RepositoryPath}");
                    Console.WriteLine($"Status: {agent.Status}");
                    Console.WriteLine($"Last Ping: {agent.LastPing}");
                    if (!string.IsNullOrEmpty(agent.CurrentTask))
                    {
                        Console.WriteLine($"Current Task: {agent.CurrentTask}");
                    }
                    Console.WriteLine();
                }
            }
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("Failed to connect to orchestrator");
        }
    }

    static async Task ShowQueue()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_orchestratorUrl}/state");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var state = JsonSerializer.Deserialize<OrchestratorState>(json, JsonHelper.Options);

                Console.WriteLine("=== Task Queue ===");
                if (state?.TaskQueue.Any() == true)
                {
                    foreach (var task in state.TaskQueue)
                    {
                        Console.WriteLine($"ID: {task.Id}");
                        Console.WriteLine($"Agent: {task.AgentId}");
                        Console.WriteLine($"Command: {task.Command}");
                        Console.WriteLine($"Repository: {task.RepositoryPath}");
                        Console.WriteLine($"Priority: {task.Priority}");
                        Console.WriteLine($"Created: {task.CreatedAt}");
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine("Queue is empty");
                }
            }
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("Failed to connect to orchestrator");
        }
    }

    static async Task QueueTask(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: queue add <command> <repository-path> [priority]");
            return;
        }

        var command = args[0];
        var repositoryPath = args[1];
        var priority = args.Length > 2 && Enum.TryParse<TaskPriority>(args[2], true, out var p) ? p : TaskPriority.Normal;

        var request = new
        {
            Command = command,
            RepositoryPath = repositoryPath,
            Priority = priority
        };

        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_orchestratorUrl}/tasks/queue", content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Task queued successfully");
            }
            else
            {
                Console.WriteLine($"Failed to queue task: {response.StatusCode}");
            }
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("Failed to connect to orchestrator");
        }
    }

    static async Task StartOrchestrator()
    {
        Console.WriteLine("Starting orchestrator...");
        Console.WriteLine("Note: This would typically start the orchestrator service");
        Console.WriteLine("For now, run: dotnet run --project src/Orchestra.API");
    }

    static async Task PingOrchestrator()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_orchestratorUrl}/state");
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✓ Orchestrator is responding");
            }
            else
            {
                Console.WriteLine($"✗ Orchestrator responded with status: {response.StatusCode}");
            }
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("✗ Orchestrator is not accessible");
        }
    }

    static async Task ShowConfig()
    {
        var configPath = "agent-config.json";
        if (File.Exists(configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(configPath);
                var config = JsonSerializer.Deserialize<AgentConfiguration>(json, JsonHelper.Options);

                Console.WriteLine("=== Agent Configuration ===");
                Console.WriteLine($"Ping Interval: {config?.PingIntervalSeconds} seconds");
                Console.WriteLine($"Agents: {config?.Agents.Count}");
                Console.WriteLine();

                foreach (var agent in config?.Agents ?? new List<ConfiguredAgent>())
                {
                    Console.WriteLine($"ID: {agent.Id}");
                    Console.WriteLine($"Name: {agent.Name}");
                    Console.WriteLine($"Type: {agent.Type}");
                    Console.WriteLine($"Repository: {agent.RepositoryPath}");
                    Console.WriteLine($"Enabled: {agent.Enabled}");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading config: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Configuration file not found");
        }
    }

    static void ShowHelp()
    {
        Console.WriteLine("Usage: orchestra <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  status                    - Show orchestrator status");
        Console.WriteLine("  agents                    - List all registered agents");
        Console.WriteLine("  queue                     - Show task queue");
        Console.WriteLine("  queue add <cmd> <repo>    - Add task to queue");
        Console.WriteLine("  start                     - Start orchestrator");
        Console.WriteLine("  ping                      - Ping orchestrator");
        Console.WriteLine("  config                    - Show configuration");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  orchestra status");
        Console.WriteLine("  orchestra queue add \"Run tests\" C:\\MyProject");
        Console.WriteLine("  orchestra agents");
    }
}