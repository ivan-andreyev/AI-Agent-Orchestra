using Microsoft.AspNetCore.Mvc;
using Orchestra.Core;
using Orchestra.Core.Models;
using Orchestra.API.Services;

namespace Orchestra.API.Controllers;

[ApiController]
[Route("")]
public class OrchestratorController : ControllerBase
{
    private readonly HangfireOrchestrator _hangfireOrchestrator;

    public OrchestratorController(HangfireOrchestrator hangfireOrchestrator)
    {
        _hangfireOrchestrator = hangfireOrchestrator;
    }

    [HttpGet("state")]
    public ActionResult<OrchestratorState> GetState()
    {
        return Ok(_hangfireOrchestrator.GetCurrentState());
    }

    [HttpGet("agents")]
    public ActionResult<List<AgentInfo>> GetAgents()
    {
        return Ok(_hangfireOrchestrator.GetAllAgents());
    }

    [HttpPost("agents/register")]
    public async Task<ActionResult> RegisterAgent([FromBody] RegisterAgentRequest request)
    {
        var success = await _hangfireOrchestrator.RegisterAgentAsync(request.Id, request.Name, request.Type, request.RepositoryPath);
        if (success)
        {
            return Ok(new { Message = "Agent registered successfully", AgentId = request.Id });
        }
        else
        {
            return BadRequest(new { Message = "Failed to register agent", AgentId = request.Id });
        }
    }

    [HttpPost("agents/discover")]
    public async Task<ActionResult> DiscoverAgents([FromBody] DiscoverAgentsRequest? request = null)
    {
        var repositoryPath = request?.RepositoryPath ?? Environment.CurrentDirectory;

        try
        {
            // Get current agents from Entity Framework store
            var currentAgents = await _hangfireOrchestrator.GetAllAgentsAsync();

            // Check if we have any agents for this repository
            var repositoryAgents = currentAgents.Where(a =>
                string.IsNullOrEmpty(repositoryPath) ||
                (a.RepositoryPath?.Contains(repositoryPath, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

            if (!repositoryAgents.Any())
            {
                // No agents found, attempt to create one automatically
                var agentId = $"claude-discovery-{Guid.NewGuid().ToString()[..8]}";
                var agentName = "Claude Code Agent (Auto-discovered)";

                var success = await _hangfireOrchestrator.RegisterAgentAsync(agentId, agentName, "claude-code", repositoryPath);

                if (success)
                {
                    return Ok(new
                    {
                        Message = "New agent discovered and registered",
                        AgentsFound = 1,
                        AgentId = agentId,
                        RepositoryPath = repositoryPath
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Message = "No agents found and failed to create new agent",
                        AgentsFound = 0,
                        RepositoryPath = repositoryPath
                    });
                }
            }

            return Ok(new
            {
                Message = "Existing agents discovered",
                AgentsFound = repositoryAgents.Count,
                Agents = repositoryAgents.Select(a => new { a.Id, a.Name, a.Status, a.RepositoryPath }).ToList(),
                RepositoryPath = repositoryPath
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Message = "Agent discovery failed",
                Error = ex.Message,
                RepositoryPath = repositoryPath
            });
        }
    }

    [HttpPost("agents/{agentId}/ping")]
    public ActionResult PingAgent(string agentId, [FromBody] PingRequest request)
    {
        _hangfireOrchestrator.UpdateAgentStatus(agentId, request.Status, request.CurrentTask);
        return Ok("Agent status updated");
    }

    [HttpPost("tasks/queue")]
    public async Task<ActionResult> QueueTask([FromBody] QueueTaskRequest request)
    {
        var taskId = await _hangfireOrchestrator.QueueTaskAsync(request.Command, request.RepositoryPath, request.Priority);
        return Ok(new { Message = "Task queued via Hangfire", TaskId = taskId });
    }

    [HttpGet("agents/{agentId}/next-task")]
    public ActionResult<TaskRequest> GetNextTask(string agentId)
    {
        var task = _hangfireOrchestrator.GetNextTaskForAgent(agentId);
        if (task == null)
        {
            return NoContent();
        }

        return Ok(task);
    }

    [HttpGet("repositories")]
    public ActionResult<Dictionary<string, RepositoryInfo>> GetRepositories()
    {
        return Ok(_hangfireOrchestrator.GetRepositories());
    }

    [HttpPost("refresh")]
    public ActionResult RefreshAgents()
    {
        _hangfireOrchestrator.RefreshAgents();
        return Ok("Agents refreshed");
    }

    [HttpPost("tasks/assign")]
    public ActionResult TriggerTaskAssignment()
    {
        _hangfireOrchestrator.TriggerTaskAssignment();
        return Ok("Task assignment triggered");
    }

    [HttpGet("agents/{sessionId}/history")]
    public ActionResult<List<AgentHistoryEntry>> GetAgentHistory(string sessionId, [FromQuery] int maxEntries = 50)
    {
        var history = _hangfireOrchestrator.GetAgentHistory(sessionId, maxEntries);
        return Ok(history);
    }
}

public record RegisterAgentRequest(string Id, string Name, string Type, string RepositoryPath);
public record DiscoverAgentsRequest(string? RepositoryPath = null);
public record PingRequest(AgentStatus Status, string? CurrentTask);
public record QueueTaskRequest(string Command, string RepositoryPath, TaskPriority Priority);