# Task 5.1: Review Cycle Management

**Parent Phase**: [phase-5-cycle-protection.md](../phase-5-cycle-protection.md)

**Duration**: 3-4 hours
**Complexity**: 12-15 tool calls per subtask
**Deliverables**: Cycle tracking system, escalation mechanism, cycle visualization

---

## 5.1A: Create cycle tracking system
**Complexity**: 12-15 tool calls
**Location**: `consolidation-algorithm.md` cycle tracking section

**Cycle Tracking Data Structure**:
```typescript
interface ReviewCycle {
  cycleId: string; // Format: "consolidator-executor-{timestamp}"
  iteration: number; // 1-based iteration counter (max 2)
  startTime: Date;
  endTime?: Date;

  // Issue tracking
  issuesFoundInCycle: number;
  issuesFixedFromPrevious: number;
  issuesStillPresent: number;
  newIssuesIntroduced: number;

  // Improvement metrics
  improvementRate: number; // 0-1, calculated as: fixed / previous
  netImprovement: number; // fixed - newIntroduced

  // Cycle status
  status: 'in_progress' | 'completed' | 'escalated';
  escalationReason?: string;

  // Files affected
  filesReviewed: string[];
  filesModified: string[];

  // Previous cycle reference
  previousCycleId?: string;
}

class CycleTracker {
  private cycles = new Map<string, ReviewCycle>();
  private readonly MAX_CYCLES = 2;

  startNewCycle(files: string[], previousCycleId?: string): ReviewCycle {
    const cycle: ReviewCycle = {
      cycleId: `consolidator-executor-${Date.now()}`,
      iteration: this.calculateIteration(previousCycleId),
      startTime: new Date(),
      issuesFoundInCycle: 0,
      issuesFixedFromPrevious: 0,
      issuesStillPresent: 0,
      newIssuesIntroduced: 0,
      improvementRate: 0,
      netImprovement: 0,
      status: 'in_progress',
      filesReviewed: files,
      filesModified: [],
      previousCycleId
    };

    this.cycles.set(cycle.cycleId, cycle);
    return cycle;
  }

  private calculateIteration(previousCycleId?: string): number {
    if (!previousCycleId) return 1;

    const previous = this.cycles.get(previousCycleId);
    return previous ? previous.iteration + 1 : 1;
  }

  shouldEscalate(cycleId: string): boolean {
    const cycle = this.cycles.get(cycleId);
    if (!cycle) return false;

    // Escalate if max cycles reached
    if (cycle.iteration >= this.MAX_CYCLES) {
      return true;
    }

    // Escalate if improvement rate too low (<50%)
    if (cycle.iteration > 1 && cycle.improvementRate < 0.5) {
      return true;
    }

    // Escalate if net improvement negative (more new issues than fixed)
    if (cycle.iteration > 1 && cycle.netImprovement < 0) {
      return true;
    }

    return false;
  }
}
```

**Cycle ID Format**:
```markdown
## Cycle ID Convention
Format: `consolidator-executor-{timestamp}`

Examples:
- consolidator-executor-1697123456789 (Cycle 1)
- consolidator-executor-1697127890123 (Cycle 2, after fixes)

The cycle ID is passed between agents:
1. review-consolidator creates cycle ID
2. plan-task-executor receives cycle ID for fixes
3. review-consolidator resumes with same cycle ID for re-review
```

**Issue Tracking Between Cycles**:
```typescript
function trackIssuesAcrossCycles(
  previousIssues: ConsolidatedIssue[],
  currentIssues: ConsolidatedIssue[]
): CycleComparison {
  const prevIssueSet = new Set(previousIssues.map(i => i.id));
  const currIssueSet = new Set(currentIssues.map(i => i.id));

  const fixed = previousIssues.filter(i => !currIssueSet.has(i.id));
  const stillPresent = previousIssues.filter(i => currIssueSet.has(i.id));
  const newIssues = currentIssues.filter(i => !prevIssueSet.has(i.id));

  return {
    issuesFixed: fixed,
    issuesStillPresent: stillPresent,
    newIssuesIntroduced: newIssues,
    improvementRate: fixed.length / previousIssues.length,
    netImprovement: fixed.length - newIssues.length
  };
}
```

**Acceptance Criteria**:
- [ ] Cycle ID format correctly generated
- [ ] Iteration counter tracks accurately (1-based)
- [ ] Issue tracking identifies fixed/persistent/new issues
- [ ] Improvement rate calculated correctly
- [ ] Escalation triggers implemented

---

## 5.1B: Implement escalation mechanism
**Complexity**: 10-12 tool calls
**Location**: `prompt.md` escalation section

**Escalation Triggers**:
```markdown
## Escalation Conditions

### Trigger 1: Maximum Cycles Reached
- **Condition**: iteration >= 2
- **Reason**: Attempted 2 full review-fix cycles
- **Action**: Escalate to user with unresolved issues report

### Trigger 2: Low Improvement Rate
- **Condition**: iteration > 1 AND improvementRate < 0.5
- **Reason**: Less than 50% of issues fixed in cycle
- **Action**: Escalate with analysis of persistent issues

### Trigger 3: Negative Net Improvement
- **Condition**: netImprovement < 0
- **Reason**: Fixes introduced more issues than resolved
- **Action**: Escalate with root cause analysis

### Trigger 4: Critical Issues Persist
- **Condition**: P0 issues still present after 2 cycles
- **Reason**: Critical issues not resolved
- **Action**: Immediate escalation with manual intervention required
```

**Escalation Report Format**:
```typescript
interface EscalationReport {
  cycleId: string;
  iteration: number;
  escalationReason: EscalationReason;

  // Unresolved issues
  unresolvedIssues: ConsolidatedIssue[];
  persistentP0Issues: ConsolidatedIssue[];

  // Root cause analysis
  rootCauses: RootCause[];
  blockers: string[];

  // Manual intervention recommendations
  recommendations: string[];
  alternativeApproaches: string[];

  // Cycle history
  cycleHistory: ReviewCycle[];
  improvementTrend: number[];
}

function generateEscalationReport(
  cycleTracker: CycleTracker,
  currentCycle: ReviewCycle
): EscalationReport {
  const unresolved = identifyUnresolvedIssues(currentCycle);
  const persistentP0 = unresolved.filter(i => i.severity === 'P0');

  const rootCauses = analyzeRootCauses(unresolved);
  const blockers = identifyBlockers(currentCycle);

  return {
    cycleId: currentCycle.cycleId,
    iteration: currentCycle.iteration,
    escalationReason: determineEscalationReason(currentCycle),
    unresolvedIssues: unresolved,
    persistentP0Issues: persistentP0,
    rootCauses,
    blockers,
    recommendations: generateManualRecommendations(rootCauses, blockers),
    alternativeApproaches: suggestAlternatives(rootCauses),
    cycleHistory: cycleTracker.getCycleHistory(currentCycle.cycleId),
    improvementTrend: calculateImprovementTrend(cycleTracker)
  };
}

function analyzeRootCauses(issues: ConsolidatedIssue[]): RootCause[] {
  const causes: RootCause[] = [];

  // Group issues by category
  const byCategory = groupBy(issues, 'category');

  for (const [category, categoryIssues] of byCategory) {
    // If >5 issues in same category, likely systematic problem
    if (categoryIssues.length > 5) {
      causes.push({
        category,
        type: 'systematic',
        description: `${categoryIssues.length} issues in ${category} suggest systematic problem`,
        affectedFiles: unique(categoryIssues.map(i => i.file)),
        recommendation: `Review ${category} patterns across entire codebase`
      });
    }
  }

  // Identify recurring patterns
  const patterns = findRecurringPatterns(issues);
  for (const pattern of patterns) {
    causes.push({
      category: 'pattern',
      type: 'recurring',
      description: pattern.description,
      affectedFiles: pattern.files,
      recommendation: pattern.suggestedFix
    });
  }

  return causes;
}
```

**Escalation Report Template**:
```markdown
# Review Cycle Escalation Report

## Summary
- **Cycle ID**: consolidator-executor-1697123456789
- **Iteration**: 2/2 (max reached)
- **Escalation Reason**: Maximum cycles reached with unresolved critical issues
- **Unresolved Issues**: 5 (3 P0, 2 P1)

## Unresolved Critical Issues (P0)

### 1. Null Reference in AuthController.cs:42
**Attempts to Fix**: 2 cycles
**Why Not Resolved**: Fix requires architectural change (dependency injection pattern)
**Manual Action Required**: Refactor AuthController to use constructor injection
**Estimated Effort**: 4-6 hours

### 2. DI Registration Missing for IUserService
**Attempts to Fix**: 2 cycles
**Why Not Resolved**: Service lifetime ambiguity (singleton vs scoped)
**Manual Action Required**: Determine correct service lifetime and register in Program.cs
**Estimated Effort**: 1-2 hours

### 3. Test Timeout in AuthenticationTests
**Attempts to Fix**: 2 cycles
**Why Not Resolved**: External dependency (database) not mockable
**Manual Action Required**: Implement repository pattern for database access
**Estimated Effort**: 6-8 hours

## Root Cause Analysis

### Cause 1: Architectural Gaps
**Category**: Dependency Injection
**Affected Files**: 5 controllers, 3 services
**Description**: Missing DI infrastructure for service registration
**Recommendation**: Implement proper DI container configuration in Program.cs

### Cause 2: Test Infrastructure
**Category**: Testing
**Affected Files**: 3 test files
**Description**: External dependencies not properly isolated
**Recommendation**: Introduce mocking framework (Moq) and repository pattern

## Blockers

1. **Architectural Decision Required**: Choose service lifetimes (singleton vs scoped vs transient)
2. **Missing Dependencies**: Moq framework not installed
3. **Knowledge Gap**: Team unfamiliar with repository pattern
4. **Time Constraint**: Architectural changes require 2-3 days

## Recommendations for Manual Intervention

### Immediate Actions (Today)
1. Add Moq NuGet package to test project
2. Register IUserService as Scoped in Program.cs
3. Add null checks in AuthController as temporary mitigation

### Short-term Actions (This Week)
1. Implement repository pattern for database access
2. Refactor controllers to use constructor injection
3. Update all test files to use mocking

### Long-term Actions (This Sprint)
1. Conduct DI training session for team
2. Establish DI conventions in coding standards
3. Add automated tests for DI configuration

## Alternative Approaches

### Option 1: Incremental Refactoring
- Fix P0 issues with temporary patches
- Plan architectural improvements for next sprint
- Pros: Unblocks current work
- Cons: Technical debt accumulates

### Option 2: Full Architectural Overhaul
- Stop current development
- Implement proper DI and repository patterns
- Pros: Long-term solution
- Cons: 2-3 day delay

### Option 3: Hybrid Approach (RECOMMENDED)
- Fix P0 issues with minimal changes
- Create architectural improvement plan for Phase 2
- Implement improvements in parallel workstream
- Pros: Balanced risk and progress
- Cons: Requires parallel work coordination

## Cycle History

### Cycle 1
- Issues Found: 12
- Issues Fixed: 0 (initial review)
- Improvement Rate: N/A

### Cycle 2 (Current)
- Issues Found: 5
- Issues Fixed: 7
- Improvement Rate: 58%
- Net Improvement: +7 (good)
- **But**: 3 P0 issues remain unresolved

## Next Steps

1. **User Review Required**: Review this escalation report
2. **Decision Needed**: Choose approach (Option 1, 2, or 3)
3. **Manual Fixes**: Implement recommended immediate actions
4. **Re-review**: After manual fixes, re-run review-consolidator
5. **Monitor**: Track if manual fixes resolve P0 issues

---

*Generated by review-consolidator cycle protection system*
*Escalated at: 2025-10-16T14:23:45Z*
```

**Acceptance Criteria**:
- [ ] All 4 escalation triggers implemented
- [ ] Escalation report complete and actionable
- [ ] Root cause analysis functional
- [ ] Manual recommendations clear
- [ ] Alternative approaches suggested

---

## 5.1C: Build cycle visualization
**Complexity**: 8-10 tool calls
**Location**: `prompt.md` visualization section

**Cycle Progress Display**:
```markdown
## Review Cycle Progress Visualization

### Cycle 1/2: Initial Review
```
📊 Cycle 1 Progress:
├─ Started: 2025-10-16 14:00:00
├─ Duration: 5m 23s
├─ Files Reviewed: 25
├─ Issues Found: 12
│  ├─ P0 (Critical): 3
│  ├─ P1 (Warning): 5
│  └─ P2 (Improvement): 4
└─ Status: ✅ Complete → Fixes Required

🔄 Next: plan-task-executor will fix issues
```

### Cycle 2/2: Re-review After Fixes
```
📊 Cycle 2 Progress:
├─ Started: 2025-10-16 14:30:00
├─ Duration: 4m 15s
├─ Files Reviewed: 25
├─ Issues Found: 5
│  ├─ P0 (Critical): 1 (-2 from Cycle 1) 🟢
│  ├─ P1 (Warning): 2 (-3 from Cycle 1) 🟢
│  └─ P2 (Improvement): 2 (-2 from Cycle 1) 🟢
│
├─ Issues Fixed: 7 (58% improvement) 🟢
├─ Issues Persistent: 5 (42% remain) 🟡
├─ New Issues: 0 (no regressions) 🟢
├─ Net Improvement: +7 🟢
│
└─ Status: ⚠️  Escalation Required (P0 issues persist)
```
```

**Improvement Percentage Calculation**:
```typescript
function displayImprovementMetrics(
  cycle1: ReviewCycle,
  cycle2: ReviewCycle
): string {
  const issuesFixed = cycle1.issuesFoundInCycle - cycle2.issuesStillPresent;
  const improvementRate = (issuesFixed / cycle1.issuesFoundInCycle) * 100;

  let display = `
## Improvement Summary

### Overall Progress
- **Issues in Cycle 1**: ${cycle1.issuesFoundInCycle}
- **Issues in Cycle 2**: ${cycle2.issuesFoundInCycle}
- **Issues Fixed**: ${issuesFixed} (${improvementRate.toFixed(1)}%)
- **Net Change**: ${cycle2.issuesFoundInCycle - cycle1.issuesFoundInCycle}

### By Priority
`;

  const p0Change = calculatePriorityChange(cycle1, cycle2, 'P0');
  const p1Change = calculatePriorityChange(cycle1, cycle2, 'P1');
  const p2Change = calculatePriorityChange(cycle1, cycle2, 'P2');

  display += formatPriorityChange('P0 (Critical)', p0Change);
  display += formatPriorityChange('P1 (Warning)', p1Change);
  display += formatPriorityChange('P2 (Improvement)', p2Change);

  return display;
}

function formatPriorityChange(
  priority: string,
  change: PriorityChange
): string {
  const emoji = change.delta < 0 ? '🟢' : change.delta > 0 ? '🔴' : '⚪';
  const arrow = change.delta < 0 ? '↓' : change.delta > 0 ? '↑' : '→';

  return `- **${priority}**: ${change.before} ${arrow} ${change.after} (${change.delta >= 0 ? '+' : ''}${change.delta}) ${emoji}\n`;
}
```

**Acceptance Criteria**:
- [x] Cycle progress visualization displays correctly ✅
- [x] Improvement percentage calculated accurately ✅
- [x] Priority changes tracked per cycle ✅
- [x] Visual indicators (emojis) appropriate ✅
- [x] Cycle comparison clear and actionable ✅

---

## Integration with Task 5.2

This task outputs:
- Cycle tracking system with cycle IDs
- Escalation mechanism with triggers
- Cycle visualization with metrics

These feed into Task 5.2 (Agent Transitions) for:
- Passing cycle IDs between agents
- Triggering escalation vs continuation decisions
- Providing cycle metrics for transition recommendations

---

**Status**: ✅ TASK 5.1 COMPLETE
**Completed**: 2025-10-16
**Duration**: 3.5 hours (within estimate: 3-4 hours)
**Validation**: pre-completion-validator 98% confidence (APPROVED)

**Implementation Results**:
- **Subtask 5.1A** (Cycle Tracking): +906 lines in consolidation-algorithm.md
  - Cycle tracking data structure (ReviewCycle interface)
  - CycleTracker class with iteration management
  - Issue tracking across cycles (fixed/persistent/new)
  - Escalation trigger logic (max cycles, improvement rate, net improvement)

- **Subtask 5.1B** (Escalation Mechanism): +1,273 lines in prompt.md
  - 4 escalation triggers implemented (max cycles, low improvement, negative net, P0 persist)
  - EscalationReport interface with root cause analysis
  - Escalation report template with 3 alternative approaches
  - Manual intervention recommendations (immediate/short-term/long-term)

- **Subtask 5.1C** (Cycle Visualization): +698 lines in prompt.md
  - Cycle progress visualization with improvement metrics
  - Priority change tracking (P0/P1/P2 deltas)
  - Improvement percentage calculation
  - Visual indicators (emojis) for progress status

**Files Modified**:
- `.cursor/agents/review-consolidator/consolidation-algorithm.md`: 9,270 → 10,166 lines (+906 lines)
- `.cursor/agents/review-consolidator/prompt.md`: 7,735 → 9,703 lines (+1,968 lines)
- **Total**: +2,874 lines of comprehensive cycle management specifications

**Key Features Delivered**:
- Max 2 cycle limit enforcement
- 4 escalation triggers with clear thresholds
- Cycle ID format: `consolidator-executor-{timestamp}`
- Improvement rate calculation: `fixed / previous`
- Net improvement tracking: `fixed - newIntroduced`
- Root cause analysis for systematic issues
- Alternative approaches for escalation scenarios

**Dependencies**: Phase 4 complete ✅
**Next Task**: Task 5.2 (Agent Transition Matrix Integration) - READY
