# Phase 2: Batch Operations - Детальный План Декомпозиции

**Дата создания:** 2025-09-19
**Общая оценка времени:** 10-14 часов
**Статус:** Detailed Planning
**Основание:** Декомпозиция задач из actions-block-refactoring-workplan.md (строки 148-224)

## Цели декомпозиции

- **Планирование:** Разбить задачи по 1-3 часа для удобного планирования
- **Отслеживание:** Создать четкие чекпойнты для валидации прогресса
- **Реализация:** Упростить сложные алгоритмы до выполнимых шагов
- **Качество:** Учесть уроки Phase 1 (LOW compliance 23/100)

## Структура декомпозиции

### Задача 1: Batch Execution Engine (Общая оценка: 5-7 часов)

#### 1.1 Базовая инфраструктура Batch Engine (2.5 часа)

**Подзадачи:**

**1.1.1 Создание BatchTaskExecutor класса (1 час)**
- **Файл:** `src/Orchestra.Core/Services/BatchTaskExecutor.cs`
- **Описание:** Создать основной класс для координации batch операций
- **Acceptance Criteria:**
  - Класс реализует IBatchTaskExecutor интерфейс
  - Конструктор принимает ILogger, IServiceProvider
  - Публичный метод ExecuteBatchAsync(BatchExecutionRequest)
  - Соответствие правилам стиля (.cursor/rules/csharp-codestyle.mdc)
  - XML документация на русском языке
- **Зависимости:** Нет
- **Файлы для создания:**
  - `src/Orchestra.Core/Interfaces/IBatchTaskExecutor.cs`
  - `src/Orchestra.Core/Models/BatchExecutionRequest.cs`
  - `src/Orchestra.Core/Models/BatchExecutionResult.cs`

**1.1.2 Модели данных для Batch операций (1 час)**
- **Файлы:**
  - `src/Orchestra.Core/Models/BatchTaskContext.cs`
  - `src/Orchestra.Core/Models/BatchExecutionOptions.cs`
  - `src/Orchestra.Core/Models/TaskDependency.cs`
- **Описание:** Создать модели для хранения состояния batch операций
- **Acceptance Criteria:**
  - BatchTaskContext содержит Id, Status, Dependencies, Result
  - BatchExecutionOptions содержит MaxConcurrency (1-20), ErrorPolicy, Timeout
  - TaskDependency содержит TaskId, DependsOnTaskId, Type
  - Все классы immutable (record types)
  - XML документация на русском языке
- **Зависимости:** 1.1.1

**1.1.3 Регистрация DI для Batch Engine (0.5 часа)**
- **Файл:** `src/Orchestra.API/Program.cs` или DI extension
- **Описание:** Настроить dependency injection для batch components
- **Acceptance Criteria:**
  - IBatchTaskExecutor зарегистрирован как Scoped
  - Все зависимости корректно разрешаются
  - Unit тест проверяет регистрацию
- **Зависимости:** 1.1.1, 1.1.2

#### 1.2 Алгоритм построения dependency graph (1.5 часа)

**1.2.1 Dependency Graph Builder (1 час)**
- **Файл:** `src/Orchestra.Core/Services/DependencyGraphBuilder.cs`
- **Описание:** Реализация алгоритма построения DAG из зависимостей
- **Acceptance Criteria:**
  - Метод BuildGraph(IEnumerable&lt;TaskDependency&gt;) возвращает DirectedGraph
  - Обнаружение циклических зависимостей через DFS
  - Генерация CircularDependencyException при обнаружении циклов
  - Корректная топологическая сортировка
  - Unit тесты покрывают сценарии с циклами и без
- **Зависимости:** 1.1.2

**1.2.2 Topological Sort Implementation (0.5 часа)**
- **Файл:** `src/Orchestra.Core/Algorithms/TopologicalSort.cs`
- **Описание:** Алгоритм топологической сортировки для порядка выполнения
- **Acceptance Criteria:**
  - Статический метод Sort(DirectedGraph) возвращает упорядоченный список
  - Корректная обработка графов без зависимостей
  - Unit тесты с различными конфигурациями графов
- **Зависимости:** 1.2.1

#### 1.3 Валидация и контроль доступа (1 час)

**1.3.1 Repository Access Validator (1 час)**
- **Файл:** `src/Orchestra.Core/Services/RepositoryAccessValidator.cs`
- **Описание:** Валидация доступа к репозиториям перед выполнением
- **Acceptance Criteria:**
  - Асинхронная проверка существования репозиториев
  - Валидация пользовательских разрешений
  - Возврат списка недоступных репозиториев
  - Кэширование результатов проверки на время сессии
  - Unit тесты с mock репозиториями
- **Зависимости:** 1.1.2

#### 1.4 Исполнение с управлением concurrency (2 часа)

**1.4.1 Concurrent Task Executor (1.5 часа)**
- **Файл:** `src/Orchestra.Core/Services/ConcurrentTaskExecutor.cs`
- **Описание:** Выполнение задач с контролем параллелизма
- **Acceptance Criteria:**
  - Использование SemaphoreSlim для ограничения concurrency
  - Graceful cancellation через CancellationToken
  - Thread-safe обновление статусов задач
  - Retry механизм с exponential backoff
  - Unit тесты с различными уровнями concurrency
- **Зависимости:** 1.2.2, 1.3.1

**1.4.2 Error Handling Strategies (0.5 часа)**
- **Файл:** `src/Orchestra.Core/Services/BatchErrorHandler.cs`
- **Описание:** Стратегии обработки ошибок в batch операциях
- **Acceptance Criteria:**
  - Поддержка ContinueOnError и StopOnError политик
  - Логирование ошибок с контекстом batch операции
  - Агрегация ошибок в BatchExecutionResult
  - Unit тесты для каждой стратегии
- **Зависимости:** 1.4.1

### Задача 2: Repository Multi-Select Interface (Общая оценка: 2-3 часа)

#### 2.1 Backend для multi-select (1 час)

**2.1.1 Repository Multi-Select API (1 час)**
- **Файл:** `src/Orchestra.API/Controllers/RepositoryController.cs` (расширение)
- **Описание:** API endpoints для работы с множественным выбором репозиториев
- **Acceptance Criteria:**
  - GET /api/repositories/available - список доступных репозиториев
  - POST /api/repositories/validate-batch - валидация выбранных репозиториев
  - Поддержка фильтрации и группировки
  - OpenAPI документация обновлена
  - Интеграционные тесты для endpoints
- **Зависимости:** 1.3.1

#### 2.2 Frontend компоненты (1.5-2 часа)

**2.2.1 Repository Multi-Select Component (1 час)**
- **Файл:** `src/Orchestra.Web/Components/RepositoryMultiSelect.tsx`
- **Описание:** React компонент для множественного выбора репозиториев
- **Acceptance Criteria:**
  - Checkboxes для individual selection
  - Select All / Deselect All функциональность
  - Поиск и фильтрация по названию
  - Группировка по организациям/командам
  - Validation feedback для недоступных репозиториев
  - Responsive design
- **Зависимости:** 2.1.1

**2.2.2 Repository Group Filter (0.5-1 час)**
- **Файл:** `src/Orchestra.Web/Components/RepositoryGroupFilter.tsx`
- **Описание:** Компонент фильтрации репозиториев по группам
- **Acceptance Criteria:**
  - Dropdown для выбора групп/организаций
  - Быстрые фильтры (Recent, Favorites)
  - State синхронизация с RepositoryMultiSelect
  - Unit тесты с React Testing Library
- **Зависимости:** 2.2.1

### Задача 3: Progress Visualization (Общая оценка: 2.5-4 часа)

#### 3.1 Backend для progress tracking (1-1.5 часа)

**3.1.1 Progress Tracking Service (1 час)**
- **Файл:** `src/Orchestra.Core/Services/BatchProgressTracker.cs`
- **Описание:** Сервис для отслеживания и вычисления прогресса
- **Acceptance Criteria:**
  - Реализация алгоритма UpdateBatchProgress из спецификации
  - Расчет completion percentage и ETA
  - Thread-safe обновления прогресса
  - Интеграция с SignalR для real-time updates
  - Unit тесты для алгоритмов расчета
- **Зависимости:** 1.1.2

**3.1.2 SignalR Progress Hub Extension (0.5 часа)**
- **Файл:** `src/Orchestra.API/Hubs/BatchProgressHub.cs`
- **Описание:** SignalR hub для broadcast прогресса batch операций
- **Acceptance Criteria:**
  - Метод JoinBatchProgress(batchId) для подписки
  - Broadcast BatchProgressUpdate events
  - Connection management для multiple batches
  - Интеграционные тесты SignalR functionality
- **Зависимости:** 3.1.1

#### 3.2 Frontend progress visualization (1.5-2.5 часа)

**3.2.1 Batch Progress Component (1 час)**
- **Файл:** `src/Orchestra.Web/Components/BatchProgress.tsx`
- **Описание:** Основной компонент визуализации прогресса
- **Acceptance Criteria:**
  - Overall progress bar с процентами
  - ETA отображение в human-readable формате
  - Individual task status indicators
  - Error summary при наличии ошибок
  - Real-time updates via SignalR
- **Зависимости:** 3.1.2

**3.2.2 Execution Timeline Visualization (1-1.5 часа)**
- **Файл:** `src/Orchestra.Web/Components/ExecutionTimeline.tsx`
- **Описание:** Timeline визуализация выполнения задач
- **Acceptance Criteria:**
  - Chronological timeline с задачами
  - Visual indication of dependencies
  - Status colors (pending, running, completed, failed)
  - Hover tooltips с деталями задач
  - Responsive design для различных screen sizes
- **Зависимости:** 3.2.1

**3.2.3 Progress Integration с Actions Panel (0.5 часа)**
- **Файл:** Расширение существующего Actions Panel компонента
- **Описание:** Интеграция progress visualization в Actions Panel
- **Acceptance Criteria:**
  - Modal/drawer для детального прогресса
  - Compact progress indicator в main panel
  - Переключение между modes (compact/detailed)
  - State management синхронизация
- **Зависимости:** 3.2.1, 3.2.2

## Последовательность выполнения и зависимости

### Критический путь:
1. **1.1 → 1.2 → 1.3 → 1.4** (Batch Engine development)
2. **2.1 → 2.2** (Repository Multi-Select)
3. **3.1 → 3.2** (Progress Visualization)

### Параллельные треки:
- **Задачи 2.1** и **3.1** могут выполняться параллельно после завершения 1.1
- **Frontend задачи 2.2** и **3.2** могут выполняться параллельно

## Критерии качества и соответствия

### Код Style Requirements (из урока Phase 1):
- **Обязательные брaces:** Все control structures с braces на отдельных строках
- **Размер файлов:** Максимум 300 строк на файл
- **Один тип на файл:** Избегать multiple types в одном файле
- **XML документация:** На русском языке для всех public members
- **Boy Scout Rule:** Оставлять код лучше, чем нашли

### Архитектурные требования:
- **Mediator Pattern:** Все business operations через IGameMediator
- **Command/Query разделение:** Отдельные commands для batch operations
- **Event-driven:** Events для communication между компонентами
- **Framework-first:** Расширяемость для различных типов AI agents

### Testing Requirements:
- **Unit тесты:** Для каждого service class и algorithm
- **Integration тесты:** Для API endpoints и SignalR hubs
- **Frontend тесты:** React Testing Library для UI components
- **Coverage:** Минимум 80% для critical business logic

## Итоговые Deliverables

### Phase 2 Completion Criteria:
1. **Functional Batch Engine** с dependency resolution
2. **Repository Multi-Select Interface** с validation
3. **Real-time Progress Visualization** с ETA
4. **Comprehensive Error Handling** для всех failure scenarios
5. **Integration Tests** подтверждающие end-to-end functionality
6. **Performance Tests** для concurrency и scalability
7. **Code Style Compliance** 90%+ (vs Phase 1's 23%)

### Готовность к Phase 3:
- Stable batch execution foundation
- Extensible progress tracking system
- Robust error handling mechanisms
- Clean, maintainable codebase готовый для advanced features

---

**Создан:** work-plan-architect agent
**Следующий шаг:** Валидация через work-plan-reviewer agent
**Статус:** Ready for Review