using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Commands.Permissions;
using Orchestra.Core.Data;
using Orchestra.Core.Services;
using Orchestra.Core.Services.Connectors;
using Xunit;

namespace Orchestra.Tests.Commands.Permissions;

/// <summary>
/// Тесты для RequestHumanApprovalCommandHandler
/// </summary>
public class RequestHumanApprovalCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidPermissionDenials_ReturnSuccessWithApprovalId()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestHumanApprovalCommandHandler>>();
        var mockTelegramService = new Mock<ITelegramEscalationService>();
        var mockContext = new Mock<OrchestraDbContext>();

        var handler = new RequestHumanApprovalCommandHandler(
            mockLogger.Object,
            mockTelegramService.Object,
            mockContext.Object);

        var denials = new List<PermissionDenial>
        {
            new() { ToolName = "Bash", ToolUseId = "tool-123", ToolInput = new() { { "command", "ls" } } }
        };

        var command = new RequestHumanApprovalCommand(
            "agent-test",
            "session-uuid",
            denials,
            "Execute ls command",
            DateTime.UtcNow);

        mockTelegramService
            .Setup(x => x.SendEscalationAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ApprovalId);
        Assert.False(result.IsApproved);
        Assert.Contains("agent-test", result.ApprovalId);
    }

    [Fact]
    public async Task Handle_TelegramFails_StillCreatesApprovalId()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestHumanApprovalCommandHandler>>();
        var mockTelegramService = new Mock<ITelegramEscalationService>();
        var mockContext = new Mock<OrchestraDbContext>();

        var handler = new RequestHumanApprovalCommandHandler(
            mockLogger.Object,
            mockTelegramService.Object,
            mockContext.Object);

        var denials = new List<PermissionDenial>
        {
            new() { ToolName = "Bash", ToolUseId = "tool-456" }
        };

        var command = new RequestHumanApprovalCommand(
            "agent-test",
            "session-uuid",
            denials,
            "Execute command",
            DateTime.UtcNow);

        mockTelegramService
            .Setup(x => x.SendEscalationAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ApprovalId);
    }

    [Fact]
    public async Task Handle_WithEmptyPermissionDenials_StillCreatesApprovalId()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestHumanApprovalCommandHandler>>();
        var mockTelegramService = new Mock<ITelegramEscalationService>();
        var mockContext = new Mock<OrchestraDbContext>();

        var handler = new RequestHumanApprovalCommandHandler(
            mockLogger.Object,
            mockTelegramService.Object,
            mockContext.Object);

        var command = new RequestHumanApprovalCommand(
            "agent-test",
            "session-uuid",
            new List<PermissionDenial>(),
            "Execute command",
            DateTime.UtcNow);

        mockTelegramService
            .Setup(x => x.SendEscalationAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ApprovalId);
    }

    [Fact]
    public async Task Handle_TelegramServiceCalled_WithCorrectParameters()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestHumanApprovalCommandHandler>>();
        var mockTelegramService = new Mock<ITelegramEscalationService>();
        var mockContext = new Mock<OrchestraDbContext>();

        var handler = new RequestHumanApprovalCommandHandler(
            mockLogger.Object,
            mockTelegramService.Object,
            mockContext.Object);

        var agentId = "test-agent";
        var denials = new List<PermissionDenial>
        {
            new() { ToolName = "Bash", ToolUseId = "tool-789" }
        };

        var command = new RequestHumanApprovalCommand(
            agentId,
            "session-uuid",
            denials,
            "Test command",
            DateTime.UtcNow);

        mockTelegramService
            .Setup(x => x.SendEscalationAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockTelegramService.Verify(
            x => x.SendEscalationAsync(agentId, It.IsAny<string>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestHumanApprovalCommandHandler>>();
        var mockTelegramService = new Mock<ITelegramEscalationService>();
        var mockContext = new Mock<OrchestraDbContext>();

        var handler = new RequestHumanApprovalCommandHandler(
            mockLogger.Object,
            mockTelegramService.Object,
            mockContext.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, CancellationToken.None));
    }
}
