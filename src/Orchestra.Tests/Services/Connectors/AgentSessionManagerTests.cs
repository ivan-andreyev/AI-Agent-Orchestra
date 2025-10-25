using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Models;
using Orchestra.Core.Services.Connectors;
using Xunit;

namespace Orchestra.Tests.Services.Connectors;

/// <summary>
/// Комплексные тесты для AgentSessionManager.
/// Покрывают создание, получение, отключение сессий, события и потокобезопасность.
/// </summary>
public class AgentSessionManagerTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Mock<IAgentConnector> _mockConnector;
    private readonly Mock<IAgentOutputBuffer> _mockOutputBuffer;
    private readonly AgentSessionManager _sessionManager;

    public AgentSessionManagerTests()
    {
        // Настраиваем mock для IAgentConnector
        _mockConnector = new Mock<IAgentConnector>();
        _mockConnector.SetupGet(c => c.Status).Returns(ConnectionStatus.Disconnected);
        _mockConnector
            .Setup(c => c.ConnectAsync(It.IsAny<string>(), It.IsAny<AgentConnectionParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionResult { Success = true });
        _mockConnector
            .Setup(c => c.DisconnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DisconnectionResult { Success = true });

        // Настраиваем mock для IAgentOutputBuffer
        _mockOutputBuffer = new Mock<IAgentOutputBuffer>();

        // Настраиваем DI контейнер для тестов
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddTransient<IAgentConnector>(_ => _mockConnector.Object);
        services.AddTransient<IAgentOutputBuffer>(_ => _mockOutputBuffer.Object);

        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<AgentSessionManager>>();
        _sessionManager = new AgentSessionManager(_serviceProvider, logger);
    }

    public void Dispose()
    {
        _sessionManager?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
    }

    #region CreateSessionAsync Tests

    [Fact]
    public async Task CreateSessionAsync_ValidParams_CreatesSession()
    {
        // Arrange
        var agentId = "test-agent-1";
        var connectionParams = new AgentConnectionParams
        {
            ConnectorType = "terminal",
            SocketPath = "/tmp/test.sock",
            ConnectionTimeoutSeconds = 30
        };

        // Act
        var session = await _sessionManager.CreateSessionAsync(agentId, connectionParams);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(agentId, session.AgentId);
        Assert.NotNull(session.Connector);
        Assert.NotNull(session.OutputBuffer);
        Assert.True(session.CreatedAt <= DateTime.UtcNow);
        Assert.True(session.LastActivityAt <= DateTime.UtcNow);
        Assert.Same(connectionParams, session.ConnectionParams);
    }

    [Fact]
    public async Task CreateSessionAsync_CallsConnectorConnect()
    {
        // Arrange
        var agentId = "test-agent-2";
        var connectionParams = new AgentConnectionParams { ConnectorType = "terminal", SocketPath = "/tmp/test2.sock" };

        // Act
        await _sessionManager.CreateSessionAsync(agentId, connectionParams);

        // Assert
        _mockConnector.Verify(
            c => c.ConnectAsync(agentId, connectionParams, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSessionAsync_NullAgentId_ThrowsArgumentNullException()
    {
        // Arrange
        var connectionParams = new AgentConnectionParams { ConnectorType = "terminal", SocketPath = "/tmp/test.sock" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _sessionManager.CreateSessionAsync(null!, connectionParams));
    }

    [Fact]
    public async Task CreateSessionAsync_EmptyAgentId_ThrowsArgumentNullException()
    {
        // Arrange
        var connectionParams = new AgentConnectionParams { ConnectorType = "terminal", SocketPath = "/tmp/test.sock" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _sessionManager.CreateSessionAsync("", connectionParams));
    }

    [Fact]
    public async Task CreateSessionAsync_NullConnectionParams_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _sessionManager.CreateSessionAsync("agent-id", null!));
    }

    [Fact]
    public async Task CreateSessionAsync_DuplicateAgentId_ThrowsInvalidOperationException()
    {
        // Arrange
        var agentId = "duplicate-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/test.sock" };

        // Act
        await _sessionManager.CreateSessionAsync(agentId, connectionParams);

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _sessionManager.CreateSessionAsync(agentId, connectionParams));
        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task CreateSessionAsync_ConnectionFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var agentId = "failing-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/fail.sock" };

        _mockConnector
            .Setup(c => c.ConnectAsync(agentId, connectionParams, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionResult { Success = false, ErrorMessage = "Connection refused" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _sessionManager.CreateSessionAsync(agentId, connectionParams));
        Assert.Contains("Failed to connect", exception.Message);
    }

    [Fact]
    public async Task CreateSessionAsync_RaisesSessionCreatedEvent()
    {
        // Arrange
        var agentId = "event-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/event.sock" };
        SessionCreatedEventArgs? eventArgs = null;

        _sessionManager.SessionCreated += (sender, args) => eventArgs = args;

        // Act
        await _sessionManager.CreateSessionAsync(agentId, connectionParams);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal(agentId, eventArgs.AgentId);
        Assert.True(eventArgs.CreatedAt <= DateTime.UtcNow);
    }

    #endregion

    #region GetSessionAsync Tests

    [Fact]
    public async Task GetSessionAsync_ExistingSession_ReturnsSession()
    {
        // Arrange
        var agentId = "existing-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/existing.sock" };
        await _sessionManager.CreateSessionAsync(agentId, connectionParams);

        // Act
        var session = _sessionManager.GetSessionAsync(agentId);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(agentId, session.AgentId);
    }

    [Fact]
    public void GetSessionAsync_NonExistingSession_ReturnsNull()
    {
        // Act
        var session = _sessionManager.GetSessionAsync("non-existing-agent");

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public void GetSessionAsync_NullAgentId_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _sessionManager.GetSessionAsync(null!));
    }

    [Fact]
    public void GetSessionAsync_EmptyAgentId_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _sessionManager.GetSessionAsync(""));
    }

    [Fact]
    public async Task GetSessionAsync_UpdatesLastActivityAt()
    {
        // Arrange
        var agentId = "activity-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/activity.sock" };
        var createdSession = await _sessionManager.CreateSessionAsync(agentId, connectionParams);
        var originalActivityTime = createdSession.LastActivityAt;

        // Небольшая задержка для точности теста
        await Task.Delay(50);

        // Act
        var retrievedSession = _sessionManager.GetSessionAsync(agentId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.True(retrievedSession.LastActivityAt > originalActivityTime);
    }

    #endregion

    #region DisconnectSessionAsync Tests

    [Fact]
    public async Task DisconnectSessionAsync_ExistingSession_ReturnsTrue()
    {
        // Arrange
        var agentId = "disconnect-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/disconnect.sock" };
        await _sessionManager.CreateSessionAsync(agentId, connectionParams);

        // Act
        var result = await _sessionManager.DisconnectSessionAsync(agentId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DisconnectSessionAsync_NonExistingSession_ReturnsFalse()
    {
        // Act
        var result = await _sessionManager.DisconnectSessionAsync("non-existing");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DisconnectSessionAsync_NullAgentId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _sessionManager.DisconnectSessionAsync(null!));
    }

    [Fact]
    public async Task DisconnectSessionAsync_CallsConnectorDisconnect()
    {
        // Arrange
        var agentId = "connector-disconnect-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/conn-disconnect.sock" };
        await _sessionManager.CreateSessionAsync(agentId, connectionParams);

        // Act
        await _sessionManager.DisconnectSessionAsync(agentId);

        // Assert
        _mockConnector.Verify(
            c => c.DisconnectAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DisconnectSessionAsync_RemovesSessionFromManager()
    {
        // Arrange
        var agentId = "remove-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/remove.sock" };
        await _sessionManager.CreateSessionAsync(agentId, connectionParams);

        // Act
        await _sessionManager.DisconnectSessionAsync(agentId);

        // Assert
        var session = _sessionManager.GetSessionAsync(agentId);
        Assert.Null(session);
    }

    [Fact]
    public async Task DisconnectSessionAsync_RaisesSessionDisconnectedEvent()
    {
        // Arrange
        var agentId = "event-disconnect-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/event-disconnect.sock" };
        await _sessionManager.CreateSessionAsync(agentId, connectionParams);

        SessionDisconnectedEventArgs? eventArgs = null;
        _sessionManager.SessionDisconnected += (sender, args) => eventArgs = args;

        // Act
        await _sessionManager.DisconnectSessionAsync(agentId);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal(agentId, eventArgs.AgentId);
        Assert.True(eventArgs.DisconnectedAt <= DateTime.UtcNow);
    }

    #endregion

    #region GetAllSessionsAsync Tests

    [Fact]
    public async Task GetAllSessionsAsync_NoSessions_ReturnsEmptyCollection()
    {
        // Act
        var sessions = await _sessionManager.GetAllSessionsAsync();

        // Assert
        Assert.NotNull(sessions);
        Assert.Empty(sessions);
    }

    [Fact]
    public async Task GetAllSessionsAsync_MultipleSessions_ReturnsAllSessions()
    {
        // Arrange
        var connectionParams = new AgentConnectionParams { ConnectorType = "terminal", SocketPath = "/tmp/test.sock" };
        await _sessionManager.CreateSessionAsync("agent1", connectionParams);
        await _sessionManager.CreateSessionAsync("agent2", connectionParams);
        await _sessionManager.CreateSessionAsync("agent3", connectionParams);

        // Act
        var sessions = await _sessionManager.GetAllSessionsAsync();

        // Assert
        Assert.NotNull(sessions);
        Assert.Equal(3, sessions.Count);
    }

    [Fact]
    public async Task GetAllSessionsAsync_ReturnsReadOnlyCollection()
    {
        // Arrange
        var connectionParams = new AgentConnectionParams { ConnectorType = "terminal", SocketPath = "/tmp/test.sock" };
        await _sessionManager.CreateSessionAsync("agent1", connectionParams);

        // Act
        var sessions = await _sessionManager.GetAllSessionsAsync();

        // Assert
        Assert.IsAssignableFrom<IReadOnlyCollection<AgentSession>>(sessions);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentCreateSession_DifferentAgents_AllSucceed()
    {
        // Arrange
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/concurrent.sock" };
        var tasks = new List<Task<AgentSession>>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var agentId = $"concurrent-agent-{i}";
            tasks.Add(_sessionManager.CreateSessionAsync(agentId, connectionParams));
        }

        var sessions = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, sessions.Length);
        var allSessions = await _sessionManager.GetAllSessionsAsync();
        Assert.Equal(10, allSessions.Count);
    }

    [Fact]
    public async Task ConcurrentCreateSession_SameAgent_OnlyOneSucceeds()
    {
        // Arrange
        var agentId = "duplicate-concurrent-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/duplicate.sock" };
        var tasks = new List<Task<AgentSession>>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_sessionManager.CreateSessionAsync(agentId, connectionParams));
        }

        // Assert
        var exceptions = 0;
        var successfulSessions = 0;

        foreach (var task in tasks)
        {
            try
            {
                await task;
                successfulSessions++;
            }
            catch (InvalidOperationException)
            {
                exceptions++;
            }
        }

        Assert.Equal(1, successfulSessions);
        Assert.Equal(4, exceptions);
    }

    [Fact]
    public async Task ConcurrentGetSession_MultipleCalls_AllSucceed()
    {
        // Arrange
        var agentId = "get-concurrent-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/get-concurrent.sock" };
        await _sessionManager.CreateSessionAsync(agentId, connectionParams);

        // Act
        var tasks = new List<Task<AgentSession?>>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => _sessionManager.GetSessionAsync(agentId)));
        }

        var sessions = await Task.WhenAll(tasks);

        // Assert
        Assert.All(sessions, session => Assert.NotNull(session));
        Assert.All(sessions, session => Assert.Equal(agentId, session!.AgentId));
    }

    [Fact]
    public async Task ConcurrentDisconnect_MultipleCalls_OnlyOneSucceeds()
    {
        // Arrange
        var agentId = "disconnect-concurrent-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/disconnect-concurrent.sock" };
        await _sessionManager.CreateSessionAsync(agentId, connectionParams);

        // Act
        var tasks = new List<Task<bool>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_sessionManager.DisconnectSessionAsync(agentId));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, results.Count(r => r == true));
        Assert.Equal(4, results.Count(r => r == false));
    }

    #endregion

    #region Event Handling Tests

    [Fact]
    public async Task SessionError_ConnectorError_RaisesEvent()
    {
        // Arrange
        var agentId = "error-agent";
        var connectionParams = new AgentConnectionParams { ConnectorType = "terminal", SocketPath = "/tmp/error.sock" };

        SessionErrorEventArgs? errorEventArgs = null;
        _sessionManager.SessionError += (sender, args) => errorEventArgs = args;

        // Use IOException (not InvalidOperationException) so it gets caught and SessionError event fires
        var testException = new System.IO.IOException("Test connector error");
        _mockConnector
            .Setup(c => c.ConnectAsync(agentId, connectionParams, It.IsAny<CancellationToken>()))
            .ThrowsAsync(testException);

        // Act & Assert
        await Assert.ThrowsAsync<System.IO.IOException>(
            async () => await _sessionManager.CreateSessionAsync(agentId, connectionParams));

        Assert.NotNull(errorEventArgs);
        Assert.Equal(agentId, errorEventArgs.AgentId);
        Assert.Equal(testException, errorEventArgs.Error);
    }

    [Fact]
    public async Task Dispose_ClosesAllSessions()
    {
        // Arrange
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/dispose.sock" };
        await _sessionManager.CreateSessionAsync("agent1", connectionParams);
        await _sessionManager.CreateSessionAsync("agent2", connectionParams);

        // Act
        _sessionManager.Dispose();

        // Assert
        _mockConnector.Verify(
            c => c.DisconnectAsync(It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/dispose-twice.sock" };
        await _sessionManager.CreateSessionAsync("agent", connectionParams);

        // Act
        _sessionManager.Dispose();
        _sessionManager.Dispose();

        // Assert - no exception thrown
    }

    [Fact]
    public async Task AfterDispose_CreateSessionAsync_ThrowsObjectDisposedException()
    {
        // Arrange
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/after-dispose.sock" };
        _sessionManager.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await _sessionManager.CreateSessionAsync("agent", connectionParams));
    }

    [Fact]
    public void AfterDispose_GetSessionAsync_ThrowsObjectDisposedException()
    {
        // Arrange
        _sessionManager.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _sessionManager.GetSessionAsync("agent"));
    }

    [Fact]
    public async Task AfterDispose_DisconnectSessionAsync_ThrowsObjectDisposedException()
    {
        // Arrange
        _sessionManager.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await _sessionManager.DisconnectSessionAsync("agent"));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullLifecycle_CreateGetDisconnect_WorksCorrectly()
    {
        // Arrange
        var agentId = "lifecycle-agent";
        var connectionParams = new AgentConnectionParams { SocketPath = "/tmp/lifecycle.sock" };

        // Act & Assert - Create
        var session = await _sessionManager.CreateSessionAsync(agentId, connectionParams);
        Assert.NotNull(session);

        // Act & Assert - Get
        var retrievedSession = _sessionManager.GetSessionAsync(agentId);
        Assert.NotNull(retrievedSession);
        Assert.Equal(agentId, retrievedSession.AgentId);

        // Act & Assert - Disconnect
        var disconnectResult = await _sessionManager.DisconnectSessionAsync(agentId);
        Assert.True(disconnectResult);

        // Act & Assert - Get after disconnect
        var afterDisconnect = _sessionManager.GetSessionAsync(agentId);
        Assert.Null(afterDisconnect);
    }

    #endregion
}
