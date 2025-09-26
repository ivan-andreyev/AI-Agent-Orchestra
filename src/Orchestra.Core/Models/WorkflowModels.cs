namespace Orchestra.Core.Models;

/// <summary>
/// Определение рабочего процесса
/// </summary>
public record WorkflowDefinition
{
    /// <summary>
    /// Уникальный идентификатор рабочего процесса
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Название рабочего процесса
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Описание рабочего процесса
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Шаги рабочего процесса
    /// </summary>
    public required List<WorkflowStep> Steps { get; init; } = new();

    /// <summary>
    /// Метаданные рабочего процесса
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Шаг рабочего процесса
/// </summary>
public record WorkflowStep
{
    /// <summary>
    /// Уникальный идентификатор шага
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Название шага
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Команда для выполнения
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Идентификатор агента для выполнения шага
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Является ли шаг обязательным для успешного завершения процесса
    /// </summary>
    public bool IsRequired { get; init; } = true;

    /// <summary>
    /// Порядок выполнения шага
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Условия для выполнения шага
    /// </summary>
    public Dictionary<string, object> Conditions { get; init; } = new();
}

/// <summary>
/// Результат выполнения рабочего процесса
/// </summary>
public record WorkflowExecutionResult
{
    /// <summary>
    /// Успешность выполнения
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Идентификатор рабочего процесса
    /// </summary>
    public required string WorkflowId { get; init; }

    /// <summary>
    /// Идентификатор выполнения
    /// </summary>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Время начала выполнения
    /// </summary>
    public required DateTime StartTime { get; init; }

    /// <summary>
    /// Время завершения выполнения
    /// </summary>
    public required DateTime EndTime { get; init; }

    /// <summary>
    /// Длительность выполнения
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Результаты выполнения шагов
    /// </summary>
    public required List<WorkflowStepResult> StepResults { get; init; } = new();

    /// <summary>
    /// Выходные данные процесса
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Сообщение об ошибке, если выполнение неуспешно
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;
}

/// <summary>
/// Результат выполнения шага рабочего процесса
/// </summary>
public record WorkflowStepResult
{
    /// <summary>
    /// Идентификатор шага
    /// </summary>
    public required string StepId { get; init; }

    /// <summary>
    /// Успешность выполнения шага
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Выходные данные шага
    /// </summary>
    public required string Output { get; init; }

    /// <summary>
    /// Сообщение об ошибке, если выполнение неуспешно
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Время начала выполнения шага
    /// </summary>
    public required DateTime StartTime { get; init; }

    /// <summary>
    /// Время завершения выполнения шага
    /// </summary>
    public required DateTime EndTime { get; init; }

    /// <summary>
    /// Длительность выполнения шага
    /// </summary>
    public TimeSpan Duration { get; init; }
}