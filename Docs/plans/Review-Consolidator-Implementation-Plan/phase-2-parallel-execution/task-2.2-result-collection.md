# Task 2.2: Result Collection Framework

**Parent**: [Phase 2: Parallel Execution Engine](../phase-2-parallel-execution.md)

**Duration**: 3-4 hours
**Complexity**: 8-12 tool calls per subtask
**Deliverables**: Result collection interface and parsers in consolidation-algorithm.md

---

## 2.2A: Define result collection interface

**Complexity**: 8-10 tool calls
**File**: Update `consolidation-algorithm.md`

### ReviewResult Data Structure

```typescript
interface ReviewResult {
  reviewer_name: string;
  execution_time_ms: number;
  status: 'success' | 'timeout' | 'error' | 'partial';
  issues: Issue[];
  confidence: number; // Overall confidence 0-1
  metadata: {
    files_reviewed: number;
    rules_applied: number;
    cache_hit: boolean;
    version: string;
  };
}

interface Issue {
  id: string; // Hash for deduplication
  file: string;
  line: number;
  column?: number;
  severity: 'P0' | 'P1' | 'P2';
  category: string;
  rule: string;
  message: string;
  suggestion?: string;
  confidence: number; // 0-1
  reviewer: string;
}
```

---

## 2.2B: Implement result parsing (decomposed into 3 subtasks)

### 2.2B-1: Create code-style-reviewer parser

**Complexity**: 12 tool calls
**Location**: `prompt.md` parsing section - code-style parser

#### Parser Implementation

```typescript
function parseStyleReviewerJSON(output: string): ReviewResult {
  const parsed = JSON.parse(output);
  const issues: Issue[] = [];

  for (const rawIssue of parsed.issues) {
    issues.push({
      id: generateHash(rawIssue.file, rawIssue.line, rawIssue.rule),
      file: rawIssue.file,
      line: rawIssue.line,
      column: rawIssue.column,
      severity: rawIssue.severity,
      category: 'code-style',
      rule: rawIssue.rule,
      message: rawIssue.message,
      suggestion: rawIssue.suggestion,
      confidence: rawIssue.confidence || 0.95,
      reviewer: 'code-style-reviewer'
    });
  }

  return {
    reviewer_name: 'code-style-reviewer',
    execution_time_ms: parsed.execution_time || 0,
    status: 'success',
    issues,
    confidence: 0.95,
    metadata: {
      files_reviewed: parsed.files_reviewed || 0,
      rules_applied: parsed.rules_applied || 0,
      cache_hit: parsed.cache_hit || false,
      version: parsed.version || '1.0'
    }
  };
}
```

#### Error Handling

- [ ] Handle malformed JSON gracefully
- [ ] Extract partial issues if parsing fails
- [ ] Return confidence 0.5 for fallback parsing
- [ ] Log parse errors for debugging

---

### 2.2B-2: Create code-principles-reviewer parser

**Complexity**: 12 tool calls
**Location**: `prompt.md` parsing section - code-principles parser

#### Parser Implementation

```typescript
function parsePrinciplesMarkdown(output: string): ReviewResult {
  const issues: Issue[] = [];
  const lines = output.split('\n');

  let currentFile = '';
  let currentCategory = '';

  for (const line of lines) {
    // Parse file headers: ### UserService.cs
    if (line.startsWith('### ') && line.endsWith('.cs')) {
      currentFile = line.substring(4).trim();
      continue;
    }

    // Parse category headers: ## SOLID Violations
    if (line.startsWith('## ')) {
      currentCategory = line.substring(3).trim();
      continue;
    }

    // Parse issue lines: - Line 15: Single Responsibility violated
    const issueMatch = line.match(/^- Line (\d+): (.+)$/);
    if (issueMatch) {
      const [, lineNum, message] = issueMatch;
      issues.push({
        id: generateHash(currentFile, parseInt(lineNum), currentCategory),
        file: currentFile,
        line: parseInt(lineNum),
        severity: extractSeverityFromNextLines(lines, line),
        category: currentCategory,
        rule: currentCategory,
        message,
        confidence: 0.85,
        reviewer: 'code-principles-reviewer'
      });
    }
  }

  return {
    reviewer_name: 'code-principles-reviewer',
    execution_time_ms: 0,
    status: 'success',
    issues,
    confidence: 0.85,
    metadata: {
      files_reviewed: new Set(issues.map(i => i.file)).size,
      rules_applied: new Set(issues.map(i => i.category)).size,
      cache_hit: false,
      version: '1.0'
    }
  };
}
```

#### Error Handling

- [ ] Handle irregular markdown formatting
- [ ] Skip unparseable sections
- [ ] Extract what's possible from malformed output
- [ ] Log structural issues

---

### 2.2B-3: Create test-healer parser

**Complexity**: 12 tool calls
**Location**: `prompt.md` parsing section - test-healer parser

#### Parser Implementation

```typescript
function parseTestHealerXML(output: string): ReviewResult {
  const issues: Issue[] = [];

  // Parse XML or mixed format (XML + recommendations)
  const xmlMatch = output.match(/<TestResults>([\s\S]*)<\/TestResults>/);

  if (xmlMatch) {
    const xmlContent = xmlMatch[1];
    const failedTests = xmlContent.matchAll(
      /<FailedTest file="([^"]+)" line="(\d+)" reason="([^"]+)"\/>/g
    );

    for (const [, file, line, reason] of failedTests) {
      issues.push({
        id: generateHash(file, parseInt(line), 'test-failure'),
        file,
        line: parseInt(line),
        severity: 'P0', // Test failures are critical
        category: 'test-failure',
        rule: 'test-must-pass',
        message: `Test failed: ${reason}`,
        confidence: 0.90,
        reviewer: 'test-healer'
      });
    }
  }

  // Parse recommendations section (markdown after XML)
  const recommendationsMatch = output.match(/## Recommendations\n([\s\S]+)/);
  if (recommendationsMatch) {
    const recommendations = parseMarkdownRecommendations(recommendationsMatch[1]);
    issues.push(...recommendations.map(r => ({
      ...r,
      category: 'test-improvement',
      reviewer: 'test-healer'
    })));
  }

  return {
    reviewer_name: 'test-healer',
    execution_time_ms: 0,
    status: 'success',
    issues,
    confidence: 0.90,
    metadata: {
      files_reviewed: new Set(issues.map(i => i.file)).size,
      rules_applied: 2, // test-must-pass + recommendations
      cache_hit: false,
      version: '1.0'
    }
  };
}
```

#### Error Handling

- [ ] Handle missing XML tags
- [ ] Parse recommendations separately from failures
- [ ] Extract failures even if recommendations fail to parse
- [ ] Return partial results on XML parse errors

---

### Shared Error Handling Wrapper

```typescript
function parseReviewerOutput(output: string, reviewer: string): ReviewResult {
  try {
    switch(reviewer) {
      case 'code-style-reviewer':
        return parseStyleReviewerJSON(output);
      case 'code-principles-reviewer':
        return parsePrinciplesMarkdown(output);
      case 'test-healer':
        return parseTestHealerXML(output);
      default:
        throw new Error(`Unknown reviewer: ${reviewer}`);
    }
  } catch (error) {
    // Return partial result with what we could parse
    return {
      reviewer_name: reviewer,
      status: 'partial',
      issues: extractFallbackIssues(output),
      confidence: 0.5, // Low confidence for fallback parsing
      error: error.message
    };
  }
}
```

---

## 2.2C: Create result storage

**Complexity**: 8-10 tool calls
**Location**: `consolidation-algorithm.md` storage section

### In-Memory Storage with TTL

```typescript
class ResultCache {
  private cache = new Map<string, CachedResult>();
  private readonly TTL = 900000; // 15 minutes

  store(key: string, result: ReviewResult): void {
    this.cache.set(key, {
      result,
      timestamp: Date.now(),
      expires: Date.now() + this.TTL
    });
  }

  retrieve(key: string): ReviewResult | null {
    const cached = this.cache.get(key);
    if (!cached) return null;

    if (Date.now() > cached.expires) {
      this.cache.delete(key);
      return null;
    }

    return cached.result;
  }

  // Generate cache key from file list + reviewer
  getCacheKey(files: string[], reviewer: string): string {
    const fileHash = hashFiles(files);
    return `${reviewer}:${fileHash}`;
  }
}
```

---

## Acceptance Criteria

- [ ] All three parsers handle their respective formats
- [ ] Error handling gracefully degrades to partial results
- [ ] Result storage caches with 15-minute TTL
- [ ] Cache invalidation works on file changes
- [ ] Parsing tested with malformed outputs

---

**Status**: READY FOR IMPLEMENTATION
**Risk Level**: Medium (parsing complexity)
