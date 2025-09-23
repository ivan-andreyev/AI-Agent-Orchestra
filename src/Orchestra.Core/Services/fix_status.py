import re

# Read the file
with open('WorkflowEngine.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Fix the workflow status logic to consider blocked steps
old_status_logic = r'''            // Workflow is Complete only if ALL attempted steps succeeded
            // If there are blocked steps or any failures, the workflow is Failed
            var hasFailedSteps = stepResults\.Any\(sr => sr\.Status == WorkflowStatus\.Failed\);
            var finalStatus = \(!hasFailedSteps && stepResults\.All\(sr => sr\.Status == WorkflowStatus\.Completed\)\)
                \? WorkflowStatus\.Completed
                : WorkflowStatus\.Failed;'''

new_status_logic = '''            // Workflow is Complete only if ALL steps were executed successfully
            // If there are blocked steps (missing from results) or any failures, the workflow is Failed
            var hasFailedSteps = stepResults.Any(sr => sr.Status == WorkflowStatus.Failed);
            var hasBlockedSteps = stepResults.Count < workflow.Steps.Count;
            var finalStatus = (!hasFailedSteps && !hasBlockedSteps && stepResults.All(sr => sr.Status == WorkflowStatus.Completed))
                ? WorkflowStatus.Completed
                : WorkflowStatus.Failed;'''

content = re.sub(old_status_logic, new_status_logic, content, flags=re.MULTILINE)

# Write the file back
with open('WorkflowEngine.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print("Fixed workflow status determination logic")
