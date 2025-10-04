using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchestra.Agents.ClaudeCode;
using Orchestra.Core.Services;
using Hangfire;
using Orchestra.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Orchestra.Tests;

/// <summary>
/// WebApplicationFactory for REAL end-to-end tests with actual Claude Code CLI execution.
/// Unlike TestWebApplicationFactory, this uses the real ClaudeCodeExecutor instead of mocks.
/// WARNING: These tests will execute real Claude Code commands and may take several minutes.
/// </summary>
/// <typeparam name="TStartup">The startup class type</typeparam>
public class RealEndToEndTestFactory<TStartup> : WebApplicationFactory<TStartup>
    where TStartup : class
{
    private readonly string _testInstanceId;
    private readonly string _efCoreDbName;

    public RealEndToEndTestFactory()
    {
        // Create unique database name for this test factory instance
        // Note: Hangfire uses InMemoryStorage - no database file needed
        var processId = Environment.ProcessId;
        var threadId = Environment.CurrentManagedThreadId;
        _testInstanceId = $"{Guid.NewGuid().ToString("N")}_{DateTime.Now.Ticks}_{processId}_{threadId}";
        _efCoreDbName = $"test-orchestra-efcore-real-e2e-{_testInstanceId}.db";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override database connection strings with test-specific databases
            // Set HANGFIRE_CONNECTION = "InMemory" to trigger Hangfire InMemoryStorage
            // in Startup.cs which provides complete test isolation without database conflicts
            var testConfig = new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                ["HANGFIRE_CONNECTION"] = "InMemory",    // Use InMemoryStorage for Hangfire
                ["EFCORE_CONNECTION"] = _efCoreDbName,
                // Configure Claude Code CLI for real execution
                // First Claude request can take 10+ minutes due to model loading
                ["ClaudeCodeConfiguration:DefaultTimeout"] = "00:15:00", // 15 minutes
                ["ClaudeCodeConfiguration:EnableVerboseLogging"] = "true",
                ["ClaudeCodeConfiguration:MaxConcurrentExecutions"] = "1" // Sequential for tests
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove background services that can interfere with tests
            // Keep Hangfire server for task execution
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

            // IMPORTANT: Replace old ClaudeAgentExecutor with new ClaudeCodeExecutor
            // Remove the old executor
            var oldExecutorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAgentExecutor));
            if (oldExecutorDescriptor != null)
            {
                services.Remove(oldExecutorDescriptor);
            }

            // Register the new ClaudeCodeExecutor with proper configuration
            services.Configure<ClaudeCodeConfiguration>(config =>
            {
                config.DefaultTimeout = TimeSpan.FromMinutes(10);
                config.EnableVerboseLogging = true;
                config.MaxConcurrentExecutions = 1; // Sequential execution for predictable tests
            });

            // Register ClaudeCodeExecutor as the IAgentExecutor
            services.AddSingleton<IAgentExecutor, ClaudeCodeExecutor>();
        });
    }

    public void ResetMockAgentState()
    {
        // No-op for real E2E tests - no mocks to reset
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