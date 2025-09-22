# Фаза 3: Web Dashboard Enhancement

**Родительский план**: [00-MARKDOWN_WORKFLOW_EXTENSION.md](../00-MARKDOWN_WORKFLOW_EXTENSION.md)

## Цель фазы
Создать простой веб-интерфейс для управления markdown workflow'ами, расширив существующий AgentCommunicationHub и проверив достаточность Hangfire Dashboard для мониторинга.

## Входные зависимости
- [ ] Фаза 1 завершена: markdown workflow'ы обрабатываются корректно
- [ ] Фаза 2 завершена: Claude Code агенты интегрированы с системой
- [ ] Существующий AgentCommunicationHub функционирует для SignalR коммуникации
- [ ] Hangfire Dashboard работает для мониторинга background jobs

## Задачи фазы

### Простые задачи (выполняются здесь)
- [ ] Оценить функциональность существующего Hangfire Dashboard (< 30 минут)
- [ ] Определить минимальные требования к веб-интерфейсу (< 30 минут)
- [ ] Создать базовые модели для веб-компонентов (< 30 минут)
- [ ] Настроить базовую структуру Blazor компонентов (< 30 минут)

### Сложные задачи (ссылки на дочерние файлы)
- [ ] [03-01-hangfire-assessment.md](./03-Web-Dashboard/03-01-hangfire-assessment.md) - Оценка достаточности Hangfire Dashboard
- [ ] [03-02-blazor-components.md](./03-Web-Dashboard/03-02-blazor-components.md) - Blazor компоненты для workflow управления
- [ ] [03-03-signalr-integration.md](./03-Web-Dashboard/03-03-signalr-integration.md) - Интеграция с существующим AgentCommunicationHub
- [ ] [03-04-workflow-dashboard.md](./03-Web-Dashboard/03-04-workflow-dashboard.md) - Dashboard для мониторинга markdown workflow'ов
- [ ] [03-05-workflow-forms.md](./03-Web-Dashboard/03-05-workflow-forms.md) - Формы создания/редактирования workflow'ов
- [ ] [03-06-agent-management-ui.md](./03-Web-Dashboard/03-06-agent-management-ui.md) - UI для управления агентами

## Выходные артефакты

### Новые веб-компоненты
- `WorkflowListComponent.razor` - список всех markdown workflow'ов
- `WorkflowExecutionComponent.razor` - мониторинг выполнения workflow'а
- `AgentStatusComponent.razor` - отображение статусов агентов
- `WorkflowCreateForm.razor` - форма создания нового workflow'а
- `WorkflowEditForm.razor` - форма редактирования существующего workflow'а

### Расширения существующих компонентов
- Расширенный `AgentCommunicationHub` с методами для workflow статусов
- Новые SignalR группы для workflow мониторинга
- Обновлённые модели в `Orchestra.Web.Models`

### API контроллеры
- `MarkdownWorkflowController` - REST API для workflow операций
- `AgentManagementController` - API для управления агентами
- Расширенный `OrchestratorController` с markdown workflow поддержкой

## Критерии готовности фазы
- [ ] Все простые задачи выполнены
- [ ] Все дочерние файлы завершены с детальными техническими спецификациями
- [ ] Hangfire Dashboard оценён на достаточность для базового мониторинга
- [ ] Blazor компоненты созданы и интегрированы с backend
- [ ] AgentCommunicationHub расширен для workflow статусов
- [ ] Веб-интерфейс позволяет создавать и запускать markdown workflow'ы
- [ ] Real-time статусы workflow'ов отображаются через SignalR
- [ ] Управление Claude Code агентами доступно через веб-интерфейс
- [ ] Unit тесты покрывают новые веб-компоненты >= 80%

## Влияние на следующие этапы
- **Фаза 4 (Enhanced Features)** может начаться после завершения задач 03-02 и 03-04 (основные компоненты и dashboard)
- **Production deployment** возможен после завершения этой фазы с базовой функциональностью
- **Advanced UI features** будут добавлены в Фазе 4

## Детали реализации

### Hangfire Dashboard оценка
```csharp
// Проверяем, достаточно ли Hangfire Dashboard для наших нужд:
// ✅ Мониторинг background jobs
// ✅ Статистика выполнения
// ✅ Failed jobs tracking
// ❓ Markdown workflow специфичная информация
// ❓ Real-time agent status
// ❓ Workflow linking visualization

// Вывод: Hangfire Dashboard покрывает ~70% потребностей,
// нужен дополнительный lightweight dashboard
```

### Blazor компонент структура
```razor
@* WorkflowListComponent.razor *@
<div class="workflow-list">
    <h3>Markdown Workflows</h3>

    @foreach (var workflow in workflows)
    {
        <div class="workflow-card">
            <h4>@workflow.Name</h4>
            <p>Status: <span class="status-@workflow.Status.ToString().ToLower()">@workflow.Status</span></p>
            <div class="actions">
                <button @onclick="() => ExecuteWorkflow(workflow.Id)">Execute</button>
                <button @onclick="() => EditWorkflow(workflow.Id)">Edit</button>
            </div>
        </div>
    }
</div>

@code {
    private List<MarkdownWorkflowInfo> workflows = new();

    protected override async Task OnInitializedAsync()
    {
        // TODO: Load workflows from API
        workflows = await WorkflowService.GetAllMarkdownWorkflowsAsync();
    }

    private async Task ExecuteWorkflow(string workflowId)
    {
        // TODO: Execute via API and update UI
        await WorkflowService.ExecuteWorkflowAsync(workflowId);
    }
}
```

### AgentCommunicationHub расширение
```csharp
public class AgentCommunicationHub : Hub
{
    // Existing methods remain unchanged

    // NEW: Workflow-specific methods
    public async Task JoinWorkflowGroup(string workflowId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"workflow_{workflowId}");
    }

    public async Task LeaveWorkflowGroup(string workflowId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"workflow_{workflowId}");
    }

    // Broadcast workflow status updates
    public async Task BroadcastWorkflowStatus(string workflowId, string status, string? details = null)
    {
        await Clients.Group($"workflow_{workflowId}").SendAsync("WorkflowStatusChanged", new
        {
            WorkflowId = workflowId,
            Status = status,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
    }

    // Broadcast agent assignment updates
    public async Task BroadcastAgentAssignment(string agentId, string workflowId, string status)
    {
        await Clients.All.SendAsync("AgentAssignmentChanged", new
        {
            AgentId = agentId,
            WorkflowId = workflowId,
            Status = status,
            Timestamp = DateTime.UtcNow
        });
    }
}
```

### API Controller для workflow операций
```csharp
[ApiController]
[Route("api/[controller]")]
public class MarkdownWorkflowController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<MarkdownWorkflowController> _logger;

    [HttpGet]
    public async Task<ActionResult<List<MarkdownWorkflowInfo>>> GetAllWorkflows()
    {
        // TODO: Implement via mediator command
        var query = new GetAllMarkdownWorkflowsQuery();
        var result = await _mediator.Send(query);
        return Ok(result.Workflows);
    }

    [HttpPost("{workflowId}/execute")]
    public async Task<ActionResult<WorkflowExecutionResult>> ExecuteWorkflow(string workflowId, [FromBody] ExecuteWorkflowRequest request)
    {
        // TODO: Implement via mediator command
        var command = new ExecuteMarkdownWorkflowCommand
        {
            WorkflowId = workflowId,
            AgentId = request.AgentId,
            Parameters = request.Parameters
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpGet("{workflowId}/status")]
    public async Task<ActionResult<WorkflowExecutionStatus>> GetWorkflowStatus(string workflowId)
    {
        // TODO: Implement via mediator query
        var query = new GetWorkflowStatusQuery { WorkflowId = workflowId };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
```

## Команды и хендлеры

### GetAllMarkdownWorkflowsQuery
```csharp
public class GetAllMarkdownWorkflowsQuery : IRequest<GetAllMarkdownWorkflowsResult>
{
    public bool IncludeInactive { get; set; } = false;
    public string? FilterByAgent { get; set; }
}

public class GetAllMarkdownWorkflowsQueryHandler : IRequestHandler<GetAllMarkdownWorkflowsQuery, GetAllMarkdownWorkflowsResult>
{
    private readonly IMarkdownWorkflowService _workflowService;

    public async Task<GetAllMarkdownWorkflowsResult> Handle(GetAllMarkdownWorkflowsQuery request, CancellationToken cancellationToken)
    {
        // TODO: Implement workflow discovery from markdown files
        // 1. Scan configured directories for .md files
        // 2. Parse and validate workflow format
        // 3. Return list with status information
        throw new NotImplementedException();
    }
}
```

### ExecuteMarkdownWorkflowCommand
```csharp
public class ExecuteMarkdownWorkflowCommand : IRequest<WorkflowExecutionResult>
{
    public string WorkflowId { get; set; }
    public string AgentId { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class ExecuteMarkdownWorkflowCommandHandler : IRequestHandler<ExecuteMarkdownWorkflowCommand, WorkflowExecutionResult>
{
    private readonly IMarkdownWorkflowService _workflowService;
    private readonly IClaudeCodeService _claudeCodeService;
    private readonly IHubContext<AgentCommunicationHub> _hubContext;

    public async Task<WorkflowExecutionResult> Handle(ExecuteMarkdownWorkflowCommand request, CancellationToken cancellationToken)
    {
        // TODO: Implement workflow execution
        // 1. Load and validate workflow
        // 2. Assign to specified agent
        // 3. Execute via appropriate service (Claude Code or generic)
        // 4. Broadcast status updates via SignalR
        throw new NotImplementedException();
    }
}
```

## UI/UX принципы

### Минималистичный дизайн
- Фокус на функциональности, не на визуальных эффектах
- Простая навигация между workflow'ами и агентами
- Real-time обновления статусов без перезагрузки страницы

### Responsive layout
- Работает на десктопе и планшетах
- Приоритет desktop experience для developer workflows
- Мобильная версия не критична для первой версии

### Интеграция с существующими инструментами
- Ссылки на Hangfire Dashboard для детального мониторинга
- Возможность открытия workflow файлов в редакторе
- Экспорт логов выполнения

## Риски и митигация

### Технические риски
- **SignalR connection stability** → Reconnection logic и graceful degradation
- **Blazor performance** → Lazy loading и virtualization для больших списков
- **API response times** → Async loading и progress indicators

### UX риски
- **Complexity overload** → Поэтапное раскрытие функциональности
- **Workflow file management** → Integration с file system для easy editing
- **Error presentation** → User-friendly error messages и recovery suggestions

---

**СТАТУС**: Готов к детальной декомпозиции в дочерних файлах
**СЛЕДУЮЩИЕ ДЕЙСТВИЯ**: Создать дочерние файлы с микро-декомпозицией каждой сложной задачи (≤30 минут на задачу)