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
/// Unit tests для CreateAgentSessionCommandHandler
/// </summary>
public class CreateAgentSessionCommandHandlerTests : IDisposable
{
    private readonly OrchestraDbContext _context;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<CreateAgentSessionCommandHandler>> _mockLogger;
    private readonly CreateAgentSessionCommandHandler _handler;

    public CreateAgentSessionCommandHandlerTests()
    {
        // Создаем InMemory database для изоляции тестов
        var options = new DbContextOptionsBuilder<OrchestraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new OrchestraDbContext(options);
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<CreateAgentSessionCommandHandler>>();

        _handler = new CreateAgentSessionCommandHandler(_context, _mockMediator.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    [Fact]
    public async Task Handle_CreatesSession_WhenAgentExists()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "test-agent-id",
            Name = "Test Agent",
            Type = "ClaudeCode",
            Status = AgentStatus.Idle,
            CreatedAt = DateTime.UtcNow
        };
        _context.Agents.Add(agent);
        await _context.SaveChangesAsync();

        var command = new CreateAgentSessionCommand(
            AgentId: "test-agent-id",
            SessionId: "test-session-uuid",
            WorkingDirectory: "C:\\test\\directory",
            ProcessId: 12345
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Id);
        Assert.Equal("test-agent-id", result.AgentId);
        Assert.Equal("test-session-uuid", result.SessionId);
        Assert.Equal("C:\\test\\directory", result.WorkingDirectory);
        Assert.Equal(12345, result.ProcessId);
        Assert.Equal(SessionStatus.Active, result.Status);
        Assert.Equal(0, result.TotalCostUsd);
        Assert.Equal(0, result.TotalDurationMs);
        Assert.Equal(0, result.MessageCount);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
        Assert.Null(result.ClosedAt);
    }

    [Fact]
    public async Task Handle_ThrowsException_WhenAgentNotFound()
    {
        // Arrange
        var command = new CreateAgentSessionCommand(
            AgentId: "non-existent-agent-id",
            SessionId: "test-session-uuid",
            WorkingDirectory: "C:\\test\\directory"
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None)
        );

        Assert.Contains("Agent non-existent-agent-id not found", exception.Message);
    }

    [Fact]
    public async Task Handle_SavesSessionToDatabase()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "agent-for-db-test",
            Name = "DB Test Agent",
            Type = "ClaudeCode",
            Status = AgentStatus.Idle,
            CreatedAt = DateTime.UtcNow
        };
        _context.Agents.Add(agent);
        await _context.SaveChangesAsync();

        var command = new CreateAgentSessionCommand(
            AgentId: "agent-for-db-test",
            SessionId: "session-db-uuid",
            WorkingDirectory: "C:\\db\\test"
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - проверяем что сессия сохранена в БД
        var savedSession = await _context.AgentSessions
            .FirstOrDefaultAsync(s => s.SessionId == "session-db-uuid");

        Assert.NotNull(savedSession);
        Assert.Equal("agent-for-db-test", savedSession.AgentId);
        Assert.Equal("session-db-uuid", savedSession.SessionId);
        Assert.Equal("C:\\db\\test", savedSession.WorkingDirectory);
        Assert.Equal(SessionStatus.Active, savedSession.Status);
    }

    [Fact]
    public async Task Handle_PublishesAgentSessionCreatedEvent()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "event-test-agent",
            Name = "Event Test Agent",
            Type = "ClaudeCode",
            Status = AgentStatus.Idle,
            CreatedAt = DateTime.UtcNow
        };
        _context.Agents.Add(agent);
        await _context.SaveChangesAsync();

        var command = new CreateAgentSessionCommand(
            AgentId: "event-test-agent",
            SessionId: "event-session-uuid",
            WorkingDirectory: "C:\\event\\test"
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - проверяем что событие опубликовано
        _mockMediator.Verify(
            m => m.Publish(
                It.Is<AgentSessionCreatedEvent>(e => e.Session.SessionId == "event-session-uuid"),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }
}
