# Claude Code Integration - Completion Micro-Plan

**Created**: 2025-10-04
**Purpose**: Complete the remaining 30-40% of Claude Code integration
**Time Estimate**: 8 hours total
**Priority**: HIGH - Core functionality blocker

---

## Current State (60-70% Complete)

### ✅ What's Already Implemented
- **ClaudeCodeExecutor.cs** (469 lines) - Base implementation complete
- **ClaudeCodeConfiguration.cs** - Fully implemented
- **ClaudeCodeService.cs** - Service layer with interfaces
- **Test structure** - Tests exist but some hanging
- **Basic integration** - Partial TaskExecutionJob connection

### 🔄 What Actually Remains (30-40%)

---

## Completion Tasks

### Task 1: Fix Hanging Integration Tests
**Time**: 1 hour
**Priority**: CRITICAL - Blocks all other work

**Actions**:
```bash
# 1. Identify hanging tests
dotnet test src/Orchestra.Tests/ --filter "FullyQualifiedName~ClaudeCode" --logger "console;verbosity=detailed"

# 2. Add timeout attributes to async tests
[Timeout(30000)] // 30 seconds

# 3. Fix async/await issues in test methods
```

**Files to modify**:
- `src/Orchestra.Tests/Integration/HangfireCoordinationE2ETests.cs`
- `src/Orchestra.Tests/RealEndToEndTests.cs`

**Success Criteria**: All tests pass or skip gracefully

---

### Task 2: Complete ClaudeCodeExecutor Output Parsing
**Time**: 2 hours
**Priority**: HIGH

**Actions**:
```csharp
// In ClaudeCodeExecutor.cs, complete the ParseClaudeOutput method:
private ClaudeCodeExecutionResult ParseClaudeOutput(string output)
{
    // 1. Parse JSON responses from Claude CLI
    // 2. Extract execution steps
    // 3. Map to AgentExecutionResponse format
    // 4. Handle multi-line outputs
}
```

**Implementation checklist**:
- [ ] JSON parsing for structured responses
- [ ] Plain text fallback for unstructured output
- [ ] Error message extraction
- [ ] Execution time tracking
- [ ] Step-by-step progress parsing

**Files to modify**:
- `src/Orchestra.Agents/ClaudeCode/ClaudeCodeExecutor.cs` (lines 300-400)

---

### Task 3: Wire TaskExecutionJob Integration
**Time**: 2 hours
**Priority**: HIGH

**Actions**:
```csharp
// In TaskExecutionJob.cs, add ClaudeCode support:
if (agent.Type == "ClaudeCode")
{
    var executor = _serviceProvider.GetService<ClaudeCodeExecutor>();
    var result = await executor.ExecuteCommandAsync(
        task.Command,
        task.WorkingDirectory,
        cancellationToken);

    // Map result back to task status
    await UpdateTaskStatusAsync(taskId, result);
}
```

**Integration points**:
- [ ] Dependency injection registration in Startup.cs
- [ ] Agent type detection logic
- [ ] Result mapping to TaskResult
- [ ] Status updates via SignalR
- [ ] Error handling and retry logic

**Files to modify**:
- `src/Orchestra.API/Jobs/TaskExecutionJob.cs`
- `src/Orchestra.API/Startup.cs`

---

### Task 4: Implement Error Handling & Retry Logic
**Time**: 2 hours
**Priority**: MEDIUM

**Actions**:
```csharp
// Add Polly retry policy
services.AddSingleton<IAsyncPolicy>(Policy
    .Handle<ProcessException>()
    .WaitAndRetryAsync(
        3,
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            logger.LogWarning($"Retry {retryCount} after {timespan}");
        }));
```

**Error scenarios to handle**:
- [ ] Claude CLI not found
- [ ] Process timeout
- [ ] Invalid command syntax
- [ ] Network failures (for HTTP API mode)
- [ ] Insufficient permissions

**Files to modify**:
- `src/Orchestra.Agents/ClaudeCode/ClaudeCodeExecutor.cs`
- `src/Orchestra.Agents/ClaudeCode/ClaudeCodeService.cs`

---

### Task 5: Resolve Technical Debt from Phase 1
**Time**: 2 hours
**Priority**: MEDIUM

**From TECHNICAL_DEBT_PHASE1.md**:

1. **Create IChatContextService**:
```csharp
public interface IChatContextService
{
    Task<ChatSession> GetOrCreateSessionAsync(string userId);
    Task<ChatMessage> AddMessageAsync(Guid sessionId, ChatMessage message);
    Task<List<ChatMessage>> GetRecentMessagesAsync(Guid sessionId, int count = 50);
}
```

2. **Add comprehensive logging**:
```csharp
_logger.LogInformation("Executing Claude command: {Command}", command);
_logger.LogDebug("Command parameters: {@Parameters}", parameters);
_logger.LogError(ex, "Failed to execute command");
```

3. **Add error handling to all DB operations**

**Files to create/modify**:
- `src/Orchestra.Core/Services/IChatContextService.cs` (create)
- `src/Orchestra.Core/Services/ChatContextService.cs` (create)
- All ClaudeCode files (add logging)

---

### Task 6: End-to-End Testing with Real CLI
**Time**: 1 hour
**Priority**: LOW (can use mocks initially)

**Test scenarios**:
```bash
# 1. Test basic command execution
claude "List files in current directory"

# 2. Test with specific tools
claude --tool Bash "echo 'Hello from Claude'"

# 3. Test timeout handling
claude --timeout 30 "Perform a complex task"
```

**Create test file**:
- `src/Orchestra.Tests/Integration/ClaudeCodeE2ETests.cs`

**Test cases**:
- [ ] Simple command execution
- [ ] Multi-step workflow
- [ ] Error handling
- [ ] Timeout scenarios
- [ ] Concurrent executions

---

## Completion Verification Checklist

### Functional Requirements
- [ ] Claude Code commands execute successfully via API
- [ ] Results display in UI chat interface
- [ ] Status updates work through SignalR
- [ ] Error messages are user-friendly
- [ ] Retry logic prevents transient failures

### Technical Requirements
- [ ] All tests pass (no hanging)
- [ ] Code coverage ≥ 80% for new code
- [ ] No compiler warnings
- [ ] Logging implemented throughout
- [ ] Configuration externalized

### Integration Requirements
- [ ] TaskExecutionJob triggers ClaudeCodeExecutor
- [ ] Hangfire processes ClaudeCode tasks
- [ ] SignalR updates work end-to-end
- [ ] Database stores chat context
- [ ] UI reflects real-time status

---

## Quick Start Commands

```bash
# 1. Run API with ClaudeCode enabled
cd src/Orchestra.API
dotnet run --launch-profile http

# 2. Test ClaudeCode endpoint
curl -X POST https://localhost:5002/api/tasks/claude \
  -H "Content-Type: application/json" \
  -d '{"command": "List files", "agentId": "claude-001"}'

# 3. Run integration tests
dotnet test src/Orchestra.Tests/ --filter "ClaudeCode"

# 4. Check logs for execution details
tail -f logs/orchestra-api.log | grep Claude
```

---

## Success Metrics

### Immediate Success (Today)
- [ ] At least one ClaudeCode command executes end-to-end
- [ ] No hanging tests
- [ ] Basic error handling works

### Complete Success (8 hours)
- [ ] All 6 tasks complete
- [ ] 95%+ test coverage on new code
- [ ] Production-ready error handling
- [ ] Full integration with UI

### Bonus Goals (If Time Permits)
- [ ] Performance optimization (caching)
- [ ] Advanced retry strategies
- [ ] Metrics and monitoring
- [ ] Documentation updates

---

## Risk Mitigation

### Risk: Claude CLI unavailable
**Mitigation**: Use mock mode with predefined responses

### Risk: Tests continue hanging
**Mitigation**: Skip integration tests temporarily, use unit tests only

### Risk: SignalR integration complex
**Mitigation**: Start with simple polling, add real-time later

### Risk: Database migrations fail
**Mitigation**: Use in-memory database for initial testing

---

## Next Steps After Completion

1. Update MASTER-ROADMAP.md to show Claude Code as COMPLETE
2. Remove outdated plan files
3. Document the actual architecture in README
4. Plan next integration (GitHub Copilot or other agents)

---

## Notes

- Focus on WORKING CODE over perfect documentation
- Use existing patterns from ClaudeAgentExecutor as reference
- Don't create new plan files - just complete the code
- Test with real Claude CLI if available, mocks if not

**Remember**: We're 60-70% done. Don't overthink - just finish it!