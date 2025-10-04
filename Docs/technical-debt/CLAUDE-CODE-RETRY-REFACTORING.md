# Technical Debt: ClaudeCodeExecutor Retry Logic Refactoring

**Created**: 2025-10-04
**Priority**: MEDIUM
**Estimated Effort**: 4-6 hours
**Type**: Architecture Alignment

---

## Problem Statement

ClaudeCodeExecutor implements custom retry logic (140 lines) that duplicates functionality already available in `Orchestra.Core.Services.RetryExecutor`. This violates DRY principle at the architecture level and creates maintenance burden.

### Code Location
- `src/Orchestra.Agents/ClaudeCode/ClaudeCodeExecutor.cs` (lines 88-229)

### Violations Identified
1. **DRY Violation** - Retry logic duplicates RetryExecutor
2. **SRP Violation** - ExecuteWithRetryAsync has 6+ responsibilities
3. **OCP Violation** - Hardcoded exception types, not extensible
4. **Reinventing the Wheel** - Ignores existing framework service

---

## Current Implementation (Custom Retry)

```csharp
// ClaudeCodeExecutor.cs: Lines 88-229
private async Task<AgentExecutionResponse> ExecuteWithRetryAsync(...)
{
    for (int attempt = 0; attempt <= _configuration.RetryAttempts; attempt++)
    {
        try
        {
            // Manual retry loop with exponential backoff
            // Response construction
            // Metadata tracking
            // Exception handling
        }
        catch (Exception ex) when (IsRetryableException(ex) && attempt < _configuration.RetryAttempts)
        {
            var delay = CalculateExponentialBackoff(attempt);
            await Task.Delay(delay, cancellationToken);
        }
    }
}

private static bool IsRetryableException(Exception ex)
{
    return ex is HttpRequestException ||
           ex is TimeoutException ||
           ex is IOException ||
           ex is UnauthorizedAccessException ||
           (ex is InvalidOperationException && ex.Message.Contains("process"));
}

private TimeSpan CalculateExponentialBackoff(int attempt)
{
    var baseDelayMs = _configuration.RetryDelay.TotalMilliseconds;
    var exponentialDelayMs = baseDelayMs * Math.Pow(2, attempt);
    var jitterMs = Random.Shared.NextDouble() * 0.3 * exponentialDelayMs;
    return TimeSpan.FromMilliseconds(exponentialDelayMs + jitterMs);
}
```

**Problems**:
- 140 lines of retry infrastructure code
- Manual loop management
- Hardcoded exception types
- Custom backoff calculation (duplicates framework logic)
- Response construction mixed with retry logic

---

## Existing Framework Solution

`Orchestra.Core.Services.RetryExecutor` provides:

```csharp
public class RetryExecutor
{
    public async Task<(T? Result, RetryExecutionResult RetryResult)> ExecuteWithRetryAsync<T>(
        Func<Task<T>> taskFunc,
        RetryPolicy retryPolicy,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        // Implements:
        // - Configurable retry policies
        // - Expression-based retry conditions
        // - Exponential backoff with jitter
        // - Detailed attempt tracking
        // - Comprehensive retry result metadata
    }
}
```

**Features**:
- ✅ Exponential backoff with jitter
- ✅ Configurable retry policies
- ✅ Detailed retry attempt tracking
- ✅ Expression-based retry conditions
- ✅ Comprehensive result metadata
- ✅ Proper separation of concerns
- ✅ Already tested (RetryExecutorTests.cs)

---

## Proposed Refactoring

### Step 1: Create ClaudeCodeRetryPolicy

```csharp
public static class ClaudeCodeRetryPolicy
{
    public static RetryPolicy Create(ClaudeCodeConfiguration config)
    {
        return new RetryPolicy
        {
            MaxRetryCount = config.RetryAttempts,
            BackoffStrategy = BackoffStrategy.Exponential,
            BaseDelay = config.RetryDelay,
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromMinutes(2),
            JitterEnabled = true,
            JitterPercentage = 0.3,
            RetryableExceptions = new[]
            {
                nameof(HttpRequestException),
                nameof(TimeoutException),
                nameof(IOException),
                nameof(UnauthorizedAccessException),
                "InvalidOperationException:process" // Custom condition
            }
        };
    }
}
```

### Step 2: Refactor ClaudeCodeExecutor

```csharp
public class ClaudeCodeExecutor : IAgentExecutor
{
    private readonly ClaudeCodeConfiguration _configuration;
    private readonly ILogger<ClaudeCodeExecutor> _logger;
    private readonly RetryExecutor _retryExecutor;

    public ClaudeCodeExecutor(
        IOptions<ClaudeCodeConfiguration> configuration,
        ILogger<ClaudeCodeExecutor> logger,
        RetryExecutor retryExecutor)
    {
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryExecutor = retryExecutor ?? throw new ArgumentNullException(nameof(retryExecutor));
    }

    public async Task<AgentExecutionResponse> ExecuteCommandAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        // Validation (unchanged)
        ValidateInputs(command, workingDirectory);

        var startTime = DateTime.UtcNow;
        await _executionSemaphore.WaitAsync(cancellationToken);

        try
        {
            var retryPolicy = ClaudeCodeRetryPolicy.Create(_configuration);

            var (result, retryResult) = await _retryExecutor.ExecuteWithRetryAsync(
                () => ExecuteSingleAttemptAsync(command, workingDirectory, cancellationToken),
                retryPolicy,
                cancellationToken: cancellationToken
            );

            return BuildResponse(result, retryResult, startTime, workingDirectory);
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    private async Task<AgentExecutionResult> ExecuteSingleAttemptAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        // Try HTTP API first
        var httpResult = await TryExecuteViaHttpApi(command, workingDirectory, cancellationToken);
        if (httpResult != null)
        {
            return httpResult;
        }

        // Fallback to CLI
        return await ExecuteViaCli(command, workingDirectory, cancellationToken);
    }

    private AgentExecutionResponse BuildResponse(
        AgentExecutionResult result,
        RetryExecutionResult retryResult,
        DateTime startTime,
        string workingDirectory)
    {
        var executionTime = DateTime.UtcNow - startTime;

        return new AgentExecutionResponse
        {
            Success = result.Success,
            Output = result.Output,
            ErrorMessage = result.ErrorMessage,
            ExecutionTime = executionTime,
            Metadata = new Dictionary<string, object>
            {
                { "ExecutionMethod", result.ExecutionMethod },
                { "WorkingDirectory", workingDirectory },
                { "AgentType", AgentType },
                { "TotalAttempts", retryResult.TotalAttempts },
                { "RetrySuccess", retryResult.Success },
                { "TotalRetryTime", retryResult.TotalExecutionTime }
            }
        };
    }
}
```

### Step 3: Update DI Registration

```csharp
// Startup.cs
services.AddSingleton<RetryExecutor>();
services.AddSingleton<Orchestra.Agents.ClaudeCode.ClaudeCodeExecutor>();
```

---

## Benefits

### Code Reduction
- **Before**: 475 lines (ClaudeCodeExecutor)
- **After**: ~300 lines (140 lines of retry logic removed)
- **Reduction**: ~37% less code to maintain

### Architecture Improvements
1. ✅ **DRY Compliance** - Uses framework retry service
2. ✅ **SRP Compliance** - ExecuteCommandAsync focuses on orchestration
3. ✅ **OCP Compliance** - Extensible via RetryPolicy configuration
4. ✅ **KISS Compliance** - Simpler, clearer code
5. ✅ **Testability** - Can mock RetryExecutor

### Maintainability
- Single source of truth for retry logic
- Framework-level improvements benefit all consumers
- Consistent retry behavior across services
- Better debugging with RetryExecutionResult

---

## Migration Plan

### Phase 1: Preparation (1 hour)
1. [ ] Verify RetryExecutor supports all required features
2. [ ] Review RetryExecutorTests for edge cases
3. [ ] Create ClaudeCodeRetryPolicy helper
4. [ ] Write integration tests for new approach

### Phase 2: Implementation (2-3 hours)
1. [ ] Refactor ExecuteCommandAsync to use RetryExecutor
2. [ ] Extract ExecuteSingleAttemptAsync method
3. [ ] Create BuildResponse helper method
4. [ ] Update DI registration in Startup.cs

### Phase 3: Validation (1-2 hours)
1. [ ] Run all 46 ClaudeCode unit tests (should pass)
2. [ ] Add tests for retry scenarios
3. [ ] Verify metadata structure unchanged
4. [ ] Performance testing (should be equal or better)

### Phase 4: Cleanup (1 hour)
1. [ ] Remove ExecuteWithRetryAsync method
2. [ ] Remove IsRetryableException helper
3. [ ] Remove CalculateExponentialBackoff helper
4. [ ] Update documentation

---

## Testing Strategy

### Unit Tests (Existing)
All 46 existing tests should pass without modification:
- ClaudeCodeServiceTests (40 tests)
- ClaudeCodeServiceBasicTests (6 tests)

### New Tests Required
1. Test retry policy creation
2. Test ExecuteSingleAttemptAsync fallback
3. Test BuildResponse metadata structure
4. Test RetryExecutor integration

### Integration Tests
Verify retry behavior with:
- Network failures (HTTP API)
- Process timeouts (CLI)
- Permission errors
- Multiple retry attempts

---

## Risks and Mitigation

### Risk 1: RetryExecutor Missing Features
**Probability**: Low
**Mitigation**: Extend RetryExecutor if needed (framework improvement)

### Risk 2: Breaking Existing Tests
**Probability**: Low
**Mitigation**: Tests use public interface, should remain compatible

### Risk 3: Performance Regression
**Probability**: Very Low
**Mitigation**: RetryExecutor likely more optimized than custom loop

### Risk 4: Metadata Structure Changes
**Probability**: Medium
**Mitigation**: Carefully map RetryExecutionResult to existing metadata format

---

## Acceptance Criteria

1. ✅ All 46 ClaudeCode tests passing
2. ✅ Retry logic removed from ClaudeCodeExecutor (140 lines)
3. ✅ Uses Orchestra.Core.Services.RetryExecutor
4. ✅ Metadata structure unchanged (for API compatibility)
5. ✅ Performance equal or better
6. ✅ Code review approves architecture

---

## Related Technical Debt

### Similar Issues
- Check if other executors have custom retry logic
- Consider creating IRetryableExecutor interface

### Documentation Updates
- Update architecture documentation
- Add RetryExecutor usage examples
- Document retry policy configuration

---

## Priority Justification

**Priority: MEDIUM** (not critical, but important)

**Why not HIGH**:
- Current implementation works correctly
- Tests are passing
- No production issues

**Why not LOW**:
- Violates core SOLID principles
- Creates maintenance burden
- Duplicates framework functionality
- Blocks other improvements

**Recommended Timeline**: Next refactoring sprint (post-MVP)

---

## Estimated Effort

| Phase | Estimated Time |
|-------|---------------|
| Preparation | 1 hour |
| Implementation | 2-3 hours |
| Validation | 1-2 hours |
| Cleanup | 1 hour |
| **Total** | **5-7 hours** |

---

## Additional Considerations

### Style Violations (Separate Issue)
While refactoring, also fix:
- XML documentation language (English → Russian)
- Inline comments language (English → Russian)

### Code Review Findings
From code-principles-reviewer:
- Response construction duplication (HTTP vs CLI paths)
- Magic numbers (0.3 jitter percentage)
- Method length concerns

---

## Conclusion

This refactoring aligns ClaudeCodeExecutor with framework architecture, reduces code complexity, and improves maintainability. The effort is justified by long-term benefits, though not critical for MVP launch.

**Recommendation**: Schedule for first post-MVP refactoring sprint.

---

**Related Documents**:
- [RetryExecutor.cs](../../src/Orchestra.Core/Services/RetryExecutor.cs) - Framework retry service
- [RetryExecutorTests.cs](../../src/Orchestra.Tests/UnitTests/Workflow/RetryExecutorTests.cs) - Test examples
- [code-principles-review](../reviews/code-principles-review-2025-10-04.md) - Review findings
- [TECHNICAL_DEBT_PHASE1.md](../TECHNICAL_DEBT_PHASE1.md) - Related debt items
