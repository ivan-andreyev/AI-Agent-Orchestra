import re

# Read the file
with open('WorkflowEngine.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Fix the blocked step handling to distinguish between different blocking types
old_blocking_logic = r'''                if \(!dependencyCheckResult\.CanExecute\)
                \{
                    // Шаг заблокирован - НЕ добавляем в результаты, только отслеживаем для блокировки потомков
                    blockedSteps\.Add\(stepId\);
                    failedSteps\.Add\(stepId\);

                    _logger\.LogWarning\("Шаг \{StepId\} заблокирован: \{Reason\}", stepId, dependencyCheckResult\.BlockingReason\);

                    // Пропускаем выполнение заблокированного шага
                    continue;
                \}'''

new_blocking_logic = '''                if (!dependencyCheckResult.CanExecute)
                {
                    // Различаем типы блокировки:
                    // - Отсутствующие зависимости (должны появиться в результатах как Failed)
                    // - Неудачные зависимости (НЕ должны появиться в результатах)
                    var blockingReason = dependencyCheckResult.BlockingReason ?? "";
                    
                    if (blockingReason.Contains("отсутствуют зависимости") || blockingReason.Contains("Отсутствующие зависимости"))
                    {
                        // Шаг заблокирован из-за отсутствующих зависимостей - создаем Failed результат
                        var blockedResult = CreateBlockedStepResult(
                            stepId,
                            dependencyCheckResult.BlockingReason,
                            dependencyCheckResult.FailedDependencies);

                        stepResults.Add(blockedResult);
                        stepResultLookup[stepId] = blockedResult;
                        blockedSteps.Add(stepId);
                        failedSteps.Add(stepId);

                        _logger.LogWarning("Шаг {StepId} заблокирован и добавлен как Failed: {Reason}", stepId, dependencyCheckResult.BlockingReason);
                    }
                    else
                    {
                        // Шаг заблокирован из-за неудачных зависимостей - НЕ добавляем в результаты
                        blockedSteps.Add(stepId);
                        failedSteps.Add(stepId);

                        _logger.LogWarning("Шаг {StepId} заблокирован и пропущен: {Reason}", stepId, dependencyCheckResult.BlockingReason);
                    }

                    // Пропускаем выполнение заблокированного шага
                    continue;
                }'''

content = re.sub(old_blocking_logic, new_blocking_logic, content, flags=re.MULTILINE)

# Write the file back
with open('WorkflowEngine.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print("Fixed blocking logic to distinguish between different types of blocking")
