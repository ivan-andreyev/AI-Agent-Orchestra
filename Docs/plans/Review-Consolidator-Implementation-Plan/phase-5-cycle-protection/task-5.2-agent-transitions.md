# Task 5.2: Agent Transition Matrix Integration

**Parent Phase**: [phase-5-cycle-protection.md](../phase-5-cycle-protection.md)

**Duration**: 3-4 hours
**Complexity**: 8-12 tool calls per subtask
**Deliverables**: Upstream/downstream agent specifications, transition recommendations

---

## 5.2A: Define upstream transitions
**Complexity**: 8-10 tool calls
**Location**: `.cursor/agents/review-consolidator/agent.md` transitions section

**Upstream Agent Integration**:
```markdown
## UPSTREAM AGENTS (Who Invokes review-consolidator)

### 1. plan-task-executor → review-consolidator (CRITICAL)
**When**: After executing code-writing tasks
**Why**: Validate code quality before marking task complete
**Invocation**:
```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Review code changes from task execution",
  prompt: `Review the following files for code quality issues:

Files modified:
${modifiedFiles.join('\n')}

Review types: code-style, code-principles, test-healer

Cycle ID: ${cycleId} (iteration 1)
`,
  context: {
    files: modifiedFiles,
    reviewTypes: ['code-style-reviewer', 'code-principles-reviewer', 'test-healer'],
    cycleId: cycleId
  }
})
```

### 2. plan-task-completer → review-consolidator (RECOMMENDED)
**When**: Before marking task as complete
**Why**: Final validation before completion
**Invocation**:
```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Final review before task completion",
  prompt: `Perform final code quality review for task completion:

Task: ${taskDescription}
Files: ${taskFiles.join(', ')}
Review types: All applicable reviewers

This is a final review before marking task complete.
`,
  context: {
    files: taskFiles,
    reviewTypes: 'auto', // Auto-detect based on file types
    finalReview: true
  }
})
```

### 3. User Manual Invocation (OPTIONAL)
**When**: Ad-hoc code quality audits
**Why**: On-demand review without task context
**Invocation**:
```bash
# Manual invocation example
Task subagent_type="review-consolidator" description="Review UserService.cs" \\
  prompt="Review src/Services/UserService.cs for all issues"
```
```

**Acceptance Criteria**:
- [ ] All upstream agents documented
- [ ] Invocation examples valid and complete
- [ ] Context parameters specified
- [ ] Cycle ID passing mechanism defined
- [ ] Manual invocation pattern documented

---

## 5.2B: Define downstream transitions
**Complexity**: 10-12 tool calls
**Location**: `.cursor/agents/review-consolidator/agent.md` recommendations section

**Downstream Agent Transitions**:
```markdown
## DOWNSTREAM AGENTS (Where review-consolidator Transitions)

### 1. review-consolidator → plan-task-executor (CRITICAL if P0 found)
**When**: Critical (P0) issues discovered
**Why**: Issues must be fixed before proceeding
**Recommendation Format**:
```
⚠️  CRITICAL ISSUES FOUND - IMMEDIATE ACTION REQUIRED ⚠️

Found ${p0Count} critical (P0) issues that must be fixed:
${p0Issues.map(formatIssue).join('\n')}

🔄 RECOMMENDED NEXT ACTION:
Invoke plan-task-executor agent to fix these issues:

Task({
  subagent_type: "plan-task-executor",
  description: "Fix critical issues from review",
  prompt: \`Fix the following critical issues:

${p0Issues.map(i => \`- ${i.file}:${i.line}: ${i.message}\`).join('\n')}

Cycle ID: ${cycleId} (iteration ${iteration})
After fixes, review-consolidator will re-review automatically.
\`,
  context: {
    cycleId: cycleId,
    issuesToFix: p0Issues,
    reReviewAfterFix: true
  }
})
```

### 2. review-consolidator → pre-completion-validator (CRITICAL if all clear)
**When**: No P0 issues, only P1/P2 or no issues
**Why**: Validate task completion requirements
**Recommendation Format**:
```
✅ Code Quality Review Passed

No critical issues found. ${p1Count} warnings and ${p2Count} improvements suggested.

🔄 RECOMMENDED NEXT ACTION:
Invoke pre-completion-validator to verify task completion:

Task({
  subagent_type: "pre-completion-validator",
  description: "Validate task completion after code review",
  prompt: \`Validate that the task has been completed according to requirements.

Code quality review passed with ${totalIssues} non-critical issues.
Review report: ${reportPath}
\`,
  context: {
    reviewReport: reportPath,
    reviewPassed: true
  }
})
```

### 3. review-consolidator → git-workflow-manager (OPTIONAL if ready)
**When**: Code is ready to commit (no P0, P1 acceptable)
**Why**: Proceed to version control
**Recommendation Format**:
```
✅ Code Ready for Commit

Code quality sufficient for commit:
- P0 (Critical): 0
- P1 (Warning): ${p1Count} (acceptable)
- P2 (Improvement): ${p2Count} (can be addressed later)

💡 OPTIONAL NEXT ACTION:
If ready to commit, invoke git-workflow-manager:

Task({
  subagent_type: "git-workflow-manager",
  description: "Commit reviewed code changes",
  prompt: \`Create commit for reviewed changes:

Files: ${modifiedFiles.join(', ')}
Review: Passed with ${p1Count} warnings
\`,
  context: {
    reviewReport: reportPath
  }
})
```
```

**Acceptance Criteria**:
- [ ] All downstream agents documented
- [ ] Recommendation templates formatted correctly
- [ ] Priority-based routing rules defined
- [ ] Invocation examples complete
- [ ] Parameter passing verified

---

## 5.2C: Create transition recommendations
**Complexity**: 6-8 tool calls
**Location**: `prompt.md` recommendations section

**Automatic Recommendation Generation**:
```typescript
interface TransitionRecommendation {
  targetAgent: string;
  priority: 'CRITICAL' | 'RECOMMENDED' | 'OPTIONAL';
  reason: string;
  invocationExample: string;
  parameters: Record<string, any>;
}

function generateTransitionRecommendations(
  report: ConsolidatedReport
): TransitionRecommendation[] {
  const recommendations: TransitionRecommendation[] = [];

  // Rule 1: P0 issues → CRITICAL: plan-task-executor
  if (report.criticalIssues.length > 0) {
    recommendations.push({
      targetAgent: 'plan-task-executor',
      priority: 'CRITICAL',
      reason: `${report.criticalIssues.length} critical issues must be fixed`,
      invocationExample: generateExecutorInvocation(report),
      parameters: {
        cycleId: report.cycleId,
        issuesToFix: report.criticalIssues,
        reReviewAfterFix: true
      }
    });
  }

  // Rule 2: No P0 → CRITICAL: pre-completion-validator
  if (report.criticalIssues.length === 0) {
    recommendations.push({
      targetAgent: 'pre-completion-validator',
      priority: 'CRITICAL',
      reason: 'No critical issues - validate task completion',
      invocationExample: generateValidatorInvocation(report),
      parameters: {
        reviewReport: report.filePath,
        reviewPassed: true
      }
    });
  }

  // Rule 3: Clean or minor issues → OPTIONAL: git-workflow-manager
  if (report.criticalIssues.length === 0 && report.warnings.length <= 3) {
    recommendations.push({
      targetAgent: 'git-workflow-manager',
      priority: 'OPTIONAL',
      reason: 'Code quality sufficient for commit',
      invocationExample: generateGitInvocation(report),
      parameters: {
        reviewReport: report.filePath
      }
    });
  }

  return recommendations;
}

function generateExecutorInvocation(report: ConsolidatedReport): string {
  return `
Task({
  subagent_type: "plan-task-executor",
  description: "Fix ${report.criticalIssues.length} critical issues from review",
  prompt: \`Fix the following critical (P0) issues:

${report.criticalIssues.map((issue, idx) =>
  \`\${idx + 1}. \${issue.file}:\${issue.line}: \${issue.message}\`
).join('\n')}

Cycle ID: ${report.cycleId} (iteration ${report.iteration})
After fixes, invoke review-consolidator for re-review.
\`,
  context: {
    cycleId: "${report.cycleId}",
    iteration: ${report.iteration},
    issuesToFix: ${JSON.stringify(report.criticalIssues, null, 2)},
    reReviewAfterFix: true
  }
})
`;
}

function generateValidatorInvocation(report: ConsolidatedReport): string {
  return `
Task({
  subagent_type: "pre-completion-validator",
  description: "Validate task completion after successful code review",
  prompt: \`Validate task completion against original requirements.

Code quality review completed successfully:
- Critical issues (P0): 0
- Warnings (P1): ${report.warnings.length}
- Improvements (P2): ${report.improvements.length}

Review report: ${report.filePath}
\`,
  context: {
    reviewReport: "${report.filePath}",
    reviewPassed: true,
    issuesFound: ${report.totalIssues},
    cycleId: "${report.cycleId}"
  }
})
`;
}

function generateGitInvocation(report: ConsolidatedReport): string {
  return `
Task({
  subagent_type: "git-workflow-manager",
  description: "Commit reviewed code changes",
  prompt: \`Create commit for reviewed code changes:

Files modified: ${report.filesModified.length} files
Review status: Passed
Non-critical issues: ${report.warnings.length} warnings, ${report.improvements.length} improvements

Review report: ${report.filePath}
\`,
  context: {
    reviewReport: "${report.filePath}",
    filesModified: ${JSON.stringify(report.filesModified)},
    reviewPassed: true
  }
})
`;
}
```

**Recommendation Display Format**:
```markdown
## Automatic Transition Recommendations

Based on review results, the following agent transitions are recommended:

### 🚨 CRITICAL: plan-task-executor
**Reason**: 3 critical (P0) issues must be fixed immediately
**Invocation**:
```typescript
Task({
  subagent_type: "plan-task-executor",
  description: "Fix 3 critical issues from review",
  prompt: `Fix the following critical (P0) issues:

1. AuthController.cs:42: Null reference exception risk
2. UserService.cs:15: DI registration missing
3. AuthTests.cs:78: Test failure (timeout)

Cycle ID: consolidator-executor-1697123456789 (iteration 1)
After fixes, invoke review-consolidator for re-review.
`,
  context: {
    cycleId: "consolidator-executor-1697123456789",
    iteration: 1,
    issuesToFix: [/* P0 issues */],
    reReviewAfterFix: true
  }
})
```

---

### ⚠️ RECOMMENDED: None (fixing P0 issues takes precedence)

---

### 💡 OPTIONAL: None (cannot commit until P0 issues resolved)
```

**Acceptance Criteria**:
- [ ] Recommendation generation logic complete
- [ ] All transition rules implemented
- [ ] Invocation examples auto-generated
- [ ] Display format clear and actionable
- [ ] Priority-based ordering functional

---

## Integration with Agent Transition Matrix

**Update Required in `.claude/AGENTS_ARCHITECTURE.md`**:
```markdown
## Review-Consolidator Transition Matrix

| From Agent | To Agent | Type | Condition | Priority |
|------------|----------|------|-----------|----------|
| plan-task-executor | review-consolidator | CRITICAL | After code changes | P0 |
| plan-task-completer | review-consolidator | RECOMMENDED | Before completion | P1 |
| User | review-consolidator | OPTIONAL | Ad-hoc review | P2 |
| review-consolidator | plan-task-executor | CRITICAL | P0 issues found | P0 |
| review-consolidator | pre-completion-validator | CRITICAL | No P0 issues | P0 |
| review-consolidator | git-workflow-manager | OPTIONAL | Ready to commit | P2 |
```

**Cycle Flow Diagram**:
```
┌─────────────────────────────────────────────────────────────┐
│                    Review-Fix Cycle                         │
└─────────────────────────────────────────────────────────────┘

  plan-task-executor
        │
        │ (1) Write code
        ▼
  review-consolidator ────────► pre-completion-validator
        │                       (if no P0 issues)
        │
        │ (2) P0 issues found?
        ▼
  plan-task-executor
        │
        │ (3) Fix P0 issues
        ▼
  review-consolidator ────────► ESCALATE (if cycle limit)
        │                       (after 2 cycles)
        │
        │ (4) Re-review
        ▼
  [repeat cycle or complete]
```

**Acceptance Criteria**:
- [ ] Transition matrix complete
- [ ] Cycle flow diagram accurate
- [ ] All agent relationships documented
- [ ] Priority levels defined
- [ ] Condition logic specified

---

## Dependencies from Task 5.1

This task depends on:
- Cycle ID format from Task 5.1A
- Escalation triggers from Task 5.1B
- Cycle tracking metrics from Task 5.1C

These are used for:
- Passing cycle context between agents
- Determining transition recommendations
- Displaying cycle progress in recommendations

---

**Status**: READY FOR IMPLEMENTATION
**Estimated Completion**: 3-4 hours
**Dependencies**: Task 5.1 complete
