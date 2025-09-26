using Microsoft.AspNetCore.Mvc;
using MediatR;
using Orchestra.Core.Commands.Tasks;
using Orchestra.Core.Queries.Tasks;
using Orchestra.Core.Models;
using Orchestra.API.Models;

namespace Orchestra.API.Controllers;

/// <summary>
/// Контроллер для управления задачами через MediatR
/// </summary>
[ApiController]
[Route("api/tasks")]
public class TaskController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TaskController> _logger;

    public TaskController(IMediator mediator, ILogger<TaskController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Создать новую задачу
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<string>> CreateTask([FromBody] CreateTaskRequest request)
    {
        try
        {
            var command = new CreateTaskCommand(request.Command, request.RepositoryPath, request.Priority);
            var taskId = await _mediator.Send(command);

            _logger.LogInformation("Task created via MediatR: {TaskId}", taskId);

            return Ok(new { TaskId = taskId, Message = "Task created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create task");
            return StatusCode(500, new { Message = "Failed to create task", Error = ex.Message });
        }
    }

    /// <summary>
    /// Обновить статус задачи
    /// </summary>
    [HttpPut("{taskId}/status")]
    public async Task<ActionResult<bool>> UpdateTaskStatus(string taskId, [FromBody] UpdateTaskStatusRequest request)
    {
        try
        {
            var command = new UpdateTaskStatusCommand(taskId, request.Status, request.Result, request.ErrorMessage);
            var success = await _mediator.Send(command);

            if (success)
            {
                return Ok(new { Success = true, Message = "Task status updated successfully" });
            }
            else
            {
                return NotFound(new { Success = false, Message = "Task not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task status");
            return StatusCode(500, new { Message = "Failed to update task status", Error = ex.Message });
        }
    }

    // TODO: Temporarily disabled until MediatR IRequest issue is resolved
    // /// <summary>
    // /// Получить следующую задачу для агента
    // /// </summary>
    // [HttpGet("next-for-agent/{agentId}")]
    // public async Task<ActionResult<TaskRequest>> GetNextTaskForAgent(string agentId)
    // {
    //     try
    //     {
    //         var query = new GetNextTaskForAgentQuery(agentId);
    //         var task = await _mediator.Send(query);

    //         if (task.IsEmpty)
    //         {
    //             return NoContent();
    //         }

    //         return Ok(task);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Failed to get next task for agent");
    //         return StatusCode(500, new { Message = "Failed to get next task", Error = ex.Message });
    //     }
    // }
}

