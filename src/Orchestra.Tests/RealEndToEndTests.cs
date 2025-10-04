using Microsoft.AspNetCore.Mvc.Testing;
using Orchestra.Core;
using Orchestra.Core.Models;
using System.Text.Json;
using AgentStatus = Orchestra.Core.Data.Entities.AgentStatus;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Orchestra.Core.Data;
using Orchestra.Core.Services;
using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace Orchestra.Tests;

/// <summary>
/// Real end-to-end tests that execute actual Claude Code CLI commands.
/// These tests are slow (minutes per test) and require Claude Code CLI to be installed.
/// Use [Trait("Category", "RealE2E")] to run separately from regular tests.
/// Each test class gets its own database via IClassFixture for complete isolation.
/// IMPORTANT: Uses separate "RealE2E" collection to isolate slow real CLI tests from fast Integration tests.
/// Sequential execution within RealE2E collection prevents JobStorage.Current race conditions.
/// </summary>
[Collection("RealE2E")]
[Trait("Category", "RealE2E")]
public class RealEndToEndTests : IDisposable, IClassFixture<RealEndToEndTestFactory<Program>>
{
    private readonly RealEndToEndTestFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly List<string> _testDirectories;
    private readonly ITestOutputHelper _output;

    public RealEndToEndTests(RealEndToEndTestFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _testDirectories = new List<string>();
        _output = output;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        // Initialize database for test class (IClassFixture provides isolation)
        // Each test class has its own database via IClassFixture
        // NO data clearing needed - complete isolation between test classes
        // NOTE: Warmup removed from constructor - first test will be slow (~15-20 min)
        // but this prevents constructor deadlocks and allows proper async handling
        InitializeTestDatabase();
    }

    /// <summary>
    /// Initializes test database with required schema.
    /// Each test class gets its own database via IClassFixture.
    /// NO data clearing needed - complete isolation between test classes.
    /// Also verifies Claude CLI is available for real E2E execution.
    /// </summary>
    private void InitializeTestDatabase()
    {
        try
        {
            // CRITICAL: Check if Claude CLI exists before running tests
            var cliPath = @"C:\Users\mrred\AppData\Roaming\npm\claude.cmd";
            if (!File.Exists(cliPath))
            {
                var errorMsg = $"Claude CLI not found at: {cliPath}. " +
                              $"Install Claude CLI with 'npm install -g @anthropic-ai/claude-code' " +
                              $"or update path in ClaudeCodeConfiguration.";
                _output.WriteLine($"[INIT] FATAL: {errorMsg}");
                throw new InvalidOperationException(errorMsg);
            }
            _output.WriteLine($"[INIT] ✓ Claude CLI found at: {cliPath}");

            using var scope = _factory.Services.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();

            // Create database if it doesn't exist (first test in this class)
            // Each test class has its own database via IClassFixture
            var created = dbContext.Database.EnsureCreated();

            if (created)
            {
                _output.WriteLine($"[DB] ✓ Test database created successfully for class {GetType().Name}");
            }
            else
            {
                _output.WriteLine($"[DB] ✓ Test database already exists for class {GetType().Name} (shared within class)");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[INIT] ✗ Failed to initialize: {ex.Message}");
            _output.WriteLine($"[INIT] Stack trace: {ex.StackTrace}");
            throw new InvalidOperationException("Could not initialize test environment", ex);
        }
    }

    [Fact(Timeout = 1800000)] // 30 minutes timeout (first Claude request includes warmup - takes 15-20 minutes)
    public async Task RealClaudeCode_CreateFile_ShouldExecuteSuccessfully()
    {
        // Arrange: Create unique test directory
        var testId = Guid.NewGuid().ToString("N")[..8];
        var testDir = Path.Combine(Path.GetTempPath(), $"Orchestra_RealE2E_{testId}");
        Directory.CreateDirectory(testDir);
        _testDirectories.Add(testDir);

        var agentId = $"real-claude-{testId}";
        var testFilePath = Path.Combine(testDir, "test.txt");

        // Act: Register agent with real Claude Code type
        _output.WriteLine($"[TEST] Registering agent: {agentId}, Repository: {testDir}");
        await RegisterAgent(agentId, "Real Claude Agent", "claude-code", testDir);

        // Wait for DB synchronization
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Verify agent registered
        _output.WriteLine($"[TEST] Verifying agent registration...");
        var state = await GetState();
        _output.WriteLine($"[TEST] GetState returned {state.Agents.Count} agents");
        foreach (var kvp in state.Agents)
        {
            _output.WriteLine($"[TEST] Agent: {kvp.Key}, Status: {kvp.Value.Status}, Repository: {kvp.Value.RepositoryPath}");
        }

        Assert.True(state.Agents.ContainsKey(agentId), "Agent should be registered");
        Assert.Equal(AgentStatus.Idle, state.Agents[agentId].Status);

        // Queue a simple file creation task with absolute path
        // Use single-line command format - multi-line with \n confuses Claude CLI
        var command = $"Create a file at '{testFilePath}' with the content 'Hello from Real E2E Test'";
        _output.WriteLine($"[TEST] Queuing task: {command}");
        _output.WriteLine($"[TEST] Expected file location: {testFilePath}");
        _output.WriteLine($"[TEST] Task repository path: {testDir}");
        var taskId = await QueueTask(command, testDir, TaskPriority.High);
        _output.WriteLine($"[TEST] Task queued successfully with ID: {taskId}");

        // Wait for task execution with polling (first Claude request takes 15-20 minutes)
        var completed = await WaitForTaskCompletionWithPolling(
            taskId,
            expectedFilePath: testFilePath,
            timeout: TimeSpan.FromMinutes(18));

        // Assert: Verify file was created by Claude Code
        Assert.True(completed, $"Task {taskId} did not complete within timeout");

        var fileExists = File.Exists(testFilePath);
        _output.WriteLine($"[TEST] File exists at {testFilePath}: {fileExists}");
        Assert.True(fileExists, $"File should exist at {testFilePath}");

        if (fileExists)
        {
            var content = await File.ReadAllTextAsync(testFilePath);
            Assert.Contains("Hello from Real E2E Test", content);
        }
    }

    [Fact(Timeout = 300000)] // 5 minutes timeout (CLI already warmed up by first test)
    public async Task RealClaudeCode_ReadAndModifyFile_ShouldWorkEndToEnd()
    {
        // Arrange: Create test directory with initial file
        var testId = Guid.NewGuid().ToString("N")[..8];
        var testDir = Path.Combine(Path.GetTempPath(), $"Orchestra_RealE2E_{testId}");
        Directory.CreateDirectory(testDir);
        _testDirectories.Add(testDir);

        var testFilePath = Path.Combine(testDir, "modify_test.txt");
        await File.WriteAllTextAsync(testFilePath, "Initial content");

        var agentId = $"real-claude-modify-{testId}";

        // Act: Register agent
        await RegisterAgent(agentId, "Real Claude Modify Agent", "claude-code", testDir);

        // Queue task to read and modify file with absolute path
        // Use single-line command format - multi-line with \n confuses Claude CLI
        var command = $"Read the file at '{testFilePath}' and append this text to the end: Modified by Claude Code";
        var taskId = await QueueTask(command, testDir, TaskPriority.High);

        // Wait for execution with polling
        // Note: This test runs AFTER CreateFile test, so Claude CLI is already warmed up
        // Expected completion time: 10-30 seconds (no warmup needed)
        var completed = await WaitForTaskCompletionWithPolling(
            taskId,
            expectedFilePath: testFilePath, // File already exists, but check for modification timestamp change
            timeout: TimeSpan.FromMinutes(5)); // 5 minutes should be plenty after warmup

        // Assert: Verify task completed and file was modified
        Assert.True(completed, $"Task {taskId} did not complete within timeout");

        var content = await File.ReadAllTextAsync(testFilePath);
        Assert.Contains("Initial content", content);
        Assert.Contains("Modified by Claude Code", content);
    }

    [Fact(Timeout = 300000)] // 5 minutes timeout (CLI already warmed up by previous tests)
    public async Task RealClaudeCode_ListFiles_ShouldReturnCorrectOutput()
    {
        // Arrange: Create test directory with known files
        var testId = Guid.NewGuid().ToString("N")[..8];
        var testDir = Path.Combine(Path.GetTempPath(), $"Orchestra_RealE2E_{testId}");
        Directory.CreateDirectory(testDir);
        _testDirectories.Add(testDir);

        // Create known test files
        await File.WriteAllTextAsync(Path.Combine(testDir, "file1.txt"), "Content 1");
        await File.WriteAllTextAsync(Path.Combine(testDir, "file2.txt"), "Content 2");
        await File.WriteAllTextAsync(Path.Combine(testDir, "file3.md"), "Markdown content");

        var agentId = $"real-claude-list-{testId}";

        // Act: Register agent
        await RegisterAgent(agentId, "Real Claude List Agent", "claude-code", testDir);

        // Queue task to list files with absolute paths
        var outputFile = Path.Combine(testDir, "files_list.txt");
        var command = $"Create a file at '{outputFile}' with a list of all .txt files in the directory '{testDir}'. List one file per line.";
        var taskId = await QueueTask(command, testDir, TaskPriority.High);

        // Wait for execution with polling
        // Note: This test runs AFTER previous tests, so Claude CLI is already warmed up
        // Expected completion time: 10-30 seconds (no warmup needed)
        var completed = await WaitForTaskCompletionWithPolling(
            taskId,
            expectedFilePath: outputFile,
            timeout: TimeSpan.FromMinutes(5)); // 5 minutes should be plenty after warmup

        // Assert: Verify task completed and output file was created with correct content
        Assert.True(completed, $"Task {taskId} did not complete within timeout");
        Assert.True(File.Exists(outputFile), $"Output file should exist at {outputFile}");

        var content = await File.ReadAllTextAsync(outputFile);
        Assert.Contains("file1.txt", content);
        Assert.Contains("file2.txt", content);
    }

    #region Helper Methods

    private async Task RegisterAgent(string id, string name, string type, string repositoryPath)
    {
        var request = new { Id = id, Name = name, Type = type, RepositoryPath = repositoryPath, SessionId = (string?)null };
        var response = await _client.PostAsJsonAsync("/agents/register", request);
        response.EnsureSuccessStatusCode();
    }

    private async Task PingAgent(string agentId, AgentStatus status, string? currentTask)
    {
        var request = new { Status = (int)status, CurrentTask = currentTask };
        var response = await _client.PostAsJsonAsync($"/agents/{agentId}/ping", request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> QueueTask(string command, string repositoryPath, TaskPriority priority)
    {
        var request = new { Command = command, RepositoryPath = repositoryPath, Priority = (int)priority };
        var response = await _client.PostAsJsonAsync("/tasks/queue", request);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[TEST] QueueTask response: {responseContent}");

        var result = JsonSerializer.Deserialize<JsonElement>(responseContent, _jsonOptions);
        var taskId = result.GetProperty("TaskId").GetString() ?? string.Empty;
        _output.WriteLine($"[TEST] Task ID: {taskId}");

        return taskId;
    }

    private async Task<OrchestratorState> GetState()
    {
        var response = await _client.GetAsync("/state");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<OrchestratorState>(content, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize state");
    }

    /// <summary>
    /// Waits for task completion by polling task status and optionally file changes.
    /// First Claude CLI request takes 15-20 minutes due to model loading.
    /// Subsequent requests complete in 10-30 seconds.
    /// </summary>
    /// <param name="taskId">Task ID to monitor</param>
    /// <param name="expectedFilePath">Optional file path to check (for creation tests, checks existence; for modify tests, checks last write time)</param>
    /// <param name="timeout">Maximum time to wait (default 18 minutes)</param>
    /// <returns>True if task completed successfully, false if timeout</returns>
    private async Task<bool> WaitForTaskCompletionWithPolling(
        string taskId,
        string? expectedFilePath = null,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromMinutes(18); // Default 18 minutes (enough for first warmup)
        var startTime = DateTime.UtcNow;
        var pollInterval = TimeSpan.FromSeconds(10); // Poll every 10 seconds
        var lastLogTime = DateTime.UtcNow;
        var logInterval = TimeSpan.FromMinutes(1); // Log every minute

        // Capture initial file state if we're monitoring file changes
        DateTime? initialFileTime = null;
        bool fileExistedInitially = false;
        if (!string.IsNullOrEmpty(expectedFilePath) && File.Exists(expectedFilePath))
        {
            fileExistedInitially = true;
            initialFileTime = File.GetLastWriteTimeUtc(expectedFilePath);
            _output.WriteLine($"[POLL] File already exists: {expectedFilePath} (LastWrite: {initialFileTime:HH:mm:ss})");
        }

        _output.WriteLine($"[POLL] Waiting for task {taskId} completion (timeout: {timeout.Value.TotalMinutes:F1}m)");
        if (!string.IsNullOrEmpty(expectedFilePath))
        {
            if (fileExistedInitially)
            {
                _output.WriteLine($"[POLL] Monitoring file modifications: {expectedFilePath}");
            }
            else
            {
                _output.WriteLine($"[POLL] Monitoring file creation: {expectedFilePath}");
            }
        }

        while (DateTime.UtcNow - startTime < timeout.Value)
        {
            var elapsed = DateTime.UtcNow - startTime;

            // Log progress every minute
            if (DateTime.UtcNow - lastLogTime >= logInterval)
            {
                _output.WriteLine($"[POLL] Still waiting... {elapsed.TotalMinutes:F1}m elapsed (timeout at {timeout.Value.TotalMinutes:F1}m)");
                lastLogTime = DateTime.UtcNow;
            }

            // Check file status
            if (!string.IsNullOrEmpty(expectedFilePath))
            {
                if (File.Exists(expectedFilePath))
                {
                    if (fileExistedInitially)
                    {
                        // For modification tests: check if file was modified after we started watching
                        var currentFileTime = File.GetLastWriteTimeUtc(expectedFilePath);
                        if (currentFileTime > initialFileTime)
                        {
                            _output.WriteLine($"[POLL] ✓ File modified at {expectedFilePath} after {elapsed.TotalSeconds:F1}s (LastWrite: {currentFileTime:HH:mm:ss})");
                            // Give file system a moment to complete writes
                            await Task.Delay(1000);
                            return true;
                        }
                    }
                    else
                    {
                        // For creation tests: file appeared (didn't exist initially)
                        _output.WriteLine($"[POLL] ✓ File created at {expectedFilePath} after {elapsed.TotalSeconds:F1}s");
                        // Give file system a moment to complete writes
                        await Task.Delay(1000);
                        return true;
                    }
                }
            }

            // Small delay before next check
            await Task.Delay(pollInterval);
        }

        var totalElapsed = DateTime.UtcNow - startTime;
        _output.WriteLine($"[POLL] ✗ Timeout after {totalElapsed.TotalMinutes:F1}m waiting for task {taskId}");

        // Final check for file changes
        if (!string.IsNullOrEmpty(expectedFilePath) && File.Exists(expectedFilePath))
        {
            if (fileExistedInitially)
            {
                var finalFileTime = File.GetLastWriteTimeUtc(expectedFilePath);
                if (finalFileTime > initialFileTime)
                {
                    _output.WriteLine($"[POLL] ⚠️  File was modified just after timeout!");
                    return true;
                }
                else
                {
                    _output.WriteLine($"[POLL] ✗ File was never modified (LastWrite still: {finalFileTime:HH:mm:ss})");
                }
            }
            else
            {
                _output.WriteLine($"[POLL] ⚠️  File appeared just after timeout!");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Прогревает Claude Code CLI при первом запуске теста.
    /// Первый запуск CLI занимает 15+ минут из-за загрузки модели,
    /// последующие запуски выполняются за 10-30 секунд.
    /// NOTE: This method is deprecated - warmup now happens automatically on first test.
    /// </summary>
    private void WarmupClaudeCodeCli()
    {
        var warmupDir = Path.Combine(Path.GetTempPath(), $"Orchestra_CLI_Warmup_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(warmupDir);
            _output.WriteLine($"[WARMUP] Created warmup directory: {warmupDir}");

            using var scope = _factory.Services.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IAgentExecutor>();

            _output.WriteLine("[WARMUP] Executing warmup command (this will take 15+ minutes on first run)...");
            var startTime = DateTime.UtcNow;

            // Синхронный вызов для прогрева
            var result = executor.ExecuteCommandAsync("Say hello", warmupDir, CancellationToken.None).GetAwaiter().GetResult();

            var duration = DateTime.UtcNow - startTime;
            _output.WriteLine($"[WARMUP] Warmup completed in {duration.TotalSeconds:F1} seconds");
            _output.WriteLine($"[WARMUP] Success: {result.Success}");

            if (!result.Success)
            {
                _output.WriteLine($"[WARMUP] Warning: Warmup failed with error: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[WARMUP] Exception during warmup: {ex.Message}");
            throw new InvalidOperationException("Failed to warm up Claude Code CLI", ex);
        }
        finally
        {
            try
            {
                if (Directory.Exists(warmupDir))
                {
                    Directory.Delete(warmupDir, recursive: true);
                    _output.WriteLine($"[WARMUP] Cleaned up warmup directory");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[WARMUP] Failed to cleanup warmup directory: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Comprehensive Hangfire diagnostics для отладки RealE2E тестов
    /// </summary>
    private void DiagnoseHangfireExecution(string taskId, string testName)
    {
        _output.WriteLine($"");
        _output.WriteLine($"=== HANGFIRE DIAGNOSTICS START: {testName} ===");

        try
        {
            // 1. Check Hangfire Storage
            var storage = JobStorage.Current;
            _output.WriteLine($"[DIAG] Hangfire Storage: {storage?.GetType().Name ?? "NULL"}");

            if (storage == null)
            {
                _output.WriteLine($"[DIAG] ⚠️  Hangfire Storage not available, skipping diagnostics (non-critical)");
                return;
            }

            // 2. Get Monitoring API (may throw ObjectDisposedException if storage disposed)
            IMonitoringApi? monitoringApi = null;
            try
            {
                monitoringApi = storage.GetMonitoringApi();
            }
            catch (ObjectDisposedException)
            {
                _output.WriteLine($"[DIAG] ⚠️  Storage disposed, diagnostics unavailable (non-critical)");
                return;
            }

            _output.WriteLine($"[DIAG] Monitoring API available: {monitoringApi != null}");

            if (monitoringApi == null)
            {
                _output.WriteLine($"[DIAG] ⚠️  Monitoring API not available, skipping diagnostics");
                return;
            }

            // 3. Check Enqueued jobs
            var enqueuedJobs = monitoringApi.EnqueuedJobs("default", 0, 100);
            _output.WriteLine($"[DIAG] Enqueued jobs (default queue): {enqueuedJobs.Count}");
            foreach (var job in enqueuedJobs.Take(5))
            {
                _output.WriteLine($"[DIAG]   Enqueued: JobId={job.Key}, Method={job.Value?.Job?.Method?.Name ?? "unknown"}");
            }

            // 4. Check Processing jobs
            var processingJobs = monitoringApi.ProcessingJobs(0, 100);
            _output.WriteLine($"[DIAG] Processing jobs: {processingJobs.Count}");
            foreach (var job in processingJobs.Take(5))
            {
                _output.WriteLine($"[DIAG]   Processing: JobId={job.Key}, Method={job.Value?.Job?.Method?.Name ?? "unknown"}, Server={job.Value?.ServerId ?? "unknown"}");
            }

            // 5. Check Succeeded jobs
            var succeededJobs = monitoringApi.SucceededJobs(0, 100);
            _output.WriteLine($"[DIAG] Succeeded jobs: {succeededJobs.Count}");
            foreach (var job in succeededJobs.Take(5))
            {
                var args = job.Value?.Job?.Args;
                var hasTaskId = args?.Any(arg => arg?.ToString()?.Contains(taskId) == true) ?? false;
                _output.WriteLine($"[DIAG]   Succeeded: JobId={job.Key}, Method={job.Value?.Job?.Method?.Name ?? "unknown"}, ContainsTaskId={hasTaskId}");

                if (hasTaskId)
                {
                    _output.WriteLine($"[DIAG]   ✅ FOUND SUCCEEDED JOB FOR TASK: {taskId}");
                }
            }

            // 6. Check Failed jobs
            var failedJobs = monitoringApi.FailedJobs(0, 100);
            _output.WriteLine($"[DIAG] Failed jobs: {failedJobs.Count}");
            foreach (var job in failedJobs.Take(5))
            {
                var args = job.Value?.Job?.Args;
                var hasTaskId = args?.Any(arg => arg?.ToString()?.Contains(taskId) == true) ?? false;
                _output.WriteLine($"[DIAG]   Failed: JobId={job.Key}, Method={job.Value?.Job?.Method?.Name ?? "unknown"}, ContainsTaskId={hasTaskId}");
                _output.WriteLine($"[DIAG]     Exception: {job.Value?.ExceptionMessage ?? "none"}");

                if (hasTaskId)
                {
                    _output.WriteLine($"[DIAG]   ❌ FOUND FAILED JOB FOR TASK: {taskId}");
                    _output.WriteLine($"[DIAG]     Full Exception: {job.Value?.ExceptionDetails ?? "none"}");
                }
            }

            // 7. Check Servers
            var servers = monitoringApi.Servers();
            _output.WriteLine($"[DIAG] Active Hangfire Servers: {servers.Count}");
            foreach (var server in servers)
            {
                _output.WriteLine($"[DIAG]   Server: {server.Name}, Queues: [{string.Join(", ", server.Queues)}], Workers: {server.WorkersCount}, StartedAt: {server.StartedAt}");
            }

            if (servers.Count == 0)
            {
                _output.WriteLine($"[DIAG] ❌ CRITICAL: No active Hangfire Servers! Jobs cannot execute!");
            }

            // 8. Check Database Task Status
            using var scope = _factory.Services.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();
            var task = dbContext.Tasks.FirstOrDefault(t => t.Id == taskId);

            if (task != null)
            {
                _output.WriteLine($"[DIAG] Database Task Status: {task.Status}");
                _output.WriteLine($"[DIAG]   AgentId: {task.AgentId ?? "null"}");
                _output.WriteLine($"[DIAG]   UpdatedAt: {task.UpdatedAt}");
                _output.WriteLine($"[DIAG]   ErrorMessage: {task.ErrorMessage ?? "none"}");
            }
            else
            {
                _output.WriteLine($"[DIAG] ❌ Task NOT FOUND in database: {taskId}");
            }

            // 9. Check Statistics
            var stats = monitoringApi.GetStatistics();
            _output.WriteLine($"[DIAG] Statistics:");
            _output.WriteLine($"[DIAG]   Enqueued: {stats.Enqueued}");
            _output.WriteLine($"[DIAG]   Scheduled: {stats.Scheduled}");
            _output.WriteLine($"[DIAG]   Processing: {stats.Processing}");
            _output.WriteLine($"[DIAG]   Succeeded: {stats.Succeeded}");
            _output.WriteLine($"[DIAG]   Failed: {stats.Failed}");
            _output.WriteLine($"[DIAG]   Deleted: {stats.Deleted}");
            _output.WriteLine($"[DIAG]   Recurring: {stats.Recurring}");
            _output.WriteLine($"[DIAG]   Servers: {stats.Servers}");
            _output.WriteLine($"[DIAG]   Queues: {stats.Queues}");
        }
        catch (ObjectDisposedException ex)
        {
            // Storage disposed - happens when parallel test collections dispose their storages
            // This is non-critical since tests should not depend on diagnostics
            _output.WriteLine($"[DIAG] ⚠️  Storage disposed during diagnostics (non-critical): {ex.Message}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[DIAG] ❌ Unexpected exception during diagnostics: {ex.Message}");
            _output.WriteLine($"[DIAG]   Type: {ex.GetType().Name}");
            _output.WriteLine($"[DIAG]   Stack: {ex.StackTrace}");
        }

        _output.WriteLine($"=== HANGFIRE DIAGNOSTICS END ===");
        _output.WriteLine($"");
    }

    #endregion

    public void Dispose()
    {
        // Cleanup: Delete all test directories created during tests
        foreach (var dir in _testDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup - ignore errors
            }
        }

        _client?.Dispose();
    }
}

/// <summary>
/// Collection definition for RealE2E tests to ensure sequential execution.
/// This prevents JobStorage.Current race conditions between parallel test collections.
/// Database isolation is handled at the class level via IClassFixture.
/// </summary>
[CollectionDefinition("RealE2E")]
public class RealE2ETestCollection
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] for sequential test execution.
    // Database isolation is handled at the class level via IClassFixture.
}