using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Commands.Agents;
using System.Diagnostics;
using System.Text.Json;

namespace Orchestra.Core.Services;

/// <summary>
/// Сервис для автоматического обнаружения и регистрации агентов
/// </summary>
public class AgentDiscoveryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentDiscoveryService> _logger;
    private readonly AgentDiscoveryOptions _options;
    private readonly IProcessDiscoveryService _processDiscovery;
    private readonly HashSet<string> _knownAgents = new();

    /// <summary>
    /// Инициализирует новый экземпляр AgentDiscoveryService
    /// </summary>
    /// <param name="scopeFactory">Фабрика для создания scope</param>
    /// <param name="logger">Логгер</param>
    /// <param name="options">Настройки discovery</param>
    /// <param name="processDiscovery">Сервис для обнаружения процессов Claude Code</param>
    public AgentDiscoveryService(
        IServiceScopeFactory scopeFactory,
        ILogger<AgentDiscoveryService> logger,
        IOptions<AgentDiscoveryOptions> options,
        IProcessDiscoveryService processDiscovery)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _processDiscovery = processDiscovery ?? throw new ArgumentNullException(nameof(processDiscovery));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent Discovery Service started with interval {Interval}",
            _options.ScanInterval);

        // Начальный скан с задержкой для прогрева системы
        await Task.Delay(_options.StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformAgentDiscovery(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during agent discovery");
            }

            await Task.Delay(_options.ScanInterval, stoppingToken);
        }

        _logger.LogInformation("Agent Discovery Service stopped");
    }

    private async Task PerformAgentDiscovery(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        _logger.LogDebug("Starting agent discovery scan");

        var discoveryTasks = new List<Task>
        {
            DiscoverClaudeCodeAgents(mediator, cancellationToken),
            DiscoverCopilotAgents(mediator, cancellationToken)
        };

        if (_options.EnableProcessScanning)
        {
            discoveryTasks.Add(DiscoverAgentsByProcesses(mediator, cancellationToken));
        }

        await Task.WhenAll(discoveryTasks);

        // Sync SessionIds for discovered agents
        await SyncSessionIdsWithDiscoveredProcesses(mediator, cancellationToken);

        _logger.LogDebug("Agent discovery scan completed");
    }

    #region Claude Code Discovery

    private async Task DiscoverClaudeCodeAgents(IMediator mediator, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Scanning for Claude Code agents");

            // Проверяем стандартные порты Claude Code
            var claudePorts = _options.ClaudeCodePorts;

            var discoveryTasks = claudePorts.Select(port =>
                TryDiscoverClaudeCodeOnPort(mediator, port, cancellationToken));

            await Task.WhenAll(discoveryTasks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover Claude Code agents");
        }
    }

    private async Task TryDiscoverClaudeCodeOnPort(IMediator mediator, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = _options.ConnectionTimeout;

            var baseUrl = $"http://localhost:{port}";

            // Проверяем доступность API
            var healthResponse = await httpClient.GetAsync($"{baseUrl}/health", cancellationToken);
            if (!healthResponse.IsSuccessStatusCode)
            {
                return;
            }

            // Получаем информацию об агенте
            var infoResponse = await httpClient.GetAsync($"{baseUrl}/info", cancellationToken);
            if (!infoResponse.IsSuccessStatusCode)
            {
                // Fallback: создаем агента с базовой информацией
                await RegisterBasicClaudeCodeAgent(mediator, port, cancellationToken);
                return;
            }

            var infoContent = await infoResponse.Content.ReadAsStringAsync(cancellationToken);
            var agentInfo = JsonSerializer.Deserialize<AgentInfoResponse>(infoContent);

            await RegisterClaudeCodeAgent(mediator, agentInfo, port, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Порт недоступен, это нормально
        }
        catch (TaskCanceledException)
        {
            // Таймаут, это тоже нормально
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to check Claude Code on port {Port}: {Error}", port, ex.Message);
        }
    }

    private async Task RegisterClaudeCodeAgent(IMediator mediator, AgentInfoResponse? agentInfo, int port, CancellationToken cancellationToken)
    {
        var agentId = agentInfo?.Id ?? $"claude-{port}-{Environment.MachineName}";
        var repositoryPath = agentInfo?.WorkingDirectory ?? Environment.CurrentDirectory;

        if (_knownAgents.Contains(agentId))
        {
            return; // Уже зарегистрирован
        }

        var command = new RegisterAgentCommand
        {
            Id = agentId,
            Name = agentInfo?.Name ?? $"Claude Code Agent (Port {port})",
            Type = "claude-code",
            RepositoryPath = repositoryPath,
            SessionId = agentInfo?.SessionId ?? Guid.NewGuid().ToString(),
            MaxConcurrentTasks = 1,
            ConfigurationJson = JsonSerializer.Serialize(new
            {
                Port = port,
                BaseUrl = $"http://localhost:{port}",
                ApiVersion = agentInfo?.Version ?? "unknown"
            })
        };

        var result = await mediator.Send(command, cancellationToken);

        if (result.Success)
        {
            _knownAgents.Add(agentId);
            _logger.LogInformation("Discovered and registered Claude Code agent {AgentId} on port {Port}",
                agentId, port);
        }
        else
        {
            _logger.LogWarning("Failed to register discovered Claude Code agent {AgentId}: {Error}",
                agentId, result.ErrorMessage);
        }
    }

    private async Task RegisterBasicClaudeCodeAgent(IMediator mediator, int port, CancellationToken cancellationToken)
    {
        var agentId = $"claude-{port}-{Environment.MachineName}";

        if (_knownAgents.Contains(agentId))
        {
            return;
        }

        var command = new RegisterAgentCommand
        {
            Id = agentId,
            Name = $"Claude Code Agent (Port {port})",
            Type = "claude-code",
            RepositoryPath = Environment.CurrentDirectory,
            MaxConcurrentTasks = 1,
            ConfigurationJson = JsonSerializer.Serialize(new
            {
                Port = port,
                BaseUrl = $"http://localhost:{port}"
            })
        };

        var result = await mediator.Send(command, cancellationToken);

        if (result.Success)
        {
            _knownAgents.Add(agentId);
            _logger.LogInformation("Discovered and registered basic Claude Code agent {AgentId} on port {Port}",
                agentId, port);
        }
    }

    #endregion

    #region Copilot Discovery

    private async Task DiscoverCopilotAgents(IMediator mediator, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Scanning for GitHub Copilot agents");

            // Пока что простая проверка на наличие VS Code процессов
            var vsCodeProcesses = Process.GetProcessesByName("code")
                .Concat(Process.GetProcessesByName("Code"))
                .ToList();

            if (vsCodeProcesses.Any())
            {
                await RegisterCopilotAgent(mediator, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover Copilot agents");
        }
    }

    private async Task RegisterCopilotAgent(IMediator mediator, CancellationToken cancellationToken)
    {
        var agentId = $"copilot-{Environment.MachineName}";

        if (_knownAgents.Contains(agentId))
        {
            return;
        }

        var command = new RegisterAgentCommand
        {
            Id = agentId,
            Name = "GitHub Copilot Agent",
            Type = "github-copilot",
            RepositoryPath = Environment.CurrentDirectory,
            MaxConcurrentTasks = 1,
            ConfigurationJson = JsonSerializer.Serialize(new
            {
                Type = "vscode-extension",
                DetectedAt = DateTime.UtcNow
            })
        };

        var result = await mediator.Send(command, cancellationToken);

        if (result.Success)
        {
            _knownAgents.Add(agentId);
            _logger.LogInformation("Discovered and registered Copilot agent {AgentId}", agentId);
        }
    }

    #endregion

    #region Process-based Discovery

    private async Task DiscoverAgentsByProcesses(IMediator mediator, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Scanning system processes for agents");

            var processNames = _options.ProcessNamesToScan;
            var discoveredProcesses = new List<Process>();

            foreach (var processName in processNames)
            {
                var processes = Process.GetProcessesByName(processName);
                discoveredProcesses.AddRange(processes);
            }

            foreach (var process in discoveredProcesses)
            {
                await TryRegisterProcessAsAgent(mediator, process, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover agents by processes");
        }
    }

    private async Task TryRegisterProcessAsAgent(IMediator mediator, Process process, CancellationToken cancellationToken)
    {
        try
        {
            var agentId = $"process-{process.ProcessName}-{process.Id}";

            if (_knownAgents.Contains(agentId))
            {
                return;
            }

            var agentType = DetermineAgentTypeFromProcess(process);
            if (string.IsNullOrEmpty(agentType))
            {
                return; // Неизвестный тип агента
            }

            var command = new RegisterAgentCommand
            {
                Id = agentId,
                Name = $"{agentType} Agent (PID {process.Id})",
                Type = agentType,
                RepositoryPath = Environment.CurrentDirectory,
                MaxConcurrentTasks = 1,
                ConfigurationJson = JsonSerializer.Serialize(new
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    StartTime = process.StartTime,
                    DiscoveredBy = "process-scanner"
                })
            };

            var result = await mediator.Send(command, cancellationToken);

            if (result.Success)
            {
                _knownAgents.Add(agentId);
                _logger.LogInformation("Discovered and registered process-based agent {AgentId} ({ProcessName})",
                    agentId, process.ProcessName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to register process {ProcessName} as agent: {Error}",
                process.ProcessName, ex.Message);
        }
    }

    private string DetermineAgentTypeFromProcess(Process process)
    {
        var processName = process.ProcessName.ToLowerInvariant();

        return processName switch
        {
            "claude" or "claude-desktop" => "claude-desktop",
            "code" or "code-insiders" => "vscode-copilot",
            "cursor" => "cursor-ai",
            _ => string.Empty
        };
    }

    #endregion

    #region SessionId Synchronization

    /// <summary>
    /// Синхронизирует SessionId для зарегистрированных агентов с обнаруженными процессами Claude Code
    /// </summary>
    private async Task SyncSessionIdsWithDiscoveredProcesses(IMediator mediator, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Starting SessionId synchronization");

            // Get all discovered Claude Code processes
            var discoveredProcesses = await _processDiscovery.DiscoverClaudeProcessesAsync();

            if (!discoveredProcesses.Any())
            {
                _logger.LogDebug("No Claude Code processes found for SessionId sync");
                return;
            }

            _logger.LogInformation("Found {Count} Claude Code processes for SessionId synchronization",
                discoveredProcesses.Count);

            // Get all registered agents from database
            var getAllAgentsQuery = new Orchestra.Core.Queries.Agents.GetAllAgentsQuery();
            var allAgents = await mediator.Send(getAllAgentsQuery, cancellationToken);

            int syncedCount = 0;

            // Match agents with discovered processes and update SessionId
            foreach (var agent in allAgents)
            {
                // Skip if SessionId already populated
                if (!string.IsNullOrWhiteSpace(agent.SessionId))
                {
                    _logger.LogTrace("Agent {AgentId} already has SessionId: {SessionId}",
                        agent.Id, agent.SessionId);
                    continue;
                }

                // Try to match agent with discovered process
                var matchedProcess = TryMatchAgentWithProcess(agent, discoveredProcesses);

                if (matchedProcess != null)
                {
                    // Update agent's SessionId
                    var updateCommand = new Orchestra.Core.Commands.Agents.UpdateAgentSessionIdCommand
                    {
                        AgentId = agent.Id,
                        SessionId = matchedProcess.SessionId
                    };

                    var updateResult = await mediator.Send(updateCommand, cancellationToken);

                    if (updateResult.Success)
                    {
                        syncedCount++;
                        _logger.LogInformation("Synced SessionId for agent {AgentId}: {SessionId}",
                            agent.Id, matchedProcess.SessionId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to update SessionId for agent {AgentId}: {Error}",
                            agent.Id, updateResult.ErrorMessage);
                    }
                }
                else
                {
                    _logger.LogDebug("No matching process found for agent {AgentId}", agent.Id);
                }
            }

            _logger.LogInformation("SessionId synchronization completed: {SyncedCount}/{TotalAgents} agents updated",
                syncedCount, allAgents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SessionId synchronization");
        }
    }

    /// <summary>
    /// Пытается сопоставить агента с обнаруженным процессом
    /// </summary>
    private ClaudeProcessInfo? TryMatchAgentWithProcess(
        Orchestra.Core.Data.Entities.Agent agent,
        List<ClaudeProcessInfo> discoveredProcesses)
    {
        // Try to extract ProcessId from agent's ConfigurationJson
        if (!string.IsNullOrWhiteSpace(agent.ConfigurationJson))
        {
            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(agent.ConfigurationJson);

                if (config != null && config.TryGetValue("ProcessId", out var processIdElement))
                {
                    var processId = processIdElement.GetInt32();

                    // Match by ProcessId
                    var matchedByPid = discoveredProcesses.FirstOrDefault(p => p.ProcessId == processId);
                    if (matchedByPid != null)
                    {
                        _logger.LogDebug("Matched agent {AgentId} with process PID {ProcessId}",
                            agent.Id, processId);
                        return matchedByPid;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to parse ConfigurationJson for agent {AgentId}", agent.Id);
            }
        }

        // Fallback: try to match by repository path
        if (!string.IsNullOrWhiteSpace(agent.RepositoryPath))
        {
            var matchedByPath = discoveredProcesses.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.WorkingDirectory) &&
                (p.WorkingDirectory.Equals(agent.RepositoryPath, StringComparison.OrdinalIgnoreCase) ||
                 agent.RepositoryPath.StartsWith(p.WorkingDirectory, StringComparison.OrdinalIgnoreCase)));

            if (matchedByPath != null)
            {
                _logger.LogDebug("Matched agent {AgentId} with process by repository path: {Path}",
                    agent.Id, agent.RepositoryPath);
                return matchedByPath;
            }
        }

        return null;
    }

    #endregion

    private class AgentInfoResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
    }
}