using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace Orchestra.Web.Components.AgentTerminal
{
    /// <summary>
    /// Blazor component for interactive agent terminal with real-time output streaming.
    /// </summary>
    public partial class AgentTerminalComponent : ComponentBase, IAsyncDisposable
    {
        [Parameter] public string? AgentId { get; set; }
        [Parameter] public string ConnectorType { get; set; } = "terminal";
        [Parameter] public EventCallback<string> OnCommandExecuted { get; set; }

        private HubConnection? _hubConnection;
        private string? _sessionId;
        private ElementReference _outputElement;
        private string _commandInput = string.Empty;
        private List<OutputLine> OutputLines { get; set; } = new();
        private List<string> _commandHistory = new();
        private int _historyIndex = -1;

        private bool IsConnected => _hubConnection?.State == HubConnectionState.Connected && !string.IsNullOrEmpty(_sessionId);
        private string ConnectionStatus => GetConnectionStatus();
        private bool AutoScroll { get; set; } = true;
        private bool ShowConnectionDialog { get; set; }

        private string _connectAgentId = string.Empty;
        private string _connectType = "terminal";

        protected override async Task OnInitializedAsync()
        {
            // Component initialization - hub connection will be established in ConnectAsync
            await base.OnInitializedAsync();
        }

        /// <summary>
        /// Initializes SignalR hub connection with automatic reconnection.
        /// </summary>
        private async Task InitializeHubConnection()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(Navigation.ToAbsoluteUri("/hubs/agent-interaction"))
                .WithAutomaticReconnect()
                .Build();

            // Configure reconnection event handlers
            _hubConnection.Reconnecting += OnReconnecting;
            _hubConnection.Reconnected += OnReconnected;
            _hubConnection.Closed += OnClosed;

            // Register server-to-client event handlers
            _hubConnection.On<CommandSentNotification>("CommandSent", OnCommandSent);
            _hubConnection.On<string>("SessionClosed", OnSessionClosed);
            _hubConnection.On<string>("CommandError", OnCommandError);

            await _hubConnection.StartAsync();
        }

        private async Task OnReconnecting(Exception? exception)
        {
            AddSystemLine("Connection lost. Attempting to reconnect...");
            await InvokeAsync(StateHasChanged);
        }

        private async Task OnReconnected(string? connectionId)
        {
            AddSystemLine("Reconnected successfully.");

            // Re-establish session if we had one
            if (!string.IsNullOrEmpty(_sessionId))
            {
                await ReconnectToSession();
            }

            await InvokeAsync(StateHasChanged);
        }

        private async Task OnClosed(Exception? exception)
        {
            if (exception != null)
            {
                AddErrorLine($"Connection closed with error: {exception.Message}");
            }
            else
            {
                AddSystemLine("Connection closed.");
            }

            _sessionId = null;
            await InvokeAsync(StateHasChanged);
        }

        private void OnCommandSent(CommandSentNotification notification)
        {
            // Handle command sent notification from server
            if (notification.Success)
            {
                AddSystemLine($"Command executed at {notification.Timestamp:HH:mm:ss}");
            }
        }

        private void OnSessionClosed(string sessionId)
        {
            if (sessionId == _sessionId)
            {
                AddSystemLine($"Session {sessionId} closed by server.");
                _sessionId = null;
                StateHasChanged();
            }
        }

        private void OnCommandError(string errorMessage)
        {
            AddErrorLine($"Command error: {errorMessage}");
            StateHasChanged();
        }

        private async Task ReconnectToSession()
        {
            // NOTE: Reconnection logic for session recovery after network interruption
            AddSystemLine($"Attempting to reconnect to session {_sessionId}...");

            // Session recovery not yet implemented - would need to check if session still exists
            // and re-subscribe to output stream
        }

        private string GetConnectionStatus()
        {
            if (_hubConnection == null)
            {
                return "Disconnected";
            }

            return _hubConnection.State switch
            {
                HubConnectionState.Connected when !string.IsNullOrEmpty(_sessionId) => "Connected",
                HubConnectionState.Connected => "Connecting...",
                HubConnectionState.Connecting => "Connecting...",
                HubConnectionState.Reconnecting => "Reconnecting...",
                HubConnectionState.Disconnected => "Disconnected",
                _ => "Unknown"
            };
        }

        private string GetStatusClass()
        {
            return IsConnected ? "status-connected" : "status-disconnected";
        }

        private string GetStatusIcon()
        {
            return IsConnected ? "bi-check-circle-fill" : "bi-x-circle";
        }

        private string GetLineClass(OutputLine line)
        {
            return line.Type switch
            {
                OutputLineType.Command => "line-command",
                OutputLineType.Error => "line-error",
                OutputLineType.System => "line-system",
                OutputLineType.KeepAlive => "line-keepalive",
                _ => "line-standard"
            };
        }

        private void ClearOutput()
        {
            OutputLines.Clear();
            StateHasChanged();
        }

        private void ToggleAutoScroll()
        {
            AutoScroll = !AutoScroll;
            StateHasChanged();
        }

        private async Task ConnectAsync()
        {
            try
            {
                AddSystemLine($"Connecting to agent {_connectAgentId}...");

                // Initialize hub connection if not already connected
                if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
                {
                    await InitializeHubConnection();
                }

                // Create connection request
                var request = new ConnectToAgentRequest(
                    _connectAgentId,
                    _connectType,
                    new Dictionary<string, string>());

                // Invoke server method to connect to agent
                var response = await _hubConnection.InvokeAsync<ConnectToAgentResponse>(
                    "ConnectToAgent",
                    request);

                if (response.Success)
                {
                    _sessionId = response.SessionId;
                    AgentId = _connectAgentId;
                    AddSystemLine($"Connected to agent. Session: {_sessionId}");

                    // Start streaming output in background
                    _ = StreamOutputAsync();
                }
                else
                {
                    AddErrorLine($"Failed to connect: {response.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                AddErrorLine($"Connection error: {ex.Message}");
            }
            finally
            {
                ShowConnectionDialog = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task DisconnectAsync()
        {
            if (_hubConnection != null && !string.IsNullOrEmpty(_sessionId))
            {
                try
                {
                    AddSystemLine("Disconnecting...");

                    // Invoke server method to disconnect from agent
                    var result = await _hubConnection.InvokeAsync<bool>(
                        "DisconnectFromAgent",
                        _sessionId);

                    if (result)
                    {
                        AddSystemLine("Disconnected successfully.");
                    }
                }
                catch (Exception ex)
                {
                    AddErrorLine($"Disconnect error: {ex.Message}");
                }
                finally
                {
                    _sessionId = null;
                    await InvokeAsync(StateHasChanged);
                }
            }
        }

        private void CloseConnectionDialog()
        {
            ShowConnectionDialog = false;
            StateHasChanged();
        }

        private async Task SendCommandAsync()
        {
            // NOTE: Command sending logic will be implemented in Task 3.2C
            if (!IsConnected || string.IsNullOrWhiteSpace(_commandInput))
            {
                return;
            }

            var command = _commandInput;
            _commandHistory.Add(command);
            _historyIndex = _commandHistory.Count;
            _commandInput = string.Empty;

            // Add command to output
            OutputLines.Add(new OutputLine($"$ {command}", DateTime.UtcNow, OutputLineType.Command));
            StateHasChanged();

            // Notify parent component
            await OnCommandExecuted.InvokeAsync(command);
        }

        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
            {
                await SendCommandAsync();
            }
            else if (e.Key == "ArrowUp")
            {
                // Command history navigation - up
                if (_historyIndex > 0)
                {
                    _historyIndex--;
                    _commandInput = _commandHistory[_historyIndex];
                    StateHasChanged();
                }
            }
            else if (e.Key == "ArrowDown")
            {
                // Command history navigation - down
                if (_historyIndex < _commandHistory.Count - 1)
                {
                    _historyIndex++;
                    _commandInput = _commandHistory[_historyIndex];
                    StateHasChanged();
                }
                else if (_historyIndex == _commandHistory.Count - 1)
                {
                    _historyIndex = _commandHistory.Count;
                    _commandInput = string.Empty;
                    StateHasChanged();
                }
            }
        }

        /// <summary>
        /// Adds a system message to the terminal output.
        /// </summary>
        private void AddSystemLine(string message)
        {
            OutputLines.Add(new OutputLine(message, DateTime.UtcNow, OutputLineType.System));
            ScrollToBottom();
        }

        /// <summary>
        /// Adds an error message to the terminal output.
        /// </summary>
        private void AddErrorLine(string message)
        {
            OutputLines.Add(new OutputLine(message, DateTime.UtcNow, OutputLineType.Error));
            ScrollToBottom();
        }

        /// <summary>
        /// Adds a standard output line to the terminal.
        /// </summary>
        private void AddOutputLine(string message)
        {
            OutputLines.Add(new OutputLine(message, DateTime.UtcNow, OutputLineType.Standard));
            ScrollToBottom();
        }

        /// <summary>
        /// Scrolls terminal output to bottom if auto-scroll is enabled.
        /// </summary>
        private async void ScrollToBottom()
        {
            if (AutoScroll)
            {
                try
                {
                    await JSRuntime.InvokeVoidAsync("scrollToBottom", _outputElement);
                }
                catch
                {
                    // JS interop may fail during disposal or if element not ready
                }
            }
        }

        /// <summary>
        /// Streams output from agent via SignalR in background.
        /// NOTE: Output streaming implementation will be in Task 3.2B.
        /// </summary>
        private async Task StreamOutputAsync()
        {
            if (_hubConnection == null || string.IsNullOrEmpty(_sessionId))
            {
                return;
            }

            try
            {
                // NOTE: StreamOutput will be implemented in Task 3.2B
                // For now, this is a placeholder that will be replaced with actual streaming logic
                AddSystemLine("Output streaming will be implemented in Task 3.2B");
            }
            catch (Exception ex)
            {
                AddErrorLine($"Output streaming error: {ex.Message}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Represents a single line of output in the terminal.
    /// </summary>
    public record OutputLine(string Content, DateTime Timestamp, OutputLineType Type = OutputLineType.Standard);

    /// <summary>
    /// Type of terminal output line for styling purposes.
    /// </summary>
    public enum OutputLineType
    {
        Standard,
        Command,
        Error,
        System,
        KeepAlive
    }
}
