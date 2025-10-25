# Agent Interaction System Architecture

## Overview

**Purpose**: Обеспечить двустороннюю коммуникацию между Orchestra (агент-оркестратор) и внешними агентами (Claude Code, Cursor, и т.д.) через их нативные интерфейсы взаимодействия.

**Status**: 🟡 Planned - In Development
**Priority**: P1 (High)
**Created**: 2025-10-25
**Author**: Claude Code

---

## Problem Statement

### Текущее состояние
Orchestra может выполнять команды через внешние агенты, но только одноразово (fire-and-forget):
- ✅ ShellAgentExecutor создает новый процесс для каждой команды
- ❌ Нет подключения к существующим терминалам/сессиям агентов
- ❌ Нет получения real-time вывода из агентов
- ❌ Нет возможности отправить команду в существующую сессию

### Целевое состояние
Orchestra должна иметь возможность:
- ✅ Подключаться к существующим терминалам/сессиям внешних агентов
- ✅ Читать вывод из этих сессий в real-time
- ✅ Отправлять команды В существующие сессии
- ✅ Поддерживать разные типы агентов с разными моделями взаимодействия

---

## System Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Orchestra Web UI (Blazor)                    │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │          AgentTerminalComponent.razor                     │  │
│  │  - Display output                                         │  │
│  │  - Input commands                                         │  │
│  │  - Connection management                                  │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                            │ SignalR
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                Orchestra API (ASP.NET Core)                     │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │          AgentInteractionHub (SignalR Hub)                │  │
│  │  - ConnectToAgent(agentId, params)                        │  │
│  │  - SendCommandToAgent(sessionId, command)                 │  │
│  │  - StreamAgentOutput(sessionId) → IAsyncEnumerable        │  │
│  │  - DisconnectFromAgent(sessionId)                         │  │
│  └───────────────────────────────────────────────────────────┘  │
│                            │                                     │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │          AgentSessionManager                              │  │
│  │  - CreateSession(agentId, connector)                      │  │
│  │  - GetSession(sessionId)                                  │  │
│  │  - CloseSession(sessionId)                                │  │
│  │  - Auto-cleanup inactive sessions                         │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│              Orchestra.Core (Business Logic)                    │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │         IAgentConnector (Interface)                       │  │
│  │  + ConnectAsync(agentId, params)                          │  │
│  │  + SendCommandAsync(command)                              │  │
│  │  + ReadOutputAsync()                                      │  │
│  │  + DisconnectAsync()                                      │  │
│  └───────────────────────────────────────────────────────────┘  │
│         │                         │                              │
│         ▼                         ▼                              │
│  ┌──────────────────┐    ┌────────────────────────┐             │
│  │TerminalAgent     │    │TabBasedAgent           │             │
│  │Connector         │    │Connector               │             │
│  │(Claude Code)     │    │(Cursor)                │             │
│  │                  │    │                        │             │
│  │- Named Pipes     │    │- API calls             │             │
│  │- Process attach  │    │- Tab management        │             │
│  └──────────────────┘    └────────────────────────┘             │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │         IAgentOutputBuffer (Interface)                    │  │
│  │  + AppendLineAsync(line)                                  │  │
│  │  + GetLinesAsync(filter?, count?) → IAsyncEnumerable     │  │
│  │  + ClearAsync()                                           │  │
│  └───────────────────────────────────────────────────────────┘  │
│                            │                                     │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │         AgentOutputBuffer (Implementation)                │  │
│  │  - CircularBuffer<string> (10,000 lines)                  │  │
│  │  - SemaphoreSlim for thread-safety                        │  │
│  │  - Regex filtering support                                │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│              External Agents (Claude Code, Cursor)              │
│  ┌────────────────────┐    ┌────────────────────┐               │
│  │ Terminal Session   │    │ Cursor Tabs        │               │
│  │ (Claude Code)      │    │                    │               │
│  │                    │    │                    │               │
│  │ stdin/stdout/      │    │ API endpoint       │               │
│  │ stderr streams     │    │                    │               │
│  └────────────────────┘    └────────────────────┘               │
└─────────────────────────────────────────────────────────────────┘
```

---

## Component Specifications

### 1. IAgentConnector Interface

**Purpose**: Абстракция для подключения к различным типам внешних агентов

```csharp
namespace Orchestra.Core.Services.Connectors
{
    /// <summary>
    /// Интерфейс для подключения к внешним агентам различных типов
    /// </summary>
    public interface IAgentConnector : IDisposable
    {
        /// <summary>
        /// Тип коннектора (terminal, api, tab-based)
        /// </summary>
        string ConnectorType { get; }

        /// <summary>
        /// Идентификатор подключенного агента
        /// </summary>
        string? AgentId { get; }

        /// <summary>
        /// Статус подключения
        /// </summary>
        ConnectionStatus Status { get; }

        /// <summary>
        /// Подключение к агенту
        /// </summary>
        Task<ConnectionResult> ConnectAsync(
            string agentId,
            AgentConnectionParams connectionParams,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Отправка команды в агента
        /// </summary>
        Task<CommandResult> SendCommandAsync(
            string command,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Чтение вывода из агента (асинхронный поток)
        /// </summary>
        IAsyncEnumerable<string> ReadOutputAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Отключение от агента
        /// </summary>
        Task<DisconnectionResult> DisconnectAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// События изменения статуса подключения
        /// </summary>
        event EventHandler<ConnectionStatusChangedEventArgs>? StatusChanged;
    }

    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }
}
```

**Key Design Decisions**:
- Используем `IAsyncEnumerable<string>` для streaming вывода
- Event-based подход для уведомлений о статусе
- CancellationToken для корректного управления lifetime

---

### 2. TerminalAgentConnector (Claude Code)

**Purpose**: Реализация коннектора для терминальных агентов (Claude Code)

```csharp
namespace Orchestra.Core.Services.Connectors
{
    /// <summary>
    /// Коннектор для подключения к терминальным агентам через stdin/stdout
    /// </summary>
    public class TerminalAgentConnector : IAgentConnector
    {
        private readonly ILogger<TerminalAgentConnector> _logger;
        private readonly IAgentOutputBuffer _outputBuffer;

        private Process? _attachedProcess;
        private StreamWriter? _stdinWriter;
        private Task? _outputReaderTask;
        private CancellationTokenSource? _readerCancellation;

        public string ConnectorType => "terminal";
        public string? AgentId { get; private set; }
        public ConnectionStatus Status { get; private set; }

        public event EventHandler<ConnectionStatusChangedEventArgs>? StatusChanged;

        // Implementation details...
    }
}
```

**Connection Mechanisms**:

1. **Named Pipes** (Windows):
   ```csharp
   var pipeName = $"orchestra_agent_{agentId}";
   var pipeServer = new NamedPipeServerStream(
       pipeName,
       PipeDirection.InOut,
       maxNumberOfServerInstances: 1,
       PipeTransmissionMode.Byte,
       PipeOptions.Asynchronous);

   await pipeServer.WaitForConnectionAsync(cancellationToken);
   ```

2. **Process Attach** (by PID):
   ```csharp
   var process = Process.GetProcessById(connectionParams.ProcessId);

   // Redirect streams (requires special permissions)
   // Alternative: Use debugging API to inject communication channel
   ```

3. **Unix Domain Sockets** (Linux/macOS):
   ```csharp
   var socketPath = $"/tmp/orchestra_agent_{agentId}.sock";
   var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
   var endpoint = new UnixDomainSocketEndPoint(socketPath);
   await socket.ConnectAsync(endpoint, cancellationToken);
   ```

**Preferred Approach for Claude Code**: Named Pipes / Unix Domain Sockets

**Rationale**:
- Process attach requires elevated permissions
- Named pipes/sockets allow Claude Code to create server endpoint that Orchestra connects to
- More secure and doesn't require process manipulation

---

### 3. AgentSessionManager

**Purpose**: Управление lifecycle сессий подключения к агентам

```csharp
namespace Orchestra.Core.Services.Connectors
{
    /// <summary>
    /// Менеджер сессий подключения к внешним агентам
    /// </summary>
    public class AgentSessionManager : IAgentSessionManager
    {
        private readonly ConcurrentDictionary<string, AgentSession> _activeSessions;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AgentSessionManager> _logger;

        // Session timeout: 30 minutes of inactivity
        private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

        public Task<AgentSession> CreateSessionAsync(
            string agentId,
            string connectorType,
            AgentConnectionParams connectionParams,
            CancellationToken cancellationToken = default);

        public AgentSession? GetSession(string sessionId);

        public Task<bool> CloseSessionAsync(string sessionId);

        public IReadOnlyCollection<AgentSession> GetActiveSessions();

        // Background cleanup task
        private async Task CleanupInactiveSessionsAsync(CancellationToken cancellationToken);
    }

    public class AgentSession
    {
        public string SessionId { get; init; }
        public string AgentId { get; init; }
        public IAgentConnector Connector { get; init; }
        public IAgentOutputBuffer OutputBuffer { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime LastActivityAt { get; set; }
        public ConnectionStatus Status => Connector.Status;
    }
}
```

**Key Features**:
- Thread-safe session management (ConcurrentDictionary)
- Automatic cleanup of inactive sessions
- Session lifecycle tracking (created, last activity)
- Integration with DI для создания connectors

---

### 4. IAgentOutputBuffer

**Purpose**: Буферизация вывода из агентов с поддержкой фильтрации

```csharp
namespace Orchestra.Core.Services.Connectors
{
    /// <summary>
    /// Буфер вывода из внешнего агента
    /// </summary>
    public interface IAgentOutputBuffer
    {
        /// <summary>
        /// Добавить строку вывода в буфер
        /// </summary>
        Task AppendLineAsync(string line, CancellationToken cancellationToken = default);

        /// <summary>
        /// Получить последние N строк вывода
        /// </summary>
        Task<IReadOnlyList<string>> GetLastLinesAsync(
            int count = 100,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Получить все строки вывода в виде асинхронного потока
        /// </summary>
        IAsyncEnumerable<string> GetLinesAsync(
            string? regexFilter = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Очистить буфер
        /// </summary>
        Task ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Текущий размер буфера (количество строк)
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Событие добавления новой строки вывода
        /// </summary>
        event EventHandler<OutputLineAddedEventArgs>? LineAdded;
    }
}
```

**Implementation**: Circular Buffer with thread-safety

```csharp
public class AgentOutputBuffer : IAgentOutputBuffer
{
    private readonly CircularBuffer<OutputLine> _buffer;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly int _maxLines;

    public AgentOutputBuffer(int maxLines = 10000)
    {
        _maxLines = maxLines;
        _buffer = new CircularBuffer<OutputLine>(maxLines);
    }

    // Implementation...
}

public record OutputLine(string Content, DateTime Timestamp);
```

---

### 5. AgentInteractionHub (SignalR)

**Purpose**: Real-time коммуникация между UI и агентами через SignalR

```csharp
namespace Orchestra.API.Hubs
{
    /// <summary>
    /// SignalR hub для взаимодействия с внешними агентами
    /// </summary>
    public class AgentInteractionHub : Hub
    {
        private readonly IAgentSessionManager _sessionManager;
        private readonly ILogger<AgentInteractionHub> _logger;

        /// <summary>
        /// Подключиться к внешнему агенту
        /// </summary>
        public async Task<string> ConnectToAgent(
            string agentId,
            string connectorType,
            AgentConnectionParams connectionParams)
        {
            var session = await _sessionManager.CreateSessionAsync(
                agentId,
                connectorType,
                connectionParams);

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"agent_session_{session.SessionId}");

            return session.SessionId;
        }

        /// <summary>
        /// Отправить команду в агента
        /// </summary>
        public async Task SendCommand(string sessionId, string command)
        {
            var session = _sessionManager.GetSession(sessionId);
            if (session == null)
                throw new InvalidOperationException($"Session {sessionId} not found");

            var result = await session.Connector.SendCommandAsync(command);

            await Clients.Group($"agent_session_{sessionId}")
                .SendAsync("CommandSent", new { sessionId, command, result });
        }

        /// <summary>
        /// Подписаться на вывод из агента (streaming)
        /// </summary>
        public async IAsyncEnumerable<string> StreamOutput(
            string sessionId,
            string? filter = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var session = _sessionManager.GetSession(sessionId);
            if (session == null)
                throw new InvalidOperationException($"Session {sessionId} not found");

            await foreach (var line in session.OutputBuffer.GetLinesAsync(filter, cancellationToken))
            {
                yield return line;
            }
        }

        /// <summary>
        /// Отключиться от агента
        /// </summary>
        public async Task DisconnectFromAgent(string sessionId)
        {
            await _sessionManager.CloseSessionAsync(sessionId);
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                $"agent_session_{sessionId}");
        }
    }
}
```

**SignalR Features Used**:
- **Server-to-Client Streaming**: `IAsyncEnumerable<T>` для real-time вывода
- **Groups**: Для broadcast сообщений всем подключенным клиентам сессии
- **CancellationToken**: Для корректного закрытия потоков

---

### 6. AgentTerminalComponent (Frontend)

**Purpose**: Blazor компонент для отображения терминала агента и ввода команд

```razor
<!-- AgentTerminalComponent.razor -->
@using Microsoft.AspNetCore.SignalR.Client
@inject NavigationManager Navigation
@implements IAsyncDisposable

<div class="agent-terminal">
    <div class="terminal-header">
        <span class="agent-id">Agent: @AgentId</span>
        <span class="connection-status @StatusClass">@Status</span>
        <button class="btn btn-danger btn-sm" @onclick="DisconnectAsync">Disconnect</button>
    </div>

    <div class="terminal-output" @ref="_outputContainer">
        @foreach (var line in _outputLines)
        {
            <div class="terminal-line">@line</div>
        }
    </div>

    <div class="terminal-input">
        <input type="text"
               class="form-control"
               @bind="_commandInput"
               @bind:event="oninput"
               @onkeypress="HandleKeyPress"
               placeholder="Enter command..." />
        <button class="btn btn-primary" @onclick="SendCommandAsync">Send</button>
    </div>
</div>

@code {
    [Parameter] public string AgentId { get; set; } = string.Empty;
    [Parameter] public string ConnectorType { get; set; } = "terminal";

    private HubConnection? _hubConnection;
    private string? _sessionId;
    private List<string> _outputLines = new();
    private string _commandInput = string.Empty;
    private ElementReference _outputContainer;

    // Implementation...
}
```

**UI Features**:
- Real-time вывод с автоскроллом
- История команд (стрелки вверх/вниз)
- Индикатор статуса подключения
- Поддержка фильтрации вывода

---

## Data Flow Diagrams

### Connection Flow

```
User                UI Component         SignalR Hub        SessionManager     Connector          External Agent
  │                      │                    │                  │                │                   │
  │ Click "Connect"      │                    │                  │                │                   │
  ├─────────────────────>│                    │                  │                │                   │
  │                      │ ConnectToAgent()   │                  │                │                   │
  │                      ├───────────────────>│                  │                │                   │
  │                      │                    │ CreateSession()  │                │                   │
  │                      │                    ├─────────────────>│                │                   │
  │                      │                    │                  │ Create         │                   │
  │                      │                    │                  │ Connector      │                   │
  │                      │                    │                  ├───────────────>│                   │
  │                      │                    │                  │                │ ConnectAsync()    │
  │                      │                    │                  │                ├──────────────────>│
  │                      │                    │                  │                │<──────────────────┤
  │                      │                    │                  │                │  Connection OK    │
  │                      │                    │                  │<───────────────┤                   │
  │                      │                    │<─────────────────┤                │                   │
  │                      │<───────────────────┤  SessionId       │                │                   │
  │<─────────────────────┤  Connected         │                  │                │                   │
  │  Show Terminal       │                    │                  │                │                   │
```

### Command Send Flow

```
User                UI Component         SignalR Hub        SessionManager     Connector          External Agent
  │                      │                    │                  │                │                   │
  │ Enter command        │                    │                  │                │                   │
  ├─────────────────────>│                    │                  │                │                   │
  │                      │ SendCommand()      │                  │                │                   │
  │                      ├───────────────────>│                  │                │                   │
  │                      │                    │ GetSession()     │                │                   │
  │                      │                    ├─────────────────>│                │                   │
  │                      │                    │<─────────────────┤                │                   │
  │                      │                    │                  │                │                   │
  │                      │                    │ SendCommandAsync()                │                   │
  │                      │                    ├───────────────────────────────────>│                   │
  │                      │                    │                  │                │ Write to stdin    │
  │                      │                    │                  │                ├──────────────────>│
  │                      │                    │                  │                │                   │
  │                      │                    │                  │                │ Command executed  │
  │                      │                    │<──────────────────────────────────┤                   │
  │                      │<───────────────────┤  Command sent    │                │                   │
  │<─────────────────────┤  Confirmation      │                  │                │                   │
```

### Output Streaming Flow

```
External Agent      Connector         OutputBuffer      SignalR Hub         UI Component         User
     │                  │                  │                  │                    │                │
     │ Stdout line      │                  │                  │                    │                │
     ├─────────────────>│                  │                  │                    │                │
     │                  │ AppendLine()     │                  │                    │                │
     │                  ├─────────────────>│                  │                    │                │
     │                  │                  │ LineAdded event  │                    │                │
     │                  │                  ├─────────────────>│                    │                │
     │                  │                  │                  │ StreamOutput()     │                │
     │                  │                  │                  │ (IAsyncEnumerable) │                │
     │                  │                  │                  ├───────────────────>│                │
     │                  │                  │                  │                    │ Display line   │
     │                  │                  │                  │                    ├───────────────>│
     │                  │                  │                  │                    │                │
     │ Next line        │                  │                  │                    │                │
     ├─────────────────>│                  │                  │                    │                │
     │                  ├─────────────────>│                  │                    │                │
     │                  │                  ├─────────────────>│───────────────────>│───────────────>│
```

---

## Technology Stack

### Backend
- **.NET 9.0**: Core framework
- **ASP.NET Core SignalR**: Real-time communication
- **System.Threading.Channels**: For async streaming
- **Microsoft.Extensions.DependencyInjection**: DI container

### Frontend
- **Blazor WebAssembly**: UI framework
- **Microsoft.AspNetCore.SignalR.Client**: SignalR client
- **JavaScript Interop**: Для advanced terminal features (если нужно)

### Testing
- **xUnit**: Unit testing framework
- **Moq**: Mocking framework
- **FluentAssertions**: Assertion library
- **Microsoft.AspNetCore.SignalR.Client.Testing**: SignalR hub testing

---

## Security Considerations

### 1. Connection Authorization
- Только авторизованные пользователи могут подключаться к агентам
- Валидация `agentId` и `connectorType` перед созданием сессии
- Rate limiting для предотвращения abuse

### 2. Command Validation
- Blacklist опасных команд (rm -rf, format, shutdown, и т.д.)
- Escape special characters в командах
- Audit log всех отправленных команд

### 3. Output Sanitization
- Escape HTML/JS в выводе для предотвращения XSS
- Ограничение размера вывода (max 10,000 строк в буфере)
- Фильтрация sensitive data (пароли, токены)

### 4. Session Management
- Автоматическое закрытие неактивных сессий (30 минут)
- Ограничение количества одновременных сессий на пользователя
- Cleanup orphaned sessions при отключении SignalR

---

## Performance Considerations

### 1. Output Buffering
- **CircularBuffer**: Эффективное использование памяти (фиксированный размер)
- **SemaphoreSlim**: Минимизация lock contention
- **Async operations**: Не блокируем потоки

### 2. SignalR Streaming
- **Backpressure handling**: Если клиент медленный, буферизуем на сервере
- **Compression**: SignalR message compression для больших объемов данных
- **Connection pooling**: Переиспользование SignalR connections

### 3. Memory Management
- **Dispose pattern**: Корректное освобождение resources (Process, Streams)
- **Weak references**: Для caching session metadata
- **GC optimization**: Minimize allocations в hot paths

---

## Implementation Phases

### Phase 1: Core Infrastructure (2-3 дня)
**Goal**: Создать основную инфраструктуру для подключения к агентам

**Tasks**:
- [ ] Создать `IAgentConnector` интерфейс
- [ ] Реализовать `TerminalAgentConnector` (базовая версия с named pipes)
- [ ] Создать `AgentSessionManager`
- [ ] Создать `IAgentOutputBuffer` и `AgentOutputBuffer`
- [ ] Unit-тесты для всех компонентов

**Deliverables**:
- `Orchestra.Core.Services.Connectors` namespace со всеми классами
- >80% code coverage в тестах

---

### Phase 2: SignalR Integration (1-2 дня)
**Goal**: Интегрировать core компоненты с SignalR для real-time коммуникации

**Tasks**:
- [ ] Создать `AgentInteractionHub`
- [ ] Реализовать server-to-client streaming для вывода
- [ ] Реализовать отправку команд через SignalR
- [ ] Integration тесты с SignalR testing framework

**Deliverables**:
- `Orchestra.API.Hubs.AgentInteractionHub` класс
- Integration тесты

---

### Phase 3: Frontend Component (1-2 дня)
**Goal**: Создать UI компонент для взаимодействия с терминалом

**Tasks**:
- [ ] Создать `AgentTerminalComponent.razor`
- [ ] Реализовать SignalR client подключение
- [ ] Реализовать display вывода с автоскроллом
- [ ] Реализовать input команд
- [ ] Стилизация компонента

**Deliverables**:
- `Orchestra.Web.Components.AgentTerminalComponent` компонент
- CSS стили для терминала

---

### Phase 4: Testing & Documentation (1 день)
**Goal**: Комплексное тестирование и документация

**Tasks**:
- [ ] End-to-end тесты
- [ ] Performance тесты (stress testing)
- [ ] API документация (XML comments)
- [ ] Пользовательская документация (README)
- [ ] Примеры использования

**Deliverables**:
- Полный набор тестов (unit + integration + e2e)
- Документация

---

## Success Criteria

### Functional Requirements
- [ ] ✅ Можно подключиться к существующему терминалу Claude Code
- [ ] ✅ Получение вывода из терминала в real-time через SignalR
- [ ] ✅ Отправка команды в терминал из UI
- [ ] ✅ Автоматическое отключение при закрытии терминала
- [ ] ✅ Поддержка множественных одновременных подключений

### Non-Functional Requirements
- [ ] ✅ Latency вывода <100ms (от stdout в External Agent до UI)
- [ ] ✅ Throughput >1000 строк/секунду
- [ ] ✅ Memory usage <100MB для 10 одновременных сессий
- [ ] ✅ Code coverage >80%
- [ ] ✅ Zero memory leaks (validated with profiler)

---

## Future Enhancements (Post-MVP)

### 1. TabBasedAgentConnector (Cursor)
Реализовать коннектор для tab-based агентов:
- API для управления табами
- Множественные сессии на один агент
- Tab isolation

### 2. Advanced Terminal Features
- Syntax highlighting вывода
- Autocomplete команд
- History search (Ctrl+R)
- Terminal themes

### 3. Agent Discovery
- Автоматическое обнаружение запущенных агентов
- Health checks
- Reconnection logic

### 4. Analytics & Monitoring
- Command usage statistics
- Performance metrics
- Error tracking
- Session analytics

---

## References

### External Documentation
- [SignalR Streaming](https://learn.microsoft.com/en-us/aspnet/core/signalr/streaming)
- [Named Pipes](https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-use-named-pipes-for-network-interprocess-communication)
- [Unix Domain Sockets](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.unixdomainsocketendpoint)

### Internal Documentation
- [Orchestra Architecture](../README.md)
- [Agent Executor Pattern](./agent-executor-pattern.md)
- [SignalR Hubs Guide](../../Development/signalr-hubs.md)

---

## Changelog

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-10-25 | 0.1.0 | Initial architecture document | Claude Code |

---

**Status**: 🟡 In Development
**Next Review**: After Phase 1 completion
**Owner**: Development Team
