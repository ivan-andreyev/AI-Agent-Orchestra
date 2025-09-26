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

    // Методы для поддержки Markdown workflow'ов

    /// <summary>
    /// Выполняет workflow из markdown файла с автоматической конвертацией в WorkflowDefinition.
    /// Интегрируется с существующей системой валидации workflow через ValidateWorkflowAsync и использует
    /// стандартную инфраструктуру выполнения ExecuteAsync для обеспечения единообразной обработки.
    /// </summary>
    /// <param name="markdownFilePath">Путь к markdown файлу с определением workflow. Должен быть абсолютным или относительным путем к существующему файлу с расширением .md</param>
    /// <param name="context">Контекст выполнения с переменными и состоянием</param>
    /// <returns>Результат выполнения workflow</returns>
    /// <exception cref="ArgumentNullException">Если markdownFilePath или context равны null</exception>
    /// <exception cref="ArgumentException">Если markdownFilePath представляет пустую строку или недопустимый путь к файлу</exception>
    /// <exception cref="FileNotFoundException">Если markdown файл не найден по указанному пути</exception>
    /// <exception cref="InvalidOperationException">Если markdown не может быть преобразован в workflow или содержит некорректную структуру</exception>
    Task<WorkflowExecutionResult> ExecuteMarkdownWorkflowAsync(string markdownFilePath, WorkflowContext context);

    /// <summary>
    /// Конвертирует markdown файл в объект WorkflowDefinition для дальнейшего использования.
    /// Созданный WorkflowDefinition полностью совместим с существующими методами ValidateWorkflowAsync и ExecuteAsync,
    /// обеспечивая бесшовную интеграцию с основной инфраструктурой выполнения workflow.
    /// </summary>
    /// <param name="markdownFilePath">Путь к markdown файлу с определением workflow. Должен указывать на существующий файл с расширением .md и корректной структурой workflow</param>
    /// <returns>Объект WorkflowDefinition, созданный из markdown файла</returns>
    /// <exception cref="ArgumentNullException">Если markdownFilePath равен null</exception>
    /// <exception cref="ArgumentException">Если markdownFilePath представляет пустую строку или содержит недопустимые символы пути</exception>
    /// <exception cref="FileNotFoundException">Если markdown файл не найден по указанному пути</exception>
    /// <exception cref="InvalidOperationException">Если markdown имеет некорректную структуру или не содержит валидного определения workflow</exception>
    Task<WorkflowDefinition> ConvertMarkdownToWorkflowAsync(string markdownFilePath);

    /// <summary>
    /// Валидирует корректность структуры markdown файла как workflow определения.
    /// Использует те же правила валидации, что и ValidateWorkflowAsync, обеспечивая
    /// консистентность проверок между различными источниками workflow определений.
    /// </summary>
    /// <param name="markdownFilePath">Путь к markdown файлу для валидации. Должен указывать на существующий файл с расширением .md</param>
    /// <returns>True если markdown файл содержит корректное workflow определение, false в противном случае</returns>
    /// <exception cref="ArgumentNullException">Если markdownFilePath равен null</exception>
    /// <exception cref="ArgumentException">Если markdownFilePath представляет пустую строку или недопустимый путь к файлу</exception>
    Task<bool> ValidateMarkdownWorkflowAsync(string markdownFilePath);

    /// <summary>
    /// Получает список всех markdown файлов в указанной директории, которые содержат корректные workflow определения.
    /// Каждый найденный файл автоматически валидируется через ValidateMarkdownWorkflowAsync для обеспечения
    /// совместимости с системой выполнения workflow и соответствия стандартам определения процессов.
    /// </summary>
    /// <param name="directoryPath">Путь к директории для поиска markdown workflow файлов. Должен указывать на существующую директорию с правами на чтение</param>
    /// <returns>Список абсолютных путей к валидным markdown workflow файлам, отсортированный по имени файла</returns>
    /// <exception cref="ArgumentNullException">Если directoryPath равен null</exception>
    /// <exception cref="ArgumentException">Если directoryPath представляет пустую строку или содержит недопустимые символы пути</exception>
    /// <exception cref="DirectoryNotFoundException">Если указанная директория не найдена по заданному пути</exception>
    Task<IEnumerable<string>> GetMarkdownWorkflowFilesAsync(string directoryPath);
}