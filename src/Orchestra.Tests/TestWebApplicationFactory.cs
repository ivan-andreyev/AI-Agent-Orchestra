using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace Orchestra.Tests;

/// <summary>
/// Custom WebApplicationFactory that provides test isolation by using unique databases
/// for each test run. This prevents test interference through shared SQLite storage.
/// Separates EF Core and Hangfire databases to avoid SQLiteStorage disposal conflicts.
/// </summary>
/// <typeparam name="TStartup">The startup class type</typeparam>
public class TestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
    where TStartup : class
{
    private readonly string _testInstanceId;
    private readonly string _hangfireDbName;
    private readonly string _efCoreDbName;

    public TestWebApplicationFactory()
    {
        // Create unique database names for this test factory instance
        _testInstanceId = Guid.NewGuid().ToString("N");
        _hangfireDbName = $"test-orchestra-hangfire-{_testInstanceId}.db";
        _efCoreDbName = $"test-orchestra-efcore-{_testInstanceId}.db";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override database connection strings with test-specific databases
            // This separates EF Core and Hangfire databases to prevent disposal conflicts
            var testConfig = new Dictionary<string, string?>
            {
                ["HANGFIRE_CONNECTION"] = _hangfireDbName,
                ["EFCORE_CONNECTION"] = _efCoreDbName
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up both test database files
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
                // This is expected in some test scenarios and doesn't affect test isolation
            }
        }

        base.Dispose(disposing);
    }
}
