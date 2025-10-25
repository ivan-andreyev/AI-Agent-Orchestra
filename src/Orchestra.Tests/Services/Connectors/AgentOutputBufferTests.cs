using Orchestra.Core.Services.Connectors;
using Xunit;

namespace Orchestra.Tests.Services.Connectors;

/// <summary>
/// Тесты для AgentOutputBuffer
/// </summary>
/// <remarks>
/// Полное покрытие функциональности AgentOutputBuffer:
/// - Конструктор и валидация параметров
/// - Операции добавления строк
/// - Операции получения строк (последние N, все, с фильтром)
/// - Очистка буфера
/// - Поведение циркулярного буфера
/// - Потокобезопасность
/// - IDisposable паттерн
/// - События LineAdded
/// </remarks>
public class AgentOutputBufferTests : IDisposable
{
    private AgentOutputBuffer? _buffer;

    public void Dispose()
    {
        _buffer?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidMaxLines_CreatesInstance()
    {
        // Arrange & Act
        _buffer = new AgentOutputBuffer(maxLines: 100);

        // Assert
        Assert.NotNull(_buffer);
        Assert.Equal(0, _buffer.Count);
    }

    [Fact]
    public void Constructor_WithDefaultMaxLines_CreatesInstance()
    {
        // Arrange & Act
        _buffer = new AgentOutputBuffer();

        // Assert
        Assert.NotNull(_buffer);
        Assert.Equal(0, _buffer.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidMaxLines_ThrowsArgumentOutOfRangeException(int invalidMaxLines)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new AgentOutputBuffer(maxLines: invalidMaxLines));

        Assert.Equal("maxLines", exception.ParamName);
        Assert.Contains("maxLines must be greater than 0", exception.Message);
    }

    #endregion

    #region AppendLineAsync Tests

    [Fact]
    public async Task AppendLineAsync_WithNullLine_ThrowsArgumentNullException()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _buffer.AppendLineAsync(null!));

        Assert.Equal("line", exception.ParamName);
    }

    [Fact]
    public async Task AppendLineAsync_WithEmptyLine_AddsToBuffer()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();

        // Act
        await _buffer.AppendLineAsync(string.Empty);

        // Assert
        Assert.Equal(1, _buffer.Count);
        var lines = await _buffer.GetLastLinesAsync(1);
        Assert.Single(lines);
        Assert.Equal(string.Empty, lines[0]);
    }

    [Fact]
    public async Task AppendLineAsync_WithValidLine_IncreasesCount()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();

        // Act
        await _buffer.AppendLineAsync("Test line");

        // Assert
        Assert.Equal(1, _buffer.Count);
    }

    [Fact]
    public async Task AppendLineAsync_WithMultipleLines_AddsAllLines()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        var lines = new[] { "Line 1", "Line 2", "Line 3" };

        // Act
        foreach (var line in lines)
        {
            await _buffer.AppendLineAsync(line);
        }

        // Assert
        Assert.Equal(3, _buffer.Count);
        var retrievedLines = await _buffer.GetLastLinesAsync(3);
        Assert.Equal(lines, retrievedLines);
    }

    [Fact]
    public async Task AppendLineAsync_WhenCalled_RaisesLineAddedEvent()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        var eventRaised = false;
        string? capturedLine = null;
        DateTime? capturedTimestamp = null;

        _buffer.LineAdded += (sender, e) =>
        {
            eventRaised = true;
            capturedLine = e.Line;
            capturedTimestamp = e.Timestamp;
        };

        // Act
        var testLine = "Test event line";
        var beforeTime = DateTime.UtcNow;
        await _buffer.AppendLineAsync(testLine);
        var afterTime = DateTime.UtcNow;

        // Assert
        Assert.True(eventRaised);
        Assert.Equal(testLine, capturedLine);
        Assert.NotNull(capturedTimestamp);
        Assert.True(capturedTimestamp >= beforeTime);
        Assert.True(capturedTimestamp <= afterTime);
    }

    [Fact]
    public async Task AppendLineAsync_WithConcurrentAppends_MaintainsThreadSafety()
    {
        // Arrange
        _buffer = new AgentOutputBuffer(maxLines: 1000);
        const int concurrentTasks = 10;
        const int linesPerTask = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < concurrentTasks; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < linesPerTask; j++)
                {
                    await _buffer.AppendLineAsync($"Task {taskId} Line {j}");
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentTasks * linesPerTask, _buffer.Count);
    }

    #endregion

    #region GetLastLinesAsync Tests

    [Fact]
    public async Task GetLastLinesAsync_FromEmptyBuffer_ReturnsEmptyList()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();

        // Act
        var lines = await _buffer.GetLastLinesAsync(10);

        // Assert
        Assert.Empty(lines);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetLastLinesAsync_WithInvalidCount_ThrowsArgumentOutOfRangeException(int invalidCount)
    {
        // Arrange
        _buffer = new AgentOutputBuffer();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _buffer.GetLastLinesAsync(invalidCount));

        Assert.Equal("count", exception.ParamName);
        Assert.Contains("count must be greater than 0", exception.Message);
    }

    [Fact]
    public async Task GetLastLinesAsync_WithCountLessThanAvailable_ReturnsRequestedCount()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        await _buffer.AppendLineAsync("Line 1");
        await _buffer.AppendLineAsync("Line 2");
        await _buffer.AppendLineAsync("Line 3");
        await _buffer.AppendLineAsync("Line 4");
        await _buffer.AppendLineAsync("Line 5");

        // Act
        var lines = await _buffer.GetLastLinesAsync(3);

        // Assert
        Assert.Equal(3, lines.Count);
        Assert.Equal(new[] { "Line 3", "Line 4", "Line 5" }, lines);
    }

    [Fact]
    public async Task GetLastLinesAsync_WithCountMoreThanAvailable_ReturnsAllLines()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        await _buffer.AppendLineAsync("Line 1");
        await _buffer.AppendLineAsync("Line 2");
        await _buffer.AppendLineAsync("Line 3");

        // Act
        var lines = await _buffer.GetLastLinesAsync(100);

        // Assert
        Assert.Equal(3, lines.Count);
        Assert.Equal(new[] { "Line 1", "Line 2", "Line 3" }, lines);
    }

    #endregion

    #region GetLinesAsync Tests

    [Fact]
    public async Task GetLinesAsync_FromEmptyBuffer_ReturnsEmptyEnumerable()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();

        // Act
        var lines = new List<string>();
        await foreach (var line in _buffer.GetLinesAsync())
        {
            lines.Add(line);
        }

        // Assert
        Assert.Empty(lines);
    }

    [Fact]
    public async Task GetLinesAsync_WithoutFilter_ReturnsAllLines()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        await _buffer.AppendLineAsync("Line 1");
        await _buffer.AppendLineAsync("Line 2");
        await _buffer.AppendLineAsync("Line 3");

        // Act
        var lines = new List<string>();
        await foreach (var line in _buffer.GetLinesAsync())
        {
            lines.Add(line);
        }

        // Assert
        Assert.Equal(3, lines.Count);
        Assert.Equal(new[] { "Line 1", "Line 2", "Line 3" }, lines);
    }

    [Fact]
    public async Task GetLinesAsync_WithRegexFilter_ReturnsMatchingLinesOnly()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        await _buffer.AppendLineAsync("ERROR: Something went wrong");
        await _buffer.AppendLineAsync("INFO: Operation succeeded");
        await _buffer.AppendLineAsync("ERROR: Another error");
        await _buffer.AppendLineAsync("DEBUG: Debug message");

        // Act
        var lines = new List<string>();
        await foreach (var line in _buffer.GetLinesAsync(regexFilter: "^ERROR:"))
        {
            lines.Add(line);
        }

        // Assert
        Assert.Equal(2, lines.Count);
        Assert.Equal(new[] { "ERROR: Something went wrong", "ERROR: Another error" }, lines);
    }

    [Fact]
    public async Task GetLinesAsync_WithComplexRegexFilter_ReturnsMatchingLines()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        await _buffer.AppendLineAsync("User: john@example.com logged in");
        await _buffer.AppendLineAsync("User: jane@test.com logged out");
        await _buffer.AppendLineAsync("System message");
        await _buffer.AppendLineAsync("User: bob@company.org registered");

        // Act
        var lines = new List<string>();
        await foreach (var line in _buffer.GetLinesAsync(regexFilter: @"User: \w+@\w+\.\w+"))
        {
            lines.Add(line);
        }

        // Assert
        Assert.Equal(3, lines.Count);
        Assert.DoesNotContain("System message", lines);
    }

    [Fact]
    public async Task GetLinesAsync_WithInvalidRegex_ThrowsArgumentException()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        await _buffer.AppendLineAsync("Test line");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var line in _buffer.GetLinesAsync(regexFilter: "[invalid(regex"))
            {
                // Should not reach here
            }
        });

        Assert.Equal("regexFilter", exception.ParamName);
        Assert.Contains("Invalid regex pattern", exception.Message);
    }

    [Fact]
    public async Task GetLinesAsync_WithCancellation_StopsIteration()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        for (int i = 0; i < 100; i++)
        {
            await _buffer.AppendLineAsync($"Line {i}");
        }

        var cts = new CancellationTokenSource();
        var lines = new List<string>();

        // Act
        try
        {
            await foreach (var line in _buffer.GetLinesAsync(cancellationToken: cts.Token))
            {
                lines.Add(line);
                if (lines.Count >= 10)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        Assert.Equal(10, lines.Count);
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_WithNonEmptyBuffer_RemovesAllLines()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        await _buffer.AppendLineAsync("Line 1");
        await _buffer.AppendLineAsync("Line 2");
        await _buffer.AppendLineAsync("Line 3");

        // Act
        await _buffer.ClearAsync();

        // Assert
        Assert.Equal(0, _buffer.Count);
        var lines = await _buffer.GetLastLinesAsync(10);
        Assert.Empty(lines);
    }

    [Fact]
    public async Task ClearAsync_WithEmptyBuffer_DoesNotThrow()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();

        // Act
        var exception = await Record.ExceptionAsync(async () => await _buffer.ClearAsync());

        // Assert
        Assert.Null(exception);
        Assert.Equal(0, _buffer.Count);
    }

    #endregion

    #region Count Property Tests

    [Fact]
    public void Count_OnNewBuffer_ReturnsZero()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();

        // Act
        var count = _buffer.Count;

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Count_AfterAppendingLines_ReturnsCorrectCount()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();

        // Act
        await _buffer.AppendLineAsync("Line 1");
        await _buffer.AppendLineAsync("Line 2");
        await _buffer.AppendLineAsync("Line 3");

        // Assert
        Assert.Equal(3, _buffer.Count);
    }

    [Fact]
    public async Task Count_AfterClear_ReturnsZero()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        await _buffer.AppendLineAsync("Line 1");
        await _buffer.AppendLineAsync("Line 2");

        // Act
        await _buffer.ClearAsync();

        // Assert
        Assert.Equal(0, _buffer.Count);
    }

    #endregion

    #region Circular Buffer Behavior Tests

    [Fact]
    public async Task CircularBuffer_WhenExceedingMaxLines_ReplacesOldestLines()
    {
        // Arrange
        _buffer = new AgentOutputBuffer(maxLines: 5);

        // Act - Add 7 lines to buffer with capacity 5
        for (int i = 1; i <= 7; i++)
        {
            await _buffer.AppendLineAsync($"Line {i}");
        }

        // Assert
        Assert.Equal(5, _buffer.Count); // Capped at maxLines
        var lines = await _buffer.GetLastLinesAsync(10);
        Assert.Equal(5, lines.Count);
        Assert.Equal(new[] { "Line 3", "Line 4", "Line 5", "Line 6", "Line 7" }, lines);
        Assert.DoesNotContain("Line 1", lines);
        Assert.DoesNotContain("Line 2", lines);
    }

    [Fact]
    public async Task CircularBuffer_MaintainsFIFOOrder_WhenOverflowing()
    {
        // Arrange
        _buffer = new AgentOutputBuffer(maxLines: 3);

        // Act
        await _buffer.AppendLineAsync("First");
        await _buffer.AppendLineAsync("Second");
        await _buffer.AppendLineAsync("Third");
        await _buffer.AppendLineAsync("Fourth");  // Should replace "First"
        await _buffer.AppendLineAsync("Fifth");   // Should replace "Second"

        // Assert
        var lines = await _buffer.GetLastLinesAsync(10);
        Assert.Equal(new[] { "Third", "Fourth", "Fifth" }, lines);
    }

    [Fact]
    public async Task CircularBuffer_CountCappedAtMaxLines_EvenWithManyAppends()
    {
        // Arrange
        const int maxLines = 100;
        _buffer = new AgentOutputBuffer(maxLines: maxLines);

        // Act - Add 1000 lines
        for (int i = 0; i < 1000; i++)
        {
            await _buffer.AppendLineAsync($"Line {i}");
        }

        // Assert
        Assert.Equal(maxLines, _buffer.Count);
    }

    #endregion

    #region IDisposable Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes_WithoutError()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();

        // Act
        var exception = Record.Exception(() =>
        {
            _buffer.Dispose();
            _buffer.Dispose();
            _buffer.Dispose();
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Count_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        _buffer.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _buffer.Count);
    }

    [Fact]
    public async Task AppendLineAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        _buffer.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await _buffer.AppendLineAsync("Test"));
    }

    [Fact]
    public async Task GetLastLinesAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        _buffer.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await _buffer.GetLastLinesAsync(10));
    }

    [Fact]
    public async Task GetLinesAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        _buffer.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await foreach (var line in _buffer.GetLinesAsync())
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task ClearAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        _buffer.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await _buffer.ClearAsync());
    }

    #endregion

    #region Thread-Safety Tests

    [Fact]
    public async Task ConcurrentAppendAndRead_DoNotCauseDeadlock()
    {
        // Arrange
        _buffer = new AgentOutputBuffer(maxLines: 1000);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var appendTask = Task.Run(async () =>
        {
            for (int i = 0; i < 500; i++)
            {
                await _buffer.AppendLineAsync($"Line {i}");
                await Task.Delay(1); // Small delay to ensure interleaving
            }
        }, cts.Token);

        var readTask = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                var lines = await _buffer.GetLastLinesAsync(10);
                await Task.Delay(5);
            }
        }, cts.Token);

        // Assert
        var exception = await Record.ExceptionAsync(async () => await Task.WhenAll(appendTask, readTask));
        Assert.Null(exception);
    }

    [Fact]
    public async Task ConcurrentReads_DoNotCorruptData()
    {
        // Arrange
        _buffer = new AgentOutputBuffer();
        for (int i = 0; i < 100; i++)
        {
            await _buffer.AppendLineAsync($"Line {i}");
        }

        // Act
        var tasks = new List<Task<IReadOnlyList<string>>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_buffer.GetLastLinesAsync(50));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        foreach (var result in results)
        {
            Assert.Equal(50, result.Count);

            // Verify lines are in ascending order
            for (int i = 1; i < result.Count; i++)
            {
                var currentNum = int.Parse(result[i].Split(' ')[1]);
                var previousNum = int.Parse(result[i - 1].Split(' ')[1]);
                Assert.True(currentNum > previousNum);
            }
        }
    }

    [Fact]
    public async Task ConcurrentClearAndAppend_MaintainsConsistency()
    {
        // Arrange
        _buffer = new AgentOutputBuffer(maxLines: 100);

        // Act
        var tasks = new List<Task>();

        // Start 5 append tasks
        for (int i = 0; i < 5; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < 20; j++)
                {
                    await _buffer.AppendLineAsync($"Task {taskId} Line {j}");
                }
            }));
        }

        // Start 2 clear tasks
        tasks.Add(Task.Run(async () =>
        {
            await Task.Delay(10);
            await _buffer.ClearAsync();
        }));

        tasks.Add(Task.Run(async () =>
        {
            await Task.Delay(20);
            await _buffer.ClearAsync();
        }));

        await Task.WhenAll(tasks);

        // Assert - Count should be valid (not negative, not exceeding max)
        Assert.InRange(_buffer.Count, 0, 100);
    }

    #endregion
}
