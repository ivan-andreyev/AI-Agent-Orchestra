# Task 4.1B: End-to-End Tests Implementation - Decomposed

**Parent Phase**: [phase-4-testing-documentation.md](../phase-4-testing-documentation.md)

**Original Task**: 4.1B - Write Connection and Command E2E Tests (~50 tool calls)

**Goal**: Implement comprehensive E2E tests with proper decomposition

**Total Estimate**: 1-2 hours

---

## Subtask 4.1B.1: Connection Flow Tests

**Estimate**: 30-40 minutes

**Goal**: Test complete connection lifecycle

### Technical Steps:

1. **Create base test fixture**:
   ```csharp
   // File: src/Orchestra.E2ETests/AgentConnectionE2ETests.cs
   using FluentAssertions;
   using Microsoft.AspNetCore.SignalR.Client;
   using Microsoft.AspNetCore.TestHost;
   using Microsoft.Extensions.DependencyInjection;
   using Xunit;

   public class AgentConnectionE2ETests : IClassFixture<WebApplicationFactory<Program>>
   {
       private readonly WebApplicationFactory<Program> _factory;
       private readonly ITestOutputHelper _output;

       public AgentConnectionE2ETests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
       {
           _factory = factory;
           _output = output;
       }

       private HubConnection CreateHubConnection(HttpMessageHandler handler)
       {
           var hubUrl = $"http://localhost/hubs/agent-interaction";
           return new HubConnectionBuilder()
               .WithUrl(hubUrl, options =>
               {
                   options.HttpMessageHandlerFactory = _ => handler;
               })
               .Build();
       }
   }
   ```

2. **Implement connection success test**:
   ```csharp
   [Fact]
   public async Task ConnectToAgent_ValidParams_Success()
   {
       // Arrange
       await using var agent = new AgentTestFixture();
       await agent.InitializeAsync();

       var client = _factory.WithWebHostBuilder(builder =>
       {
           builder.ConfigureServices(services =>
           {
               services.AddSingleton(agent.GetConnector());
           });
       }).CreateClient();

       using var hubConnection = CreateHubConnection(_factory.Server.CreateHandler());
       await hubConnection.StartAsync();

       // Act
       var connectRequest = new ConnectToAgentRequest
       {
           AgentId = "test-agent-1",
           ConnectorType = "terminal",
           ConnectionParams = new Dictionary<string, string>
           {
               ["socketPath"] = agent.GetSocketPath()
           }
       };

       var response = await hubConnection.InvokeAsync<ConnectToAgentResponse>(
           "ConnectToAgent",
           connectRequest);

       // Assert
       response.Should().NotBeNull();
       response.Success.Should().BeTrue();
       response.SessionId.Should().NotBeNullOrEmpty();
       response.Error.Should().BeNull();

       // Cleanup
       await hubConnection.InvokeAsync("DisconnectFromAgent", response.SessionId);
       await hubConnection.DisposeAsync();
   }
   ```

3. **Implement connection failure test**:
   ```csharp
   [Fact]
   public async Task ConnectToAgent_InvalidSocket_ReturnsError()
   {
       // Arrange
       using var hubConnection = CreateHubConnection(_factory.Server.CreateHandler());
       await hubConnection.StartAsync();

       // Act
       var connectRequest = new ConnectToAgentRequest
       {
           AgentId = "invalid-agent",
           ConnectorType = "terminal",
           ConnectionParams = new Dictionary<string, string>
           {
               ["socketPath"] = "/invalid/socket/path"
           }
       };

       var response = await hubConnection.InvokeAsync<ConnectToAgentResponse>(
           "ConnectToAgent",
           connectRequest);

       // Assert
       response.Success.Should().BeFalse();
       response.SessionId.Should().BeNullOrEmpty();
       response.Error.Should().NotBeNullOrEmpty();
       response.Error.Should().Contain("Failed to connect");

       await hubConnection.DisposeAsync();
   }
   ```

4. **DI Registration for test services**:
   ```csharp
   // In test setup
   services.AddSingleton<IAgentConnectorFactory, TestAgentConnectorFactory>();
   services.Configure<TestOptions>(options =>
   {
       options.UseTestConnectors = true;
       options.SimulateLatency = false;
   });
   ```

**Acceptance Criteria**:
- [ ] Test fixture created and configured
- [ ] Successful connection test passes
- [ ] Failed connection test handles errors
- [ ] Hub connection properly disposed
- [ ] DI services registered for tests

---

## Subtask 4.1B.2: Command Execution Tests

**Estimate**: 20-30 minutes

**Goal**: Test command sending and output streaming

### Technical Steps:

1. **Implement command execution test**:
   ```csharp
   [Fact]
   public async Task SendCommand_ValidSession_ExecutesAndStreamsOutput()
   {
       // Arrange
       await using var agent = new AgentTestFixture();
       await agent.InitializeAsync();

       using var hubConnection = CreateHubConnection(_factory.Server.CreateHandler());
       await hubConnection.StartAsync();

       // Connect first
       var connectRequest = new ConnectToAgentRequest
       {
           AgentId = "command-test-agent",
           ConnectorType = "terminal",
           ConnectionParams = new Dictionary<string, string>
           {
               ["socketPath"] = agent.GetSocketPath()
           }
       };

       var connectResponse = await hubConnection.InvokeAsync<ConnectToAgentResponse>(
           "ConnectToAgent",
           connectRequest);

       connectResponse.Success.Should().BeTrue();
       var sessionId = connectResponse.SessionId;

       // Act - Send command
       var command = "echo 'Hello from test'";
       var sendRequest = new SendCommandRequest
       {
           SessionId = sessionId,
           Command = command
       };

       await hubConnection.InvokeAsync("SendCommand", sendRequest);

       // Collect output
       var outputLines = new List<string>();
       var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

       await foreach (var line in hubConnection.StreamAsync<string>(
           "StreamOutput",
           sessionId,
           null,
           cts.Token))
       {
           outputLines.Add(line);
           _output.WriteLine($"Received: {line}");

           // Break after receiving expected output
           if (line.Contains("Hello from test"))
               break;
       }

       // Assert
       outputLines.Should().NotBeEmpty();
       outputLines.Should().Contain(line => line.Contains("Hello from test"));

       // Cleanup
       await hubConnection.InvokeAsync("DisconnectFromAgent", sessionId);
       await hubConnection.DisposeAsync();
   }
   ```

2. **Implement invalid command test**:
   ```csharp
   [Fact]
   public async Task SendCommand_InvalidSession_ReturnsError()
   {
       // Arrange
       using var hubConnection = CreateHubConnection(_factory.Server.CreateHandler());
       await hubConnection.StartAsync();

       // Act - Try to send command without session
       var sendRequest = new SendCommandRequest
       {
           SessionId = "invalid-session-id",
           Command = "test command"
       };

       var errorOccurred = false;
       string? errorMessage = null;

       // Register error handler
       hubConnection.On<CommandErrorNotification>("CommandError", notification =>
       {
           errorOccurred = true;
           errorMessage = notification.Message;
       });

       await hubConnection.InvokeAsync("SendCommand", sendRequest);

       // Wait for error notification
       await Task.Delay(1000);

       // Assert
       errorOccurred.Should().BeTrue();
       errorMessage.Should().Contain("Session not found");

       await hubConnection.DisposeAsync();
   }
   ```

3. **Implement command history test**:
   ```csharp
   [Fact]
   public async Task CommandHistory_MultipleCommands_TrackedCorrectly()
   {
       // Arrange & Act
       var commands = new[] { "ls", "pwd", "echo test" };
       var sessionId = await CreateSessionAsync();

       foreach (var cmd in commands)
       {
           await SendCommandAsync(sessionId, cmd);
       }

       // Get command history (if API supports)
       var history = await GetCommandHistoryAsync(sessionId);

       // Assert
       history.Should().HaveCount(3);
       history.Should().ContainInOrder(commands);

       await DisconnectSessionAsync(sessionId);
   }
   ```

**Acceptance Criteria**:
- [ ] Command execution test passes
- [ ] Output streaming verified
- [ ] Invalid session handled
- [ ] Command history tracked
- [ ] Error notifications received

---

## Subtask 4.1B.3: Concurrent Sessions Tests

**Estimate**: 20-30 minutes

**Goal**: Test multiple concurrent agent sessions

### Technical Steps:

1. **Implement multiple sessions test**:
   ```csharp
   [Fact]
   public async Task MultipleSessions_ConcurrentOperations_AllSucceed()
   {
       // Arrange
       const int sessionCount = 5;
       var tasks = new List<Task<TestSessionResult>>();

       // Act - Create and test multiple sessions concurrently
       for (int i = 0; i < sessionCount; i++)
       {
           var agentId = $"concurrent-agent-{i}";
           tasks.Add(CreateAndTestSessionAsync(agentId));
       }

       var results = await Task.WhenAll(tasks);

       // Assert
       results.Should().HaveCount(sessionCount);
       results.Should().OnlyContain(r => r.Success);
       results.Select(r => r.SessionId).Should().OnlyHaveUniqueItems();

       // Cleanup
       foreach (var result in results)
       {
           await DisconnectSessionAsync(result.SessionId);
       }
   }

   private async Task<TestSessionResult> CreateAndTestSessionAsync(string agentId)
   {
       try
       {
           // Create hub connection
           using var hubConnection = CreateHubConnection(_factory.Server.CreateHandler());
           await hubConnection.StartAsync();

           // Connect to agent
           var connectRequest = new ConnectToAgentRequest
           {
               AgentId = agentId,
               ConnectorType = "terminal",
               ConnectionParams = new Dictionary<string, string>
               {
                   ["socketPath"] = $"/tmp/test-{agentId}.sock"
               }
           };

           var response = await hubConnection.InvokeAsync<ConnectToAgentResponse>(
               "ConnectToAgent",
               connectRequest);

           if (!response.Success)
               return new TestSessionResult { Success = false };

           // Send test command
           await hubConnection.InvokeAsync("SendCommand",
               new SendCommandRequest
               {
                   SessionId = response.SessionId,
                   Command = $"echo 'Session {agentId}'"
               });

           // Verify output
           var hasOutput = await VerifyOutputAsync(hubConnection, response.SessionId, agentId);

           return new TestSessionResult
           {
               Success = hasOutput,
               SessionId = response.SessionId,
               AgentId = agentId
           };
       }
       catch (Exception ex)
       {
           _output.WriteLine($"Session test failed for {agentId}: {ex.Message}");
           return new TestSessionResult { Success = false };
       }
   }
   ```

2. **Implement session isolation test**:
   ```csharp
   [Fact]
   public async Task SessionIsolation_CommandsDoNotInterfere()
   {
       // Arrange - Create two sessions
       var session1 = await CreateSessionAsync("isolated-agent-1");
       var session2 = await CreateSessionAsync("isolated-agent-2");

       // Act - Send different commands to each
       await SendCommandAsync(session1, "echo 'Session 1 output'");
       await SendCommandAsync(session2, "echo 'Session 2 output'");

       // Collect output from both
       var output1 = await CollectOutputAsync(session1, 5);
       var output2 = await CollectOutputAsync(session2, 5);

       // Assert - Each session has only its own output
       output1.Should().Contain(line => line.Contains("Session 1 output"));
       output1.Should().NotContain(line => line.Contains("Session 2 output"));

       output2.Should().Contain(line => line.Contains("Session 2 output"));
       output2.Should().NotContain(line => line.Contains("Session 1 output"));

       // Cleanup
       await DisconnectSessionAsync(session1);
       await DisconnectSessionAsync(session2);
   }
   ```

3. **DI Registration for concurrent test support**:
   ```csharp
   // In test configuration
   services.Configure<SessionManagerOptions>(options =>
   {
       options.MaxConcurrentSessions = 10;
       options.SessionTimeout = TimeSpan.FromMinutes(5);
   });

   services.AddSingleton<IConcurrentTestHelper, ConcurrentTestHelper>();
   ```

**Acceptance Criteria**:
- [ ] Multiple concurrent sessions work
- [ ] Sessions are isolated from each other
- [ ] All sessions get unique IDs
- [ ] Concurrent operations don't interfere
- [ ] Proper cleanup after tests

---

## Helper Methods

```csharp
private class TestSessionResult
{
    public bool Success { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
}

private async Task<string> CreateSessionAsync(string agentId)
{
    // Helper to create a session
}

private async Task SendCommandAsync(string sessionId, string command)
{
    // Helper to send command
}

private async Task<List<string>> CollectOutputAsync(string sessionId, int maxLines)
{
    // Helper to collect output
}

private async Task DisconnectSessionAsync(string sessionId)
{
    // Helper to disconnect session
}
```

---

## Integration Testing Requirements

After all subtasks complete:
- [ ] Run all tests in CI/CD pipeline
- [ ] Verify test coverage >80%
- [ ] Test with real agent connectors
- [ ] Performance benchmarks recorded
- [ ] Test reports generated