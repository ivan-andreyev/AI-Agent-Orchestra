using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Models;

namespace Orchestra.Core.Services;

/// <summary>
/// Background service that automatically assigns unassigned tasks to available agents.
/// Runs continuously to handle cases where tasks are created when no agents are available,
/// and agents become available later.
/// </summary>
public class BackgroundTaskAssignmentService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundTaskAssignmentService> _logger;
    private readonly TimeSpan _assignmentInterval;

    public BackgroundTaskAssignmentService(
        IServiceProvider serviceProvider,
        ILogger<BackgroundTaskAssignmentService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _assignmentInterval = TimeSpan.FromSeconds(30); // Check every 30 seconds
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Task Assignment Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessUnassignedTasks();
                await Task.Delay(_assignmentInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing unassigned tasks");
                // Continue running even if there's an error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Background Task Assignment Service stopped");
    }

    /// <summary>
    /// Processes unassigned tasks and attempts to assign them to available agents.
    /// Uses scoped service provider to ensure proper dependency injection lifecycle.
    /// </summary>
    private async Task ProcessUnassignedTasks()
    {
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<SimpleOrchestrator>();

        try
        {
            var state = orchestrator.GetCurrentState();
            var unassignedTasks = state.TaskQueue.Where(t => string.IsNullOrEmpty(t.AgentId)).ToList();
            var availableAgents = state.Agents.Values.Where(a => a.Status == AgentStatus.Idle).ToList();

            if (unassignedTasks.Any() && availableAgents.Any())
            {
                _logger.LogInformation(
                    "Found {UnassignedTaskCount} unassigned tasks and {AvailableAgentCount} available agents. Triggering assignment.",
                    unassignedTasks.Count,
                    availableAgents.Count);

                // Trigger task assignment using the existing orchestrator logic
                orchestrator.TriggerTaskAssignment();

                // Verify results after assignment
                var newState = orchestrator.GetCurrentState();
                var remainingUnassigned = newState.TaskQueue.Where(t => string.IsNullOrEmpty(t.AgentId)).Count();
                var newlyAssigned = unassignedTasks.Count - remainingUnassigned;

                if (newlyAssigned > 0)
                {
                    _logger.LogInformation(
                        "Successfully assigned {AssignedCount} tasks. {RemainingCount} tasks remain unassigned.",
                        newlyAssigned,
                        remainingUnassigned);
                }
            }
            else if (unassignedTasks.Any())
            {
                _logger.LogDebug(
                    "Found {UnassignedTaskCount} unassigned tasks but no available agents (Idle status).",
                    unassignedTasks.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during task assignment processing");
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Background Task Assignment Service");
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Background Task Assignment Service");
        await base.StopAsync(cancellationToken);
    }
}