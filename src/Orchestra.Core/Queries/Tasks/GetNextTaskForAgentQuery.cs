using MediatR;
using Orchestra.Core.Models;

namespace Orchestra.Core.Queries.Tasks;

/// <summary>
/// Запрос получения следующей задачи для агента
/// </summary>
public record GetNextTaskForAgentQuery(string AgentId) : IRequest<TaskRequest>;