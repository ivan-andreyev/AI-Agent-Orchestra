# Task 4.1: Master Report Generator

**Parent**: [Phase 4: Report Generation & Output](../phase-4-report-generation.md)

**Duration**: 3-4 hours
**Complexity**: 10-15 tool calls per subtask
**Deliverables**: Master report template and generation logic in prompt.md

---

## 4.1A: Design report structure

**Complexity**: 10-12 tool calls
**Location**: `prompt.md` report template section

### Report Structure Design

```markdown
# Consolidated Code Review Report

## Executive Summary
Brief overview (1-2 paragraphs):
- Total files reviewed
- Total issues found (before/after consolidation)
- Critical findings requiring immediate action
- Overall code quality assessment

## Critical Issues (P0) - Immediate Action Required
For each P0 issue:
- 🔴 **Issue Title** (File:Line)
  - Description: What is wrong
  - Impact: Why this is critical
  - Action: What to do immediately
  - Reviewers: Who reported this (agreement %)
  - Confidence: 🟢 High / 🟡 Medium / 🔴 Low

## Warnings (P1) - Recommended Fixes
For each P1 issue:
- 🟡 **Issue Title** (File:Line)
  - Description: What needs improvement
  - Rationale: Why this matters
  - Recommendation: Suggested fix
  - Reviewers: Who reported this
  - Confidence score

## Improvements (P2) - Optional Enhancements
Grouped by category:
- Refactoring opportunities
- Code style improvements
- Documentation suggestions
- Performance optimizations

## Common Themes Across Reviewers
Top 5-10 recurring patterns:
1. **Theme Name** (N occurrences)
   - Files affected: list
   - Recommended action: summary
   - Quick wins available: yes/no

## Prioritized Action Items
Ordered by priority and effort:
1. [P0] Fix null reference in AuthController (2h) → Issues #12, #15, #23
2. [P0] Resolve DI registration (1h) → Issue #7
3. [P1] Add input validation (4h) → Issues #3, #8, #14, #19
4. [P2] Extract common methods (6h) → Issues #5, #11, #22
```

### Implementation Requirements

- [ ] Section headers with emoji indicators
- [ ] Issue grouping by file/component
- [ ] Code snippets for context (5 lines before/after)
- [ ] Automatic table of contents for reports >100 issues
- [ ] Confidence indicators (🟢 High ≥0.8, 🟡 Medium 0.6-0.8, 🔴 Low <0.6)

---

## 4.1B: Implement report formatting

**Complexity**: 12-15 tool calls
**Location**: `consolidation-algorithm.md` formatting section

### Markdown Formatting Logic

```typescript
interface ReportSection {
  title: string;
  issues: ConsolidatedIssue[];
  summary?: string;
}

function formatReport(
  sections: ReportSection[],
  metadata: ReportMetadata
): string {
  let markdown = generateHeader(metadata);
  markdown += generateExecutiveSummary(sections, metadata);

  // Generate TOC if report is large
  if (getTotalIssues(sections) > 50) {
    markdown += generateTableOfContents(sections);
  }

  // Format each section
  for (const section of sections) {
    markdown += formatSection(section);
  }

  // Add metadata footer
  markdown += generateMetadataFooter(metadata);

  return markdown;
}

function formatSection(section: ReportSection): string {
  let markdown = `\n## ${section.title}\n\n`;

  if (section.summary) {
    markdown += `${section.summary}\n\n`;
  }

  // Group issues by file
  const byFile = groupByFile(section.issues);

  for (const [file, issues] of byFile) {
    markdown += `### ${file}\n\n`;

    for (const issue of issues) {
      markdown += formatIssue(issue);
    }
  }

  return markdown;
}

function formatIssue(issue: ConsolidatedIssue): string {
  const emoji = getPriorityEmoji(issue.severity);
  const confidence = getConfidenceIndicator(issue.confidence);

  return `
${emoji} **${issue.message}** (Line ${issue.line})
- **Impact**: ${issue.impact || 'See description'}
- **Action**: ${issue.suggestion || 'Manual fix required'}
- **Reviewers**: ${issue.reviewers.join(', ')} (${issue.agreement}% agreement)
- **Confidence**: ${confidence}

${formatCodeContext(issue)}

---
`;
}

function formatCodeContext(issue: ConsolidatedIssue): string {
  if (!issue.codeSnippet) return '';

  return `
\`\`\`csharp
// ${issue.file}:${issue.line}
${issue.codeSnippet}
\`\`\`
`;
}
```

### Formatting Guidelines

- [ ] Maximum line length: 120 characters
- [ ] Code snippets: Always show 5 lines before/after issue
- [ ] Links: Use relative paths for file references
- [ ] Lists: Use consistent bullet style (-, not *)
- [ ] Headers: Use proper hierarchy (##, ###, ####)

---

## 4.1C: Create report metadata

**Complexity**: 8-10 tool calls
**Location**: `consolidation-algorithm.md` metadata section

### Metadata Structure

```typescript
interface ReportMetadata {
  // Timing information
  timestamp: Date;
  reviewDuration: number; // milliseconds
  consolidationDuration: number; // milliseconds

  // Review scope
  filesReviewed: number;
  linesOfCode: number;
  reviewers: ReviewerMetadata[];

  // Issue statistics
  issuesBeforeConsolidation: number;
  issuesAfterConsolidation: number;
  deduplicationRatio: number;

  // Priority breakdown
  criticalIssues: number; // P0
  warnings: number; // P1
  improvements: number; // P2

  // Performance metrics
  averageReviewTimePerFile: number;
  cacheHitRate?: number;
  timeoutCount: number;

  // Quality indicators
  overallConfidence: number;
  reviewerAgreement: number; // 0-1
  coveragePercentage: number; // % of files successfully reviewed
}

interface ReviewerMetadata {
  name: string;
  status: 'success' | 'timeout' | 'error' | 'partial';
  executionTime: number;
  issuesFound: number;
  cacheHit: boolean;
}

function generateMetadataFooter(metadata: ReportMetadata): string {
  return `
---

## Review Metadata

### Execution Summary
- **Review Started**: ${metadata.timestamp.toISOString()}
- **Total Duration**: ${formatDuration(metadata.reviewDuration)}
- **Consolidation Time**: ${formatDuration(metadata.consolidationDuration)}
- **Files Reviewed**: ${metadata.filesReviewed}
- **Lines of Code**: ${metadata.linesOfCode.toLocaleString()}

### Issue Statistics
- **Issues Before Consolidation**: ${metadata.issuesBeforeConsolidation}
- **Issues After Consolidation**: ${metadata.issuesAfterConsolidation}
- **Deduplication Ratio**: ${(metadata.deduplicationRatio * 100).toFixed(1)}%
- **Critical Issues (P0)**: ${metadata.criticalIssues}
- **Warnings (P1)**: ${metadata.warnings}
- **Improvements (P2)**: ${metadata.improvements}

### Reviewer Participation
${metadata.reviewers.map(r => `
- **${r.name}**: ${r.status} (${formatDuration(r.executionTime)}, ${r.issuesFound} issues)
`).join('')}

### Quality Metrics
- **Overall Confidence**: ${(metadata.overallConfidence * 100).toFixed(1)}%
- **Reviewer Agreement**: ${(metadata.reviewerAgreement * 100).toFixed(1)}%
- **Coverage**: ${(metadata.coveragePercentage * 100).toFixed(1)}%
- **Average Time Per File**: ${formatDuration(metadata.averageReviewTimePerFile)}

---

*Generated by review-consolidator v1.0*
`;
}
```

---

## Acceptance Criteria

- [ ] Report structure includes all required sections
- [ ] Executive summary generated from statistics
- [ ] Table of contents auto-generated for large reports (>50 issues)
- [ ] Issues formatted with emoji indicators and confidence scores
- [ ] Code snippets included with 5-line context
- [ ] Metadata footer shows complete execution statistics
- [ ] Markdown syntax valid (no parsing errors)

---

**Status**: READY FOR IMPLEMENTATION
**Risk Level**: Low (formatting logic)
