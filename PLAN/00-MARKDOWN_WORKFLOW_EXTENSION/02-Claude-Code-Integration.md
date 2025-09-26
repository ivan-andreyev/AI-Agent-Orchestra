# Фаза 2: Claude Code Integration

**Родительский план**: [00-MARKDOWN_WORKFLOW_EXTENSION.md](../00-MARKDOWN_WORKFLOW_EXTENSION.md)

## Цель фазы
Добавить реальную интеграцию с Claude Code агентами, расширив существующие AgentConnector и SimpleOrchestrator компоненты для работы с реальными Claude Code командами.

## Входные зависимости
- [ ] Фаза 1 завершена: markdown workflow'ы конвертируются в JSON WorkflowDefinition
- [ ] IWorkflowEngine.ExecuteMarkdownWorkflowAsync() работает корректно
- [ ] Существующий AgentScheduler функционирует с базовыми агентами
- [ ] Существующий SimpleOrchestrator готов к расширению

## Задачи фазы

### Простые задачи (выполняются здесь)
- [ ] Исследовать Claude Code CLI команды и их параметры (< 30 минут)
- [ ] Определить структуру команд для реального взаимодействия (< 30 минут)
- [ ] Создать константы для Claude Code специфичных команд (< 30 минут)
- [ ] Добавить конфигурационные настройки для Claude Code агентов (< 30 минут)

### Сложные задачи (ссылки на дочерние файлы)
- [x] [02-01-claude-code-connector.md](./02-Claude-Code-Integration/02-01-claude-code-connector.md) - ClaudeCodeAgentConnector для реального взаимодействия ✅ COMPLETE
- [ ] [02-02-agent-connector-extension.md](./02-Claude-Code-Integration/02-02-agent-connector-extension.md) - Расширение IAgentConnector интерфейса
- [ ] [02-03-orchestrator-enhancement.md](./02-Claude-Code-Integration/02-03-orchestrator-enhancement.md) - Обновление SimpleOrchestrator для Claude Code
- [ ] [02-04-command-mapping.md](./02-Claude-Code-Integration/02-04-command-mapping.md) - Mapping markdown команд к Claude Code CLI
- [ ] [02-05-agent-scheduler-integration.md](./02-Claude-Code-Integration/02-05-agent-scheduler-integration.md) - Интеграция с существующим AgentScheduler
- [ ] [02-06-real-agent-testing.md](./02-Claude-Code-Integration/02-06-real-agent-testing.md) - Тестирование с реальными Claude Code агентами
- [x] [02-07-chat-integration.md](./02-Claude-Code-Integration/02-07-chat-integration.md) - Исправление координаторского чата в Blazor UI ✅ COMPLETE
- [ ] [02-08-context-management.md](./02-Claude-Code-Integration/02-08-context-management.md) - Единый контекст между инстансами координатора

## Выходные артефакты

### Новые классы и интерфейсы
- `ClaudeCodeAgentConnector` - основной коннектор для Claude Code агентов
- `IClaudeCodeService` - сервис для взаимодействия с Claude Code CLI
- `ClaudeCodeCommandMapper` - mapping workflow команд к CLI командам
- `ClaudeCodeConfiguration` - конфигурация Claude Code агентов
- `ExecuteClaudeCodeCommandCommand` - команда для выполнения Claude Code операций

### Расширения существующих компонентов
- Расширенный `IAgentConnector` с методами для Claude Code
- Обновлённый `SimpleOrchestrator` с поддержкой Claude Code агентов
- Улучшенный `AgentScheduler` с Claude Code специфичной логикой
- Новые настройки в `AgentConfiguration`

### Интеграционные компоненты
- `ClaudeCodeAgentFactory` - фабрика для создания Claude Code агентов
- `ClaudeCodeWorkflowExecutor` - выполнитель workflow'ов для Claude Code
- `ClaudeCodeStatusMonitor` - мониторинг статуса Claude Code агентов

## Критерии готовности фазы
- [ ] Все простые задачи выполнены
- [ ] Все дочерние файлы завершены с детальными техническими спецификациями
- [ ] ClaudeCodeAgentConnector реализован и протестирован
- [ ] Существующий AgentScheduler корректно работает с Claude Code агентами
- [ ] SimpleOrchestrator может назначать markdown workflow'ы Claude Code агентам
- [ ] Реальное взаимодействие с Claude Code CLI работает
- [ ] Статусы агентов обновляются в реальном времени через SignalR
- [x] **✅ ЗАВЕРШЕНО**: Координаторский чат работает в Blazor UI (02-07) ✅ COMPLETE - Дата завершения: 2025-09-26
- [x] **✅ В ОСНОВНОМ ЗАВЕРШЕНО**: Единый контекст между инстансами координатора реализован (02-08) - базовая персистентность готова
- [ ] Unit тесты покрывают новую функциональность >= 85%
- [ ] Интеграционные тесты с реальными агентами проходят

## Влияние на следующие этапы
- **Фаза 3 (Web Dashboard)** может начаться после завершения задач 02-01 и 02-03 (connector и orchestrator)
- **Фаза 4 (Enhanced Features)** зависит от задачи 02-06 (тестирование с реальными агентами) для advanced функциональности
- **Полная система** будет готова к production использованию после завершения этой фазы

## Детали реализации

### Claude Code Command Mapping
```markdown
# Markdown Workflow Command
### Build Project
- **Type**: Task
- **Command**: dotnet build
- **Parameters**:
  - path: {{projectPath}}
  - configuration: Release

# Translated to Claude Code CLI
claude-code execute --command "dotnet build {{projectPath}} --configuration Release" --timeout 300
```

### AgentConnector расширение
```csharp
public interface IAgentConnector
{
    // Существующие методы остаются
    Task<bool> PingAsync(string agentId);
    Task<TaskResult> ExecuteTaskAsync(string agentId, AgentTask task);

    // НОВЫЕ методы для Claude Code
    Task<bool> IsClaudeCodeAgentAsync(string agentId);
    Task<ClaudeCodeExecutionResult> ExecuteClaudeCodeCommandAsync(string agentId, string command, Dictionary<string, object> parameters);
    Task<string> GetClaudeCodeVersionAsync(string agentId);
}
```

### SimpleOrchestrator интеграция
```csharp
public class SimpleOrchestrator
{
    // Новый метод для Claude Code workflow'ов
    public async Task<string> ExecuteMarkdownWorkflowAsync(string markdownFilePath, string targetAgentId)
    {
        // 1. Конвертировать markdown → JSON workflow через Фазу 1
        var workflow = await _markdownService.ConvertToWorkflowAsync(markdownFilePath);

        // 2. Проверить, что агент - Claude Code
        var isClaudeCode = await _agentConnector.IsClaudeCodeAgentAsync(targetAgentId);

        // 3. Выполнить через Claude Code connector
        if (isClaudeCode)
        {
            return await _claudeCodeConnector.ExecuteWorkflowAsync(targetAgentId, workflow);
        }

        // 4. Fallback к обычному выполнению
        return await _workflowEngine.ExecuteAsync(workflow, context);
    }
}
```

### AgentScheduler расширение
```csharp
public class AgentScheduler
{
    private async Task PingAgent(ConfiguredAgent agent)
    {
        try
        {
            // Existing logic remains
            var status = await CheckAgentStatus(agent);

            // NEW: Claude Code specific ping
            if (agent.Type == "claude-code")
            {
                status = await _claudeCodeConnector.PingClaudeCodeAgentAsync(agent.Id);
            }

            _orchestrator.UpdateAgentStatus(agent.Id, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ping agent: {AgentName}", agent.Name);
            _orchestrator.UpdateAgentStatus(agent.Id, AgentStatus.Error);
        }
    }
}
```

## Команды и хендлеры

### ExecuteClaudeCodeCommandCommand
```csharp
public class ExecuteClaudeCodeCommandCommand : IRequest<ClaudeCodeExecutionResult>
{
    public string AgentId { get; set; }
    public string Command { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public string WorkflowId { get; set; }
}

public class ExecuteClaudeCodeCommandCommandHandler : IRequestHandler<ExecuteClaudeCodeCommandCommand, ClaudeCodeExecutionResult>
{
    private readonly IClaudeCodeService _claudeCodeService;
    private readonly ILogger<ExecuteClaudeCodeCommandCommandHandler> _logger;

    public async Task<ClaudeCodeExecutionResult> Handle(ExecuteClaudeCodeCommandCommand request, CancellationToken cancellationToken)
    {
        // TODO: Implement Claude Code command execution logic
        // 1. Validate agent is available
        // 2. Map command to CLI parameters
        // 3. Execute via Claude Code CLI
        // 4. Parse and return results
        throw new NotImplementedException();
    }
}
```

## Риски и митигация

### Технические риски
- **Claude Code CLI нестабильность** → Retry логика и graceful fallback
- **Timeout управление** → Configurable timeouts для разных типов команд
- **Процесс management** → Proper cleanup of Claude Code processes

### Интеграционные риски
- **Конфликты с существующим AgentScheduler** → Тщательное тестирование расширений
- **Performance impact** → Async операции и proper resource management
- **Error handling** → Comprehensive exception handling и logging

---

**СТАТУС**: Готов к детальной декомпозиции в дочерних файлах
**СЛЕДУЮЩИЕ ДЕЙСТВИЯ**: Создать дочерние файлы с микро-декомпозицией каждой сложной задачи (≤30 минут на задачу)