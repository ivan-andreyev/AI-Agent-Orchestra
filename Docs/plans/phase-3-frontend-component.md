# Phase 3: Frontend Component

**Parent Plan**: [Agent-Interaction-System-Implementation-Plan.md](./Agent-Interaction-System-Implementation-Plan.md)

**Goal**: Create Blazor terminal component for agent interaction UI

**Total Estimate**: 10-13 hours

**Dependencies**: Phase 2 must be complete (SignalR hub operational)

---

## Phase 3.1: Terminal Component Creation

**Estimate**: 4-5 hours

**Goal**: Build core Blazor component for terminal display and input

### Task 3.1A: Create AgentTerminalComponent Base (1-2 hours)

**Technical Steps**:

1. **Create component file structure**:
   ```
   src/Orchestra.Web/Components/AgentTerminal/
   ├── AgentTerminalComponent.razor
   ├── AgentTerminalComponent.razor.cs
   └── AgentTerminalComponent.razor.css
   ```

2. **Create component markup**:
   ```razor
   <!-- File: src/Orchestra.Web/Components/AgentTerminal/AgentTerminalComponent.razor -->
   @namespace Orchestra.Web.Components.AgentTerminal
   @inject NavigationManager Navigation
   @inject IJSRuntime JSRuntime
   @implements IAsyncDisposable

   <div class="agent-terminal-container">
       <!-- Terminal Header -->
       <div class="terminal-header">
           <div class="terminal-info">
               <span class="agent-label">Agent:</span>
               <span class="agent-id">@AgentId</span>
               <span class="connection-status @GetStatusClass()">
                   <i class="bi @GetStatusIcon()"></i>
                   @ConnectionStatus
               </span>
           </div>
           <div class="terminal-actions">
               <button class="btn btn-sm btn-outline-secondary"
                       @onclick="ClearOutput"
                       disabled="@(!IsConnected)">
                   <i class="bi bi-trash"></i> Clear
               </button>
               <button class="btn btn-sm btn-outline-secondary"
                       @onclick="ToggleAutoScroll">
                   <i class="bi @(AutoScroll ? "bi-arrow-down-square-fill" : "bi-arrow-down-square")"></i>
                   Auto-scroll
               </button>
               <button class="btn btn-sm btn-danger"
                       @onclick="DisconnectAsync"
                       disabled="@(!IsConnected)">
                   <i class="bi bi-x-circle"></i> Disconnect
               </button>
           </div>
       </div>

       <!-- Terminal Output -->
       <div class="terminal-output" @ref="_outputElement">
           @if (!OutputLines.Any())
           {
               <div class="terminal-welcome">
                   <p>Terminal ready. Click "Connect" to start.</p>
               </div>
           }
           else
           {
               @foreach (var line in OutputLines)
               {
                   <div class="terminal-line @GetLineClass(line)">
                       <span class="line-timestamp">[@line.Timestamp:HH:mm:ss]</span>
                       <span class="line-content">@line.Content</span>
                   </div>
               }
           }
       </div>

       <!-- Terminal Input -->
       <div class="terminal-input-container">
           <div class="input-group">
               <span class="input-prompt">$</span>
               <input type="text"
                      class="terminal-input"
                      @bind="_commandInput"
                      @bind:event="oninput"
                      @onkeydown="HandleKeyDown"
                      disabled="@(!IsConnected)"
                      placeholder="@(IsConnected ? "Enter command..." : "Not connected")" />
               <button class="btn btn-primary"
                       @onclick="SendCommandAsync"
                       disabled="@(!IsConnected || string.IsNullOrWhiteSpace(_commandInput))">
                   <i class="bi bi-send"></i> Send
               </button>
           </div>
       </div>

       <!-- Connection Dialog -->
       @if (ShowConnectionDialog)
       {
           <div class="connection-dialog-overlay">
               <div class="connection-dialog">
                   <h5>Connect to Agent</h5>
                   <div class="form-group">
                       <label>Agent ID:</label>
                       <input type="text" class="form-control" @bind="_connectAgentId" />
                   </div>
                   <div class="form-group">
                       <label>Connector Type:</label>
                       <select class="form-control" @bind="_connectType">
                           <option value="terminal">Terminal (Claude Code)</option>
                           <option value="tab">Tab-based (Cursor)</option>
                       </select>
                   </div>
                   <div class="dialog-actions">
                       <button class="btn btn-primary" @onclick="ConnectAsync">Connect</button>
                       <button class="btn btn-secondary" @onclick="CloseConnectionDialog">Cancel</button>
                   </div>
               </div>
           </div>
       }
   </div>
   ```

3. **Create code-behind file**:
   ```csharp
   // File: src/Orchestra.Web/Components/AgentTerminal/AgentTerminalComponent.razor.cs
   using Microsoft.AspNetCore.Components;
   using Microsoft.AspNetCore.SignalR.Client;
   using Microsoft.JSInterop;

   namespace Orchestra.Web.Components.AgentTerminal
   {
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
               // TODO: Initialize component
           }

           public async ValueTask DisposeAsync()
           {
               // TODO: Cleanup resources
           }
       }

       public record OutputLine(string Content, DateTime Timestamp, OutputLineType Type = OutputLineType.Standard);

       public enum OutputLineType
       {
           Standard,
           Command,
           Error,
           System,
           KeepAlive
       }
   }
   ```

**Acceptance Criteria**:
- [ ] Component structure created
- [ ] Markup defines terminal UI
- [ ] Code-behind has necessary properties
- [ ] Styling isolated with CSS

### Task 3.1B: Implement Terminal Styling (1-2 hours) - DECOMPOSED

**⚠️ This task has been decomposed**: See [task-3.1b-terminal-styling.md](./phase-3-frontend-component/task-3.1b-terminal-styling.md) for detailed subtasks

**Technical Steps**:

1. **Create component CSS**:
   ```css
   /* File: src/Orchestra.Web/Components/AgentTerminal/AgentTerminalComponent.razor.css */
   .agent-terminal-container {
       display: flex;
       flex-direction: column;
       height: 600px;
       border: 1px solid var(--bs-border-color);
       border-radius: 8px;
       background-color: #1e1e1e;
       font-family: 'Cascadia Code', 'Courier New', monospace;
   }

   .terminal-header {
       display: flex;
       justify-content: space-between;
       align-items: center;
       padding: 10px 15px;
       background-color: #2d2d2d;
       border-bottom: 1px solid #444;
       border-radius: 8px 8px 0 0;
   }

   .terminal-info {
       display: flex;
       align-items: center;
       gap: 10px;
       color: #e0e0e0;
   }

   .agent-label {
       color: #888;
       font-size: 0.9em;
   }

   .agent-id {
       font-weight: 600;
       color: #4fc3f7;
   }

   .connection-status {
       padding: 2px 8px;
       border-radius: 12px;
       font-size: 0.85em;
       display: flex;
       align-items: center;
       gap: 4px;
   }

   .connection-status.connected {
       background-color: rgba(76, 175, 80, 0.2);
       color: #4caf50;
   }

   .connection-status.connecting {
       background-color: rgba(255, 193, 7, 0.2);
       color: #ffc107;
   }

   .connection-status.disconnected {
       background-color: rgba(244, 67, 54, 0.2);
       color: #f44336;
   }

   .terminal-output {
       flex: 1;
       overflow-y: auto;
       padding: 10px 15px;
       background-color: #0c0c0c;
       color: #d4d4d4;
       font-size: 14px;
       line-height: 1.6;
   }

   .terminal-line {
       display: flex;
       gap: 10px;
       padding: 2px 0;
       word-break: break-all;
   }

   .terminal-line:hover {
       background-color: rgba(255, 255, 255, 0.05);
   }

   .line-timestamp {
       color: #666;
       font-size: 0.85em;
       flex-shrink: 0;
   }

   .line-content {
       flex: 1;
   }

   .terminal-line.command {
       color: #4fc3f7;
       font-weight: 600;
   }

   .terminal-line.error {
       color: #f44336;
   }

   .terminal-line.system {
       color: #ffc107;
       font-style: italic;
   }

   .terminal-input-container {
       padding: 10px 15px;
       background-color: #2d2d2d;
       border-top: 1px solid #444;
       border-radius: 0 0 8px 8px;
   }

   .input-group {
       display: flex;
       align-items: center;
       gap: 8px;
   }

   .input-prompt {
       color: #4caf50;
       font-weight: 600;
   }

   .terminal-input {
       flex: 1;
       background-color: #1e1e1e;
       border: 1px solid #444;
       color: #e0e0e0;
       padding: 6px 10px;
       border-radius: 4px;
       font-family: inherit;
   }

   .terminal-input:focus {
       outline: none;
       border-color: #4fc3f7;
       box-shadow: 0 0 0 2px rgba(79, 195, 247, 0.2);
   }

   .terminal-input:disabled {
       background-color: #0c0c0c;
       color: #666;
       cursor: not-allowed;
   }

   /* Scrollbar styling */
   .terminal-output::-webkit-scrollbar {
       width: 8px;
   }

   .terminal-output::-webkit-scrollbar-track {
       background: #1e1e1e;
   }

   .terminal-output::-webkit-scrollbar-thumb {
       background: #444;
       border-radius: 4px;
   }

   .terminal-output::-webkit-scrollbar-thumb:hover {
       background: #555;
   }

   /* Connection dialog */
   .connection-dialog-overlay {
       position: fixed;
       top: 0;
       left: 0;
       right: 0;
       bottom: 0;
       background-color: rgba(0, 0, 0, 0.5);
       display: flex;
       align-items: center;
       justify-content: center;
       z-index: 1000;
   }

   .connection-dialog {
       background-color: white;
       padding: 20px;
       border-radius: 8px;
       min-width: 400px;
       box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
   }
   ```

2. **Add dark/light theme support**:
   ```css
   /* Light theme overrides */
   [data-theme="light"] .agent-terminal-container {
       background-color: #ffffff;
       border-color: #dee2e6;
   }

   [data-theme="light"] .terminal-header {
       background-color: #f8f9fa;
       border-bottom-color: #dee2e6;
   }

   [data-theme="light"] .terminal-output {
       background-color: #ffffff;
       color: #212529;
   }

   [data-theme="light"] .terminal-input {
       background-color: #ffffff;
       border-color: #ced4da;
       color: #212529;
   }
   ```

**Acceptance Criteria**:
- [ ] Terminal has dark theme by default
- [ ] Responsive layout works
- [ ] Scrollbar styled appropriately
- [ ] Light theme supported

### Task 3.1C: Implement Command History Navigation (1 hour)

**Technical Steps**:

1. **Add command history logic**:
   ```csharp
   private void HandleKeyDown(KeyboardEventArgs e)
   {
       switch (e.Key)
       {
           case "Enter":
               if (!string.IsNullOrWhiteSpace(_commandInput))
               {
                   _ = SendCommandAsync();
               }
               break;

           case "ArrowUp":
               NavigateHistory(-1);
               break;

           case "ArrowDown":
               NavigateHistory(1);
               break;

           case "Escape":
               ClearInput();
               break;

           case "Tab":
               // TODO: Future - implement autocomplete
               break;
       }
   }

   private void NavigateHistory(int direction)
   {
       if (_commandHistory.Count == 0) return;

       _historyIndex += direction;
       _historyIndex = Math.Max(0, Math.Min(_commandHistory.Count - 1, _historyIndex));

       _commandInput = _commandHistory[_historyIndex];
       StateHasChanged();
   }

   private void AddToHistory(string command)
   {
       // Remove duplicates
       _commandHistory.Remove(command);

       // Add to end
       _commandHistory.Add(command);

       // Limit history size
       if (_commandHistory.Count > 100)
       {
           _commandHistory.RemoveAt(0);
       }

       // Reset index
       _historyIndex = _commandHistory.Count;
   }

   private void ClearInput()
   {
       _commandInput = string.Empty;
       _historyIndex = _commandHistory.Count;
       StateHasChanged();
   }
   ```

2. **Persist history in local storage**:
   ```javascript
   // Add to wwwroot/js/terminal.js
   window.terminalFunctions = {
       saveHistory: (history) => {
           localStorage.setItem('terminal-history', JSON.stringify(history));
       },

       loadHistory: () => {
           const history = localStorage.getItem('terminal-history');
           return history ? JSON.parse(history) : [];
       },

       scrollToBottom: (element) => {
           element.scrollTop = element.scrollHeight;
       }
   };
   ```

**Acceptance Criteria**:
- [ ] Arrow keys navigate history
- [ ] History persisted in localStorage
- [ ] Duplicate commands not added
- [ ] History limited to 100 items

---

## Phase 3.2: SignalR Client Integration

**Estimate**: 3-4 hours

**Goal**: Connect component to SignalR hub for real-time communication

### Task 3.2A: Implement Hub Connection Management (2 hours)

**Technical Steps**:

1. **Create hub connection**:
   ```csharp
   private async Task InitializeHubConnection()
   {
       _hubConnection = new HubConnectionBuilder()
           .WithUrl(Navigation.ToAbsoluteUri("/hubs/agent-interaction"))
           .WithAutomaticReconnect()
           .Build();

       // TODO: Configure reconnection
       _hubConnection.Reconnecting += OnReconnecting;
       _hubConnection.Reconnected += OnReconnected;
       _hubConnection.Closed += OnClosed;

       // TODO: Register event handlers
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

       // TODO: Re-establish session if needed
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
   ```

2. **Implement connection to agent**:
   ```csharp
   public async Task ConnectAsync()
   {
       try
       {
           AddSystemLine($"Connecting to agent {_connectAgentId}...");

           if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
           {
               await InitializeHubConnection();
           }

           var request = new ConnectToAgentRequest(
               _connectAgentId,
               _connectType,
               new Dictionary<string, string>());

           var response = await _hubConnection.InvokeAsync<ConnectToAgentResponse>(
               "ConnectToAgent",
               request);

           if (response.Success)
           {
               _sessionId = response.SessionId;
               AgentId = _connectAgentId;
               AddSystemLine($"Connected to agent. Session: {_sessionId}");

               // Start streaming output
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
   ```

3. **Implement disconnection**:
   ```csharp
   public async Task DisconnectAsync()
   {
       if (_hubConnection != null && !string.IsNullOrEmpty(_sessionId))
       {
           try
           {
               AddSystemLine("Disconnecting...");

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
   ```

**Acceptance Criteria**:
- [ ] Hub connection established
- [ ] Auto-reconnect configured
- [ ] Connection state tracked
- [ ] Error handling implemented

### Task 3.2B: Implement Output Streaming (1-2 hours) - DECOMPOSED

**⚠️ This task has been decomposed**: See [task-3.2b-output-streaming.md](./phase-3-frontend-component/task-3.2b-output-streaming.md) for detailed subtasks

**Technical Steps**:

1. **Implement streaming consumer**:
   ```csharp
   private async Task StreamOutputAsync()
   {
       if (_hubConnection == null || string.IsNullOrEmpty(_sessionId))
           return;

       try
       {
           var cancellationToken = _streamCancellation.Token;

           await foreach (var line in _hubConnection.StreamAsync<string>(
               "StreamOutput",
               _sessionId,
               null,
               cancellationToken))
           {
               // Filter keep-alive messages
               if (line == "[KEEPALIVE]")
                   continue;

               AddOutputLine(line);

               // Auto-scroll if enabled
               if (AutoScroll)
               {
                   await ScrollToBottomAsync();
               }

               await InvokeAsync(StateHasChanged);
           }
       }
       catch (OperationCanceledException)
       {
           // Normal cancellation
       }
       catch (Exception ex)
       {
           AddErrorLine($"Streaming error: {ex.Message}");
       }
   }

   private void AddOutputLine(string content, OutputLineType type = OutputLineType.Standard)
   {
       var line = new OutputLine(content, DateTime.Now, type);
       OutputLines.Add(line);

       // Limit output buffer
       if (OutputLines.Count > 1000)
       {
           OutputLines.RemoveRange(0, OutputLines.Count - 1000);
       }
   }

   private void AddSystemLine(string message)
   {
       AddOutputLine(message, OutputLineType.System);
   }

   private void AddErrorLine(string message)
   {
       AddOutputLine(message, OutputLineType.Error);
   }

   private void AddCommandLine(string command)
   {
       AddOutputLine($"$ {command}", OutputLineType.Command);
   }
   ```

2. **Implement command sending**:
   ```csharp
   private async Task SendCommandAsync()
   {
       if (!IsConnected || string.IsNullOrWhiteSpace(_commandInput))
           return;

       var command = _commandInput.Trim();

       try
       {
           // Add to display
           AddCommandLine(command);

           // Add to history
           AddToHistory(command);

           // Send command
           var request = new SendCommandRequest(_sessionId!, command);
           await _hubConnection!.InvokeAsync("SendCommand", request);

           // Clear input
           ClearInput();

           // Notify parent
           if (OnCommandExecuted.HasDelegate)
           {
               await OnCommandExecuted.InvokeAsync(command);
           }
       }
       catch (Exception ex)
       {
           AddErrorLine($"Command error: {ex.Message}");
       }

       await InvokeAsync(StateHasChanged);
   }
   ```

3. **Handle SignalR events**:
   ```csharp
   private async Task OnCommandSent(CommandSentNotification notification)
   {
       if (notification.SessionId == _sessionId && !notification.Success)
       {
           AddErrorLine("Command execution failed");
       }
       await InvokeAsync(StateHasChanged);
   }

   private async Task OnSessionClosed(string sessionId)
   {
       if (sessionId == _sessionId)
       {
           AddSystemLine("Session closed by server");
           _sessionId = null;
       }
       await InvokeAsync(StateHasChanged);
   }

   private async Task OnCommandError(string error)
   {
       AddErrorLine($"Command error: {error}");
       await InvokeAsync(StateHasChanged);
   }
   ```

**Acceptance Criteria**:
- [ ] Output streams in real-time
- [ ] Commands sent successfully
- [ ] Events handled properly
- [ ] Buffer limited to prevent memory issues

---

## Phase 3.3: UI Polish & Styling

**Estimate**: 3-4 hours

**Goal**: Add UI enhancements and polish

### Task 3.3A: Implement Auto-Scroll and Clear Functions (1 hour)

**Technical Steps**:

1. **Implement auto-scroll**:
   ```csharp
   private async Task ScrollToBottomAsync()
   {
       await JSRuntime.InvokeVoidAsync("terminalFunctions.scrollToBottom", _outputElement);
   }

   private void ToggleAutoScroll()
   {
       AutoScroll = !AutoScroll;
       if (AutoScroll)
       {
           _ = ScrollToBottomAsync();
       }
   }
   ```

2. **Implement clear output**:
   ```csharp
   private void ClearOutput()
   {
       OutputLines.Clear();
       AddSystemLine("Output cleared");
       StateHasChanged();
   }
   ```

3. **Add copy functionality**:
   ```javascript
   // Add to terminal.js
   copyToClipboard: (text) => {
       navigator.clipboard.writeText(text).then(() => {
           console.log('Copied to clipboard');
       });
   },

   selectLine: (element) => {
       const selection = window.getSelection();
       const range = document.createRange();
       range.selectNodeContents(element);
       selection.removeAllRanges();
       selection.addRange(range);
   }
   ```

**Acceptance Criteria**:
- [ ] Auto-scroll works when enabled
- [ ] Clear function removes output
- [ ] Copy functionality available
- [ ] Smooth scrolling behavior

### Task 3.3B: Add Loading and Status Indicators (1-2 hours)

**Technical Steps**:

1. **Add loading spinner**:
   ```razor
   @if (IsConnecting)
   {
       <div class="spinner-overlay">
           <div class="spinner-border text-primary" role="status">
               <span class="visually-hidden">Connecting...</span>
           </div>
       </div>
   }
   ```

2. **Implement connection status helpers**:
   ```csharp
   private string GetConnectionStatus()
   {
       if (_hubConnection == null)
           return "Disconnected";

       return _hubConnection.State switch
       {
           HubConnectionState.Connected when !string.IsNullOrEmpty(_sessionId) => "Connected",
           HubConnectionState.Connected => "Ready",
           HubConnectionState.Connecting => "Connecting",
           HubConnectionState.Reconnecting => "Reconnecting",
           _ => "Disconnected"
       };
   }

   private string GetStatusClass()
   {
       return ConnectionStatus.ToLower() switch
       {
           "connected" => "connected",
           "connecting" or "reconnecting" => "connecting",
           _ => "disconnected"
       };
   }

   private string GetStatusIcon()
   {
       return ConnectionStatus.ToLower() switch
       {
           "connected" => "bi-check-circle-fill",
           "connecting" or "reconnecting" => "bi-arrow-repeat",
           _ => "bi-x-circle"
       };
   }
   ```

3. **Add output filtering UI**:
   ```razor
   <div class="terminal-filter">
       <input type="text"
              class="form-control form-control-sm"
              placeholder="Filter output (regex)..."
              @bind="_filterPattern"
              @bind:event="oninput"
              @onkeydown="ApplyFilter" />
   </div>
   ```

**Acceptance Criteria**:
- [ ] Loading states visible
- [ ] Connection status accurate
- [ ] Icons represent states
- [ ] Filter UI functional

### Task 3.3C: Add Responsive Design and Accessibility (1 hour)

**Technical Steps**:

1. **Add responsive breakpoints**:
   ```css
   @media (max-width: 768px) {
       .agent-terminal-container {
           height: 400px;
       }

       .terminal-header {
           flex-direction: column;
           gap: 10px;
       }

       .terminal-actions {
           width: 100%;
           display: flex;
           justify-content: space-around;
       }

       .terminal-line {
           font-size: 12px;
       }
   }

   @media (max-width: 480px) {
       .line-timestamp {
           display: none;
       }

       .terminal-actions .btn-text {
           display: none;
       }
   }
   ```

2. **Add keyboard accessibility**:
   ```csharp
   protected override async Task OnAfterRenderAsync(bool firstRender)
   {
       if (firstRender)
       {
           // Focus input on load
           await JSRuntime.InvokeVoidAsync("document.querySelector", ".terminal-input")
               .ContinueWith(_ => _.Result?.FocusAsync());
       }
   }

   // Add keyboard shortcuts
   private async Task HandleGlobalKeyDown(KeyboardEventArgs e)
   {
       if (e.CtrlKey)
       {
           switch (e.Key)
           {
               case "l": // Ctrl+L to clear
                   ClearOutput();
                   break;
               case "c": // Ctrl+C to copy selection
                   await CopySelection();
                   break;
           }
       }
   }
   ```

3. **Add ARIA labels**:
   ```razor
   <div class="terminal-output"
        role="log"
        aria-label="Terminal output"
        aria-live="polite"
        tabindex="0">
   ```

**Acceptance Criteria**:
- [ ] Responsive on mobile devices
- [ ] Keyboard shortcuts work
- [ ] ARIA labels present
- [ ] Focus management correct

---

## Integration Checklist

### Component Registration
```csharp
// Add to _Imports.razor
@using Orchestra.Web.Components.AgentTerminal

// Use in pages
<AgentTerminalComponent AgentId="claude-code-1"
                        ConnectorType="terminal"
                        OnCommandExecuted="HandleCommand" />
```

### JavaScript Interop
```html
<!-- Add to index.html or _Host.cshtml -->
<script src="js/terminal.js"></script>
```

### SignalR Client Package
```xml
<!-- Add to Orchestra.Web.csproj -->
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="9.0.0" />
```

---

## Validation Criteria for Phase 3

### Functional Requirements
- [ ] Component connects to SignalR hub
- [ ] Real-time output displayed
- [ ] Commands sent successfully
- [ ] History navigation works
- [ ] Auto-scroll functions properly

### UI/UX Requirements
- [ ] Dark theme implemented
- [ ] Responsive on all devices
- [ ] Loading states visible
- [ ] Error messages clear

### Performance Requirements
- [ ] Output renders smoothly
- [ ] No UI freezing with high volume
- [ ] Memory usage stable
- [ ] Reconnection works seamlessly

---

**Next Phase**: [Phase-4-Testing-Documentation.md](./Phase-4-Testing-Documentation.md)