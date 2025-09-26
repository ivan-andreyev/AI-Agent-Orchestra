# Задача 01-05: Расширение IWorkflowEngine для поддержки Markdown

**Родительский план**: [01-Markdown-Integration.md](../01-Markdown-Integration.md)
**Приоритет**: КРИТИЧЕСКИЙ
**Статус**: [ ] В разработке
**Оценка времени**: 2-3 часа

## Цель задачи
Расширить существующий интерфейс IWorkflowEngine и класс WorkflowEngine новыми методами для работы с markdown workflow'ами, сохраняя полную обратную совместимость с существующими JSON workflow'ами.

## Входные зависимости
- [x] Модели данных созданы (01-01-markdown-models.md) ✅ COMPLETE
- [x] Парсер реализован (01-02-markdown-parser.md) ✅ COMPLETE
- [x] Конвертер реализован (01-03-workflow-converter.md) ✅ COMPLETE
- [x] File Watcher реализован (01-04-file-watcher.md) ✅ COMPLETE

## Детальная декомпозиция

### Подзадача 05-01: Расширение интерфейса IWorkflowEngine (30 минут)
**Файл**: `src/Orchestra.Core/Interfaces/IWorkflowEngine.cs`

**Действия**:
1. Добавить новые методы в существующий интерфейс IWorkflowEngine:
   ```csharp
   // Новые методы для markdown поддержки
   Task<WorkflowExecutionResult> ExecuteMarkdownWorkflowAsync(string markdownFilePath, WorkflowContext context);
   Task<WorkflowDefinition> ConvertMarkdownToWorkflowAsync(string markdownFilePath);
   Task<bool> ValidateMarkdownWorkflowAsync(string markdownFilePath);
   Task<IEnumerable<string>> GetMarkdownWorkflowFilesAsync(string directoryPath);
   ```

2. Добавить XML документацию на русском языке для всех новых методов

**Критерии приёмки**:
- Интерфейс скомпилируется без ошибок
- Сохранена обратная совместимость со всеми существующими методами
- Все новые методы имеют XML документацию

### Подзадача 05-02: Имплементация методов в WorkflowEngine (60 минут)
**Файл**: `src/Orchestra.Core/Services/WorkflowEngine.cs`

**Действия**:
1. Добавить зависимости через DI:
   ```csharp
   private readonly IMarkdownWorkflowService _markdownService;
   private readonly ILogger<WorkflowEngine> _logger;
   ```

2. Реализовать метод `ExecuteMarkdownWorkflowAsync`:
   ```csharp
   public async Task<WorkflowExecutionResult> ExecuteMarkdownWorkflowAsync(
       string markdownFilePath,
       WorkflowContext context)
   {
       // 1. Валидация входных параметров
       // 2. Конвертация markdown → WorkflowDefinition
       // 3. Выполнение через существующий ExecuteAsync
       // 4. Логирование и обработка ошибок
   }
   ```

3. Реализовать метод `ConvertMarkdownToWorkflowAsync`:
   ```csharp
   public async Task<WorkflowDefinition> ConvertMarkdownToWorkflowAsync(string markdownFilePath)
   {
       // 1. Валидация файла
       // 2. Парсинг markdown
       // 3. Конвертация в WorkflowDefinition
       // 4. Валидация результата
   }
   ```

4. Реализовать метод `ValidateMarkdownWorkflowAsync`:
   ```csharp
   public async Task<bool> ValidateMarkdownWorkflowAsync(string markdownFilePath)
   {
       // 1. Проверка существования файла
       // 2. Парсинг и валидация структуры
       // 3. Проверка связности шагов
       // 4. Возврат результата валидации
   }
   ```

5. Реализовать метод `GetMarkdownWorkflowFilesAsync`:
   ```csharp
   public async Task<IEnumerable<string>> GetMarkdownWorkflowFilesAsync(string directoryPath)
   {
       // 1. Поиск всех .md файлов
       // 2. Фильтрация файлов с workflow структурой
       // 3. Возврат списка валидных workflow файлов
   }
   ```

**Критерии приёмки**:
- Все методы корректно реализованы
- Используется dependency injection для IMarkdownWorkflowService
- Обработка ошибок и логирование добавлены
- Обратная совместимость с JSON workflow'ами сохранена

### Подзадача 05-03: Регистрация зависимостей в DI (15 минут)
**Файл**: `src/Orchestra.API/Program.cs` или `src/Orchestra.Core/ServiceCollectionExtensions.cs`

**Действия**:
1. Найти место регистрации IWorkflowEngine в DI контейнере
2. Убедиться, что IMarkdownWorkflowService зарегистрирован
3. Проверить регистрацию всех связанных сервисов:
   ```csharp
   services.AddScoped<IMarkdownWorkflowService, MarkdownWorkflowService>();
   services.AddScoped<IMarkdownWorkflowParser, MarkdownWorkflowParser>();
   services.AddScoped<IMarkdownToWorkflowConverter, MarkdownToWorkflowConverter>();
   ```

**Критерии приёмки**:
- Все новые сервисы зарегистрированы в DI
- Зависимости корректно разрешаются при создании WorkflowEngine
- Приложение запускается без ошибок DI

### Подзадача 05-04: Создание unit тестов (45 минут)
**Файл**: `src/Orchestra.Tests/Services/WorkflowEngineMarkdownTests.cs`

**Действия**:
1. Создать новый тестовый класс для markdown функциональности:
   ```csharp
   public class WorkflowEngineMarkdownTests
   {
       private readonly Mock<IMarkdownWorkflowService> _mockMarkdownService;
       private readonly WorkflowEngine _workflowEngine;
       // Настройка mocks и SUT
   }
   ```

2. Создать тесты для каждого нового метода:
   - `ExecuteMarkdownWorkflowAsync_ValidFile_ReturnsSuccessResult()`
   - `ExecuteMarkdownWorkflowAsync_InvalidFile_ThrowsException()`
   - `ConvertMarkdownToWorkflowAsync_ValidMarkdown_ReturnsWorkflowDefinition()`
   - `ValidateMarkdownWorkflowAsync_ValidFile_ReturnsTrue()`
   - `ValidateMarkdownWorkflowAsync_InvalidFile_ReturnsFalse()`
   - `GetMarkdownWorkflowFilesAsync_ValidDirectory_ReturnsWorkflowFiles()`

3. Создать интеграционный тест для проверки полного цикла:
   ```csharp
   [Test]
   public async Task FullCycle_MarkdownToExecution_Works()
   {
       // 1. Создать тестовый markdown файл
       // 2. Конвертировать в WorkflowDefinition
       // 3. Выполнить workflow
       // 4. Проверить результат
   }
   ```

**Критерии приёмки**:
- Все тесты проходят
- Покрытие новых методов >= 85%
- Интеграционный тест демонстрирует работоспособность полного цикла

### Подзадача 05-05: Обновление документации (30 минут)
**Файлы**:
- `src/Orchestra.Core/Services/WorkflowEngine.cs` (XML комментарии)
- `Docs/Architecture/Workflow-Engine-Markdown-Extension.md` (архитектурная документация)

**Действия**:
1. Добавить подробные XML комментарии для всех новых методов
2. Создать архитектурную документацию:
   ```markdown
   # Расширение WorkflowEngine для Markdown поддержки

   ## Обзор
   Документ описывает архитектурные изменения в WorkflowEngine для поддержки markdown workflow'ов

   ## Новые методы
   ### ExecuteMarkdownWorkflowAsync
   - Назначение: Выполнение workflow из markdown файла
   - Входные параметры: filePath, context
   - Возвращаемое значение: WorkflowExecutionResult
   - Зависимости: IMarkdownWorkflowService
   ```

3. Обновить README.md с примерами использования новых методов

**Критерии приёмки**:
- XML документация покрывает все новые методы
- Архитектурная документация создана
- Примеры использования добавлены в README

## Итоговые артефакты

### Изменённые файлы
1. `src/Orchestra.Core/Interfaces/IWorkflowEngine.cs` - расширен 4 новыми методами
2. `src/Orchestra.Core/Services/WorkflowEngine.cs` - реализация новых методов
3. `src/Orchestra.API/Program.cs` - регистрация DI зависимостей
4. `src/Orchestra.Tests/Services/WorkflowEngineMarkdownTests.cs` - новые тесты

### Новые файлы
1. `Docs/Architecture/Workflow-Engine-Markdown-Extension.md` - архитектурная документация

## Критерии готовности задачи
- [ ] Все подзадачи выполнены
- [ ] Интерфейс IWorkflowEngine расширен новыми методами
- [ ] Класс WorkflowEngine содержит полную реализацию новых методов
- [ ] DI зависимости корректно настроены
- [ ] Unit тесты покрывают новую функциональность >= 85%
- [ ] Интеграционный тест демонстрирует работоспособность
- [ ] Архитектурная документация создана
- [ ] Обратная совместимость с JSON workflow'ами сохранена на 100%
- [ ] Код скомпилируется и все тесты проходят

## Влияние на архитектуру
- **Расширение существующего интерфейса** - добавлены новые методы без изменения существующих
- **Сохранение обратной совместимости** - все существующие JSON workflow'ы продолжают работать
- **Использование Dependency Injection** - новые сервисы интегрируются через DI контейнер
- **Следование Command/Query паттерну** - новые методы следуют существующим конвенциям

## Интеграционные точки
- **IMarkdownWorkflowService** - основная зависимость для работы с markdown
- **ILogger<WorkflowEngine>** - логирование операций
- **Существующий ExecuteAsync** - переиспользование логики выполнения
- **WorkflowContext** - использование существующей модели контекста

## Риски и митигация
- **Производительность конвертации** → Кэширование результатов парсинга
- **Ошибки в markdown файлах** → Подробная валидация и понятные сообщения об ошибках
- **Совместимость с будущими версиями** → Использование интерфейсов вместо конкретных классов

---

**СТАТУС**: Готов к реализации
**СЛЕДУЮЩИЕ ДЕЙСТВИЯ**: Приступить к подзадаче 05-01 (расширение интерфейса IWorkflowEngine)