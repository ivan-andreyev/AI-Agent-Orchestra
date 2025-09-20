using Microsoft.Extensions.Logging;
using Orchestra.Core.Models.Workflow;
using System.Collections;

namespace Orchestra.Core.Services;

/// <summary>
/// Интерфейс для выполнения циклов в workflow
/// </summary>
public interface ILoopExecutor
{
    /// <summary>
    /// Выполняет цикл согласно определению
    /// </summary>
    /// <param name="loopDefinition">Определение цикла</param>
    /// <param name="nestedSteps">Шаги, выполняемые в теле цикла</param>
    /// <param name="context">Контекст выполнения workflow</param>
    /// <param name="stepExecutor">Функция выполнения шагов</param>
    /// <returns>Результат выполнения цикла</returns>
    Task<LoopExecutionResult> ExecuteLoopAsync(
        LoopDefinition loopDefinition,
        List<WorkflowStep> nestedSteps,
        WorkflowContext context,
        Func<List<WorkflowStep>, WorkflowContext, Task<List<WorkflowStepResult>>> stepExecutor);
}

/// <summary>
/// Реализация исполнителя циклов в workflow
/// </summary>
public class LoopExecutor : ILoopExecutor
{
    private readonly ILogger<LoopExecutor> _logger;
    private readonly IExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Инициализирует новый экземпляр LoopExecutor
    /// </summary>
    /// <param name="logger">Логгер для записи событий выполнения</param>
    /// <param name="expressionEvaluator">Оценщик выражений для условий цикла</param>
    public LoopExecutor(ILogger<LoopExecutor> logger, IExpressionEvaluator expressionEvaluator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));
    }

    /// <summary>
    /// Выполняет цикл согласно определению
    /// </summary>
    public async Task<LoopExecutionResult> ExecuteLoopAsync(
        LoopDefinition loopDefinition,
        List<WorkflowStep> nestedSteps,
        WorkflowContext context,
        Func<List<WorkflowStep>, WorkflowContext, Task<List<WorkflowStepResult>>> stepExecutor)
    {
        if (loopDefinition == null)
        {
            throw new ArgumentNullException(nameof(loopDefinition));
        }

        if (nestedSteps == null)
        {
            throw new ArgumentNullException(nameof(nestedSteps));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (stepExecutor == null)
        {
            throw new ArgumentNullException(nameof(stepExecutor));
        }

        _logger.LogDebug("Начало выполнения цикла типа {LoopType} с максимум {MaxIterations} итераций",
            loopDefinition.Type, loopDefinition.MaxIterations);

        var loopContext = new LoopExecutionContext
        {
            Status = LoopExecutionStatus.Running,
            StartTime = DateTime.UtcNow
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = loopDefinition.Type switch
            {
                LoopType.ForEach => await ExecuteForEachLoopAsync(loopDefinition, nestedSteps, context, stepExecutor, loopContext),
                LoopType.While => await ExecuteWhileLoopAsync(loopDefinition, nestedSteps, context, stepExecutor, loopContext),
                LoopType.Retry => await ExecuteRetryLoopAsync(loopDefinition, nestedSteps, context, stepExecutor, loopContext),
                _ => throw new NotSupportedException($"Тип цикла {loopDefinition.Type} не поддерживается")
            };

            stopwatch.Stop();

            _logger.LogInformation("Цикл типа {LoopType} завершен со статусом {Status} за {Duration} мс. Итераций: {TotalIterations}",
                loopDefinition.Type, result.Status, stopwatch.ElapsedMilliseconds, result.TotalIterations);

            return result with { Duration = stopwatch.Elapsed };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Критическая ошибка при выполнении цикла типа {LoopType}", loopDefinition.Type);

            loopContext.Status = LoopExecutionStatus.Failed;

            return new LoopExecutionResult(
                loopDefinition.Type,
                LoopExecutionStatus.Failed,
                loopContext.TotalIterations,
                loopContext.IterationResults.Count(r => r.Status == WorkflowStatus.Completed),
                loopContext.IterationResults.Count(r => r.Status == WorkflowStatus.Failed),
                stopwatch.Elapsed,
                new Dictionary<string, object>(),
                loopContext.IterationResults,
                ex
            );
        }
    }

    /// <summary>
    /// Выполняет цикл ForEach по коллекции
    /// </summary>
    private async Task<LoopExecutionResult> ExecuteForEachLoopAsync(
        LoopDefinition loopDefinition,
        List<WorkflowStep> nestedSteps,
        WorkflowContext context,
        Func<List<WorkflowStep>, WorkflowContext, Task<List<WorkflowStepResult>>> stepExecutor,
        LoopExecutionContext loopContext)
    {
        if (string.IsNullOrWhiteSpace(loopDefinition.Collection))
        {
            throw new ArgumentException("ForEach цикл требует указания коллекции", nameof(loopDefinition));
        }

        // Получение коллекции из контекста
        var collection = ResolveCollection(loopDefinition.Collection, context);
        if (collection == null)
        {
            _logger.LogWarning("Коллекция {CollectionName} не найдена в контексте", loopDefinition.Collection);
            loopContext.Status = LoopExecutionStatus.Completed;
            return CreateLoopResult(loopDefinition, loopContext);
        }

        var collectionArray = collection.Cast<object>().ToArray();
        _logger.LogDebug("ForEach цикл обрабатывает коллекцию из {ItemCount} элементов", collectionArray.Length);

        for (int index = 0; index < collectionArray.Length && loopContext.CurrentIteration < loopDefinition.MaxIterations; index++)
        {
            var item = collectionArray[index];
            loopContext.CurrentItem = item;
            loopContext.CurrentIndex = index;

            // Обновление переменных области видимости
            UpdateScopedVariables(loopDefinition, loopContext, item, index);

            var iterationResult = await ExecuteLoopIterationAsync(
                loopDefinition,
                nestedSteps,
                context,
                stepExecutor,
                loopContext);

            loopContext.IterationResults.Add(iterationResult);
            loopContext.CurrentIteration++;
            loopContext.TotalIterations++;

            // Проверка условий выхода
            if (iterationResult.BreakRequested || await ShouldBreakLoop(loopDefinition, loopContext, context))
            {
                _logger.LogDebug("ForEach цикл прерван на итерации {Iteration}", loopContext.CurrentIteration);
                loopContext.Status = LoopExecutionStatus.Broken;
                break;
            }

            if (iterationResult.ContinueRequested)
            {
                _logger.LogDebug("ForEach цикл пропускает итерацию {Iteration}", loopContext.CurrentIteration);
                continue;
            }

            // Проверка токена отмены
            context.CancellationToken.ThrowIfCancellationRequested();
        }

        // Проверка достижения максимального количества итераций
        if (loopContext.CurrentIteration >= loopDefinition.MaxIterations)
        {
            _logger.LogWarning("ForEach цикл достиг максимального количества итераций: {MaxIterations}", loopDefinition.MaxIterations);
            loopContext.Status = LoopExecutionStatus.MaxIterationsReached;
        }
        else if (loopContext.Status == LoopExecutionStatus.Running)
        {
            loopContext.Status = LoopExecutionStatus.Completed;
        }

        return CreateLoopResult(loopDefinition, loopContext);
    }

    /// <summary>
    /// Выполняет цикл While с условием продолжения
    /// </summary>
    private async Task<LoopExecutionResult> ExecuteWhileLoopAsync(
        LoopDefinition loopDefinition,
        List<WorkflowStep> nestedSteps,
        WorkflowContext context,
        Func<List<WorkflowStep>, WorkflowContext, Task<List<WorkflowStepResult>>> stepExecutor,
        LoopExecutionContext loopContext)
    {
        if (string.IsNullOrWhiteSpace(loopDefinition.Condition))
        {
            throw new ArgumentException("While цикл требует указания условия", nameof(loopDefinition));
        }

        _logger.LogDebug("While цикл с условием: {Condition}", loopDefinition.Condition);

        while (loopContext.CurrentIteration < loopDefinition.MaxIterations)
        {
            // Оценка условия продолжения цикла
            var executionContext = CreateExpressionContext(context, loopContext);
            var shouldContinue = await _expressionEvaluator.EvaluateAsync(loopDefinition.Condition, executionContext);

            if (!shouldContinue)
            {
                _logger.LogDebug("While цикл завершен - условие больше не выполняется на итерации {Iteration}",
                    loopContext.CurrentIteration);
                break;
            }

            // Обновление переменных области видимости
            UpdateScopedVariables(loopDefinition, loopContext, null, loopContext.CurrentIteration);

            var iterationResult = await ExecuteLoopIterationAsync(
                loopDefinition,
                nestedSteps,
                context,
                stepExecutor,
                loopContext);

            loopContext.IterationResults.Add(iterationResult);
            loopContext.CurrentIteration++;
            loopContext.TotalIterations++;

            // Проверка условий выхода
            if (iterationResult.BreakRequested || await ShouldBreakLoop(loopDefinition, loopContext, context))
            {
                _logger.LogDebug("While цикл прерван на итерации {Iteration}", loopContext.CurrentIteration);
                loopContext.Status = LoopExecutionStatus.Broken;
                break;
            }

            if (iterationResult.ContinueRequested)
            {
                _logger.LogDebug("While цикл пропускает итерацию {Iteration}", loopContext.CurrentIteration);
                continue;
            }

            // Проверка токена отмены
            context.CancellationToken.ThrowIfCancellationRequested();
        }

        // Проверка достижения максимального количества итераций
        if (loopContext.CurrentIteration >= loopDefinition.MaxIterations)
        {
            _logger.LogWarning("While цикл достиг максимального количества итераций: {MaxIterations}", loopDefinition.MaxIterations);
            loopContext.Status = LoopExecutionStatus.MaxIterationsReached;
        }
        else if (loopContext.Status == LoopExecutionStatus.Running)
        {
            loopContext.Status = LoopExecutionStatus.Completed;
        }

        return CreateLoopResult(loopDefinition, loopContext);
    }

    /// <summary>
    /// Выполняет цикл Retry для повторных попыток
    /// </summary>
    private async Task<LoopExecutionResult> ExecuteRetryLoopAsync(
        LoopDefinition loopDefinition,
        List<WorkflowStep> nestedSteps,
        WorkflowContext context,
        Func<List<WorkflowStep>, WorkflowContext, Task<List<WorkflowStepResult>>> stepExecutor,
        LoopExecutionContext loopContext)
    {
        _logger.LogDebug("Retry цикл с максимум {MaxIterations} попыток", loopDefinition.MaxIterations);

        Exception? lastException = null;

        while (loopContext.CurrentIteration < loopDefinition.MaxIterations)
        {
            // Обновление переменных области видимости
            UpdateScopedVariables(loopDefinition, loopContext, null, loopContext.CurrentIteration);

            var iterationResult = await ExecuteLoopIterationAsync(
                loopDefinition,
                nestedSteps,
                context,
                stepExecutor,
                loopContext);

            loopContext.IterationResults.Add(iterationResult);
            loopContext.CurrentIteration++;
            loopContext.TotalIterations++;

            // Для Retry цикла успешное выполнение означает завершение
            if (iterationResult.Status == WorkflowStatus.Completed)
            {
                _logger.LogDebug("Retry цикл успешно завершен на попытке {Iteration}", loopContext.CurrentIteration);
                loopContext.Status = LoopExecutionStatus.Completed;
                break;
            }

            // Сохраняем последнюю ошибку
            if (iterationResult.Error != null)
            {
                lastException = iterationResult.Error;
            }

            // Проверка условий выхода
            if (iterationResult.BreakRequested || await ShouldBreakLoop(loopDefinition, loopContext, context))
            {
                _logger.LogDebug("Retry цикл прерван на итерации {Iteration}", loopContext.CurrentIteration);
                loopContext.Status = LoopExecutionStatus.Broken;
                break;
            }

            // Задержка между попытками (кроме последней)
            if (loopContext.CurrentIteration < loopDefinition.MaxIterations)
            {
                var delay = CalculateRetryDelay(loopContext.CurrentIteration);
                _logger.LogDebug("Ожидание {DelayMs} мс перед следующей попыткой", delay.TotalMilliseconds);
                await Task.Delay(delay, context.CancellationToken);
            }

            // Проверка токена отмены
            context.CancellationToken.ThrowIfCancellationRequested();
        }

        // Проверка достижения максимального количества итераций
        if (loopContext.CurrentIteration >= loopDefinition.MaxIterations && loopContext.Status == LoopExecutionStatus.Running)
        {
            _logger.LogWarning("Retry цикл исчерпал все попытки: {MaxIterations}", loopDefinition.MaxIterations);
            loopContext.Status = LoopExecutionStatus.MaxIterationsReached;
        }

        var result = CreateLoopResult(loopDefinition, loopContext);

        // Для неудачного Retry цикла добавляем последнюю ошибку
        if (loopContext.Status != LoopExecutionStatus.Completed && lastException != null)
        {
            return result with { Error = lastException };
        }

        return result;
    }

    /// <summary>
    /// Выполняет одну итерацию цикла
    /// </summary>
    private async Task<LoopIterationResult> ExecuteLoopIterationAsync(
        LoopDefinition loopDefinition,
        List<WorkflowStep> nestedSteps,
        WorkflowContext context,
        Func<List<WorkflowStep>, WorkflowContext, Task<List<WorkflowStepResult>>> stepExecutor,
        LoopExecutionContext loopContext)
    {
        var iterationStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var iterationNumber = loopContext.CurrentIteration;

        try
        {
            _logger.LogDebug("Выполнение итерации {IterationNumber} цикла", iterationNumber);

            // Создание изолированного контекста для итерации
            var iterationContext = loopContext.CreateIterationContext(context);

            // Проверка условия continue до выполнения шагов
            if (!string.IsNullOrWhiteSpace(loopDefinition.ContinueCondition))
            {
                var expressionContext = CreateExpressionContext(iterationContext, loopContext);
                var shouldContinue = await _expressionEvaluator.EvaluateAsync(loopDefinition.ContinueCondition, expressionContext);

                if (shouldContinue)
                {
                    _logger.LogDebug("Итерация {IterationNumber} пропущена по условию continue", iterationNumber);
                    iterationStopwatch.Stop();
                    return new LoopIterationResult(
                        iterationNumber,
                        WorkflowStatus.Completed,
                        new Dictionary<string, object> { ["skipped"] = true, ["reason"] = "continue_condition" },
                        iterationStopwatch.Elapsed,
                        null,
                        false,
                        true
                    );
                }
            }

            // Выполнение шагов итерации
            var stepResults = await stepExecutor(nestedSteps, iterationContext);

            iterationStopwatch.Stop();

            // Определение статуса итерации
            var iterationStatus = stepResults.All(sr => sr.Status == WorkflowStatus.Completed)
                ? WorkflowStatus.Completed
                : WorkflowStatus.Failed;

            // Сбор выходных переменных итерации
            var iterationVariables = new Dictionary<string, object>();
            foreach (var stepResult in stepResults)
            {
                if (stepResult.Output != null)
                {
                    foreach (var output in stepResult.Output)
                    {
                        iterationVariables[$"{stepResult.StepId}.{output.Key}"] = output.Value;
                    }
                }
            }

            // Проверка условия break после выполнения шагов
            bool breakRequested = false;
            if (!string.IsNullOrWhiteSpace(loopDefinition.BreakCondition))
            {
                var expressionContext = CreateExpressionContext(iterationContext, loopContext);
                breakRequested = await _expressionEvaluator.EvaluateAsync(loopDefinition.BreakCondition, expressionContext);
            }

            // Обновление основного контекста переменными итерации
            MergeIterationVariables(context, iterationVariables, iterationNumber);

            var firstError = stepResults.FirstOrDefault(sr => sr.Error != null)?.Error;

            return new LoopIterationResult(
                iterationNumber,
                iterationStatus,
                iterationVariables,
                iterationStopwatch.Elapsed,
                firstError,
                breakRequested,
                false
            );
        }
        catch (Exception ex)
        {
            iterationStopwatch.Stop();
            _logger.LogError(ex, "Ошибка при выполнении итерации {IterationNumber} цикла", iterationNumber);

            return new LoopIterationResult(
                iterationNumber,
                WorkflowStatus.Failed,
                new Dictionary<string, object> { ["error"] = ex.Message },
                iterationStopwatch.Elapsed,
                ex
            );
        }
    }

    /// <summary>
    /// Разрешает коллекцию из контекста переменных
    /// </summary>
    private IEnumerable? ResolveCollection(string collectionReference, WorkflowContext context)
    {
        var variableName = collectionReference.TrimStart('$');

        if (context.Variables.TryGetValue(variableName, out var value))
        {
            return value switch
            {
                string str => str.ToCharArray(), // Строка как коллекция символов
                IEnumerable enumerable => enumerable,
                _ => new[] { value } // Одиночное значение как коллекция из одного элемента
            };
        }

        _logger.LogDebug("Коллекция {CollectionName} не найдена в контексте", collectionReference);
        return null;
    }

    /// <summary>
    /// Обновляет переменные области видимости цикла
    /// </summary>
    private void UpdateScopedVariables(LoopDefinition loopDefinition, LoopExecutionContext loopContext, object? currentItem, int index)
    {
        // Обновление переменной итератора
        if (!string.IsNullOrWhiteSpace(loopDefinition.IteratorVariable) && currentItem != null)
        {
            loopContext.ScopedVariables[loopDefinition.IteratorVariable] = currentItem;
        }

        // Обновление переменной индекса
        if (!string.IsNullOrWhiteSpace(loopDefinition.IndexVariable))
        {
            loopContext.ScopedVariables[loopDefinition.IndexVariable] = index;
        }
    }

    /// <summary>
    /// Проверяет условие выхода из цикла
    /// </summary>
    private async Task<bool> ShouldBreakLoop(LoopDefinition loopDefinition, LoopExecutionContext loopContext, WorkflowContext context)
    {
        if (string.IsNullOrWhiteSpace(loopDefinition.BreakCondition))
        {
            return false;
        }

        try
        {
            var expressionContext = CreateExpressionContext(context, loopContext);
            return await _expressionEvaluator.EvaluateAsync(loopDefinition.BreakCondition, expressionContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при оценке условия выхода из цикла: {BreakCondition}", loopDefinition.BreakCondition);
            return false;
        }
    }

    /// <summary>
    /// Создает контекст для оценки выражений в цикле
    /// </summary>
    private WorkflowExecutionContext CreateExpressionContext(WorkflowContext workflowContext, LoopExecutionContext loopContext)
    {
        var executionContext = new WorkflowExecutionContext
        {
            Variables = new Dictionary<string, object>(workflowContext.Variables),
            StepResults = new Dictionary<string, object>(),
            Metadata = new Dictionary<string, object>
            {
                ["_loop_iteration"] = loopContext.CurrentIteration,
                ["_loop_total_iterations"] = loopContext.TotalIterations,
                ["_loop_status"] = loopContext.Status.ToString()
            }
        };

        // Добавление переменных области видимости цикла
        foreach (var scopedVar in loopContext.ScopedVariables)
        {
            executionContext.Variables[scopedVar.Key] = scopedVar.Value;
        }

        return executionContext;
    }

    /// <summary>
    /// Вычисляет задержку для повторной попытки
    /// </summary>
    private TimeSpan CalculateRetryDelay(int attemptNumber)
    {
        // Экспоненциальная задержка: 100ms * 2^attemptNumber, максимум 5 секунд
        var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attemptNumber));
        var maxDelay = TimeSpan.FromSeconds(5);
        return delay > maxDelay ? maxDelay : delay;
    }

    /// <summary>
    /// Объединяет переменные итерации с основным контекстом
    /// </summary>
    private void MergeIterationVariables(WorkflowContext context, Dictionary<string, object> iterationVariables, int iterationNumber)
    {
        foreach (var variable in iterationVariables)
        {
            var variableName = $"iteration_{iterationNumber}_{variable.Key}";
            context.Variables[variableName] = variable.Value;
        }

        // Обновление последних значений (без номера итерации)
        foreach (var variable in iterationVariables)
        {
            context.Variables[$"last_{variable.Key}"] = variable.Value;
        }
    }

    /// <summary>
    /// Создает результат выполнения цикла
    /// </summary>
    private LoopExecutionResult CreateLoopResult(LoopDefinition loopDefinition, LoopExecutionContext loopContext)
    {
        var successfulIterations = loopContext.IterationResults.Count(r => r.Status == WorkflowStatus.Completed);
        var failedIterations = loopContext.IterationResults.Count(r => r.Status == WorkflowStatus.Failed);

        var outputVariables = new Dictionary<string, object>
        {
            ["totalIterations"] = loopContext.TotalIterations,
            ["successfulIterations"] = successfulIterations,
            ["failedIterations"] = failedIterations,
            ["loopStatus"] = loopContext.Status.ToString(),
            ["loopType"] = loopDefinition.Type.ToString()
        };

        // Добавление итоговых переменных из области видимости
        foreach (var scopedVar in loopContext.ScopedVariables)
        {
            outputVariables[$"final_{scopedVar.Key}"] = scopedVar.Value;
        }

        return new LoopExecutionResult(
            loopDefinition.Type,
            loopContext.Status,
            loopContext.TotalIterations,
            successfulIterations,
            failedIterations,
            DateTime.UtcNow - loopContext.StartTime,
            outputVariables,
            loopContext.IterationResults
        );
    }
}