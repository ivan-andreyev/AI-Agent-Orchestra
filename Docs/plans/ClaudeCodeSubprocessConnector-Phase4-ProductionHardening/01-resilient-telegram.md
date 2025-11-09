# Phase 4.1: Resilient Telegram Integration

**Parent Plan**: [ClaudeCodeSubprocessConnector-Phase4-ProductionHardening.md](../ClaudeCodeSubprocessConnector-Phase4-ProductionHardening.md)
**Status**: ✅ **COMPLETED** (2025-11-09)
**Estimate**: 2-3 hours
**Actual**: ~2 hours
**Priority**: P0 (First component of production hardening)

**Implementation Summary**: [01-resilient-telegram-IMPLEMENTATION-SUMMARY.md](./01-resilient-telegram-IMPLEMENTATION-SUMMARY.md)

## Overview

Implement industry-standard retry logic using Polly library to handle transient failures in Telegram API calls. This ensures reliable message delivery even during network instability or temporary Telegram service issues.

## Task Breakdown

### Task 1.1: Install and Configure Polly (30 minutes)

#### 1.1A: Add Polly NuGet Packages
**Tool Calls**: ~5
- Edit Orchestra.Core.csproj to add PackageReference
- Run dotnet restore
- Verify package installation

**Implementation**:
```xml
<!-- Add to Orchestra.Core.csproj -->
<PackageReference Include="Polly" Version="8.4.2" />
<PackageReference Include="Polly.Extensions.Http" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="8.0.11" />
```

#### 1.1B: Create Policy Registry
**Tool Calls**: ~8
- Create Services/Resilience/IPolicyRegistry.cs
- Create Services/Resilience/PolicyRegistry.cs
- Register in DI container

**Files to Create**:
```csharp
// IPolicyRegistry.cs
public interface IPolicyRegistry
{
    IAsyncPolicy<HttpResponseMessage> GetTelegramRetryPolicy();
    IAsyncPolicy<T> GetGenericRetryPolicy<T>();
}

// PolicyRegistry.cs
public class PolicyRegistry : IPolicyRegistry
{
    private readonly IOptions<TelegramRetryOptions> _options;
    // Implementation with configurable policies
}
```

#### 1.1C: Register in DI Container
**Tool Calls**: ~3
- Edit Program.cs to register PolicyRegistry
- Configure HttpClient with Polly handler
- Verify DI resolution

**Acceptance Criteria**:
- ✅ Polly packages installed and restored
- ✅ PolicyRegistry registered in DI
- ✅ Configuration compiles without errors

### Task 1.2: Implement Retry Policy for TelegramEscalationService (1.5 hours)

#### 1.2A: Create Retry Configuration Options
**Tool Calls**: ~5
- Create Options/TelegramRetryOptions.cs
- Add validation attributes
- Add configuration section to appsettings.json

**Implementation**:
```csharp
public class TelegramRetryOptions
{
    [Range(1, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    [Range(100, 60000)]
    public int InitialDelayMs { get; set; } = 1000;

    [Range(1000, 300000)]
    public int MaxDelayMs { get; set; } = 16000;

    public bool JitterEnabled { get; set; } = true;

    public int[] RetryOn { get; set; } = { 429, 500, 502, 503, 504 };
}
```

#### 1.2B: Enhance TelegramEscalationService with Retry
**Tool Calls**: ~12
- Inject IPolicyRegistry into TelegramEscalationService
- Wrap SendTextMessageAsync calls with retry policy
- Add structured logging for retry attempts
- Handle PolicyExecutionContext for correlation

**Key Changes**:
```csharp
public async Task<string> SendApprovalRequestAsync(request)
{
    var retryPolicy = _policyRegistry.GetTelegramRetryPolicy();

    return await retryPolicy.ExecuteAsync(async (context) =>
    {
        _logger.LogDebug("Sending Telegram message, Attempt: {Attempt}",
            context["retryCount"]);

        var message = await _botClient.SendTextMessageAsync(
            chatId: _chatId,
            text: request.Message,
            replyMarkup: keyboard);

        return message.MessageId.ToString();
    },
    new Dictionary<string, object>
    {
        ["correlationId"] = request.CorrelationId
    });
}
```

#### 1.2C: Implement Exponential Backoff with Jitter
**Tool Calls**: ~8
- Create backoff strategy with decorrelated jitter
- Configure Polly WaitAndRetry policy
- Add telemetry for backoff delays

**Policy Configuration**:
```csharp
public IAsyncPolicy<HttpResponseMessage> GetTelegramRetryPolicy()
{
    var jitterer = new Random();

    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => _options.Value.RetryOn.Contains((int)msg.StatusCode))
        .WaitAndRetryAsync(
            retryCount: _options.Value.MaxRetryAttempts,
            sleepDurationProvider: retryAttempt =>
            {
                var exponentialDelay = TimeSpan.FromMilliseconds(
                    Math.Min(
                        _options.Value.InitialDelayMs * Math.Pow(2, retryAttempt - 1),
                        _options.Value.MaxDelayMs));

                return _options.Value.JitterEnabled
                    ? exponentialDelay + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000))
                    : exponentialDelay;
            },
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                _logger.LogWarning(
                    "Telegram API call failed. Waiting {Delay}ms before retry {RetryCount}/{MaxRetries}. CorrelationId: {CorrelationId}",
                    timespan.TotalMilliseconds,
                    retryCount,
                    _options.Value.MaxRetryAttempts,
                    context.Values.GetValueOrDefault("correlationId"));
            });
}
```

**Acceptance Criteria**:
- ✅ All Telegram API calls wrapped with retry policy
- ✅ Exponential backoff implemented with configurable jitter
- ✅ Retry attempts logged with correlation IDs
- ✅ Specific HTTP status codes trigger retries

### Task 1.3: Add Retry Configuration (1 hour)

#### 1.3A: Update appsettings.json
**Tool Calls**: ~4
- Edit appsettings.json with retry configuration
- Edit appsettings.Development.json with dev overrides
- Add appsettings.Production.json template

**Configuration Structure**:
```json
{
  "ClaudeCodeSubprocess": {
    "TelegramRetry": {
      "MaxRetryAttempts": 3,
      "InitialDelayMs": 1000,
      "MaxDelayMs": 16000,
      "JitterEnabled": true,
      "RetryOn": [429, 500, 502, 503, 504]
    }
  }
}
```

#### 1.3B: Implement Configuration Validation
**Tool Calls**: ~6
- Add IValidateOptions implementation
- Register validation in DI
- Add startup validation check

**Validation Implementation**:
```csharp
public class TelegramRetryOptionsValidator : IValidateOptions<TelegramRetryOptions>
{
    public ValidateOptionsResult Validate(string name, TelegramRetryOptions options)
    {
        var errors = new List<string>();

        if (options.MaxRetryAttempts < 1 || options.MaxRetryAttempts > 10)
            errors.Add("MaxRetryAttempts must be between 1 and 10");

        if (options.InitialDelayMs >= options.MaxDelayMs)
            errors.Add("InitialDelayMs must be less than MaxDelayMs");

        return errors.Any()
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
```

#### 1.3C: Add Hot Reload Support
**Tool Calls**: ~5
- Configure IOptionsMonitor for hot reload
- Add configuration change callback
- Test configuration reload without restart

**Acceptance Criteria**:
- ✅ Configuration validates on startup
- ✅ Invalid configuration prevents startup
- ✅ Configuration changes apply without restart
- ✅ Environment-specific overrides work

### Task 1.4: Create Unit Tests (45 minutes)

#### 1.4A: Test Retry Policy Behavior
**Tool Calls**: ~10
- Create PolicyRegistryTests.cs
- Test exponential backoff calculation
- Test jitter distribution
- Test max retry exhaustion

**Test Cases**:
```csharp
[Fact]
public async Task RetryPolicy_ShouldRetryOnTransientFailure()
{
    // Arrange
    var attempts = 0;
    var policy = CreateTestPolicy();

    // Act
    var result = await policy.ExecuteAsync(async () =>
    {
        attempts++;
        if (attempts < 3)
            throw new HttpRequestException("Transient error");
        return new HttpResponseMessage(HttpStatusCode.OK);
    });

    // Assert
    Assert.Equal(3, attempts);
    Assert.Equal(HttpStatusCode.OK, result.StatusCode);
}

[Fact]
public async Task RetryPolicy_ShouldRespectMaxRetries()
{
    // Test that after max retries, exception is thrown
}

[Fact]
public void ExponentialBackoff_ShouldCalculateCorrectly()
{
    // Test backoff calculation with and without jitter
}
```

#### 1.4B: Test TelegramEscalationService Integration
**Tool Calls**: ~8
- Create enhanced TelegramEscalationServiceTests
- Mock Telegram API failures
- Verify retry behavior
- Test logging output

**Acceptance Criteria**:
- ✅ All retry scenarios covered
- ✅ Backoff timing verified
- ✅ Logging assertions pass
- ✅ 95%+ code coverage for retry logic

## Success Metrics

### Implementation Checklist
- [ ] Polly library integrated successfully
- [ ] All Telegram API calls use retry policy
- [ ] Exponential backoff with jitter implemented
- [ ] Configuration hot-reloadable
- [ ] Comprehensive test coverage (95%+)
- [ ] Structured logging for all retry attempts

### Performance Metrics
- Retry adds <5ms overhead for successful calls
- 95%+ recovery rate from transient failures
- Jitter prevents thundering herd problem
- Memory usage stable under retry scenarios

### Operational Metrics
- All retry attempts logged with correlation IDs
- Configuration changes apply without downtime
- Failed retries include actionable error details
- Metrics available for retry success/failure rates

## Dependencies

- Polly 8.4.2
- Polly.Extensions.Http 8.0.0
- Microsoft.Extensions.Http.Polly 8.0.11
- Existing TelegramEscalationService
- IOptions configuration pattern

## Next Steps

After completing resilient Telegram integration:
1. Proceed to Task 2: Timeout Management
2. Integrate retry metrics with OpenTelemetry (Task 3)
3. Add circuit breaker on top of retry (Task 4)

---

**Status**: READY FOR IMPLEMENTATION
**Estimated Completion**: 2-3 hours
**Test Coverage Target**: 95%+