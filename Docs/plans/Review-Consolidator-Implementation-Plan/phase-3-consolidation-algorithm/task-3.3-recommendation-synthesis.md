# Task 3.3: Recommendation Synthesis

**Parent**: [Phase 3: Consolidation Algorithm Implementation](../phase-3-consolidation-algorithm.md)

**Duration**: 2 hours
**Complexity**: 6-10 tool calls per subtask
**Deliverables**: Recommendation synthesis logic in consolidation-algorithm.md

---

## 3.3A: Create recommendation extractor

**Complexity**: 8-10 tool calls
**Location**: `consolidation-algorithm.md` recommendation extraction section

### Extraction Logic

```typescript
interface Recommendation {
  theme: string;
  description: string;
  frequency: number; // How many reviewers suggested this
  confidence: number;
  relatedIssues: string[]; // Issue IDs
  effort: 'low' | 'medium' | 'high';
}

function extractRecommendations(reviewResults: ReviewResult[]): Recommendation[] {
  const recommendations: Recommendation[] = [];

  for (const result of reviewResults) {
    for (const issue of result.issues) {
      if (issue.suggestion && issue.confidence >= 0.60) {
        const theme = categorizeRecommendation(issue.suggestion);
        addOrUpdateRecommendation(recommendations, theme, issue);
      }
    }
  }

  // Rank by frequency
  return recommendations.sort((a, b) => b.frequency - a.frequency);
}

function categorizeRecommendation(suggestion: string): string {
  const keywords = {
    'refactoring': ['refactor', 'extract', 'simplify', 'clean up'],
    'testing': ['test', 'coverage', 'assertion', 'mock'],
    'documentation': ['document', 'comment', 'explain', 'describe'],
    'performance': ['optimize', 'cache', 'speed', 'efficient'],
    'security': ['secure', 'validate', 'sanitize', 'encrypt']
  };

  for (const [theme, words] of Object.entries(keywords)) {
    if (words.some(word => suggestion.toLowerCase().includes(word))) {
      return theme;
    }
  }

  return 'general';
}
```

### Requirements

- [ ] Extract recommendations from issues with confidence ≥0.60
- [ ] Categorize by theme (refactoring, testing, documentation, performance, security)
- [ ] Track recommendation frequency across reviewers
- [ ] Rank recommendations by frequency
- [ ] Link recommendations to related issue IDs

---

## 3.3B: Generate action items

**Complexity**: 6-8 tool calls
**Location**: `consolidation-algorithm.md` action items section

### Action Item Format

```markdown
## Prioritized Action Items

### Immediate Actions (P0 - Critical)
1. Fix null reference in AuthController.cs:42 (2h)
   - Related: Issues #12, #15, #23
   - Recommendation: Add null checks before property access
   - Reviewers: code-principles-reviewer, test-healer

2. Resolve DI registration for IUserService (1h)
   - Related: Issue #7
   - Recommendation: Register in Program.cs Startup
   - Reviewer: code-principles-reviewer

### Recommended Fixes (P1 - Warnings)
3. Add braces to all if statements (3h)
   - Related: 15 issues across 8 files
   - Recommendation: Auto-fix with formatter
   - Reviewer: code-style-reviewer
```

### Implementation

```typescript
interface ActionItem {
  priority: Priority;
  title: string;
  estimatedEffort: string; // e.g., "2h", "3h"
  relatedIssues: string[];
  recommendation: string;
  reviewers: string[];
}

function generateActionItems(consolidatedIssues: Issue[]): ActionItem[] {
  const items: ActionItem[] = [];

  for (const issue of consolidatedIssues) {
    items.push({
      priority: issue.severity,
      title: `${issue.message} (${issue.file}:${issue.line})`,
      estimatedEffort: estimateEffort(issue),
      relatedIssues: [issue.id, ...findRelatedIssues(issue)],
      recommendation: issue.suggestion || 'Manual fix required',
      reviewers: issue.sources?.map(s => s.reviewer) || [issue.reviewer]
    });
  }

  // Sort by priority (P0 first) then effort (low first)
  return items.sort(compareActionItems);
}
```

### Requirements

- [ ] Generate action items from consolidated issues
- [ ] Estimate effort based on issue complexity
- [ ] Link related issues together
- [ ] Sort by priority (P0 > P1 > P2) then effort (low > medium > high)

---

## 3.3C: Build recommendation summary

**Complexity**: 4-6 tool calls
**Location**: `consolidation-algorithm.md` summary section

### Summary Structure

```markdown
## Common Themes (Top 5)

1. **Missing Input Validation** (reported by 3/3 reviewers)
   - 18 occurrences across 12 files
   - Recommended: Implement validation attributes
   - Quick win: Add [Required] and [StringLength] annotations

2. **DRY Violations** (reported by 2/3 reviewers)
   - 7 occurrences in service layer
   - Recommended: Extract common methods to base class
   - Effort: 4-6 hours

3. **Test Coverage Gaps** (reported by 1/3 reviewers)
   - 23 untested methods
   - Recommended: Add unit tests for business logic
   - Long-term improvement
```

### Implementation

```typescript
interface ThemeSummary {
  theme: string;
  reportedBy: number; // Number of reviewers
  totalReviewers: number;
  occurrences: number;
  filesAffected: number;
  recommendation: string;
  quickWin: boolean;
  effort: string;
}

function buildThemeSummary(recommendations: Recommendation[]): ThemeSummary[] {
  const themes = new Map<string, ThemeSummary>();

  for (const rec of recommendations) {
    if (!themes.has(rec.theme)) {
      themes.set(rec.theme, {
        theme: rec.theme,
        reportedBy: rec.frequency,
        totalReviewers: getTotalReviewers(),
        occurrences: rec.relatedIssues.length,
        filesAffected: countUniqueFiles(rec.relatedIssues),
        recommendation: rec.description,
        quickWin: rec.effort === 'low',
        effort: formatEffort(rec.effort)
      });
    }
  }

  // Return top 5 themes by occurrences
  return Array.from(themes.values())
    .sort((a, b) => b.occurrences - a.occurrences)
    .slice(0, 5);
}
```

### Requirements

- [ ] Identify top 5 most common themes
- [ ] Show reviewer agreement (X/Y reviewers reported)
- [ ] Count occurrences and affected files
- [ ] Highlight quick wins (low effort items)
- [ ] Provide actionable recommendations

---

## Validation Tests

### Test Scenarios

1. **Recommendation Extraction Test**
   - Input: 50 issues with varying confidence (0.3-1.0)
   - Expected: Only issues with confidence ≥0.60 extracted
   - Verify: Categorization into correct themes

2. **Action Item Generation Test**
   - Input: 30 consolidated issues (mixed priorities)
   - Expected: Sorted by P0 > P1 > P2, then by effort
   - Verify: Related issues linked correctly

3. **Theme Summary Test**
   - Input: 100 recommendations across 5 themes
   - Expected: Top 5 themes by occurrence
   - Verify: Quick wins identified, effort estimated

---

## Acceptance Criteria

- [ ] Recommendations extracted from issues with ≥60% confidence
- [ ] Categorization accuracy >90% (manual validation)
- [ ] Action items sorted correctly (priority > effort)
- [ ] Theme summary shows top 5 patterns
- [ ] Quick wins identified (low effort, high impact)

---

**Status**: READY FOR IMPLEMENTATION
**Risk Level**: Low (straightforward logic)
