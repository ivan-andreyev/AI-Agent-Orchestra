using Orchestra.Web.Models;

namespace Orchestra.Web.Services;

/// <summary>
/// Интерфейс для построения и валидации графов зависимостей для batch task execution
/// Обрабатывает построение DAG, обнаружение циклических зависимостей и топологическое упорядочивание
/// </summary>
public interface IDependencyGraphBuilder
{
    /// <summary>
    /// Построить направленный ациклический граф (DAG) из зависимостей задач
    /// </summary>
    /// <param name="tasks">Список задач для построения графа</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Граф выполнения с узлами и рёбрами</returns>
    Task<ExecutionGraph> BuildDependencyGraphAsync(List<BatchTaskRequest> tasks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обнаружить циклические зависимости с использованием поиска в глубину
    /// </summary>
    /// <param name="graph">Граф для проверки</param>
    /// <exception cref="CircularDependencyException">Выбрасывается при обнаружении циклической зависимости</exception>
    void ValidateNoCyclicDependencies(ExecutionGraph graph);

    /// <summary>
    /// Рассчитать топологический порядок для последовательности выполнения с использованием алгоритма Кана
    /// </summary>
    /// <param name="graph">Граф выполнения</param>
    /// <returns>Список узлов в топологическом порядке</returns>
    List<TaskNode> CalculateTopologicalOrder(ExecutionGraph graph);
}