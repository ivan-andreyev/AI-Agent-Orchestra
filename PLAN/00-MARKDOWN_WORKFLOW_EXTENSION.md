# План расширения AI Agent Orchestra для поддержки Markdown Workflow

**Версия**: 1.0
**Дата создания**: 2025-09-21
**Автор**: work-plan-architect agent
**Статус**: В разработке

## Обзор проекта

### Цель
Расширение существующей архитектуры AI Agent Orchestra для поддержки системы перелинкованных markdown-документов как источника workflow-определений, при сохранении всех существующих компонентов.

### Ключевая стратегия: РАСШИРЕНИЕ СУЩЕСТВУЮЩЕГО
**КРИТИЧНО**: Проект фокусируется на расширении готовых компонентов, а НЕ на создании новых систем.

### Существующие компоненты (ГОТОВЫ к использованию)
- ✅ **Hangfire Dashboard** - мониторинг background jobs
- ✅ **AgentCommunicationHub** - SignalR коммуникация в реальном времени
- ✅ **IWorkflowEngine + WorkflowEngine** - система выполнения workflow'ов
- ✅ **AgentScheduler** - cron пинги агентов каждые 30 секунд
- ✅ **BackgroundTaskAssignmentService** - назначение задач агентам
- ✅ **SimpleOrchestrator** - базовая оркестрация агентов

### Ключевая лакуна: Markdown Integration
**Проблема**: Текущая система использует JSON WorkflowDefinition, но пользователь требует "систему перелинкованных markdown-документов"

**Решение**: Создать преобразователь markdown → JSON WorkflowDefinition для интеграции с существующим WorkflowEngine

## Фазы разработки

### 📋 [Фаза 1: Markdown Integration](./00-MARKDOWN_WORKFLOW_EXTENSION/01-Markdown-Integration.md)
**Приоритет**: КРИТИЧЕСКИЙ
**Зависимости**: Нет
**Цель**: Расширить WorkflowEngine поддержкой markdown-файлов
**Статус завершения**: 33% - Модели данных и парсер завершены

- [x] Модели данных для markdown workflow (01-01-markdown-models.md) ✅ COMPLETE
- [x] Парсер markdown документов (01-02-markdown-parser.md) ✅ COMPLETE
- [ ] Markdown → JSON конвертер с поддержкой перелинковки
- [ ] Файловый watcher для автоматического обновления workflow'ов
- [ ] Расширение IWorkflowEngine интерфейса методами для markdown
- [ ] Интеграция с существующим WorkflowEngine

### 📋 [Фаза 2: Claude Code Integration](./00-MARKDOWN_WORKFLOW_EXTENSION/02-Claude-Code-Integration.md) ✅ В ОСНОВНОМ ЗАВЕРШЕНО
**Приоритет**: ВЫСОКИЙ
**Зависимости**: Фаза 1 завершена
**Цель**: Добавить реальную интеграцию с Claude Code агентами
**Статус завершения**: 85% - Основные компоненты реализованы и протестированы

- [x] Расширение IAgentConnector для специфики Claude Code ✅ COMPLETE
- [x] ClaudeCodeAgentConnector с реальными командами ✅ COMPLETE
- [x] Интеграция с существующим AgentScheduler ✅ COMPLETE
- [x] Обновление SimpleOrchestrator для работы с Claude Code ✅ COMPLETE
- [x] Тестирование реального взаимодействия с агентами ✅ COMPLETE

### 📋 [Фаза 3: Web Dashboard Enhancement](./00-MARKDOWN_WORKFLOW_EXTENSION/03-Web-Dashboard.md)
**Приоритет**: СРЕДНИЙ
**Зависимости**: Фаза 1-2 завершены
**Цель**: Простой веб-интерфейс над существующими сервисами

- [ ] Blazor компоненты для управления workflow'ами
- [ ] Интеграция с существующим AgentCommunicationHub
- [ ] Dashboard для мониторинга markdown workflow'ов
- [ ] Простые формы создания/редактирования workflow'ов
- [ ] Проверка достаточности Hangfire Dashboard для мониторинга

### 📋 [Фаза 4: Enhanced Features](./00-MARKDOWN_WORKFLOW_EXTENSION/04-Enhanced-Features.md)
**Приоритет**: НИЗКИЙ
**Зависимости**: Фазы 1-3 завершены
**Цель**: Дополнительные возможности для продвинутого использования

- [ ] Goal tracking в markdown формате
- [ ] Workflow linking между markdown документами
- [ ] Расширенная аналитика выполнения workflow'ов
- [ ] Шаблоны часто используемых workflow'ов
- [ ] Автоматическая генерация отчётов

## Технические принципы

### Архитектурные правила
- **Mediator Pattern**: Все новые операции через IGameMediator
- **Command/Query Separation**: Новые команды следуют паттерну {Action}{Entity}Command
- **Framework-First**: Расширения как plugin'ы к существующему фреймворку
- **Обратная совместимость**: JSON workflow'ы продолжают работать

### Именование новых компонентов
- **Команды**: `ProcessMarkdownWorkflowCommand`, `ConvertMarkdownToWorkflowCommand`
- **Хендлеры**: `ProcessMarkdownWorkflowCommandHandler`, `ConvertMarkdownToWorkflowCommandHandler`
- **События**: `MarkdownWorkflowProcessedEvent`, `WorkflowConvertedEvent`
- **Сервисы**: `IMarkdownWorkflowService`, `IMarkdownConverter`

### Интеграция с существующими сервисами
```csharp
// Расширение существующего интерфейса
public interface IWorkflowEngine
{
    // Существующие методы остаются без изменений
    Task<WorkflowExecutionResult> ExecuteAsync(WorkflowDefinition workflow, WorkflowContext context);

    // НОВЫЕ методы для markdown
    Task<WorkflowExecutionResult> ExecuteMarkdownWorkflowAsync(string markdownFilePath, WorkflowContext context);
    Task<WorkflowDefinition> ConvertMarkdownToWorkflowAsync(string markdownFilePath);
    Task<bool> ValidateMarkdownWorkflowAsync(string markdownFilePath);
}
```

## Критерии успеха

### Функциональные требования
- [ ] Markdown-файлы автоматически преобразуются в WorkflowDefinition
- [ ] Существующий агентский scheduler работает с markdown workflow'ами
- [ ] SignalR Hub передаёт статусы выполнения markdown workflow'ов
- [ ] Перелинковка между markdown документами работает корректно
- [ ] Claude Code агенты получают команды через существующую инфраструктуру

### Технические требования
- [ ] Обратная совместимость с JSON workflow'ами на 100%
- [ ] Производительность преобразования markdown → JSON < 100ms для файлов до 1MB
- [ ] Файловый watcher обновляет workflow'ы в течение 5 секунд после изменения
- [ ] Все новые компоненты покрыты unit-тестами >= 85%
- [ ] Интеграционные тесты с существующими сервисами

### Пользовательские требования
- [ ] Простота создания workflow'ов в markdown формате
- [ ] Понятная система перелинковки документов
- [ ] Мониторинг выполнения через существующий Hangfire Dashboard
- [ ] Управление агентами из единого интерфейса

## Этапы валидации

### После каждой фазы
1. **Функциональное тестирование** - все новые функции работают изолированно
2. **Интеграционное тестирование** - интеграция с существующими компонентами
3. **Обратная совместимость** - старые JSON workflow'ы продолжают работать
4. **Производительность** - новые компоненты не замедляют существующую систему

### Финальная валидация
1. **End-to-End тестирование** - полный цикл от markdown до выполнения агентом
2. **Нагрузочное тестирование** - система выдерживает типичную нагрузку
3. **Пользовательское тестирование** - интерфейс интуитивно понятен
4. **Документация** - все новые компоненты задокументированы

## Зависимости и ограничения

### Внешние зависимости
- **MarkDig library** - для парсинга markdown файлов
- **FileSystemWatcher** - для отслеживания изменений файлов
- **Claude Code CLI** - для реального взаимодействия с агентами

### Технические ограничения
- Markdown файлы должны следовать определённой структуре для корректного парсинга
- Максимальный размер markdown workflow: 10MB
- Максимальная глубина вложенности в перелинковке: 5 уровней
- Поддержка только UTF-8 кодировки

### Ресурсные ограничения
- Разработка должна использовать существующую архитектуру максимально
- Минимальные изменения в работающих компонентах
- Новые зависимости добавляются только при крайней необходимости

---

**СЛЕДУЮЩИЕ ДЕЙСТВИЯ**:
1. Создать детальную декомпозицию Фазы 1 в `./00-MARKDOWN_WORKFLOW_EXTENSION/01-Markdown-Integration.md`
2. Invoke work-plan-reviewer для валидации структуры плана
3. После одобрения ревьюером - начать реализацию с Фазы 1

**АРХИТЕКТУРНАЯ ДОКУМЕНТАЦИЯ**: Для архитектурных компонентов этого плана необходимо создать соответствующую архитектурную документацию в `Docs/Architecture/Planned/` с помощью architecture-documenter агента.