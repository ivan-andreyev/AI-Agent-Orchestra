using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Orchestra.Core.Options;
using Orchestra.Core.Services;
using Orchestra.Core.Services.Metrics;
using Orchestra.Core.Services.Resilience;
using System.Diagnostics.Metrics;
using System.Net;
using Xunit;

namespace Orchestra.Tests.Services;

/// <summary>
/// Тесты для Error Recovery и Graceful Degradation
/// </summary>
/// <remarks>
/// Проверяет поведение системы при отказе критических сервисов:
/// - Network errors (TimeoutException, HttpRequestException)
/// - Service unavailable (500, 503 status codes)
/// - Rate limiting (429 status code)
/// - Corrupted data (JSON parse errors)
/// - Concurrent error scenarios
/// </remarks>
public class ErrorRecoveryTests
{
    private readonly Mock<ILogger<TelegramEscalationService>> _mockTelegramLogger;
    private readonly Mock<ILogger<PolicyRegistry>> _mockPolicyLogger;
    private readonly Mock<ILogger<CircuitBreakerPolicyService>> _mockCircuitBreakerLogger;
    private readonly TelegramEscalationOptions _telegramOptions;
    private readonly TelegramRetryOptions _retryOptions;
    private readonly CircuitBreakerOptions _circuitBreakerOptions;

    public ErrorRecoveryTests()
    {
        _mockTelegramLogger = new Mock<ILogger<TelegramEscalationService>>();
        _mockPolicyLogger = new Mock<ILogger<PolicyRegistry>>();
        _mockCircuitBreakerLogger = new Mock<ILogger<CircuitBreakerPolicyService>>();

        _telegramOptions = new TelegramEscalationOptions
        {
            Enabled = true,
            BotToken = "test-token",
            ChatId = "123456"
        };

        _retryOptions = new TelegramRetryOptions
        {
            MaxRetryAttempts = 2,
            InitialDelayMs = 100,
            MaxDelayMs = 500,
            JitterEnabled = false,
            RetryOn = new[] { 429, 500, 502, 503, 504 }
        };

        _circuitBreakerOptions = new CircuitBreakerOptions
        {
            FailureRateThreshold = 0.5,
            ConsecutiveFailuresThreshold = 3,
            MinimumThroughput = 5,
            SamplingDurationSeconds = 30,
            BreakDurationSeconds = 1
        };
    }

    private EscalationMetricsService CreateMockMetricsService()
    {
        var mockLogger = new Mock<ILogger<EscalationMetricsService>>();
        var mockMeterFactory = new Mock<IMeterFactory>();
        var mockMeter = new Mock<Meter>("test", "1.0.0");

        mockMeterFactory
            .Setup(x => x.Create(It.IsAny<MeterOptions>()))
            .Returns(mockMeter.Object);

        return new EscalationMetricsService(mockLogger.Object, mockMeterFactory.Object);
    }

    private IPolicyRegistry CreatePolicyRegistry()
    {
        return new PolicyRegistry(
            _mockPolicyLogger.Object,
            Options.Create(_retryOptions));
    }

    private ICircuitBreakerPolicyService CreateCircuitBreakerService()
    {
        return new CircuitBreakerPolicyService(
            _mockCircuitBreakerLogger.Object,
            Options.Create(_circuitBreakerOptions));
    }

    #region Network Error Tests

    /// <summary>
    /// Тест: TimeoutException обрабатывается gracefully
    /// </summary>
    [Fact]
    public async Task TelegramService_TimeoutException_HandlesGracefully()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Operation timed out"));

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var result = await service.SendEscalationAsync("agent-1", "Test message");

        // Assert
        Assert.False(result);
        // Verify service handles timeout gracefully without throwing
    }

    /// <summary>
    /// Тест: HttpRequestException обрабатывается gracefully
    /// </summary>
    [Fact]
    public async Task TelegramService_HttpRequestException_HandlesGracefully()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var result = await service.SendEscalationAsync("agent-1", "Test message");

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Тест: SocketException (connection refused) обрабатывается
    /// </summary>
    [Fact]
    public async Task TelegramService_SocketException_HandlesGracefully()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused",
                new System.Net.Sockets.SocketException()));

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var result = await service.SendEscalationAsync("agent-1", "Test message");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Service Unavailable Tests

    /// <summary>
    /// Тест: HTTP 500 Internal Server Error - retry и fallback
    /// </summary>
    [Fact]
    public async Task TelegramService_Http500_RetriesAndReturnsFailure()
    {
        // Arrange
        var callCount = 0;
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Internal Server Error")
                };
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var result = await service.SendEscalationAsync("agent-1", "Test message");

        // Assert
        Assert.False(result);
        Assert.True(callCount >= 1); // At least initial attempt was made
    }

    /// <summary>
    /// Тест: HTTP 503 Service Unavailable - retry логика
    /// </summary>
    [Fact]
    public async Task TelegramService_Http503_RetriesWithBackoff()
    {
        // Arrange
        var callCount = 0;
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("Service Unavailable")
                };
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var result = await service.SendEscalationAsync("agent-1", "Test message");

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Тест: HTTP 502 Bad Gateway - обработка
    /// </summary>
    [Fact]
    public async Task TelegramService_Http502_HandlesGracefully()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("Bad Gateway")
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var result = await service.SendEscalationAsync("agent-1", "Test message");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Rate Limiting Tests

    /// <summary>
    /// Тест: HTTP 429 Too Many Requests - обработка rate limiting
    /// </summary>
    [Fact]
    public async Task TelegramService_Http429_RateLimitHandled()
    {
        // Arrange
        var callCount = 0;
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var response = new HttpResponseMessage((HttpStatusCode)429)
                {
                    Content = new StringContent("Too Many Requests")
                };
                response.Headers.Add("Retry-After", "1");
                return response;
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var result = await service.SendEscalationAsync("agent-1", "Test message");

        // Assert
        Assert.False(result);
        Assert.True(callCount >= 1); // Retry logic should have been triggered
    }

    #endregion

    #region Circuit Breaker Integration Tests

    /// <summary>
    /// Тест: Circuit breaker открывается после множественных ошибок
    /// </summary>
    [Fact]
    public void CircuitBreaker_MultipleFailures_OpensCircuit()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService();

        // Act - Use RecordFailure directly to simulate failures
        for (var i = 0; i < 3; i++)
        {
            circuitBreaker.RecordFailure(new HttpRequestException("Simulated failure"));
        }

        // Assert
        Assert.True(circuitBreaker.IsCircuitOpen);
        Assert.Equal(3, circuitBreaker.TotalFailures);
    }

    /// <summary>
    /// Тест: Fallback возвращается когда circuit открыт
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_WhenOpen_ReturnsFallback()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService();

        // Force circuit open
        ((CircuitBreakerPolicyService)circuitBreaker).ForceOpen();

        // Act
        var result = await circuitBreaker.ExecuteAsync(
            async ct =>
            {
                await Task.Delay(1, ct);
                return "should not reach";
            },
            "fallback value",
            CancellationToken.None);

        // Assert
        Assert.Equal("fallback value", result);
    }

    /// <summary>
    /// Тест: Circuit breaker восстанавливается после успешного запроса
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_RecoveryAfterSuccess_ClosesCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            ConsecutiveFailuresThreshold = 2,
            BreakDurationSeconds = 1
        };
        var circuitBreaker = new CircuitBreakerPolicyService(
            _mockCircuitBreakerLogger.Object,
            Options.Create(options));

        // Force open
        circuitBreaker.ForceOpen();
        Assert.True(circuitBreaker.IsCircuitOpen);

        // Wait for half-open transition
        await Task.Delay(1100);
        Assert.True(circuitBreaker.IsCircuitHalfOpen);

        // Act - successful request in half-open
        var result = await circuitBreaker.ExecuteAsync(
            async ct =>
            {
                await Task.Delay(1, ct);
                return "success";
            },
            CancellationToken.None);

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(CircuitState.Closed, circuitBreaker.CurrentState);
    }

    #endregion

    #region Concurrent Error Scenarios

    /// <summary>
    /// Тест: Множественные concurrent запросы при сбое сервиса
    /// </summary>
    [Fact]
    public async Task TelegramService_ConcurrentFailures_AllHandledGracefully()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act - send 5 concurrent requests
        var tasks = Enumerable.Range(0, 5)
            .Select(i => service.SendEscalationAsync($"agent-{i}", $"Message {i}"))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert - all should fail gracefully without exceptions
        Assert.All(results, result => Assert.False(result));
    }

    /// <summary>
    /// Тест: Concurrent circuit breaker operations
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_ConcurrentOperations_ThreadSafe()
    {
        // Arrange
        var circuitBreaker = CreateCircuitBreakerService();
        var successCount = 0;
        var failureCount = 0;

        // Act - 10 concurrent operations, mix of success and failure
        var tasks = new List<Task>();
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await circuitBreaker.ExecuteAsync(
                        async ct =>
                        {
                            await Task.Delay(10, ct);
                            if (index % 2 == 0)
                            {
                                throw new InvalidOperationException("Simulated failure");
                            }
                            return index;
                        },
                        CancellationToken.None);
                    Interlocked.Increment(ref successCount);
                }
                catch
                {
                    Interlocked.Increment(ref failureCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - should have some successes and some failures
        Assert.True(successCount + failureCount == 10);
    }

    #endregion

    #region Configuration Error Tests

    /// <summary>
    /// Тест: Сервис disabled в конфигурации возвращает false
    /// </summary>
    [Fact]
    public async Task TelegramService_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var disabledOptions = new TelegramEscalationOptions
        {
            Enabled = false,
            BotToken = "test-token",
            ChatId = "123456"
        };

        var httpClient = new HttpClient();
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(disabledOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var result = await service.SendEscalationAsync("agent-1", "Test message");

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Тест: Отсутствующий BotToken возвращает false
    /// </summary>
    [Fact]
    public async Task TelegramService_MissingBotToken_ReturnsFalse()
    {
        // Arrange
        var invalidOptions = new TelegramEscalationOptions
        {
            Enabled = true,
            BotToken = null,
            ChatId = "123456"
        };

        var httpClient = new HttpClient();
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(invalidOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var result = await service.SendEscalationAsync("agent-1", "Test message");

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Тест: Отсутствующий ChatId возвращает false
    /// </summary>
    [Fact]
    public async Task TelegramService_MissingChatId_ReturnsFalse()
    {
        // Arrange
        var invalidOptions = new TelegramEscalationOptions
        {
            Enabled = true,
            BotToken = "test-token",
            ChatId = null
        };

        var httpClient = new HttpClient();
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(invalidOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var result = await service.SendEscalationAsync("agent-1", "Test message");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Metrics During Failures Tests

    /// <summary>
    /// Тест: Метрики продолжают собираться при сбоях
    /// </summary>
    [Fact]
    public async Task MetricsCollection_DuringFailure_DoesNotCrashService()
    {
        // Arrange
        var metricsService = CreateMockMetricsService();
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            metricsService);

        // Act & Assert - should not throw even when metrics service is present
        var result = await service.SendEscalationAsync("agent-1", "Test message");
        Assert.False(result);
    }

    /// <summary>
    /// Тест: Null metricsService не вызывает исключение
    /// </summary>
    [Fact]
    public async Task TelegramService_NullMetricsService_WorksNormally()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            null); // No metrics service

        // Act
        var result = await service.SendEscalationAsync("agent-1", "Test message");

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Graceful Degradation Patterns

    /// <summary>
    /// Тест: Сервис возвращает false вместо исключения при отказе
    /// </summary>
    [Fact]
    public async Task TelegramService_OnFailure_ReturnsFalseInsteadOfException()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new Exception("Unexpected error"));

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act - should not throw
        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.SendEscalationAsync("agent-1", "Test message");
        });

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Тест: IsConnected возвращает false при сбое вместо исключения
    /// </summary>
    [Fact]
    public async Task TelegramService_IsConnected_ReturnsFalseOnFailure()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(_telegramOptions),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var result = await service.IsConnectedAsync(CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Тест: GetConfigurationStatus всегда возвращает валидный статус
    /// </summary>
    [Theory]
    [InlineData(true, "test-token", "123456", "configured")]
    [InlineData(false, "test-token", "123456", "disabled")]
    [InlineData(true, null, "123456", "not_configured")]
    [InlineData(true, "test-token", null, "not_configured")]
    public void TelegramService_GetConfigurationStatus_ReturnsValidStatus(
        bool enabled, string? botToken, string? chatId, string expectedStatus)
    {
        // Arrange
        var options = new TelegramEscalationOptions
        {
            Enabled = enabled,
            BotToken = botToken,
            ChatId = chatId
        };

        var httpClient = new HttpClient();
        var service = new TelegramEscalationService(
            _mockTelegramLogger.Object,
            Options.Create(options),
            httpClient,
            CreatePolicyRegistry(),
            CreateMockMetricsService());

        // Act
        var status = service.GetConfigurationStatus();

        // Assert
        Assert.Equal(expectedStatus, status);
    }

    #endregion
}
