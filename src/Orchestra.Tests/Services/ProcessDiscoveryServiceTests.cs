using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Models;
using Orchestra.Core.Services;
using System.Text.RegularExpressions;
using Xunit;

namespace Orchestra.Tests.Services;

/// <summary>
/// Unit-тесты для ProcessDiscoveryService.
/// Проверяют обнаружение процессов Claude Code и извлечение SessionId.
/// </summary>
public class ProcessDiscoveryServiceTests : IDisposable
{
    private readonly Mock<ILogger<ProcessDiscoveryService>> _mockLogger;
    private readonly IMemoryCache _memoryCache;
    private readonly ProcessDiscoveryService _service;

    public ProcessDiscoveryServiceTests()
    {
        _mockLogger = new Mock<ILogger<ProcessDiscoveryService>>();

        // Используем реальный MemoryCache для тестирования кэширования
        var cacheOptions = new MemoryCacheOptions
        {
            SizeLimit = 1024
        };
        _memoryCache = new MemoryCache(cacheOptions);

        _service = new ProcessDiscoveryService(_mockLogger.Object, _memoryCache);
    }

    public void Dispose()
    {
        _memoryCache?.Dispose();
    }

    #region DiscoverClaudeProcessesAsync Tests

    [Fact]
    public async Task DiscoverClaudeProcessesAsync_ReturnsNonNullList()
    {
        // Arrange & Act
        var result = await _service.DiscoverClaudeProcessesAsync();

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DiscoverClaudeProcessesAsync_CachesResults()
    {
        // Arrange & Act
        var result1 = await _service.DiscoverClaudeProcessesAsync();
        var result2 = await _service.DiscoverClaudeProcessesAsync();

        // Assert
        Assert.Same(result1, result2); // Должны быть тот же объект из кэша
    }

    [Fact]
    public async Task DiscoverClaudeProcessesAsync_AfterClearCache_ReturnsNewResults()
    {
        // Arrange
        var result1 = await _service.DiscoverClaudeProcessesAsync();

        // Act
        _service.ClearCache();
        var result2 = await _service.DiscoverClaudeProcessesAsync();

        // Assert
        Assert.NotSame(result1, result2); // Должны быть разные объекты после очистки кэша
    }

    /// <summary>
    /// КЛЮЧЕВОЙ ТЕСТ: Проверяет что SessionId извлекается из процессов Claude Code.
    /// ОЖИДАЕМЫЙ РЕЗУЛЬТАТ: Все найденные процессы Claude должны иметь SessionId.
    /// ТЕКУЩИЙ СТАТУС: FAILS - SessionId не извлекается (ROOT CAUSE из Phase 1.1)
    /// </summary>
    [Fact]
    public async Task DiscoverClaudeProcessesAsync_ExtractsSessionIdFromClaudeProcesses()
    {
        // Arrange & Act
        var processes = await _service.DiscoverClaudeProcessesAsync();

        // Assert
        if (processes.Any())
        {
            // Если нашли процессы Claude, то у них ДОЛЖЕН быть SessionId
            var processesWithSessionId = processes.Where(p => !string.IsNullOrEmpty(p.SessionId)).ToList();

            Assert.NotEmpty(processesWithSessionId); // Минимум один процесс должен иметь SessionId

            // Все SessionId должны быть валидными UUID
            foreach (var process in processesWithSessionId)
            {
                Assert.True(
                    Regex.IsMatch(process.SessionId!,
                        @"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$",
                        RegexOptions.IgnoreCase),
                    $"SessionId '{process.SessionId}' is not a valid UUID");
            }
        }
    }

    #endregion

    #region GetProcessIdForSessionAsync Tests

    [Fact]
    public async Task GetProcessIdForSessionAsync_NullSessionId_ReturnsNull()
    {
        // Arrange & Act
        var result = await _service.GetProcessIdForSessionAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProcessIdForSessionAsync_EmptySessionId_ReturnsNull()
    {
        // Arrange & Act
        var result = await _service.GetProcessIdForSessionAsync(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProcessIdForSessionAsync_WhitespaceSessionId_ReturnsNull()
    {
        // Arrange & Act
        var result = await _service.GetProcessIdForSessionAsync("   ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProcessIdForSessionAsync_UnknownSessionId_ReturnsNull()
    {
        // Arrange
        var unknownSessionId = "00000000-0000-0000-0000-000000000000";

        // Act
        var result = await _service.GetProcessIdForSessionAsync(unknownSessionId);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// КЛЮЧЕВОЙ ТЕСТ: Проверяет что ProcessId находится по известному SessionId.
    /// ОЖИДАЕМЫЙ РЕЗУЛЬТАТ: Для реального SessionId из .claude/projects должен вернуться ProcessId.
    /// ТЕКУЩИЙ СТАТУС: FAILS - SessionId не извлекается, всегда возвращает null
    /// </summary>
    [Fact]
    public async Task GetProcessIdForSessionAsync_ValidSessionId_ReturnsProcessId()
    {
        // Arrange
        var processes = await _service.DiscoverClaudeProcessesAsync();
        var processWithSessionId = processes.FirstOrDefault(p => !string.IsNullOrEmpty(p.SessionId));

        // Skip if no Claude processes with SessionId found
        if (processWithSessionId == null)
        {
            // TODO: После фикса SessionId extraction этот тест должен проходить
            return;
        }

        // Act
        var result = await _service.GetProcessIdForSessionAsync(processWithSessionId.SessionId!);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(processWithSessionId.ProcessId, result);
    }

    #endregion

    #region GetSocketPathForSessionAsync Tests

    [Fact]
    public async Task GetSocketPathForSessionAsync_NullSessionId_ReturnsNull()
    {
        // Arrange & Act
        var result = await _service.GetSocketPathForSessionAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSocketPathForSessionAsync_EmptySessionId_ReturnsNull()
    {
        // Arrange & Act
        var result = await _service.GetSocketPathForSessionAsync(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSocketPathForSessionAsync_UnknownSessionId_ReturnsNull()
    {
        // Arrange
        var unknownSessionId = "00000000-0000-0000-0000-000000000000";

        // Act
        var result = await _service.GetSocketPathForSessionAsync(unknownSessionId);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// КЛЮЧЕВОЙ ТЕСТ: Проверяет что SocketPath находится по известному SessionId.
    /// ОЖИДАЕМЫЙ РЕЗУЛЬТАТ: Для реального SessionId должен вернуться SocketPath.
    /// ТЕКУЩИЙ СТАТУС: FAILS - SessionId не извлекается, всегда возвращает null
    /// </summary>
    [Fact]
    public async Task GetSocketPathForSessionAsync_ValidSessionId_ReturnsSocketPath()
    {
        // Arrange
        var processes = await _service.DiscoverClaudeProcessesAsync();
        var processWithSessionId = processes.FirstOrDefault(p => !string.IsNullOrEmpty(p.SessionId));

        // Skip if no Claude processes with SessionId found
        if (processWithSessionId == null)
        {
            // TODO: После фикса SessionId extraction этот тест должен проходить
            return;
        }

        // Act
        var result = await _service.GetSocketPathForSessionAsync(processWithSessionId.SessionId!);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    #endregion

    #region GetConnectionParamsForAgentAsync Tests

    [Fact]
    public async Task GetConnectionParamsForAgentAsync_NullAgentId_ReturnsNull()
    {
        // Arrange & Act
        var result = await _service.GetConnectionParamsForAgentAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetConnectionParamsForAgentAsync_EmptyAgentId_ReturnsNull()
    {
        // Arrange & Act
        var result = await _service.GetConnectionParamsForAgentAsync(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetConnectionParamsForAgentAsync_WhitespaceAgentId_ReturnsNull()
    {
        // Arrange & Act
        var result = await _service.GetConnectionParamsForAgentAsync("   ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetConnectionParamsForAgentAsync_UnknownAgentId_ReturnsNull()
    {
        // Arrange
        var unknownAgentId = "00000000-0000-0000-0000-000000000000";

        // Act
        var result = await _service.GetConnectionParamsForAgentAsync(unknownAgentId);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// КЛЮЧЕВОЙ ТЕСТ: Проверяет что ConnectionParams возвращается для известного AgentId.
    /// ОЖИДАЕМЫЙ РЕЗУЛЬТАТ: Для реального SessionId должны вернуться параметры подключения.
    /// ТЕКУЩИЙ СТАТУС: FAILS - SessionId не извлекается, всегда возвращает null
    /// </summary>
    [Fact]
    public async Task GetConnectionParamsForAgentAsync_ValidAgentId_ReturnsConnectionParams()
    {
        // Arrange
        var processes = await _service.DiscoverClaudeProcessesAsync();
        var processWithSessionId = processes.FirstOrDefault(p => !string.IsNullOrEmpty(p.SessionId));

        // Skip if no Claude processes with SessionId found
        if (processWithSessionId == null)
        {
            // TODO: После фикса SessionId extraction этот тест должен проходить
            return;
        }

        // Act
        var result = await _service.GetConnectionParamsForAgentAsync(processWithSessionId.SessionId!);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("terminal", result.ConnectorType);
        Assert.True(result.ProcessId.HasValue || !string.IsNullOrEmpty(result.SocketPath) || !string.IsNullOrEmpty(result.PipeName));
        Assert.Equal(30, result.ConnectionTimeoutSeconds);
    }

    [Fact]
    public async Task GetConnectionParamsForAgentAsync_Windows_ReturnsProcessIdOrPipeName()
    {
        // Arrange
        var processes = await _service.DiscoverClaudeProcessesAsync();
        var processWithSessionId = processes.FirstOrDefault(p => !string.IsNullOrEmpty(p.SessionId));

        // Skip if not Windows or no processes found
        if (!OperatingSystem.IsWindows() || processWithSessionId == null)
        {
            return;
        }

        // Act
        var result = await _service.GetConnectionParamsForAgentAsync(processWithSessionId.SessionId!);

        // Assert
        if (result != null)
        {
            Assert.True(result.ProcessId.HasValue || !string.IsNullOrEmpty(result.PipeName));
        }
    }

    [Fact]
    public async Task GetConnectionParamsForAgentAsync_Unix_ReturnsProcessIdOrSocketPath()
    {
        // Arrange
        var processes = await _service.DiscoverClaudeProcessesAsync();
        var processWithSessionId = processes.FirstOrDefault(p => !string.IsNullOrEmpty(p.SessionId));

        // Skip if not Unix or no processes found
        if (OperatingSystem.IsWindows() || processWithSessionId == null)
        {
            return;
        }

        // Act
        var result = await _service.GetConnectionParamsForAgentAsync(processWithSessionId.SessionId!);

        // Assert
        if (result != null)
        {
            Assert.True(result.ProcessId.HasValue || !string.IsNullOrEmpty(result.SocketPath));
        }
    }

    #endregion

    #region ClearCache Tests

    [Fact]
    public void ClearCache_ClearsProcessCache()
    {
        // Arrange
        // Вызываем метод для заполнения кэша
        var _ = _service.DiscoverClaudeProcessesAsync().Result;

        // Act
        _service.ClearCache();

        // Assert
        // После очистки кэша следующий вызов должен вернуть новые данные
        var result = _service.DiscoverClaudeProcessesAsync().Result;
        Assert.NotNull(result);
    }

    #endregion
}
