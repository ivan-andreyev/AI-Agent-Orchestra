using Orchestra.Core.Models;

namespace Orchestra.Core.Interfaces;

/// <summary>
/// Интерфейс для выполнения рабочих процессов
/// </summary>
public interface IWorkflowExecutor
{
    /// <summary>
    /// Выполнить рабочий процесс по определению
    /// </summary>
    /// <param name="workflowDefinition">Определение рабочего процесса</param>
    /// <param name="parameters">Параметры выполнения</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат выполнения рабочего процесса</returns>
    Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        WorkflowDefinition workflowDefinition,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);
}