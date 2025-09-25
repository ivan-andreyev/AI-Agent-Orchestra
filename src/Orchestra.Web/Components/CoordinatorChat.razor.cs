using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;

namespace Orchestra.Web.Components;

public partial class CoordinatorChat
{
    private HubConnection? _hubConnection;
    private ElementReference commandInput;
    private readonly List<ChatMessage> _messages = new();
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;
    private string _currentCommand = string.Empty;
    private string _connectionState = "Disconnected";
    private bool _isConnecting = false;
    private bool _isProcessingCommand = false;

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

    protected override async Task RefreshDataAsync()
    {
        // Update connection state
        if (_hubConnection != null)
        {
            var previousState = _connectionState;
            _connectionState = _hubConnection.State.ToString();

            if (previousState != _connectionState)
            {
                Logger.LogInformation(
                    "CoordinatorChat: Connection state changed from {PreviousState} to {CurrentState}",
                    previousState, _connectionState);
                StateHasChanged();
            }
        }
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
                _hubConnection.On<CoordinatorResponse>("ReceiveResponse", (responseData) =>
                {
                    InvokeAsync(() =>
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
                                    Timestamp = responseData.Timestamp.ToLocalTime()
                                });

                                _isProcessingCommand = false;
                                StateHasChanged();
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
                _hubConnection.On<HistoryMessage>("ReceiveHistoryMessage", (historyData) =>
                {
                    InvokeAsync(() =>
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
                        _connectionState = "Reconnecting";
                        AddSystemMessage("🔄 Connection lost, reconnecting...", "warning");
                        StateHasChanged();
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
                        _connectionState = "Connected";
                        AddSystemMessage("✅ Reconnected to coordinator", "success");
                        StateHasChanged();
                        return Task.CompletedTask;
                    });
                    return Task.CompletedTask;
                };

                _hubConnection.Closed += (error) =>
                {
                    InvokeAsync(() =>
                    {
                        Logger.LogWarning("CoordinatorChat: Connection closed. Error: {Error}", error?.Message);
                        _connectionState = "Disconnected";
                        _isConnecting = false;
                        AddSystemMessage("❌ Connection closed", "error");
                        StateHasChanged();
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
            _connectionState = "Connecting";
            StateHasChanged();

            Logger.LogInformation("CoordinatorChat: Attempting to connect to SignalR hub");
            await _hubConnection.StartAsync();

            Logger.LogInformation("CoordinatorChat: Successfully connected to SignalR hub");
            _connectionState = "Connected";
            AddSystemMessage("🟢 Connected to coordinator agent", "success");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "CoordinatorChat: Failed to connect to SignalR hub");
            _connectionState = "Disconnected";
            AddErrorMessage($"Connection failed: {ex.Message}");
        }
        finally
        {
            _isConnecting = false;
            // Update connection state based on actual hub state
            if (_hubConnection != null)
            {
                _connectionState = _hubConnection.State.ToString();
            }

            StateHasChanged();
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

                // Add command to messages
                _messages.Add(new ChatMessage
                {
                    Message = command,
                    Type = "command",
                    Timestamp = DateTime.Now
                });

                _isProcessingCommand = true;
                StateHasChanged();

                // Send to hub
                await _hubConnection!.SendAsync("SendCommand", command);
                LoggingService.LogSignalREvent("CoordinatorChatHub", "SendCommand",
                    _hubConnection.ConnectionId, new { Command = command });

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
    private void AddSystemMessage(string message, string type = "info")
    {
        _messages.Add(new ChatMessage
        {
            Message = message,
            Type = type,
            Timestamp = DateTime.Now
        });
        StateHasChanged();
    }

    /// <summary>
    /// Adds an error message to the chat
    /// </summary>
    private void AddErrorMessage(string message)
    {
        AddSystemMessage($"❌ {message}", "error");
    }

    /// <summary>
    /// Gets connection status text with icon
    /// </summary>
    private string GetConnectionStatusText()
    {
        return _connectionState switch
        {
            "Connected" => "🟢 Connected",
            "Connecting" => "🟡 Connecting",
            "Reconnecting" => "🟡 Reconnecting",
            "Disconnected" => "🔴 Disconnected",
            _ => $"🔴 {_connectionState}"
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
            var defaultUrl = "http://localhost:55001/coordinatorHub";
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