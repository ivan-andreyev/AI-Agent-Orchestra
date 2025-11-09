using Microsoft.Extensions.Logging;
using Moq;
using Orchestra.Core.Services;
using Orchestra.Core.Services.Connectors;
using Xunit;

namespace Orchestra.Tests.Services;

/// <summary>
/// Тесты для PermissionDenialDetectionService
/// </summary>
public class PermissionDenialDetectionServiceTests
{
    private readonly Mock<ILogger<PermissionDenialDetectionService>> _mockLogger;

    public PermissionDenialDetectionServiceTests()
    {
        _mockLogger = new Mock<ILogger<PermissionDenialDetectionService>>();
    }

    [Fact]
    public void TryParseResponse_WithValidJsonContainingPermissionDenials_ReturnsParsedResponse()
    {
        // Arrange
        var service = new PermissionDenialDetectionService(_mockLogger.Object);

        var json = """
            {
                "type": "result",
                "subtype": "permission_denied",
                "is_error": true,
                "result": "Permission denied for tool execution",
                "session_id": "test-session",
                "permission_denials": [
                    {
                        "tool_name": "Bash",
                        "tool_use_id": "tool-123",
                        "tool_input": {
                            "command": "rm -rf /"
                        }
                    }
                ]
            }
            """;

        // Act
        var result = service.TryParseResponse(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("result", result.Type);
        Assert.True(result.IsError);
        Assert.NotNull(result.PermissionDenials);
        Assert.Single(result.PermissionDenials);
        Assert.Equal("Bash", result.PermissionDenials[0].ToolName);
        Assert.Equal("tool-123", result.PermissionDenials[0].ToolUseId);
    }

    [Fact]
    public void TryParseResponse_WithValidJsonNoPermissionDenials_ReturnsParsedResponse()
    {
        // Arrange
        var service = new PermissionDenialDetectionService(_mockLogger.Object);

        var json = """
            {
                "type": "result",
                "subtype": "success",
                "is_error": false,
                "result": "Command executed successfully",
                "session_id": "test-session"
            }
            """;

        // Act
        var result = service.TryParseResponse(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("success", result.Subtype);
        Assert.False(result.IsError);
        Assert.Null(result.PermissionDenials);
    }

    [Fact]
    public void TryParseResponse_WithMultiplePermissionDenials_ReturnAll()
    {
        // Arrange
        var service = new PermissionDenialDetectionService(_mockLogger.Object);

        var json = """
            {
                "type": "result",
                "subtype": "permission_denied",
                "is_error": true,
                "permission_denials": [
                    {
                        "tool_name": "Bash",
                        "tool_use_id": "bash-123"
                    },
                    {
                        "tool_name": "Write",
                        "tool_use_id": "write-456"
                    },
                    {
                        "tool_name": "Edit",
                        "tool_use_id": "edit-789"
                    }
                ]
            }
            """;

        // Act
        var result = service.TryParseResponse(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.PermissionDenials!.Count);
    }

    [Fact]
    public void TryParseResponse_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var service = new PermissionDenialDetectionService(_mockLogger.Object);

        var invalidJson = "{ invalid json }";

        // Act
        var result = service.TryParseResponse(invalidJson);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryParseResponse_WithNullOrEmptyString_ReturnsNull()
    {
        // Arrange
        var service = new PermissionDenialDetectionService(_mockLogger.Object);

        // Act
        var resultNull = service.TryParseResponse(null!);
        var resultEmpty = service.TryParseResponse("");
        var resultWhitespace = service.TryParseResponse("   ");

        // Assert
        Assert.Null(resultNull);
        Assert.Null(resultEmpty);
        Assert.Null(resultWhitespace);
    }

    [Fact]
    public void HasPermissionDenials_WithPermissionDenials_ReturnsTrue()
    {
        // Arrange
        var service = new PermissionDenialDetectionService(_mockLogger.Object);

        var response = new ClaudeResponse
        {
            Type = "result",
            PermissionDenials = new List<PermissionDenial>
            {
                new() { ToolName = "Bash", ToolUseId = "test" }
            }
        };

        // Act
        var result = service.HasPermissionDenials(response);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasPermissionDenials_WithoutPermissionDenials_ReturnsFalse()
    {
        // Arrange
        var service = new PermissionDenialDetectionService(_mockLogger.Object);

        var response = new ClaudeResponse
        {
            Type = "result",
            PermissionDenials = null
        };

        // Act
        var result = service.HasPermissionDenials(response);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasPermissionDenials_WithEmptyPermissionDenialsList_ReturnsFalse()
    {
        // Arrange
        var service = new PermissionDenialDetectionService(_mockLogger.Object);

        var response = new ClaudeResponse
        {
            Type = "result",
            PermissionDenials = new List<PermissionDenial>()
        };

        // Act
        var result = service.HasPermissionDenials(response);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasPermissionDenials_WithNullResponse_ReturnsFalse()
    {
        // Arrange
        var service = new PermissionDenialDetectionService(_mockLogger.Object);

        // Act
        var result = service.HasPermissionDenials(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryParseResponse_CaseInsensitivePropertyNames_ParsesCorrectly()
    {
        // Arrange
        var service = new PermissionDenialDetectionService(_mockLogger.Object);

        // JSON с различными регистрами символов
        var json = """
            {
                "Type": "result",
                "Subtype": "permission_denied",
                "Is_Error": true,
                "Permission_Denials": [
                    {
                        "Tool_Name": "Bash",
                        "Tool_Use_Id": "test-id"
                    }
                ]
            }
            """;

        // Act
        var result = service.TryParseResponse(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("result", result.Type);
    }
}
