using Orchestra.Core.Models.Workflow;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests.Workflow;

public class RetryExecutorTests
{
    private readonly RetryExecutor _retryExecutor;
    private readonly ExpressionEvaluator _expressionEvaluator;

    public RetryExecutorTests()
    {
        _expressionEvaluator = new ExpressionEvaluator();
        _retryExecutor = new RetryExecutor(_expressionEvaluator);
    }

    #region Success Cases

    [Fact]
    public async Task ExecuteWithRetryAsync_TaskSucceedsFirstTry_ReturnsSuccessWithSingleAttempt()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetryCount: 3);
        var expectedResult = "success";

        // Act
        var (result, retryResult) = await _retryExecutor.ExecuteWithRetryAsync(
            () => Task.FromResult(expectedResult),
            retryPolicy
        );

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.True(retryResult.Success);
        Assert.Equal(1, retryResult.TotalAttempts);
        Assert.Single(retryResult.Attempts);
        Assert.True(retryResult.Attempts[0].Success);
        Assert.Null(retryResult.FinalException);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_TaskSucceedsAfterRetries_ReturnsSuccessWithCorrectAttempts()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetryCount: 3);
        var attemptCount = 0;
        var expectedResult = "success";

        // Act
        var (result, retryResult) = await _retryExecutor.ExecuteWithRetryAsync(
            () =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new InvalidOperationException("Temporary failure");
                }
                return Task.FromResult(expectedResult);
            },
            retryPolicy
        );

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.True(retryResult.Success);
        Assert.Equal(3, retryResult.TotalAttempts);
        Assert.Equal(3, retryResult.Attempts.Count);

        // First two attempts failed
        Assert.False(retryResult.Attempts[0].Success);
        Assert.False(retryResult.Attempts[1].Success);

        // Third attempt succeeded
        Assert.True(retryResult.Attempts[2].Success);
        Assert.Null(retryResult.FinalException);
    }

    #endregion

    #region Failure Cases

    [Fact]
    public async Task ExecuteWithRetryAsync_TaskFailsAllAttempts_ReturnsFailureWithAllAttempts()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetryCount: 3);
        var expectedException = new InvalidOperationException("Persistent failure");

        // Act
        var (result, retryResult) = await _retryExecutor.ExecuteWithRetryAsync<string>(
            () => throw expectedException,
            retryPolicy
        );

        // Assert
        Assert.Null(result);
        Assert.False(retryResult.Success);
        Assert.Equal(3, retryResult.TotalAttempts);
        Assert.Equal(3, retryResult.Attempts.Count);

        // All attempts failed
        Assert.All(retryResult.Attempts, attempt => Assert.False(attempt.Success));
        Assert.Equal(expectedException, retryResult.FinalException);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_NonRetryableException_StopsImmediately()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            MaxRetryCount: 3,
            RetryableExceptions: new[] { "TimeoutException" }
        );
        var nonRetryableException = new ArgumentException("Non-retryable");

        // Act
        var (result, retryResult) = await _retryExecutor.ExecuteWithRetryAsync<string>(
            () => throw nonRetryableException,
            retryPolicy
        );

        // Assert
        Assert.Null(result);
        Assert.False(retryResult.Success);
        Assert.Equal(1, retryResult.TotalAttempts); // Only one attempt
        Assert.Single(retryResult.Attempts);
        Assert.Equal(nonRetryableException, retryResult.FinalException);
    }

    #endregion

    #region Exponential Backoff Tests

    [Fact]
    public void CalculateNextDelay_ExponentialBackoff_ReturnsCorrectDelays()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            BaseDelay: TimeSpan.FromSeconds(1),
            BackoffMultiplier: 2.0,
            MaxDelay: TimeSpan.FromMinutes(5)
        );

        // Act & Assert
        var delay1 = _retryExecutor.CalculateNextDelay(1, retryPolicy);
        var delay2 = _retryExecutor.CalculateNextDelay(2, retryPolicy);
        var delay3 = _retryExecutor.CalculateNextDelay(3, retryPolicy);

        Assert.Equal(TimeSpan.FromSeconds(1), delay1);    // 1 * 2^0 = 1 second
        Assert.Equal(TimeSpan.FromSeconds(2), delay2);    // 1 * 2^1 = 2 seconds
        Assert.Equal(TimeSpan.FromSeconds(4), delay3);    // 1 * 2^2 = 4 seconds
    }

    [Fact]
    public void CalculateNextDelay_ExceedsMaxDelay_LimitedToMaxDelay()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            BaseDelay: TimeSpan.FromSeconds(10),
            BackoffMultiplier: 3.0,
            MaxDelay: TimeSpan.FromSeconds(15)
        );

        // Act
        var delay = _retryExecutor.CalculateNextDelay(5, retryPolicy); // Would be 10 * 3^4 = 810 seconds

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(15), delay); // Limited to MaxDelay
    }

    #endregion

    #region Exception Filtering Tests

    [Fact]
    public async Task ExecuteWithRetryAsync_RetryableException_RetriesAsExpected()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            MaxRetryCount: 2,
            RetryableExceptions: new[] { "TimeoutException", "HttpRequestException" }
        );

        var attemptCount = 0;

        // Act
        var (result, retryResult) = await _retryExecutor.ExecuteWithRetryAsync<string>(
            () =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    throw new TimeoutException("Network timeout");
                }
                return Task.FromResult("success");
            },
            retryPolicy
        );

        // Assert
        Assert.Equal("success", result);
        Assert.True(retryResult.Success);
        Assert.Equal(2, retryResult.TotalAttempts);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_AllExceptionsRetryableWhenListEmpty_RetriesForAnyException()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetryCount: 2);
        var attemptCount = 0;

        // Act
        var (result, retryResult) = await _retryExecutor.ExecuteWithRetryAsync<string>(
            () =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    throw new NotImplementedException("Any exception");
                }
                return Task.FromResult("success");
            },
            retryPolicy
        );

        // Assert
        Assert.Equal("success", result);
        Assert.True(retryResult.Success);
        Assert.Equal(2, retryResult.TotalAttempts);
    }

    #endregion

    #region Retry Condition Tests

    [Fact]
    public async Task ExecuteWithRetryAsync_RetryConditionFalse_StopsRetrying()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            MaxRetryCount: 3,
            RetryCondition: "$exception_type == 'TimeoutException'"
        );

        // Act
        var (result, retryResult) = await _retryExecutor.ExecuteWithRetryAsync<string>(
            () => throw new ArgumentException("Not a timeout"),
            retryPolicy
        );

        // Assert
        Assert.Null(result);
        Assert.False(retryResult.Success);
        Assert.Equal(1, retryResult.TotalAttempts); // Stopped after first attempt
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_RetryConditionTrue_ContinuesRetrying()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            MaxRetryCount: 3,
            RetryCondition: "$exception_type == 'TimeoutException'"
        );

        // Act
        var (result, retryResult) = await _retryExecutor.ExecuteWithRetryAsync<string>(
            () => throw new TimeoutException("Network timeout"),
            retryPolicy
        );

        // Assert
        Assert.Null(result);
        Assert.False(retryResult.Success);
        Assert.Equal(3, retryResult.TotalAttempts); // All attempts used
    }

    #endregion

    #region Timing and Performance Tests

    [Fact]
    public async Task ExecuteWithRetryAsync_WithDelays_RecordsAccurateTimings()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            MaxRetryCount: 2,
            BaseDelay: TimeSpan.FromMilliseconds(10),
            BackoffMultiplier: 1.0 // No exponential growth for predictable timing
        );

        var attemptCount = 0;

        // Act
        var startTime = DateTime.UtcNow;
        var (result, retryResult) = await _retryExecutor.ExecuteWithRetryAsync<string>(
            () =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    throw new InvalidOperationException("First attempt fails");
                }
                return Task.FromResult("success");
            },
            retryPolicy
        );
        var endTime = DateTime.UtcNow;

        // Assert
        Assert.Equal("success", result);
        Assert.True(retryResult.Success);
        Assert.Equal(2, retryResult.TotalAttempts);

        // Check that total execution time includes delays
        var actualDuration = endTime - startTime;
        Assert.True(retryResult.TotalExecutionTime >= TimeSpan.FromMilliseconds(10));
        Assert.True(actualDuration >= TimeSpan.FromMilliseconds(10));

        // Check retry delay was recorded
        Assert.NotNull(retryResult.Attempts[0].NextRetryDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(10), retryResult.Attempts[0].NextRetryDelay);
    }

    #endregion

    #region RetryPolicy Model Tests

    [Fact]
    public void RetryPolicy_DefaultConstructor_SetsReasonableDefaults()
    {
        // Act
        var policy = new RetryPolicy();

        // Assert
        Assert.Equal(3, policy.MaxRetryCount);
        Assert.Equal(TimeSpan.FromSeconds(1), policy.BaseDelay);
        Assert.Equal(TimeSpan.FromMinutes(5), policy.MaxDelay);
        Assert.Equal(2.0, policy.BackoffMultiplier);
        Assert.Null(policy.RetryableExceptions);
        Assert.Null(policy.RetryCondition);
    }

    [Fact]
    public void RetryPolicy_MaxRetryCountConstructor_SetsCorrectValues()
    {
        // Act
        var policy = new RetryPolicy(maxRetryCount: 5);

        // Assert
        Assert.Equal(5, policy.MaxRetryCount);
        Assert.Equal(TimeSpan.FromSeconds(1), policy.BaseDelay);
        Assert.Equal(TimeSpan.FromMinutes(5), policy.MaxDelay);
        Assert.Equal(2.0, policy.BackoffMultiplier);
    }

    [Fact]
    public void RetryAttemptResult_CreatesCorrectRecord()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var executionTime = TimeSpan.FromMilliseconds(500);
        var nextDelay = TimeSpan.FromSeconds(2);

        // Act
        var result = new RetryAttemptResult(
            AttemptNumber: 2,
            Success: false,
            Exception: exception,
            ExecutionTime: executionTime,
            NextRetryDelay: nextDelay
        );

        // Assert
        Assert.Equal(2, result.AttemptNumber);
        Assert.False(result.Success);
        Assert.Equal(exception, result.Exception);
        Assert.Equal(executionTime, result.ExecutionTime);
        Assert.Equal(nextDelay, result.NextRetryDelay);
    }

    #endregion
}