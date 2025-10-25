# Phase 4: Testing & Documentation

**Parent Plan**: [Agent-Interaction-System-Implementation-Plan.md](./Agent-Interaction-System-Implementation-Plan.md)

**Goal**: Comprehensive testing and documentation for production readiness

**Total Estimate**: 8-11 hours

**Dependencies**: Phases 1-3 must be complete (full system functional)

---

## Phase 4.1: End-to-End Testing

**Estimate**: 3-4 hours

**Goal**: Verify complete system functionality with real agent connections

### Task 4.1A: Setup E2E Test Environment (1 hour)

**Technical Steps**:

1. **Create E2E test project**:
   ```xml
   <!-- File: src/Orchestra.E2ETests/Orchestra.E2ETests.csproj -->
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net9.0</TargetFramework>
       <IsPackable>false</IsPackable>
     </PropertyGroup>

     <ItemGroup>
       <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
       <PackageReference Include="Playwright" Version="1.40.0" />
       <PackageReference Include="xunit" Version="2.6.1" />
       <PackageReference Include="FluentAssertions" Version="6.12.0" />
     </ItemGroup>

     <ItemGroup>
       <ProjectReference Include="../Orchestra.API/Orchestra.API.csproj" />
       <ProjectReference Include="../Orchestra.Web/Orchestra.Web.csproj" />
     </ItemGroup>
   </Project>
   ```

2. **Create test fixture with mock agent**:
   ```csharp
   // File: src/Orchestra.E2ETests/Fixtures/AgentTestFixture.cs
   public class AgentTestFixture : IAsyncLifetime
   {
       private Process? _mockAgentProcess;
       private string? _socketPath;

       public async Task InitializeAsync()
       {
           // TODO: Create Unix Domain Socket server
           _socketPath = Path.Combine(Path.GetTempPath(), $"test_agent_{Guid.NewGuid()}.sock");

           // TODO: Start mock agent process
           _mockAgentProcess = new Process
           {
               StartInfo = new ProcessStartInfo
               {
                   FileName = "dotnet",
                   Arguments = $"run --project ../MockAgent/MockAgent.csproj -- --socket {_socketPath}",
                   CreateNoWindow = true,
                   RedirectStandardOutput = true,
                   RedirectStandardError = true
               }
           };

           _mockAgentProcess.Start();

           // Wait for socket to be ready
           await WaitForSocketAsync(_socketPath);
       }

       public async Task DisposeAsync()
       {
           _mockAgentProcess?.Kill();
           _mockAgentProcess?.Dispose();

           if (File.Exists(_socketPath))
           {
               File.Delete(_socketPath);
           }
       }

       private async Task WaitForSocketAsync(string path, int timeoutMs = 5000)
       {
           var stopwatch = Stopwatch.StartNew();
           while (!File.Exists(path) && stopwatch.ElapsedMilliseconds < timeoutMs)
           {
               await Task.Delay(100);
           }

           if (!File.Exists(path))
           {
               throw new TimeoutException($"Socket {path} not created within timeout");
           }
       }

       public string GetSocketPath() => _socketPath!;
   }
   ```

3. **Create mock agent implementation**:
   ```csharp
   // File: src/MockAgent/Program.cs
   public class MockAgent
   {
       public static async Task Main(string[] args)
       {
           var socketPath = args[1]; // Get from command line

           // TODO: Create Unix Domain Socket server
           var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
           var endpoint = new UnixDomainSocketEndPoint(socketPath);
           socket.Bind(endpoint);
           socket.Listen(1);

           Console.WriteLine($"Mock agent listening on {socketPath}");

           while (true)
           {
               var client = await socket.AcceptAsync();
               _ = HandleClientAsync(client);
           }
       }

       private static async Task HandleClientAsync(Socket client)
       {
           using var stream = new NetworkStream(client);
           using var reader = new StreamReader(stream);
           using var writer = new StreamWriter(stream) { AutoFlush = true };

           // TODO: Send welcome message
           await writer.WriteLineAsync("Mock Agent v1.0 - Connected");

           // TODO: Echo commands back with prefix
           string? line;
           while ((line = await reader.ReadLineAsync()) != null)
           {
               await writer.WriteLineAsync($"[ECHO] {line}");
               await writer.WriteLineAsync($"[RESULT] Command executed: {line}");
           }
       }
   }
   ```

**Acceptance Criteria**:
- [ ] E2E test project created
- [ ] Mock agent can be started
- [ ] Socket communication works
- [ ] Test fixture manages lifecycle

### Task 4.1B: Write Connection and Command E2E Tests (1-2 hours) - DECOMPOSED

**⚠️ This task has been decomposed**: See [task-4.1b-e2e-tests.md](./phase-4-testing-documentation/task-4.1b-e2e-tests.md) for detailed subtasks

**Test Files**:
```
src/Orchestra.E2ETests/AgentConnectionE2ETests.cs
```

**Test Cases**:

1. **Full connection flow test**:
   ```csharp
   [Fact]
   public async Task FullConnectionFlow_ConnectSendCommandDisconnect_Success()
   {
       // Arrange
       await using var agent = new AgentTestFixture();
       await agent.InitializeAsync();

       var factory = new WebApplicationFactory<Program>()
           .WithWebHostBuilder(builder =>
           {
               builder.ConfigureServices(services =>
               {
                   // Override with test services if needed
               });
           });

       var client = factory.CreateClient();
       var hubConnection = new HubConnectionBuilder()
           .WithUrl($"{client.BaseAddress}hubs/agent-interaction", options =>
           {
               options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
           })
           .Build();

       await hubConnection.StartAsync();

       // Act - Connect
       var connectRequest = new ConnectToAgentRequest(
           "test-agent",
           "terminal",
           new Dictionary<string, string>
           {
               ["socketPath"] = agent.GetSocketPath()
           });

       var connectResponse = await hubConnection.InvokeAsync<ConnectToAgentResponse>(
           "ConnectToAgent",
           connectRequest);

       // Assert - Connection
       connectResponse.Success.Should().BeTrue();
       connectResponse.SessionId.Should().NotBeNullOrEmpty();

       // Act - Send Command
       var command = "test command";
       await hubConnection.InvokeAsync("SendCommand",
           new SendCommandRequest(connectResponse.SessionId, command));

       // Collect output
       var outputLines = new List<string>();
       var outputTask = Task.Run(async () =>
       {
           await foreach (var line in hubConnection.StreamAsync<string>(
               "StreamOutput",
               connectResponse.SessionId,
               null))
           {
               outputLines.Add(line);
               if (outputLines.Count >= 3) break;
           }
       });

       await outputTask.WaitAsync(TimeSpan.FromSeconds(5));

       // Assert - Output
       outputLines.Should().Contain(line => line.Contains("Mock Agent"));
       outputLines.Should().Contain(line => line.Contains(command));

       // Act - Disconnect
       var disconnectResult = await hubConnection.InvokeAsync<bool>(
           "DisconnectFromAgent",
           connectResponse.SessionId);

       // Assert - Disconnection
       disconnectResult.Should().BeTrue();

       await hubConnection.DisposeAsync();
   }
   ```

2. **Multiple concurrent sessions test**:
   ```csharp
   [Fact]
   public async Task MultipleSessions_ConcurrentConnections_AllWork()
   {
       // Arrange
       const int sessionCount = 5;
       var tasks = new List<Task<bool>>();

       // Act
       for (int i = 0; i < sessionCount; i++)
       {
           tasks.Add(CreateAndTestSessionAsync($"agent-{i}"));
       }

       var results = await Task.WhenAll(tasks);

       // Assert
       results.Should().AllBeEquivalentTo(true);
   }

   private async Task<bool> CreateAndTestSessionAsync(string agentId)
   {
       // TODO: Create connection
       // TODO: Send command
       // TODO: Verify output
       // TODO: Disconnect
       return true;
   }
   ```

3. **Reconnection test**:
   ```csharp
   [Fact]
   public async Task Reconnection_AfterNetworkFailure_RestoresSession()
   {
       // TODO: Connect to agent
       // TODO: Simulate network failure
       // TODO: Verify reconnection
       // TODO: Verify session still works
   }
   ```

**Acceptance Criteria**:
- [ ] Full flow test passes
- [ ] Multiple sessions work
- [ ] Reconnection handled
- [ ] Commands execute correctly

### Task 4.1C: Write UI Integration Tests with Playwright (1 hour)

**Test Files**:
```
src/Orchestra.E2ETests/UI/TerminalComponentUITests.cs
```

**Test Cases**:

1. **UI interaction test**:
   ```csharp
   [Fact]
   public async Task TerminalUI_ConnectAndSendCommand_DisplaysOutput()
   {
       // Arrange
       using var playwright = await Playwright.CreateAsync();
       await using var browser = await playwright.Chromium.LaunchAsync(new()
       {
           Headless = true
       });

       var page = await browser.NewPageAsync();
       await page.GotoAsync("https://localhost:5001/terminal");

       // Act - Open connection dialog
       await page.ClickAsync("button:has-text('Connect')");
       await page.FillAsync("input[placeholder='Agent ID']", "test-agent");
       await page.SelectOptionAsync("select", "terminal");
       await page.ClickAsync("button:has-text('Connect')");

       // Wait for connection
       await page.WaitForSelectorAsync(".connection-status.connected");

       // Act - Send command
       await page.FillAsync(".terminal-input", "echo Hello World");
       await page.PressAsync(".terminal-input", "Enter");

       // Assert - Output appears
       var outputLine = await page.WaitForSelectorAsync(
           ".terminal-line:has-text('Hello World')",
           new() { Timeout = 5000 });

       outputLine.Should().NotBeNull();

       // Act - Disconnect
       await page.ClickAsync("button:has-text('Disconnect')");

       // Assert - Status changes
       await page.WaitForSelectorAsync(".connection-status.disconnected");
   }
   ```

2. **Command history test**:
   ```csharp
   [Fact]
   public async Task CommandHistory_ArrowKeys_NavigatesHistory()
   {
       // TODO: Send multiple commands
       // TODO: Press arrow up
       // TODO: Verify previous command appears
       // TODO: Press arrow down
       // TODO: Verify next command appears
   }
   ```

**Acceptance Criteria**:
- [ ] UI loads correctly
- [ ] Connection dialog works
- [ ] Output displays properly
- [ ] Commands can be sent

---

## Phase 4.2: Performance Testing

**Estimate**: 2-3 hours

**Goal**: Verify system meets performance requirements

### Task 4.2A: Load Testing with NBomber (1-2 hours)

**Technical Steps**:

1. **Create load test project**:
   ```csharp
   // File: src/Orchestra.LoadTests/AgentLoadTests.cs
   using NBomber.CSharp;
   using NBomber.Http.CSharp;

   public class AgentLoadTests
   {
       [Fact]
       public void HighVolumeOutput_1000LinesPerSecond_HandledCorrectly()
       {
           var scenario = Scenario.Create("high_volume_output", async context =>
           {
               // TODO: Connect to agent
               var hubConnection = await CreateHubConnection();
               var sessionId = await ConnectToAgent(hubConnection);

               // TODO: Generate high volume output
               for (int i = 0; i < 1000; i++)
               {
                   await hubConnection.InvokeAsync("SendCommand",
                       new SendCommandRequest(sessionId, $"echo Line {i}"));
               }

               // TODO: Measure throughput
               var stopwatch = Stopwatch.StartNew();
               var lineCount = 0;

               await foreach (var line in hubConnection.StreamAsync<string>(
                   "StreamOutput", sessionId, null)
                   .WithCancellation(new CancellationTokenSource(5000).Token))
               {
                   lineCount++;
               }

               stopwatch.Stop();

               var throughput = lineCount / (stopwatch.ElapsedMilliseconds / 1000.0);

               return throughput > 1000 ? Response.Ok() : Response.Fail();
           })
           .WithLoadSimulations(
               Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromSeconds(30))
           );

           var stats = NBomberRunner
               .RegisterScenarios(scenario)
               .Run();

           // Assert
           stats.AllOkCount.Should().BeGreaterThan(0);
           stats.AllFailCount.Should().Be(0);
       }
   }
   ```

2. **Memory usage test**:
   ```csharp
   [Fact]
   public async Task MemoryUsage_10ConcurrentSessions_Under100MB()
   {
       // Arrange
       var initialMemory = GC.GetTotalMemory(true);
       var sessions = new List<string>();

       // Act - Create 10 sessions
       for (int i = 0; i < 10; i++)
       {
           var sessionId = await CreateSessionWithHighVolume();
           sessions.Add(sessionId);
       }

       // Generate traffic
       await Task.Delay(TimeSpan.FromMinutes(1));

       // Measure memory
       GC.Collect();
       GC.WaitForPendingFinalizers();
       GC.Collect();

       var finalMemory = GC.GetTotalMemory(false);
       var memoryUsedMB = (finalMemory - initialMemory) / (1024 * 1024);

       // Assert
       memoryUsedMB.Should().BeLessThan(100);

       // Cleanup
       foreach (var sessionId in sessions)
       {
           await DisconnectSession(sessionId);
       }
   }
   ```

**Acceptance Criteria**:
- [ ] Throughput >1000 lines/sec
- [ ] Memory <100MB for 10 sessions
- [ ] No memory leaks detected
- [ ] Response time <100ms

### Task 4.2B: Stress Testing and Failure Modes (1 hour)

**Test Cases**:

1. **Buffer overflow test**:
   ```csharp
   [Fact]
   public async Task BufferOverflow_10000Lines_HandledGracefully()
   {
       // TODO: Send 10,000+ lines rapidly
       // TODO: Verify buffer limits work
       // TODO: Verify no crashes
       // TODO: Verify old lines removed
   }
   ```

2. **Connection failure recovery**:
   ```csharp
   [Fact]
   public async Task ConnectionFailure_DuringStreaming_RecoversGracefully()
   {
       // TODO: Start streaming
       // TODO: Kill agent process
       // TODO: Verify error handling
       // TODO: Verify cleanup occurs
   }
   ```

3. **Resource exhaustion test**:
   ```csharp
   [Fact]
   public async Task ResourceExhaustion_MaxSessions_RejectsNew()
   {
       // TODO: Create maximum sessions
       // TODO: Try to create one more
       // TODO: Verify rejection
       // TODO: Verify error message
   }
   ```

**Acceptance Criteria**:
- [ ] Buffer overflow handled
- [ ] Connection failures recovered
- [ ] Resource limits enforced
- [ ] Graceful degradation

---

## Phase 4.3: Documentation

**Estimate**: 3-4 hours

**Goal**: Complete user and developer documentation

### Task 4.3A: API Documentation (1 hour)

**Technical Steps**:

1. **Add XML documentation to all public APIs**:
   ```csharp
   /// <summary>
   /// Manages connection sessions to external agents.
   /// </summary>
   /// <remarks>
   /// The AgentSessionManager maintains active connections to external agents
   /// and provides lifecycle management including automatic cleanup of inactive sessions.
   /// </remarks>
   /// <example>
   /// <code>
   /// var session = await sessionManager.CreateSessionAsync(
   ///     "claude-code-1",
   ///     "terminal",
   ///     new AgentConnectionParams { ... });
   /// </code>
   /// </example>
   public interface IAgentSessionManager
   {
       /// <summary>
       /// Creates a new connection session to an external agent.
       /// </summary>
       /// <param name="agentId">Unique identifier for the agent</param>
       /// <param name="connectorType">Type of connector (terminal, tab, etc.)</param>
       /// <param name="connectionParams">Connection-specific parameters</param>
       /// <param name="cancellationToken">Cancellation token</param>
       /// <returns>Created session with active connection</returns>
       /// <exception cref="ArgumentNullException">Thrown when agentId is null</exception>
       /// <exception cref="NotSupportedException">Thrown when connectorType is not supported</exception>
       /// <exception cref="ConnectionException">Thrown when connection fails</exception>
       Task<AgentSession> CreateSessionAsync(/*...*/);
   }
   ```

2. **Configure Swagger documentation**:
   ```csharp
   builder.Services.AddSwaggerGen(c =>
   {
       c.SwaggerDoc("v1", new OpenApiInfo
       {
           Title = "Orchestra Agent Interaction API",
           Version = "v1",
           Description = "API for real-time interaction with external AI agents",
           Contact = new OpenApiContact
           {
               Name = "Orchestra Team",
               Email = "team@orchestra.ai"
           }
       });

       // Include XML comments
       var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
       var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
       c.IncludeXmlComments(xmlPath);

       // Document SignalR endpoints
       c.DocumentFilter<SignalRSwaggerGen>();
   });
   ```

**Acceptance Criteria**:
- [ ] All public APIs documented
- [ ] Examples provided
- [ ] Exceptions documented
- [ ] Swagger shows descriptions

### Task 4.3B: User Documentation (1-2 hours)

**Create user guide**:
```markdown
<!-- File: Docs/UserGuide/agent-interaction.md -->
# Agent Interaction System User Guide

## Overview
The Agent Interaction System allows Orchestra to connect to and control external AI agents like Claude Code and Cursor through their native terminal interfaces.

## Getting Started

### Connecting to an Agent

1. Navigate to the Terminal page
2. Click the "Connect" button
3. Enter the Agent ID (e.g., "claude-code-1")
4. Select the connector type:
   - **Terminal**: For Claude Code and similar terminal-based agents
   - **Tab**: For Cursor and tab-based agents (future)
5. Click "Connect"

### Sending Commands

Once connected, you can send commands to the agent:

1. Type your command in the input field
2. Press Enter or click Send
3. The command and its output will appear in the terminal

### Terminal Features

- **Auto-scroll**: Automatically scrolls to show new output
- **Clear**: Clears the terminal output
- **Command History**: Use ↑/↓ arrow keys to navigate previous commands
- **Copy**: Select text and use Ctrl+C to copy
- **Filter**: Use regex to filter output (advanced)

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Enter | Send command |
| ↑ | Previous command |
| ↓ | Next command |
| Ctrl+L | Clear output |
| Ctrl+C | Copy selection |
| Esc | Clear input |

## Troubleshooting

### Connection Issues

**Problem**: Cannot connect to agent
**Solutions**:
- Verify the agent is running
- Check the Agent ID is correct
- Ensure Orchestra has permissions to access the agent's socket

### Performance Issues

**Problem**: Terminal is slow or laggy
**Solutions**:
- Enable filtering to reduce output volume
- Clear old output regularly
- Check network connection stability

## Advanced Features

### Output Filtering

Use regular expressions to filter output:
- `^ERROR` - Show only error lines
- `\[INFO\]` - Show info messages
- `command.*failed` - Show failed commands

### Multiple Sessions

You can open multiple terminal windows to different agents:
1. Open new browser tab
2. Navigate to Terminal
3. Connect to different agent

## Security Notes

- Commands are logged for audit purposes
- Dangerous commands (rm -rf, format, etc.) are blocked
- Sessions timeout after 30 minutes of inactivity
```

**Acceptance Criteria**:
- [ ] Getting started guide complete
- [ ] All features documented
- [ ] Troubleshooting section
- [ ] Security notes included

### Task 4.3C: Developer README (1 hour)

**Create developer documentation**:
```markdown
<!-- File: README.md -->
# Agent Interaction System

Real-time bidirectional communication system for Orchestra to interact with external AI agents.

## Architecture

The system uses a layered architecture:

- **Frontend**: Blazor component with SignalR client
- **API**: SignalR hub for real-time communication
- **Core**: Business logic and agent connectors
- **Infrastructure**: Cross-platform IPC (Unix Domain Sockets)

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- Windows 10+ / Linux / macOS
- External agent (Claude Code, Cursor, etc.)

### Installation

1. Clone the repository
2. Build the solution:
   ```bash
   dotnet build
   ```

3. Run tests:
   ```bash
   dotnet test
   ```

4. Start the application:
   ```bash
   dotnet run --project src/Orchestra.API
   ```

## Development

### Project Structure

```
src/
├── Orchestra.Core/
│   └── Services/Connectors/      # Agent connectors
├── Orchestra.API/
│   └── Hubs/                     # SignalR hubs
├── Orchestra.Web/
│   └── Components/AgentTerminal/ # UI components
└── Orchestra.Tests/              # Unit tests
```

### Adding a New Connector

1. Implement `IAgentConnector` interface
2. Register in DI container
3. Add to connector factory in `AgentSessionManager`

Example:
```csharp
public class CustomAgentConnector : IAgentConnector
{
    // Implementation
}

// In Program.cs
services.AddTransient<CustomAgentConnector>();
```

### Testing

Run all tests:
```bash
dotnet test
```

Run specific test category:
```bash
dotnet test --filter Category=Integration
```

Run with coverage:
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Configuration

### appsettings.json

```json
{
  "AgentInteraction": {
    "SessionTimeout": "00:30:00",
    "MaxSessions": 100,
    "BufferSize": 10000,
    "EnableDangerousCommands": false
  }
}
```

## Performance

- **Throughput**: >1000 lines/second
- **Latency**: <100ms (stdout to UI)
- **Memory**: <100MB for 10 sessions
- **Connections**: Up to 100 concurrent

## Troubleshooting

### Common Issues

1. **Socket connection fails**
   - Check permissions on socket file
   - Verify agent is listening

2. **High memory usage**
   - Reduce buffer size in configuration
   - Enable session timeout

3. **Slow performance**
   - Check network latency
   - Reduce logging verbosity

## Contributing

1. Fork the repository
2. Create feature branch
3. Make changes with tests
4. Submit pull request

## License

MIT License - see LICENSE file
```

**Acceptance Criteria**:
- [ ] Architecture explained
- [ ] Quick start guide
- [ ] Development instructions
- [ ] Configuration documented

---

## Final Integration Checklist

### Code Quality
- [ ] All XML documentation complete
- [ ] No compiler warnings
- [ ] Code analysis passed
- [ ] Security scan passed

### Testing
- [ ] Unit tests >80% coverage
- [ ] Integration tests pass
- [ ] E2E tests pass
- [ ] Performance tests pass
- [ ] Load tests pass

### Documentation
- [ ] API documentation generated
- [ ] User guide complete
- [ ] Developer README complete
- [ ] Architecture diagram updated

### Deployment Readiness
- [ ] Configuration externalized
- [ ] Logging configured
- [ ] Health checks working
- [ ] Metrics exposed
- [ ] Docker image builds

---

## Validation Criteria for Phase 4

### Quality Gates
- [ ] Zero critical bugs
- [ ] Test coverage >80%
- [ ] Documentation complete
- [ ] Performance targets met

### Production Readiness
- [ ] Security review passed
- [ ] Monitoring configured
- [ ] Runbook created
- [ ] Rollback plan defined

---

**Plan Complete**: All phases documented with full technical decomposition