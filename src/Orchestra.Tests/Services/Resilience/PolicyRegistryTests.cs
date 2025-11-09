using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Options;
using Orchestra.Core.Services.Resilience;
using System.Net;
using Xunit;

namespace Orchestra.Tests.Services.Resilience;

/// <summary>
/// Comprehensive unit tests for PolicyRegistry retry policies
/// </summary>
public class PolicyRegistryTests
{
    private readonly ILogger<PolicyRegistry> _logger;
    private readonly TelegramRetryOptions _defaultOptions;

    public PolicyRegistryTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<PolicyRegistry>();

        _defaultOptions = new TelegramRetryOptions
        {
            MaxRetryAttempts = 3,
            InitialDelayMs = 100, // Shorter for tests
            MaxDelayMs = 1000,
            JitterEnabled = false, // Disable jitter for deterministic tests
            RetryOn = new[] { 429, 500, 502, 503, 504 }
        };
    }

    /// <summary>
    /// Test 1: Policy succeeds on first attempt without retry
    /// </summary>
    [Fact]
    public async Task GetTelegramRetryPolicy_SucceedsOnFirstAttempt_NoRetry()
    {
        // Arrange
        var options = Options.Create(_defaultOptions);
        var registry = new PolicyRegistry(_logger, options);
        var policy = registry.GetTelegramRetryPolicy();
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.Delay(10, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        Assert.Equal(1, attemptCount);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.IsSuccessStatusCode);
    }

    /// <summary>
    /// Test 2: Policy succeeds after retry (transient failure recovery)
    /// </summary>
    [Fact]
    public async Task GetTelegramRetryPolicy_SucceedsAfterRetry_TransientFailureRecovery()
    {
        // Arrange
        var options = Options.Create(_defaultOptions);
        var registry = new PolicyRegistry(_logger, options);
        var policy = registry.GetTelegramRetryPolicy();
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.Delay(10, ct);

            if (attemptCount < 3)
            {
                // Fail first 2 attempts with retryable status code
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); // 503
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        Assert.Equal(3, attemptCount); // 1 initial + 2 retries
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.IsSuccessStatusCode);
    }

    /// <summary>
    /// Test 3: Policy fails after max retries exhausted
    /// </summary>
    [Fact]
    public async Task GetTelegramRetryPolicy_FailsAfterMaxRetries_RetriesExhausted()
    {
        // Arrange
        var options = Options.Create(_defaultOptions);
        var registry = new PolicyRegistry(_logger, options);
        var policy = registry.GetTelegramRetryPolicy();
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.Delay(10, ct);
            // Always fail with retryable status code
            return new HttpResponseMessage(HttpStatusCode.InternalServerError); // 500
        }, CancellationToken.None);

        // Assert
        Assert.Equal(4, attemptCount); // 1 initial + 3 retries (MaxRetryAttempts = 3)
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.False(result.IsSuccessStatusCode);
    }

    /// <summary>
    /// Test 4: Policy applies exponential backoff timing
    /// </summary>
    [Fact]
    public async Task GetTelegramRetryPolicy_AppliesExponentialBackoff_DelayIncreases()
    {
        // Arrange
        var options = Options.Create(_defaultOptions);
        var registry = new PolicyRegistry(_logger, options);
        var policy = registry.GetTelegramRetryPolicy();
        var attemptTimes = new List<DateTime>();

        // Act
        await policy.ExecuteAsync(async ct =>
        {
            attemptTimes.Add(DateTime.UtcNow);
            await Task.Delay(10, ct);

            if (attemptTimes.Count < 4)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); // 503
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        Assert.Equal(4, attemptTimes.Count);

        // Verify exponential backoff delays (with tolerance for timing variance)
        // Expected delays: ~100ms, ~200ms, ~400ms (2^n * InitialDelayMs)
        var delay1 = (attemptTimes[1] - attemptTimes[0]).TotalMilliseconds;
        var delay2 = (attemptTimes[2] - attemptTimes[1]).TotalMilliseconds;
        var delay3 = (attemptTimes[3] - attemptTimes[2]).TotalMilliseconds;

        Assert.InRange(delay1, 80, 150);  // ~100ms (first retry)
        Assert.InRange(delay2, 180, 250); // ~200ms (second retry)
        Assert.InRange(delay3, 380, 450); // ~400ms (third retry)
    }

    /// <summary>
    /// Test 5: Policy retries only on configured HTTP status codes
    /// </summary>
    [Fact]
    public async Task GetTelegramRetryPolicy_RetriesOnlyConfiguredStatusCodes_NoRetryOn404()
    {
        // Arrange
        var options = Options.Create(_defaultOptions);
        var registry = new PolicyRegistry(_logger, options);
        var policy = registry.GetTelegramRetryPolicy();
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.Delay(10, ct);
            // 404 Not Found is NOT in RetryOn list, should not retry
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }, CancellationToken.None);

        // Assert
        Assert.Equal(1, attemptCount); // No retries for 404
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    /// <summary>
    /// Test 6: Policy retries on HttpRequestException (network failure)
    /// </summary>
    [Fact]
    public async Task GetTelegramRetryPolicy_RetriesOnHttpRequestException_NetworkFailure()
    {
        // Arrange
        var options = Options.Create(_defaultOptions);
        var registry = new PolicyRegistry(_logger, options);
        var policy = registry.GetTelegramRetryPolicy();
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync<HttpResponseMessage>(async ct =>
            {
                attemptCount++;
                await Task.Delay(10, ct);
                // Simulate network failure
                throw new HttpRequestException("Network connection failed");
            }, CancellationToken.None);
        });

        // Assert
        Assert.Equal(4, attemptCount); // 1 initial + 3 retries before throwing
    }

    /// <summary>
    /// Test 7: Policy retries on TaskCanceledException (timeout)
    /// </summary>
    [Fact]
    public async Task GetTelegramRetryPolicy_RetriesOnTaskCanceledException_Timeout()
    {
        // Arrange
        var options = Options.Create(_defaultOptions);
        var registry = new PolicyRegistry(_logger, options);
        var policy = registry.GetTelegramRetryPolicy();
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await policy.ExecuteAsync<HttpResponseMessage>(async ct =>
            {
                attemptCount++;
                await Task.Delay(10, ct);
                // Simulate timeout
                throw new TaskCanceledException("Request timed out");
            }, CancellationToken.None);
        });

        // Assert
        Assert.Equal(4, attemptCount); // 1 initial + 3 retries before throwing
    }

    /// <summary>
    /// Test 8: Policy respects MaxDelayMs cap on exponential backoff
    /// </summary>
    [Fact]
    public async Task GetTelegramRetryPolicy_RespectsMaxDelayCap_DelayDoesNotExceedMax()
    {
        // Arrange
        var customOptions = new TelegramRetryOptions
        {
            MaxRetryAttempts = 5,
            InitialDelayMs = 500,
            MaxDelayMs = 1000, // Cap at 1 second
            JitterEnabled = false,
            RetryOn = new[] { 500 }
        };
        var options = Options.Create(customOptions);
        var registry = new PolicyRegistry(_logger, options);
        var policy = registry.GetTelegramRetryPolicy();
        var attemptTimes = new List<DateTime>();

        // Act
        await policy.ExecuteAsync(async ct =>
        {
            attemptTimes.Add(DateTime.UtcNow);
            await Task.Delay(10, ct);

            if (attemptTimes.Count < 6)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        Assert.Equal(6, attemptTimes.Count);

        // Verify delays are capped at MaxDelayMs
        // Expected: 500ms, 1000ms (capped), 1000ms (capped), 1000ms (capped), 1000ms (capped)
        for (int i = 2; i < attemptTimes.Count; i++)
        {
            var delay = (attemptTimes[i] - attemptTimes[i - 1]).TotalMilliseconds;
            Assert.InRange(delay, 950, 1100); // Should be capped at ~1000ms
        }
    }

    /// <summary>
    /// Test 9: Generic retry policy works with custom types
    /// </summary>
    [Fact]
    public async Task GetGenericRetryPolicy_WorksWithCustomType_StringResult()
    {
        // Arrange
        var options = Options.Create(_defaultOptions);
        var registry = new PolicyRegistry(_logger, options);
        var policy = registry.GetGenericRetryPolicy<string>();
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.Delay(10, ct);

            if (attemptCount < 2)
            {
                throw new InvalidOperationException("Transient error");
            }

            return "Success";
        }, CancellationToken.None);

        // Assert
        Assert.Equal(2, attemptCount);
        Assert.Equal("Success", result);
    }

    /// <summary>
    /// Test 10: Policy configuration can be updated via IOptions
    /// </summary>
    [Fact]
    public void PolicyRegistry_AcceptsCustomOptions_ConfigurationApplied()
    {
        // Arrange
        var customOptions = new TelegramRetryOptions
        {
            MaxRetryAttempts = 5,
            InitialDelayMs = 2000,
            MaxDelayMs = 30000,
            JitterEnabled = true,
            RetryOn = new[] { 429, 503 }
        };
        var options = Options.Create(customOptions);

        // Act
        var registry = new PolicyRegistry(_logger, options);

        // Assert
        Assert.NotNull(registry);
        var policy = registry.GetTelegramRetryPolicy();
        Assert.NotNull(policy);
    }
}
