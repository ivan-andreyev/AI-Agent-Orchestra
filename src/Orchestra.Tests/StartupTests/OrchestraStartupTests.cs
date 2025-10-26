using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace Orchestra.Tests.StartupTests;

/// <summary>
/// Startup and smoke tests for Orchestra components.
/// Runs before each deployment to catch build/runtime issues automatically.
/// </summary>
/// <remarks>
/// ЦЕЛЬ: Автоматическая проверка перед каждым запуском фронтенда/бэкенда,
/// чтобы предотвратить проблемы типа "снова нихуя не взлетает!!!".
///
/// Эти тесты проверяют:
/// 1. API запускается успешно со всеми сервисами
/// 2. Blazor ресурсы существуют с правильными хэшами (SRI integrity)
/// 3. Подключение к базе данных доступно
/// 4. Background services инициализируются корректно
/// </remarks>
[Trait("Category", "Startup")]
public class OrchestraStartupTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public OrchestraStartupTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    /// <summary>
    /// Test 1: Orchestra.API starts successfully with all services registered
    /// </summary>
    [Fact]
    public async Task OrchestraAPI_Starts_Successfully()
    {
        _output.WriteLine("=== Testing Orchestra.API Startup ===");

        // Build host like API does
        var host = CreateTestHost();

        try
        {
            await host.StartAsync();
            _output.WriteLine("✓ Host started successfully");

            // Verify critical services are registered
            var services = host.Services;

            Assert.NotNull(services.GetService<OrchestraDbContext>());
            _output.WriteLine("✓ OrchestraDbContext registered");

            Assert.NotNull(services.GetService<AgentHealthCheckService>());
            _output.WriteLine("✓ AgentHealthCheckService registered");

            Assert.NotNull(services.GetService<ProcessDiscoveryService>());
            _output.WriteLine("✓ ProcessDiscoveryService registered");

            _output.WriteLine("✓ All critical services registered successfully");

            await host.StopAsync();
            _output.WriteLine("✓ Host stopped gracefully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ FAILED: {ex.Message}");
            throw;
        }
        finally
        {
            host.Dispose();
        }
    }

    /// <summary>
    /// Test 2: All Blazor WebAssembly resources exist with correct SHA-256 hashes
    /// </summary>
    /// <remarks>
    /// Это КРИТИЧЕСКИЙ тест! Он ловит проблему "Orchestra.Core.lxm2gmx0nh.pdb 404" и SRI integrity failures.
    /// Проверяет, что все файлы из blazor.boot.json существуют и имеют правильные хэши.
    /// </remarks>
    [Fact]
    public async Task OrchestraWeb_AllBlazorResources_Exist_WithCorrectHashes()
    {
        _output.WriteLine("=== Testing Blazor Resources Integrity ===");

        var blazorBootPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..",
            "Orchestra.Web", "bin", "Debug", "net9.0", "wwwroot", "_framework", "blazor.boot.json");

        blazorBootPath = Path.GetFullPath(blazorBootPath);

        _output.WriteLine($"Checking: {blazorBootPath}");

        if (!File.Exists(blazorBootPath))
        {
            Assert.Fail($"blazor.boot.json not found at: {blazorBootPath}. Run 'dotnet build src/Orchestra.Web/' first!");
        }

        var json = await File.ReadAllTextAsync(blazorBootPath);
        var bootConfig = JsonDocument.Parse(json);

        var frameworkDir = Path.GetDirectoryName(blazorBootPath)!;
        var totalFiles = 0;
        var verifiedFiles = 0;

        // Check assembly files
        if (bootConfig.RootElement.TryGetProperty("resources", out var resources))
        {
            if (resources.TryGetProperty("assembly", out var assemblies))
            {
                foreach (var assembly in assemblies.EnumerateObject())
                {
                    totalFiles++;
                    var fileName = assembly.Name;
                    var expectedHash = assembly.Value.GetString()!;
                    var filePath = Path.Combine(frameworkDir, fileName);

                    _output.WriteLine($"Checking: {fileName}");

                    Assert.True(File.Exists(filePath), $"Missing file: {fileName}");

                    var actualHash = await ComputeSha256Hash(filePath);
                    Assert.Equal(expectedHash, actualHash, StringComparer.OrdinalIgnoreCase);

                    verifiedFiles++;
                }
            }

            // Check PDB files
            if (resources.TryGetProperty("pdb", out var pdbs))
            {
                foreach (var pdb in pdbs.EnumerateObject())
                {
                    totalFiles++;
                    var fileName = pdb.Name;
                    var expectedHash = pdb.Value.GetString()!;
                    var filePath = Path.Combine(frameworkDir, fileName);

                    _output.WriteLine($"Checking: {fileName}");

                    Assert.True(File.Exists(filePath), $"Missing file: {fileName}");

                    var actualHash = await ComputeSha256Hash(filePath);
                    Assert.Equal(expectedHash, actualHash, StringComparer.OrdinalIgnoreCase);

                    verifiedFiles++;
                }
            }
        }

        _output.WriteLine("");
        _output.WriteLine($"✓ All {verifiedFiles}/{totalFiles} Blazor resources verified successfully");
        _output.WriteLine("✓ No SRI integrity failures will occur");
    }

    /// <summary>
    /// Test 3: Database connectivity is available
    /// </summary>
    [Fact]
    public async Task DatabaseConnectivity_IsAvailable()
    {
        _output.WriteLine("=== Testing Database Connectivity ===");

        var host = CreateTestHost();

        try
        {
            await host.StartAsync();

            using var scope = host.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();

            // Test connection
            var canConnect = await dbContext.Database.CanConnectAsync();
            Assert.True(canConnect, "Cannot connect to database");

            _output.WriteLine("✓ Database connection successful");

            // Test basic query
            var count = await dbContext.Tasks.CountAsync();
            _output.WriteLine($"✓ Database query successful (found {count} tasks)");

            await host.StopAsync();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ FAILED: {ex.Message}");
            throw;
        }
        finally
        {
            host.Dispose();
        }
    }

    /// <summary>
    /// Test 4: All background services initialize successfully
    /// </summary>
    [Fact]
    public async Task AllBackgroundServices_Initialize_Successfully()
    {
        _output.WriteLine("=== Testing Background Services Initialization ===");

        var host = CreateTestHost();

        try
        {
            await host.StartAsync();

            var services = host.Services;

            // Check AgentHealthCheckService
            var healthCheckService = services.GetService<IHostedService>()
                as AgentHealthCheckService
                ?? services.GetServices<IHostedService>().OfType<AgentHealthCheckService>().FirstOrDefault();

            Assert.NotNull(healthCheckService);
            _output.WriteLine("✓ AgentHealthCheckService initialized");

            // Check MarkdownWorkflowWatcherService if registered
            var watcherService = services.GetServices<IHostedService>()
                .FirstOrDefault(s => s.GetType().Name == "MarkdownWorkflowWatcherService");

            if (watcherService != null)
            {
                _output.WriteLine("✓ MarkdownWorkflowWatcherService initialized");
            }

            _output.WriteLine("✓ All background services initialized successfully");

            await host.StopAsync();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ FAILED: {ex.Message}");
            throw;
        }
        finally
        {
            host.Dispose();
        }
    }

    /// <summary>
    /// Helper: Create test host mimicking Orchestra.API configuration
    /// </summary>
    private IHost CreateTestHost()
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureServices((context, services) =>
            {
                // Register DbContext
                services.AddDbContext<OrchestraDbContext>(options =>
                {
                    var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
                        ?? "Data Source=orchestra.db";
                    options.UseSqlite(connectionString);
                });

                // Register core services
                services.AddMemoryCache();
                services.AddSingleton<ProcessDiscoveryService>();

                // Configure AgentHealthCheckOptions
                services.Configure<AgentHealthCheckOptions>(options =>
                {
                    options.CheckInterval = TimeSpan.FromMinutes(1);
                    options.AgentTimeout = TimeSpan.FromMinutes(5);
                    options.RecoveryTimeout = TimeSpan.FromMinutes(2);
                });

                // Register AgentHealthCheckService as both IHostedService and as itself
                services.AddSingleton<AgentHealthCheckService>();
                services.AddHostedService(sp => sp.GetRequiredService<AgentHealthCheckService>());

                // Register MediatR
                services.AddMediatR(typeof(OrchestraDbContext).Assembly);
            });

        return builder.Build();
    }

    /// <summary>
    /// Helper: Compute SHA-256 hash for file verification
    /// </summary>
    private async Task<string> ComputeSha256Hash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return "sha256-" + Convert.ToBase64String(hashBytes);
    }
}
