using Orchestra.Core.Models;
using MediatR;

namespace Orchestra.Core.Queries.Tasks;

/// <summary>
/// Запрос получения следующей задачи для агента
/// </summary>
public class GetNextTaskForAgentQuery : IRequest<TaskRequest>
{
    public string AgentId { get; init; }

    public GetNextTaskForAgentQuery(string agentId)
    {
        AgentId = agentId;
    }
}