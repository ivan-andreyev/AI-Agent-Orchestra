# Task 2.1: Parallel Reviewer Launcher

**Parent**: [Phase 2: Parallel Execution Engine](../phase-2-parallel-execution.md)

**Duration**: 3-4 hours
**Complexity**: 12-15 tool calls per subtask
**Deliverables**: Parallel execution orchestrator specifications in prompt.md

---

## 2.1A: Create parallel execution orchestrator

**Complexity**: 12-15 tool calls
**Location**: Update `prompt.md` with orchestration logic

### Implementation Pattern

```typescript
// CORRECT: Parallel execution in single message
async function launchParallelReviews(files: string[], reviewTypes: string[]) {
  const tasks = reviewTypes.map(type => ({
    tool: "Task",
    params: {
      subagent_type: type,
      description: `Review ${files.length} files for ${type} issues`,
      prompt: generateReviewPrompt(type, files)
    }
  }));

  // Execute all tasks in parallel (single message)
  return await executeTasks(tasks);
}

// ANTI-PATTERN: Sequential execution (creates unnecessary delay)
// DO NOT: await task1, then await task2, then await task3
```

### Key Requirements

- [ ] Single message with multiple Task tool calls
- [ ] Timeout handling per reviewer (5 minutes)
- [ ] Fallback to sequential if parallel fails
- [ ] Progress tracking during execution

---

## 2.1B: Implement reviewer selection logic

**Complexity**: 8-10 tool calls
**Location**: `consolidation-algorithm.md` section on reviewer selection

### File Type Mapping

```markdown
## Reviewer Selection Rules

### C# Files (*.cs)
- code-style-reviewer: ALWAYS
- code-principles-reviewer: ALWAYS
- test-healer: IF contains "Test" in filename

### Test Files (*Tests.cs, *.Tests.cs)
- code-style-reviewer: ALWAYS
- code-principles-reviewer: ALWAYS
- test-healer: ALWAYS (priority)

### Configuration Files (*.json, *.xml, *.yaml)
- code-style-reviewer: SKIP
- code-principles-reviewer: SKIP
- test-healer: SKIP

### Documentation (*.md)
- ALL reviewers: SKIP (future: documentation-reviewer)
```

### Dynamic Reviewer List Generation

```typescript
function selectReviewers(files: string[]): string[] {
  const reviewers = new Set<string>();

  for (const file of files) {
    if (file.endsWith('.cs')) {
      reviewers.add('code-style-reviewer');
      reviewers.add('code-principles-reviewer');

      if (file.includes('Test')) {
        reviewers.add('test-healer');
      }
    }
  }

  return Array.from(reviewers);
}
```

---

## 2.1C: Create execution monitoring

**Complexity**: 10-12 tool calls
**Location**: `prompt.md` monitoring section

### Progress Tracking Format

```markdown
## Execution Progress Display

Starting parallel review of 25 files with 3 reviewers...

[1/3] code-style-reviewer: ⏳ Running (elapsed: 45s)
[2/3] code-principles-reviewer: ✅ Complete (112 issues found)
[3/3] test-healer: ⏳ Running (elapsed: 32s)

Overall progress: 33% complete
Estimated time remaining: 2-3 minutes
```

### Timeout Detection

```typescript
const REVIEWER_TIMEOUT = 300000; // 5 minutes

interface ReviewerStatus {
  name: string;
  status: 'pending' | 'running' | 'complete' | 'timeout' | 'error';
  startTime?: number;
  endTime?: number;
  issueCount?: number;
  error?: string;
}

// Monitor and update status every 30 seconds
// If elapsed > REVIEWER_TIMEOUT, mark as timeout
// Continue with partial results
```

---

## Integration Steps

1. Add orchestration logic to `prompt.md`
2. Implement reviewer selection in `consolidation-algorithm.md`
3. Create monitoring UI specification
4. Test with sample file sets (10, 25, 50 files)

---

## Acceptance Criteria

- [ ] Parallel execution launches all reviewers simultaneously
- [ ] Reviewer selection accurately maps to file types
- [ ] Timeout handling prevents hanging (5-minute limit)
- [ ] Progress tracking shows real-time status
- [ ] Fallback to sequential works if parallel fails

---

**Status**: READY FOR IMPLEMENTATION
**Risk Level**: Medium (parallel execution complexity)
