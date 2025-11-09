using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Commands.Sessions;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Events.Sessions;
using Xunit;

namespace Orchestra.Tests.Commands.Sessions;

/// <summary>
/// Unit tests для UpdateSessionStatusCommandHandler
/// </summary>
public class UpdateSessionStatusCommandHandlerTests : IDisposable
{
    private readonly OrchestraDbContext _context;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<UpdateSessionStatusCommandHandler>> _mockLogger;
    private readonly UpdateSessionStatusCommandHandler _handler;

    public UpdateSessionStatusCommandHandlerTests()
    {
        // Создаем InMemory database для изоляции тестов
        var options = new DbContextOptionsBuilder<OrchestraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new OrchestraDbContext(options);
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<UpdateSessionStatusCommandHandler>>();

        _handler = new UpdateSessionStatusCommandHandler(_context, _mockMediator.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    private async Task<AgentSession> CreateTestSessionAsync(string sessionId = "test-session-uuid")
    {
        var agent = new Agent
        {
            Id = $"agent-{sessionId}",
            Name = "Test Agent",
            Type = "ClaudeCode",
            Status = AgentStatus.Idle,
            CreatedAt = DateTime.UtcNow
        };
        _context.Agents.Add(agent);

        var session = new AgentSession
        {
            Id = Guid.NewGuid().ToString(),
            AgentId = agent.Id,
            SessionId = sessionId,
            WorkingDirectory = "C:\\test",
            ProcessId = 1000,
            Status = SessionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TotalCostUsd = 0.1,
            TotalDurationMs = 5000,
            MessageCount = 5
        };
        _context.AgentSessions.Add(session);
        await _context.SaveChangesAsync();

        return session;
    }

    [Fact]
    public async Task Handle_UpdatesStatus_ToPaused()
    {
        // Arrange
        var session = await CreateTestSessionAsync("pause-test-session");
        var command = new UpdateSessionStatusCommand(
            SessionId: "pause-test-session",
            NewStatus: SessionStatus.Paused
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SessionStatus.Paused, result.Status);
        Assert.Null(result.ProcessId); // ProcessId должен быть сброшен при Paused
        Assert.NotNull(result.LastResumedAt);
        Assert.True(result.UpdatedAt >= session.UpdatedAt);
    }

    [Fact]
    public async Task Handle_UpdatesStatus_ToClosed()
    {
        // Arrange
        var session = await CreateTestSessionAsync("close-test-session");
        var command = new UpdateSessionStatusCommand(
            SessionId: "close-test-session",
            NewStatus: SessionStatus.Closed
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SessionStatus.Closed, result.Status);
        Assert.Null(result.ProcessId); // ProcessId должен быть сброшен при Closed
        Assert.NotNull(result.ClosedAt);
        Assert.True(result.ClosedAt.Value <= DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_UpdatesMetrics_WhenProvided()
    {
        // Arrange
        var session = await CreateTestSessionAsync("metrics-test-session");
        var initialCost = session.TotalCostUsd;
        var initialDuration = session.TotalDurationMs;
        var initialMessageCount = session.MessageCount;

        var command = new UpdateSessionStatusCommand(
            SessionId: "metrics-test-session",
            NewStatus: SessionStatus.Active,
            AddCost: 0.05,
            AddDurationMs: 2000
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(initialCost + 0.05, result.TotalCostUsd);
        Assert.Equal(initialDuration + 2000, result.TotalDurationMs);
        Assert.Equal(initialMessageCount + 1, result.MessageCount); // Счетчик должен увеличиться
    }

    [Fact]
    public async Task Handle_ThrowsException_WhenSessionNotFound()
    {
        // Arrange
        var command = new UpdateSessionStatusCommand(
            SessionId: "non-existent-session",
            NewStatus: SessionStatus.Closed
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None)
        );

        Assert.Contains("Session non-existent-session not found", exception.Message);
    }

    [Fact]
    public async Task Handle_SetsClosed_WhenStatusClosed()
    {
        // Arrange
        var session = await CreateTestSessionAsync("closed-timestamp-test");
        var beforeClosed = DateTime.UtcNow;

        var command = new UpdateSessionStatusCommand(
            SessionId: "closed-timestamp-test",
            NewStatus: SessionStatus.Closed
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result.ClosedAt);
        Assert.True(result.ClosedAt.Value >= beforeClosed);
        Assert.True(result.ClosedAt.Value <= DateTime.UtcNow);
        Assert.Equal(SessionStatus.Closed, result.Status);
    }

    [Fact]
    public async Task Handle_PublishesStatusChangedEvent()
    {
        // Arrange
        var session = await CreateTestSessionAsync("event-test-session");
        var command = new UpdateSessionStatusCommand(
            SessionId: "event-test-session",
            NewStatus: SessionStatus.Paused
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - проверяем что событие опубликовано с правильными параметрами
        _mockMediator.Verify(
            m => m.Publish(
                It.Is<AgentSessionStatusChangedEvent>(e =>
                    e.SessionId == "event-test-session" &&
                    e.PreviousStatus == SessionStatus.Active &&
                    e.NewStatus == SessionStatus.Paused &&
                    e.Session != null
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }
}
