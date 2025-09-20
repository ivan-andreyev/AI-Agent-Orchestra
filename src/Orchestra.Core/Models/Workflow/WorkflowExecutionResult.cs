namespace Orchestra.Core.Models.Workflow;

/// <summary>
/// Статус выполнения workflow
/// </summary>
public enum WorkflowStatus
{
    /// <summary>
    /// Ожидает выполнения
    /// </summary>
    Pending,

    /// <summary>
    /// В процессе выполнения
    /// </summary>
    Running,

    /// <summary>
    /// Приостановлен
    /// </summary>
    Paused,

    /// <summary>
    /// Успешно завершен
    /// </summary>
    Completed,

    /// <summary>
    /// Завершен с ошибкой
    /// </summary>
    Failed
}

/// <summary>
/// Результат выполнения отдельного шага workflow
/// </summary>
/// <param name="StepId">Идентификатор шага</param>
/// <param name="Status">Статус выполнения шага</param>
/// <param name="Output">Выходные данные шага</param>
/// <param name="Error">Ошибка выполнения, если есть</param>
/// <param name="Duration">Длительность выполнения</param>
public record WorkflowStepResult(
    string StepId,
    WorkflowStatus Status,
    Dictionary<string, object>? Output = null,
    Exception? Error = null,
    TimeSpan? Duration = null
);

/// <summary>
/// Контекст выполнения workflow с переменными и состоянием
/// </summary>
/// <param name="Variables">Переменные контекста</param>
/// <param name="ExecutionId">Идентификатор выполнения</param>
/// <param name="CancellationToken">Токен отмены выполнения</param>
public record WorkflowContext(
    Dictionary<string, object> Variables,
    string ExecutionId,
    CancellationToken CancellationToken = default
);

/// <summary>
/// Результат выполнения workflow
/// </summary>
/// <param name="ExecutionId">Уникальный идентификатор выполнения</param>
/// <param name="Status">Статус выполнения workflow</param>
/// <param name="OutputVariables">Выходные переменные workflow</param>
/// <param name="StepResults">Результаты выполнения отдельных шагов</param>
/// <param name="Error">Ошибка выполнения, если workflow завершен неуспешно</param>
public record WorkflowExecutionResult(
    string ExecutionId,
    WorkflowStatus Status,
    Dictionary<string, object> OutputVariables,
    List<WorkflowStepResult> StepResults,
    Exception? Error = null
);