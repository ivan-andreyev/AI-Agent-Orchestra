using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchestra.Core.Services;
using Orchestra.Tests.Integration.Mocks;
using System.Collections.Generic;
using Hangfire;
using Orchestra.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Orchestra.Tests;

/// <summary>
/// Custom WebApplicationFactory that provides test isolation by using unique databases
/// for each test run. This prevents test interference through shared SQLite storage.
/// Uses Hangfire InMemoryStorage for complete test isolation without database conflicts.
/// </summary>
/// <typeparam name="TStartup">The startup class type</typeparam>
public class TestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
    where TStartup : class
{
    private readonly string _testInstanceId;
    private readonly string _efCoreDbName;

    public TestWebApplicationFactory()
    {
        // Create unique database name for this test factory instance
        // Use Guid, DateTime ticks, and process ID to ensure uniqueness even across rapid parallel test execution
        // Note: Hangfire uses InMemoryStorage - no database file needed
        var processId = Environment.ProcessId;
        var threadId = Environment.CurrentManagedThreadId;
        _testInstanceId = $"{Guid.NewGuid().ToString("N")}_{DateTime.Now.Ticks}_{processId}_{threadId}";
        _efCoreDbName = $"test-orchestra-efcore-{_testInstanceId}.db";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override database connection strings with test-specific databases
            // Set HANGFIRE_CONNECTION = "InMemory" to trigger Hangfire InMemoryStorage
            // in Startup.cs (line 106-113) which provides complete test isolation without database conflicts
            var testConfig = new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Testing",  // Force testing environment
                ["HANGFIRE_CONNECTION"] = "InMemory",    // Use InMemoryStorage for Hangfire
                ["EFCORE_CONNECTION"] = _efCoreDbName
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove specific background services that can interfere with tests
            // BUT keep Hangfire server which is needed for task execution
            var hostedServicesToRemove = services.Where(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType != null &&
                !d.ImplementationType.Name.Contains("HangfireServer") &&
                (d.ImplementationType.Name.Contains("AgentScheduler") ||
                 d.ImplementationType.Name.Contains("BackgroundTaskAssignmentService") ||
                 d.ImplementationType.Name.Contains("AgentHealthCheckService") ||
                 d.ImplementationType.Name.Contains("AgentDiscoveryService"))).ToList();

            foreach (var serviceDescriptor in hostedServicesToRemove)
            {
                services.Remove(serviceDescriptor);
            }
            // Replace real IAgentExecutor with MockAgentExecutor for tests
            var agentExecutorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAgentExecutor));
            if (agentExecutorDescriptor != null)
            {
                services.Remove(agentExecutorDescriptor);
            }

            // Replace IAgentStateStore with TestAgentStateStore for test isolation
            var agentStateStoreDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Orchestra.Core.Abstractions.IAgentStateStore));
            if (agentStateStoreDescriptor != null)
            {
                services.Remove(agentStateStoreDescriptor);
            }

            // Register mock services as scoped for better test isolation
            // Using scoped services helps prevent state leakage between tests
            services.AddScoped<MockAgentExecutor>();
            services.AddScoped<IAgentExecutor>(provider => provider.GetRequiredService<MockAgentExecutor>());
            services.AddScoped<TestAgentRegistry>(provider =>
                new TestAgentRegistry(provider.GetRequiredService<MockAgentExecutor>()));

            // Register TestAgentStateStore that integrates with MockAgentExecutor
            services.AddScoped<Orchestra.Core.Abstractions.IAgentStateStore, TestAgentStateStore>(provider =>
                new TestAgentStateStore(provider.GetRequiredService<MockAgentExecutor>()));

            // Replace HangfireOrchestrator with test configuration (disable auto-agent creation)
            var hangfireOrchestratorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Orchestra.API.Services.HangfireOrchestrator));
            if (hangfireOrchestratorDescriptor != null)
            {
                services.Remove(hangfireOrchestratorDescriptor);
            }

            services.AddScoped<Orchestra.API.Services.HangfireOrchestrator>(provider =>
            {
                var jobClient = provider.GetRequiredService<Hangfire.IBackgroundJobClient>();
                var entityOrchestrator = provider.GetRequiredService<Orchestra.API.Services.EntityFrameworkOrchestrator>();
                var legacyOrchestrator = provider.GetRequiredService<Orchestra.Core.SimpleOrchestrator>();
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Orchestra.API.Services.HangfireOrchestrator>>();
                var taskRepository = provider.GetRequiredService<Orchestra.API.Services.TaskRepository>();
                var agentRepository = provider.GetRequiredService<Orchestra.API.Services.AgentRepository>();

                // Disable auto-agent creation for tests to allow proper failure testing
                return new Orchestra.API.Services.HangfireOrchestrator(jobClient, entityOrchestrator, legacyOrchestrator, logger, taskRepository, agentRepository, autoCreateAgents: false);
            });
        });

        builder.UseEnvironment("Testing");
    }

    /// <summary>
    /// Resets test state to ensure clean environment between tests
    /// Clears Hangfire InMemory storage and database state
    /// </summary>
    public async Task ResetTestStateAsync()
    {
        try
        {
            using var scope = Services.CreateScope();

            // Reset Hangfire InMemory storage state
            ResetHangfireStorage();

            // Reset database state
            await ResetDatabaseStateAsync(scope);

            // Reset mock agent state
            ResetMockAgentState(scope);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the test - this is cleanup
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to reset test state: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets Hangfire InMemory storage to clear all jobs and state
    /// </summary>
    private void ResetHangfireStorage()
    {
        try
        {
            var monitoringApi = JobStorage.Current.GetMonitoringApi();

            // Clear all job queues and state
            var enqueuedJobs = monitoringApi.EnqueuedJobs("default", 0, 1000);
            var processingJobs = monitoringApi.ProcessingJobs(0, 1000);
            var succeededJobs = monitoringApi.SucceededJobs(0, 1000);
            var failedJobs = monitoringApi.FailedJobs(0, 1000);

            // Note: InMemory storage doesn't provide direct job deletion methods
            // State will be cleared when storage is recreated
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Could not reset Hangfire storage: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets database state by clearing all tables
    /// </summary>
    private async Task ResetDatabaseStateAsync(IServiceScope scope)
    {
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();

            // Clear all data in proper order to respect foreign key constraints
            await dbContext.Tasks.ExecuteDeleteAsync();
            await dbContext.Agents.ExecuteDeleteAsync();
            await dbContext.ChatMessages.ExecuteDeleteAsync();
            await dbContext.ChatSessions.ExecuteDeleteAsync();
            await dbContext.OrchestrationLogs.ExecuteDeleteAsync();
            await dbContext.PerformanceMetrics.ExecuteDeleteAsync();
            await dbContext.UserPreferences.ExecuteDeleteAsync();

            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Could not reset database state: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets mock agent state to clear all registered agents and behaviors
    /// </summary>
    private void ResetMockAgentState(IServiceScope scope)
    {
        try
        {
            // Reset SimpleOrchestrator state
            var simpleOrchestrator = scope.ServiceProvider.GetRequiredService<Orchestra.Core.SimpleOrchestrator>();
            simpleOrchestrator.ClearAllAgents();

            // Reset mock agent registry
            var testAgentRegistry = scope.ServiceProvider.GetRequiredService<TestAgentRegistry>();
            testAgentRegistry.ResetAllAgents();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Could not reset mock agent state: {ex.Message}");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                // Step 1: Stop Hangfire server gracefully to prevent zombie background jobs
                try
                {
                    var server = Services.GetService<Hangfire.BackgroundJobServer>();
                    if (server != null)
                    {
                        server.SendStop();
                        server.Dispose();
                    }
                }
                catch
                {
                    // Ignore - server may already be stopped
                }

                // Step 2: Wait briefly for background jobs to complete
                System.Threading.Thread.Sleep(500);

                // Step 3: Close all SQLite connections to release file locks
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                // Step 4: Delete test database file with retries (file might be locked briefly)
                if (File.Exists(_efCoreDbName))
                {
                    for (int i = 0; i < 3; i++)  // 3 retry attempts
                    {
                        try
                        {
                            File.Delete(_efCoreDbName);
                            break;  // Success - exit loop
                        }
                        catch
                        {
                            if (i < 2)  // Not last attempt
                            {
                                System.Threading.Thread.Sleep(100);  // Wait before retry
                            }
                            // Ignore errors on last attempt - file will be cleaned up eventually
                        }
                    }
                }
            }
            catch
            {
                // Ignore all cleanup errors - disposal should never fail
                // Files will be cleaned up by OS or next test run
            }
        }

        base.Dispose(disposing);
    }
}
