# AI Agent Orchestra MVP - Quick Start

## Что это

Простой MVP для управления множественными AI агентами из одного места. Оркестратор с собственным "мозгом", который пингуется автоматически.

## Архитектура

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   CLI           │    │   API/Web        │    │   Scheduler     │
│   orchestra.exe │───▶│   localhost:5002 │◀───│   Background    │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                              │
                              ▼
                       ┌──────────────────┐
                       │   Orchestrator   │
                       │   + Agent State  │
                       └──────────────────┘
                              │
                              ▼
                       ┌──────────────────┐
                       │   JSON Storage   │
                       │   orchestrator-  │
                       │   state.json     │
                       └──────────────────┘
```

## Быстрый запуск

### 1. Запустить оркестратор

```bash
cd src/Orchestra.API
dotnet run
```

Оркестратор запустится на `http://localhost:5002` и автоматически:
- Создаст файл конфигурации `agent-config.json`
- Запустит планировщик для автоматического пинга агентов каждые 30 секунд
- Начнет сохранять состояние в `orchestrator-state.json`

### 2. Проверить статус

```bash
cd src/Orchestra.CLI
dotnet run status
```

### 3. Настроить агентов

Отредактировать `agent-config.json`:

```json
{
  "PingIntervalSeconds": 30,
  "Agents": [
    {
      "Id": "claude-1",
      "Name": "Claude Agent 1",
      "Type": "claude-code",
      "RepositoryPath": "C:\\Users\\mrred\\RiderProjects\\Galactic-Idlers",
      "Enabled": true
    },
    {
      "Id": "claude-2",
      "Name": "Claude Agent 2",
      "Type": "claude-code",
      "RepositoryPath": "C:\\Users\\mrred\\RiderProjects\\AI-Agent-Orchestra",
      "Enabled": true
    }
  ]
}
```

### 4. Добавить задачу

```bash
cd src/Orchestra.CLI
dotnet run queue add "Run tests" "C:\\MyProject" Normal
```

## API Endpoints

- `GET /state` - Состояние оркестратора
- `GET /agents` - Список агентов
- `POST /agents/register` - Регистрация агента
- `POST /agents/{id}/ping` - Пинг агента
- `POST /tasks/queue` - Добавить задачу
- `GET /agents/{id}/next-task` - Получить следующую задачу

## CLI Commands

```bash
# Статус системы
dotnet run status

# Список агентов
dotnet run agents

# Очередь задач
dotnet run queue

# Добавить задачу
dotnet run queue add "command" "repo-path" [priority]

# Проверить доступность
dotnet run ping

# Показать конфигурацию
dotnet run config
```

## Файлы состояния

- `agent-config.json` - Конфигурация агентов
- `orchestrator-state.json` - Текущее состояние системы (автосохранение)

## Автоматизация

Планировщик (`AgentScheduler`) автоматически:
- Регистрирует агентов при запуске
- Пингует агентов каждые N секунд
- Проверяет доступность репозиториев
- Обрабатывает очередь задач
- Логирует все операции

## Что дальше

1. **Интеграция с Claude Code**: Добавить реальное взаимодействие с терминалами
2. **Веб-интерфейс**: Простая панель управления
3. **Уведомления**: Slack/Discord интеграция
4. **Метрики**: Производительность агентов
5. **AI логика**: Умное распределение задач

## Пример использования

```bash
# Terminal 1 - Запуск оркестратора
cd src/Orchestra.API
dotnet run

# Terminal 2 - Мониторинг
cd src/Orchestra.CLI
dotnet run status

# Terminal 3 - Добавление задач
cd src/Orchestra.CLI
dotnet run queue add "Analyze codebase" "C:\\MyProject"
dotnet run queue add "Run unit tests" "C:\\MyProject" High
```

Система автоматически распределит задачи между доступными агентами на основе их статуса и специализации.