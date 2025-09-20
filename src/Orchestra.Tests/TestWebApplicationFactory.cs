using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace Orchestra.Tests;

/// <summary>
/// Custom WebApplicationFactory that provides test isolation by using unique Hangfire database
/// for each test run. This prevents test interference through shared SQLite storage.
/// </summary>
/// <typeparam name="TStartup">The startup class type</typeparam>
public class TestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
    where TStartup : class
{
    private readonly string _testDatabaseName;

    public TestWebApplicationFactory()
    {
        // Create unique database name for this test factory instance
        _testDatabaseName = $"test-orchestra-{Guid.NewGuid():N}.db";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override Hangfire connection string with test-specific database
            var testConfig = new Dictionary<string, string?>
            {
                ["HANGFIRE_CONNECTION"] = _testDatabaseName
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up test database file
            try
            {
                if (File.Exists(_testDatabaseName))
                {
                    File.Delete(_testDatabaseName);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        base.Dispose(disposing);
    }
}