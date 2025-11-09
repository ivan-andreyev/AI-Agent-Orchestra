using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Commands.Permissions;
using Orchestra.Core.Commands.Sessions;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Queries.Sessions;
using Xunit;

namespace Orchestra.Tests.Commands.Permissions;

/// <summary>
/// Тесты для ProcessHumanApprovalCommandHandler с проверками timeout
/// </summary>
public class ProcessHumanApprovalCommandHandler_TimeoutTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly OrchestraDbContext _context;
    private readonly IMediator _mediator;

    public ProcessHumanApprovalCommandHandler_TimeoutTests()
    {
        var services = new ServiceCollection();

        // Setup InMemory database
        services.AddDbContext<OrchestraDbContext>(options =>
        {
            options.UseInMemoryDatabase($"ProcessApprovalTimeoutTests_{Guid.NewGuid()}");
        });

        // Register MediatR
        services.AddMediatR(typeof(ProcessHumanApprovalCommand).Assembly);

        // Register logging
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<OrchestraDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task ProcessApproval_WhenExpired_ShouldReturnFailure()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "agent-timeout-1",
            Name = "Test Agent",
            Type = "ClaudeCode",
            RepositoryPath = "/test",
            Status = AgentStatus.Idle
        };

        var session = new AgentSession
        {
            Id = Guid.NewGuid().ToString(),
            AgentId = "agent-timeout-1",
            SessionId = "session-timeout-1",
            WorkingDirectory = "/test",
            Status = SessionStatus.Paused,
            CreatedAt = DateTime.UtcNow.AddMinutes(-35),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-35)
        };

        var approval = new ApprovalRequest
        {
            ApprovalId = "approval-timeout-1",
            SessionId = "session-timeout-1",
            AgentId = "agent-timeout-1",
            Status = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-35),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-35),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5) // Expired 5 minutes ago
        };

        _context.Agents.Add(agent);
        _context.AgentSessions.Add(session);
        _context.ApprovalRequests.Add(approval);
        await _context.SaveChangesAsync();

        var command = new ProcessHumanApprovalCommand(
            "approval-timeout-1",
            "session-timeout-1",
            "agent-timeout-1",
            true,
            "operator-1",
            DateTime.UtcNow,
            null);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("истёк", result.Message);
    }

    [Fact]
    public async Task ProcessApproval_WhenCancelled_ShouldReturnFailure()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "agent-cancelled-1",
            Name = "Test Agent",
            Type = "ClaudeCode",
            RepositoryPath = "/test",
            Status = AgentStatus.Idle
        };

        var session = new AgentSession
        {
            Id = Guid.NewGuid().ToString(),
            AgentId = "agent-cancelled-1",
            SessionId = "session-cancelled-1",
            WorkingDirectory = "/test",
            Status = SessionStatus.Paused,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var approval = new ApprovalRequest
        {
            ApprovalId = "approval-cancelled-1",
            SessionId = "session-cancelled-1",
            AgentId = "agent-cancelled-1",
            Status = ApprovalStatus.Cancelled,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(20),
            CancellationReason = "Timeout"
        };

        _context.Agents.Add(agent);
        _context.AgentSessions.Add(session);
        _context.ApprovalRequests.Add(approval);
        await _context.SaveChangesAsync();

        var command = new ProcessHumanApprovalCommand(
            "approval-cancelled-1",
            "session-cancelled-1",
            "agent-cancelled-1",
            true,
            "operator-1",
            DateTime.UtcNow,
            null);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("отменён", result.Message);
        Assert.Contains("Timeout", result.Message);
    }

    [Fact]
    public async Task ProcessApproval_WhenValid_ShouldUpdateApprovalStatus()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "agent-valid-1",
            Name = "Test Agent",
            Type = "ClaudeCode",
            RepositoryPath = "/test",
            Status = AgentStatus.Idle
        };

        var session = new AgentSession
        {
            Id = Guid.NewGuid().ToString(),
            AgentId = "agent-valid-1",
            SessionId = "session-valid-1",
            WorkingDirectory = "/test",
            Status = SessionStatus.Paused,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var approval = new ApprovalRequest
        {
            ApprovalId = "approval-valid-1",
            SessionId = "session-valid-1",
            AgentId = "agent-valid-1",
            Status = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(25) // Still valid
        };

        _context.Agents.Add(agent);
        _context.AgentSessions.Add(session);
        _context.ApprovalRequests.Add(approval);
        await _context.SaveChangesAsync();

        var command = new ProcessHumanApprovalCommand(
            "approval-valid-1",
            "session-valid-1",
            "agent-valid-1",
            true,
            "operator-1",
            DateTime.UtcNow,
            null);

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.SessionResumed);

        var updatedApproval = await _context.ApprovalRequests
            .FirstOrDefaultAsync(a => a.ApprovalId == "approval-valid-1");

        Assert.NotNull(updatedApproval);
        Assert.Equal(ApprovalStatus.Approved, updatedApproval.Status);
        Assert.NotNull(updatedApproval.ApprovedAt);
        Assert.Equal("operator-1", updatedApproval.ApprovedBy);
    }

    [Fact]
    public async Task ProcessApproval_WhenRejected_ShouldUpdateStatusToRejected()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "agent-rejected-1",
            Name = "Test Agent",
            Type = "ClaudeCode",
            RepositoryPath = "/test",
            Status = AgentStatus.Idle
        };

        var session = new AgentSession
        {
            Id = Guid.NewGuid().ToString(),
            AgentId = "agent-rejected-1",
            SessionId = "session-rejected-1",
            WorkingDirectory = "/test",
            Status = SessionStatus.Paused,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var approval = new ApprovalRequest
        {
            ApprovalId = "approval-rejected-1",
            SessionId = "session-rejected-1",
            AgentId = "agent-rejected-1",
            Status = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(25)
        };

        _context.Agents.Add(agent);
        _context.AgentSessions.Add(session);
        _context.ApprovalRequests.Add(approval);
        await _context.SaveChangesAsync();

        var command = new ProcessHumanApprovalCommand(
            "approval-rejected-1",
            "session-rejected-1",
            "agent-rejected-1",
            false, // Rejected
            "operator-1",
            DateTime.UtcNow,
            "Security concerns");

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.SessionResumed);

        var updatedApproval = await _context.ApprovalRequests
            .FirstOrDefaultAsync(a => a.ApprovalId == "approval-rejected-1");

        Assert.NotNull(updatedApproval);
        Assert.Equal(ApprovalStatus.Rejected, updatedApproval.Status);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}
