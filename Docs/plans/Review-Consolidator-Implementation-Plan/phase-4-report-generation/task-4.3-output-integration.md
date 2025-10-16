# Task 4.3: Output Management and Integration

**Parent**: [Phase 4: Report Generation & Output](../phase-4-report-generation.md)

**Duration**: 1-2 hours
**Complexity**: 6-8 tool calls
**Deliverables**: Output file management specifications in prompt.md

---

## Output File Management

**Complexity**: 6-8 tool calls
**Location**: `prompt.md` output section

### File Naming Convention

```typescript
interface OutputFiles {
  masterReport: string; // review-report-{timestamp}.md
  appendices: string[]; // review-appendix-{reviewer}-{timestamp}.md
  traceability: string; // review-traceability-{timestamp}.md
  metadata: string; // review-metadata-{timestamp}.json
}

function generateOutputFilenames(timestamp: Date): OutputFiles {
  const dateStr = timestamp.toISOString().replace(/:/g, '-').split('.')[0];

  return {
    masterReport: `Docs/reviews/review-report-${dateStr}.md`,
    appendices: [
      `Docs/reviews/appendices/code-style-${dateStr}.md`,
      `Docs/reviews/appendices/code-principles-${dateStr}.md`,
      `Docs/reviews/appendices/test-healer-${dateStr}.md`
    ],
    traceability: `Docs/reviews/review-traceability-${dateStr}.md`,
    metadata: `Docs/reviews/metadata/review-metadata-${dateStr}.json`
  };
}
```

### Requirements

- [ ] Master report in `Docs/reviews/`
- [ ] Appendices in `Docs/reviews/appendices/`
- [ ] Traceability matrix in `Docs/reviews/`
- [ ] Metadata JSON in `Docs/reviews/metadata/`
- [ ] ISO 8601 timestamp in filenames (colon-safe)

---

## Report Versioning System

```typescript
interface ReportVersion {
  version: number; // Incremental version for same files
  timestamp: Date;
  filesReviewed: string[];
  issueCount: number;
  previousVersion?: string; // Link to previous report
}

function createVersionedReport(
  files: string[],
  issues: ConsolidatedIssue[]
): ReportVersion {
  const previousReport = findPreviousReport(files);
  const version = previousReport ? previousReport.version + 1 : 1;

  return {
    version,
    timestamp: new Date(),
    filesReviewed: files,
    issueCount: issues.length,
    previousVersion: previousReport?.filename
  };
}
```

### Requirements

- [ ] Incremental version numbers for same file sets
- [ ] Link to previous report for comparison
- [ ] Track file sets and issue counts
- [ ] Version metadata in JSON output

---

## Report Archival Strategy

```markdown
## Archival Policy
- Keep last 5 reports for each file set
- Archive reports older than 30 days to `Docs/reviews/archive/`
- Compress archived reports (gzip)
- Maintain index of all reports in `Docs/reviews/index.json`

## Cleanup Logic
1. Check report age on each new report generation
2. Move reports >30 days old to archive/
3. Keep only 5 most recent in main reviews/ directory
4. Update index.json with archival information
```

### Requirements

- [ ] Keep 5 most recent reports per file set
- [ ] Auto-archive reports >30 days old
- [ ] Compress archived reports (gzip)
- [ ] Maintain searchable index (index.json)

---

## Report Distribution Mechanism

```typescript
interface ReportDistribution {
  saveToFile: boolean; // Always true
  printToConsole: boolean; // Summary only
  notifyUser: boolean; // If P0 issues found
  openInEditor: boolean; // Optional, for interactive mode
}

function distributeReport(
  report: string,
  options: ReportDistribution
): void {
  if (options.saveToFile) {
    const filename = generateFilename();
    writeFile(filename, report);
    console.log(`Report saved: ${filename}`);
  }

  if (options.printToConsole) {
    const summary = extractExecutiveSummary(report);
    console.log(summary);
  }

  if (options.notifyUser) {
    const p0Count = countCriticalIssues(report);
    if (p0Count > 0) {
      console.log(`⚠️  ${p0Count} CRITICAL ISSUES found - review immediately!`);
    }
  }

  if (options.openInEditor) {
    openInDefaultEditor(filename);
  }
}
```

### Requirements

- [ ] Always save report to file
- [ ] Print executive summary to console
- [ ] Notify user if P0 issues found
- [ ] Optional: Open report in default editor

---

## Validation Tests

### Test Scenarios

#### 4.T5: Output Integration Test
**Input**: Complete review results
**Expected**:
- All output files created in correct locations
- Filenames follow convention
- Report archival triggered if needed
- Index updated with new report

---

## Acceptance Criteria

- [ ] All output files saved to correct directories
- [ ] Filenames follow ISO 8601 timestamp format
- [ ] Versioning system tracks report lineage
- [ ] Archival policy enforced (5 recent + archive >30 days)
- [ ] Index.json updated with new report metadata
- [ ] Console output shows summary + P0 notifications

---

**Status**: READY FOR IMPLEMENTATION
**Risk Level**: Very Low (file operations)
