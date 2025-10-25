# Task 3.1B: Terminal Styling Implementation - Decomposed

**Parent Phase**: [phase-3-frontend-component.md](../phase-3-frontend-component.md)

**Original Task**: 3.1B - Implement Terminal Styling (~35 tool calls)

**Goal**: Implement comprehensive terminal styling with proper decomposition

**Total Estimate**: 1-2 hours

---

## Subtask 3.1B.1: Core Terminal Container Styles

**Estimate**: 30 minutes

**Goal**: Create base container and layout styles

### Technical Steps:

1. **Create component CSS file**:
   ```bash
   # Create CSS file for component
   touch src/Orchestra.Web/Components/AgentTerminal/AgentTerminalComponent.razor.css
   ```

2. **Implement base container styles**:
   ```css
   /* Container and layout */
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

   .terminal-output {
       flex: 1;
       overflow-y: auto;
       padding: 10px 15px;
       background-color: #0c0c0c;
       color: #d4d4d4;
       font-size: 14px;
       line-height: 1.6;
   }

   .terminal-input-container {
       padding: 10px 15px;
       background-color: #2d2d2d;
       border-top: 1px solid #444;
       border-radius: 0 0 8px 8px;
   }
   ```

3. **Register styles in component**:
   ```csharp
   // In AgentTerminalComponent.razor
   @page "/terminal"
   @using Microsoft.AspNetCore.Components.Web
   @implements IAsyncDisposable

   <link href="AgentTerminalComponent.razor.css" rel="stylesheet" />
   ```

**Acceptance Criteria**:
- [ ] CSS file created and linked
- [ ] Container layout responsive
- [ ] Dark theme applied by default

---

## Subtask 3.1B.2: Connection Status and Line Styling

**Estimate**: 30 minutes

**Goal**: Style connection indicators and terminal output lines

### Technical Steps:

1. **Connection status indicators**:
   ```css
   /* Connection status styling */
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

   /* Status dot animation */
   @keyframes pulse {
       0% { opacity: 1; }
       50% { opacity: 0.5; }
       100% { opacity: 1; }
   }

   .connection-status.connecting::before {
       content: '•';
       animation: pulse 1.5s infinite;
   }
   ```

2. **Terminal line types styling**:
   ```css
   /* Terminal line styles */
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

   /* Line type specific colors */
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

   .terminal-line.output {
       color: #d4d4d4;
   }
   ```

3. **DI Registration for styles configuration**:
   ```csharp
   // In Program.cs
   builder.Services.Configure<TerminalStyleOptions>(options =>
   {
       options.ShowTimestamps = true;
       options.MaxOutputLines = 1000;
       options.EnableLineHover = true;
   });
   ```

**Acceptance Criteria**:
- [ ] Connection status styled with colors
- [ ] Line types visually differentiated
- [ ] Hover effects working
- [ ] Animation on connecting state

---

## Subtask 3.1B.3: Input Controls and Theme Support

**Estimate**: 30-45 minutes

**Goal**: Style input controls and add theme support

### Technical Steps:

1. **Input field and prompt styling**:
   ```css
   /* Input group styling */
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
   ```

2. **Custom scrollbar styling**:
   ```css
   /* Custom scrollbar */
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
   ```

3. **Light theme support**:
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

   [data-theme="light"] .line-content {
       color: #212529;
   }

   [data-theme="light"] .terminal-line.command {
       color: #0066cc;
   }

   [data-theme="light"] .terminal-line.error {
       color: #dc3545;
   }

   [data-theme="light"] .terminal-line.system {
       color: #fd7e14;
   }
   ```

4. **Theme integration in component**:
   ```csharp
   // In AgentTerminalComponent.razor.cs
   [Parameter] public string Theme { get; set; } = "dark";

   protected override void OnInitialized()
   {
       // Apply theme from user preferences or system setting
       var userTheme = _preferences.GetTheme() ?? "dark";
       Theme = userTheme;
   }
   ```

5. **DI Registration for theme service**:
   ```csharp
   // In Program.cs
   builder.Services.AddScoped<IUserPreferencesService, UserPreferencesService>();
   ```

**Acceptance Criteria**:
- [ ] Input field properly styled
- [ ] Custom scrollbar implemented
- [ ] Light theme fully functional
- [ ] Theme switching works
- [ ] Focus states visible
- [ ] Disabled state styling applied

---

## Integration Testing Requirements

After all subtasks complete:
- [ ] Test in Chrome, Firefox, Edge
- [ ] Verify responsive behavior
- [ ] Test dark/light theme switching
- [ ] Verify accessibility (keyboard navigation)
- [ ] Test with long output (1000+ lines)