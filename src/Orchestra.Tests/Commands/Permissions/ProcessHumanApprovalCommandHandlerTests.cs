using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Commands.Permissions;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Queries.Sessions;
using Orchestra.Core.Services.Metrics;
using System.Diagnostics.Metrics;
using Xunit;

namespace Orchestra.Tests.Commands.Permissions;

/// <summary>
/// Тесты для ProcessHumanApprovalCommandHandler
/// </summary>
public class ProcessHumanApprovalCommandHandlerTests
{
    /// <summary>
    /// Создаёт InMemory базу данных для тестов
    /// </summary>
    private OrchestraDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OrchestraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OrchestraDbContext(options);
    }

    /// <summary>
    /// Создаёт mock EscalationMetricsService для тестов
    /// </summary>
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
    [Fact]
    public async Task Handle_WithValidApprovalAndPausedSession_ReturnSuccess()
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        var mockLogger = new Mock<ILogger<ProcessHumanApprovalCommandHandler>>();
        var context = CreateInMemoryContext();

        var approval = new ApprovalRequest
        {
            ApprovalId = "approval-123",
            SessionId = "uuid-session",
            AgentId = "agent-123",
            Status = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        context.ApprovalRequests.Add(approval);
        await context.SaveChangesAsync();

        var session = new AgentSession
        {
            Id = "session-123",
            AgentId = "agent-123",
            SessionId = "uuid-session",
            Status = SessionStatus.Paused,
            WorkingDirectory = "/test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.AgentSessions.Add(session);
        await context.SaveChangesAsync();

        mockMediator
            .Setup(x => x.Send(It.IsAny<GetAgentSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new ProcessHumanApprovalCommandHandler(
            mockLogger.Object,
            context,
            mockMediator.Object,
            CreateMockMetricsService());

        var command = new ProcessHumanApprovalCommand(
            "approval-123",
            "uuid-session",
            "agent-123",
            true,
            "operator@example.com",
            DateTime.UtcNow);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, $"Expected success=true, got: {result.Message}");
        Assert.True(result.SessionResumed);
        Assert.Equal("uuid-session", result.ResumeSessionId);
    }

    [Fact]
    public async Task Handle_WithApprovalFalse_ReturnSuccessButNotResumed()
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        var mockLogger = new Mock<ILogger<ProcessHumanApprovalCommandHandler>>();
        var context = CreateInMemoryContext();

        var approval = new ApprovalRequest
        {
            ApprovalId = "approval-rejected",
            SessionId = "uuid-session-2",
            AgentId = "agent-456",
            Status = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        context.ApprovalRequests.Add(approval);
        await context.SaveChangesAsync();

        var handler = new ProcessHumanApprovalCommandHandler(
            mockLogger.Object,
            context,
            mockMediator.Object,
            CreateMockMetricsService());

        var command = new ProcessHumanApprovalCommand(
            "approval-rejected",
            "uuid-session-2",
            "agent-456",
            false,
            "operator@example.com",
            DateTime.UtcNow,
            "Approval rejected by operator");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.SessionResumed);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnFailure()
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        var mockLogger = new Mock<ILogger<ProcessHumanApprovalCommandHandler>>();
        var context = CreateInMemoryContext();

        var approval = new ApprovalRequest
        {
            ApprovalId = "approval-notfound",
            SessionId = "nonexistent-session",
            AgentId = "agent-123",
            Status = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        context.ApprovalRequests.Add(approval);
        await context.SaveChangesAsync();

        var handler = new ProcessHumanApprovalCommandHandler(
            mockLogger.Object,
            context,
            mockMediator.Object,
            CreateMockMetricsService());

        mockMediator
            .Setup(x => x.Send(It.IsAny<GetAgentSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentSession)null!);

        var command = new ProcessHumanApprovalCommand(
            "approval-notfound",
            "nonexistent-session",
            "agent-123",
            true,
            "operator@example.com",
            DateTime.UtcNow);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("не найдена", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_SessionNotPaused_ReturnFailure()
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        var mockLogger = new Mock<ILogger<ProcessHumanApprovalCommandHandler>>();
        var context = CreateInMemoryContext();

        var approval = new ApprovalRequest
        {
            ApprovalId = "approval-active",
            SessionId = "uuid-session-3",
            AgentId = "agent-789",
            Status = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        context.ApprovalRequests.Add(approval);
        await context.SaveChangesAsync();

        var session = new AgentSession
        {
            Id = "session-789",
            AgentId = "agent-789",
            SessionId = "uuid-session-3",
            Status = SessionStatus.Active,
            WorkingDirectory = "/test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var handler = new ProcessHumanApprovalCommandHandler(
            mockLogger.Object,
            context,
            mockMediator.Object,
            CreateMockMetricsService());

        mockMediator
            .Setup(x => x.Send(It.IsAny<GetAgentSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new ProcessHumanApprovalCommand(
            "approval-active",
            "uuid-session-3",
            "agent-789",
            true,
            "operator@example.com",
            DateTime.UtcNow);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("не может быть возобновлена", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        var mockLogger = new Mock<ILogger<ProcessHumanApprovalCommandHandler>>();
        var context = CreateInMemoryContext();

        var handler = new ProcessHumanApprovalCommandHandler(
            mockLogger.Object,
            context,
            mockMediator.Object,
            CreateMockMetricsService());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, CancellationToken.None));
    }
}
