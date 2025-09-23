import re

# Read the file
with open('WorkflowEngine.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Fix 1: Replace the blocked step logic (lines 649-665)
old_blocked_logic = r'''                if \(!dependencyCheckResult\.CanExecute\)
                \{
                    // Создаем результат для заблокированного шага
                    var blockedResult = CreateBlockedStepResult\(
                        stepId,
                        dependencyCheckResult\.BlockingReason,
                        dependencyCheckResult\.FailedDependencies\);

                    stepResults\.Add\(blockedResult\);
                    stepResultLookup\[stepId\] = blockedResult;
                    blockedSteps\.Add\(stepId\);
                    failedSteps\.Add\(stepId\);

                    _logger\.LogWarning\("Шаг \{StepId\} заблокирован: \{Reason\}", stepId, dependencyCheckResult\.BlockingReason\);

                    // НЕ распространяем блокировку сразу - продолжаем выполнение других шагов
                \}'''

new_blocked_logic = '''                if (!dependencyCheckResult.CanExecute)
                {
                    // Шаг заблокирован - НЕ добавляем в результаты, только отслеживаем для блокировки потомков
                    blockedSteps.Add(stepId);
                    failedSteps.Add(stepId);

                    _logger.LogWarning("Шаг {StepId} заблокирован: {Reason}", stepId, dependencyCheckResult.BlockingReason);

                    // Пропускаем выполнение заблокированного шага
                    continue;
                }'''

content = re.sub(old_blocked_logic, new_blocked_logic, content, flags=re.MULTILINE)

# Fix 2: Replace the workflow status logic (lines 95-98)
old_status_logic = r'''            var finalStatus = \(stepResults\.Count == workflow\.Steps\.Count && 
                              stepResults\.All\(sr => sr\.Status == WorkflowStatus\.Completed\)\)
                \? WorkflowStatus\.Completed
                : WorkflowStatus\.Failed;'''

new_status_logic = '''            // Workflow is Complete only if ALL attempted steps succeeded
            // If there are blocked steps or any failures, the workflow is Failed
            var hasFailedSteps = stepResults.Any(sr => sr.Status == WorkflowStatus.Failed);
            var finalStatus = (!hasFailedSteps && stepResults.All(sr => sr.Status == WorkflowStatus.Completed))
                ? WorkflowStatus.Completed
                : WorkflowStatus.Failed;'''

content = re.sub(old_status_logic, new_status_logic, content, flags=re.MULTILINE)

# Write the file back
with open('WorkflowEngine.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print("Fixed WorkflowEngine.cs")
