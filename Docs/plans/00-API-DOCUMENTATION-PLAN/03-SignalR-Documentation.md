# Phase 3: SignalR Documentation

**Parent Plan**: [00-API-DOCUMENTATION-PLAN.md](../00-API-DOCUMENTATION-PLAN.md)

**Estimated Time**: 2-3 hours
**Dependencies**: Phase 1 analysis completed, SignalR hubs operational

## Objectives
- Document all SignalR hubs and their methods
- Create client connection examples
- Document real-time communication patterns
- Provide integration scenarios

## Task 3.1: SignalR Overview (45 minutes)

### 3.1A: Create SIGNALR-OVERVIEW.md
**Technical Steps**:
```markdown
# File: Docs/API/SIGNALR-OVERVIEW.md
# Content structure:
- SignalR connection endpoints
- Transport protocols (WebSockets, SSE, Long Polling)
- Authentication and authorization
- Automatic reconnection strategies
- Message serialization formats
- Group management patterns
```

**Integration Requirements**:
- [ ] Document hub URLs and connection strings
- [ ] Explain authentication flow with JWT tokens
- [ ] Document reconnection configuration
- [ ] Include connection lifecycle events
- [ ] Document binary vs text message handling

**Connection Template**:
```javascript
// JavaScript client connection
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/coordinatorHub", {
        accessTokenFactory: () => getAccessToken()
    })
    .withAutomaticReconnect([0, 2000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Connection lifecycle
connection.onreconnecting(error => {
    console.log("Reconnecting...", error);
});

connection.onreconnected(connectionId => {
    console.log("Reconnected with ID:", connectionId);
});

connection.onclose(error => {
    console.error("Connection closed:", error);
});
```

### 3.1B: Document Authentication Flow
**Technical Steps**:
```csharp
// Server-side hub authentication
[Authorize]
public class CoordinatorChatHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        var connectionId = Context.ConnectionId;

        // Add to user groups
        await Groups.AddToGroupAsync(connectionId, $"user-{userId}");

        // Track connection
        await _connectionTracker.AddConnection(userId, connectionId);

        await base.OnConnectedAsync();
    }
}
```

**Integration Requirements**:
- [ ] Document JWT token in query string vs headers
- [ ] Explain cookie authentication for browsers
- [ ] Document custom authentication handlers
- [ ] Include authorization policies for hubs
- [ ] Document user-to-connection mapping

## Task 3.2: Hub Documentation (120 minutes)

### 3.2A: Document CoordinatorChatHub
**Hub Method Documentation**:
```csharp
public class CoordinatorChatHub : Hub
{
    /// <summary>
    /// Send message to all connected clients
    /// </summary>
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    /// <summary>
    /// Send message to specific group
    /// </summary>
    public async Task SendToGroup(string groupName, string message)
    {
        await Clients.Group(groupName).SendAsync("GroupMessage", message);
    }

    /// <summary>
    /// Join a chat group
    /// </summary>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("UserJoined", Context.UserIdentifier);
    }
}
```

**Client Examples**:
```javascript
// JavaScript client implementation
// Connect to hub
await connection.start();

// Send message
await connection.invoke("SendMessage", userName, messageText);

// Receive messages
connection.on("ReceiveMessage", (user, message) => {
    console.log(`${user}: ${message}`);
    addMessageToUI(user, message);
});

// Join group
await connection.invoke("JoinGroup", "developers");

// Handle group messages
connection.on("GroupMessage", (message) => {
    console.log(`Group message: ${message}`);
});
```

**TypeScript Interfaces**:
```typescript
interface IChatHub {
    sendMessage(user: string, message: string): Promise<void>;
    joinGroup(groupName: string): Promise<void>;
    leaveGroup(groupName: string): Promise<void>;
    sendToGroup(groupName: string, message: string): Promise<void>;
}

interface IChatClient {
    receiveMessage: (user: string, message: string) => void;
    groupMessage: (message: string) => void;
    userJoined: (userId: string) => void;
    userLeft: (userId: string) => void;
}
```

### 3.2B: Document AgentInteractionHub
**Hub Documentation**:
```csharp
public class AgentInteractionHub : Hub
{
    /// <summary>
    /// Register an agent with the hub
    /// </summary>
    public async Task<AgentRegistrationResult> RegisterAgent(AgentRegistration registration)
    {
        // Validate agent capabilities
        // Store agent connection mapping
        // Return registration confirmation
    }

    /// <summary>
    /// Stream agent output in real-time
    /// </summary>
    public async IAsyncEnumerable<OutputChunk> StreamOutput(
        string agentId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Stream output chunks as they become available
        await foreach (var chunk in GetOutputChunks(agentId, cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Send command to specific agent
    /// </summary>
    public async Task<CommandResult> SendCommand(string agentId, AgentCommand command)
    {
        // Route command to specific agent
        // Wait for acknowledgment
        // Return result
    }
}
```

**Client Streaming Example**:
```javascript
// Streaming in JavaScript
connection.stream("StreamOutput", agentId)
    .subscribe({
        next: (chunk) => {
            appendOutput(chunk.content);
            updateProgress(chunk.progress);
        },
        complete: () => {
            console.log("Stream completed");
            markTaskComplete();
        },
        error: (err) => {
            console.error("Stream error:", err);
            handleStreamError(err);
        }
    });

// Sending commands
const result = await connection.invoke("SendCommand", agentId, {
    type: "ExecuteTask",
    parameters: {
        taskId: "task-123",
        timeout: 300
    }
});
```

### 3.2C: Document AgentCommunicationHub
**Hub Documentation**:
```csharp
public class AgentCommunicationHub : Hub
{
    /// <summary>
    /// Broadcast message to all agents
    /// </summary>
    public async Task BroadcastToAgents(AgentMessage message)
    {
        await Clients.Group("agents").SendAsync("AgentMessage", message);
    }

    /// <summary>
    /// Request next available task
    /// </summary>
    public async Task<TaskAssignment> RequestTask(string agentId, AgentCapabilities capabilities)
    {
        // Find suitable task for agent capabilities
        // Assign task to agent
        // Return task details
    }

    /// <summary>
    /// Report task progress
    /// </summary>
    public async Task ReportProgress(string taskId, ProgressReport progress)
    {
        // Update task progress
        // Notify interested clients
        await Clients.Group($"task-{taskId}").SendAsync("ProgressUpdate", progress);
    }
}
```

**Agent Client Implementation**:
```csharp
// C# agent client
public class AgentClient
{
    private HubConnection _connection;

    public async Task ConnectAsync(string hubUrl, string agentToken)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(agentToken);
            })
            .WithAutomaticReconnect()
            .Build();

        // Register handlers
        _connection.On<AgentMessage>("AgentMessage", HandleAgentMessage);
        _connection.On<TaskAssignment>("TaskAssigned", HandleTaskAssignment);

        await _connection.StartAsync();
    }

    public async Task<TaskAssignment> RequestTaskAsync()
    {
        return await _connection.InvokeAsync<TaskAssignment>(
            "RequestTask",
            _agentId,
            _capabilities);
    }

    public async Task ReportProgressAsync(string taskId, double percentage, string status)
    {
        await _connection.InvokeAsync("ReportProgress", taskId, new ProgressReport
        {
            Percentage = percentage,
            Status = status,
            Timestamp = DateTime.UtcNow
        });
    }
}
```

## Task 3.3: SignalR Integration Examples (45 minutes)

### 3.3A: Create JavaScript Client Examples
**Full Example Application**:
```html
<!-- index.html -->
<!DOCTYPE html>
<html>
<head>
    <title>Orchestra SignalR Client</title>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@latest/dist/browser/signalr.min.js"></script>
</head>
<body>
    <div id="app">
        <div id="connectionStatus">Disconnected</div>
        <div id="messages"></div>
        <input type="text" id="messageInput" />
        <button onclick="sendMessage()">Send</button>
    </div>

    <script src="orchestra-client.js"></script>
</body>
</html>
```

```javascript
// orchestra-client.js
class OrchestraClient {
    constructor(hubUrl) {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Debug)
            .build();

        this.setupHandlers();
    }

    setupHandlers() {
        // Message handlers
        this.connection.on("ReceiveMessage", this.onMessageReceived.bind(this));
        this.connection.on("TaskUpdate", this.onTaskUpdate.bind(this));
        this.connection.on("AgentStatusChanged", this.onAgentStatusChanged.bind(this));

        // Connection handlers
        this.connection.onreconnecting(this.onReconnecting.bind(this));
        this.connection.onreconnected(this.onReconnected.bind(this));
        this.connection.onclose(this.onConnectionClosed.bind(this));
    }

    async connect() {
        try {
            await this.connection.start();
            this.updateConnectionStatus("Connected");
            console.log("Connected to SignalR hub");
        } catch (err) {
            console.error("Failed to connect:", err);
            setTimeout(() => this.connect(), 5000);
        }
    }

    async sendMessage(message) {
        try {
            await this.connection.invoke("SendMessage", "User", message);
        } catch (err) {
            console.error("Failed to send message:", err);
        }
    }

    onMessageReceived(user, message) {
        const messageElement = document.createElement("div");
        messageElement.textContent = `${user}: ${message}`;
        document.getElementById("messages").appendChild(messageElement);
    }

    onTaskUpdate(taskId, status) {
        console.log(`Task ${taskId} status: ${status}`);
    }

    onAgentStatusChanged(agentId, status) {
        console.log(`Agent ${agentId} status: ${status}`);
    }

    onReconnecting(error) {
        this.updateConnectionStatus("Reconnecting...");
        console.log("Reconnecting:", error);
    }

    onReconnected(connectionId) {
        this.updateConnectionStatus("Connected");
        console.log("Reconnected with ID:", connectionId);
    }

    onConnectionClosed(error) {
        this.updateConnectionStatus("Disconnected");
        console.error("Connection closed:", error);
    }

    updateConnectionStatus(status) {
        document.getElementById("connectionStatus").textContent = status;
    }
}

// Initialize client
const client = new OrchestraClient("/coordinatorHub");
client.connect();

// Global function for button
function sendMessage() {
    const input = document.getElementById("messageInput");
    client.sendMessage(input.value);
    input.value = "";
}
```

### 3.3B: Create C# Client Examples
**Console Application Example**:
```csharp
// Program.cs
using Microsoft.AspNetCore.SignalR.Client;

class Program
{
    static async Task Main(string[] args)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/coordinatorHub")
            .WithAutomaticReconnect()
            .Build();

        // Setup handlers
        connection.On<string, string>("ReceiveMessage", (user, message) =>
        {
            Console.WriteLine($"{user}: {message}");
        });

        connection.On<string>("TaskCompleted", taskId =>
        {
            Console.WriteLine($"Task {taskId} completed");
        });

        // Connection events
        connection.Reconnecting += error =>
        {
            Console.WriteLine($"Reconnecting: {error?.Message}");
            return Task.CompletedTask;
        };

        connection.Reconnected += connectionId =>
        {
            Console.WriteLine($"Reconnected: {connectionId}");
            return Task.CompletedTask;
        };

        // Start connection
        await connection.StartAsync();
        Console.WriteLine("Connected to hub");

        // Interactive loop
        while (true)
        {
            var line = Console.ReadLine();
            if (string.IsNullOrEmpty(line))
                break;

            await connection.InvokeAsync("SendMessage", "Console", line);
        }

        await connection.DisposeAsync();
    }
}
```

## Deliverables

### Documentation Files
- [ ] `Docs/API/SIGNALR-OVERVIEW.md` - SignalR concepts and setup
- [ ] `Docs/API/SIGNALR-REFERENCE.md` - Complete hub documentation
- [ ] `Docs/API/SIGNALR-CLIENTS.md` - Client implementation guides

### Example Applications
- [ ] `Examples/JavaScript/signalr-client/` - Full JS client
- [ ] `Examples/CSharp/SignalRConsole/` - C# console client
- [ ] `Examples/TypeScript/signalr-typed/` - TypeScript client

### Integration Tests
- [ ] Connection tests for all hubs
- [ ] Authentication flow tests
- [ ] Message routing tests
- [ ] Reconnection scenario tests

## Success Criteria
- [ ] All hub methods documented with examples
- [ ] Client libraries documented for 3+ languages
- [ ] Authentication flow clearly explained
- [ ] Streaming patterns documented
- [ ] Group management explained

## Validation Checklist
- [ ] All hub methods have TypeScript interfaces
- [ ] JavaScript examples tested in browser
- [ ] C# examples compile and connect
- [ ] Authentication works in examples
- [ ] Reconnection logic functions properly