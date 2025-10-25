# Task 3.2B: Output Streaming Implementation - Decomposed

**Parent Phase**: [phase-3-frontend-component.md](../phase-3-frontend-component.md)

**Original Task**: 3.2B - Implement Output Streaming (~45 tool calls)

**Goal**: Implement real-time output streaming with proper decomposition

**Total Estimate**: 1-2 hours

---

## Subtask 3.2B.1: Streaming Consumer Implementation

**Estimate**: 30-40 minutes

**Goal**: Implement core streaming consumer for SignalR

### Technical Steps:

1. **Create streaming method**:
   ```csharp
   // In AgentTerminalComponent.razor.cs
   private async Task StreamOutputAsync()
   {
       if (_hubConnection == null || string.IsNullOrEmpty(_sessionId))
           return;

       try
       {
           var cancellationToken = _streamCancellation.Token;

           // Start streaming from hub
           await foreach (var line in _hubConnection.StreamAsync<string>(
               "StreamOutput",
               _sessionId,
               null,
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
           _logger.LogInformation("Output streaming cancelled for session {SessionId}", _sessionId);
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Streaming error for session {SessionId}", _sessionId);
           AddErrorLine($"Streaming error: {ex.Message}");
       }
   }
   ```

2. **Implement line processing**:
   ```csharp
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
   ```

3. **Configure streaming options**:
   ```csharp
   // In Program.cs - DI registration
   builder.Services.Configure<StreamingOptions>(options =>
   {
       options.BufferSize = 1000;
       options.KeepAliveInterval = TimeSpan.FromSeconds(30);
       options.EnableAutoScroll = true;
   });

   // Register the terminal component service
   builder.Services.AddScoped<ITerminalService, TerminalService>();
   ```

**Acceptance Criteria**:
- [ ] Streaming consumer connects to SignalR
- [ ] Keep-alive messages filtered
- [ ] Lines processed and added to buffer
- [ ] Auto-scroll flag handled
- [ ] DI configuration complete

---

## Subtask 3.2B.2: Output Buffer Management

**Estimate**: 20-30 minutes

**Goal**: Implement efficient output buffer with line management

### Technical Steps:

1. **Create output line model and buffer**:
   ```csharp
   // Output line model
   public class OutputLine
   {
       public string Content { get; init; }
       public DateTime Timestamp { get; init; }
       public OutputLineType Type { get; init; }

       public OutputLine(string content, DateTime timestamp, OutputLineType type)
       {
           Content = content;
           Timestamp = timestamp;
           Type = type;
       }
   }

   public enum OutputLineType
   {
       Standard,
       Command,
       Error,
       System
   }
   ```

2. **Implement buffer management methods**:
   ```csharp
   private void AddOutputLine(string content, OutputLineType type = OutputLineType.Standard)
   {
       var line = new OutputLine(content, DateTime.Now, type);
       OutputLines.Add(line);

       // Limit output buffer to prevent memory issues
       if (OutputLines.Count > _maxOutputLines)
       {
           var removeCount = OutputLines.Count - _maxOutputLines;
           OutputLines.RemoveRange(0, removeCount);

           // Log buffer truncation
           _logger.LogDebug("Truncated {Count} lines from output buffer", removeCount);
       }

       // Notify listeners
       OnOutputLineAdded?.Invoke(this, line);
   }

   private void AddSystemLine(string message)
   {
       AddOutputLine($"[SYSTEM] {message}", OutputLineType.System);
   }

   private void AddErrorLine(string message)
   {
       AddOutputLine($"[ERROR] {message}", OutputLineType.Error);
   }

   private void AddCommandLine(string command)
   {
       AddOutputLine($"$ {command}", OutputLineType.Command);
   }
   ```

3. **Implement buffer clearing and filtering**:
   ```csharp
   public void ClearOutput()
   {
       OutputLines.Clear();
       _logger.LogInformation("Output buffer cleared for session {SessionId}", _sessionId);
   }

   public IEnumerable<OutputLine> GetFilteredLines(string filter)
   {
       if (string.IsNullOrWhiteSpace(filter))
           return OutputLines;

       return OutputLines.Where(line =>
           line.Content.Contains(filter, StringComparison.OrdinalIgnoreCase));
   }
   ```

4. **DI Registration for buffer service**:
   ```csharp
   // In Program.cs
   builder.Services.AddSingleton<IOutputBufferFactory, OutputBufferFactory>();
   builder.Services.Configure<OutputBufferOptions>(options =>
   {
       options.MaxLines = 1000;
       options.EnableFiltering = true;
   });
   ```

**Acceptance Criteria**:
- [ ] Output lines properly typed
- [ ] Buffer size limited to prevent memory issues
- [ ] Line types differentiated visually
- [ ] Buffer clearing works
- [ ] Filtering implemented

---

## Subtask 3.2B.3: Command Execution and Event Handling

**Estimate**: 20-30 minutes

**Goal**: Implement command sending and SignalR event handling

### Technical Steps:

1. **Implement command sending**:
   ```csharp
   private async Task SendCommandAsync()
   {
       if (!IsConnected || string.IsNullOrWhiteSpace(_commandInput))
           return;

       var command = _commandInput.Trim();

       try
       {
           // Add command to display
           AddCommandLine(command);

           // Add to command history
           AddToHistory(command);

           // Create and send command request
           var request = new SendCommandRequest
           {
               SessionId = _sessionId!,
               Command = command,
               Timestamp = DateTime.UtcNow
           };

           await _hubConnection!.InvokeAsync("SendCommand", request);

           // Clear input field
           ClearInput();

           // Notify parent component
           if (OnCommandExecuted.HasDelegate)
           {
               await OnCommandExecuted.InvokeAsync(command);
           }

           _logger.LogInformation("Command sent: {Command}", command);
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Failed to send command: {Command}", command);
           AddErrorLine($"Command error: {ex.Message}");
       }

       await InvokeAsync(StateHasChanged);
   }
   ```

2. **Handle SignalR events**:
   ```csharp
   private async Task OnCommandSent(CommandSentNotification notification)
   {
       if (notification.SessionId != _sessionId)
           return;

       if (!notification.Success)
       {
           AddErrorLine($"Command execution failed: {notification.Error}");
       }

       await InvokeAsync(StateHasChanged);
   }

   private async Task OnSessionClosed(string sessionId)
   {
       if (sessionId != _sessionId)
           return;

       AddSystemLine("Session closed by server");
       _sessionId = null;
       _isConnected = false;

       // Cancel streaming
       _streamCancellation?.Cancel();

       await InvokeAsync(StateHasChanged);
   }

   private async Task OnCommandError(CommandErrorNotification error)
   {
       if (error.SessionId != _sessionId)
           return;

       AddErrorLine($"Command error: {error.Message}");
       await InvokeAsync(StateHasChanged);
   }
   ```

3. **Register event handlers during connection**:
   ```csharp
   private void RegisterEventHandlers()
   {
       if (_hubConnection == null)
           return;

       // Command events
       _hubConnection.On<CommandSentNotification>("CommandSent", OnCommandSent);
       _hubConnection.On<CommandErrorNotification>("CommandError", OnCommandError);

       // Session events
       _hubConnection.On<string>("SessionClosed", OnSessionClosed);

       // Connection events
       _hubConnection.Closed += OnConnectionClosed;
       _hubConnection.Reconnecting += OnReconnecting;
       _hubConnection.Reconnected += OnReconnected;
   }
   ```

4. **Implement auto-scroll functionality**:
   ```csharp
   private async Task ScrollToBottomAsync()
   {
       if (_outputElement != null)
       {
           await _jsRuntime.InvokeVoidAsync(
               "scrollToBottom",
               _outputElement);
       }
   }

   // JavaScript interop (in wwwroot/js/terminal.js)
   window.scrollToBottom = (element) => {
       element.scrollTop = element.scrollHeight;
   };
   ```

5. **DI Registration for command service**:
   ```csharp
   // In Program.cs
   builder.Services.AddScoped<ICommandHistoryService, CommandHistoryService>();
   builder.Services.Configure<CommandOptions>(options =>
   {
       options.MaxHistorySize = 100;
       options.EnableAutoComplete = true;
   });
   ```

**Acceptance Criteria**:
- [ ] Commands sent to hub successfully
- [ ] Command history maintained
- [ ] SignalR events handled properly
- [ ] Session closure handled gracefully
- [ ] Auto-scroll works when enabled
- [ ] Error states displayed to user
- [ ] DI services registered

---

## Integration Testing Requirements

After all subtasks complete:
- [ ] Test streaming with high-volume output
- [ ] Verify command execution and response
- [ ] Test connection loss and recovery
- [ ] Verify buffer management (>1000 lines)
- [ ] Test event handling for all scenarios
- [ ] Verify auto-scroll behavior