using System.Text.Json.Serialization;

namespace Orchestra.Core.Models.Workflow;

/// <summary>
/// Тип цикла в workflow
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LoopType
{
    /// <summary>
    /// Цикл ForEach - итерация по коллекции
    /// </summary>
    [JsonPropertyName("ForEach")]
    ForEach,

    /// <summary>
    /// Цикл While - выполнение пока условие истинно
    /// </summary>
    [JsonPropertyName("While")]
    While,

    /// <summary>
    /// Цикл Retry - повторные попытки выполнения
    /// </summary>
    [JsonPropertyName("Retry")]
    Retry
}

/// <summary>
/// Определение цикла для выполнения в workflow
/// </summary>
/// <param name="Type">Тип цикла</param>
/// <param name="Collection">Коллекция для итерации (для ForEach)</param>
/// <param name="Condition">Условие продолжения цикла (для While)</param>
/// <param name="MaxIterations">Максимальное количество итераций для защиты от бесконечных циклов</param>
/// <param name="IteratorVariable">Имя переменной итератора</param>
/// <param name="IndexVariable">Имя переменной индекса (опционально)</param>
/// <param name="BreakCondition">Условие для принудительного выхода из цикла</param>
/// <param name="ContinueCondition">Условие для пропуска текущей итерации</param>
public record LoopDefinition(
    [property: JsonPropertyName("type")] LoopType Type,
    [property: JsonPropertyName("collection")] string? Collection = null,
    [property: JsonPropertyName("condition")] string? Condition = null,
    [property: JsonPropertyName("maxIterations")] int MaxIterations = 1000,
    [property: JsonPropertyName("iteratorVariable")] string? IteratorVariable = null,
    [property: JsonPropertyName("indexVariable")] string? IndexVariable = null,
    [property: JsonPropertyName("breakCondition")] string? BreakCondition = null,
    [property: JsonPropertyName("continueCondition")] string? ContinueCondition = null
);

/// <summary>
/// Состояние выполнения цикла
/// </summary>
public enum LoopExecutionStatus
{
    /// <summary>
    /// Цикл инициализирован
    /// </summary>
    Initialized,

    /// <summary>
    /// Цикл выполняется
    /// </summary>
    Running,

    /// <summary>
    /// Цикл завершен успешно
    /// </summary>
    Completed,

    /// <summary>
    /// Цикл завершен с ошибкой
    /// </summary>
    Failed,

    /// <summary>
    /// Цикл прерван по условию break
    /// </summary>
    Broken,

    /// <summary>
    /// Цикл достиг максимального количества итераций
    /// </summary>
    MaxIterationsReached
}

/// <summary>
/// Контекст выполнения цикла
/// </summary>
public class LoopExecutionContext
{
    /// <summary>
    /// Текущая итерация (начиная с 0)
    /// </summary>
    public int CurrentIteration { get; set; } = 0;

    /// <summary>
    /// Общее количество выполненных итераций
    /// </summary>
    public int TotalIterations { get; set; } = 0;

    /// <summary>
    /// Текущий элемент коллекции (для ForEach)
    /// </summary>
    public object? CurrentItem { get; set; }

    /// <summary>
    /// Индекс текущего элемента (для ForEach)
    /// </summary>
    public int CurrentIndex { get; set; } = -1;

    /// <summary>
    /// Переменные, видимые в области видимости цикла
    /// </summary>
    public Dictionary<string, object> ScopedVariables { get; init; } = new();

    /// <summary>
    /// Статус выполнения цикла
    /// </summary>
    public LoopExecutionStatus Status { get; set; } = LoopExecutionStatus.Initialized;

    /// <summary>
    /// Время начала выполнения цикла
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Результаты выполнения итераций
    /// </summary>
    public List<LoopIterationResult> IterationResults { get; init; } = new();

    /// <summary>
    /// Создает изолированный контекст для итерации
    /// </summary>
    /// <param name="baseContext">Базовый контекст workflow</param>
    /// <returns>Контекст с переменными текущей итерации</returns>
    public WorkflowContext CreateIterationContext(WorkflowContext baseContext)
    {
        var iterationVariables = new Dictionary<string, object>(baseContext.Variables);

        // Добавляем переменные области видимости цикла
        foreach (var scopedVar in ScopedVariables)
        {
            iterationVariables[scopedVar.Key] = scopedVar.Value;
        }

        // Добавляем системные переменные цикла
        iterationVariables["_loop_iteration"] = CurrentIteration;
        iterationVariables["_loop_index"] = CurrentIndex;
        iterationVariables["_loop_total_iterations"] = TotalIterations;

        if (CurrentItem != null)
        {
            iterationVariables["_loop_current_item"] = CurrentItem;
        }

        return new WorkflowContext(iterationVariables, baseContext.ExecutionId, baseContext.CancellationToken);
    }
}

/// <summary>
/// Результат выполнения одной итерации цикла
/// </summary>
/// <param name="IterationNumber">Номер итерации</param>
/// <param name="Status">Статус выполнения итерации</param>
/// <param name="Variables">Переменные, созданные или изменены в итерации</param>
/// <param name="Duration">Длительность выполнения итерации</param>
/// <param name="Error">Ошибка выполнения, если есть</param>
/// <param name="BreakRequested">Запрошен ли выход из цикла</param>
/// <param name="ContinueRequested">Запрошен ли переход к следующей итерации</param>
public record LoopIterationResult(
    int IterationNumber,
    WorkflowStatus Status,
    Dictionary<string, object>? Variables = null,
    TimeSpan? Duration = null,
    Exception? Error = null,
    bool BreakRequested = false,
    bool ContinueRequested = false
);

/// <summary>
/// Результат выполнения цикла в workflow
/// </summary>
/// <param name="LoopType">Тип выполненного цикла</param>
/// <param name="Status">Итоговый статус выполнения цикла</param>
/// <param name="TotalIterations">Общее количество выполненных итераций</param>
/// <param name="SuccessfulIterations">Количество успешных итераций</param>
/// <param name="FailedIterations">Количество неудачных итераций</param>
/// <param name="Duration">Общая длительность выполнения цикла</param>
/// <param name="OutputVariables">Выходные переменные цикла</param>
/// <param name="IterationResults">Детальные результаты итераций</param>
/// <param name="Error">Критическая ошибка цикла, если есть</param>
public record LoopExecutionResult(
    LoopType LoopType,
    LoopExecutionStatus Status,
    int TotalIterations,
    int SuccessfulIterations,
    int FailedIterations,
    TimeSpan Duration,
    Dictionary<string, object> OutputVariables,
    List<LoopIterationResult> IterationResults,
    Exception? Error = null
);