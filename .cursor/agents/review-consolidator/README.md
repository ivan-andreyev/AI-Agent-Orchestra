# review-consolidator Agent

**Purpose**: Coordinate parallel review army and consolidate feedback into unified actionable report

**Status**: P0 (Critical - Agent 3/3 for MVP)

---

## Overview

review-consolidator orchestrates multiple code reviewers in parallel, consolidates their findings with intelligent deduplication, and generates comprehensive actionable reports with cycle protection.

The agent solves the critical problem of **sequential review bottlenecks** (saving 60-70% time) and **duplicate issue noise** (reducing 70-80% redundancy) through smart parallel execution and semantic deduplication.

### Key Features

- **Parallel Execution**: Reviews code with 3+ reviewers simultaneously
  - 60-70% time savings vs sequential reviews
  - Independent timeout per reviewer (5 minutes default)
  - Graceful degradation on partial failures

- **Smart Consolidation**: Exact match + semantic similarity deduplication
  - Exact match: 100% identical issue descriptions
  - Semantic similarity: >80% content overlap after normalization
  - 70-80% issue reduction in practice

- **Priority Aggregation**: Intelligent priority rules
  - P0 (CRITICAL): ANY reviewer marks P0 → aggregated P0
  - P1 (WARNING): MAJORITY of reviewers mark P1 → aggregated P1
  - P2 (IMPROVEMENT): Default for all others

- **Cycle Protection**: Prevents infinite review-fix loops
  - Max 2 review-fix cycles
  - Automatic escalation on cycle 3
  - Low improvement detection (<50% issues fixed)
  - Persistent P0 tracking

- **Performance**: Production-ready targets
  - <6 minutes for 100 files
  - <500MB memory usage
  - <30s consolidation time

---

## Usage

### Basic Invocation

```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Review authentication service implementation",
  prompt: `Review the following files for code quality issues:

Files:
- src/Services/AuthService.cs
- src/Controllers/AuthController.cs
- src/Tests/AuthServiceTests.cs

Review types: code-style-reviewer, code-principles-reviewer, test-healer
`,
  context: {
    reviewTypes: ['code-style-reviewer', 'code-principles-reviewer', 'test-healer'],
    files: [
      'src/Services/AuthService.cs',
      'src/Controllers/AuthController.cs',
      'src/Tests/AuthServiceTests.cs'
    ]
  }
})
```

### With Cycle Tracking (Re-review after fixes)

```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Re-review after fixing P0 issues (Cycle 2)",
  prompt: `Re-review files after fixing critical issues from Cycle 1:

Cycle ID: consolidator-executor-1697123456789
Iteration: 2

Files:
- src/Services/AuthService.cs (fixed DI issues)
- src/Controllers/AuthController.cs (fixed error handling)
- src/Tests/AuthServiceTests.cs (added missing tests)
`,
  context: {
    cycleId: "consolidator-executor-1697123456789",
    iteration: 2,
    previousIssueCount: 12,
    files: [
      'src/Services/AuthService.cs',
      'src/Controllers/AuthController.cs',
      'src/Tests/AuthServiceTests.cs'
    ]
  }
})
```

### Advanced: Manual Reviewer Selection

```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Security-focused review",
  prompt: `Perform security-focused review:

Files: src/**/*.cs

Reviewers:
- code-principles-reviewer (SOLID, DI)
- code-style-reviewer (naming, formatting)
- security-reviewer (future: authentication, authorization)
`,
  context: {
    reviewTypes: ['code-principles-reviewer', 'code-style-reviewer'],
    files: ['src/**/*.cs'],
    focusArea: 'security'
  }
})
```

---

## Output

### Master Report
**Location**: `Docs/reviews/review-report-{timestamp}.md`

The master report consolidates all findings into a unified, prioritized action plan.

**Structure**:
```markdown
# Code Review Report - {Timestamp}

## Executive Summary
- Total Issues: 35 (12 P0, 15 P1, 8 P2)
- Files Reviewed: 47
- Reviewers: 3 (code-style-reviewer, code-principles-reviewer, test-healer)
- Deduplication: 127 raw issues → 35 consolidated (72% reduction)

## Critical Issues (P0) - Immediate action required
[12 critical issues with file locations and recommendations]

## Warnings (P1) - Recommended fixes
[15 warning-level issues]

## Improvements (P2) - Optional enhancements
[8 improvement suggestions]

## Common Themes
1. Dependency Injection violations (8 occurrences)
2. Missing null checks (5 occurrences)
3. Test coverage gaps (4 occurrences)

## Prioritized Action Items
1. Fix all P0 issues (12 items)
2. Address P1 DI violations (5 items)
3. Add missing tests (4 items)

## Metadata
- Review Duration: 5m 42s
- Parallel Speedup: 63%
- Cache Hits: 15/47 files (32%)
```

### Appendices (Per-Reviewer Details)
**Location**: `Docs/reviews/appendices/{reviewer}-{timestamp}.md`

One appendix per reviewer with full details and traceability to master report.

**Example**: `Docs/reviews/appendices/code-style-reviewer-20250125123045.md`
```markdown
# code-style-reviewer Appendix

## Reviewer Details
- Total Issues Found: 52
- Issues in Master Report: 18 (after deduplication)
- Execution Time: 3m 12s

## All Issues (Raw)
[Complete list of 52 issues with file paths and line numbers]

## Traceability Matrix
Issue #1 (Master P0) ← code-style-reviewer Issue #5
Issue #2 (Master P1) ← code-style-reviewer Issue #12 + code-principles-reviewer Issue #8
...
```

### Traceability Matrix
**Location**: `Docs/reviews/review-traceability-{timestamp}.md`

Maps consolidated issues back to original reviewer findings.

**Structure**:
```markdown
# Review Traceability Matrix - {Timestamp}

## Consolidated Issue #1 (P0)
**File**: src/Services/AuthService.cs
**Description**: Missing null check for ILogger dependency
**Sources**:
- code-principles-reviewer Issue #3 (P0)
- code-style-reviewer Issue #5 (P1)
**Aggregated Priority**: P0 (ANY rule)

## Consolidated Issue #2 (P1)
**File**: src/Controllers/AuthController.cs
**Description**: Inconsistent error handling pattern
**Sources**:
- code-style-reviewer Issue #12 (P1)
- code-principles-reviewer Issue #8 (P1)
**Aggregated Priority**: P1 (MAJORITY rule)
```

---

## Configuration

### Reviewer Selection (Automatic)

Reviewers are selected automatically based on file types:

| File Pattern | Reviewers | Reason |
|--------------|-----------|--------|
| `*.cs` (non-test) | code-style-reviewer<br>code-principles-reviewer | C# code quality |
| `*Tests.cs`, `*Test.cs` | code-style-reviewer<br>code-principles-reviewer<br>test-healer | Test quality + coverage |
| `*.json`, `*.xml` | (skipped) | Future: config-reviewer |
| `*.md`, `*.txt` | (skipped) | Future: doc-reviewer |

**Manual Override**: Specify `reviewTypes` in `context` to override automatic selection.

### Performance Tuning

| Parameter | Default | Range | Purpose |
|-----------|---------|-------|---------|
| Parallel Timeout | 5 minutes | 2-10 min | Max time per reviewer |
| Consolidation Timeout | 30 seconds | 10-60s | Max deduplication time |
| Cache TTL | 15 minutes | 5-30 min | File content cache |
| Max Cycles | 2 | 1-3 | Review-fix cycles before escalation |
| Similarity Threshold | 0.80 | 0.70-0.95 | Semantic deduplication sensitivity |

**Tuning Recommendations**:
- **Large codebases (100+ files)**: Increase parallel timeout to 8-10 minutes
- **High-velocity changes**: Decrease cache TTL to 5-10 minutes
- **Strict deduplication**: Increase similarity threshold to 0.85-0.90
- **Lenient deduplication**: Decrease similarity threshold to 0.75-0.80

---

## Cycle Protection

### Escalation Triggers

review-consolidator automatically escalates to user after detecting:

1. **Max Cycles Reached**: 2 cycles completed, still has P0 issues
   - Example: Cycle 1 (12 P0) → Cycle 2 (8 P0) → **ESCALATE**

2. **Low Improvement Rate**: <50% issues fixed between cycles
   - Example: Cycle 1 (20 issues) → Cycle 2 (18 issues) = 10% improvement → **ESCALATE**

3. **Negative Net Progress**: More new issues than fixed
   - Example: Cycle 1 (15 issues) → Cycle 2 (18 issues) = -3 net → **ESCALATE**

4. **Persistent P0 Issues**: Same critical issues remain after 2 cycles
   - Example: "Missing null check in AuthService" appears in Cycle 1 and Cycle 2 → **ESCALATE**

### Escalation Output

**Location**: `Docs/reviews/escalation-report-{timestamp}.md`

**Structure**:
```markdown
# Escalation Report - {Timestamp}

## Escalation Trigger
Max cycles reached (2/2) with 5 persistent P0 issues

## Cycle History
### Cycle 1 (2025-01-25 10:30:00)
- Total Issues: 27 (12 P0, 10 P1, 5 P2)
- Files: 47

### Cycle 2 (2025-01-25 11:15:00)
- Total Issues: 18 (5 P0, 8 P1, 5 P2)
- Fixed: 9 issues (58% P0 fixed)
- New: 0 issues
- Persistent P0: 5 issues

## Root Cause Analysis
1. Complex architectural issue (DI container misconfiguration)
2. Missing domain knowledge (authentication flow)
3. Test infrastructure limitations (async testing)

## Persistent P0 Issues
[Detailed list of 5 critical issues that remain after 2 cycles]

## Manual Intervention Recommendations
1. Architecture Review: Redesign DI container initialization
2. Knowledge Transfer: Review OAuth2 flow documentation
3. Test Infrastructure: Upgrade to async test harness

## Alternative Approaches
1. Incremental refactoring (split P0 issues into smaller tasks)
2. Pair programming session (senior + junior developer)
3. Architecture consultation (external expert)
```

---

## Performance Targets

| Metric | Target | Typical | Worst Case | Notes |
|--------|--------|---------|------------|-------|
| **Total Review Time** (100 files) | <6 min | 5m 40s | 8m 30s | With 3 reviewers parallel |
| **Parallel Speedup** | >60% | 63% | 45% | vs sequential execution |
| **Deduplication Ratio** | >70% | 75% | 55% | Issue reduction |
| **Memory Usage** | <500MB | 420MB | 680MB | Peak during consolidation |
| **Cache Hit Rate** | >30% | 35% | 15% | File content cache |
| **Consolidation Time** | <30s | 18s | 42s | Deduplication algorithm |

**Performance Notes**:
- Parallel speedup decreases with reviewer count (3 reviewers optimal)
- Deduplication ratio improves with more similar reviewers (code-style + code-principles)
- Memory usage scales linearly with file count (4-5 MB per file)

---

## Troubleshooting

### Issue 1: Reviewer Timeout

**Symptom**: One or more reviewers exceed 5-minute timeout

**Console Output**:
```
⚠️ Reviewer timeout: code-principles-reviewer (5m 12s)
Continuing with partial results (2/3 reviewers completed)
```

**Solution**:
- Review continues with partial results
- Timeout status indicated in master report
- Missing reviewer results noted in traceability matrix
- **Action**: Increase parallel timeout if frequent (see Configuration)

### Issue 2: No Issues Found

**Symptom**: Master report shows 0 issues across all reviewers

**Console Output**:
```
✅ Review complete: 0 issues found
Files reviewed: 12
Reviewers: 3 (all completed successfully)
```

**Possible Causes**:
1. Files have no code (empty or comments-only)
2. Code quality already excellent
3. Reviewer selection logic excluded all files
4. Reviewers misconfigured or not found

**Solution**:
- Verify files have meaningful content (use `Read` tool)
- Check reviewer selection logic in SPEC.md
- Test reviewers individually to isolate issue
- Review `.cursor/agents/{reviewer}/SPEC.md` for each reviewer

### Issue 3: Excessive Duplicates (Low Deduplication)

**Symptom**: Deduplication ratio <50% (expected 70-80%)

**Console Output**:
```
⚠️ Low deduplication efficiency: 48%
Raw issues: 100 → Consolidated: 52
```

**Possible Causes**:
1. Similarity threshold too high (0.90+)
2. Reviewers using very different terminology
3. Issues genuinely distinct (not duplicates)

**Solution**:
- Lower similarity threshold to 0.75-0.80 (see Configuration)
- Review sample issues to verify they're truly duplicates
- Check reviewer outputs for formatting inconsistencies
- **Advanced**: Implement custom similarity algorithm

### Issue 4: Escalation on First Cycle

**Symptom**: Escalation triggered immediately (before Cycle 2)

**Console Output**:
```
❌ ESCALATION: Max cycles reached (1/2)
```

**Root Cause**: `cycleId` and `iteration` not passed correctly from upstream agent

**Solution**:
- Verify `context.cycleId` matches between cycles
- Ensure `context.iteration` increments (1 → 2)
- Check plan-task-executor or plan-review-iterator passes cycle info
- **Workaround**: Manually pass cycle info in prompt

### Issue 5: High Memory Usage (>500MB)

**Symptom**: Memory usage exceeds 500MB target

**Possible Causes**:
1. Large files (>100KB each)
2. Many files (>200)
3. Cache not cleared between runs

**Solution**:
- Reduce cache TTL to 5-10 minutes
- Process files in batches (50-100 at a time)
- Clear cache manually between runs
- **Advanced**: Implement streaming file reader

---

## Integration

### Upstream Agents (Who Invokes Us)

| Agent | Transition Type | Condition | Reason |
|-------|----------------|-----------|--------|
| **plan-task-executor** | CRITICAL | After code written | Mandatory review after implementation |
| **plan-task-completer** | RECOMMENDED | Before task completion | Final quality check |
| **User** | OPTIONAL | Manual ad-hoc review | On-demand code review |

**Example Invocation from plan-task-executor**:
```markdown
✅ plan-task-executor completed: Create AuthService

🔄 Recommended Next Actions:

1. 🚨 CRITICAL: review-consolidator
   Reason: Review AuthService implementation for quality issues
   Command: Use Task tool with subagent_type: "review-consolidator"
   Parameters:
     files: ['src/Services/AuthService.cs', 'src/Tests/AuthServiceTests.cs']
     reviewTypes: ['code-style-reviewer', 'code-principles-reviewer', 'test-healer']
```

### Downstream Agents (Where We Transition)

| Agent | Transition Type | Condition | Reason |
|-------|----------------|-----------|--------|
| **plan-task-executor** | CRITICAL | P0 issues found | Fix critical issues immediately |
| **pre-completion-validator** | CRITICAL | No P0 issues | Validate task completion |
| **git-workflow-manager** | OPTIONAL | All reviews passed | Commit clean code |

**Example Recommendation to plan-task-executor**:
```markdown
✅ review-consolidator completed: 12 P0 issues found

🔄 Recommended Next Actions:

1. 🚨 CRITICAL: plan-task-executor
   Reason: Fix 12 critical issues before proceeding
   Command: Use Task tool with subagent_type: "plan-task-executor"
   Parameters:
     task: "Fix P0 issues from review-consolidator"
     cycleId: "consolidator-executor-1697123456789"
     iteration: 2
     issues: [list of 12 P0 issues]
```

---

## Examples

See **[EXAMPLES.md](./EXAMPLES.md)** for detailed usage examples:

### Example 1: Simple Consolidation
- **Scenario**: 3 reviewers, 10 files
- **Output**: 35 consolidated issues from 127 raw issues (72% reduction)
- **Duration**: 3m 12s

### Example 2: Large Codebase Review
- **Scenario**: 100+ files, full codebase review
- **Performance**: 5m 42s, 63% speedup, 127→35 issues
- **Deduplication**: 75% efficiency

### Example 3: Cycle Protection (Escalation)
- **Scenario**: 2 cycles with persistent P0 issues
- **Behavior**: Automatic escalation after Cycle 2
- **Output**: Escalation report with root cause analysis

### Example 4: Timeout Handling (Partial Results)
- **Scenario**: One reviewer times out (5+ minutes)
- **Behavior**: Continue with 2/3 reviewers
- **Output**: Master report with timeout noted

---

## Architecture

See **[review-consolidator-architecture.md](../../../Docs/Architecture/Planned/review-consolidator-architecture.md)** for:
- Component diagram (Parallel Executor, Consolidation Engine, Report Generator)
- Data flow (reviewer invocation → consolidation → report generation)
- Integration points (upstream/downstream agents)
- Cycle protection design (escalation algorithm, cycle tracking)

**Key Architectural Decisions**:
1. **Parallel execution over sequential**: 60-70% time savings
2. **Smart deduplication over naive merging**: 70-80% noise reduction
3. **Cycle protection over infinite loops**: Prevents reviewer fatigue
4. **Appendices over inline details**: Traceability without clutter

---

## Agent Metadata

| Property | Value |
|----------|-------|
| **Agent Type** | Coordinator/Consolidator |
| **Tools** | Task, Bash, Glob, Grep, Read, Write, Edit, TodoWrite |
| **Model** | sonnet (fast parallel execution) |
| **Priority** | P0 (Critical for MVP) |
| **Status** | ✅ COMPLETE |
| **Deliverables** | Master report, appendices, traceability matrix, escalation report (if triggered) |
| **Dependencies** | code-style-reviewer, code-principles-reviewer, test-healer (reviewers) |

---

**Version**: 1.0.0
**Last Updated**: 2025-01-25
**Maintainer**: AI Agent Orchestra Team
