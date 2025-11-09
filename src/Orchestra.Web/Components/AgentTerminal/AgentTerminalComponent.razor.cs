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
        private CancellationTokenSource _streamCancellation = new();
        private DateTime _lastKeepAlive = DateTime.Now;
        private bool _pendingScrollToBottom;
        private bool _isConnecting;

        private bool IsConnected => _hubConnection?.State == HubConnectionState.Connected && !string.IsNullOrEmpty(_sessionId);
        private string ConnectionStatus => GetConnectionStatus();
        private bool AutoScroll { get; set; } = true;
        private bool ShowConnectionDialog { get; set; }

        private string _connectAgentId = string.Empty;
        private string _connectType = "terminal";

        // Advanced connection options
        private bool _showAdvancedOptions = false;
        private string _manualProcessId = string.Empty;
        private string _manualPipeName = string.Empty;
        private string _manualSocketPath = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            // Pre-fill connection dialog with AgentId from parameter
            if (!string.IsNullOrEmpty(AgentId))
            {
                _connectAgentId = AgentId;
            }

            // Show connection dialog if not connected on first load
            if (!IsConnected)
            {
                ShowConnectionDialog = true;
            }

            await base.OnInitializedAsync();
        }

        protected override void OnParametersSet()
        {
            // Update connection dialog when AgentId parameter changes
            if (!string.IsNullOrEmpty(AgentId) && _connectAgentId != AgentId)
            {
                _connectAgentId = AgentId;
            }

            base.OnParametersSet();
        }

        /// <summary>
        /// Выполняется после рендеринга компонента для установки фокуса на поле ввода.
        /// </summary>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try
                {
                    await JSRuntime.InvokeVoidAsync("terminalFunctions.focusInput", ".terminal-input");
                }
                catch
                {
                    // Focus may fail if element not ready yet
                }
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        /// <summary>
        /// Initializes SignalR hub connection with automatic reconnection.
        /// </summary>
        private async Task InitializeHubConnection()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:55002/hubs/agent-interaction")
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
            _isConnecting = true;
            await InvokeAsync(StateHasChanged);

            try
            {
                AddSystemLine($"Connecting to agent {_connectAgentId}...");

                // Initialize hub connection if not already connected
                if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
                {
                    await InitializeHubConnection();
                }

                // Create connection parameters from manual inputs
                var connectionParams = new Dictionary<string, string>();

                if (!string.IsNullOrWhiteSpace(_manualProcessId))
                {
                    connectionParams["ProcessId"] = _manualProcessId;
                }

                if (!string.IsNullOrWhiteSpace(_manualPipeName))
                {
                    connectionParams["PipeName"] = _manualPipeName;
                }

                if (!string.IsNullOrWhiteSpace(_manualSocketPath))
                {
                    connectionParams["SocketPath"] = _manualSocketPath;
                }

                // Create connection request
                var request = new ConnectToAgentRequest(
                    _connectAgentId,
                    _connectType,
                    connectionParams);

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
                _isConnecting = false;
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

        /// <summary>
        /// Shows the connection dialog for connecting to an external agent.
        /// Public method to be called from parent component.
        /// </summary>
        public void ShowDialog()
        {
            ShowConnectionDialog = true;
            StateHasChanged();
        }

        /// <summary>
        /// Отправляет команду подключенному агенту через SignalR hub.
        /// </summary>
        private async Task SendCommandAsync()
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(_commandInput) || _hubConnection == null)
            {
                return;
            }

            var command = _commandInput;

            // Update UI immediately for responsiveness
            _commandHistory.Add(command);
            _historyIndex = _commandHistory.Count;
            _commandInput = string.Empty;

            // Add command to output
            OutputLines.Add(new OutputLine($"$ {command}", DateTime.UtcNow, OutputLineType.Command));
            StateHasChanged();

            try
            {
                // Send command to agent via SignalR hub
                var request = new SendCommandRequest(_sessionId!, command);
                var success = await _hubConnection.InvokeAsync<bool>("SendCommand", request);

                if (!success)
                {
                    AddErrorLine("Failed to send command to agent");
                }
            }
            catch (Exception ex)
            {
                AddErrorLine($"Error sending command: {ex.Message}");
            }

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
                    await JSRuntime.InvokeVoidAsync("terminalFunctions.scrollToBottom", _outputElement);
                }
                catch
                {
                    // JS interop may fail during disposal or if element not ready
                }
            }
        }

        /// <summary>
        /// Копирует весь вывод терминала в буфер обмена.
        /// </summary>
        private async Task CopyAllOutput()
        {
            try
            {
                var allText = string.Join("\n", OutputLines.Select(l => $"[{l.Timestamp:HH:mm:ss}] {l.Content}"));
                var success = await JSRuntime.InvokeAsync<bool>("terminalFunctions.copyToClipboard", allText);

                if (success)
                {
                    AddSystemLine("Output copied to clipboard");
                }
            }
            catch (Exception ex)
            {
                AddErrorLine($"Failed to copy: {ex.Message}");
            }
        }

        /// <summary>
        /// Streams output from agent via SignalR in background.
        /// </summary>
        private async Task StreamOutputAsync()
        {
            if (_hubConnection == null || string.IsNullOrEmpty(_sessionId))
            {
                return;
            }

            try
            {
                var cancellationToken = _streamCancellation.Token;

                // Start streaming from hub
                await foreach (var line in _hubConnection.StreamAsync<string>(
                    "StreamOutput",
                    _sessionId,
                    cancellationToken))
                {
                    // Process each line
                    ProcessStreamedLine(line);

                    // Update UI
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (OperationCanceledException)
            {
                // Streaming cancelled - this is normal on disconnect
                AddSystemLine("Output streaming stopped.");
            }
            catch (Exception ex)
            {
                AddErrorLine($"Streaming error: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a single line received from the output stream.
        /// </summary>
        private void ProcessStreamedLine(string line)
        {
            // Filter keep-alive messages
            if (line == "[KEEPALIVE]")
            {
                _lastKeepAlive = DateTime.Now;
                return;
            }

            // Add to output buffer
            AddOutputLine(line);

            // Handle auto-scroll
            if (AutoScroll)
            {
                _pendingScrollToBottom = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            // Cancel streaming
            _streamCancellation?.Cancel();
            _streamCancellation?.Dispose();

            // Dispose hub connection
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
