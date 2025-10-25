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
            // NOTE: Connection logic will be implemented in Task 3.2A
            ShowConnectionDialog = false;
            StateHasChanged();
        }

        private async Task DisconnectAsync()
        {
            // NOTE: Disconnection logic will be implemented in Task 3.2A
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                _sessionId = null;
                StateHasChanged();
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
