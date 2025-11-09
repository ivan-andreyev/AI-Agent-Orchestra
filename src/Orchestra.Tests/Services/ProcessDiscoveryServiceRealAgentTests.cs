using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Services;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Orchestra.Tests.Services;

/// <summary>
/// Интеграционные тесты для ProcessDiscoveryService с реальными процессами Claude Code.
/// Эти тесты проверяют подключение к активным Claude Code агентам в системе.
/// </summary>
/// <remarks>
/// ЦЕЛЬ: Автоматическая проверка подключения к реальным Claude Code процессам,
/// чтобы не тыкать руками через Orchestra.Web каждый раз!
///
/// Тест автоматически:
/// 1. Находит все запущенные процессы Claude Code
/// 2. Извлекает SessionId из .claude/projects
/// 3. Получает параметры подключения
/// 4. Проверяет валидность всех данных
/// </remarks>
[Collection("IntegrationTests")]
public class ProcessDiscoveryServiceRealAgentTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<ProcessDiscoveryService>> _mockLogger;
    private readonly IMemoryCache _memoryCache;
    private readonly ProcessDiscoveryService _service;

    public ProcessDiscoveryServiceRealAgentTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<ProcessDiscoveryService>>();

        var cacheOptions = new MemoryCacheOptions
        {
            SizeLimit = 1024
        };
        _memoryCache = new MemoryCache(cacheOptions);

        _service = new ProcessDiscoveryService(_mockLogger.Object, _memoryCache);

        _output.WriteLine("=== ProcessDiscoveryServiceRealAgentTests initialized ===");
        _output.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }

    public void Dispose()
    {
        _memoryCache?.Dispose();
    }

    /// <summary>
    /// ГЛАВНЫЙ ТЕСТ: Проверяет подключение к реальным запущенным Claude Code агентам.
    /// </summary>
    /// <remarks>
    /// Этот тест:
    /// - Находит все запущенные Claude Code процессы
    /// - Проверяет извлечение SessionId из .claude/projects
    /// - Получает ConnectionParams для каждого агента
    /// - Валидирует все параметры подключения
    ///
    /// SKIP LOGIC: Если не найдено ни одного процесса Claude Code с SessionId,
    /// тест пропускается (не падает), т.к. это нормально - агенты могут быть не запущены.
    /// </remarks>
    [Fact]
    public async Task CanConnectToRunningClaudeCodeAgent_RealTest()
    {
        _output.WriteLine("");
        _output.WriteLine("=== STARTING REAL AGENT CONNECTION TEST ===");
        _output.WriteLine("");

        // Step 1: Discover all Claude Code processes
        _output.WriteLine("Step 1: Discovering Claude Code processes...");
        var processes = await _service.DiscoverClaudeProcessesAsync();

        _output.WriteLine($"Found {processes.Count} Claude Code process(es)");

        if (processes.Count == 0)
        {
            _output.WriteLine("WARNING: No Claude Code processes found running on this system.");
            _output.WriteLine("This is not a failure - just means no agents are currently running.");
            _output.WriteLine("SKIPPING TEST");
            return; // Skip test if no processes found
        }

        // Log details of all found processes
        _output.WriteLine("");
        _output.WriteLine("Found processes:");
        foreach (var process in processes)
        {
            _output.WriteLine($"  - PID: {process.ProcessId}, SessionId: {process.SessionId ?? "(none)"}, WorkDir: {process.WorkingDirectory}");
        }

        // Step 2: Filter processes with SessionId
        _output.WriteLine("");
        _output.WriteLine("Step 2: Filtering processes with SessionId...");
        var processesWithSession = processes.Where(p => !string.IsNullOrEmpty(p.SessionId)).ToList();

        _output.WriteLine($"Processes with SessionId: {processesWithSession.Count}");

        if (processesWithSession.Count == 0)
        {
            _output.WriteLine("WARNING: Found Claude Code processes, but none have SessionId extracted.");
            _output.WriteLine("This means SessionId extraction from .claude/projects is not working!");
            _output.WriteLine("FAILING TEST - SessionId extraction should work!");

            // This IS a failure - we found processes but can't extract SessionId
            Assert.Fail("Found Claude Code processes but SessionId extraction failed. Check ProcessDiscoveryService.ExtractSessionIdFromProjectPath()");
        }

        // Step 3: Validate SessionId format
        _output.WriteLine("");
        _output.WriteLine("Step 3: Validating SessionId format...");
        foreach (var process in processesWithSession)
        {
            _output.WriteLine($"Validating SessionId: {process.SessionId}");

            var isValidUuid = Regex.IsMatch(
                process.SessionId!,
                @"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$",
                RegexOptions.IgnoreCase);

            Assert.True(isValidUuid, $"SessionId '{process.SessionId}' is not a valid UUID format");
            _output.WriteLine($"  ✓ SessionId is valid UUID");
        }

        // Step 4: Get connection parameters for each agent
        _output.WriteLine("");
        _output.WriteLine("Step 4: Getting connection parameters...");

        var successfulConnections = 0;

        foreach (var process in processesWithSession)
        {
            _output.WriteLine($"");
            _output.WriteLine($"Testing agent: {process.SessionId}");
            _output.WriteLine($"  PID: {process.ProcessId}");
            _output.WriteLine($"  WorkDir: {process.WorkingDirectory}");
            _output.WriteLine($"  SocketPath: {process.SocketPath}");

            // Get connection params
            var connectionParams = await _service.GetConnectionParamsForAgentAsync(process.SessionId!);

            if (connectionParams == null)
            {
                _output.WriteLine($"  ✗ FAILED: Could not get connection parameters");
                Assert.Fail($"Failed to get connection parameters for SessionId: {process.SessionId}");
            }

            _output.WriteLine($"  ✓ Got connection parameters:");
            _output.WriteLine($"    ConnectorType: {connectionParams.ConnectorType}");
            _output.WriteLine($"    ProcessId: {connectionParams.ProcessId?.ToString() ?? "(null)"}");
            _output.WriteLine($"    SocketPath: {connectionParams.SocketPath ?? "(null)"}");
            _output.WriteLine($"    PipeName: {connectionParams.PipeName ?? "(null)"}");
            _output.WriteLine($"    Timeout: {connectionParams.ConnectionTimeoutSeconds}s");

            // Validate connection params
            Assert.NotNull(connectionParams);
            Assert.Equal("terminal", connectionParams.ConnectorType);
            Assert.Equal(30, connectionParams.ConnectionTimeoutSeconds);

            // Check that we have either ProcessId or SocketPath/PipeName
            var hasProcessId = connectionParams.ProcessId.HasValue;
            var hasSocketPath = !string.IsNullOrEmpty(connectionParams.SocketPath);
            var hasPipeName = !string.IsNullOrEmpty(connectionParams.PipeName);

            Assert.True(
                hasProcessId || hasSocketPath || hasPipeName,
                "Connection params must have either ProcessId, SocketPath, or PipeName");

            _output.WriteLine($"  ✓ Connection parameters are valid");
            successfulConnections++;
        }

        // Final summary
        _output.WriteLine("");
        _output.WriteLine("=== TEST SUMMARY ===");
        _output.WriteLine($"Total processes found: {processes.Count}");
        _output.WriteLine($"Processes with SessionId: {processesWithSession.Count}");
        _output.WriteLine($"Successful connections: {successfulConnections}");
        _output.WriteLine("");
        _output.WriteLine("✓ TEST PASSED - All real agents are connectable!");
        _output.WriteLine("");

        // Assert final success
        Assert.True(successfulConnections > 0, "Should have at least one successful connection");
    }

    /// <summary>
    /// Дополнительный тест: Проверяет что ProcessId можно получить по SessionId.
    /// </summary>
    [Fact]
    public async Task GetProcessIdForSession_WithRealAgent_ReturnsProcessId()
    {
        _output.WriteLine("");
        _output.WriteLine("=== Testing GetProcessIdForSessionAsync ===");

        var processes = await _service.DiscoverClaudeProcessesAsync();
        var processWithSession = processes.FirstOrDefault(p => !string.IsNullOrEmpty(p.SessionId));

        if (processWithSession == null)
        {
            _output.WriteLine("SKIP: No Claude Code processes with SessionId found");
            return;
        }

        _output.WriteLine($"Testing with SessionId: {processWithSession.SessionId}");

        var processId = await _service.GetProcessIdForSessionAsync(processWithSession.SessionId!);

        Assert.NotNull(processId);
        Assert.Equal(processWithSession.ProcessId, processId);

        _output.WriteLine($"✓ Successfully got ProcessId: {processId}");
    }

    /// <summary>
    /// Дополнительный тест: Проверяет что SocketPath можно получить по SessionId.
    /// </summary>
    [Fact]
    public async Task GetSocketPathForSession_WithRealAgent_ReturnsSocketPath()
    {
        _output.WriteLine("");
        _output.WriteLine("=== Testing GetSocketPathForSessionAsync ===");

        var processes = await _service.DiscoverClaudeProcessesAsync();
        var processWithSession = processes.FirstOrDefault(p => !string.IsNullOrEmpty(p.SessionId));

        if (processWithSession == null)
        {
            _output.WriteLine("SKIP: No Claude Code processes with SessionId found");
            return;
        }

        _output.WriteLine($"Testing with SessionId: {processWithSession.SessionId}");

        var socketPath = await _service.GetSocketPathForSessionAsync(processWithSession.SessionId!);

        Assert.NotNull(socketPath);
        Assert.NotEmpty(socketPath);

        _output.WriteLine($"✓ Successfully got SocketPath: {socketPath}");
    }
}
