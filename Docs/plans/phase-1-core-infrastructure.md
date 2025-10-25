# Phase 1: Core Infrastructure

**Parent Plan**: [Agent-Interaction-System-Implementation-Plan.md](./Agent-Interaction-System-Implementation-Plan.md)

**Goal**: Create core infrastructure for connecting to external agents

**Total Estimate**: 22-29 hours

**Status**: Partially Complete (Phase 1.1 ✅ COMPLETE)

---

## Phase 1.1: Core Interfaces ✅ COMPLETE

**Status**: ✅ COMPLETE (commit 8ce7e5e)

**Completed Files**:
- `src/Orchestra.Core/Services/Connectors/IAgentConnector.cs`
- `src/Orchestra.Core/Services/Connectors/ConnectionStatusChangedEventArgs.cs`
- `src/Orchestra.Core/Models/ConnectionResult.cs`
- `src/Orchestra.Core/Models/ConnectionStatus.cs`
- `src/Orchestra.Core/Models/AgentConnectionParams.cs`

---

## Phase 1.2: TerminalAgentConnector Implementation

**Estimate**: 8-10 hours

**Goal**: Implement connector for terminal-based agents using Unix Domain Sockets

### Task 1.2A: Create Base TerminalAgentConnector Class ✅ COMPLETE

**Status**: [x] ✅ COMPLETE
**Completed**: 2025-10-25
**Review Confidence**: 95% (pre-completion-validator)

**Files Created**:
- `src/Orchestra.Core/Services/Connectors/TerminalAgentConnector.cs` (280 lines)
- `src/Orchestra.Core/Services/Connectors/IAgentOutputBuffer.cs` (stub, 67 lines)
- `src/Orchestra.Tests/Services/Connectors/TerminalAgentConnectorTests.cs` (360 lines, 24 tests)

**Results**:
- Full TerminalAgentConnector implementation with all IAgentConnector members stubbed
- Proper disposal pattern with resource cleanup implemented
- IAgentOutputBuffer stub interface created for compilation
- Comprehensive unit tests: 24/24 passing (100%)
- Build: SUCCESS (no errors, no warnings)
- All methods throw NotImplementedException with NOTE comments referencing future tasks

**Review Iterations**: 1/2 (single iteration sufficient)
**Reviewers**: pre-completion-validator (95%), code-principles-reviewer (Medium), code-style-reviewer (95/100)

**Acceptance Criteria**:
- [x] Class compiles with all interface members stubbed
- [x] Disposal pattern implemented correctly
- [x] Logger and buffer dependencies injected via constructor
- [x] 24 unit tests created and passing (100%)

### Task 1.2B: Implement Cross-Platform Socket Connection (3-4 hours) - DECOMPOSED

**⚠️ This task has been decomposed**: See [task-1.2b-terminal-connector.md](./phase-1-core-infrastructure/task-1.2b-terminal-connector.md) for detailed subtasks

**Technical Steps**:

1. **Create platform detection helper**:
   ```csharp
   private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
   ```

2. **Implement Unix Domain Socket connection (Linux/macOS)**:
   ```csharp
   private async Task<Socket> ConnectUnixSocketAsync(string socketPath, CancellationToken ct)
   {
       // TODO: Create Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
       // TODO: Create UnixDomainSocketEndPoint(socketPath)
       // TODO: await socket.ConnectAsync(endpoint, ct)
       // TODO: Return connected socket
   }
   ```

3. **Implement Named Pipe fallback (Windows legacy)**:
   ```csharp
   private async Task<Stream> ConnectNamedPipeAsync(string pipeName, CancellationToken ct)
   {
       // TODO: Create NamedPipeClientStream
       // TODO: await ConnectAsync with timeout
       // TODO: Return stream
   }
   ```

4. **Implement ConnectAsync method**:
   ```csharp
   public async Task<ConnectionResult> ConnectAsync(
       string agentId,
       AgentConnectionParams connectionParams,
       CancellationToken cancellationToken)
   {
       // TODO: Validate parameters
       // TODO: Set Status = Connecting
       // TODO: Platform-specific connection logic
       // TODO: Create NetworkStream from socket
       // TODO: Start background output reader task
       // TODO: Set Status = Connected
       // TODO: Raise StatusChanged event
   }
   ```

**Integration Points**:
- [ ] Socket/Stream created and stored
- [ ] Platform detection working
- [ ] Connection timeout handling (30 seconds default)
- [ ] Status transitions firing events

**Acceptance Criteria**:
- [ ] Connects to UDS on Linux/macOS
- [ ] Falls back to named pipes on older Windows
- [ ] Connection errors handled gracefully
- [ ] Status events fired correctly

### Task 1.2C: Implement Command Sending and Output Reading (2-3 hours)

**Technical Steps**:

1. **Implement SendCommandAsync**:
   ```csharp
   public async Task<CommandResult> SendCommandAsync(string command, CancellationToken ct)
   {
       // TODO: Check connection status
       // TODO: Write command + newline to stream
       // TODO: Flush stream
       // TODO: Return success/failure result
   }
   ```

2. **Implement output reader background task**:
   ```csharp
   private async Task ReadOutputLoopAsync(Stream stream, CancellationToken ct)
   {
       using var reader = new StreamReader(stream);
       while (!ct.IsCancellationRequested)
       {
           // TODO: ReadLineAsync from stream
           // TODO: Append to IAgentOutputBuffer
           // TODO: Handle stream errors/disconnection
       }
   }
   ```

3. **Implement ReadOutputAsync streaming**:
   ```csharp
   public async IAsyncEnumerable<string> ReadOutputAsync([EnumeratorCancellation] CancellationToken ct)
   {
       // TODO: Get lines from _outputBuffer
       // TODO: Yield return each line
       // TODO: Handle cancellation
   }
   ```

**Acceptance Criteria**:
- [ ] Commands sent successfully to agent
- [ ] Output captured in real-time
- [ ] Stream errors cause proper disconnection
- [ ] Cancellation handled cleanly

### Task 1.2D: Integration and Error Handling (1 hour)

**Technical Steps**:

1. **Add reconnection logic**:
   ```csharp
   private async Task<bool> TryReconnectAsync()
   {
       // TODO: Exponential backoff
       // TODO: Max retry attempts
   }
   ```

2. **Implement DisconnectAsync**:
   ```csharp
   public async Task<DisconnectionResult> DisconnectAsync(CancellationToken ct)
   {
       // TODO: Cancel reader task
       // TODO: Close socket/stream
       // TODO: Set Status = Disconnected
       // TODO: Cleanup resources
   }
   ```

3. **Add comprehensive error handling**:
   - Socket exceptions
   - Stream IO exceptions
   - Timeout exceptions
   - Cancellation exceptions

**Acceptance Criteria**:
- [ ] Graceful disconnect works
- [ ] Reconnection attempts on failure
- [ ] All exceptions logged appropriately
- [ ] No resource leaks

---

## Phase 1.3: AgentSessionManager Implementation

**Estimate**: 6-8 hours

**Goal**: Manage lifecycle of agent connection sessions

### Task 1.3A: Create AgentSessionManager Base (2 hours)

**Technical Steps**:

1. **Create interface and class**:
   ```csharp
   // File: src/Orchestra.Core/Services/Connectors/IAgentSessionManager.cs
   public interface IAgentSessionManager
   {
       // TODO: CreateSessionAsync method
       // TODO: GetSession method
       // TODO: CloseSessionAsync method
       // TODO: GetActiveSessions method
   }

   // File: src/Orchestra.Core/Services/Connectors/AgentSessionManager.cs
   public class AgentSessionManager : IAgentSessionManager, IHostedService
   {
       // TODO: ConcurrentDictionary for sessions
       // TODO: IServiceProvider for creating connectors
       // TODO: ILogger dependency
   }
   ```

2. **Create AgentSession model**:
   ```csharp
   public class AgentSession
   {
       public string SessionId { get; init; }
       public string AgentId { get; init; }
       public IAgentConnector Connector { get; init; }
       public IAgentOutputBuffer OutputBuffer { get; init; }
       public DateTime CreatedAt { get; init; }
       public DateTime LastActivityAt { get; set; }
   }
   ```

3. **DI Registration in Program.cs**:
   ```csharp
   // In Program.cs or Startup.cs
   services.AddSingleton<IAgentSessionManager, AgentSessionManager>();
   services.AddHostedService<AgentSessionManager>();

   // Register connector types
   services.AddTransient<TerminalAgentConnector>();

   // Register output buffer factory
   services.AddSingleton<IAgentOutputBufferFactory, AgentOutputBufferFactory>();

   // Configure options
   services.Configure<AgentSessionOptions>(builder.Configuration.GetSection("AgentSession"));
   ```

**Acceptance Criteria**:
- [ ] Interface defines all session operations
- [ ] Session model captures all metadata
- [ ] Manager registered in DI container
- [ ] Compiles without errors

### Task 1.3B: Implement Session Creation and Management (2-3 hours)

**Technical Steps**:

1. **Implement CreateSessionAsync**:
   ```csharp
   public async Task<AgentSession> CreateSessionAsync(
       string agentId,
       string connectorType,
       AgentConnectionParams connectionParams,
       CancellationToken ct)
   {
       // TODO: Create connector based on type (factory pattern)
       // TODO: Create output buffer
       // TODO: Connect to agent
       // TODO: Create session object
       // TODO: Add to concurrent dictionary
       // TODO: Return session
   }
   ```

2. **Implement connector factory**:
   ```csharp
   private IAgentConnector CreateConnector(string connectorType)
   {
       return connectorType switch
       {
           "terminal" => _serviceProvider.GetRequiredService<TerminalAgentConnector>(),
           _ => throw new NotSupportedException($"Connector type {connectorType} not supported")
       };
   }
   ```

3. **Implement session retrieval and closure**:
   ```csharp
   public AgentSession? GetSession(string sessionId)
   {
       // TODO: Lookup in dictionary
       // TODO: Update LastActivityAt
   }

   public async Task<bool> CloseSessionAsync(string sessionId)
   {
       // TODO: Remove from dictionary
       // TODO: Disconnect connector
       // TODO: Dispose resources
   }
   ```

**Integration Points**:
- [ ] Connector factory creates correct types
- [ ] Sessions stored in thread-safe dictionary
- [ ] Activity tracking updates on access

**Acceptance Criteria**:
- [ ] Sessions created with unique IDs
- [ ] Connectors properly initialized
- [ ] Session retrieval updates activity
- [ ] Closure cleans up resources

### Task 1.3C: Implement Background Cleanup Service (2-3 hours)

**Technical Steps**:

1. **Implement IHostedService methods**:
   ```csharp
   public async Task StartAsync(CancellationToken ct)
   {
       // TODO: Start cleanup timer (every 5 minutes)
       _cleanupTimer = new Timer(CleanupCallback, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
   }

   public Task StopAsync(CancellationToken ct)
   {
       // TODO: Stop timer
       // TODO: Close all active sessions
   }
   ```

2. **Implement cleanup logic**:
   ```csharp
   private async void CleanupCallback(object? state)
   {
       var timeout = TimeSpan.FromMinutes(30);
       var expiredSessions = _activeSessions
           .Where(kvp => DateTime.UtcNow - kvp.Value.LastActivityAt > timeout)
           .Select(kvp => kvp.Key);

       foreach (var sessionId in expiredSessions)
       {
           // TODO: Close expired session
           // TODO: Log cleanup action
       }
   }
   ```

3. **Add health checks**:
   ```csharp
   public class SessionManagerHealthCheck : IHealthCheck
   {
       // TODO: Check session count
       // TODO: Check connector health
       // TODO: Return healthy/unhealthy
   }
   ```

**Acceptance Criteria**:
- [ ] Background service starts/stops correctly
- [ ] Inactive sessions cleaned up after 30 min
- [ ] Health check reports status
- [ ] No memory leaks from expired sessions

---

## Phase 1.4: Output Buffer Implementation

**Estimate**: 4-6 hours

**Goal**: Implement efficient circular buffer for agent output

### Task 1.4A: Create IAgentOutputBuffer Interface and AgentOutputBuffer Class (2 hours)

**Technical Steps**:

1. **Create interface**:
   ```csharp
   // File: src/Orchestra.Core/Services/Connectors/IAgentOutputBuffer.cs
   public interface IAgentOutputBuffer
   {
       Task AppendLineAsync(string line, CancellationToken ct = default);
       Task<IReadOnlyList<string>> GetLastLinesAsync(int count = 100, CancellationToken ct = default);
       IAsyncEnumerable<string> GetLinesAsync(string? regexFilter = null, CancellationToken ct = default);
       Task ClearAsync(CancellationToken ct = default);
       int Count { get; }
       event EventHandler<OutputLineAddedEventArgs>? LineAdded;
   }
   ```

2. **Create circular buffer implementation**:
   ```csharp
   // File: src/Orchestra.Core/Services/Connectors/AgentOutputBuffer.cs
   public class AgentOutputBuffer : IAgentOutputBuffer
   {
       private readonly CircularBuffer<OutputLine> _buffer;
       private readonly SemaphoreSlim _lock = new(1, 1);
       private readonly int _maxLines;

       // TODO: Constructor with maxLines parameter (default 10000)
       // TODO: Initialize circular buffer
   }
   ```

3. **Create supporting types**:
   ```csharp
   public record OutputLine(string Content, DateTime Timestamp);
   public class OutputLineAddedEventArgs : EventArgs
   {
       public string Line { get; init; }
       public DateTime Timestamp { get; init; }
   }
   ```

**Acceptance Criteria**:
- [ ] Interface defines all buffer operations
- [ ] Circular buffer initialized with max capacity
- [ ] Thread-safety with SemaphoreSlim
- [ ] Event args for line additions

### Task 1.4B: Implement Core Buffer Operations (2-3 hours)

**Technical Steps**:

1. **Implement AppendLineAsync**:
   ```csharp
   public async Task AppendLineAsync(string line, CancellationToken ct)
   {
       await _lock.WaitAsync(ct);
       try
       {
           // TODO: Add to circular buffer
           // TODO: Raise LineAdded event
       }
       finally
       {
           _lock.Release();
       }
   }
   ```

2. **Implement GetLastLinesAsync**:
   ```csharp
   public async Task<IReadOnlyList<string>> GetLastLinesAsync(int count, CancellationToken ct)
   {
       await _lock.WaitAsync(ct);
       try
       {
           // TODO: Get last N items from buffer
           // TODO: Extract content strings
           // TODO: Return as IReadOnlyList
       }
       finally
       {
           _lock.Release();
       }
   }
   ```

3. **Implement streaming with filter**:
   ```csharp
   public async IAsyncEnumerable<string> GetLinesAsync(
       string? regexFilter,
       [EnumeratorCancellation] CancellationToken ct)
   {
       Regex? filter = regexFilter != null ? new Regex(regexFilter) : null;

       await _lock.WaitAsync(ct);
       try
       {
           foreach (var line in _buffer)
           {
               if (filter == null || filter.IsMatch(line.Content))
                   yield return line.Content;
           }
       }
       finally
       {
           _lock.Release();
       }
   }
   ```

**Acceptance Criteria**:
- [ ] Lines added to buffer atomically
- [ ] Last N lines retrieved efficiently
- [ ] Regex filtering works correctly
- [ ] Thread-safe operations

### Task 1.4C: Implement Circular Buffer Helper (1-2 hours)

**Technical Steps**:

1. **Create generic CircularBuffer<T>**:
   ```csharp
   internal class CircularBuffer<T> : IEnumerable<T>
   {
       private readonly T[] _buffer;
       private int _head;
       private int _tail;
       private int _count;

       // TODO: Constructor with capacity
       // TODO: Add method
       // TODO: GetLast(n) method
       // TODO: IEnumerable implementation
   }
   ```

2. **DI Registration**:
   ```csharp
   services.AddTransient<IAgentOutputBuffer>(sp =>
       new AgentOutputBuffer(maxLines: 10000));
   ```

**Acceptance Criteria**:
- [ ] Circular buffer handles overflow correctly
- [ ] Enumeration returns items in order
- [ ] Memory usage bounded by max lines
- [ ] Registered in DI container

---

## Phase 1.5: Unit Tests for Core Components

**Estimate**: 4-5 hours

**Goal**: Comprehensive unit tests with >80% coverage

### Task 1.5A: TerminalAgentConnector Tests (2 hours)

**Test Files**:
```
src/Orchestra.Tests/Services/Connectors/TerminalAgentConnectorTests.cs
```

**Test Cases**:
1. **Connection Tests**:
   - `ConnectAsync_ValidParams_ReturnsSuccess()`
   - `ConnectAsync_InvalidSocket_ReturnsError()`
   - `ConnectAsync_Timeout_ThrowsTimeoutException()`
   - `ConnectAsync_SetsStatusToConnected()`
   - `ConnectAsync_RaisesStatusChangedEvent()`

2. **Command Tests**:
   - `SendCommandAsync_WhenConnected_SendsSuccessfully()`
   - `SendCommandAsync_WhenDisconnected_ReturnsError()`
   - `SendCommandAsync_LargeCommand_HandlesCorrectly()`

3. **Output Reading Tests**:
   - `ReadOutputAsync_ReturnsBufferedLines()`
   - `ReadOutputAsync_HandlesEmptyBuffer()`
   - `ReadOutputAsync_CancellationStops()`

4. **Disconnection Tests**:
   - `DisconnectAsync_ClosesConnection()`
   - `DisconnectAsync_CleansUpResources()`
   - `Dispose_ReleasesAllResources()`

**Acceptance Criteria**:
- [ ] All test cases pass
- [ ] Mocked dependencies (ILogger, IAgentOutputBuffer)
- [ ] >85% code coverage

### Task 1.5B: AgentSessionManager Tests (1-2 hours)

**Test Files**:
```
src/Orchestra.Tests/Services/Connectors/AgentSessionManagerTests.cs
```

**Test Cases**:
1. **Session Creation**:
   - `CreateSessionAsync_ValidParams_CreatesSession()`
   - `CreateSessionAsync_GeneratesUniqueIds()`
   - `CreateSessionAsync_ConnectsToAgent()`

2. **Session Management**:
   - `GetSession_ExistingId_ReturnsSession()`
   - `GetSession_UpdatesLastActivity()`
   - `CloseSessionAsync_RemovesSession()`
   - `CloseSessionAsync_DisconnectsAgent()`

3. **Cleanup Service**:
   - `CleanupService_RemovesExpiredSessions()`
   - `CleanupService_KeepsActiveSessions()`
   - `StopAsync_ClosesAllSessions()`

**Acceptance Criteria**:
- [ ] Session lifecycle tested
- [ ] Cleanup logic verified
- [ ] Thread-safety tested

### Task 1.5C: AgentOutputBuffer Tests (1 hour)

**Test Files**:
```
src/Orchestra.Tests/Services/Connectors/AgentOutputBufferTests.cs
```

**Test Cases**:
1. **Buffer Operations**:
   - `AppendLineAsync_AddsToBuffer()`
   - `AppendLineAsync_RaisesEvent()`
   - `AppendLineAsync_HandlesOverflow()`

2. **Retrieval Operations**:
   - `GetLastLinesAsync_ReturnsCorrectCount()`
   - `GetLinesAsync_FiltersWithRegex()`
   - `ClearAsync_EmptiesBuffer()`

3. **Thread Safety**:
   - `ConcurrentAppends_MaintainIntegrity()`
   - `ConcurrentReadWrite_NoDeadlock()`

**Acceptance Criteria**:
- [ ] Buffer overflow handled correctly
- [ ] Thread-safety verified
- [ ] Event firing tested

---

## Integration Points Checklist

### DI Registration (Program.cs)
```csharp
// Add to Program.cs or ServiceCollectionExtensions
services.AddTransient<TerminalAgentConnector>();
services.AddTransient<IAgentOutputBuffer, AgentOutputBuffer>();
services.AddSingleton<IAgentSessionManager, AgentSessionManager>();
services.AddHostedService<AgentSessionManager>();
```

### Entity Framework (if persisting sessions)
```csharp
// Optional: Add to ApplicationDbContext if persisting
public DbSet<AgentSessionEntity> AgentSessions { get; set; }

// Migration needed if persisting
dotnet ef migrations add AddAgentSessions
```

### Health Checks
```csharp
// Add to health checks
services.AddHealthChecks()
    .AddCheck<SessionManagerHealthCheck>("agent_sessions");
```

---

## Validation Criteria for Phase 1

### Technical Completeness
- [ ] All interfaces implemented
- [ ] Cross-platform socket handling works
- [ ] Session management thread-safe
- [ ] Buffer handles high throughput
- [ ] Unit tests >80% coverage

### Integration Readiness
- [ ] All services registered in DI
- [ ] Logging implemented throughout
- [ ] Error handling comprehensive
- [ ] Memory management verified

### Performance Targets
- [ ] Connection time <1 second
- [ ] Command latency <10ms
- [ ] Buffer handles 1000+ lines/sec
- [ ] Memory <10MB per session

---

**Next Phase**: [Phase-2-SignalR-Integration.md](./Phase-2-SignalR-Integration.md)