# Task 3.2: Priority Aggregation System

**Parent**: [Phase 3: Consolidation Algorithm Implementation](../phase-3-consolidation-algorithm.md)

**Duration**: 3-4 hours
**Complexity**: 8-12 tool calls per subtask
**Deliverables**: Priority aggregation logic in consolidation-algorithm.md

---

## 3.2A: Implement priority rules engine

**Complexity**: 10-12 tool calls
**Location**: `consolidation-algorithm.md` priority rules section

### Priority Rules

```typescript
enum Priority {
  P0 = 'P0', // Critical
  P1 = 'P1', // Warning
  P2 = 'P2'  // Improvement
}

function aggregatePriority(issues: Issue[]): Priority {
  const priorities = issues.map(i => i.severity);

  // P0 rule: ANY reviewer marks P0 → P0
  if (priorities.includes('P0')) {
    return Priority.P0;
  }

  // P1 rule: MAJORITY (≥50%) mark P1 → P1
  const p1Count = priorities.filter(p => p === 'P1').length;
  if (p1Count / priorities.length >= 0.5) {
    return Priority.P1;
  }

  // P2 rule: Default
  return Priority.P2;
}

// Special overrides
function applyPriorityOverrides(issue: Issue, priority: Priority): Priority {
  // Security issues always P0
  if (issue.category === 'security') {
    return Priority.P0;
  }

  // Breaking changes always P0
  if (issue.message.includes('breaking change')) {
    return Priority.P0;
  }

  // Test failures in critical paths → P0
  if (issue.category === 'test-failure' && isCriticalPath(issue.file)) {
    return Priority.P0;
  }

  return priority;
}
```

### Requirements

- [ ] P0 if ANY reviewer marks critical
- [ ] P1 if MAJORITY (≥50%) marks warning
- [ ] P2 as default fallback
- [ ] Security issues auto-escalate to P0
- [ ] Breaking changes auto-escalate to P0
- [ ] Critical path test failures → P0

---

## 3.2B: Create confidence weighting

**Complexity**: 8-10 tool calls
**Location**: `consolidation-algorithm.md` confidence section

### Weighted Confidence

```typescript
interface ReviewerWeight {
  name: string;
  baseWeight: number;
  categoryWeights: Map<string, number>;
}

const reviewerWeights: ReviewerWeight[] = [
  {
    name: 'code-style-reviewer',
    baseWeight: 1.0,
    categoryWeights: new Map([
      ['formatting', 1.2],
      ['naming', 1.1]
    ])
  },
  {
    name: 'code-principles-reviewer',
    baseWeight: 1.0,
    categoryWeights: new Map([
      ['solid', 1.3],
      ['dry', 1.2],
      ['architecture', 1.1]
    ])
  },
  {
    name: 'test-healer',
    baseWeight: 1.0,
    categoryWeights: new Map([
      ['test-failure', 1.5],
      ['coverage', 1.2]
    ])
  }
];

function calculateWeightedConfidence(issues: Issue[]): number {
  let totalWeight = 0;
  let weightedSum = 0;

  for (const issue of issues) {
    const reviewer = findReviewerWeight(issue.reviewer);
    const weight = reviewer.categoryWeights.get(issue.category)
      || reviewer.baseWeight;

    weightedSum += issue.confidence * weight;
    totalWeight += weight;
  }

  return weightedSum / totalWeight;
}
```

### Requirements

- [ ] Reviewer-specific base weights (all default 1.0)
- [ ] Category-specific weight multipliers
- [ ] Weighted average calculation
- [ ] Higher weights for domain expertise:
  - test-healer: 1.5x for test-failure
  - code-principles-reviewer: 1.3x for SOLID
  - code-style-reviewer: 1.2x for formatting

---

## 3.2C: Implement priority validation

**Complexity**: 6-8 tool calls
**Location**: `consolidation-algorithm.md` validation section

### Validation Rules

```typescript
function validatePriorityConsistency(consolidatedIssues: Issue[]): ValidationResult {
  const inconsistencies: string[] = [];

  // Group by similarity
  const similarGroups = groupBySimilarity(consolidatedIssues);

  for (const group of similarGroups) {
    const priorities = group.map(i => i.severity);
    const uniquePriorities = new Set(priorities);

    // If similar issues have different priorities → inconsistency
    if (uniquePriorities.size > 1) {
      inconsistencies.push(
        `Similar issues have conflicting priorities: ${group.map(i => i.id).join(', ')}`
      );

      // Reconcile: use highest priority
      const highestPriority = priorities.includes('P0') ? 'P0'
        : priorities.includes('P1') ? 'P1' : 'P2';

      for (const issue of group) {
        issue.severity = highestPriority;
      }
    }
  }

  return {
    isValid: inconsistencies.length === 0,
    inconsistencies,
    reconciled: true
  };
}
```

### Requirements

- [ ] Detect priority conflicts in similar issues
- [ ] Reconcile conflicts by choosing highest priority
- [ ] Report inconsistencies for manual review
- [ ] Auto-reconciliation for non-ambiguous cases

---

## Acceptance Criteria

- [ ] P0 aggregation: ANY reviewer triggers P0
- [ ] P1 aggregation: MAJORITY (≥50%) triggers P1
- [ ] P2 as default fallback works correctly
- [ ] Confidence weighting reflects reviewer expertise
- [ ] Priority overrides apply (security, breaking changes)
- [ ] Validation detects and reconciles conflicts

---

**Status**: ✅ COMPLETE
**Completed**: 2025-10-16
**Validation**: pre-completion-validator 95% confidence (APPROVED)
**Results**: Implemented priority aggregation system with rules engine (ANY P0, MAJORITY P1, DEFAULT P2), confidence weighting with domain expertise multipliers (test-healer 1.5x, code-principles 1.3x, code-style 1.2x), and priority validation with conflict reconciliation. Added 1,283 specification lines with 15+ comprehensive examples.
**Files Modified**:
- `.cursor/agents/review-consolidator/consolidation-algorithm.md` (2,462 → 3,745 lines, +1,283 lines for Task 3.2)

**Risk Level**: Low-Medium (priority logic complexity)
