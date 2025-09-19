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
    public ActionResult RegisterAgent([FromBody] RegisterAgentRequest request)
    {
        _hangfireOrchestrator.RegisterAgent(request.Id, request.Name, request.Type, request.RepositoryPath);
        return Ok("Agent registered");
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
public record PingRequest(AgentStatus Status, string? CurrentTask);
public record QueueTaskRequest(string Command, string RepositoryPath, TaskPriority Priority);