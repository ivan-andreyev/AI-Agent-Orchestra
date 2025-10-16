# Review Consolidator Implementation Plan - Phase Files

**Main Plan**: [../Review-Consolidator-Implementation-Plan.md](../Review-Consolidator-Implementation-Plan.md)

## Phase Files Overview

### ✅ Phase 1: Foundation & Specifications (Day 1, 8-10h)
**File**: [phase-1-foundation.md](./phase-1-foundation.md)
- Agent specifications (review-consolidator, code-style-reviewer, code-principles-reviewer, test-healer)
- Consolidation algorithm document
- Prompt template
- Architecture documentation

### ✅ Phase 2: Parallel Execution Engine (Day 2, 8-10h)
**File**: [phase-2-parallel-execution.md](./phase-2-parallel-execution.md) + [child tasks](./phase-2-parallel-execution/)
- Parallel reviewer launcher
- Result collection framework
- Performance optimization (caching, timeouts, early termination)

### ✅ Phase 3: Consolidation Algorithm (Day 3, 8-10h)
**File**: [phase-3-consolidation-algorithm.md](./phase-3-consolidation-algorithm.md) + [child tasks](./phase-3-consolidation-algorithm/)
- Issue deduplication engine (exact + semantic)
- Priority aggregation system
- Recommendation synthesis

### ✅ Phase 4: Report Generation & Output (Day 4, 6-8h)
**File**: [phase-4-report-generation.md](./phase-4-report-generation.md) + [child tasks](./phase-4-report-generation/)
- Master report generator with executive summary, P0/P1/P2 sections
- Common themes, prioritized action items
- Individual reviewer appendices, traceability matrix

### ✅ Phase 5: Cycle Protection & Integration (Day 5, 8-10h)
**File**: [phase-5-cycle-protection.md](./phase-5-cycle-protection.md)
- Review cycle management (max 2 cycles), escalation mechanism
- Agent transition matrix integration, integration testing setup

### ✅ Phase 6: Testing & Documentation (Day 6, 6-8h)
**File**: [phase-6-testing-documentation.md](./phase-6-testing-documentation.md)
- Component testing (parallel execution, consolidation, reports)
- Integration testing with real reviewers, performance testing (<6 min target)
- README.md, AGENTS_ARCHITECTURE.md updates, usage examples

---

## Usage Instructions

### Invoking review-consolidator

**Basic Invocation** (after Phase 1 complete):
```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Review code changes for quality issues",
  prompt: `Review the following files for code quality issues:

Files to review:
- src/Services/UserService.cs
- src/Controllers/UserController.cs
- src/Models/User.cs
- tests/UserServiceTests.cs

Review types: code-style-reviewer, code-principles-reviewer, test-healer

Please launch reviewers in parallel and consolidate findings.
`,
  context: {
    files: ["src/Services/UserService.cs", "src/Controllers/UserController.cs", "src/Models/User.cs", "tests/UserServiceTests.cs"],
    reviewTypes: ['code-style-reviewer', 'code-principles-reviewer', 'test-healer']
  }
})
```

**With Cycle Tracking** (Phase 5+):
```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Re-review after fixes (Cycle 2)",
  prompt: `Re-review files after fixing P0 issues from Cycle 1:

Files: [same as Cycle 1]
Cycle ID: consolidator-executor-1697123456789
Iteration: 2

Compare with previous cycle results and calculate improvement rate.
`,
  context: {
    cycleId: "consolidator-executor-1697123456789",
    iteration: 2,
    previousIssueCount: 15
  }
})
```

### Configuration Options

**Timeout Configuration**:
- Default timeout per reviewer: 5 minutes (300,000ms)
- Total review target: <6 minutes for 100 files
- Configurable via `options.timeout` parameter

**Parallel Execution**:
- Default: Parallel execution enabled
- Fallback: Sequential execution if parallel fails
- Max concurrent reviewers: 3-5 (configurable)

**Caching**:
- Result cache TTL: 15 minutes (900,000ms)
- Cache key: hash(files + reviewer + version)
- Invalidation: Automatic on file changes

### Expected Outputs

**Consolidated Report** (Phase 4+):
- Location: `Docs/reviews/review-report-{timestamp}.md`
- Structure:
  - Executive Summary
  - Critical Issues (P0)
  - Warnings (P1)
  - Improvements (P2)
  - Common Themes
  - Prioritized Action Items
  - Metadata (timing, deduplication statistics)

**Individual Reviewer Appendices**:
- Location: `Docs/reviews/appendices/{reviewer}-{timestamp}.md`
- Contains: Original reviewer findings with cross-references

**Traceability Matrix**:
- Location: `Docs/reviews/review-traceability-{timestamp}.md`
- Maps: Consolidated issues → source reviewers

### Troubleshooting

**Issue: Reviewer Timeout**
- **Symptom**: Review takes >5 minutes per reviewer
- **Cause**: Large codebase or complex analysis
- **Solution**: Partial results returned, timeout indicated in metadata
- **Workaround**: Split files into smaller batches

**Issue: No Parallel Execution**
- **Symptom**: Reviewers run sequentially, total time ~15-20 minutes
- **Cause**: Task tool parallel pattern not working
- **Solution**: Check that all Task calls in single message
- **Workaround**: Automatic fallback to sequential

**Issue: Low Deduplication Ratio**
- **Symptom**: <50% deduplication (target >70%)
- **Cause**: Reviewers finding different issues (not duplicates)
- **Solution**: This is normal - indicates diverse feedback
- **Action**: No action needed, not an error

**Issue: Escalation Triggered**
- **Symptom**: "Escalation Report" generated after 2 cycles
- **Cause**: P0 issues persist after 2 review-fix cycles
- **Solution**: Manual intervention required (see escalation report)
- **Action**: Review unresolved issues, apply manual fixes

**Issue: Missing Reviewer Output**
- **Symptom**: Report shows "2/3 reviewers completed"
- **Cause**: One reviewer crashed or timed out
- **Solution**: Consolidation proceeds with available results
- **Action**: Check logs for reviewer error details

### Integration Workflow

**Standard Workflow**:
1. **plan-task-executor** writes code → invokes review-consolidator
2. **review-consolidator** finds P0 issues → invokes plan-task-executor to fix
3. **plan-task-executor** fixes issues → invokes review-consolidator (Cycle 2)
4. **review-consolidator** (no P0) → invokes pre-completion-validator
5. **pre-completion-validator** confirms → ready for git-workflow-manager

**Cycle Protection**:
- Max 2 cycles enforced
- If P0 issues persist after Cycle 2 → Escalation Report
- Manual intervention required for escalated issues

### Performance Expectations

**Small Codebase** (10 files, ~1500 LOC):
- Review time: <2 minutes
- Parallel execution: ~45 seconds
- Consolidation: ~15 seconds

**Medium Codebase** (50 files, ~7500 LOC):
- Review time: <4 minutes
- Parallel execution: ~2 minutes
- Consolidation: ~30 seconds

**Large Codebase** (100 files, ~15000 LOC):
- Review time: <6 minutes (target limit)
- Parallel execution: ~4 minutes
- Consolidation: ~45 seconds

---

## Implementation Sequence

```
Phase 1 (Foundation)
   ↓
Phase 2 (Parallel Execution) ← depends on Phase 1
   ↓
Phase 3 (Consolidation) ← depends on Phase 2
   ↓
Phase 4 (Report Generation) ← depends on Phase 3
   ↓
Phase 5 (Cycle Protection) ← depends on Phase 4
   ↓
Phase 6 (Testing & Docs) ← depends on Phase 5
```

## Total Effort

- **Total Duration**: 4-6 days (32-48 hours)
- **Critical Path**: Sequential through all 6 phases
- **Parallelizable**: Individual reviewer specs (Phase 1.2), Documentation (Phase 6.3)

---

**Plan Status**: READY FOR REVIEW (after fixes applied)
**Next Step**: Invoke work-plan-reviewer for validation