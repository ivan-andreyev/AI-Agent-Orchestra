using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Queries.Sessions;
using Xunit;

namespace Orchestra.Tests.Queries.Sessions;

/// <summary>
/// Unit tests для GetAgentSessionQueryHandler
/// </summary>
public class GetAgentSessionQueryHandlerTests : IDisposable
{
    private readonly OrchestraDbContext _context;
    private readonly Mock<ILogger<GetAgentSessionQueryHandler>> _mockLogger;
    private readonly GetAgentSessionQueryHandler _handler;

    public GetAgentSessionQueryHandlerTests()
    {
        // Создаем InMemory database для изоляции тестов
        var options = new DbContextOptionsBuilder<OrchestraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new OrchestraDbContext(options);
        _mockLogger = new Mock<ILogger<GetAgentSessionQueryHandler>>();

        _handler = new GetAgentSessionQueryHandler(_context, _mockLogger.Object);
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
    public async Task Handle_ReturnsSession_WhenFound()
    {
        // Arrange
        var createdSession = await CreateTestSessionAsync("found-session-uuid");
        var query = new GetAgentSessionQuery("found-session-uuid");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("found-session-uuid", result.SessionId);
        Assert.Equal(createdSession.AgentId, result.AgentId);
        Assert.Equal(SessionStatus.Active, result.Status);
        Assert.Equal(1000, result.ProcessId);
        Assert.Equal("C:\\test", result.WorkingDirectory);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var query = new GetAgentSessionQuery("non-existent-session-uuid");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_LogsWarning_WhenNotFound()
    {
        // Arrange
        var query = new GetAgentSessionQuery("missing-session-uuid");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);

        // Проверяем что был залогирован Debug уровень при поиске
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieving session")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );

        // Проверяем что был залогирован Debug уровень при не найденной сессии
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }
}
