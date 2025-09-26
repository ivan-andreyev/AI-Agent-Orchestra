# Фаза 1: Markdown Integration

**Родительский план**: [00-MARKDOWN_WORKFLOW_EXTENSION.md](../00-MARKDOWN_WORKFLOW_EXTENSION.md)

## Цель фазы
Расширить существующий WorkflowEngine поддержкой markdown-файлов как источника workflow-определений с сохранением полной обратной совместимости с JSON workflow'ами.

## Входные зависимости
- [ ] Существующий IWorkflowEngine и WorkflowEngine функционируют корректно
- [ ] Проект Orchestra.Core доступен для расширения
- [ ] NuGet пакет MarkDig доступен для установки

## Задачи фазы

### Простые задачи (выполняются здесь)
- [x] Установить NuGet пакет MarkDig в Orchestra.Core проект (< 30 минут) ✅ COMPLETE
- [x] Создать базовые модели для markdown workflow (< 30 минут) ✅ COMPLETE
- [x] Добавить константы и enums для markdown обработки (< 30 минут) ✅ COMPLETE
- [x] Создать unit тесты для основных компонентов (< 1 часа) ✅ COMPLETE

### Сложные задачи (ссылки на дочерние файлы)
- [x] [01-01-markdown-models.md](./01-Markdown-Integration/01-01-markdown-models.md) - Модели данных для markdown workflow ✅ COMPLETE
- [x] [01-02-markdown-parser.md](./01-Markdown-Integration/01-02-markdown-parser.md) - Парсер markdown документов ✅ COMPLETE
- [x] [01-03-workflow-converter.md](./01-Markdown-Integration/01-03-workflow-converter.md) - Конвертер markdown → JSON WorkflowDefinition ✅ COMPLETE
- [x] [01-04-file-watcher.md](./01-Markdown-Integration/01-04-file-watcher.md) - Отслеживание изменений markdown файлов ✅ COMPLETE
- [ ] [01-05-workflow-engine-extension.md](./01-Markdown-Integration/01-05-workflow-engine-extension.md) - Расширение существующего IWorkflowEngine
- [ ] [01-06-mediator-commands.md](./01-Markdown-Integration/01-06-mediator-commands.md) - Command/Handler паттерн для markdown операций

## Выходные артефакты

### Новые классы и интерфейсы
- `IMarkdownWorkflowService` - основной сервис для работы с markdown workflow'ами
- `MarkdownWorkflowParser` - парсер markdown документов
- `MarkdownToWorkflowConverter` - конвертер в JSON формат
- `MarkdownFileWatcher` - мониторинг изменений файлов
- `ProcessMarkdownWorkflowCommand` - команда для обработки markdown workflow
- `ProcessMarkdownWorkflowCommandHandler` - хендлер команды

### Расширения существующих компонентов
- Расширенный `IWorkflowEngine` с методами для markdown
- Обновлённый `WorkflowEngine` с поддержкой markdown источников
- Новые модели в `Orchestra.Core.Models.Workflow` namespace

### Тесты
- `MarkdownWorkflowParserTests` - тесты парсера
- `MarkdownToWorkflowConverterTests` - тесты конвертера
- `ProcessMarkdownWorkflowCommandTests` - тесты команды
- Интеграционные тесты с существующим WorkflowEngine

## Критерии готовности фазы
- [ ] Все простые задачи выполнены
- [ ] Все дочерние файлы завершены с детальными техническими спецификациями
- [ ] MarkDig успешно интегрирован в проект
- [ ] Базовые модели данных созданы и протестированы
- [ ] Unit тесты покрывают новую функциональность >= 85%
- [ ] Интеграционные тесты с существующим WorkflowEngine проходят
- [ ] Обратная совместимость с JSON workflow'ами сохранена на 100%
- [ ] Производительность конвертации markdown → JSON < 100ms для файлов до 1MB

## Влияние на следующие этапы
- **Фаза 2 (Claude Code Integration)** может начаться после завершения задач 01-03 и 01-05 (конвертер и расширение WorkflowEngine)
- **Фаза 3 (Web Dashboard)** зависит от полного завершения этой фазы для корректного отображения markdown workflow'ов
- **Фаза 4 (Enhanced Features)** требует всех компонентов этой фазы для advanced функциональности

## Детали реализации

### Структура markdown workflow файлов
```markdown
# Workflow: Название workflow

## Metadata
- Author: Автор
- Version: 1.0
- Tags: tag1, tag2

## Variables
- **projectPath** (string, required): Путь к проекту
- **buildConfig** (string, default: "Release"): Конфигурация сборки

## Steps

### 1. Build Project
- **Type**: Task
- **Command**: dotnet build
- **Parameters**:
  - path: {{projectPath}}
  - configuration: {{buildConfig}}

### 2. Run Tests
- **Type**: Task
- **Command**: dotnet test
- **DependsOn**: 1
- **Parameters**:
  - path: {{projectPath}}
```

### Ключевые интеграционные точки
1. **IWorkflowEngine.ExecuteMarkdownWorkflowAsync()** - новый метод в существующем интерфейсе
2. **MarkdownWorkflowParser.ParseAsync()** - основной метод парсинга
3. **MarkdownToWorkflowConverter.ConvertAsync()** - преобразование в JSON
4. **MarkdownFileWatcher.StartWatching()** - мониторинг изменений

### Command/Handler интеграция
```csharp
// Команда для обработки markdown workflow
public class ProcessMarkdownWorkflowCommand : IRequest<ProcessMarkdownWorkflowResult>
{
    public string MarkdownFilePath { get; set; }
    public WorkflowContext Context { get; set; }
    public bool ValidateOnly { get; set; } = false;
}

// Хендлер команды
public class ProcessMarkdownWorkflowCommandHandler : IRequestHandler<ProcessMarkdownWorkflowCommand, ProcessMarkdownWorkflowResult>
{
    private readonly IMarkdownWorkflowService _markdownService;
    private readonly IWorkflowEngine _workflowEngine;

    // Реализация Handle метода
}
```

## Риски и митигация

### Технические риски
- **Производительность парсинга** → Кэширование результатов конвертации
- **Совместимость с MarkDig** → Фиксация версии пакета в проекте
- **Сложность markdown структуры** → Строгая валидация формата

### Интеграционные риски
- **Конфликты с существующим WorkflowEngine** → Тщательное тестирование обратной совместимости
- **Изменения в Command/Handler паттерне** → Следование существующим конвенциям проекта

---

**СТАТУС**: Готов к детальной декомпозиции в дочерних файлах
**СЛЕДУЮЩИЕ ДЕЙСТВИЯ**: Создать дочерние файлы с микро-декомпозицией каждой сложной задачи (≤30 минут на задачу)