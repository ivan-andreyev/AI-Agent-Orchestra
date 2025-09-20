using System.Text.RegularExpressions;
using Orchestra.Core.Models.Workflow;

namespace Orchestra.Core.Services;

/// <summary>
/// Безопасный оценщик выражений для условной логики workflow.
/// Поддерживает базовые операции сравнения и предотвращает инъекцию кода.
/// </summary>
public interface IExpressionEvaluator
{
    /// <summary>
    /// Оценивает условное выражение в контексте выполнения workflow
    /// </summary>
    /// <param name="expression">Выражение для оценки</param>
    /// <param name="context">Контекст выполнения с переменными</param>
    /// <returns>Результат оценки выражения</returns>
    Task<bool> EvaluateAsync(string expression, WorkflowExecutionContext context);

    /// <summary>
    /// Проверяет корректность синтаксиса выражения
    /// </summary>
    /// <param name="expression">Выражение для проверки</param>
    /// <returns>True, если выражение корректно</returns>
    bool ValidateExpression(string expression);
}

/// <summary>
/// Контекст выполнения workflow с переменными и результатами шагов
/// </summary>
public class WorkflowExecutionContext
{
    /// <summary>
    /// Переменные workflow
    /// </summary>
    public Dictionary<string, object> Variables { get; init; } = new();

    /// <summary>
    /// Результаты выполнения шагов
    /// </summary>
    public Dictionary<string, object> StepResults { get; init; } = new();

    /// <summary>
    /// Метаданные контекста
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Получает значение переменной или результата шага
    /// </summary>
    /// <param name="name">Имя переменной или идентификатор шага</param>
    /// <returns>Значение или null, если не найдено</returns>
    public object? GetValue(string name)
    {
        if (Variables.TryGetValue(name, out var variable))
        {
            return variable;
        }

        if (StepResults.TryGetValue(name, out var stepResult))
        {
            return stepResult;
        }

        return null;
    }
}

/// <summary>
/// Результат разбора выражения
/// </summary>
public record ExpressionParseResult(
    string LeftOperand,
    string Operator,
    string RightOperand,
    bool IsValid,
    string? ErrorMessage = null
);

/// <summary>
/// Типы узлов в дереве выражений
/// </summary>
public enum ExpressionNodeType
{
    Comparison,
    LogicalOperator,
    Function,
    Parentheses
}

/// <summary>
/// Узел в дереве выражений для поддержки сложной логики
/// </summary>
public abstract record ExpressionNode(ExpressionNodeType Type);

/// <summary>
/// Узел сравнения (например: $var == "value")
/// </summary>
public record ComparisonNode(
    string LeftOperand,
    string Operator,
    string RightOperand
) : ExpressionNode(ExpressionNodeType.Comparison);

/// <summary>
/// Узел логического оператора (AND, OR, NOT)
/// </summary>
public record LogicalOperatorNode(
    string Operator,
    ExpressionNode Left,
    ExpressionNode? Right = null // null для унарных операторов как NOT
) : ExpressionNode(ExpressionNodeType.LogicalOperator);

/// <summary>
/// Узел функции (например: len($list) > 0)
/// </summary>
public record FunctionNode(
    string FunctionName,
    string Argument
) : ExpressionNode(ExpressionNodeType.Function);

/// <summary>
/// Результат разбора сложного выражения
/// </summary>
public record ComplexExpressionParseResult(
    ExpressionNode? Root,
    bool IsValid,
    string? ErrorMessage = null
);

/// <summary>
/// Реализация безопасного оценщика выражений
/// </summary>
public class ExpressionEvaluator : IExpressionEvaluator
{
    /// <summary>
    /// Поддерживаемые операторы сравнения
    /// </summary>
    private static readonly HashSet<string> SupportedOperators = new()
    {
        "==", "!=", ">", "<", ">=", "<=", "contains", "regex"
    };

    /// <summary>
    /// Поддерживаемые логические операторы
    /// </summary>
    private static readonly HashSet<string> LogicalOperators = new()
    {
        "AND", "OR", "NOT"
    };

    /// <summary>
    /// Поддерживаемые функции
    /// </summary>
    private static readonly HashSet<string> SupportedFunctions = new()
    {
        "len", "contains", "regex"
    };

    /// <summary>
    /// Регулярное выражение для разбора простых выражений сравнения
    /// Формат: $variable operator value или $variable1 operator $variable2
    /// </summary>
    private static readonly Regex ExpressionPattern = new(
        @"^\s*(\$[a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)\s+(==|!=|>=|<=|>|<|contains|regex)\s+(.+)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Регулярное выражение для валидации имен переменных
    /// </summary>
    private static readonly Regex VariableNamePattern = new(
        @"^\$[a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*$",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Оценивает условное выражение в контексте выполнения workflow
    /// </summary>
    public async Task<bool> EvaluateAsync(string expression, WorkflowExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Expression cannot be null or empty", nameof(expression));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // Сначала попробуем разобрать как сложное выражение
        var complexParseResult = ParseComplexExpression(expression);
        if (complexParseResult.IsValid && complexParseResult.Root != null)
        {
            return await EvaluateExpressionNodeAsync(complexParseResult.Root, context);
        }

        // Если не получилось, попробуем разобрать как простое выражение (обратная совместимость)
        var parseResult = ParseExpression(expression);
        if (!parseResult.IsValid)
        {
            throw new InvalidOperationException($"Invalid expression syntax: {parseResult.ErrorMessage}");
        }

        // Получение значений операндов
        var leftValue = await ResolveOperandAsync(parseResult.LeftOperand, context);
        var rightValue = await ResolveOperandAsync(parseResult.RightOperand, context);

        // Выполнение операции сравнения
        return await ExecuteComparisonAsync(leftValue, parseResult.Operator, rightValue);
    }

    /// <summary>
    /// Проверяет корректность синтаксиса выражения
    /// </summary>
    public bool ValidateExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        // Сначала проверяем как сложное выражение
        var complexParseResult = ParseComplexExpression(expression);
        if (complexParseResult.IsValid)
        {
            return true;
        }

        // Если не прошло, проверяем как простое выражение
        var parseResult = ParseExpression(expression);
        return parseResult.IsValid;
    }

    /// <summary>
    /// Разбирает выражение на компоненты
    /// </summary>
    private ExpressionParseResult ParseExpression(string expression)
    {
        try
        {
            var match = ExpressionPattern.Match(expression);
            if (!match.Success)
            {
                return new ExpressionParseResult("", "", "", false,
                    "Expression must be in format: $variable operator value");
            }

            var leftOperand = match.Groups[1].Value.Trim();
            var operatorSymbol = match.Groups[2].Value.Trim().ToLowerInvariant();
            var rightOperand = match.Groups[3].Value.Trim();

            // Валидация левого операнда (должен быть переменной)
            if (!VariableNamePattern.IsMatch(leftOperand))
            {
                return new ExpressionParseResult("", "", "", false,
                    "Left operand must be a valid variable name starting with $");
            }

            // Валидация оператора
            if (!SupportedOperators.Contains(operatorSymbol))
            {
                return new ExpressionParseResult("", "", "", false,
                    $"Unsupported operator: {operatorSymbol}. Supported: {string.Join(", ", SupportedOperators)}");
            }

            return new ExpressionParseResult(leftOperand, operatorSymbol, rightOperand, true);
        }
        catch (Exception ex)
        {
            return new ExpressionParseResult("", "", "", false, $"Parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Разбирает сложное выражение с поддержкой логических операторов и скобок
    /// </summary>
    private ComplexExpressionParseResult ParseComplexExpression(string expression)
    {
        try
        {
            // Упрощенный парсер сложных выражений
            // Поддерживает: AND, OR, NOT, скобки, функции

            expression = expression.Trim();

            // Проверяем наличие логических операторов
            if (!ContainsLogicalOperators(expression))
            {
                // Это может быть простое выражение или функция
                return TryParseAsSimpleOrFunction(expression);
            }

            // Парсим выражение с логическими операторами
            var root = ParseLogicalExpression(expression);
            if (root == null)
            {
                return new ComplexExpressionParseResult(null, false, "Failed to parse logical expression");
            }

            return new ComplexExpressionParseResult(root, true);
        }
        catch (Exception ex)
        {
            return new ComplexExpressionParseResult(null, false, $"Parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Проверяет наличие логических операторов в выражении
    /// </summary>
    private bool ContainsLogicalOperators(string expression)
    {
        var upperExpression = expression.ToUpperInvariant();
        return LogicalOperators.Any(op => upperExpression.Contains(op));
    }

    /// <summary>
    /// Пытается разобрать как простое выражение или функцию
    /// </summary>
    private ComplexExpressionParseResult TryParseAsSimpleOrFunction(string expression)
    {
        // Проверяем, является ли это функцией
        var functionMatch = Regex.Match(expression, @"^(\w+)\s*\(\s*(.+?)\s*\)\s*([><=!]+)\s*(.+)$");
        if (functionMatch.Success)
        {
            var functionName = functionMatch.Groups[1].Value.ToLowerInvariant();
            var argument = functionMatch.Groups[2].Value.Trim();
            var operatorSymbol = functionMatch.Groups[3].Value.Trim();
            var rightOperand = functionMatch.Groups[4].Value.Trim();

            if (SupportedFunctions.Contains(functionName))
            {
                // Создаем составное выражение с функцией
                var functionNode = new FunctionNode(functionName, argument);
                var comparisonNode = new ComparisonNode($"{functionName}({argument})", operatorSymbol, rightOperand);
                return new ComplexExpressionParseResult(comparisonNode, true);
            }
        }

        // Пробуем разобрать как простое выражение
        var parseResult = ParseExpression(expression);
        if (parseResult.IsValid)
        {
            var comparisonNode = new ComparisonNode(parseResult.LeftOperand, parseResult.Operator, parseResult.RightOperand);
            return new ComplexExpressionParseResult(comparisonNode, true);
        }

        return new ComplexExpressionParseResult(null, false, "Not a valid simple expression or function");
    }

    /// <summary>
    /// Парсит логическое выражение с поддержкой приоритетов операторов
    /// </summary>
    private ExpressionNode? ParseLogicalExpression(string expression)
    {
        // Упрощенный парсер для демонстрации
        // В реальной реализации нужен полноценный рекурсивный парсер

        expression = expression.Trim();

        // Обработка скобок
        if (expression.StartsWith('(') && expression.EndsWith(')'))
        {
            var innerExpression = expression[1..^1].Trim();
            return ParseLogicalExpression(innerExpression);
        }

        // Обработка NOT оператора
        if (expression.ToUpperInvariant().StartsWith("NOT "))
        {
            var innerExpression = expression[4..].Trim();
            var innerNode = ParseLogicalExpression(innerExpression);
            if (innerNode != null)
            {
                return new LogicalOperatorNode("NOT", innerNode);
            }
        }

        // Поиск основного логического оператора (OR имеет меньший приоритет)
        var orIndex = FindMainLogicalOperator(expression, "OR");
        if (orIndex >= 0)
        {
            var leftPart = expression[..orIndex].Trim();
            var rightPart = expression[(orIndex + 2)..].Trim();

            var leftNode = ParseLogicalExpression(leftPart);
            var rightNode = ParseLogicalExpression(rightPart);

            if (leftNode != null && rightNode != null)
            {
                return new LogicalOperatorNode("OR", leftNode, rightNode);
            }
        }

        // Поиск AND оператора
        var andIndex = FindMainLogicalOperator(expression, "AND");
        if (andIndex >= 0)
        {
            var leftPart = expression[..andIndex].Trim();
            var rightPart = expression[(andIndex + 3)..].Trim();

            var leftNode = ParseLogicalExpression(leftPart);
            var rightNode = ParseLogicalExpression(rightPart);

            if (leftNode != null && rightNode != null)
            {
                return new LogicalOperatorNode("AND", leftNode, rightNode);
            }
        }

        // Если нет логических операторов, пытаемся разобрать как простое выражение
        var simpleResult = TryParseAsSimpleOrFunction(expression);
        return simpleResult.IsValid ? simpleResult.Root : null;
    }

    /// <summary>
    /// Находит основной логический оператор в выражении (не внутри скобок)
    /// </summary>
    private int FindMainLogicalOperator(string expression, string operatorName)
    {
        var upperExpression = expression.ToUpperInvariant();
        var parenthesesLevel = 0;

        for (int i = 0; i <= upperExpression.Length - operatorName.Length; i++)
        {
            if (expression[i] == '(')
            {
                parenthesesLevel++;
            }
            else if (expression[i] == ')')
            {
                parenthesesLevel--;
            }
            else if (parenthesesLevel == 0)
            {
                if (upperExpression.Substring(i, operatorName.Length) == operatorName)
                {
                    // Проверяем, что это отдельное слово (не часть другого слова)
                    var prevChar = i > 0 ? expression[i - 1] : ' ';
                    var nextChar = i + operatorName.Length < expression.Length ? expression[i + operatorName.Length] : ' ';

                    if (char.IsWhiteSpace(prevChar) && char.IsWhiteSpace(nextChar))
                    {
                        return i;
                    }
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Оценивает узел выражения
    /// </summary>
    private async Task<bool> EvaluateExpressionNodeAsync(ExpressionNode node, WorkflowExecutionContext context)
    {
        return node switch
        {
            ComparisonNode comparison => await EvaluateComparisonNodeAsync(comparison, context),
            LogicalOperatorNode logical => await EvaluateLogicalOperatorNodeAsync(logical, context),
            FunctionNode function => await EvaluateFunctionNodeAsync(function, context),
            _ => throw new NotSupportedException($"Expression node type {node.Type} is not supported")
        };
    }

    /// <summary>
    /// Оценивает узел сравнения
    /// </summary>
    private async Task<bool> EvaluateComparisonNodeAsync(ComparisonNode node, WorkflowExecutionContext context)
    {
        // Проверяем, является ли левый операнд функцией
        if (node.LeftOperand.Contains('(') && node.LeftOperand.Contains(')'))
        {
            var functionValue = await EvaluateFunctionCallAsync(node.LeftOperand, context);
            var rightOperandValue = await ResolveOperandAsync(node.RightOperand, context);
            return await ExecuteComparisonAsync(functionValue, node.Operator, rightOperandValue);
        }

        // Обычное сравнение
        var leftValue = await ResolveOperandAsync(node.LeftOperand, context);
        var rightValue = await ResolveOperandAsync(node.RightOperand, context);
        return await ExecuteComparisonAsync(leftValue, node.Operator, rightValue);
    }

    /// <summary>
    /// Оценивает узел логического оператора
    /// </summary>
    private async Task<bool> EvaluateLogicalOperatorNodeAsync(LogicalOperatorNode node, WorkflowExecutionContext context)
    {
        return node.Operator.ToUpperInvariant() switch
        {
            "AND" => node.Right != null &&
                     await EvaluateExpressionNodeAsync(node.Left, context) &&
                     await EvaluateExpressionNodeAsync(node.Right, context),
            "OR" => node.Right != null &&
                    (await EvaluateExpressionNodeAsync(node.Left, context) ||
                     await EvaluateExpressionNodeAsync(node.Right, context)),
            "NOT" => !await EvaluateExpressionNodeAsync(node.Left, context),
            _ => throw new NotSupportedException($"Logical operator {node.Operator} is not supported")
        };
    }

    /// <summary>
    /// Оценивает узел функции
    /// </summary>
    private async Task<bool> EvaluateFunctionNodeAsync(FunctionNode node, WorkflowExecutionContext context)
    {
        var result = await EvaluateFunctionCallAsync($"{node.FunctionName}({node.Argument})", context);
        return result is bool boolResult ? boolResult : Convert.ToBoolean(result);
    }

    /// <summary>
    /// Оценивает вызов функции
    /// </summary>
    private async Task<object?> EvaluateFunctionCallAsync(string functionCall, WorkflowExecutionContext context)
    {
        var match = Regex.Match(functionCall, @"^(\w+)\s*\(\s*(.+?)\s*\)$");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Invalid function call syntax: {functionCall}");
        }

        var functionName = match.Groups[1].Value.ToLowerInvariant();
        var argumentStr = match.Groups[2].Value.Trim();

        if (!SupportedFunctions.Contains(functionName))
        {
            throw new NotSupportedException($"Function {functionName} is not supported");
        }

        var argumentValue = await ResolveOperandAsync(argumentStr, context);

        return functionName switch
        {
            "len" => GetLength(argumentValue),
            "contains" => throw new InvalidOperationException("contains function requires two arguments"),
            "regex" => throw new InvalidOperationException("regex function requires two arguments"),
            _ => throw new NotSupportedException($"Function {functionName} is not implemented")
        };
    }

    /// <summary>
    /// Получает длину значения
    /// </summary>
    private int GetLength(object? value)
    {
        return value switch
        {
            null => 0,
            string str => str.Length,
            System.Collections.ICollection collection => collection.Count,
            System.Collections.IEnumerable enumerable => enumerable.Cast<object>().Count(),
            _ => value.ToString()?.Length ?? 0
        };
    }

    /// <summary>
    /// Разрешает операнд в значение
    /// </summary>
    private async Task<object?> ResolveOperandAsync(string operand, WorkflowExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(operand))
        {
            return null;
        }

        // Если операнд - переменная (начинается с $)
        if (operand.StartsWith('$'))
        {
            var variablePath = operand[1..]; // Убираем $
            return ResolveVariablePath(variablePath, context);
        }

        // Если операнд - строковый литерал (в кавычках)
        if ((operand.StartsWith('"') && operand.EndsWith('"')) ||
            (operand.StartsWith('\'') && operand.EndsWith('\'')))
        {
            return operand[1..^1]; // Убираем кавычки
        }

        // Если операнд - числовой литерал
        if (double.TryParse(operand, out var doubleValue))
        {
            return doubleValue;
        }

        if (int.TryParse(operand, out var intValue))
        {
            return intValue;
        }

        // Если операнд - булевый литерал
        if (bool.TryParse(operand, out var boolValue))
        {
            return boolValue;
        }

        // По умолчанию возвращаем как строку
        return operand;
    }

    /// <summary>
    /// Разрешает путь переменной с поддержкой точечной нотации
    /// Например: taskResult.success или variable1
    /// </summary>
    private object? ResolveVariablePath(string variablePath, WorkflowExecutionContext context)
    {
        var parts = variablePath.Split('.');
        var rootName = parts[0];

        // Получаем корневое значение
        var rootValue = context.GetValue(rootName);
        if (rootValue == null)
        {
            return null;
        }

        // Если нет вложенных свойств, возвращаем корневое значение
        if (parts.Length == 1)
        {
            return rootValue;
        }

        // Обходим вложенные свойства
        var currentValue = rootValue;
        for (int i = 1; i < parts.Length; i++)
        {
            var propertyName = parts[i];

            if (currentValue is Dictionary<string, object> dict)
            {
                if (!dict.TryGetValue(propertyName, out currentValue))
                {
                    return null;
                }
            }
            else
            {
                // Поддержка рефлексии для объектов
                var property = currentValue.GetType().GetProperty(propertyName);
                if (property == null)
                {
                    return null;
                }
                currentValue = property.GetValue(currentValue);
            }

            if (currentValue == null)
            {
                return null;
            }
        }

        return currentValue;
    }

    /// <summary>
    /// Выполняет операцию сравнения между двумя значениями
    /// </summary>
    private async Task<bool> ExecuteComparisonAsync(object? leftValue, string operatorSymbol, object? rightValue)
    {
        return operatorSymbol switch
        {
            "==" => await CompareEqualityAsync(leftValue, rightValue, true),
            "!=" => await CompareEqualityAsync(leftValue, rightValue, false),
            ">" => await CompareNumericAsync(leftValue, rightValue, (l, r) => l > r),
            "<" => await CompareNumericAsync(leftValue, rightValue, (l, r) => l < r),
            ">=" => await CompareNumericAsync(leftValue, rightValue, (l, r) => l >= r),
            "<=" => await CompareNumericAsync(leftValue, rightValue, (l, r) => l <= r),
            "contains" => await CompareContainsAsync(leftValue, rightValue),
            "regex" => await CompareRegexAsync(leftValue, rightValue),
            _ => throw new NotSupportedException($"Operator {operatorSymbol} is not supported")
        };
    }

    /// <summary>
    /// Сравнение на равенство
    /// </summary>
    private async Task<bool> CompareEqualityAsync(object? left, object? right, bool equality)
    {
        await Task.Yield(); // Делаем метод асинхронным для единообразия

        if (left == null && right == null)
        {
            return equality;
        }

        if (left == null || right == null)
        {
            return !equality;
        }

        // Приведение типов для сравнения
        if (left.GetType() != right.GetType())
        {
            // Попытка привести к общему типу
            if (TryConvertToCommonType(left, right, out var convertedLeft, out var convertedRight))
            {
                left = convertedLeft;
                right = convertedRight;
            }
        }

        var areEqual = left.Equals(right);
        return equality ? areEqual : !areEqual;
    }

    /// <summary>
    /// Числовое сравнение
    /// </summary>
    private async Task<bool> CompareNumericAsync(object? left, object? right, Func<double, double, bool> comparison)
    {
        await Task.Yield();

        if (!TryConvertToDouble(left, out var leftDouble) ||
            !TryConvertToDouble(right, out var rightDouble))
        {
            throw new InvalidOperationException("Numeric comparison requires both operands to be convertible to numbers");
        }

        return comparison(leftDouble, rightDouble);
    }

    /// <summary>
    /// Сравнение на содержание подстроки
    /// </summary>
    private async Task<bool> CompareContainsAsync(object? left, object? right)
    {
        await Task.Yield();

        var leftStr = left?.ToString();
        var rightStr = right?.ToString();

        if (leftStr == null)
        {
            return false;
        }

        if (rightStr == null)
        {
            return false;
        }

        return leftStr.Contains(rightStr, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Сравнение с регулярным выражением
    /// </summary>
    private async Task<bool> CompareRegexAsync(object? left, object? right)
    {
        await Task.Yield();

        var leftStr = left?.ToString();
        var pattern = right?.ToString();

        if (leftStr == null || pattern == null)
        {
            return false;
        }

        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
            return regex.IsMatch(leftStr);
        }
        catch (Exception)
        {
            // Некорректное регулярное выражение
            return false;
        }
    }

    /// <summary>
    /// Попытка приведения к общему типу
    /// </summary>
    private bool TryConvertToCommonType(object left, object right, out object convertedLeft, out object convertedRight)
    {
        convertedLeft = left;
        convertedRight = right;

        // Попытка привести к числам
        if (TryConvertToDouble(left, out var leftDouble) && TryConvertToDouble(right, out var rightDouble))
        {
            convertedLeft = leftDouble;
            convertedRight = rightDouble;
            return true;
        }

        // Приведение к строкам
        convertedLeft = left.ToString() ?? string.Empty;
        convertedRight = right.ToString() ?? string.Empty;
        return true;
    }

    /// <summary>
    /// Попытка конвертации значения в double
    /// </summary>
    private bool TryConvertToDouble(object? value, out double result)
    {
        result = 0;

        if (value == null)
        {
            return false;
        }

        if (value is double d)
        {
            result = d;
            return true;
        }

        if (value is int i)
        {
            result = i;
            return true;
        }

        if (value is float f)
        {
            result = f;
            return true;
        }

        if (value is decimal dec)
        {
            result = (double)dec;
            return true;
        }

        return double.TryParse(value.ToString(), out result);
    }
}