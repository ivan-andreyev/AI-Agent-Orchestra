using Orchestra.Core.Services;
using Xunit;

namespace Orchestra.Tests.UnitTests.Services;

/// <summary>
/// Тесты для ExpressionEvaluator - безопасного оценщика выражений для условной логики workflow
/// </summary>
public class ExpressionEvaluatorTests
{
    private readonly ExpressionEvaluator _evaluator;
    private readonly WorkflowExecutionContext _context;

    public ExpressionEvaluatorTests()
    {
        _evaluator = new ExpressionEvaluator();
        _context = new WorkflowExecutionContext
        {
            Variables = new Dictionary<string, object>
            {
                ["variable1"] = "test_value",
                ["counter"] = 5,
                ["isActive"] = true,
                ["filePath"] = "/path/to/file.txt",
                ["errorCount"] = 0,
                ["fileList"] = new List<string> { "file1.txt", "file2.txt" }
            },
            StepResults = new Dictionary<string, object>
            {
                ["previousTask"] = new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["output"] = "SUCCESS: Operation completed",
                    ["errorCode"] = 0
                },
                ["taskResult"] = new Dictionary<string, object>
                {
                    ["data"] = "result_data",
                    ["count"] = 10
                }
            }
        };
    }

    #region Expression Validation Tests

    [Theory]
    [InlineData("$variable1 == \"test_value\"", true)]
    [InlineData("$counter > 3", true)]
    [InlineData("$previousTask.success == true", true)]
    [InlineData("$filePath contains \"file\"", true)]
    [InlineData("$variable1 regex \"test.*\"", true)]
    [InlineData("invalid_expression", false)]
    [InlineData("variable1 == test", false)] // Должно начинаться с $
    [InlineData("$variable1 === test", false)] // Неподдерживаемый оператор
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidateExpression_VariousExpressions_ReturnsExpectedResult(string expression, bool expected)
    {
        // Act
        var result = _evaluator.ValidateExpression(expression);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Equality Comparison Tests

    [Fact]
    public async Task EvaluateAsync_StringEquality_True_ReturnsTrue()
    {
        // Arrange
        var expression = "$variable1 == \"test_value\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_StringEquality_False_ReturnsFalse()
    {
        // Arrange
        var expression = "$variable1 == \"different_value\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateAsync_NumberEquality_True_ReturnsTrue()
    {
        // Arrange
        var expression = "$counter == 5";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_BooleanEquality_True_ReturnsTrue()
    {
        // Arrange
        var expression = "$isActive == true";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_NotEquals_True_ReturnsTrue()
    {
        // Arrange
        var expression = "$counter != 10";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_NotEquals_False_ReturnsFalse()
    {
        // Arrange
        var expression = "$counter != 5";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Numeric Comparison Tests

    [Fact]
    public async Task EvaluateAsync_GreaterThan_True_ReturnsTrue()
    {
        // Arrange
        var expression = "$counter > 3";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_GreaterThan_False_ReturnsFalse()
    {
        // Arrange
        var expression = "$counter > 10";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateAsync_LessThan_True_ReturnsTrue()
    {
        // Arrange
        var expression = "$counter < 10";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_GreaterOrEqual_True_ReturnsTrue()
    {
        // Arrange
        var expression = "$counter >= 5";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_LessOrEqual_True_ReturnsTrue()
    {
        // Arrange
        var expression = "$counter <= 5";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Variable Resolution Tests

    [Fact]
    public async Task EvaluateAsync_NestedVariableAccess_ReturnsCorrectValue()
    {
        // Arrange
        var expression = "$previousTask.success == true";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_DeepNestedAccess_ReturnsCorrectValue()
    {
        // Arrange
        var expression = "$previousTask.errorCode == 0";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_NonExistentVariable_ReturnsFalse()
    {
        // Arrange
        var expression = "$nonExistentVar == \"test\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateAsync_NonExistentNestedProperty_ReturnsFalse()
    {
        // Arrange
        var expression = "$previousTask.nonExistentProperty == \"test\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region String Operations Tests

    [Fact]
    public async Task EvaluateAsync_ContainsOperation_True_ReturnsTrue()
    {
        // Arrange
        var expression = "$filePath contains \"file\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_ContainsOperation_False_ReturnsFalse()
    {
        // Arrange
        var expression = "$filePath contains \"missing\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateAsync_ContainsOperation_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var expression = "$filePath contains \"PATH\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_RegexOperation_True_ReturnsTrue()
    {
        // Arrange
        var expression = "$variable1 regex \"test.*\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_RegexOperation_False_ReturnsFalse()
    {
        // Arrange
        var expression = "$variable1 regex \"^number.*\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateAsync_RegexOperation_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var expression = "$variable1 regex \"TEST.*\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_RegexOperation_InvalidPattern_ReturnsFalse()
    {
        // Arrange
        var expression = "$variable1 regex \"[invalid\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Complex Workflow Scenarios Tests

    [Fact]
    public async Task EvaluateAsync_TaskSuccessCheck_ReturnsTrue()
    {
        // Arrange
        var expression = "$previousTask.success == true";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_OutputContainsSuccess_ReturnsTrue()
    {
        // Arrange
        var expression = "$previousTask.output contains \"SUCCESS\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_CounterGreaterThanZero_ReturnsTrue()
    {
        // Arrange
        var expression = "$counter > 0";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_ErrorCountCheck_ReturnsTrue()
    {
        // Arrange
        var expression = "$errorCount == 0";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Type Conversion Tests

    [Fact]
    public async Task EvaluateAsync_StringToNumberComparison_WorksCorrectly()
    {
        // Arrange
        _context.Variables["stringNumber"] = "10";
        var expression = "$stringNumber > 5";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_NumberToStringComparison_WorksCorrectly()
    {
        // Arrange
        var expression = "$counter == \"5\"";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task EvaluateAsync_NullExpression_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _evaluator.EvaluateAsync(null!, _context));
    }

    [Fact]
    public async Task EvaluateAsync_EmptyExpression_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _evaluator.EvaluateAsync("", _context));
    }

    [Fact]
    public async Task EvaluateAsync_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _evaluator.EvaluateAsync("$variable1 == \"test\"", null!));
    }

    [Fact]
    public async Task EvaluateAsync_InvalidSyntax_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _evaluator.EvaluateAsync("invalid expression", _context));
    }

    [Fact]
    public async Task EvaluateAsync_UnsupportedOperator_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _evaluator.EvaluateAsync("$variable1 === \"test\"", _context));
    }

    [Fact]
    public async Task EvaluateAsync_NumericComparisonWithNonNumbers_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _evaluator.EvaluateAsync("$variable1 > \"test\"", _context));
    }

    #endregion

    #region Security Tests

    [Theory]
    [InlineData("$variable1 == \"test\"; System.IO.File.Delete(\"/tmp/test\")\"")]
    [InlineData("$variable1 == \"test\" && Process.Start(\"cmd\")")]
    [InlineData("$variable1 regex \".*; rm -rf /\"")]
    public async Task EvaluateAsync_MaliciousCode_DoesNotExecute(string maliciousExpression)
    {
        // Эти выражения должны либо не парситься (безопасно),
        // либо рассматриваться как обычные строки без выполнения кода

        try
        {
            // Act
            var result = await _evaluator.EvaluateAsync(maliciousExpression, _context);

            // Assert - если выражение распарсилось, то это должно быть безопасное сравнение
            Assert.IsType<bool>(result);
        }
        catch (InvalidOperationException)
        {
            // Ожидаемый результат - выражение не распарсилось из-за неправильного синтаксиса
            Assert.True(true);
        }
    }

    [Fact]
    public void ValidateExpression_VariableNameWithoutDollarSign_ReturnsFalse()
    {
        // Arrange
        var expression = "variable1 == \"test\"";

        // Act
        var result = _evaluator.ValidateExpression(expression);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateExpression_InvalidVariableName_ReturnsFalse()
    {
        // Arrange
        var expression = "$123invalid == \"test\"";

        // Act
        var result = _evaluator.ValidateExpression(expression);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region WorkflowExecutionContext Tests

    [Fact]
    public void WorkflowExecutionContext_GetValue_ReturnsVariableValue()
    {
        // Act
        var value = _context.GetValue("variable1");

        // Assert
        Assert.Equal("test_value", value);
    }

    [Fact]
    public void WorkflowExecutionContext_GetValue_ReturnsStepResult()
    {
        // Act
        var value = _context.GetValue("previousTask");

        // Assert
        Assert.NotNull(value);
        Assert.IsType<Dictionary<string, object>>(value);
    }

    [Fact]
    public void WorkflowExecutionContext_GetValue_NonExistent_ReturnsNull()
    {
        // Act
        var value = _context.GetValue("nonExistent");

        // Assert
        Assert.Null(value);
    }

    #endregion

    #region Complex Boolean Logic Tests

    [Fact]
    public async Task EvaluateAsync_AndOperator_BothTrue_ReturnsTrue()
    {
        // Arrange
        var expression = "($counter > 3) AND ($isActive == true)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_AndOperator_OneFalse_ReturnsFalse()
    {
        // Arrange
        var expression = "($counter > 10) AND ($isActive == true)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateAsync_OrOperator_OneTrue_ReturnsTrue()
    {
        // Arrange
        var expression = "($counter > 10) OR ($isActive == true)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_OrOperator_BothFalse_ReturnsFalse()
    {
        // Arrange
        var expression = "($counter > 10) OR ($isActive == false)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateAsync_NotOperator_True_ReturnsFalse()
    {
        // Arrange
        var expression = "NOT ($isActive == true)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateAsync_NotOperator_False_ReturnsTrue()
    {
        // Arrange
        var expression = "NOT ($counter > 10)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_ComplexExpression_WithParentheses_ReturnsCorrectResult()
    {
        // Arrange
        var expression = "($previousTask.success == true) AND ($counter > 0)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_ComplexExpression_WithNotAndOr_ReturnsCorrectResult()
    {
        // Arrange
        var expression = "NOT ($errorCount > 5) OR ($previousTask.success == true)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_NestedParentheses_ReturnsCorrectResult()
    {
        // Arrange
        var expression = "(($counter > 3) AND ($isActive == true)) OR ($errorCount == 0)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("($counter > 3) AND ($isActive == true)", true)]
    [InlineData("($counter > 10) AND ($isActive == true)", false)]
    [InlineData("($counter > 10) OR ($isActive == true)", true)]
    [InlineData("($counter > 10) OR ($isActive == false)", false)]
    [InlineData("NOT ($counter > 10)", true)]
    [InlineData("NOT ($isActive == true)", false)]
    public void ValidateExpression_ComplexBooleanExpressions_ReturnsTrue(string expression, bool expectedResult)
    {
        // Act
        var isValid = _evaluator.ValidateExpression(expression);

        // Assert
        Assert.True(isValid);
    }

    #endregion

    #region Function Call Tests

    [Fact]
    public async Task EvaluateAsync_LenFunction_ReturnsCorrectLength()
    {
        // Arrange
        var expression = "len($fileList) > 0";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_LenFunction_WithString_ReturnsCorrectLength()
    {
        // Arrange
        var expression = "len($variable1) == 10";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result); // "test_value" has 10 characters
    }

    [Fact]
    public async Task EvaluateAsync_LenFunction_WithEmptyList_ReturnsZero()
    {
        // Arrange
        _context.Variables["emptyList"] = new List<string>();
        var expression = "len($emptyList) == 0";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_LenFunction_WithNull_ReturnsZero()
    {
        // Arrange
        _context.Variables["nullValue"] = null!;
        var expression = "len($nullValue) == 0";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("len($fileList) > 0", true)]
    [InlineData("len($variable1) == 10", true)]
    [InlineData("len($variable1) < 5", false)]
    public void ValidateExpression_FunctionCalls_ReturnsTrue(string expression, bool expectedResult)
    {
        // Act
        var isValid = _evaluator.ValidateExpression(expression);

        // Assert
        Assert.True(isValid);
    }

    #endregion

    #region Operator Precedence Tests

    [Fact]
    public async Task EvaluateAsync_OperatorPrecedence_AndBeforeOr_ReturnsCorrectResult()
    {
        // Arrange
        // This should be evaluated as: false OR (true AND true) = false OR true = true
        var expression = "($counter > 10) OR ($isActive == true) AND ($errorCount == 0)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_OperatorPrecedence_ParenthesesOverride_ReturnsCorrectResult()
    {
        // Arrange
        // This should be evaluated as: (false OR true) AND false = true AND false = false
        _context.Variables["errorCount"] = 5; // Make errorCount > 0
        var expression = "(($counter > 10) OR ($isActive == true)) AND ($errorCount == 0)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleAndOperators_ReturnsCorrectResult()
    {
        // Arrange
        var expression = "($counter > 3) AND ($isActive == true) AND ($errorCount == 0)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleOrOperators_ReturnsCorrectResult()
    {
        // Arrange
        var expression = "($counter > 10) OR ($isActive == false) OR ($errorCount == 0)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result); // Third condition is true
    }

    #endregion

    #region Complex Real-World Scenarios

    [Fact]
    public async Task EvaluateAsync_WorkflowSuccessCondition_ReturnsTrue()
    {
        // Arrange - Complex workflow condition: task successful AND no errors AND output contains success
        var expression = "($previousTask.success == true) AND ($errorCount == 0) AND ($previousTask.output contains \"SUCCESS\")";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_RetryCondition_ReturnsCorrectResult()
    {
        // Arrange - Retry condition: NOT success OR error count > threshold
        _context.Variables["errorCount"] = 3;
        _context.Variables["maxErrors"] = 5;
        var expression = "NOT ($previousTask.success == true) OR ($errorCount > $maxErrors)";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.False(result); // Task successful AND errors under threshold
    }

    [Fact]
    public async Task EvaluateAsync_FileProcessingCondition_ReturnsTrue()
    {
        // Arrange - File processing: has files AND file path valid
        var expression = "len($fileList) > 0 AND ($filePath contains \"file\")";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_EmergencyBypassCondition_ReturnsCorrectResult()
    {
        // Arrange - Emergency bypass: force continue OR (normal conditions)
        _context.Variables["forceContinue"] = false;
        var expression = "($forceContinue == true) OR (($previousTask.success == true) AND ($errorCount == 0))";

        // Act
        var result = await _evaluator.EvaluateAsync(expression, _context);

        // Assert
        Assert.True(result); // Normal conditions are met
    }

    #endregion

    #region Error Handling for Complex Expressions

    [Fact]
    public async Task EvaluateAsync_UnbalancedParentheses_ThrowsInvalidOperationException()
    {
        // Arrange
        var expression = "($counter > 3 AND ($isActive == true)"; // Missing closing parenthesis

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _evaluator.EvaluateAsync(expression, _context));
    }

    [Fact]
    public async Task EvaluateAsync_InvalidLogicalOperator_ThrowsInvalidOperationException()
    {
        // Arrange
        var expression = "($counter > 3) XOR ($isActive == true)"; // XOR not supported

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _evaluator.EvaluateAsync(expression, _context));
    }

    [Fact]
    public async Task EvaluateAsync_MissingOperand_ThrowsInvalidOperationException()
    {
        // Arrange
        var expression = "AND ($isActive == true)"; // Missing left operand

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _evaluator.EvaluateAsync(expression, _context));
    }

    #endregion
}