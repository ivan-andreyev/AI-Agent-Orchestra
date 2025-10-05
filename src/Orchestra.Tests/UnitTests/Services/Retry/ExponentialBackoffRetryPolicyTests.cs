using Microsoft.Extensions.Logging.Abstractions;
using Orchestra.Core.Services.Retry;
using Xunit;

namespace Orchestra.Tests.UnitTests.Services.Retry;

/// <summary>
/// Unit tests for ExponentialBackoffRetryPolicy
/// </summary>
public class ExponentialBackoffRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_SuccessOnFirstAttempt_ShouldNotRetry()
    {
        // ARRANGE
        var policy = new ExponentialBackoffRetryPolicy(
            NullLogger<ExponentialBackoffRetryPolicy>.Instance,
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(100));

        var attempts = 0;

        // ACT
        var result = await policy.ExecuteAsync(async ct =>
        {
            attempts++;
            return "success";
        });

        // ASSERT
        Assert.Equal("success", result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_FailureThenSuccess_ShouldRetryAndSucceed()
    {
        // ARRANGE
        var policy = new ExponentialBackoffRetryPolicy(
            NullLogger<ExponentialBackoffRetryPolicy>.Instance,
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(10));

        var attempts = 0;

        // ACT
        var result = await policy.ExecuteAsync(async ct =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new HttpRequestException("Simulated failure");
            }

            return "success";
        });

        // ASSERT
        Assert.Equal("success", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_AllAttemptsFail_ShouldThrowException()
    {
        // ARRANGE
        var policy = new ExponentialBackoffRetryPolicy(
            NullLogger<ExponentialBackoffRetryPolicy>.Instance,
            maxAttempts: 2,
            baseDelay: TimeSpan.FromMilliseconds(10));

        var attempts = 0;

        // ACT & ASSERT
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync(async ct =>
            {
                attempts++;
                throw new HttpRequestException("Always fails");
            });
        });

        Assert.Equal(3, attempts); // Initial + 2 retries
    }

    [Fact]
    public async Task ExecuteAsync_NonRetryableException_ShouldNotRetry()
    {
        // ARRANGE
        var policy = new ExponentialBackoffRetryPolicy(
            NullLogger<ExponentialBackoffRetryPolicy>.Instance,
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(10));

        var attempts = 0;

        // ACT & ASSERT
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await policy.ExecuteAsync(async ct =>
            {
                attempts++;
                throw new ArgumentException("Non-retryable exception");
            });
        });

        Assert.Equal(1, attempts); // No retries
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(IOException))]
    public async Task ExecuteAsync_RetryableExceptions_ShouldRetry(Type exceptionType)
    {
        // ARRANGE
        var policy = new ExponentialBackoffRetryPolicy(
            NullLogger<ExponentialBackoffRetryPolicy>.Instance,
            maxAttempts: 2,
            baseDelay: TimeSpan.FromMilliseconds(10));

        var attempts = 0;

        // ACT & ASSERT
        await Assert.ThrowsAsync(exceptionType, async () =>
        {
            await policy.ExecuteAsync(async ct =>
            {
                attempts++;
                throw (Exception)Activator.CreateInstance(exceptionType, "Test exception")!;
            });
        });

        Assert.Equal(3, attempts); // Initial + 2 retries
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldStopImmediately()
    {
        // ARRANGE
        var policy = new ExponentialBackoffRetryPolicy(
            NullLogger<ExponentialBackoffRetryPolicy>.Instance,
            maxAttempts: 5,
            baseDelay: TimeSpan.FromMilliseconds(10));

        var cts = new CancellationTokenSource();
        var attempts = 0;

        // ACT & ASSERT
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await policy.ExecuteAsync(async ct =>
            {
                attempts++;
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return "never reached";
            }, cts.Token);
        });

        Assert.Equal(1, attempts);
    }

    [Fact]
    public void ShouldRetry_WithinMaxAttempts_ShouldReturnTrue()
    {
        // ARRANGE
        var policy = new ExponentialBackoffRetryPolicy(
            NullLogger<ExponentialBackoffRetryPolicy>.Instance,
            maxAttempts: 3,
            baseDelay: TimeSpan.FromSeconds(1));

        var context = new RetryContext
        {
            AttemptNumber = 1,
            StartTime = DateTime.UtcNow
        };

        // ACT
        var decision = policy.ShouldRetry(context);

        // ASSERT
        Assert.True(decision.ShouldRetry);
        Assert.True(decision.Delay > TimeSpan.Zero);
    }

    [Fact]
    public void ShouldRetry_ExceedingMaxAttempts_ShouldReturnFalse()
    {
        // ARRANGE
        var policy = new ExponentialBackoffRetryPolicy(
            NullLogger<ExponentialBackoffRetryPolicy>.Instance,
            maxAttempts: 2,
            baseDelay: TimeSpan.FromSeconds(1));

        var context = new RetryContext
        {
            AttemptNumber = 2,
            StartTime = DateTime.UtcNow
        };

        // ACT
        var decision = policy.ShouldRetry(context);

        // ASSERT
        Assert.False(decision.ShouldRetry);
    }

    [Fact]
    public void ShouldRetry_ExponentialBackoff_ShouldIncreaseDelay()
    {
        // ARRANGE
        var policy = new ExponentialBackoffRetryPolicy(
            NullLogger<ExponentialBackoffRetryPolicy>.Instance,
            maxAttempts: 5,
            baseDelay: TimeSpan.FromMilliseconds(100));

        // ACT
        var delay0 = policy.ShouldRetry(new RetryContext { AttemptNumber = 0, StartTime = DateTime.UtcNow }).Delay;
        var delay1 = policy.ShouldRetry(new RetryContext { AttemptNumber = 1, StartTime = DateTime.UtcNow }).Delay;
        var delay2 = policy.ShouldRetry(new RetryContext { AttemptNumber = 2, StartTime = DateTime.UtcNow }).Delay;

        // ASSERT
        Assert.True(delay1 > delay0 * 1.5); // At least 1.5x (accounting for jitter)
        Assert.True(delay2 > delay1 * 1.5); // At least 1.5x
    }

    [Fact]
    public void Constructor_NegativeMaxAttempts_ShouldThrow()
    {
        // ACT & ASSERT
        Assert.Throws<ArgumentException>(() =>
            new ExponentialBackoffRetryPolicy(
                NullLogger<ExponentialBackoffRetryPolicy>.Instance,
                maxAttempts: -1,
                baseDelay: TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_NegativeBaseDelay_ShouldThrow()
    {
        // ACT & ASSERT
        Assert.Throws<ArgumentException>(() =>
            new ExponentialBackoffRetryPolicy(
                NullLogger<ExponentialBackoffRetryPolicy>.Instance,
                maxAttempts: 3,
                baseDelay: TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Constructor_InvalidJitterFactor_ShouldThrow()
    {
        // ACT & ASSERT
        Assert.Throws<ArgumentException>(() =>
            new ExponentialBackoffRetryPolicy(
                NullLogger<ExponentialBackoffRetryPolicy>.Instance,
                maxAttempts: 3,
                baseDelay: TimeSpan.FromSeconds(1),
                jitterFactor: 1.5)); // > 1.0

        Assert.Throws<ArgumentException>(() =>
            new ExponentialBackoffRetryPolicy(
                NullLogger<ExponentialBackoffRetryPolicy>.Instance,
                maxAttempts: 3,
                baseDelay: TimeSpan.FromSeconds(1),
                jitterFactor: -0.1)); // < 0.0
    }
}
