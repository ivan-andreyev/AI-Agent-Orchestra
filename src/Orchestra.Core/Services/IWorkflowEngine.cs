using Orchestra.Core.Models.Workflow;

namespace Orchestra.Core.Services;

/// <summary>
/// Интерфейс для выполнения workflow-процессов в системе оркестрации
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Выполняет workflow асинхронно с заданным контекстом
    /// </summary>
    /// <param name="workflow">Определение workflow для выполнения</param>
    /// <param name="context">Контекст выполнения с переменными и состоянием</param>
    /// <returns>Результат выполнения workflow</returns>
    Task<WorkflowExecutionResult> ExecuteAsync(WorkflowDefinition workflow, WorkflowContext context);

    /// <summary>
    /// Валидирует структуру и корректность workflow перед выполнением
    /// </summary>
    /// <param name="workflow">Workflow для валидации</param>
    /// <returns>True если workflow корректен, false в противном случае</returns>
    Task<bool> ValidateWorkflowAsync(WorkflowDefinition workflow);

    /// <summary>
    /// Приостанавливает выполнение запущенного workflow
    /// </summary>
    /// <param name="executionId">Идентификатор выполнения для приостановки</param>
    Task PauseExecutionAsync(string executionId);

    /// <summary>
    /// Возобновляет выполнение приостановленного workflow
    /// </summary>
    /// <param name="executionId">Идентификатор выполнения для возобновления</param>
    Task ResumeExecutionAsync(string executionId);

    /// <summary>
    /// Получает текущий статус выполнения workflow
    /// </summary>
    /// <param name="executionId">Идентификатор выполнения</param>
    /// <returns>Результат выполнения или null если не найден</returns>
    WorkflowExecutionResult? GetExecutionStatus(string executionId);

    /// <summary>
    /// Получает список всех активных выполнений (не в состоянии Completed или Failed)
    /// </summary>
    /// <returns>Список активных выполнений</returns>
    IEnumerable<WorkflowExecutionResult> GetActiveExecutions();

    /// <summary>
    /// Проверяет, можно ли выполнить переход состояния для указанного выполнения
    /// </summary>
    /// <param name="executionId">Идентификатор выполнения</param>
    /// <param name="targetStatus">Целевое состояние</param>
    /// <returns>True если переход возможен</returns>
    bool CanTransitionTo(string executionId, WorkflowStatus targetStatus);
}