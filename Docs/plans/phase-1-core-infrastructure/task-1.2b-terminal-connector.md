# Task 1.2B: Cross-Platform Socket Connection - Decomposed

**Parent Phase**: [phase-1-core-infrastructure.md](../phase-1-core-infrastructure.md)

**Original Task**: 1.2B - Implement Cross-Platform Socket Connection (~40 tool calls)

**Goal**: Implement cross-platform socket connection with proper decomposition

**Total Estimate**: 3-4 hours

---

## Subtask 1.2B.1: Windows Named Pipes Implementation

**Estimate**: 1-1.5 hours

**Goal**: Implement Windows named pipe connection for legacy support

### Technical Steps:

1. **Create Windows-specific connection method**:
   ```csharp
   private async Task<Stream> ConnectWindowsNamedPipeAsync(
       string pipeName,
       CancellationToken ct)
   {
       // Step 1: Create NamedPipeClientStream
       var pipeClient = new NamedPipeClientStream(
           ".", // local machine
           pipeName,
           PipeDirection.InOut,
           PipeOptions.Asynchronous);

       // Step 2: Connect with timeout handling
       await pipeClient.ConnectAsync(30000, ct); // 30 second timeout

       // Step 3: Return connected stream
       return pipeClient;
   }
   ```

2. **Add Windows pipe name validation**:
   ```csharp
   private bool IsValidWindowsPipeName(string pipeName)
   {
       // Check for valid pipe name format
       return !string.IsNullOrWhiteSpace(pipeName)
           && !pipeName.Contains("\\")
           && pipeName.Length < 256;
   }
   ```

3. **DI Registration for Windows**:
   ```csharp
   // In Program.cs or DI configuration
   if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
   {
       services.Configure<TerminalConnectorOptions>(options =>
       {
           options.UseNamedPipes = true;
           options.DefaultPipeName = "orchestra_agent_pipe";
       });
   }
   ```

**Acceptance Criteria**:
- [ ] Named pipe client connects successfully on Windows
- [ ] Connection timeout handled (30 seconds)
- [ ] Pipe name validation works
- [ ] Stream returned for further use

---

## Subtask 1.2B.2: Unix Domain Sockets Implementation

**Estimate**: 1-1.5 hours

**Goal**: Implement Unix Domain Socket connection for Linux/macOS

### Technical Steps:

1. **Create Unix socket connection method**:
   ```csharp
   private async Task<Socket> ConnectUnixSocketAsync(
       string socketPath,
       CancellationToken ct)
   {
       // Step 1: Create Unix socket
       var socket = new Socket(
           AddressFamily.Unix,
           SocketType.Stream,
           ProtocolType.Unspecified);

       // Step 2: Create endpoint
       var endpoint = new UnixDomainSocketEndPoint(socketPath);

       // Step 3: Connect asynchronously
       await socket.ConnectAsync(endpoint, ct);

       // Step 4: Configure socket options
       socket.NoDelay = true;
       socket.ReceiveTimeout = 30000;
       socket.SendTimeout = 30000;

       return socket;
   }
   ```

2. **Add Unix socket path validation**:
   ```csharp
   private bool IsValidUnixSocketPath(string socketPath)
   {
       // Check if socket file exists and is accessible
       return File.Exists(socketPath)
           || Directory.Exists(Path.GetDirectoryName(socketPath));
   }
   ```

3. **DI Registration for Unix platforms**:
   ```csharp
   // In Program.cs or DI configuration
   if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
   {
       services.Configure<TerminalConnectorOptions>(options =>
       {
           options.UseUnixSockets = true;
           options.DefaultSocketPath = "/tmp/orchestra_agent.sock";
       });
   }
   ```

**Acceptance Criteria**:
- [ ] Unix socket connects on Linux/macOS
- [ ] Socket options configured correctly
- [ ] Path validation works
- [ ] Socket returned for stream creation

---

## Subtask 1.2B.3: Platform Detection and Integration

**Estimate**: 1 hour

**Goal**: Integrate both implementations with platform detection

### Technical Steps:

1. **Create platform detection helper**:
   ```csharp
   private static class PlatformHelper
   {
       public static bool IsWindows =>
           RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

       public static bool IsLinux =>
           RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

       public static bool IsMacOS =>
           RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

       public static bool SupportsUnixSockets =>
           IsLinux || IsMacOS ||
           (IsWindows && Environment.OSVersion.Version.Major >= 10);
   }
   ```

2. **Implement unified ConnectAsync with platform routing**:
   ```csharp
   public async Task<ConnectionResult> ConnectAsync(
       string agentId,
       AgentConnectionParams connectionParams,
       CancellationToken cancellationToken)
   {
       try
       {
           // Step 1: Validate parameters
           if (string.IsNullOrEmpty(agentId))
               return ConnectionResult.Failed("Agent ID is required");

           // Step 2: Set connecting status
           UpdateStatus(ConnectionStatus.Connecting);

           // Step 3: Platform-specific connection
           Stream connectionStream;

           if (PlatformHelper.SupportsUnixSockets &&
               !string.IsNullOrEmpty(connectionParams.SocketPath))
           {
               // Use Unix sockets (preferred)
               var socket = await ConnectUnixSocketAsync(
                   connectionParams.SocketPath,
                   cancellationToken);
               connectionStream = new NetworkStream(socket, ownsSocket: true);
           }
           else if (PlatformHelper.IsWindows &&
                    !string.IsNullOrEmpty(connectionParams.PipeName))
           {
               // Fall back to named pipes on Windows
               connectionStream = await ConnectWindowsNamedPipeAsync(
                   connectionParams.PipeName,
                   cancellationToken);
           }
           else
           {
               return ConnectionResult.Failed(
                   "No valid connection method available for platform");
           }

           // Step 4: Store connection
           _connectionStream = connectionStream;
           _agentId = agentId;

           // Step 5: Start output reader
           _outputReaderTask = Task.Run(
               () => ReadOutputLoopAsync(connectionStream, cancellationToken),
               cancellationToken);

           // Step 6: Update status
           UpdateStatus(ConnectionStatus.Connected);

           return ConnectionResult.Success();
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Failed to connect to agent {AgentId}", agentId);
           UpdateStatus(ConnectionStatus.Disconnected);
           return ConnectionResult.Failed(ex.Message);
       }
   }
   ```

3. **Add connection options configuration**:
   ```csharp
   public class TerminalConnectorOptions
   {
       public bool UseUnixSockets { get; set; }
       public bool UseNamedPipes { get; set; }
       public string? DefaultSocketPath { get; set; }
       public string? DefaultPipeName { get; set; }
       public int ConnectionTimeoutMs { get; set; } = 30000;
   }
   ```

4. **Update DI registration with options**:
   ```csharp
   // In Program.cs
   services.Configure<TerminalConnectorOptions>(
       builder.Configuration.GetSection("TerminalConnector"));
   services.AddTransient<TerminalAgentConnector>();
   ```

**Acceptance Criteria**:
- [ ] Platform detection works correctly
- [ ] Correct connection method chosen per platform
- [ ] Connection stream stored and used
- [ ] Status transitions fire events
- [ ] Error handling covers all scenarios
- [ ] DI registration complete

---

## Integration Testing Requirements

After all subtasks complete:
- [ ] Test on Windows with named pipes
- [ ] Test on Linux with Unix sockets
- [ ] Test on macOS with Unix sockets
- [ ] Test Windows 10+ with Unix socket support
- [ ] Verify cross-platform compatibility