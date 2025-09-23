# Agent Coordination System Plan

**Project**: AI Agent Orchestra
**Focus**: Unified Claude Code Agent Management + Coordinating Agent
**Architecture**: Blazor + ASP.NET Core + Markdown-based workflows
**Date**: 2025-09-21

## Core Requirements

### 1. Unified Agent Management Hub
- **Single control point** для управления всеми Claude Code агентами
- **Web UI** на Blazor для мониторинга и управления
- **Real-time status** всех агентов в одном интерфейсе
- **Task assignment** из центрального места

### 2. Coordinating Agent (Cron-based)
- **Autonomous coordinator** который пингует агентов по расписанию
- **Goal tracking** - не дает агентам останавливаться до завершения задач
- **Progress monitoring** - отслеживает прогресс каждого агента
- **Alert system** - уведомления о зависших/остановившихся агентах

### 3. Markdown-based Workflow System
- **No React/Canvas/Graphics** - только markdown документы
- **Workflow = связанные .md файлы** в репозитории
- **Tasks = markdown документы** с метаданными
- **Links between documents** для создания workflow chains
- **Git-based storage** - все workflows как part of codebase

## Architecture Overview

### Technology Stack
- **Backend**: ASP.NET Core 9.0 + Entity Framework
- **Frontend**: Blazor Server (existing)
- **Storage**: SQLite/PostgreSQL + Git repositories
- **Scheduling**: Hangfire (existing) + Cron jobs
- **Communication**: SignalR (existing)

### Component Structure
```
/Agent-Coordination/
├── Core/
│   ├── AgentCoordinator.cs        # Main coordinating logic
│   ├── MarkdownWorkflowEngine.cs  # Processes .md workflows
│   └── GoalTracker.cs             # Tracks agent goals
├── Services/
│   ├── AgentMonitoringService.cs  # Real-time agent monitoring
│   ├── CronCoordinatorService.cs  # Scheduled agent coordination
│   └── MarkdownParserService.cs   # Parses workflow .md files
├── Models/
│   ├── AgentGoal.cs              # Agent goal tracking
│   ├── WorkflowTask.cs           # Markdown-based task
│   └── CoordinatorStatus.cs     # Coordinator state
└── Components/
    ├── AgentDashboard.razor      # Unified agent control
    ├── GoalTracker.razor         # Goal progress display
    └── WorkflowViewer.razor      # Markdown workflow display
```

### Workflow Directory Structure
```
/workflows/
├── agent-goals/
│   ├── agent-001-setup-goal.md
│   ├── agent-002-build-goal.md
│   └── agent-003-test-goal.md
├── coordination-flows/
│   ├── daily-coordination.md
│   ├── goal-tracking-flow.md
│   └── alert-escalation.md
└── templates/
    ├── agent-goal-template.md
    └── coordination-template.md
```

## Implementation Plan

### Phase 1: Core Agent Coordination (1-2 days)
1. **AgentCoordinator** - центральная логика координации
2. **AgentMonitoringService** - мониторинг статусов агентов
3. **Basic Blazor dashboard** - простой UI для управления
4. **Goal tracking model** - базовая модель для отслеживания целей

### Phase 2: Cron-based Coordination (1 day)
1. **CronCoordinatorService** - scheduled pings агентов
2. **Hangfire integration** - использование существующего Hangfire
3. **Alert system** - уведомления о проблемных агентах
4. **Progress monitoring** - отслеживание прогресса к целям

### Phase 3: Markdown Workflow Integration (1-2 days)
1. **MarkdownWorkflowEngine** - обработка .md workflows
2. **MarkdownParserService** - парсинг markdown документов
3. **WorkflowViewer component** - отображение workflows
4. **Git integration** - работа с workflow файлами в git

### Phase 4: Enhanced UI & Features (1 day)
1. **Enhanced dashboard** - расширенный UI
2. **Real-time updates** - SignalR для live updates
3. **Manual interventions** - ручное управление агентами
4. **Reporting** - отчеты о работе агентов

## Technical Implementation

### 1. AgentCoordinator Core Logic
```csharp
public class AgentCoordinator
{
    public async Task<CoordinationResult> CoordinateAgents()
    {
        var agents = await GetActiveAgents();
        var results = new List<AgentResult>();

        foreach (var agent in agents)
        {
            if (!agent.IsWorkingTowardsGoal())
            {
                await PingAgent(agent);
                await CheckGoalProgress(agent);
            }
        }

        return new CoordinationResult(results);
    }

    private async Task PingAgent(AgentInfo agent)
    {
        // Ping logic to wake up dormant agents
    }

    private async Task CheckGoalProgress(AgentInfo agent)
    {
        // Check if agent is making progress towards goal
    }
}
```

### 2. Markdown Workflow Structure
```markdown
# Agent Goal: Setup Development Environment

**Agent**: agent-001
**Status**: In Progress
**Started**: 2025-09-21T10:00:00Z
**Target Completion**: 2025-09-21T12:00:00Z

## Goal Description
Setup complete development environment for the project.

## Success Criteria
- [ ] Repository cloned
- [ ] Dependencies installed
- [ ] Build successful
- [ ] Tests passing

## Next Steps
After completion: [Build Application](../agent-goals/agent-002-build-goal.md)

## Coordinator Notes
Last ping: 2025-09-21T11:30:00Z
Progress: 75% complete
```

### 3. Cron-based Coordination
```csharp
public class CronCoordinatorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _coordinator.CoordinateAgents();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

### 4. Blazor Agent Dashboard
```razor
@page "/agent-dashboard"

<h1>🎼 Agent Coordination Dashboard</h1>

<div class="agent-grid">
    @foreach (var agent in _agents)
    {
        <div class="agent-card @GetStatusClass(agent)">
            <h3>@agent.Name</h3>
            <div class="status">Status: @agent.Status</div>
            <div class="goal">Goal: @agent.CurrentGoal</div>
            <div class="progress">Progress: @agent.GoalProgress%</div>
            <button @onclick="() => PingAgent(agent)">Ping Agent</button>
        </div>
    }
</div>
```

## Benefits of This Approach

### ✅ Advantages
1. **No external dependencies** - pure Blazor + ASP.NET
2. **Git-native workflows** - все workflows в репозитории
3. **Simple and maintainable** - никаких сложных фреймворков
4. **Unified control** - все агенты в одном месте
5. **Autonomous coordination** - coordinator работает без участия человека
6. **Extensible** - легко добавлять новых агентов и workflows

### ✅ Fits Existing Architecture
- Uses existing **Blazor Server** setup
- Leverages existing **Hangfire** for scheduling
- Builds on existing **SignalR** for real-time updates
- Integrates with existing **Agent discovery** system

## Migration from React Plan

### What Gets Removed
- ❌ All React/npm/webpack dependencies (already done)
- ❌ JavaScript-heavy workflow builder
- ❌ Complex visual components
- ❌ Node.js build pipeline

### What Gets Added
- ✅ Simple Blazor components
- ✅ Markdown-based workflows
- ✅ Cron-based coordination
- ✅ Agent goal tracking
- ✅ Unified dashboard

## Success Metrics

1. **Agent Management**: All Claude Code agents visible and controllable from single dashboard
2. **Autonomous Coordination**: Coordinator successfully pings and monitors agents without human intervention
3. **Goal Tracking**: System tracks agent progress towards goals and alerts on stalls
4. **Workflow Integration**: Markdown workflows define and link agent tasks
5. **Performance**: Coordination overhead <1% of system resources

## Timeline: 4-6 days total

This replaces the inappropriate React-based Phase 3B plan with a proper Blazor + Markdown solution that fits the actual project requirements and architecture.