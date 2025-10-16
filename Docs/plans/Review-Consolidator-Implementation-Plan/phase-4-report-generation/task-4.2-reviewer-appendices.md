# Task 4.2: Individual Reviewer Appendices

**Parent**: [Phase 4: Report Generation & Output](../phase-4-report-generation.md)

**Duration**: 2-3 hours
**Complexity**: 10-12 tool calls per subtask
**Deliverables**: Individual reviewer appendix templates in prompt.md

---

## 4.2A: Format individual reports

**Complexity**: 10-12 tool calls
**Location**: `prompt.md` appendix section

### Appendix Structure

```markdown
## Appendix A: code-style-reviewer Full Report

### Summary
- **Execution Time**: 42 seconds
- **Issues Found**: 48
- **Confidence**: 95%
- **Status**: ✅ Success

### Detailed Findings

#### Mandatory Braces Violations (15 issues)
1. **File**: UserService.cs:42
   - **Rule**: mandatory-braces
   - **Severity**: P1
   - **Message**: Single-line if statement must use braces
   - **Suggestion**: Add braces around statement
   - **Confidence**: 0.95
   - **Consolidated Issue**: #12

2. **File**: AuthController.cs:67
   - **Rule**: mandatory-braces
   - **Severity**: P1
   - **Message**: Single-line for loop must use braces
   - **Suggestion**: Add braces around loop body
   - **Confidence**: 0.95
   - **Consolidated Issue**: #12 (merged)

[... more issues ...]

### Rules Applied
- mandatory-braces (C# Codestyle)
- naming-conventions (C# Codestyle)
- xml-documentation (C# Codestyle)
- [Total: 15 rules from csharp-codestyle.mdc]

### Files Not Reviewed
- None (all C# files reviewed)

---

## Appendix B: code-principles-reviewer Full Report

[Similar structure for code-principles-reviewer]

---

## Appendix C: test-healer Full Report

[Similar structure for test-healer]
```

### Cross-Reference Requirements

- [ ] Each appendix issue links to consolidated issue ID
- [ ] Show which issues were merged in consolidation
- [ ] Preserve original issue descriptions (not modified)
- [ ] Include reviewer-specific confidence scores
- [ ] List rules/principles applied

---

## 4.2B: Create traceability matrix

**Complexity**: 8-10 tool calls
**Location**: `consolidation-algorithm.md` traceability section

### Traceability Matrix Structure

```markdown
## Traceability Matrix

This matrix shows how individual reviewer issues were consolidated into the master report.

| Consolidated Issue | code-style | code-principles | test-healer | Priority | Confidence |
|-------------------|------------|----------------|------------|----------|-----------|
| #1: Null reference in AuthController:42 | - | Issue A12 | Issue T5 | P0 | 0.92 |
| #2: Missing braces (15 files) | Issues S3-S17 | Issue A8 | - | P1 | 0.95 |
| #3: DRY violation in UserService | - | Issue A15 | - | P1 | 0.85 |
| #4: Test timeout in AuthTests | - | - | Issue T12 | P0 | 0.90 |

### Legend
- **-**: Issue not reported by this reviewer
- **Issue ID**: Original issue ID from reviewer
- **Multiple IDs**: Issue is merge of several reports
- **Priority**: Consolidated priority (P0/P1/P2)
- **Confidence**: Weighted average confidence score
```

### Matrix Generation Logic

```typescript
interface TraceabilityEntry {
  consolidatedIssueId: string;
  consolidatedDescription: string;
  reviewerIssues: Map<string, string[]>; // reviewer -> issue IDs
  priority: Priority;
  confidence: number;
}

function generateTraceabilityMatrix(
  consolidatedIssues: ConsolidatedIssue[]
): string {
  const entries: TraceabilityEntry[] = [];

  for (const issue of consolidatedIssues) {
    const entry: TraceabilityEntry = {
      consolidatedIssueId: issue.id,
      consolidatedDescription: truncate(issue.message, 60),
      reviewerIssues: new Map(),
      priority: issue.severity,
      confidence: issue.confidence
    };

    for (const source of issue.sources) {
      if (!entry.reviewerIssues.has(source.reviewer)) {
        entry.reviewerIssues.set(source.reviewer, []);
      }
      entry.reviewerIssues.get(source.reviewer).push(source.originalId);
    }

    entries.push(entry);
  }

  return formatMatrixTable(entries);
}
```

### Requirements

- [ ] Matrix shows all consolidated issues
- [ ] Cross-references to original reviewer issue IDs
- [ ] Shows which reviewers reported each issue
- [ ] Displays consolidated priority and confidence
- [ ] Legend explains matrix notation

---

## Acceptance Criteria

- [ ] One appendix generated per active reviewer
- [ ] All original issues preserved (no modifications)
- [ ] Cross-references link to consolidated issues
- [ ] Rules/principles applied section complete
- [ ] Traceability matrix shows full issue lineage
- [ ] No orphaned issues (every source issue referenced)

---

**Status**: READY FOR IMPLEMENTATION
**Risk Level**: Low (straightforward formatting)
