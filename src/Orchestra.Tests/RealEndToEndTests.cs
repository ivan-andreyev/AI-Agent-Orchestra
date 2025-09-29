using Microsoft.AspNetCore.Mvc.Testing;
using Orchestra.Core;
using Orchestra.Core.Models;
using System.Text.Json;
using AgentStatus = Orchestra.Core.Data.Entities.AgentStatus;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Orchestra.Core.Data;

namespace Orchestra.Tests;

/// <summary>
/// Real end-to-end tests that execute actual Claude Code CLI commands.
/// These tests are slow (minutes per test) and require Claude Code CLI to be installed.
/// Use [Trait("Category", "RealE2E")] to run separately from regular tests.
/// </summary>
[Collection("RealE2E")]
[Trait("Category", "RealE2E")]
public class RealEndToEndTests : IDisposable
{
    private readonly RealEndToEndTestFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly List<string> _testDirectories;
    private readonly ITestOutputHelper _output;

    // Static flag and lock for database initialization synchronization
    private static bool _databaseInitialized = false;
    private static readonly object _dbLock = new object();

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

        // Initialize database once, then clean data between tests
        lock (_dbLock)
        {
            using var scope = _factory.Services.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();

            if (!_databaseInitialized)
            {
                // First test: initialize database schema
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();
                _databaseInitialized = true;
            }
            else
            {
                // Subsequent tests: clean data only, keep schema
                ClearTestData(dbContext);
            }
        }
    }

    /// <summary>
    /// Clears all test data from database tables between tests
    /// </summary>
    private void ClearTestData(OrchestraDbContext dbContext)
    {
        dbContext.Tasks.RemoveRange(dbContext.Tasks);
        dbContext.Agents.RemoveRange(dbContext.Agents);
        dbContext.Repositories.RemoveRange(dbContext.Repositories);
        dbContext.SaveChanges();
    }

    [Fact(Timeout = 1200000)] // 20 minutes timeout (first Claude request can take 15+ minutes)
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

        // Queue a simple file creation task
        var command = "Create a file called test.txt with the exact content: Hello from Real E2E Test";
        _output.WriteLine($"[TEST] Queuing task: {command}");
        _output.WriteLine($"[TEST] Task repository path: {testDir}");
        var taskId = await QueueTask(command, testDir, TaskPriority.High);
        _output.WriteLine($"[TEST] Task queued successfully with ID: {taskId}");

        // Wait for task execution (temporarily reduced for debugging)
        _output.WriteLine($"[TEST] Waiting 2 minutes for debugging...");
        await Task.Delay(TimeSpan.FromMinutes(2));

        // Assert: Verify file was created by Claude Code
        var fileExists = File.Exists(testFilePath);
        Assert.True(fileExists, $"File should exist at {testFilePath}");

        if (fileExists)
        {
            var content = await File.ReadAllTextAsync(testFilePath);
            Assert.Contains("Hello from Real E2E Test", content);
        }
    }

    [Fact(Timeout = 1200000)] // 20 minutes timeout (first Claude request can take 15+ minutes)
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

        // Queue task to read and modify file
        var command = "Read the file modify_test.txt and append the text: \\nModified by Claude Code";
        await QueueTask(command, testDir, TaskPriority.High);

        // Wait for execution (Claude Code first request takes 15+ minutes)
        await Task.Delay(TimeSpan.FromMinutes(18));

        // Assert: Verify file was modified
        var content = await File.ReadAllTextAsync(testFilePath);
        Assert.Contains("Initial content", content);
        Assert.Contains("Modified by Claude Code", content);
    }

    [Fact(Timeout = 1200000)] // 20 minutes timeout (first Claude request can take 15+ minutes)
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

        // Ping agent as busy before task
        await PingAgent(agentId, AgentStatus.Busy, "About to list files");

        // Queue task to list files
        var command = "List all .txt files in the current directory and save the list to files_list.txt";
        await QueueTask(command, testDir, TaskPriority.High);

        // Wait for execution (Claude Code first request takes 15+ minutes)
        await Task.Delay(TimeSpan.FromMinutes(18));

        // Assert: Verify output file was created with correct content
        var outputFile = Path.Combine(testDir, "files_list.txt");
        if (File.Exists(outputFile))
        {
            var content = await File.ReadAllTextAsync(outputFile);
            Assert.Contains("file1.txt", content);
            Assert.Contains("file2.txt", content);
        }
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
        var taskId = result.GetProperty("taskId").GetString() ?? string.Empty;
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
/// Collection definition for real E2E tests to ensure sequential execution
/// and shared test factory instance
/// </summary>
[CollectionDefinition("RealE2E")]
public class RealE2ETestCollection : ICollectionFixture<RealEndToEndTestFactory<Program>>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and ICollectionFixture<> interfaces.
}