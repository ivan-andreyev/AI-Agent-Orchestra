using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Models.Workflow;
using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests.Services
{
    public class MarkdownFileWatcherTests : IDisposable
    {
        private readonly Mock<ILogger<MarkdownFileWatcher>> _loggerMock;
        private readonly Mock<IMarkdownWorkflowParser> _parserMock;
        private readonly MarkdownFileWatcher _watcher;
        private readonly string _testDirectory;

        public MarkdownFileWatcherTests()
        {
            _loggerMock = new Mock<ILogger<MarkdownFileWatcher>>();
            _parserMock = new Mock<IMarkdownWorkflowParser>();
            _watcher = new MarkdownFileWatcher(_loggerMock.Object, _parserMock.Object);

            _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        [Fact]
        public async Task StartWatchingAsync_ValidDirectory_SetsIsWatchingTrue()
        {
            // Arrange & Act
            await _watcher.StartWatchingAsync(_testDirectory);

            // Assert
            Assert.True(_watcher.IsWatching);
            Assert.Equal(_testDirectory, _watcher.WatchedDirectory);
        }

        [Fact]
        public async Task StartWatchingAsync_InvalidDirectory_ThrowsException()
        {
            // Arrange
            var invalidDirectory = Path.Combine(_testDirectory, "nonexistent");

            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => _watcher.StartWatchingAsync(invalidDirectory));
        }

        [Fact]
        public async Task StartWatchingAsync_NullOrEmptyPath_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _watcher.StartWatchingAsync(string.Empty));
            await Assert.ThrowsAsync<ArgumentException>(() => _watcher.StartWatchingAsync("   "));
        }

        [Fact]
        public async Task AddFileToWatchAsync_ValidMarkdownFile_AddsToWatchList()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "test.md");
            await File.WriteAllTextAsync(testFile, "# Test Workflow");

            // Act
            await _watcher.AddFileToWatchAsync(testFile);
            var watchedFiles = await _watcher.GetWatchedFilesAsync();

            // Assert
            Assert.Contains(testFile, watchedFiles);
        }

        [Fact]
        public async Task AddFileToWatchAsync_NonMarkdownFile_ThrowsException()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "test.txt");
            await File.WriteAllTextAsync(testFile, "Not markdown");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _watcher.AddFileToWatchAsync(testFile));
        }

        [Fact]
        public async Task AddFileToWatchAsync_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.md");

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _watcher.AddFileToWatchAsync(nonExistentFile));
        }

        [Fact]
        public async Task AddFileToWatchAsync_NullOrEmptyPath_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _watcher.AddFileToWatchAsync(string.Empty));
            await Assert.ThrowsAsync<ArgumentException>(() => _watcher.AddFileToWatchAsync("   "));
        }

        [Fact]
        public async Task RemoveFileFromWatchAsync_ExistingFile_RemovesFromWatchList()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "test.md");
            await File.WriteAllTextAsync(testFile, "# Test Workflow");
            await _watcher.AddFileToWatchAsync(testFile);

            // Verify file is watched
            var watchedFilesBefore = await _watcher.GetWatchedFilesAsync();
            Assert.Contains(testFile, watchedFilesBefore);

            // Act
            await _watcher.RemoveFileFromWatchAsync(testFile);

            // Assert
            var watchedFilesAfter = await _watcher.GetWatchedFilesAsync();
            Assert.DoesNotContain(testFile, watchedFilesAfter);
        }

        [Fact]
        public async Task RemoveFileFromWatchAsync_NonWatchedFile_DoesNotThrow()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "test.md");

            // Act & Assert - Should not throw
            await _watcher.RemoveFileFromWatchAsync(testFile);
        }

        [Fact]
        public async Task RemoveFileFromWatchAsync_NullOrEmptyPath_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            await _watcher.RemoveFileFromWatchAsync(string.Empty);
            await _watcher.RemoveFileFromWatchAsync("   ");
        }

        [Fact]
        public async Task GetWatchedFilesAsync_EmptyWatcher_ReturnsEmptyList()
        {
            // Act
            var watchedFiles = await _watcher.GetWatchedFilesAsync();

            // Assert
            Assert.Empty(watchedFiles);
        }

        [Fact]
        public async Task GetWatchedFilesAsync_WithWatchedFiles_ReturnsCorrectList()
        {
            // Arrange
            var testFile1 = Path.Combine(_testDirectory, "test1.md");
            var testFile2 = Path.Combine(_testDirectory, "test2.md");
            await File.WriteAllTextAsync(testFile1, "# Test Workflow 1");
            await File.WriteAllTextAsync(testFile2, "# Test Workflow 2");

            await _watcher.AddFileToWatchAsync(testFile1);
            await _watcher.AddFileToWatchAsync(testFile2);

            // Act
            var watchedFiles = await _watcher.GetWatchedFilesAsync();

            // Assert
            Assert.Equal(2, watchedFiles.Count);
            Assert.Contains(testFile1, watchedFiles);
            Assert.Contains(testFile2, watchedFiles);
        }

        [Fact]
        public async Task StopWatchingAsync_WhenWatching_SetsIsWatchingFalse()
        {
            // Arrange
            await _watcher.StartWatchingAsync(_testDirectory);
            Assert.True(_watcher.IsWatching);

            // Act
            await _watcher.StopWatchingAsync();

            // Assert
            Assert.False(_watcher.IsWatching);
        }

        [Fact]
        public async Task StopWatchingAsync_WhenNotWatching_DoesNotThrow()
        {
            // Arrange - Watcher is not started

            // Act & Assert - Should not throw
            await _watcher.StopWatchingAsync();
        }

        [Fact]
        public async Task StopWatchingAsync_ClearsWatchedFiles()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "test.md");
            await File.WriteAllTextAsync(testFile, "# Test Workflow");
            await _watcher.AddFileToWatchAsync(testFile);

            var watchedFilesBefore = await _watcher.GetWatchedFilesAsync();
            Assert.NotEmpty(watchedFilesBefore);

            // Act
            await _watcher.StopWatchingAsync();

            // Assert
            var watchedFilesAfter = await _watcher.GetWatchedFilesAsync();
            Assert.Empty(watchedFilesAfter);
        }

        [Fact]
        public async Task FileCreated_ValidMarkdownFile_RaisesEvent()
        {
            // Arrange
            var eventRaised = false;
            MarkdownFileCreatedEventArgs? eventArgs = null;

            _parserMock.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new MarkdownWorkflowParseResult(
                    IsSuccess: true,
                    Workflow: CreateTestWorkflow()));

            _watcher.MarkdownFileCreated += (_, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            await _watcher.StartWatchingAsync(_testDirectory);
            var testFile = Path.Combine(_testDirectory, "new.md");

            // Act
            await File.WriteAllTextAsync(testFile, "# Test Workflow");

            // Wait for file system events to process
            await Task.Delay(1000);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(eventArgs);
            Assert.Equal(testFile, eventArgs.FilePath);
            Assert.Equal("new.md", eventArgs.FileName);
        }

        [Fact]
        public async Task FileCreated_InvalidWorkflowFile_DoesNotRaiseEvent()
        {
            // Arrange
            var eventRaised = false;

            _parserMock.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new MarkdownWorkflowParseResult(
                    IsSuccess: false,
                    ErrorMessage: "Invalid workflow"));

            _watcher.MarkdownFileCreated += (_, _) => eventRaised = true;

            await _watcher.StartWatchingAsync(_testDirectory);
            var testFile = Path.Combine(_testDirectory, "invalid.md");

            // Act
            await File.WriteAllTextAsync(testFile, "Invalid content");

            // Wait for file system events to process
            await Task.Delay(1000);

            // Assert
            Assert.False(eventRaised);
        }

        [Fact]
        public async Task Dispose_WhenWatching_StopsWatching()
        {
            // Arrange
            await _watcher.StartWatchingAsync(_testDirectory);
            Assert.True(_watcher.IsWatching);

            // Act
            _watcher.Dispose();

            // Assert
            Assert.False(_watcher.IsWatching);
        }

        [Fact]
        public void Dispose_WhenAlreadyDisposed_DoesNotThrow()
        {
            // Arrange
            _watcher.Dispose();

            // Act & Assert - Should not throw
            _watcher.Dispose();
        }

        [Theory]
        [InlineData("workflow.md")]
        [InlineData("test-workflow.md")]
        [InlineData("my_workflow.md")]
        [InlineData("WORKFLOW.MD")]
        public async Task AddFileToWatchAsync_VariousMarkdownExtensions_Succeeds(string fileName)
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, fileName);
            await File.WriteAllTextAsync(testFile, "# Test Workflow");

            // Act & Assert - Should not throw
            await _watcher.AddFileToWatchAsync(testFile);

            var watchedFiles = await _watcher.GetWatchedFilesAsync();
            Assert.Contains(testFile, watchedFiles);
        }

        [Theory]
        [InlineData("workflow.txt")]
        [InlineData("workflow.doc")]
        [InlineData("workflow")]
        [InlineData("workflow.markdown")]
        public async Task AddFileToWatchAsync_NonMarkdownFiles_ThrowsException(string fileName)
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, fileName);
            await File.WriteAllTextAsync(testFile, "Content");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _watcher.AddFileToWatchAsync(testFile));
        }

        private static MarkdownWorkflow CreateTestWorkflow()
        {
            return new MarkdownWorkflow(
                Id: "test-workflow",
                Name: "Test Workflow",
                SourceFilePath: "test.md",
                Metadata: new MarkdownWorkflowMetadata(
                    Author: "Test Author",
                    Version: "1.0",
                    Tags: new List<string> { "test" }),
                Variables: new Dictionary<string, MarkdownWorkflowVariable>(),
                Steps: new List<MarkdownWorkflowStep>
                {
                    new("step1", "Test Step", "task", "echo hello",
                        new Dictionary<string, string>(), new List<string>())
                },
                ParsedAt: DateTime.UtcNow,
                FileHash: "test-hash"
            );
        }

        public void Dispose()
        {
            _watcher?.Dispose();

            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }
    }
}