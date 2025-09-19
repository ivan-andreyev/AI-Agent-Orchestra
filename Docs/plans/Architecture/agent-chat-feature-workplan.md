# Agent Chat Feature Work Plan

## Executive Summary

Implement real-time bidirectional communication between users and AI agents using SignalR Hub technology, enabling interactive command sending, intermediate result streaming, manual intervention capabilities, and enhanced user experience beyond static history viewing.

## Current State Analysis

**Existing Communication:**
- HTTP-based request/response via OrchestratorService
- Static history viewing through AgentHistory component
- No real-time interaction or intermediate updates
- Limited to task queuing and completion status

**Technical Architecture:**
- Blazor WebAssembly frontend
- ASP.NET Core API backend
- RESTful API communication
- In-memory state management

## Target Architecture

### 1. SignalR Hub Infrastructure

**Hub Architecture:**
```
AgentCommunicationHub (new)
├── User → Agent Commands
├── Agent → User Progress Updates
├── Agent → User Intermediate Results
├── User → Agent Interrupts/Cancellations
└── Real-time Status Broadcasting
```

**Communication Flow:**
```
User Interface → SignalR Hub → Agent Orchestrator → Claude Agent
     ↑                                                      ↓
     ← SignalR Hub ← Agent Orchestrator ← Claude Agent Response
```

### 2. Real-time Chat Interface

**Component Structure:**
```
AgentChatPanel.razor (new)
├── ChatMessageList.razor (new)
├── CommandInputArea.razor (new)
├── AgentStatusIndicator.razor (new)
├── InterventionControls.razor (new)
└── ChatHistoryManager.razor (new)
```

**Chat Features:**
- Real-time message streaming
- Command autocomplete and templates
- Interactive result exploration
- File sharing and code snippets
- Agent status awareness
- Conversation threading

### 3. Enhanced Agent Communication

**Message Types:**
- **Command Messages**: User instructions to agents
- **Progress Updates**: Task execution progress
- **Intermediate Results**: Partial outputs and findings
- **Status Changes**: Agent availability and task state
- **Intervention Requests**: Agent asking for user input
- **Error Notifications**: Real-time error reporting

## Implementation Phases

### Phase 1: SignalR Infrastructure (Estimated: 12-16 hours)

**Tasks:**
1. **Install and Configure SignalR**
   - Add SignalR packages to API and Web projects
   - Configure SignalR hub in API Startup
   - Add SignalR client to Blazor WebAssembly
   - **Acceptance Criteria**: SignalR connection established between client and server

2. **Create AgentCommunicationHub**
   - Implement hub with core messaging methods
   - Add connection management and group handling
   - Implement authentication and authorization
   - **Detailed Algorithm for Message Routing**:
     ```
     ALGORITHM: SendCommandToAgent(agentId, command, sessionId)
     1. VALIDATE input parameters:
        - agentId must be non-empty and valid UUID format
        - command must be non-empty, max 2000 characters
        - sessionId must be valid UUID format
     2. AUTHENTICATE user:
        - VERIFY user is authenticated via Context.UserIdentifier
        - CHECK user has permission to communicate with specified agent
        - VALIDATE user is not rate-limited (max 30 commands/minute)
     3. SANITIZE command content:
        - SCAN for malicious patterns (script injection, system commands)
        - ESCAPE special characters for safe execution
        - VALIDATE command against allowed command whitelist
     4. CREATE command request:
        - GENERATE unique correlation ID
        - SET timestamp and user context
        - ADD security metadata (IP, user agent)
     5. QUEUE command for execution:
        - CHECK agent availability and status
        - ENQUEUE with priority based on user role and command type
        - SET timeout based on command complexity estimate
     6. BROADCAST to agent group:
        - NOTIFY all connected clients in agent group
        - INCLUDE command metadata but not sensitive details
        - LOG command sent event for audit trail
     7. STORE in chat history:
        - SAVE message to database with encryption for sensitive data
        - SET delivery status to "sent"
        - LINK to correlation ID for tracking
     ```
   - **Connection Management Algorithm**:
     ```
     ALGORITHM: JoinAgentGroup(agentId)
     1. VALIDATE agent access:
        - VERIFY agentId exists and is active
        - CHECK user permissions for agent communication
        - VALIDATE agent is not in maintenance mode
     2. REGISTER connection:
        - ADD connection to agent group using Groups.AddToGroupAsync
        - STORE connection metadata (user, timestamp, agent)
        - UPDATE connection tracking for load balancing
     3. BROADCAST user joined event:
        - NOTIFY other group members of new participant
        - INCLUDE user info but respect privacy settings
        - UPDATE active user count for agent
     4. SYNC initial state:
        - SEND recent messages to joining user (last 50)
        - SEND current agent status and capabilities
        - SEND any pending intervention requests
     ```
   - **Error Handling Scenarios**:
     - Invalid agentId → Send error message to caller, log security event
     - User not authorized → Close connection, audit unauthorized access attempt
     - Agent offline → Queue message for delivery when agent returns online
     - Command validation failure → Send validation error with specific reason
     - Rate limit exceeded → Send rate limit warning, implement backoff
   - **Acceptance Criteria**: Hub can route messages between users and agents with comprehensive security validation

3. **Extend OrchestratorService Integration**
   - Connect orchestrator to SignalR hub
   - Implement agent-to-hub communication bridge
   - Add real-time status broadcasting
   - **Acceptance Criteria**: Orchestrator events trigger SignalR notifications

**Technical Implementation:**

**AgentCommunicationHub.cs:**
```csharp
public class AgentCommunicationHub : Hub
{
    private readonly SimpleOrchestrator _orchestrator;
    private readonly ILogger<AgentCommunicationHub> _logger;

    public async Task JoinAgentGroup(string agentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"agent_{agentId}");
        await Clients.Group($"agent_{agentId}").SendAsync("UserJoined", Context.ConnectionId);
    }

    public async Task SendCommandToAgent(string agentId, string command, string sessionId)
    {
        var taskRequest = new AgentCommandRequest(
            Id: Guid.NewGuid().ToString(),
            AgentId: agentId,
            Command: command,
            SessionId: sessionId,
            Timestamp: DateTime.UtcNow,
            UserId: Context.UserIdentifier
        );

        await _orchestrator.QueueInteractiveTaskAsync(taskRequest);
        await Clients.Group($"agent_{agentId}").SendAsync("CommandReceived", taskRequest);
    }

    public async Task RequestInterventionResponse(string agentId, string responseData)
    {
        await Clients.Group($"agent_{agentId}").SendAsync("InterventionResponse", responseData);
    }
}
```

### Phase 2: Chat Interface Development (Estimated: 14-18 hours)

**Tasks:**
1. **Create Core Chat Components**
   - `AgentChatPanel.razor` - Main chat interface
   - `ChatMessageList.razor` - Message display and streaming
   - `CommandInputArea.razor` - Enhanced command input
   - **Acceptance Criteria**: Users can send and receive messages in real-time

2. **Implement Message Streaming**
   - Real-time message display updates
   - Typing indicators and status awareness
   - Message threading and conversation context
   - **Acceptance Criteria**: Messages appear instantly with proper threading

3. **Add Interactive Elements**
   - Clickable code snippets and file references
   - Expandable result sections
   - Message reactions and annotations
   - **Acceptance Criteria**: Users can interact with message content dynamically

**AgentChatPanel.razor:**
```csharp
@using Microsoft.AspNetCore.SignalR.Client
@using Orchestra.Web.Models.Chat
@inject NavigationManager Navigation
@inject IJSRuntime JSRuntime
@implements IAsyncDisposable

<div class="agent-chat-panel">
    <div class="chat-header">
        <div class="agent-info">
            <AgentStatusIndicator AgentId="@AgentId" Status="@AgentStatus" />
            <h4>@AgentName</h4>
        </div>
        <div class="chat-controls">
            <button class="btn btn-sm btn-outline-secondary" @onclick="ClearChat">Clear</button>
            <button class="btn btn-sm btn-outline-primary" @onclick="ExportChat">Export</button>
        </div>
    </div>

    <div class="chat-messages" @ref="messagesContainer">
        <ChatMessageList Messages="@_messages"
                        OnMessageAction="@HandleMessageAction" />
    </div>

    <div class="chat-input">
        <CommandInputArea OnCommandSent="@SendCommand"
                         OnFileUpload="@HandleFileUpload"
                         AgentCapabilities="@AgentCapabilities" />
    </div>

    @if (_isInterventionRequired)
    {
        <InterventionControls InterventionData="@_currentIntervention"
                            OnInterventionResponse="@SendInterventionResponse" />
    }
</div>

@code {
    [Parameter] public string AgentId { get; set; } = string.Empty;
    [Parameter] public string AgentName { get; set; } = string.Empty;

    private HubConnection? _hubConnection;
    private List<ChatMessage> _messages = new();
    private AgentStatus _agentStatus = AgentStatus.Unknown;
    private bool _isInterventionRequired = false;
    private InterventionRequest? _currentIntervention;

    protected override async Task OnInitializedAsync()
    {
        await InitializeSignalRConnection();
    }

    private async Task InitializeSignalRConnection()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/agentHub"))
            .Build();

        _hubConnection.On<ChatMessage>("ReceiveMessage", (message) =>
        {
            _messages.Add(message);
            InvokeAsync(StateHasChanged);
            InvokeAsync(ScrollToBottom);
        });

        _hubConnection.On<AgentStatus>("AgentStatusChanged", (status) =>
        {
            _agentStatus = status;
            InvokeAsync(StateHasChanged);
        });

        _hubConnection.On<InterventionRequest>("InterventionRequired", (intervention) =>
        {
            _currentIntervention = intervention;
            _isInterventionRequired = true;
            InvokeAsync(StateHasChanged);
        });

        await _hubConnection.StartAsync();
        await _hubConnection.InvokeAsync("JoinAgentGroup", AgentId);
    }
}
```

### Phase 3: Advanced Chat Features (Estimated: 10-14 hours)

**Tasks:**
1. **Implement Intervention System**
   - Agent request for user input during execution
   - Interactive decision points and confirmations
   - Multi-choice intervention options
   - **Acceptance Criteria**: Agents can pause execution for user guidance

2. **Add File Sharing & Code Interaction**
   - Drag-and-drop file uploads to agents
   - Inline code execution and modification
   - Screenshot and diagram sharing
   - **Acceptance Criteria**: Rich content can be shared bidirectionally

3. **Create Chat History & Search**
   - Persistent conversation storage
   - Full-text search across chat history
   - Conversation bookmarking and tagging
   - **Acceptance Criteria**: Users can find and reference past conversations

**InterventionControls.razor:**
```csharp
<div class="intervention-panel">
    <div class="intervention-header">
        <h5>Agent Requires Input</h5>
        <span class="agent-name">@_currentIntervention?.AgentName</span>
    </div>

    <div class="intervention-content">
        <p>@_currentIntervention?.Message</p>

        @if (_currentIntervention?.Options?.Any() == true)
        {
            <div class="intervention-options">
                @foreach (var option in _currentIntervention.Options)
                {
                    <button class="btn btn-outline-primary me-2"
                            @onclick="() => SendOptionResponse(option.Value)">
                        @option.Label
                    </button>
                }
            </div>
        }
        else
        {
            <div class="intervention-input">
                <textarea @bind="_responseText"
                         placeholder="Enter your response..."
                         class="form-control mb-2"></textarea>
                <button class="btn btn-primary" @onclick="SendTextResponse">
                    Send Response
                </button>
            </div>
        }
    </div>
</div>
```

### Phase 4: Integration & Performance (Estimated: 8-12 hours)

**Tasks:**
1. **Integrate with Existing UI**
   - Add chat panel to main interface
   - Coordinate with AgentSidebar and TaskQueue
   - Implement responsive layout adjustments
   - **Acceptance Criteria**: Chat integrates seamlessly with existing interface

2. **Optimize Performance**
   - Message pagination and virtual scrolling
   - Connection reconnection and error handling
   - Memory management for long conversations
   - **Acceptance Criteria**: Chat remains responsive with 1000+ messages

3. **Add Security & Monitoring**
   - Message content validation and sanitization
   - Rate limiting and abuse prevention
   - Comprehensive logging and monitoring
   - **Acceptance Criteria**: System is secure against malicious input

## Technical Specifications

### SignalR Configuration

**API Startup.cs:**
```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Existing services...

    services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = true;
        options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
        options.StreamBufferCapacity = 10;
    });

    services.AddScoped<AgentCommunicationService>();
    services.AddScoped<ChatHistoryService>();
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // Existing configuration...

    app.UseRouting();
    app.UseCors(policy =>
    {
        policy.AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .SetIsOriginAllowed(origin => true);
    });

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
        endpoints.MapHub<AgentCommunicationHub>("/agentHub");
    });
}
```

**Blazor Program.cs:**
```csharp
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5002") });

// Add SignalR
builder.Services.AddSignalR();

// Chat services
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<MessageFormattingService>();
```

### Message Models

**Chat Message Structure:**
```csharp
public record ChatMessage(
    string Id,
    string SessionId,
    string AgentId,
    MessageType Type,
    string Content,
    MessageMetadata Metadata,
    DateTime Timestamp,
    string? UserId = null,
    List<MessageAttachment>? Attachments = null,
    MessageStatus Status = MessageStatus.Sent
);

public enum MessageType
{
    UserCommand,
    AgentResponse,
    SystemNotification,
    ProgressUpdate,
    InterventionRequest,
    ErrorMessage,
    FileShare
}

public record MessageMetadata(
    Dictionary<string, object> Properties,
    string? CorrelationId = null,
    TimeSpan? ExecutionTime = null,
    string? TaskId = null
);

public record InterventionRequest(
    string Id,
    string AgentId,
    string AgentName,
    string Message,
    InterventionType Type,
    List<InterventionOption>? Options = null,
    DateTime Timestamp = default,
    TimeSpan? Timeout = null
);
```

### Service Layer

**ChatService.cs:**
```csharp
public class ChatService
{
    private readonly HubConnection _hubConnection;
    private readonly ChatHistoryService _historyService;
    private readonly ILogger<ChatService> _logger;

    public async Task<bool> SendCommandAsync(string agentId, string command, string sessionId)
    {
        try
        {
            await _hubConnection.InvokeAsync("SendCommandToAgent", agentId, command, sessionId);
            await _historyService.SaveMessageAsync(new ChatMessage(
                Id: Guid.NewGuid().ToString(),
                SessionId: sessionId,
                AgentId: agentId,
                Type: MessageType.UserCommand,
                Content: command,
                Metadata: new MessageMetadata(new Dictionary<string, object>()),
                Timestamp: DateTime.UtcNow,
                UserId: GetCurrentUserId()
            ));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to agent {AgentId}", agentId);
            return false;
        }
    }

    public async Task<List<ChatMessage>> GetChatHistoryAsync(string sessionId, int limit = 50)
    {
        return await _historyService.GetMessagesAsync(sessionId, limit);
    }

    public IObservable<ChatMessage> ObserveMessages(string sessionId)
    {
        return _hubConnection.On<ChatMessage>("ReceiveMessage")
            .Where(message => message.SessionId == sessionId)
            .ToObservable();
    }
}
```

### UI Integration Strategy

**Main Layout Integration:**
```csharp
// Home.razor modifications
<div class="main-content">
    <div class="left-panel">
        <RepositorySelector @bind-SelectedRepository="selectedRepository" />
        <AgentSidebar SelectedRepository="@selectedRepository"
                     OnAgentSelected="@HandleAgentSelected" />
    </div>

    <div class="center-panel">
        <OrchestrationControlPanel SelectedRepository="@selectedRepository" />
        <TaskQueue SelectedRepository="@selectedRepository" />
    </div>

    <div class="right-panel @(showChatPanel ? "chat-active" : "")">
        @if (showChatPanel && selectedAgent != null)
        {
            <AgentChatPanel AgentId="@selectedAgent.Id"
                           AgentName="@selectedAgent.Name"
                           OnChatClosed="@(() => showChatPanel = false)" />
        }
        else
        {
            <AgentHistory SelectedRepository="@selectedRepository" />
        }
    </div>
</div>
```

## Data Flow Architecture

### Real-time Communication Flow

```
1. User Types Command
   ↓
2. CommandInputArea validates and formats
   ↓
3. ChatService.SendCommandAsync()
   ↓
4. SignalR Hub receives command
   ↓
5. Hub forwards to AgentCommunicationService
   ↓
6. Service queues task with SimpleOrchestrator
   ↓
7. Agent processes task
   ↓
8. Agent sends progress updates via Hub
   ↓
9. Hub broadcasts to connected clients
   ↓
10. ChatPanel receives and displays updates
```

### Message Processing Pipeline

```
Incoming Message → Content Validation → Security Scanning →
Format Processing → UI Rendering → History Storage
```

## Quality Assurance

### Testing Strategy
- **Unit Tests**: Message formatting, validation, SignalR hub methods
- **Integration Tests**: End-to-end chat workflows, file sharing
- **Performance Tests**: Connection handling under load, message throughput
- **Security Tests**: Input validation, rate limiting, authentication

### Success Metrics
- **Real-time Performance**: < 100ms message delivery latency
- **Connection Stability**: 99.9% uptime with automatic reconnection
- **User Engagement**: 3x increase in agent interaction frequency
- **Error Rate**: < 0.1% message delivery failures

## Security Considerations

### Comprehensive Input Validation

**Message Content Sanitization**:
```csharp
public class MessageSanitizer
{
    private readonly HtmlSanitizer _htmlSanitizer;
    private readonly Regex[] _maliciousPatterns;

    public SanitizationResult SanitizeMessage(string content)
    {
        // 1. Length validation
        if (content.Length > 10000)
            return SanitizationResult.Rejected("Message too long");

        // 2. HTML sanitization
        var sanitized = _htmlSanitizer.Sanitize(content);

        // 3. Script injection detection
        var scriptPatterns = new[]
        {
            @"<script[^>]*>.*?</script>",
            @"javascript:",
            @"data:text/html",
            @"vbscript:",
            @"on\w+\s*=",
            @"eval\s*\(",
            @"document\.\w+",
            @"window\.\w+"
        };

        foreach (var pattern in scriptPatterns)
        {
            if (Regex.IsMatch(sanitized, pattern, RegexOptions.IgnoreCase))
                return SanitizationResult.Rejected($"Malicious pattern detected: {pattern}");
        }

        // 4. Command injection prevention
        var commandPatterns = new[]
        {
            @"\b(rm|del|format|shutdown|reboot)\s+[-/]",
            @"[;&|`]",
            @"\$\([^)]+\)",
            @"`[^`]+`",
            @">\s*/dev/null",
            @"2>&1"
        };

        foreach (var pattern in commandPatterns)
        {
            if (Regex.IsMatch(sanitized, pattern, RegexOptions.IgnoreCase))
                return SanitizationResult.Flagged($"Potential command injection: {pattern}");
        }

        return SanitizationResult.Clean(sanitized);
    }
}
```

**File Upload Security**:
```csharp
public class FileUploadValidator
{
    private readonly string[] _allowedTypes = { ".txt", ".log", ".json", ".xml", ".md", ".png", ".jpg", ".gif" };
    private readonly int _maxFileSize = 10 * 1024 * 1024; // 10MB

    public ValidationResult ValidateFile(IFormFile file)
    {
        // 1. File size check
        if (file.Length > _maxFileSize)
            return ValidationResult.Failed("File size exceeds 10MB limit");

        // 2. File type validation
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedTypes.Contains(extension))
            return ValidationResult.Failed($"File type {extension} not allowed");

        // 3. Content type validation
        var allowedContentTypes = new[]
        {
            "text/plain", "application/json", "image/png", "image/jpeg", "image/gif"
        };
        if (!allowedContentTypes.Contains(file.ContentType))
            return ValidationResult.Failed("Invalid content type");

        // 4. Scan for malicious content
        using var stream = file.OpenReadStream();
        var buffer = new byte[1024];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        var content = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        if (ContainsMaliciousContent(content))
            return ValidationResult.Failed("File contains potentially malicious content");

        return ValidationResult.Success();
    }
}
```

### Enhanced Authentication & Authorization

**SignalR Connection Authentication**:
```csharp
public class SignalRAuthenticationService
{
    public async Task<AuthenticationResult> AuthenticateConnectionAsync(HubCallerContext context)
    {
        // 1. Extract and validate JWT token
        var token = context.GetHttpContext()?.Request.Query["access_token"];
        if (string.IsNullOrEmpty(token))
            return AuthenticationResult.Failed("Missing access token");

        // 2. Validate token signature and expiration
        var principal = await ValidateJwtTokenAsync(token);
        if (principal == null)
            return AuthenticationResult.Failed("Invalid or expired token");

        // 3. Check user account status
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _userService.GetUserAsync(userId);
        if (user?.IsActive != true)
            return AuthenticationResult.Failed("User account is inactive");

        // 4. Validate IP address and location
        var clientIp = context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
        if (!await _securityService.IsIpAllowedAsync(userId, clientIp))
            return AuthenticationResult.Failed("Access denied from this location");

        return AuthenticationResult.Success(user);
    }
}
```

**Agent-Specific Permission Validation**:
```csharp
public class AgentPermissionService
{
    public async Task<PermissionResult> ValidateAgentAccessAsync(string userId, string agentId, string operation)
    {
        // 1. Check basic agent access
        var hasAccess = await _permissionRepository.HasAgentAccessAsync(userId, agentId);
        if (!hasAccess)
            return PermissionResult.Denied("No access to specified agent");

        // 2. Validate operation permissions
        var allowedOperations = await _permissionRepository.GetAllowedOperationsAsync(userId, agentId);
        if (!allowedOperations.Contains(operation))
            return PermissionResult.Denied($"Operation '{operation}' not permitted");

        // 3. Check time-based restrictions
        var timeRestrictions = await _permissionRepository.GetTimeRestrictionsAsync(userId);
        if (timeRestrictions != null && !timeRestrictions.IsCurrentTimeAllowed())
            return PermissionResult.Denied("Access denied outside allowed hours");

        // 4. Validate concurrent session limits
        var activeSessions = await _sessionService.GetActiveSessionCountAsync(userId);
        var maxSessions = await _permissionRepository.GetMaxSessionsAsync(userId);
        if (activeSessions >= maxSessions)
            return PermissionResult.Denied("Maximum concurrent sessions exceeded");

        return PermissionResult.Allowed();
    }
}
```

### Advanced Rate Limiting

**Multi-tier Rate Limiting**:
```csharp
public class AdvancedRateLimitingService
{
    private readonly IMemoryCache _cache;
    private readonly IRateLimitingRepository _repository;

    public async Task<RateLimitResult> CheckRateLimitAsync(string userId, string operation, string agentId = null)
    {
        var limits = new[]
        {
            // Per-user global limits
            new RateLimit { Scope = "user", Key = userId, Operation = "message", Limit = 100, Window = TimeSpan.FromMinutes(1) },
            new RateLimit { Scope = "user", Key = userId, Operation = "command", Limit = 30, Window = TimeSpan.FromMinutes(1) },
            new RateLimit { Scope = "user", Key = userId, Operation = "file_upload", Limit = 10, Window = TimeSpan.FromMinutes(1) },

            // Per-agent limits
            new RateLimit { Scope = "agent", Key = agentId, Operation = operation, Limit = 50, Window = TimeSpan.FromMinutes(1) },

            // Global system limits
            new RateLimit { Scope = "system", Key = "global", Operation = operation, Limit = 1000, Window = TimeSpan.FromMinutes(1) }
        };

        foreach (var limit in limits.Where(l => l.Key != null))
        {
            var result = await CheckIndividualLimitAsync(limit);
            if (!result.IsAllowed)
                return result;
        }

        return RateLimitResult.Allowed();
    }

    private async Task<RateLimitResult> CheckIndividualLimitAsync(RateLimit limit)
    {
        var cacheKey = $"rate_limit:{limit.Scope}:{limit.Key}:{limit.Operation}";
        var windowKey = $"{cacheKey}:{DateTime.UtcNow.Ticks / limit.Window.Ticks}";

        var currentCount = _cache.Get<int>(windowKey);

        if (currentCount >= limit.Limit)
        {
            // Check if this is a burst or sustained limit violation
            var violationHistory = await _repository.GetViolationHistoryAsync(limit.Key, TimeSpan.FromHours(1));
            var severityMultiplier = Math.Min(violationHistory.Count + 1, 5);

            return new RateLimitResult
            {
                IsAllowed = false,
                RetryAfter = limit.Window.Multiply(severityMultiplier),
                CurrentCount = currentCount,
                Limit = limit.Limit,
                Scope = limit.Scope
            };
        }

        _cache.Set(windowKey, currentCount + 1, limit.Window);
        return RateLimitResult.Allowed();
    }
}
```

### Data Privacy and Encryption

**Message Encryption Implementation**:
```csharp
public class MessageEncryptionService
{
    private readonly byte[] _encryptionKey;
    private readonly IDataProtectionProvider _dataProtector;

    public EncryptedMessage EncryptMessage(ChatMessage message)
    {
        var protector = _dataProtector.CreateProtector("ChatMessages");

        // 1. Serialize message
        var messageJson = JsonSerializer.Serialize(message);

        // 2. Encrypt sensitive content
        var encryptedContent = protector.Protect(messageJson);

        // 3. Create encrypted message with metadata
        return new EncryptedMessage
        {
            Id = message.Id,
            SessionId = message.SessionId,
            AgentId = message.AgentId,
            MessageType = message.Type,
            EncryptedContent = encryptedContent,
            Timestamp = message.Timestamp,
            // Keep non-sensitive metadata unencrypted for querying
            UserId = message.UserId,
            HasAttachments = message.Attachments?.Any() == true
        };
    }

    public ChatMessage DecryptMessage(EncryptedMessage encryptedMessage)
    {
        var protector = _dataProtector.CreateProtector("ChatMessages");

        try
        {
            var decryptedJson = protector.Unprotect(encryptedMessage.EncryptedContent);
            return JsonSerializer.Deserialize<ChatMessage>(decryptedJson);
        }
        catch (CryptographicException)
        {
            // Handle decryption failure gracefully
            return new ChatMessage
            {
                Id = encryptedMessage.Id,
                Content = "[Message content could not be decrypted]",
                Type = MessageType.SystemNotification,
                Timestamp = encryptedMessage.Timestamp
            };
        }
    }
}
```

### Security Monitoring and Alerting

**Real-time Security Monitoring**:
```csharp
public class SecurityMonitoringService
{
    public async Task MonitorSecurityEventsAsync()
    {
        // 1. Monitor failed authentication attempts
        var failedAuth = await _auditService.GetFailedAuthAttemptsAsync(TimeSpan.FromMinutes(5));
        if (failedAuth.Count > 10)
        {
            await _alertService.SendSecurityAlertAsync("High number of failed authentication attempts", failedAuth);
        }

        // 2. Monitor suspicious command patterns
        var suspiciousCommands = await _auditService.GetSuspiciousCommandsAsync(TimeSpan.FromMinutes(5));
        foreach (var command in suspiciousCommands)
        {
            await _alertService.SendSecurityAlertAsync($"Suspicious command detected: {command.Pattern}", command);
        }

        // 3. Monitor rate limit violations
        var rateLimitViolations = await _auditService.GetRateLimitViolationsAsync(TimeSpan.FromMinutes(5));
        if (rateLimitViolations.Count > 50)
        {
            await _alertService.SendSecurityAlertAsync("High number of rate limit violations", rateLimitViolations);
        }

        // 4. Monitor unusual agent access patterns
        var unusualAccess = await _auditService.GetUnusualAccessPatternsAsync(TimeSpan.FromHours(1));
        foreach (var pattern in unusualAccess)
        {
            await _alertService.SendSecurityAlertAsync($"Unusual access pattern detected: {pattern.Description}", pattern);
        }
    }
}
```

## Migration Strategy

### Phased Rollout
1. **Phase 1**: Deploy SignalR infrastructure alongside existing HTTP API
2. **Phase 2**: Add chat panel as optional feature, maintain AgentHistory default
3. **Phase 3**: Promote chat as primary interaction method
4. **Phase 4**: Deprecate static history view, migrate users to real-time chat

### Backward Compatibility
- Maintain existing AgentHistory component functionality
- Preserve current HTTP API endpoints
- Support gradual user migration

## Resource Requirements

### Development Team
- **Senior Full-Stack Developer**: SignalR expertise, real-time systems (45 hours)
- **Frontend Developer**: Blazor/React experience, UI/UX design (25 hours)
- **Backend Developer**: .NET Core, WebSocket protocols (20 hours)
- **QA Engineer**: Real-time testing, performance validation (15 hours)

### Infrastructure
- SignalR service scaling considerations
- WebSocket connection monitoring
- Increased bandwidth requirements for real-time messaging

## Dependencies & Risks

### External Dependencies
- Microsoft.AspNetCore.SignalR package
- Bootstrap 5 for UI components
- Browser WebSocket support

### Risk Mitigation
- **Connection Reliability**: Implement reconnection logic with exponential backoff
- **Scaling Concerns**: Design for horizontal scaling with Redis backplane
- **Security Risks**: Comprehensive input validation and rate limiting
- **Performance Impact**: Implement connection pooling and message batching

## Success Criteria

1. **Real-time Communication**: Bidirectional messaging with < 100ms latency
2. **User Experience**: Seamless chat interface integration with existing UI
3. **Agent Interaction**: Support for intervention requests and file sharing
4. **System Stability**: Reliable connection handling with automatic recovery
5. **Performance**: No degradation to existing functionality
6. **Security**: Robust protection against malicious input and abuse

This work plan establishes a comprehensive real-time communication system that transforms the static agent interaction model into a dynamic, interactive chat experience while maintaining system security and performance standards.