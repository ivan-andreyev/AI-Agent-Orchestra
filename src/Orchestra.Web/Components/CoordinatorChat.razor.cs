using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace Orchestra.Web.Components;

public partial class CoordinatorChat
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;
    private HubConnection? _hubConnection;
    private ElementReference commandInput;
    private readonly List<ChatMessage> _messages = new();
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;
    private string _currentCommand = string.Empty;
    private string _connectionState = "Disconnected";
    private bool _isConnecting = false;
    private bool _isProcessingCommand = false;
    private long _localSequence = 0;
    private bool _showProgressMessages = false;

    /// <summary>
    /// Gets messages sorted by sequence and timestamp for consistent ordering
    /// </summary>
    private IEnumerable<ChatMessage> SortedMessages =>
        _messages.OrderBy(m => m.Sequence).ThenBy(m => m.Timestamp);

    /// <summary>
    /// Gets filtered messages based on user preferences (hides progress messages by default)
    /// </summary>
    private IEnumerable<ChatMessage> FilteredMessages
    {
        get
        {
            var sorted = SortedMessages;
            if (!_showProgressMessages)
            {
                return sorted.Where(m => m.Type != "progress");
            }
            return sorted;
        }
    }

    /// <summary>
    /// Gets count of hidden progress messages
    /// </summary>
    private int HiddenProgressCount =>
        _showProgressMessages ? 0 : _messages.Count(m => m.Type == "progress");

    /// <summary>
    /// Toggles visibility of progress messages
    /// </summary>
    private void ToggleProgressMessages()
    {
        _showProgressMessages = !_showProgressMessages;
        StateHasChanged();
    }

    /// <summary>
    /// Repository path for coordinator commands
    /// </summary>
    [Parameter]
    public string? SelectedRepositoryPath { get; set; }

    /// <summary>
    /// Indicates if the SignalR connection is established
    /// </summary>
    private bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    protected override async Task OnInitializedAsync()
    {
        using (LoggingService.MeasureOperation("CoordinatorChat", "OnInitializedAsync"))
        {
            LoggingService.LogComponentLifecycle("CoordinatorChat", "Initializing");

            await InitializeSignalRConnection();
            await base.OnInitializedAsync();

            LoggingService.LogComponentLifecycle("CoordinatorChat", "Initialized");
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await commandInput.FocusAsync();
        }
    }

    protected override Task RefreshDataAsync()
    {
        // Synchronize connection state with actual hub state
        if (_hubConnection != null)
        {
            UpdateConnectionState(_hubConnection.State.ToString());
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Initializes SignalR connection to CoordinatorChatHub
    /// </summary>
    private async Task InitializeSignalRConnection()
    {
        using (LoggingService.MeasureOperation("CoordinatorChat", "InitializeSignalRConnection"))
        {
            try
            {
                LoggingService.LogSignalREvent("CoordinatorChatHub", "InitializingConnection");

                var hubUrl = GetSignalRHubUrl();
                Logger.LogInformation("CoordinatorChat: Using SignalR Hub URL: {HubUrl}", hubUrl);

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                // Handle incoming responses from coordinator
                _hubConnection.On<CoordinatorResponse>("ReceiveResponse", async (responseData) =>
                {
                    await InvokeAsync(async () =>
                    {
                        try
                        {
                            if (responseData != null)
                            {
                                LoggingService.LogSignalREvent("CoordinatorChatHub", "ReceiveResponse",
                                    _hubConnection.ConnectionId,
                                    new
                                    {
                                        Type = responseData.Type, MessageLength = responseData.Message?.Length ?? 0
                                    });

                                _messages.Add(new ChatMessage
                                {
                                    Message = responseData.Message ?? string.Empty,
                                    Type = responseData.Type ?? "info",
                                    Timestamp = responseData.Timestamp.ToLocalTime(),
                                    Sequence = responseData.Sequence
                                });

                                _isProcessingCommand = false;
                                StateHasChanged();
                                await ScrollToBottomAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogError("CoordinatorChat", "ProcessSignalRResponse", ex, responseData);
                            AddErrorMessage($"Error processing response: {ex.Message}");
                        }
                    });
                });

                // Handle incoming chat history messages
                _hubConnection.On<HistoryMessage>("ReceiveHistoryMessage", async (historyData) =>
                {
                    await InvokeAsync(async () =>
                    {
                        try
                        {
                            if (historyData != null)
                            {
                                LoggingService.LogSignalREvent("CoordinatorChatHub", "ReceiveHistoryMessage",
                                    _hubConnection.ConnectionId,
                                    new
                                    {
                                        Type = historyData.Type, MessageLength = historyData.Message?.Length ?? 0,
                                        Author = historyData.Author
                                    });

                                _messages.Add(new ChatMessage
                                {
                                    Message = historyData.Message ?? string.Empty,
                                    Type = historyData.Type ?? "info",
                                    Timestamp = historyData.Timestamp.ToLocalTime(),
                                    Author = historyData.Author
                                });

                                StateHasChanged();
                                await ScrollToBottomAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogError("CoordinatorChat", "ProcessHistoryMessage", ex, historyData);
                            AddErrorMessage($"Error processing history message: {ex.Message}");
                        }
                    });
                });

                // Handle connection state changes
                _hubConnection.Reconnecting += (error) =>
                {
                    InvokeAsync(() =>
                    {
                        Logger.LogWarning("CoordinatorChat: Connection lost, attempting to reconnect. Error: {Error}",
                            error?.Message);
                        UpdateConnectionState("Reconnecting");
                        AddSystemMessage("🔄 Connection lost, reconnecting...", "warning");
                        return Task.CompletedTask;
                    });
                    return Task.CompletedTask;
                };

                _hubConnection.Reconnected += (connectionId) =>
                {
                    InvokeAsync(() =>
                    {
                        Logger.LogInformation("CoordinatorChat: Reconnected with connection ID: {ConnectionId}",
                            connectionId);
                        UpdateConnectionState("Connected");
                        AddSystemMessage("✅ Reconnected to coordinator", "success");
                        return Task.CompletedTask;
                    });
                    return Task.CompletedTask;
                };

                _hubConnection.Closed += (error) =>
                {
                    InvokeAsync(() =>
                    {
                        Logger.LogWarning("CoordinatorChat: Connection closed. Error: {Error}", error?.Message);
                        UpdateConnectionState("Disconnected");
                        _isConnecting = false;
                        AddSystemMessage("❌ Connection closed", "error");
                        return Task.CompletedTask;
                    });
                    return Task.CompletedTask;
                };

                await ConnectAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("CoordinatorChat", "InitializeSignalRConnection", ex);
                AddErrorMessage($"Failed to initialize connection: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Connects to the SignalR hub
    /// </summary>
    private async Task ConnectAsync()
    {
        if (_hubConnection == null || _isConnecting)
            return;

        try
        {
            _isConnecting = true;
            UpdateConnectionState("Connecting");

            Logger.LogInformation("CoordinatorChat: Attempting to connect to SignalR hub");
            await _hubConnection.StartAsync();

            Logger.LogInformation("CoordinatorChat: Successfully connected to SignalR hub");
            UpdateConnectionState("Connected");
            AddSystemMessage("🟢 Connected to coordinator agent", "success");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "CoordinatorChat: Failed to connect to SignalR hub");
            UpdateConnectionState("Disconnected");
            AddErrorMessage($"Connection failed: {ex.Message}");
        }
        finally
        {
            _isConnecting = false;
            // Always sync connection state with actual hub state
            if (_hubConnection != null)
            {
                UpdateConnectionState(_hubConnection.State.ToString());
            }
        }
    }

    /// <summary>
    /// Reconnects to the SignalR hub
    /// </summary>
    private async Task ReconnectAsync()
    {
        if (_hubConnection == null)
        {
            await InitializeSignalRConnection();
            return;
        }

        try
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.StopAsync();
            }

            await ConnectAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "CoordinatorChat: Failed to reconnect");
            AddErrorMessage($"Reconnection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a command to the coordinator
    /// </summary>
    private async Task SendCommandAsync()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(_currentCommand) || _isProcessingCommand)
        {
            LoggingService.LogUserInteraction("CoordinatorChat", "SendCommandCancelled",
                new
                {
                    IsConnected, HasCommand = !string.IsNullOrWhiteSpace(_currentCommand),
                    IsProcessing = _isProcessingCommand
                });
            return;
        }

        using (LoggingService.MeasureOperation("CoordinatorChat", "SendCommand"))
        {
            try
            {
                var command = _currentCommand.Trim();
                LoggingService.LogUserInteraction("CoordinatorChat", "SendCommand", new { Command = command });

                // Add command to history
                if (!_commandHistory.Contains(command))
                {
                    _commandHistory.Insert(0, command);
                    if (_commandHistory.Count > 50) // Limit history size
                    {
                        _commandHistory.RemoveAt(_commandHistory.Count - 1);
                    }
                }

                _historyIndex = -1;

                // Add command to messages with local sequence
                _localSequence = System.Threading.Interlocked.Increment(ref _localSequence);
                _messages.Add(new ChatMessage
                {
                    Message = command,
                    Type = "command",
                    Timestamp = DateTime.Now,
                    Sequence = _localSequence
                });

                _isProcessingCommand = true;
                StateHasChanged();
                await ScrollToBottomAsync();

                // Send to hub with repository path
                await _hubConnection!.SendAsync("SendCommand", command, SelectedRepositoryPath);
                LoggingService.LogSignalREvent("CoordinatorChatHub", "SendCommand",
                    _hubConnection.ConnectionId, new { Command = command, RepositoryPath = SelectedRepositoryPath ?? "default" });

                // Clear input
                _currentCommand = string.Empty;
                await commandInput.FocusAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("CoordinatorChat", "SendCommand", ex, new { Command = _currentCommand });
                AddErrorMessage($"Failed to send command: {ex.Message}");
                _isProcessingCommand = false;
            }

            StateHasChanged();
        }
    }

    /// <summary>
    /// Handles keyboard input for command history navigation
    /// </summary>
    private async Task OnKeyPress(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SendCommandAsync();
        }
        else if (e.Key == "ArrowUp")
        {
            NavigateCommandHistory(1);
        }
        else if (e.Key == "ArrowDown")
        {
            NavigateCommandHistory(-1);
        }
    }

    /// <summary>
    /// Navigates through command history
    /// </summary>
    private void NavigateCommandHistory(int direction)
    {
        if (!_commandHistory.Any())
            return;

        _historyIndex += direction;
        _historyIndex = Math.Max(-1, Math.Min(_historyIndex, _commandHistory.Count - 1));

        _currentCommand = _historyIndex >= 0 ? _commandHistory[_historyIndex] : string.Empty;
        StateHasChanged();
    }

    /// <summary>
    /// Adds a system message to the chat
    /// </summary>
    private async void AddSystemMessage(string message, string type = "info")
    {
        _localSequence = System.Threading.Interlocked.Increment(ref _localSequence);
        _messages.Add(new ChatMessage
        {
            Message = message,
            Type = type,
            Timestamp = DateTime.Now,
            Sequence = _localSequence
        });
        StateHasChanged();
        await ScrollToBottomAsync();
    }

    /// <summary>
    /// Adds an error message to the chat
    /// </summary>
    private void AddErrorMessage(string message)
    {
        AddSystemMessage($"❌ {message}", "error");
    }

    /// <summary>
    /// Updates connection state and triggers UI refresh
    /// </summary>
    private void UpdateConnectionState(string newState)
    {
        if (_connectionState != newState)
        {
            var previousState = _connectionState;
            _connectionState = newState;

            Logger.LogInformation(
                "CoordinatorChat: Connection state changed from {PreviousState} to {CurrentState}",
                previousState, newState);

            StateHasChanged();
        }
    }

    /// <summary>
    /// Gets connection status text with icon based on actual hub connection state
    /// </summary>
    private string GetConnectionStatusText()
    {
        // Use actual hub connection state if available, otherwise use _connectionState
        var actualState = _hubConnection?.State.ToString() ?? _connectionState;

        return actualState switch
        {
            "Connected" => "🟢 Connected",
            "Connecting" => "🟡 Connecting",
            "Reconnecting" => "🟡 Reconnecting",
            "Disconnected" => "🔴 Disconnected",
            "Disconnecting" => "🟡 Disconnecting",
            _ => $"🔴 {actualState}"
        };
    }

    /// <summary>
    /// Gets CSS class for connection status based on actual hub connection state
    /// </summary>
    private string GetConnectionStatusClass()
    {
        // Use actual hub connection state if available, otherwise use _connectionState
        var actualState = _hubConnection?.State.ToString() ?? _connectionState;

        return actualState.ToLowerInvariant() switch
        {
            "connected" => "connected",
            "connecting" => "connecting",
            "reconnecting" => "connecting",
            "disconnected" => "disconnected",
            "disconnecting" => "disconnecting",
            _ => "disconnected"
        };
    }

    /// <summary>
    /// Formats message content for display (supports basic markdown)
    /// </summary>
    private string FormatMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        // Simple markdown formatting
        return message
            .Replace("**", "<strong>", StringComparison.OrdinalIgnoreCase)
            .Replace("**", "</strong>", StringComparison.OrdinalIgnoreCase)
            .Replace("`", "<code>", StringComparison.OrdinalIgnoreCase)
            .Replace("`", "</code>", StringComparison.OrdinalIgnoreCase)
            .Replace("\n", "<br/>", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scrolls chat to the last message
    /// </summary>
    private async Task ScrollToBottomAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("coordinatorChat.scrollToBottom", "#coordinator-chat-messages");
        }
        catch (Exception ex)
        {
            // Silently ignore JS interop errors (e.g., during pre-render)
            Logger.LogDebug(ex, "CoordinatorChat: Failed to scroll to bottom");
        }
    }

    /// <summary>
    /// Toggles the collapsed state of a message
    /// </summary>
    private void ToggleMessageCollapse(ChatMessage message)
    {
        message.IsCollapsed = !message.IsCollapsed;
        StateHasChanged();
    }

    /// <summary>
    /// Gets the display text for a message, truncated if collapsed
    /// </summary>
    private string GetDisplayMessage(ChatMessage message)
    {
        if (!message.IsLongMessage || !message.IsCollapsed)
        {
            return message.Message;
        }

        // Show first 1000 characters when collapsed
        return message.Message.Length > 1000
            ? message.Message[..1000] + "..."
            : message.Message;
    }

    /// <summary>
    /// Gets SignalR Hub URL from configuration with fallback logic
    /// </summary>
    private string GetSignalRHubUrl()
    {
        try
        {
            // Get or create persistent user ID
            var userId = GetOrCreatePersistentUserId();

            // Try to get primary URL from configuration
            var primaryUrl = Configuration["SignalR:HubUrl"];
            if (!string.IsNullOrWhiteSpace(primaryUrl))
            {
                Logger.LogDebug("CoordinatorChat: Using primary SignalR URL from configuration: {Url}", primaryUrl);
                return AppendUserIdToUrl(primaryUrl, userId);
            }

            // Try fallback URL from configuration
            var fallbackUrl = Configuration["SignalR:FallbackUrl"];
            if (!string.IsNullOrWhiteSpace(fallbackUrl))
            {
                Logger.LogWarning("CoordinatorChat: Primary URL not configured, using fallback URL: {Url}",
                    fallbackUrl);
                return AppendUserIdToUrl(fallbackUrl, userId);
            }

            // Final fallback to hardcoded development URL
            var defaultUrl = "http://localhost:55002/coordinatorHub";
            Logger.LogWarning("CoordinatorChat: No SignalR configuration found, using default URL: {Url}", defaultUrl);
            return AppendUserIdToUrl(defaultUrl, userId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "CoordinatorChat: Error reading SignalR configuration, using default URL");
            return "http://localhost:55001/coordinatorHub";
        }
    }

    /// <summary>
    /// Gets or creates a persistent user ID for chat session continuity
    /// </summary>
    private string GetOrCreatePersistentUserId()
    {
        // Use the exact same approach as HTML client for compatibility
        // This ensures both clients see the same chat history
        var sharedUserId = "shared_user_" + DateTime.Now.ToString("yyyyMMdd");
        var hash = JavaScriptHashCode(sharedUserId);
        var userId = $"user_{Math.Abs(hash):x}";

        Logger.LogInformation("CoordinatorChat: Using shared persistent user ID: {UserId}", userId);
        return userId;
    }

    /// <summary>
    /// JavaScript-compatible hash function to match HTML client
    /// </summary>
    private int JavaScriptHashCode(string str)
    {
        int hash = 0;
        for (int i = 0; i < str.Length; i++)
        {
            var ch = str[i];
            hash = ((hash << 5) - hash) + ch;
            hash = hash & hash; // Convert to 32bit integer
        }
        return hash;
    }

    /// <summary>
    /// Appends user ID to SignalR URL as query parameter
    /// </summary>
    private string AppendUserIdToUrl(string baseUrl, string userId)
    {
        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}userId={Uri.EscapeDataString(userId)}";
    }

    /// <summary>
    /// Освобождает ресурсы компонента, включая соединение SignalR
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Logger.LogInformation("CoordinatorChat: Disposing component");

        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }

    /// <summary>
    /// Represents a chat message
    /// </summary>
    private class ChatMessage
    {
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? Author { get; set; }
        public bool IsCollapsed { get; set; } = true;
        public bool IsLongMessage => Message.Length > 1000;
        public long Sequence { get; set; } = 0;
    }

    /// <summary>
    /// Represents a coordinator response
    /// </summary>
    private class CoordinatorResponse
    {
        [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;

        [JsonPropertyName("type")] public string Type { get; set; } = "info";

        [JsonPropertyName("timestamp")] public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("taskId")] public string? TaskId { get; set; }

        [JsonPropertyName("sequence")] public long Sequence { get; set; } = 0;
    }

    /// <summary>
    /// Represents a chat history message
    /// </summary>
    private class HistoryMessage
    {
        [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;

        [JsonPropertyName("type")] public string Type { get; set; } = "info";

        [JsonPropertyName("author")] public string Author { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")] public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}