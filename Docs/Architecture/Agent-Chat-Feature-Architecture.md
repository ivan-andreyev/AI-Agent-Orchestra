# Agent Chat Feature Architecture

## Architecture Overview

This document defines the comprehensive architecture for implementing real-time bidirectional communication between users and AI agents using SignalR Hub technology. The system enables interactive command sending, intermediate result streaming, manual intervention capabilities, and enhanced user experience beyond static history viewing.

## System Context Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           AI Agent Orchestra - Chat System                 │
│                                                                             │
│  ┌─────────────────────┐     ┌─────────────────────────────────────────┐   │
│  │   Blazor WebAssembly │     │           Agent Chat Panel             │   │
│  │   Client            │     │                                         │   │
│  │                     │     │  ┌─────────────────────────────────────┐ │   │
│  │  ┌─────────────────┐│     │  │        Chat Interface               │ │   │
│  │  │ Chat Components ││     │  │  - Message Display                  │ │   │
│  │  │ - Message List  ││◀────┼──┤  - Command Input                    │ │   │
│  │  │ - Input Area    ││     │  │  - File Sharing                     │ │   │
│  │  │ - Status        ││     │  │  - Intervention Controls            │ │   │
│  │  │   Indicators    ││     │  └─────────────────────────────────────┘ │   │
│  │  └─────────────────┘│     │                                         │   │
│  └─────────────────────┘     └─────────────────────────────────────────┘   │
│             │                                  │                           │
│             │ SignalR Connection               │ HTTP API                  │
│             ▼                                  ▼                           │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    Orchestra.API Backend                            │   │
│  │                                                                     │   │
│  │  ┌─────────────────────┐    ┌───────────────────────────────────┐   │   │
│  │  │ AgentCommunication │    │       Chat Services               │   │   │
│  │  │ Hub                │    │  - Message Routing                │   │   │
│  │  │ - Connection Mgmt  │    │  - History Management             │   │   │
│  │  │ - Message Routing  │    │  - File Handling                  │   │   │
│  │  │ - Group Management │    │  - Security Validation            │   │   │
│  │  └─────────────────────┘    └───────────────────────────────────┘   │   │
│  │             │                           │                           │   │
│  │             ▼                           ▼                           │   │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │   │
│  │  │              Agent Orchestration Layer                          │ │   │
│  │  │                                                                 │ │   │
│  │  │  ┌─────────────────┐    ┌──────────────────────────────────┐   │ │   │
│  │  │  │ Simple          │    │      Agent Communication        │   │ │   │
│  │  │  │ Orchestrator    │    │      Service                     │   │ │   │
│  │  │  │ - Task Queue    │◀───┤  - Agent Assignment             │   │ │   │
│  │  │  │ - Agent Mgmt    │    │  - Status Broadcasting          │   │ │   │
│  │  │  └─────────────────┘    │  - Progress Reporting           │   │ │   │
│  │  │                         └──────────────────────────────────┘   │ │   │
│  │  └─────────────────────────────────────────────────────────────────┘ │   │
│  │                                   │                                   │   │
│  │                                   ▼                                   │   │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │   │
│  │  │                    Claude Agent Execution                       │ │   │
│  │  │                                                                 │ │   │
│  │  │  ┌─────────────────┐    ┌──────────────────────────────────┐   │ │   │
│  │  │  │ Agent Instances │    │      Progress Reporting          │   │ │   │
│  │  │  │ - Command Exec  │────┤  - Real-time Updates             │   │ │   │
│  │  │  │ - File Ops      │    │  - Intermediate Results          │   │ │   │
│  │  │  │ - Error Handling│    │  - Intervention Requests         │   │ │   │
│  │  │  └─────────────────┘    └──────────────────────────────────┘   │ │   │
│  │  └─────────────────────────────────────────────────────────────────┘ │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## SignalR Hub Architecture

### 1. Hub Communication Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         SignalR Communication Flow                         │
│                                                                             │
│  Client                     SignalR Hub                    Agent Layer      │
│    │                            │                            │              │
│    │ 1. JoinAgentGroup(agentId) │                            │              │
│    ├───────────────────────────▶│                            │              │
│    │                            │ 2. Add to Group            │              │
│    │                            ├─────────────────────────────┤              │
│    │                            │                            │              │
│    │ 3. SendCommand(command)    │                            │              │
│    ├───────────────────────────▶│ 4. Route to Orchestrator   │              │
│    │                            ├───────────────────────────▶│              │
│    │                            │                            │              │
│    │                            │ 5. CommandReceived Event   │              │
│    │◀───────────────────────────┤                            │              │
│    │                            │                            │              │
│    │                            │ 6. Progress Updates        │              │
│    │◀───────────────────────────┤◀───────────────────────────│              │
│    │                            │                            │              │
│    │                            │ 7. Intermediate Results    │              │
│    │◀───────────────────────────┤◀───────────────────────────│              │
│    │                            │                            │              │
│    │                            │ 8. Intervention Request    │              │
│    │◀───────────────────────────┤◀───────────────────────────│              │
│    │                            │                            │              │
│    │ 9. InterventionResponse    │                            │              │
│    ├───────────────────────────▶│ 10. Forward to Agent       │              │
│    │                            ├───────────────────────────▶│              │
│    │                            │                            │              │
│    │                            │ 11. Task Completion        │              │
│    │◀───────────────────────────┤◀───────────────────────────│              │
│    │                            │                            │              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2. Hub Implementation Architecture

```csharp
public class AgentCommunicationHub : Hub<IAgentCommunicationClient>
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly IChatHistoryService _chatHistory;
    private readonly ISecurityValidator _securityValidator;
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<AgentCommunicationHub> _logger;

    // Connection Management
    public async Task JoinAgentGroup(string agentId)
    {
        var connectionInfo = new ConnectionInfo
        {
            ConnectionId = Context.ConnectionId,
            UserId = Context.UserIdentifier ?? "anonymous",
            AgentId = agentId,
            ConnectedAt = DateTime.UtcNow
        };

        await _connectionManager.RegisterConnectionAsync(connectionInfo);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"agent_{agentId}");

        await Clients.Group($"agent_{agentId}").UserJoined(connectionInfo);

        _logger.LogInformation("User {UserId} joined agent group {AgentId}",
            Context.UserIdentifier, agentId);
    }

    // Command Handling
    public async Task SendCommandToAgent(string agentId, string command, string sessionId)
    {
        // Security validation
        var validationResult = await _securityValidator.ValidateCommandAsync(command, Context.UserIdentifier);
        if (!validationResult.IsValid)
        {
            await Clients.Caller.CommandRejected(validationResult.Reason);
            return;
        }

        // Create command request
        var commandRequest = new AgentCommandRequest
        {
            Id = Guid.NewGuid().ToString(),
            AgentId = agentId,
            Command = command,
            SessionId = sessionId,
            UserId = Context.UserIdentifier,
            Timestamp = DateTime.UtcNow,
            ConnectionId = Context.ConnectionId
        };

        // Queue for execution
        await _orchestrator.QueueInteractiveTaskAsync(commandRequest);

        // Broadcast to group
        await Clients.Group($"agent_{agentId}").CommandReceived(commandRequest);

        // Store in chat history
        await _chatHistory.SaveMessageAsync(new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            AgentId = agentId,
            Type = MessageType.UserCommand,
            Content = command,
            Timestamp = DateTime.UtcNow,
            UserId = Context.UserIdentifier
        });
    }

    // Intervention Handling
    public async Task SendInterventionResponse(string agentId, string interventionId, string responseData)
    {
        var response = new InterventionResponse
        {
            InterventionId = interventionId,
            ResponseData = responseData,
            RespondedBy = Context.UserIdentifier,
            Timestamp = DateTime.UtcNow
        };

        await _orchestrator.ProcessInterventionResponseAsync(response);
        await Clients.Group($"agent_{agentId}").InterventionResponseReceived(response);
    }

    // File Sharing
    public async Task ShareFile(string agentId, string fileName, string fileData, string mimeType)
    {
        var fileShare = new FileShareMessage
        {
            Id = Guid.NewGuid().ToString(),
            AgentId = agentId,
            FileName = fileName,
            FileData = fileData,
            MimeType = mimeType,
            SharedBy = Context.UserIdentifier,
            Timestamp = DateTime.UtcNow
        };

        await _orchestrator.ProcessFileShareAsync(fileShare);
        await Clients.Group($"agent_{agentId}").FileShared(fileShare);
    }

    // Connection Lifecycle
    public override async Task OnConnectedAsync()
    {
        await _connectionManager.OnConnectedAsync(Context.ConnectionId, Context.UserIdentifier);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _connectionManager.OnDisconnectedAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

// Client interface for type-safe SignalR calls
public interface IAgentCommunicationClient
{
    Task UserJoined(ConnectionInfo connectionInfo);
    Task CommandReceived(AgentCommandRequest command);
    Task CommandRejected(string reason);
    Task ProgressUpdate(AgentProgressUpdate update);
    Task IntermediateResult(AgentResult result);
    Task InterventionRequired(InterventionRequest request);
    Task InterventionResponseReceived(InterventionResponse response);
    Task FileShared(FileShareMessage fileShare);
    Task AgentStatusChanged(string agentId, AgentStatus status);
    Task TaskCompleted(TaskCompletionNotification completion);
    Task ErrorOccurred(ErrorNotification error);
}
```

### 3. Connection Management Architecture

```csharp
public interface IConnectionManager
{
    Task RegisterConnectionAsync(ConnectionInfo connectionInfo);
    Task OnConnectedAsync(string connectionId, string? userId);
    Task OnDisconnectedAsync(string connectionId);
    Task<List<ConnectionInfo>> GetActiveConnectionsAsync(string agentId);
    Task<ConnectionInfo?> GetConnectionAsync(string connectionId);
    Task BroadcastToAgentGroupAsync(string agentId, string method, object data);
    Task SendToConnectionAsync(string connectionId, string method, object data);
}

public class ConnectionManager : IConnectionManager
{
    private readonly IMemoryCache _cache;
    private readonly IHubContext<AgentCommunicationHub> _hubContext;
    private readonly ILogger<ConnectionManager> _logger;
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();

    public async Task RegisterConnectionAsync(ConnectionInfo connectionInfo)
    {
        _connections.TryAdd(connectionInfo.ConnectionId, connectionInfo);

        // Cache user's last active agent
        _cache.Set($"user_last_agent_{connectionInfo.UserId}",
                  connectionInfo.AgentId,
                  TimeSpan.FromHours(24));

        _logger.LogDebug("Registered connection {ConnectionId} for user {UserId} with agent {AgentId}",
            connectionInfo.ConnectionId, connectionInfo.UserId, connectionInfo.AgentId);
    }

    public async Task OnDisconnectedAsync(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connectionInfo))
        {
            await _hubContext.Groups.RemoveFromGroupAsync(connectionId, $"agent_{connectionInfo.AgentId}");

            _logger.LogDebug("Removed connection {ConnectionId} for user {UserId}",
                connectionId, connectionInfo.UserId);
        }
    }

    public async Task BroadcastToAgentGroupAsync(string agentId, string method, object data)
    {
        await _hubContext.Clients.Group($"agent_{agentId}").SendAsync(method, data);
    }

    public async Task SendToConnectionAsync(string connectionId, string method, object data)
    {
        await _hubContext.Clients.Client(connectionId).SendAsync(method, data);
    }
}
```

## Chat Interface Architecture

### 1. Component Hierarchy

```
AgentChatPanel.razor (Main Container)
├── ChatHeader.razor (Agent Info, Status, Controls)
├── ChatMessagesContainer.razor (Scrollable Message Area)
│   ├── ChatMessageList.razor (Message Display)
│   │   ├── UserMessageComponent.razor (User Commands)
│   │   ├── AgentMessageComponent.razor (Agent Responses)
│   │   ├── SystemMessageComponent.razor (Status Updates)
│   │   ├── ProgressMessageComponent.razor (Progress Indicators)
│   │   ├── FileMessageComponent.razor (File Attachments)
│   │   └── InterventionMessageComponent.razor (Intervention Requests)
│   └── TypingIndicator.razor (Real-time Typing Status)
├── ChatInputArea.razor (Command Input)
│   ├── CommandTextBox.razor (Text Input with Autocomplete)
│   ├── CommandSuggestions.razor (Smart Suggestions)
│   ├── FileUploadArea.razor (Drag & Drop File Sharing)
│   └── QuickActions.razor (Predefined Commands)
├── InterventionPanel.razor (Modal Intervention Interface)
│   ├── InterventionContent.razor (Request Display)
│   ├── InterventionOptions.razor (Multiple Choice)
│   └── InterventionTextInput.razor (Free-form Response)
└── ChatSidebar.razor (Optional Context Panel)
    ├── AgentCapabilities.razor (Agent Information)
    ├── ConversationHistory.razor (Session History)
    └── ChatSettings.razor (User Preferences)
```

### 2. Message Flow Architecture

```csharp
public class ChatMessageFlow
{
    // Message Processing Pipeline
    public async Task<ProcessedMessage> ProcessIncomingMessageAsync(RawMessage rawMessage)
    {
        // 1. Security validation
        var securityResult = await _securityValidator.ValidateMessageAsync(rawMessage);
        if (!securityResult.IsValid)
        {
            return ProcessedMessage.Rejected(securityResult.Reason);
        }

        // 2. Content formatting and sanitization
        var sanitizedContent = await _contentSanitizer.SanitizeAsync(rawMessage.Content);

        // 3. Message type classification
        var messageType = _messageClassifier.ClassifyMessage(sanitizedContent);

        // 4. Metadata extraction
        var metadata = await _metadataExtractor.ExtractMetadataAsync(rawMessage);

        // 5. Create processed message
        var processedMessage = new ProcessedMessage
        {
            Id = Guid.NewGuid().ToString(),
            OriginalMessage = rawMessage,
            SanitizedContent = sanitizedContent,
            Type = messageType,
            Metadata = metadata,
            ProcessedAt = DateTime.UtcNow
        };

        // 6. Store in history
        await _chatHistory.SaveMessageAsync(processedMessage);

        return processedMessage;
    }

    // Message Rendering Pipeline
    public async Task<RenderedMessage> RenderMessageAsync(ProcessedMessage message)
    {
        var renderer = _rendererFactory.CreateRenderer(message.Type);

        var renderedMessage = new RenderedMessage
        {
            Id = message.Id,
            HtmlContent = await renderer.RenderAsync(message),
            AttachmentLinks = await renderer.GetAttachmentLinksAsync(message),
            InteractiveElements = await renderer.GetInteractiveElementsAsync(message),
            CssClasses = renderer.GetCssClasses(message)
        };

        return renderedMessage;
    }
}

// Message Renderers
public interface IMessageRenderer
{
    Task<string> RenderAsync(ProcessedMessage message);
    Task<List<AttachmentLink>> GetAttachmentLinksAsync(ProcessedMessage message);
    Task<List<InteractiveElement>> GetInteractiveElementsAsync(ProcessedMessage message);
    List<string> GetCssClasses(ProcessedMessage message);
}

public class AgentResponseRenderer : IMessageRenderer
{
    public async Task<string> RenderAsync(ProcessedMessage message)
    {
        var content = message.SanitizedContent;

        // Code block highlighting
        content = await _codeHighlighter.HighlightCodeBlocksAsync(content);

        // File reference linking
        content = await _fileLinkProcessor.ProcessFileReferencesAsync(content);

        // Command highlighting
        content = await _commandHighlighter.HighlightCommandsAsync(content);

        return content;
    }

    public async Task<List<InteractiveElement>> GetInteractiveElementsAsync(ProcessedMessage message)
    {
        var elements = new List<InteractiveElement>();

        // Extract clickable commands
        var commands = _commandExtractor.ExtractCommands(message.SanitizedContent);
        elements.AddRange(commands.Select(cmd => new InteractiveElement
        {
            Type = "command",
            Content = cmd,
            Action = "execute-command"
        }));

        // Extract file references
        var fileRefs = _fileReferenceExtractor.ExtractFileReferences(message.SanitizedContent);
        elements.AddRange(fileRefs.Select(file => new InteractiveElement
        {
            Type = "file",
            Content = file,
            Action = "open-file"
        }));

        return elements;
    }
}
```

### 3. Real-time Updates Architecture

```csharp
public class RealTimeUpdateService
{
    private readonly IHubConnectionService _hubConnection;
    private readonly IChatStateManager _stateManager;
    private readonly IUIUpdateQueue _updateQueue;

    public async Task InitializeAsync(string agentId)
    {
        await _hubConnection.StartAsync();
        await _hubConnection.InvokeAsync("JoinAgentGroup", agentId);

        // Subscribe to real-time events
        _hubConnection.On<ChatMessage>("ReceiveMessage", OnMessageReceived);
        _hubConnection.On<AgentProgressUpdate>("ProgressUpdate", OnProgressUpdate);
        _hubConnection.On<InterventionRequest>("InterventionRequired", OnInterventionRequired);
        _hubConnection.On<AgentStatus>("AgentStatusChanged", OnAgentStatusChanged);
    }

    private async Task OnMessageReceived(ChatMessage message)
    {
        // Update local state
        await _stateManager.AddMessageAsync(message);

        // Queue UI update
        _updateQueue.EnqueueUpdate(new UIUpdate
        {
            Type = UIUpdateType.NewMessage,
            Data = message,
            Timestamp = DateTime.UtcNow
        });

        // Trigger notification if needed
        if (message.Type == MessageType.AgentResponse && !_stateManager.IsWindowFocused)
        {
            await _notificationService.ShowNotificationAsync($"Message from {message.AgentId}", message.Content);
        }
    }

    private async Task OnProgressUpdate(AgentProgressUpdate update)
    {
        // Update progress indicators
        await _stateManager.UpdateProgressAsync(update.TaskId, update.Progress);

        // Update UI
        _updateQueue.EnqueueUpdate(new UIUpdate
        {
            Type = UIUpdateType.ProgressUpdate,
            Data = update,
            Timestamp = DateTime.UtcNow
        });
    }

    private async Task OnInterventionRequired(InterventionRequest intervention)
    {
        // Show intervention modal
        await _stateManager.SetInterventionAsync(intervention);

        _updateQueue.EnqueueUpdate(new UIUpdate
        {
            Type = UIUpdateType.InterventionRequired,
            Data = intervention,
            Timestamp = DateTime.UtcNow
        });

        // Play notification sound
        await _audioService.PlayNotificationSoundAsync("intervention-required");
    }
}
```

## Security Architecture

### 1. Authentication and Authorization

```csharp
public class ChatSecurityService
{
    private readonly IUserIdentityService _identityService;
    private readonly IPermissionValidator _permissionValidator;
    private readonly IRateLimitingService _rateLimiting;

    // Connection Authentication
    public async Task<AuthenticationResult> AuthenticateConnectionAsync(string connectionId, string? userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return AuthenticationResult.Anonymous();
        }

        var user = await _identityService.GetUserAsync(userId);
        if (user == null)
        {
            return AuthenticationResult.Failed("User not found");
        }

        if (!user.IsActive)
        {
            return AuthenticationResult.Failed("User account is inactive");
        }

        return AuthenticationResult.Success(user);
    }

    // Command Authorization
    public async Task<AuthorizationResult> AuthorizeCommandAsync(string command, string userId, string agentId)
    {
        // Check user permissions for agent
        var hasAgentAccess = await _permissionValidator.CanAccessAgentAsync(userId, agentId);
        if (!hasAgentAccess)
        {
            return AuthorizationResult.Denied("No access to specified agent");
        }

        // Check command permissions
        var commandPermissions = await _permissionValidator.GetCommandPermissionsAsync(userId);
        if (!commandPermissions.CanExecuteCommand(command))
        {
            return AuthorizationResult.Denied("Insufficient permissions for command");
        }

        // Rate limiting check
        var rateLimitResult = await _rateLimiting.CheckRateLimitAsync(userId, "command-execution");
        if (!rateLimitResult.IsAllowed)
        {
            return AuthorizationResult.Denied($"Rate limit exceeded. Try again in {rateLimitResult.RetryAfter}");
        }

        return AuthorizationResult.Allowed();
    }

    // Message Content Validation
    public async Task<ValidationResult> ValidateMessageContentAsync(string content, string userId)
    {
        var errors = new List<string>();

        // Content length validation
        if (content.Length > 10000)
        {
            errors.Add("Message content exceeds maximum length");
        }

        // Malicious content detection
        if (ContainsMaliciousContent(content))
        {
            errors.Add("Message contains potentially malicious content");
        }

        // Spam detection
        var spamScore = await _spamDetector.CalculateSpamScoreAsync(content, userId);
        if (spamScore > 0.8)
        {
            errors.Add("Message flagged as potential spam");
        }

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }

    private bool ContainsMaliciousContent(string content)
    {
        var maliciousPatterns = new[]
        {
            @"<script[^>]*>.*?</script>",
            @"javascript:",
            @"data:text/html",
            @"vbscript:",
            @"onload\s*=",
            @"onerror\s*=",
            @"eval\s*\(",
            @"document\.cookie",
            @"window\.location"
        };

        return maliciousPatterns.Any(pattern =>
            Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline));
    }
}
```

### 2. Rate Limiting and Abuse Prevention

```csharp
public class RateLimitingService : IRateLimitingService
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;

    public async Task<RateLimitResult> CheckRateLimitAsync(string userId, string operation)
    {
        var limits = GetRateLimits(operation);
        var cacheKey = $"ratelimit:{userId}:{operation}";

        var currentWindow = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm");
        var windowKey = $"{cacheKey}:{currentWindow}";

        var currentCount = _cache.Get<int>(windowKey);

        if (currentCount >= limits.RequestsPerWindow)
        {
            return new RateLimitResult
            {
                IsAllowed = false,
                RetryAfter = TimeSpan.FromMinutes(1),
                CurrentCount = currentCount,
                Limit = limits.RequestsPerWindow
            };
        }

        // Increment counter
        _cache.Set(windowKey, currentCount + 1, TimeSpan.FromMinutes(1));

        return new RateLimitResult
        {
            IsAllowed = true,
            CurrentCount = currentCount + 1,
            Limit = limits.RequestsPerWindow
        };
    }

    private RateLimits GetRateLimits(string operation)
    {
        return operation switch
        {
            "command-execution" => new RateLimits { RequestsPerWindow = 30, WindowMinutes = 1 },
            "message-send" => new RateLimits { RequestsPerWindow = 100, WindowMinutes = 1 },
            "file-upload" => new RateLimits { RequestsPerWindow = 10, WindowMinutes = 1 },
            _ => new RateLimits { RequestsPerWindow = 60, WindowMinutes = 1 }
        };
    }
}
```

## Performance Architecture

### 1. Message Caching and Optimization

```csharp
public class ChatPerformanceOptimizer
{
    private readonly IMemoryCache _messageCache;
    private readonly IMessageCompressor _compressor;
    private readonly IConnectionOptimizer _connectionOptimizer;

    // Message Caching Strategy
    public async Task<List<ChatMessage>> GetMessagesAsync(string sessionId, int limit = 50, int offset = 0)
    {
        var cacheKey = $"messages:{sessionId}:{limit}:{offset}";

        if (_messageCache.TryGetValue(cacheKey, out List<ChatMessage>? cachedMessages))
        {
            return cachedMessages ?? new List<ChatMessage>();
        }

        var messages = await _chatHistory.GetMessagesAsync(sessionId, limit, offset);

        // Cache for 5 minutes
        _messageCache.Set(cacheKey, messages, TimeSpan.FromMinutes(5));

        return messages;
    }

    // Message Compression for Large Content
    public async Task<CompressedMessage> CompressMessageAsync(ChatMessage message)
    {
        if (message.Content.Length < 1000)
        {
            return new CompressedMessage { Original = message, IsCompressed = false };
        }

        var compressedContent = await _compressor.CompressAsync(message.Content);

        return new CompressedMessage
        {
            Original = message,
            CompressedContent = compressedContent,
            IsCompressed = true,
            CompressionRatio = (double)compressedContent.Length / message.Content.Length
        };
    }

    // Connection Optimization
    public async Task OptimizeConnectionAsync(string connectionId)
    {
        var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
        if (connectionInfo == null) return;

        // Adjust message batching based on connection quality
        var quality = await _connectionOptimizer.AssessConnectionQualityAsync(connectionId);

        var batchSize = quality.Quality switch
        {
            ConnectionQuality.Excellent => 10,
            ConnectionQuality.Good => 5,
            ConnectionQuality.Fair => 3,
            ConnectionQuality.Poor => 1,
            _ => 1
        };

        await _connectionOptimizer.SetMessageBatchSizeAsync(connectionId, batchSize);
    }
}
```

### 2. Virtual Scrolling for Message Lists

```csharp
public class VirtualizedMessageList : ComponentBase
{
    [Parameter] public List<ChatMessage> Messages { get; set; } = new();
    [Parameter] public int ItemHeight { get; set; } = 80;
    [Parameter] public int ContainerHeight { get; set; } = 600;

    private int _scrollTop = 0;
    private int _visibleItemCount;
    private int _startIndex;
    private int _endIndex;
    private int _bufferSize = 5;

    protected override void OnParametersSet()
    {
        _visibleItemCount = ContainerHeight / ItemHeight;
        CalculateVisibleRange();
    }

    private void CalculateVisibleRange()
    {
        _startIndex = Math.Max(0, (_scrollTop / ItemHeight) - _bufferSize);
        _endIndex = Math.Min(Messages.Count - 1, _startIndex + _visibleItemCount + (_bufferSize * 2));
    }

    private void OnScroll(ChangeEventArgs args)
    {
        _scrollTop = int.Parse(args.Value?.ToString() ?? "0");
        CalculateVisibleRange();
        StateHasChanged();
    }

    private List<ChatMessage> GetVisibleMessages()
    {
        if (_startIndex >= Messages.Count) return new List<ChatMessage>();

        var count = Math.Min(_endIndex - _startIndex + 1, Messages.Count - _startIndex);
        return Messages.GetRange(_startIndex, count);
    }
}
```

## Error Handling and Recovery

### 1. Connection Recovery

```csharp
public class ConnectionRecoveryService
{
    private readonly IHubConnectionService _hubConnection;
    private readonly ILogger<ConnectionRecoveryService> _logger;
    private readonly Timer _reconnectionTimer;

    public async Task StartMonitoringAsync()
    {
        _hubConnection.Closed += OnConnectionClosed;
        _hubConnection.Reconnecting += OnReconnecting;
        _hubConnection.Reconnected += OnReconnected;
    }

    private async Task OnConnectionClosed(Exception? exception)
    {
        _logger.LogWarning(exception, "SignalR connection closed");

        // Start reconnection attempts
        await StartReconnectionAttemptsAsync();
    }

    private async Task OnReconnecting(Exception? exception)
    {
        _logger.LogInformation("Attempting to reconnect to SignalR hub");
        await _stateManager.SetConnectionStatusAsync(ConnectionStatus.Reconnecting);
    }

    private async Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("Successfully reconnected to SignalR hub with connection {ConnectionId}", connectionId);

        await _stateManager.SetConnectionStatusAsync(ConnectionStatus.Connected);

        // Rejoin agent groups
        await RejoinAgentGroupsAsync();

        // Sync missed messages
        await SyncMissedMessagesAsync();
    }

    private async Task StartReconnectionAttemptsAsync()
    {
        var attempt = 0;
        var maxAttempts = 10;
        var baseDelay = TimeSpan.FromSeconds(1);

        while (attempt < maxAttempts && _hubConnection.State == HubConnectionState.Disconnected)
        {
            attempt++;
            var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000); // Exponential backoff

            _logger.LogInformation("Reconnection attempt {Attempt} in {Delay} seconds", attempt, delay.TotalSeconds);

            await Task.Delay(delay);

            try
            {
                await _hubConnection.StartAsync();
                _logger.LogInformation("Reconnection successful on attempt {Attempt}", attempt);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reconnection attempt {Attempt} failed", attempt);
            }
        }

        if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            _logger.LogError("Failed to reconnect after {MaxAttempts} attempts", maxAttempts);
            await _stateManager.SetConnectionStatusAsync(ConnectionStatus.Failed);
        }
    }

    private async Task SyncMissedMessagesAsync()
    {
        var lastMessageTimestamp = await _stateManager.GetLastMessageTimestampAsync();
        var missedMessages = await _chatHistory.GetMessagesSinceAsync(lastMessageTimestamp);

        foreach (var message in missedMessages)
        {
            await _stateManager.AddMessageAsync(message);
        }

        _logger.LogInformation("Synced {Count} missed messages", missedMessages.Count);
    }
}
```

### 2. Message Delivery Guarantees

```csharp
public class MessageDeliveryService
{
    private readonly IChatHistoryService _chatHistory;
    private readonly IMessageQueue _messageQueue;
    private readonly ILogger<MessageDeliveryService> _logger;

    // Guaranteed message delivery with retry
    public async Task<DeliveryResult> SendMessageWithGuaranteeAsync(ChatMessage message)
    {
        var deliveryAttempt = new MessageDeliveryAttempt
        {
            MessageId = message.Id,
            Attempts = 0,
            MaxAttempts = 3,
            FirstAttemptAt = DateTime.UtcNow
        };

        while (deliveryAttempt.Attempts < deliveryAttempt.MaxAttempts)
        {
            deliveryAttempt.Attempts++;

            try
            {
                // Attempt delivery
                await _hubConnection.SendAsync("ReceiveMessage", message);

                // Wait for acknowledgment
                var ackReceived = await WaitForAcknowledgmentAsync(message.Id, TimeSpan.FromSeconds(5));

                if (ackReceived)
                {
                    deliveryAttempt.DeliveredAt = DateTime.UtcNow;
                    await _chatHistory.UpdateDeliveryStatusAsync(message.Id, DeliveryStatus.Delivered);

                    return DeliveryResult.Success(deliveryAttempt);
                }
                else
                {
                    _logger.LogWarning("No acknowledgment received for message {MessageId}, attempt {Attempt}",
                        message.Id, deliveryAttempt.Attempts);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deliver message {MessageId}, attempt {Attempt}",
                    message.Id, deliveryAttempt.Attempts);
            }

            // Wait before retry (exponential backoff)
            if (deliveryAttempt.Attempts < deliveryAttempt.MaxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, deliveryAttempt.Attempts) * 1000);
                await Task.Delay(delay);
            }
        }

        // All attempts failed
        await _chatHistory.UpdateDeliveryStatusAsync(message.Id, DeliveryStatus.Failed);
        return DeliveryResult.Failed(deliveryAttempt);
    }

    private async Task<bool> WaitForAcknowledgmentAsync(string messageId, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var ackStatus = await _chatHistory.GetAcknowledgmentStatusAsync(messageId);
                if (ackStatus == AcknowledgmentStatus.Acknowledged)
                {
                    return true;
                }

                await Task.Delay(100, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout occurred
        }

        return false;
    }
}
```

## Integration Architecture

### 1. Integration with Existing Systems

The Agent Chat Feature integrates seamlessly with existing Orchestra components:

- **OrchestratorService**: Real-time task execution status
- **AgentSidebar**: Live agent status updates
- **TaskQueue**: Interactive task management
- **RepositorySelector**: Context-aware agent assignment
- **SQLite Database**: Chat history and message persistence
- **Hangfire**: Background message processing and cleanup

### 2. Data Persistence Integration

```csharp
public class ChatDataIntegration
{
    private readonly OrchestraDbContext _dbContext;
    private readonly IChatHistoryRepository _chatRepository;

    // Chat message persistence
    public async Task SaveChatMessageAsync(ChatMessage message)
    {
        var entity = new ChatMessageEntity
        {
            Id = message.Id,
            SessionId = message.SessionId,
            AgentId = message.AgentId,
            UserId = message.UserId,
            MessageType = message.Type,
            Content = message.Content,
            Metadata = JsonSerializer.Serialize(message.Metadata),
            Timestamp = message.Timestamp,
            DeliveryStatus = DeliveryStatus.Pending
        };

        _dbContext.ChatMessages.Add(entity);
        await _dbContext.SaveChangesAsync();
    }

    // Integration with agent execution tracking
    public async Task LinkMessageToTaskAsync(string messageId, string taskId)
    {
        var message = await _dbContext.ChatMessages.FindAsync(messageId);
        var task = await _dbContext.Tasks.FindAsync(taskId);

        if (message != null && task != null)
        {
            message.LinkedTaskId = taskId;
            await _dbContext.SaveChangesAsync();
        }
    }

    // Cross-system analytics
    public async Task<ChatAnalytics> GetChatAnalyticsAsync(TimeSpan period)
    {
        var fromDate = DateTime.UtcNow.Subtract(period);

        var analytics = await _dbContext.ChatMessages
            .Where(m => m.Timestamp >= fromDate)
            .GroupBy(m => new { m.AgentId, Date = m.Timestamp.Date })
            .Select(g => new
            {
                g.Key.AgentId,
                g.Key.Date,
                MessageCount = g.Count(),
                UserMessages = g.Count(m => m.MessageType == MessageType.UserCommand),
                AgentMessages = g.Count(m => m.MessageType == MessageType.AgentResponse)
            })
            .ToListAsync();

        return new ChatAnalytics
        {
            Period = period,
            TotalMessages = analytics.Sum(a => a.MessageCount),
            TotalUserCommands = analytics.Sum(a => a.UserMessages),
            TotalAgentResponses = analytics.Sum(a => a.AgentMessages),
            ActiveAgents = analytics.Select(a => a.AgentId).Distinct().Count(),
            DailyBreakdown = analytics.GroupBy(a => a.Date)
                .ToDictionary(g => g.Key, g => g.ToList())
        };
    }
}
```

## Monitoring and Analytics

### 1. Real-time Performance Monitoring

```csharp
public class ChatPerformanceMonitor
{
    private readonly IMetricsCollector _metrics;
    private readonly ILogger<ChatPerformanceMonitor> _logger;

    public async Task TrackMessageProcessingTime(Func<Task> operation, string operationType)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await operation();
            stopwatch.Stop();

            _metrics.RecordValue($"chat.{operationType}.processing_time", stopwatch.ElapsedMilliseconds);
            _metrics.Increment($"chat.{operationType}.success");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _metrics.RecordValue($"chat.{operationType}.error_time", stopwatch.ElapsedMilliseconds);
            _metrics.Increment($"chat.{operationType}.error");

            _logger.LogError(ex, "Chat operation {OperationType} failed after {Duration}ms",
                operationType, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    public async Task MonitorConnectionHealth()
    {
        var activeConnections = await _connectionManager.GetActiveConnectionCountAsync();
        var totalMessages = await _chatHistory.GetMessageCountLastHourAsync();
        var averageLatency = await _connectionOptimizer.GetAverageLatencyAsync();

        _metrics.RecordValue("chat.connections.active", activeConnections);
        _metrics.RecordValue("chat.messages.per_hour", totalMessages);
        _metrics.RecordValue("chat.latency.average", averageLatency.TotalMilliseconds);

        // Alert on performance thresholds
        if (averageLatency > TimeSpan.FromSeconds(2))
        {
            _logger.LogWarning("High chat latency detected: {Latency}ms", averageLatency.TotalMilliseconds);
        }

        if (activeConnections > 1000)
        {
            _logger.LogWarning("High connection count detected: {Count} active connections", activeConnections);
        }
    }
}
```

This comprehensive architecture provides a robust, secure, and performant foundation for real-time agent communication while maintaining seamless integration with the existing AI Agent Orchestra system.