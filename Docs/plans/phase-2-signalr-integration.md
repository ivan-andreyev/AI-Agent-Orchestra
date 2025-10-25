# Phase 2: SignalR Integration

**Parent Plan**: [Agent-Interaction-System-Implementation-Plan.md](./Agent-Interaction-System-Implementation-Plan.md)

**Goal**: Integrate core components with SignalR for real-time communication

**Total Estimate**: 10-13 hours

**Dependencies**: Phase 1 must be complete (IAgentConnector, SessionManager, OutputBuffer)

---

## Phase 2.1: AgentInteractionHub Creation

**Estimate**: 4-5 hours

**Goal**: Create SignalR hub for real-time agent communication

### Task 2.1A: Create AgentInteractionHub Base (1-2 hours)

**Technical Steps**:

1. **Create hub class**:
   ```csharp
   // File: src/Orchestra.API/Hubs/AgentInteractionHub.cs
   namespace Orchestra.API.Hubs
   {
       public class AgentInteractionHub : Hub
       {
           private readonly IAgentSessionManager _sessionManager;
           private readonly ILogger<AgentInteractionHub> _logger;
           private readonly IMediator _mediator;

           public AgentInteractionHub(
               IAgentSessionManager sessionManager,
               ILogger<AgentInteractionHub> logger,
               IMediator mediator)
           {
               _sessionManager = sessionManager;
               _logger = logger;
               _mediator = mediator;
           }
       }
   }
   ```

2. **Create hub DTOs**:
   ```csharp
   // File: src/Orchestra.API/Hubs/Models/AgentHubModels.cs
   public record ConnectToAgentRequest(
       string AgentId,
       string ConnectorType,
       Dictionary<string, string> ConnectionParams);

   public record ConnectToAgentResponse(
       string SessionId,
       bool Success,
       string? ErrorMessage);

   public record SendCommandRequest(
       string SessionId,
       string Command);

   public record CommandSentNotification(
       string SessionId,
       string Command,
       bool Success,
       DateTime Timestamp);
   ```

3. **Configure SignalR in Program.cs**:
   ```csharp
   // Add SignalR services
   builder.Services.AddSignalR(options =>
   {
       options.EnableDetailedErrors = true;
       options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
       options.StreamBufferCapacity = 100;
   });

   // Register hub dependencies
   builder.Services.AddScoped<AgentInteractionHub>();
   builder.Services.AddSingleton<IAgentSessionManager, AgentSessionManager>();
   builder.Services.AddSingleton<IConnectionTracker, ConnectionTracker>();

   // Map hub endpoint
   app.MapHub<AgentInteractionHub>("/hubs/agent-interaction");
   ```

4. **Add CORS for SignalR**:
   ```csharp
   builder.Services.AddCors(options =>
   {
       options.AddPolicy("SignalRPolicy", policy =>
       {
           policy.WithOrigins("https://localhost:5001", "http://localhost:5000")
                 .AllowAnyHeader()
                 .AllowAnyMethod()
                 .AllowCredentials();
       });
   });
   ```

**Acceptance Criteria**:
- [ ] Hub class created with dependencies
- [ ] DTOs define request/response models
- [ ] SignalR configured in startup
- [ ] CORS configured for client origins

### Task 2.1B: Implement Connection Management (2-3 hours)

**Technical Steps**:

1. **Implement ConnectToAgent method**:
   ```csharp
   public async Task<ConnectToAgentResponse> ConnectToAgent(ConnectToAgentRequest request)
   {
       try
       {
           _logger.LogInformation("Connecting to agent {AgentId} via {ConnectorType}",
               request.AgentId, request.ConnectorType);

           // TODO: Validate user authorization
           var userId = Context.UserIdentifier ?? Context.ConnectionId;

           // TODO: Create connection parameters
           var connectionParams = new AgentConnectionParams
           {
               Parameters = request.ConnectionParams
           };

           // TODO: Create session via manager
           var session = await _sessionManager.CreateSessionAsync(
               request.AgentId,
               request.ConnectorType,
               connectionParams,
               Context.ConnectionAborted);

           // TODO: Add to SignalR group for broadcast
           await Groups.AddToGroupAsync(
               Context.ConnectionId,
               $"agent_session_{session.SessionId}");

           // TODO: Track connection in hub context
           Context.Items["SessionId"] = session.SessionId;

           return new ConnectToAgentResponse(
               session.SessionId,
               true,
               null);
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Failed to connect to agent");
           return new ConnectToAgentResponse(
               string.Empty,
               false,
               ex.Message);
       }
   }
   ```

2. **Implement DisconnectFromAgent method**:
   ```csharp
   public async Task<bool> DisconnectFromAgent(string sessionId)
   {
       try
       {
           _logger.LogInformation("Disconnecting from session {SessionId}", sessionId);

           // TODO: Close session
           var closed = await _sessionManager.CloseSessionAsync(sessionId);

           // TODO: Remove from SignalR group
           await Groups.RemoveFromGroupAsync(
               Context.ConnectionId,
               $"agent_session_{sessionId}");

           // TODO: Notify group members
           await Clients.Group($"agent_session_{sessionId}")
               .SendAsync("SessionClosed", sessionId);

           return closed;
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Failed to disconnect from agent");
           return false;
       }
   }
   ```

3. **Implement connection lifecycle handling**:
   ```csharp
   public override async Task OnConnectedAsync()
   {
       _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
       await base.OnConnectedAsync();
   }

   public override async Task OnDisconnectedAsync(Exception? exception)
   {
       _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);

       // TODO: Clean up any active sessions for this connection
       if (Context.Items.TryGetValue("SessionId", out var sessionIdObj) &&
           sessionIdObj is string sessionId)
       {
           await _sessionManager.CloseSessionAsync(sessionId);
       }

       await base.OnDisconnectedAsync(exception);
   }
   ```

**Integration Points**:
- [ ] Session creation via IAgentSessionManager
- [ ] SignalR group management for broadcasts
- [ ] Connection context tracking
- [ ] Proper cleanup on disconnect

**Acceptance Criteria**:
- [ ] Clients can connect to agents
- [ ] Sessions tracked per connection
- [ ] Disconnection cleans up resources
- [ ] Groups used for session broadcasts

### Task 2.1C: Implement Command Sending (1 hour)

**Technical Steps**:

1. **Implement SendCommand method**:
   ```csharp
   public async Task SendCommand(SendCommandRequest request)
   {
       try
       {
           _logger.LogInformation("Sending command to session {SessionId}", request.SessionId);

           // TODO: Get session from manager
           var session = _sessionManager.GetSession(request.SessionId);
           if (session == null)
           {
               await Clients.Caller.SendAsync("CommandError",
                   new { request.SessionId, Error = "Session not found" });
               return;
           }

           // TODO: Send command via connector
           var result = await session.Connector.SendCommandAsync(
               request.Command,
               Context.ConnectionAborted);

           // TODO: Notify all group members
           var notification = new CommandSentNotification(
               request.SessionId,
               request.Command,
               result.Success,
               DateTime.UtcNow);

           await Clients.Group($"agent_session_{request.SessionId}")
               .SendAsync("CommandSent", notification);

           // TODO: Publish domain event for audit
           await _mediator.Publish(new AgentCommandExecutedEvent(
               request.SessionId,
               request.Command,
               result.Success));
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Failed to send command");
           await Clients.Caller.SendAsync("CommandError",
               new { request.SessionId, Error = ex.Message });
       }
   }
   ```

2. **Add command validation**:
   ```csharp
   private static readonly HashSet<string> DangerousCommands = new()
   {
       "rm -rf", "format", "shutdown", "reboot", "del /f"
   };

   private bool IsCommandSafe(string command)
   {
       var lowerCommand = command.ToLowerInvariant();
       return !DangerousCommands.Any(dangerous =>
           lowerCommand.Contains(dangerous));
   }
   ```

**Acceptance Criteria**:
- [ ] Commands sent to correct session
- [ ] Dangerous commands blocked
- [ ] Group notifications work
- [ ] Errors handled gracefully

---

## Phase 2.2: Streaming Implementation

**Estimate**: 3-4 hours

**Goal**: Implement real-time output streaming using IAsyncEnumerable

### Task 2.2A: Implement Output Streaming Hub Method (2 hours)

**Technical Steps**:

1. **Implement StreamOutput method**:
   ```csharp
   public async IAsyncEnumerable<string> StreamOutput(
       string sessionId,
       string? filter,
       [EnumeratorCancellation] CancellationToken cancellationToken = default)
   {
       _logger.LogInformation("Starting output stream for session {SessionId}", sessionId);

       // TODO: Get session
       var session = _sessionManager.GetSession(sessionId);
       if (session == null)
       {
           yield return $"[ERROR] Session {sessionId} not found";
           yield break;
       }

       // TODO: Subscribe to new lines via event
       var tcs = new TaskCompletionSource<string>();
       var queue = Channel.CreateUnbounded<string>();

       void OnLineAdded(object? sender, OutputLineAddedEventArgs e)
       {
           if (filter == null || Regex.IsMatch(e.Line, filter))
           {
               queue.Writer.TryWrite(e.Line);
           }
       }

       session.OutputBuffer.LineAdded += OnLineAdded;

       try
       {
           // TODO: Stream existing lines first
           await foreach (var line in session.OutputBuffer.GetLinesAsync(filter, cancellationToken))
           {
               yield return line;
           }

           // TODO: Stream new lines as they arrive
           await foreach (var line in queue.Reader.ReadAllAsync(cancellationToken))
           {
               yield return line;
           }
       }
       finally
       {
           session.OutputBuffer.LineAdded -= OnLineAdded;
           queue.Writer.Complete();
       }
   }
   ```

2. **Implement batch output retrieval**:
   ```csharp
   public async Task<List<string>> GetRecentOutput(string sessionId, int lineCount = 100)
   {
       var session = _sessionManager.GetSession(sessionId);
       if (session == null)
       {
           return new List<string> { $"[ERROR] Session {sessionId} not found" };
       }

       var lines = await session.OutputBuffer.GetLastLinesAsync(
           lineCount,
           Context.ConnectionAborted);

       return lines.ToList();
   }
   ```

**Acceptance Criteria**:
- [ ] Streaming returns existing + new lines
- [ ] Filter parameter works with regex
- [ ] Cancellation stops streaming
- [ ] Memory efficient with channels

### Task 2.2B: Implement Backpressure and Flow Control (1-2 hours)

**Technical Steps**:

1. **Add streaming configuration**:
   ```csharp
   public class StreamingOptions
   {
       public int BufferSize { get; set; } = 100;
       public int MaxConcurrentStreams { get; set; } = 10;
       public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
   }
   ```

2. **Implement rate limiting**:
   ```csharp
   private readonly SemaphoreSlim _streamSemaphore = new(10); // Max 10 concurrent streams

   public async IAsyncEnumerable<string> StreamOutputWithRateLimit(
       string sessionId,
       [EnumeratorCancellation] CancellationToken cancellationToken)
   {
       await _streamSemaphore.WaitAsync(cancellationToken);
       try
       {
           await foreach (var line in StreamOutput(sessionId, null, cancellationToken))
           {
               yield return line;

               // TODO: Add small delay for rate limiting if needed
               if (DateTime.UtcNow.Millisecond % 10 == 0)
               {
                   await Task.Delay(1, cancellationToken);
               }
           }
       }
       finally
       {
           _streamSemaphore.Release();
       }
   }
   ```

3. **Add keep-alive for long streams**:
   ```csharp
   private async IAsyncEnumerable<string> StreamWithKeepAlive(
       IAsyncEnumerable<string> source,
       [EnumeratorCancellation] CancellationToken cancellationToken)
   {
       var lastMessage = DateTime.UtcNow;
       var keepAliveInterval = TimeSpan.FromSeconds(30);

       await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

       while (!cancellationToken.IsCancellationRequested)
       {
           var hasNext = await enumerator.MoveNextAsync();

           if (hasNext)
           {
               yield return enumerator.Current;
               lastMessage = DateTime.UtcNow;
           }
           else if (DateTime.UtcNow - lastMessage > keepAliveInterval)
           {
               yield return "[KEEPALIVE]";
               lastMessage = DateTime.UtcNow;
           }
           else
           {
               await Task.Delay(100, cancellationToken);
           }
       }
   }
   ```

**Acceptance Criteria**:
- [ ] Stream count limited to prevent overload
- [ ] Rate limiting prevents client flooding
- [ ] Keep-alive maintains connection
- [ ] Graceful degradation under load

---

## Phase 2.3: Integration Tests

**Estimate**: 3-4 hours

**Goal**: Comprehensive integration tests for SignalR hub

### Task 2.3A: Setup Test Infrastructure (1 hour)

**Technical Steps**:

1. **Create test base class**:
   ```csharp
   // File: src/Orchestra.Tests/Hubs/AgentInteractionHubTestBase.cs
   public class AgentInteractionHubTestBase
   {
       protected TestServer Server { get; private set; }
       protected HubConnection Connection { get; private set; }

       protected async Task InitializeAsync()
       {
           var builder = WebApplication.CreateBuilder();

           // TODO: Configure test services
           builder.Services.AddSignalR();
           builder.Services.AddSingleton<IAgentSessionManager>(MockSessionManager());

           var app = builder.Build();
           app.MapHub<AgentInteractionHub>("/hubs/agent-interaction");

           Server = new TestServer(app);

           // TODO: Create hub connection
           Connection = new HubConnectionBuilder()
               .WithUrl($"http://localhost/hubs/agent-interaction", options =>
               {
                   options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
               })
               .Build();

           await Connection.StartAsync();
       }

       protected IAgentSessionManager MockSessionManager()
       {
           var mock = new Mock<IAgentSessionManager>();
           // TODO: Setup mock behavior
           return mock.Object;
       }
   }
   ```

2. **Add test helpers**:
   ```csharp
   protected async Task<T> InvokeAsync<T>(string method, params object[] args)
   {
       return await Connection.InvokeAsync<T>(method, args);
   }

   protected async Task SubscribeToStream<T>(
       string method,
       Action<T> handler,
       params object[] args)
   {
       await foreach (var item in Connection.StreamAsync<T>(method, args))
       {
           handler(item);
       }
   }
   ```

**Acceptance Criteria**:
- [ ] Test server configured correctly
- [ ] Hub connection established in tests
- [ ] Mock services injected
- [ ] Helper methods for hub calls

### Task 2.3B: Write Connection and Command Tests (1-2 hours)

**Test Files**:
```
src/Orchestra.Tests/Hubs/AgentInteractionHubTests.cs
```

**Test Cases**:

1. **Connection Tests**:
   ```csharp
   [Fact]
   public async Task ConnectToAgent_ValidRequest_ReturnsSessionId()
   {
       // Arrange
       var request = new ConnectToAgentRequest("agent1", "terminal", new());

       // Act
       var response = await InvokeAsync<ConnectToAgentResponse>("ConnectToAgent", request);

       // Assert
       response.Success.Should().BeTrue();
       response.SessionId.Should().NotBeNullOrEmpty();
   }

   [Fact]
   public async Task DisconnectFromAgent_ValidSession_ReturnsTrue()
   {
       // Arrange
       var connectResponse = await ConnectToTestAgent();

       // Act
       var result = await InvokeAsync<bool>("DisconnectFromAgent", connectResponse.SessionId);

       // Assert
       result.Should().BeTrue();
   }
   ```

2. **Command Tests**:
   ```csharp
   [Fact]
   public async Task SendCommand_ValidSession_BroadcastsToGroup()
   {
       // Arrange
       var sessionId = await ConnectToTestAgent();
       var commandSent = new TaskCompletionSource<CommandSentNotification>();

       Connection.On<CommandSentNotification>("CommandSent", notification =>
       {
           commandSent.SetResult(notification);
       });

       // Act
       await Connection.InvokeAsync("SendCommand",
           new SendCommandRequest(sessionId, "test command"));

       // Assert
       var notification = await commandSent.Task.WaitAsync(TimeSpan.FromSeconds(5));
       notification.Command.Should().Be("test command");
       notification.Success.Should().BeTrue();
   }

   [Fact]
   public async Task SendCommand_DangerousCommand_Rejected()
   {
       // Test dangerous command rejection
   }
   ```

**Acceptance Criteria**:
- [ ] Connection lifecycle tested
- [ ] Command sending verified
- [ ] Group broadcasting tested
- [ ] Error scenarios covered

### Task 2.3C: Write Streaming Tests (1 hour)

**Test Cases**:

1. **Streaming Tests**:
   ```csharp
   [Fact]
   public async Task StreamOutput_ReturnsLines()
   {
       // Arrange
       var sessionId = await ConnectToTestAgent();
       var lines = new List<string>();

       // Act
       await foreach (var line in Connection.StreamAsync<string>(
           "StreamOutput", sessionId, null))
       {
           lines.Add(line);
           if (lines.Count >= 10) break;
       }

       // Assert
       lines.Should().HaveCountGreaterThan(0);
   }

   [Fact]
   public async Task StreamOutput_WithFilter_FiltersCorrectly()
   {
       // Arrange
       var sessionId = await ConnectToTestAgent();
       var filter = "^ERROR";

       // Act
       var lines = await Connection.StreamAsync<string>(
           "StreamOutput", sessionId, filter)
           .Take(5)
           .ToListAsync();

       // Assert
       lines.Should().OnlyContain(l => l.StartsWith("ERROR"));
   }

   [Fact]
   public async Task StreamOutput_Cancellation_StopsStreaming()
   {
       // Test cancellation behavior
   }
   ```

2. **Performance Tests**:
   ```csharp
   [Fact]
   public async Task StreamOutput_HighVolume_MaintainsPerformance()
   {
       // Arrange
       var sessionId = await ConnectToTestAgent();
       var stopwatch = Stopwatch.StartNew();
       var lineCount = 0;

       // Act
       await foreach (var line in Connection.StreamAsync<string>(
           "StreamOutput", sessionId, null)
           .WithCancellation(new CancellationTokenSource(5000).Token))
       {
           lineCount++;
       }

       // Assert
       var linesPerSecond = lineCount / (stopwatch.ElapsedMilliseconds / 1000.0);
       linesPerSecond.Should().BeGreaterThan(100); // At least 100 lines/sec
   }
   ```

**Acceptance Criteria**:
- [ ] Streaming returns data correctly
- [ ] Filters work as expected
- [ ] Cancellation stops streams
- [ ] Performance meets targets

---

## Integration Points Checklist

### SignalR Configuration (Program.cs)
```csharp
// SignalR services
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
    options.StreamBufferCapacity = 100;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// CORS for SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy.WithOrigins(
                "https://localhost:5001",
                "http://localhost:5000",
                "http://localhost:3000") // React dev server
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Hub endpoint mapping
app.UseRouting();
app.UseCors("SignalRPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<AgentInteractionHub>("/hubs/agent-interaction")
   .RequireAuthorization(); // Optional: require auth
```

### Swagger Documentation
```csharp
// Exclude SignalR hubs from Swagger
services.AddSwaggerGen(c =>
{
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        return !apiDesc.RelativePath.Contains("hubs/");
    });
});
```

### Health Checks
```csharp
services.AddHealthChecks()
    .AddCheck("signalr", () =>
    {
        // Check SignalR is responsive
        return HealthCheckResult.Healthy();
    });
```

---

## Validation Criteria for Phase 2

### Technical Completeness
- [ ] Hub methods handle all operations
- [ ] Streaming works with backpressure
- [ ] Group broadcasting implemented
- [ ] Integration tests pass

### Performance Requirements
- [ ] Stream latency <100ms
- [ ] Handle 1000+ lines/sec
- [ ] Support 10+ concurrent streams
- [ ] Memory usage stable

### Security Requirements
- [ ] Dangerous commands blocked
- [ ] User authorization checked
- [ ] Sessions properly isolated
- [ ] CORS configured correctly

---

**Next Phase**: [Phase-3-Frontend-Component.md](./Phase-3-Frontend-Component.md)