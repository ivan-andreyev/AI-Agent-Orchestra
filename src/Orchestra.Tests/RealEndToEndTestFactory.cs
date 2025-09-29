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
    private readonly string _hangfireDbName;
    private readonly string _efCoreDbName;

    public RealEndToEndTestFactory()
    {
        // Create unique database names for this test factory instance
        var processId = Environment.ProcessId;
        var threadId = Environment.CurrentManagedThreadId;
        _testInstanceId = $"{Guid.NewGuid().ToString("N")}_{DateTime.Now.Ticks}_{processId}_{threadId}";
        _hangfireDbName = $"test-orchestra-hangfire-real-e2e-{_testInstanceId}.db";
        _efCoreDbName = $"test-orchestra-efcore-real-e2e-{_testInstanceId}.db";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override database connection strings with test-specific databases
            var testConfig = new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                ["HANGFIRE_CONNECTION"] = "InMemory",
                ["EFCORE_CONNECTION"] = _efCoreDbName,
                // Configure Claude Code CLI for real execution
                ["ClaudeCodeConfiguration:DefaultTimeout"] = "00:10:00", // 10 minutes
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
            // Clean up test database files
            try
            {
                if (File.Exists(_hangfireDbName))
                {
                    File.Delete(_hangfireDbName);
                }

                if (File.Exists(_efCoreDbName))
                {
                    File.Delete(_efCoreDbName);
                }
            }
            catch
            {
                // Ignore cleanup errors - database files might be locked during disposal
            }
        }

        base.Dispose(disposing);
    }
}