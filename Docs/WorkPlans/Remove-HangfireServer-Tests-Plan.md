# Work Plan: Remove HangfireServer Dependency from Test Environment

## Executive Summary

**Goal**: Remove HangfireServer from test environment, replace with synchronous job execution to enable parallel test collections and improve test performance.

**Current State**: 582/582 tests passing with Phase 1 solution (sequential execution disabled)

**Target State**: 582/582 tests passing with parallel execution enabled (4-5 min vs current 9-10 min)

**Approach**: Create test-specific IBackgroundJobClient implementation that executes jobs synchronously without HangfireServer

## Problem Analysis

### Root Cause
```
HangfireServer (singleton) → serves → JobStorage (isolated per collection)
Integration Collection → SQLiteStorage-1
RealE2E Collection → SQLiteStorage-2
Result: "Cannot access disposed SQLiteStorage" errors when running in parallel
```

### Current Architecture
1. **HangfireOrchestrator** → uses IBackgroundJobClient → enqueues jobs to Hangfire
2. **HangfireServer** → polls storage → executes TaskExecutionJob
3. **TaskExecutionJob** → uses IAgentExecutor → executes commands
4. **Test collections** → share HangfireServer → conflict on isolated storage

### Why Existing Solutions Don't Apply
- **Hangfire.InMemory**: Still requires HangfireServer, same concurrency issue
- **Mocking IBackgroundJobClient**: Would skip TaskExecutionJob execution entirely
- **Unit test approach**: We need integration tests with real job execution

## Architectural Solution

### Core Strategy: Test-Specific IBackgroundJobClient
Create `TestBackgroundJobClient` that:
1. Implements IBackgroundJobClient interface
2. Executes jobs synchronously in-process
3. Maintains job state for test assertions
4. Bypasses HangfireServer entirely

### Key Design Decisions
1. **Synchronous Execution**: Jobs execute immediately when Enqueue() is called
2. **DI Container Access**: Use IServiceProvider to resolve job instances
3. **PerformContext Simulation**: Create minimal context for job execution
4. **State Tracking**: Maintain in-memory job state for test verification

## Implementation Plan

### Phase 1: Create Test Infrastructure (2-3 hours)

#### 1.1 Create TestBackgroundJobClient
**File**: `src/Orchestra.Tests/Infrastructure/TestBackgroundJobClient.cs`

```csharp
public class TestBackgroundJobClient : IBackgroundJobClient
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TestBackgroundJobClient> _logger;
    private readonly Dictionary<string, JobExecutionInfo> _jobHistory;

    // TODO: Implement IBackgroundJobClient.Create method
    // TODO: Implement IBackgroundJobClient.Enqueue methods
    // TODO: Implement IBackgroundJobClient.Schedule methods
    // TODO: Implement IBackgroundJobClient.Delete method
    // TODO: Implement IBackgroundJobClient.Requeue method
    // TODO: Implement IBackgroundJobClient.ContinueJobWith method

    // TODO: Add synchronous job execution logic
    // TODO: Add job state tracking
    // TODO: Add PerformContext creation
}
```

#### 1.2 Create TestPerformContext
**File**: `src/Orchestra.Tests/Infrastructure/TestPerformContext.cs`

```csharp
public class TestPerformContext : PerformContext
{
    // TODO: Create minimal PerformContext for tests
    // TODO: Implement BackgroundJob property
    // TODO: Implement CancellationToken property
    // TODO: Implement Connection property (can be null)
    // TODO: Implement Storage property (can be null)
}
```

#### 1.3 Create JobExecutionInfo for Tracking
**File**: `src/Orchestra.Tests/Infrastructure/JobExecutionInfo.cs`

```csharp
public class JobExecutionInfo
{
    public string JobId { get; set; }
    public Type JobType { get; set; }
    public string MethodName { get; set; }
    public object[] Arguments { get; set; }
    public DateTime EnqueuedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public JobExecutionStatus Status { get; set; }
    public Exception Exception { get; set; }
    public object Result { get; set; }
}
```

### Phase 2: Integrate with Test Factories (1-2 hours)

#### 2.1 Update TestWebApplicationFactory
**File**: `src/Orchestra.Tests/TestWebApplicationFactory.cs`

```csharp
// In ConfigureServices:
// TODO: Remove HangfireServer registration
// TODO: Replace IBackgroundJobClient with TestBackgroundJobClient
// TODO: Keep Hangfire configuration for JobStorage.Current (needed by some code paths)
// TODO: Remove AddHangfireServer() call
```

#### 2.2 Update RealEndToEndTestFactory
**File**: `src/Orchestra.Tests/RealEndToEndTestFactory.cs`

```csharp
// Same changes as TestWebApplicationFactory
// TODO: Ensure ClaudeCodeExecutor still works with synchronous execution
```

#### 2.3 Create TestServiceConfiguration Helper
**File**: `src/Orchestra.Tests/Infrastructure/TestServiceConfiguration.cs`

```csharp
public static class TestServiceConfiguration
{
    public static void ConfigureTestHangfire(IServiceCollection services)
    {
        // TODO: Configure Hangfire without server
        // TODO: Register TestBackgroundJobClient
        // TODO: Configure JobStorage for compatibility
    }
}
```

### Phase 3: Handle Job Execution Flow (2-3 hours)

#### 3.1 Implement Synchronous Job Execution
**In TestBackgroundJobClient.cs**:

```csharp
private async Task ExecuteJobSynchronously(Expression<Func<Task>> methodCall)
{
    // TODO: Extract job type and method from expression
    // TODO: Resolve job instance from DI container
    // TODO: Create TestPerformContext
    // TODO: Execute job method with arguments
    // TODO: Handle exceptions and track state
    // TODO: Update job history
}
```

#### 3.2 Handle TaskExecutionJob Special Case
**Consideration**: TaskExecutionJob.ExecuteAsync requires PerformContext parameter

```csharp
// Special handling for TaskExecutionJob
if (jobType == typeof(TaskExecutionJob))
{
    // TODO: Create proper PerformContext with BackgroundJob.Id
    // TODO: Pass all required parameters including PerformContext
    // TODO: Await async execution
}
```

#### 3.3 Create Job Expression Parser
**File**: `src/Orchestra.Tests/Infrastructure/JobExpressionParser.cs`

```csharp
public static class JobExpressionParser
{
    // TODO: Extract type from Expression<Func<T, Task>>
    // TODO: Extract method name from expression
    // TODO: Extract arguments from expression
    // TODO: Handle different expression types
}
```

### Phase 4: Fix Test Compatibility Issues (1-2 hours)

#### 4.1 Update Integration Tests
**File**: `src/Orchestra.Tests/Integration/HangfireCoordinationE2ETests.cs`

```csharp
// TODO: Remove any direct HangfireServer references
// TODO: Update job verification to use TestBackgroundJobClient
// TODO: Ensure DiagnoseHangfireExecution works with test client
```

#### 4.2 Update Test Base Classes
**File**: `src/Orchestra.Tests/Integration/IntegrationTestBase.cs`

```csharp
// TODO: Add helper methods for job verification
// TODO: Add methods to access TestBackgroundJobClient job history
// TODO: Update WaitForTaskCompletion to work synchronously
```

#### 4.3 Create Test Assertions Helper
**File**: `src/Orchestra.Tests/Infrastructure/HangfireTestAssertions.cs`

```csharp
public static class HangfireTestAssertions
{
    // TODO: AssertJobWasEnqueued
    // TODO: AssertJobWasExecuted
    // TODO: AssertJobSucceeded
    // TODO: AssertJobFailed
    // TODO: GetJobExecutionHistory
}
```

### Phase 5: Enable Parallel Execution (30 min)

#### 5.1 Re-enable Test Parallelization
**File**: `src/Orchestra.Tests/AssemblyInfo.cs`

```csharp
// Remove or comment out:
// [assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]

// Add:
[assembly: CollectionBehavior(DisableTestParallelization = false)]
```

#### 5.2 Verify Test Isolation
- Run tests to ensure no shared state issues
- Verify each test collection has isolated job execution
- Confirm no SQLiteStorage disposal errors

### Phase 6: Testing and Validation (1-2 hours)

#### 6.1 Test Suite Execution
```bash
# Run integration tests only
dotnet test --filter "FullyQualifiedName~Integration"

# Run RealE2E tests only
dotnet test --filter "FullyQualifiedName~RealEndToEnd"

# Run all tests in parallel
dotnet test

# Verify performance improvement
# Expected: ~4-5 minutes (vs current 9-10 minutes)
```

#### 6.2 Validation Checklist
- [ ] All 582 tests passing
- [ ] No SQLiteStorage disposal errors
- [ ] Parallel execution working
- [ ] Test execution time reduced by ~50%
- [ ] Job execution tracking working
- [ ] No regression in test reliability

## Technical Considerations

### Challenges and Solutions

1. **PerformContext Creation**
   - Challenge: PerformContext has internal constructors
   - Solution: Use reflection or create minimal mock implementation

2. **Job Expression Parsing**
   - Challenge: Complex expression trees for job methods
   - Solution: Use Hangfire's internal expression visitors as reference

3. **Async Job Execution**
   - Challenge: Synchronous test context executing async jobs
   - Solution: Use GetAwaiter().GetResult() or ConfigureAwait(false)

4. **DI Scope Management**
   - Challenge: Jobs may require scoped services
   - Solution: Create scope per job execution in TestBackgroundJobClient

### Risk Mitigation

1. **Backward Compatibility**
   - Keep changes isolated to test projects
   - Production code unchanged
   - Can revert to Phase 1 solution if issues arise

2. **Test Reliability**
   - Extensive validation after each phase
   - Keep detailed execution logs
   - Monitor for intermittent failures

3. **Performance Regression**
   - Measure baseline performance before changes
   - Profile synchronous execution overhead
   - Ensure no memory leaks in job tracking

## Success Metrics

1. **Functional**
   - 582/582 tests passing ✅
   - Zero SQLiteStorage disposal errors
   - Parallel test execution enabled

2. **Performance**
   - Test execution time: 4-5 minutes (from 9-10 minutes)
   - CPU utilization improved (parallel execution)
   - Memory usage stable

3. **Maintainability**
   - Clear separation of test and production code
   - Well-documented test infrastructure
   - Easy to debug job execution in tests

## Implementation Timeline

- **Phase 1**: Create Test Infrastructure (2-3 hours)
- **Phase 2**: Integrate with Test Factories (1-2 hours)
- **Phase 3**: Handle Job Execution Flow (2-3 hours)
- **Phase 4**: Fix Test Compatibility (1-2 hours)
- **Phase 5**: Enable Parallel Execution (30 minutes)
- **Phase 6**: Testing and Validation (1-2 hours)

**Total Estimated Time**: 8-12 hours

## Alternative Approaches (Not Recommended)

### Alternative 1: Multiple HangfireServer Instances
- Create separate HangfireServer per test collection
- Issues: Complex lifecycle management, resource overhead

### Alternative 2: Shared In-Memory Storage
- Use single Hangfire.InMemory storage for all tests
- Issues: Test contamination, state leakage

### Alternative 3: Mock Entire Job Pipeline
- Mock at higher level, skip job execution
- Issues: Loses integration test value

## Conclusion

This plan provides a clean, maintainable solution to remove HangfireServer dependency from tests while maintaining full integration test coverage. The synchronous execution approach aligns with standard testing practices and will significantly improve test performance and reliability.