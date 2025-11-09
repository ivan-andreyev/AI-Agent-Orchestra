using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Orchestra.Core.Commands.Permissions;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Options;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.Services;

/// <summary>
/// Тесты для ApprovalTimeoutService
/// </summary>
public class ApprovalTimeoutServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly OrchestraDbContext _context;
    private readonly ILogger<ApprovalTimeoutService> _logger;
    private readonly IOptions<ApprovalTimeoutOptions> _options;
    private readonly string _databaseName;

    public ApprovalTimeoutServiceTests()
    {
        var services = new ServiceCollection();

        // Use fixed database name so all scopes see the same data
        _databaseName = $"ApprovalTimeoutTests_{Guid.NewGuid()}";

        // Setup InMemory database with fixed name
        services.AddDbContext<OrchestraDbContext>(options =>
        {
            options.UseInMemoryDatabase(_databaseName);
        });

        // Register MediatR
        services.AddMediatR(typeof(CancelApprovalCommand).Assembly);

        // Register options
        services.Configure<ApprovalTimeoutOptions>(opts =>
        {
            opts.DefaultTimeoutMinutes = 30;
            opts.CheckIntervalSeconds = 1; // Fast interval for testing
            opts.GracePeriodSeconds = 0;
            opts.MaxConcurrentTimeouts = 10;
        });

        // Register logging
        services.AddLogging();

        // Register ApprovalTimeoutService
        services.AddSingleton<ApprovalTimeoutService>();

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
        _logger = _serviceProvider.GetRequiredService<ILogger<ApprovalTimeoutService>>();
        _options = _serviceProvider.GetRequiredService<IOptions<ApprovalTimeoutOptions>>();
    }

    [Fact]
    public async Task ExpiredApproval_ShouldBeAutoCancelled()
    {
        // Arrange
        var approvalRequest = new ApprovalRequest
        {
            ApprovalId = "test-approval-1",
            SessionId = "session-1",
            AgentId = "agent-1",
            Status = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-35), // Expired 5 minutes ago
            UpdatedAt = DateTime.UtcNow.AddMinutes(-35),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _context.ApprovalRequests.Add(approvalRequest);
        await _context.SaveChangesAsync();

        // Detach to avoid tracking conflicts
        _context.Entry(approvalRequest).State = EntityState.Detached;

        var service = _serviceProvider.GetRequiredService<ApprovalTimeoutService>();

        // Act
        await service.StartAsync(CancellationToken.None);

        // Wait up to 10 seconds for background service to process expired approval
        // Background service checks immediately on start, then every CheckIntervalSeconds
        var timeout = TimeSpan.FromSeconds(10);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        ApprovalRequest? currentApproval = null;

        while (stopwatch.Elapsed < timeout)
        {
            await Task.Delay(500); // Check every 500ms

            // Use new scope to query current state
            using (var scope = _serviceProvider.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();
                currentApproval = await scopedContext.ApprovalRequests
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.ApprovalId == "test-approval-1");
            }

            if (currentApproval?.Status == ApprovalStatus.Cancelled)
            {
                break; // Success - approval cancelled
            }
        }

        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(currentApproval);
        Assert.Equal(ApprovalStatus.Cancelled, currentApproval.Status);
        Assert.Equal("Timeout", currentApproval.CancellationReason);
    }

    [Fact]
    public async Task ActiveApproval_ShouldNotBeCancelled()
    {
        // Arrange
        var approvalRequest = new ApprovalRequest
        {
            ApprovalId = "test-approval-2",
            SessionId = "session-2",
            AgentId = "agent-2",
            Status = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(25) // Still active
        };

        _context.ApprovalRequests.Add(approvalRequest);
        await _context.SaveChangesAsync();

        var service = _serviceProvider.GetRequiredService<ApprovalTimeoutService>();

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        var updatedApproval = await _context.ApprovalRequests
            .FirstOrDefaultAsync(a => a.ApprovalId == "test-approval-2");

        Assert.NotNull(updatedApproval);
        Assert.Equal(ApprovalStatus.Pending, updatedApproval.Status);
        Assert.Null(updatedApproval.CancellationReason);
    }

    [Fact]
    public async Task ApprovedApproval_ShouldNotBeProcessed()
    {
        // Arrange
        var approvalRequest = new ApprovalRequest
        {
            ApprovalId = "test-approval-3",
            SessionId = "session-3",
            AgentId = "agent-3",
            Status = ApprovalStatus.Approved,
            CreatedAt = DateTime.UtcNow.AddMinutes(-35),
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            ApprovedAt = DateTime.UtcNow.AddMinutes(-10),
            ApprovedBy = "operator-1"
        };

        _context.ApprovalRequests.Add(approvalRequest);
        await _context.SaveChangesAsync();

        var service = _serviceProvider.GetRequiredService<ApprovalTimeoutService>();

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        var updatedApproval = await _context.ApprovalRequests
            .FirstOrDefaultAsync(a => a.ApprovalId == "test-approval-3");

        Assert.NotNull(updatedApproval);
        Assert.Equal(ApprovalStatus.Approved, updatedApproval.Status); // Still Approved
    }

    [Fact]
    public async Task MultipleExpiredApprovals_ShouldAllBeCancelled()
    {
        // Arrange
        for (int i = 1; i <= 3; i++)
        {
            var approval = new ApprovalRequest
            {
                ApprovalId = $"test-approval-multi-{i}",
                SessionId = $"session-multi-{i}",
                AgentId = $"agent-multi-{i}",
                Status = ApprovalStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-35),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-35),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
            };

            _context.ApprovalRequests.Add(approval);
        }
        await _context.SaveChangesAsync();

        // Clear tracking to avoid conflicts
        _context.ChangeTracker.Clear();

        var service = _serviceProvider.GetRequiredService<ApprovalTimeoutService>();

        // Act
        await service.StartAsync(CancellationToken.None);

        // Wait up to 10 seconds for background service to process all expired approvals
        var timeout = TimeSpan.FromSeconds(10);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var finalCancelledCount = 0;

        while (stopwatch.Elapsed < timeout)
        {
            await Task.Delay(500); // Check every 500ms

            // Use new scope to query current state
            using (var scope = _serviceProvider.CreateScope())
            {
                var scopedContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();
                finalCancelledCount = await scopedContext.ApprovalRequests
                    .AsNoTracking()
                    .CountAsync(a => a.ApprovalId.StartsWith("test-approval-multi-") &&
                                    a.Status == ApprovalStatus.Cancelled);
            }

            if (finalCancelledCount == 3)
            {
                break; // Success - all cancelled
            }
        }

        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(3, finalCancelledCount);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}
