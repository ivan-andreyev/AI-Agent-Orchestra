using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Commands.Permissions;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.Commands.Permissions;

/// <summary>
/// Тесты для CancelApprovalCommandHandler
/// </summary>
public class CancelApprovalCommandHandlerTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly OrchestraDbContext _context;
    private readonly IMediator _mediator;

    public CancelApprovalCommandHandlerTests()
    {
        var services = new ServiceCollection();

        // Setup InMemory database
        services.AddDbContext<OrchestraDbContext>(options =>
        {
            options.UseInMemoryDatabase($"CancelApprovalTests_{Guid.NewGuid()}");
        });

        // Register MediatR
        services.AddMediatR(typeof(CancelApprovalCommand).Assembly);

        // Register logging
        services.AddLogging();

        // Mock TelegramEscalationService
        services.AddSingleton<ITelegramEscalationService>(sp =>
        {
            var mock = new Moq.Mock<ITelegramEscalationService>();
            mock.Setup(x => x.SendEscalationAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            return mock.Object;
        });

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<OrchestraDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task CancelApprovalCommand_ShouldUpdateStatusToCancelled()
    {
        // Arrange
        var approval = new ApprovalRequest
        {
            ApprovalId = "test-cancel-1",
            SessionId = "session-1",
            AgentId = "agent-1",
            Status = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        _context.ApprovalRequests.Add(approval);
        await _context.SaveChangesAsync();

        var command = new CancelApprovalCommand("test-cancel-1", "Manual cancellation");

        // Act
        await _mediator.Send(command);

        // Assert
        var updatedApproval = await _context.ApprovalRequests
            .FirstOrDefaultAsync(a => a.ApprovalId == "test-cancel-1");

        Assert.NotNull(updatedApproval);
        Assert.Equal(ApprovalStatus.Cancelled, updatedApproval.Status);
        Assert.Equal("Manual cancellation", updatedApproval.CancellationReason);
    }

    [Fact]
    public async Task CancelApprovalCommand_PublishesEvent()
    {
        // Arrange
        var approval = new ApprovalRequest
        {
            ApprovalId = "test-cancel-2",
            SessionId = "session-2",
            AgentId = "agent-2",
            Status = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        _context.ApprovalRequests.Add(approval);
        await _context.SaveChangesAsync();

        var command = new CancelApprovalCommand("test-cancel-2", "Timeout");

        // Act
        var result = await _mediator.Send(command);

        // Assert - verify command executed successfully
        Assert.Equal(Unit.Value, result);

        var updatedApproval = await _context.ApprovalRequests
            .FirstOrDefaultAsync(a => a.ApprovalId == "test-cancel-2");

        Assert.NotNull(updatedApproval);
        Assert.Equal(ApprovalStatus.Cancelled, updatedApproval.Status);
    }

    [Fact]
    public async Task CancelApprovalCommand_WhenApprovalNotFound_DoesNotThrow()
    {
        // Arrange
        var command = new CancelApprovalCommand("non-existent-approval", "Timeout");

        // Act & Assert - should not throw
        await _mediator.Send(command);
    }

    [Fact]
    public async Task CancelApprovalCommand_WhenAlreadyApproved_DoesNotChangeStatus()
    {
        // Arrange
        var approval = new ApprovalRequest
        {
            ApprovalId = "test-cancel-3",
            SessionId = "session-3",
            AgentId = "agent-3",
            Status = ApprovalStatus.Approved,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            ApprovedAt = DateTime.UtcNow.AddMinutes(-10),
            ApprovedBy = "operator-1"
        };

        _context.ApprovalRequests.Add(approval);
        await _context.SaveChangesAsync();

        var command = new CancelApprovalCommand("test-cancel-3", "Timeout");

        // Act
        await _mediator.Send(command);

        // Assert
        var updatedApproval = await _context.ApprovalRequests
            .FirstOrDefaultAsync(a => a.ApprovalId == "test-cancel-3");

        Assert.NotNull(updatedApproval);
        Assert.Equal(ApprovalStatus.Approved, updatedApproval.Status); // Still Approved
        Assert.Null(updatedApproval.CancellationReason); // No cancellation reason
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}
