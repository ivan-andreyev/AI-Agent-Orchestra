using Microsoft.AspNetCore.Mvc;
using Orchestra.Core;
using Orchestra.Core.Models;

namespace Orchestra.API.Controllers;

[ApiController]
[Route("")]
public class OrchestratorController : ControllerBase
{
    private readonly SimpleOrchestrator _orchestrator;

    public OrchestratorController(SimpleOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpGet("state")]
    public ActionResult<OrchestratorState> GetState()
    {
        return Ok(_orchestrator.GetCurrentState());
    }

    [HttpGet("agents")]
    public ActionResult<List<AgentInfo>> GetAgents()
    {
        return Ok(_orchestrator.GetAllAgents());
    }

    [HttpPost("agents/register")]
    public ActionResult RegisterAgent([FromBody] RegisterAgentRequest request)
    {
        _orchestrator.RegisterAgent(request.Id, request.Name, request.Type, request.RepositoryPath);
        return Ok("Agent registered");
    }

    [HttpPost("agents/{agentId}/ping")]
    public ActionResult PingAgent(string agentId, [FromBody] PingRequest request)
    {
        _orchestrator.UpdateAgentStatus(agentId, request.Status, request.CurrentTask);
        return Ok("Agent status updated");
    }

    [HttpPost("tasks/queue")]
    public ActionResult QueueTask([FromBody] QueueTaskRequest request)
    {
        _orchestrator.QueueTask(request.Command, request.RepositoryPath, request.Priority);
        return Ok("Task queued");
    }

    [HttpGet("agents/{agentId}/next-task")]
    public ActionResult<TaskRequest> GetNextTask(string agentId)
    {
        var task = _orchestrator.GetNextTaskForAgent(agentId);
        if (task == null)
        {
            return NoContent();
        }

        return Ok(task);
    }

    [HttpGet("repositories")]
    public ActionResult<Dictionary<string, RepositoryInfo>> GetRepositories()
    {
        return Ok(_orchestrator.GetRepositories());
    }

    [HttpPost("refresh")]
    public ActionResult RefreshAgents()
    {
        _orchestrator.RefreshAgents();
        return Ok("Agents refreshed");
    }

    [HttpPost("tasks/assign")]
    public ActionResult TriggerTaskAssignment()
    {
        _orchestrator.TriggerTaskAssignment();
        return Ok("Task assignment triggered");
    }

    [HttpGet("agents/{sessionId}/history")]
    public ActionResult<List<AgentHistoryEntry>> GetAgentHistory(string sessionId, [FromQuery] int maxEntries = 50)
    {
        var history = _orchestrator.GetAgentHistory(sessionId, maxEntries);
        return Ok(history);
    }
}

public record RegisterAgentRequest(string Id, string Name, string Type, string RepositoryPath);
public record PingRequest(AgentStatus Status, string? CurrentTask);
public record QueueTaskRequest(string Command, string RepositoryPath, TaskPriority Priority);