# Phase 4.2: Timeout Management

**Parent Plan**: [ClaudeCodeSubprocessConnector-Phase4-ProductionHardening.md](../ClaudeCodeSubprocessConnector-Phase4-ProductionHardening.md)
**Estimate**: 2-3 hours
**Priority**: P0 (Critical for preventing hung approvals)

## Overview

Implement automatic timeout handling for approval requests to prevent indefinite waiting and resource leaks. Uses IHostedService pattern for background monitoring and automatic cleanup of expired approvals.

## Task Breakdown

### Task 2.1: Create ApprovalTimeoutService (1.5 hours)

#### 2.1A: Define Service Interface and Models
**Tool Calls**: ~6
- Create Services/IApprovalTimeoutService.cs
- Create Models/PendingApproval.cs
- Create Events/ApprovalTimedOutEvent.cs

**Interface Definition**:
```csharp
public interface IApprovalTimeoutService
{
    void TrackApproval(string approvalId, string sessionId, TimeSpan? customTimeout = null);
    void CompleteApproval(string approvalId);
    bool IsExpired(string approvalId);
    IReadOnlyList<PendingApproval> GetExpiredApprovals();
}

public class PendingApproval
{
    public string ApprovalId { get; set; }
    public string SessionId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string OperatorId { get; set; }
    public string RequestDetails { get; set; }
}
```

#### 2.1B: Implement ApprovalTimeoutService as IHostedService
**Tool Calls**: ~12
- Create Services/ApprovalTimeoutService.cs
- Implement IHostedService and IApprovalTimeoutService
- Add thread-safe collection for tracking
- Implement background timer for checking

**Implementation Structure**:
```csharp
public class ApprovalTimeoutService : BackgroundService, IApprovalTimeoutService
{
    private readonly ConcurrentDictionary<string, PendingApproval> _pendingApprovals;
    private readonly IOptions<ApprovalTimeoutOptions> _options;
    private readonly IMediator _mediator;
    private readonly ILogger<ApprovalTimeoutService> _logger;
    private readonly Timer _checkTimer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _checkTimer = new Timer(
            CheckForExpiredApprovals,
            null,
            TimeSpan.FromSeconds(_options.Value.CheckIntervalSeconds),
            TimeSpan.FromSeconds(_options.Value.CheckIntervalSeconds));

        await Task.CompletedTask;
    }

    private async void CheckForExpiredApprovals(object state)
    {
        var expired = _pendingApprovals
            .Where(kvp => kvp.Value.ExpiresAt <= DateTime.UtcNow)
            .ToList();

        foreach (var (approvalId, approval) in expired)
        {
            _logger.LogWarning(
                "Approval {ApprovalId} for session {SessionId} has timed out after {Duration}",
                approvalId,
                approval.SessionId,
                DateTime.UtcNow - approval.RequestedAt);

            await _mediator.Send(new CancelApprovalCommand(approvalId));
            _pendingApprovals.TryRemove(approvalId, out _);
        }
    }

    public void TrackApproval(string approvalId, string sessionId, TimeSpan? customTimeout = null)
    {
        var timeout = customTimeout ?? TimeSpan.FromMinutes(_options.Value.DefaultTimeoutMinutes);

        var approval = new PendingApproval
        {
            ApprovalId = approvalId,
            SessionId = sessionId,
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(timeout)
        };

        _pendingApprovals.TryAdd(approvalId, approval);
    }
}
```

#### 2.1C: Register Service in DI
**Tool Calls**: ~4
- Update Program.cs to register service
- Configure as singleton IHostedService
- Register IApprovalTimeoutService interface

**DI Registration**:
```csharp
// In Program.cs
services.Configure<ApprovalTimeoutOptions>(
    configuration.GetSection("ClaudeCodeSubprocess:ApprovalTimeout"));

services.AddSingleton<ApprovalTimeoutService>();
services.AddHostedService(provider => provider.GetRequiredService<ApprovalTimeoutService>());
services.AddSingleton<IApprovalTimeoutService>(provider =>
    provider.GetRequiredService<ApprovalTimeoutService>());
```

**Acceptance Criteria**:
- ✅ Service starts automatically with application
- ✅ Background timer runs at configured interval
- ✅ Thread-safe approval tracking
- ✅ Memory-efficient collection management

### Task 2.2: Implement Timeout Handling (1 hour)

#### 2.2A: Create CancelApprovalCommand
**Tool Calls**: ~8
- Create Commands/Permissions/CancelApprovalCommand.cs
- Create Commands/Permissions/CancelApprovalCommandHandler.cs
- Implement cancellation logic
- Send notification to operator

**Command Implementation**:
```csharp
public record CancelApprovalCommand(string ApprovalId, string Reason = "Timeout")
    : ICommand<Unit>;

public class CancelApprovalCommandHandler : IRequestHandler<CancelApprovalCommand, Unit>
{
    private readonly ITelegramEscalationService _telegramService;
    private readonly IApprovalTimeoutService _timeoutService;
    private readonly ILogger<CancelApprovalCommandHandler> _logger;

    public async Task<Unit> Handle(
        CancelApprovalCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Cancelling approval {ApprovalId} due to {Reason}",
            request.ApprovalId,
            request.Reason);

        // Notify operator of cancellation
        await _telegramService.SendNotificationAsync(
            $"⏰ Approval request {request.ApprovalId} has been cancelled due to {request.Reason}");

        // Clean up from timeout service
        _timeoutService.CompleteApproval(request.ApprovalId);

        // Publish domain event
        await _mediator.Publish(
            new ApprovalCancelledEvent(request.ApprovalId, request.Reason),
            cancellationToken);

        return Unit.Value;
    }
}
```

#### 2.2B: Update ProcessHumanApprovalCommandHandler
**Tool Calls**: ~6
- Inject IApprovalTimeoutService
- Check if approval expired before processing
- Remove from tracking on completion
- Handle race conditions

**Enhanced Handler**:
```csharp
public async Task<Unit> Handle(
    ProcessHumanApprovalCommand request,
    CancellationToken cancellationToken)
{
    // Check if approval has already expired
    if (_timeoutService.IsExpired(request.ApprovalId))
    {
        _logger.LogWarning(
            "Attempted to process expired approval {ApprovalId}",
            request.ApprovalId);

        throw new InvalidOperationException(
            $"Approval {request.ApprovalId} has expired and cannot be processed");
    }

    // Process approval...

    // Remove from timeout tracking
    _timeoutService.CompleteApproval(request.ApprovalId);

    return Unit.Value;
}
```

#### 2.2C: Update RequestHumanApprovalCommandHandler
**Tool Calls**: ~5
- Inject IApprovalTimeoutService
- Register approval for timeout tracking
- Support custom timeout per request
- Log timeout configuration

**Integration Code**:
```csharp
public async Task<string> Handle(
    RequestHumanApprovalCommand request,
    CancellationToken cancellationToken)
{
    var approvalId = Guid.NewGuid().ToString();

    // Send Telegram request...

    // Register for timeout tracking
    var customTimeout = request.TimeoutMinutes.HasValue
        ? TimeSpan.FromMinutes(request.TimeoutMinutes.Value)
        : (TimeSpan?)null;

    _timeoutService.TrackApproval(
        approvalId,
        request.SessionId,
        customTimeout);

    _logger.LogInformation(
        "Approval {ApprovalId} registered with timeout of {Timeout} minutes",
        approvalId,
        customTimeout?.TotalMinutes ?? _options.Value.DefaultTimeoutMinutes);

    return approvalId;
}
```

**Acceptance Criteria**:
- ✅ Expired approvals automatically cancelled
- ✅ Operators notified of cancellations
- ✅ Race conditions handled gracefully
- ✅ Custom timeouts supported per request

### Task 2.3: Add Timeout Configuration (30 minutes)

#### 2.3A: Create Configuration Options
**Tool Calls**: ~4
- Create Options/ApprovalTimeoutOptions.cs
- Add validation attributes
- Define sensible defaults

**Options Class**:
```csharp
public class ApprovalTimeoutOptions
{
    [Range(1, 1440)] // 1 minute to 24 hours
    public int DefaultTimeoutMinutes { get; set; } = 30;

    [Range(1, 1440)]
    public int MaxTimeoutMinutes { get; set; } = 120;

    [Range(10, 300)] // 10 seconds to 5 minutes
    public int CheckIntervalSeconds { get; set; } = 60;

    [Range(0, 60)]
    public int GracePeriodSeconds { get; set; } = 30;

    public bool SendTimeoutWarnings { get; set; } = true;

    [Range(1, 30)]
    public int WarningMinutesBeforeTimeout { get; set; } = 5;
}
```

#### 2.3B: Update Configuration Files
**Tool Calls**: ~3
- Update appsettings.json
- Add development overrides
- Create production template

**Configuration**:
```json
{
  "ClaudeCodeSubprocess": {
    "ApprovalTimeout": {
      "DefaultTimeoutMinutes": 30,
      "MaxTimeoutMinutes": 120,
      "CheckIntervalSeconds": 60,
      "GracePeriodSeconds": 30,
      "SendTimeoutWarnings": true,
      "WarningMinutesBeforeTimeout": 5
    }
  }
}
```

#### 2.3C: Add Configuration Validation
**Tool Calls**: ~4
- Create ApprovalTimeoutOptionsValidator
- Register validation in DI
- Add startup checks

**Acceptance Criteria**:
- ✅ Configuration validates on startup
- ✅ Environment-specific overrides work
- ✅ Grace period prevents premature timeouts
- ✅ Warning notifications configurable

### Task 2.4: Create Unit Tests (45 minutes)

#### 2.4A: Test ApprovalTimeoutService
**Tool Calls**: ~10
- Create ApprovalTimeoutServiceTests.cs
- Test approval tracking
- Test expiration detection
- Test cleanup behavior

**Test Cases**:
```csharp
[Fact]
public void TrackApproval_ShouldAddToPendingCollection()
{
    // Test that approvals are tracked correctly
}

[Fact]
public async Task ExpiredApprovals_ShouldBeAutomaticallyCancelled()
{
    // Test automatic cancellation after timeout
}

[Fact]
public void CompleteApproval_ShouldRemoveFromTracking()
{
    // Test that completed approvals are removed
}

[Fact]
public void IsExpired_ShouldReturnCorrectStatus()
{
    // Test expiration detection logic
}

[Fact]
public async Task BackgroundTimer_ShouldRunAtConfiguredInterval()
{
    // Test timer execution interval
}
```

#### 2.4B: Test Command Handlers
**Tool Calls**: ~8
- Test CancelApprovalCommandHandler
- Test timeout integration in ProcessHumanApprovalCommandHandler
- Test tracking in RequestHumanApprovalCommandHandler
- Verify notifications sent

**Integration Tests**:
```csharp
[Fact]
public async Task CancelApprovalCommand_ShouldNotifyOperator()
{
    // Verify Telegram notification sent on cancellation
}

[Fact]
public async Task ProcessExpiredApproval_ShouldThrowException()
{
    // Test that processing expired approval fails
}

[Fact]
public async Task RequestWithCustomTimeout_ShouldOverrideDefault()
{
    // Test custom timeout functionality
}
```

**Acceptance Criteria**:
- ✅ All timeout scenarios covered
- ✅ Thread safety verified
- ✅ Memory cleanup confirmed
- ✅ 95%+ code coverage

## Success Metrics

### Implementation Checklist
- [ ] ApprovalTimeoutService implemented as IHostedService
- [ ] Background timer checks for expired approvals
- [ ] Automatic cancellation with notifications
- [ ] Custom timeout support per request
- [ ] Thread-safe collection management
- [ ] Comprehensive test coverage (95%+)

### Performance Metrics
- Background check overhead <10ms
- Memory usage stable with 1000+ pending approvals
- No memory leaks after 24 hours operation
- Timer accuracy within 1 second

### Operational Metrics
- 100% of expired approvals cancelled
- All cancellations logged with context
- Operators notified within 30 seconds
- No false positive timeouts with grace period

## Dependencies

- IHostedService framework
- ConcurrentDictionary for thread safety
- IMediator for command handling
- Existing TelegramEscalationService
- System.Threading.Timer

## Edge Cases Handled

1. **Race Condition**: Approval processed just as timeout occurs
   - Solution: Check expiration before processing
   - Grace period prevents edge case

2. **Service Restart**: Pending approvals lost on restart
   - Solution: Consider persisting to database in future
   - Current: Accept data loss on restart

3. **Clock Skew**: Server time changes
   - Solution: Always use UTC times
   - Monitor for large time jumps

4. **Memory Pressure**: Too many pending approvals
   - Solution: Implement max pending limit
   - Alert on high memory usage

## Next Steps

After completing timeout management:
1. Proceed to Task 3: Monitoring & Metrics
2. Add timeout metrics to OpenTelemetry
3. Consider persistence for restart resilience
4. Add timeout warning notifications

---

**Status**: READY FOR IMPLEMENTATION
**Estimated Completion**: 2-3 hours
**Test Coverage Target**: 95%+