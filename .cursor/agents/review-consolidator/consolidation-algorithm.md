# Review Consolidation Algorithm

**Version**: 1.0
**Date**: 2025-10-16
**Purpose**: Define objective, reproducible methodology for consolidating multiple reviewer reports into a single unified output

---

## Overview

The Review Consolidation Algorithm processes multiple independent reviewer reports (e.g., code-style-reviewer, code-principles-reviewer, test-healer) and produces a single, deduplicated, prioritized report with aggregated confidence scores and synthesized recommendations.

**Design Philosophy**:
- **Objective**: Eliminate duplicates deterministically, minimize subjective aggregation
- **Prioritized**: Critical issues (P0) surface immediately, low-value issues filtered
- **Confidence-based**: Weight reviewer expertise, filter low-confidence findings
- **Actionable**: Synthesized recommendations ranked by frequency and relevance

**Expected Outcomes**:
- Deduplication rate: 60-70% reduction in issue count
- Aggregation quality: High-confidence issues prioritized (P0 > P1 > P2)
- Recommendation synthesis: Top 5-10 actionable themes
- Processing time: <2 seconds for typical 3-reviewer report set

---

## Reviewer Selection Algorithm

**Purpose**: Dynamically determine which reviewers to invoke based on file types and review context

**Design Philosophy**:
- **File-driven**: Select reviewers based on file extensions and patterns
- **Context-aware**: Adjust reviewer selection based on review context (post-implementation vs pre-commit)
- **Configurable**: Allow override via explicit reviewer list parameter
- **Efficient**: Skip irrelevant reviewers for configuration/documentation files

### File Type to Reviewer Mapping

**Core Mapping Rules**:

```typescript
/**
 * Maps file types to required reviewers
 * @param files Array of file paths to review
 * @returns Set of reviewer IDs to invoke
 */
function selectReviewers(files: string[]): Set<string> {
  const reviewers = new Set<string>();

  for (const file of files) {
    // C# Production Code Files
    if (file.endsWith('.cs') && !file.includes('Test')) {
      reviewers.add('code-style-reviewer');        // ALWAYS for .cs files
      reviewers.add('code-principles-reviewer');   // ALWAYS for .cs files
    }

    // C# Test Files
    if (file.endsWith('.cs') && file.includes('Test')) {
      reviewers.add('code-style-reviewer');        // ALWAYS for test files
      reviewers.add('code-principles-reviewer');   // ALWAYS for test files
      reviewers.add('test-healer');                // ALWAYS (priority) for test files
    }

    // Configuration Files - SKIP ALL REVIEWERS
    if (file.match(/\.(json|xml|yaml|yml|config)$/)) {
      // Explicitly skip - no reviewers needed for configuration
      continue;
    }

    // Documentation Files - SKIP ALL REVIEWERS (for now)
    if (file.endsWith('.md')) {
      // Future: documentation-reviewer
      continue;
    }

    // Other Code Files (TypeScript, JavaScript, etc.) - Future support
    if (file.match(/\.(ts|tsx|js|jsx)$/)) {
      // Future: Adapt to support multiple languages
      continue;
    }
  }

  return reviewers;
}
```

### File Type Mapping Table

| File Pattern | code-style-reviewer | code-principles-reviewer | test-healer | Notes |
|--------------|---------------------|--------------------------|-------------|-------|
| `*.cs` (no "Test") | ✅ ALWAYS | ✅ ALWAYS | ❌ SKIP | Production C# code |
| `*Test.cs` | ✅ ALWAYS | ✅ ALWAYS | ✅ ALWAYS (priority) | C# test files |
| `*Tests.cs` | ✅ ALWAYS | ✅ ALWAYS | ✅ ALWAYS (priority) | C# test files |
| `*.Tests.cs` | ✅ ALWAYS | ✅ ALWAYS | ✅ ALWAYS (priority) | C# test files |
| `*.json` | ❌ SKIP | ❌ SKIP | ❌ SKIP | Configuration files |
| `*.xml` | ❌ SKIP | ❌ SKIP | ❌ SKIP | Configuration files |
| `*.yaml`, `*.yml` | ❌ SKIP | ❌ SKIP | ❌ SKIP | Configuration files |
| `*.config` | ❌ SKIP | ❌ SKIP | ❌ SKIP | Configuration files |
| `*.md` | ❌ SKIP | ❌ SKIP | ❌ SKIP | Documentation (future: documentation-reviewer) |
| `*.ts`, `*.tsx` | 🔮 FUTURE | 🔮 FUTURE | 🔮 FUTURE | TypeScript support planned |
| `*.js`, `*.jsx` | 🔮 FUTURE | 🔮 FUTURE | 🔮 FUTURE | JavaScript support planned |

### Context-Based Reviewer Selection

**Review Context Modifiers**:

```typescript
/**
 * Adjusts reviewer selection based on review context
 * @param baseReviewers Set of reviewers selected by file type
 * @param context Review context (post-implementation, pre-commit, etc.)
 * @returns Adjusted set of reviewer IDs
 */
function adjustForContext(
  baseReviewers: Set<string>,
  context: ReviewContext
): Set<string> {

  switch (context) {
    case 'post-implementation':
      // Comprehensive review - use ALL selected reviewers
      return baseReviewers;

    case 'pre-commit':
      // Fast validation - skip test-healer for speed
      const fastReviewers = new Set(baseReviewers);
      fastReviewers.delete('test-healer');
      return fastReviewers;

    case 'technical-debt':
      // Deep analysis - use ALL reviewers + extended timeout
      return baseReviewers;

    case 'ad-hoc':
      // User-driven - use ALL selected reviewers
      return baseReviewers;

    default:
      return baseReviewers;
  }
}

type ReviewContext = 'post-implementation' | 'pre-commit' | 'technical-debt' | 'ad-hoc';
```

### Context Impact Table

| Review Context | Reviewer Adjustment | Timeout | Rationale |
|----------------|---------------------|---------|-----------|
| `post-implementation` | Use ALL selected reviewers | 5 min | Comprehensive post-feature review |
| `pre-commit` | SKIP test-healer | 5 min | Fast validation before commit, focus on style+principles |
| `technical-debt` | Use ALL selected reviewers | 10 min | Deep analysis requires all reviewers + more time |
| `ad-hoc` | Use ALL selected reviewers | 5 min | User-initiated, use full coverage |

### Dynamic Reviewer List Generation

**Complete Selection Algorithm**:

```typescript
/**
 * Generates final reviewer list with context adjustments
 * @param files Array of file paths to review
 * @param context Review context
 * @param explicitReviewers Optional explicit reviewer override
 * @returns Final array of reviewer IDs to invoke
 */
function generateReviewerList(
  files: string[],
  context: ReviewContext,
  explicitReviewers?: string[]
): string[] {

  // STEP 1: Check for explicit override
  if (explicitReviewers && explicitReviewers.length > 0) {
    // User explicitly specified reviewers - use those
    return explicitReviewers;
  }

  // STEP 2: Select reviewers based on file types
  const baseReviewers = selectReviewers(files);

  // STEP 3: Adjust for review context
  const adjustedReviewers = adjustForContext(baseReviewers, context);

  // STEP 4: Validate minimum reviewer requirement
  if (adjustedReviewers.size === 0) {
    throw new Error('No reviewers selected - all files are configuration/documentation');
  }

  // STEP 5: Return sorted array for consistent ordering
  return Array.from(adjustedReviewers).sort();
}
```

### Edge Case Handling

**Edge Case 1: Mixed File Types**

```typescript
// Input: Mixed C# code and test files
const files = [
  'src/Services/AuthService.cs',           // Production code
  'src/Tests/AuthServiceTests.cs',         // Test file
  'src/Program.cs'                         // Production code
];

// Expected reviewers:
// - code-style-reviewer (all .cs files)
// - code-principles-reviewer (all .cs files)
// - test-healer (test files detected)

const reviewers = selectReviewers(files);
// Result: ['code-principles-reviewer', 'code-style-reviewer', 'test-healer']
```

**Edge Case 2: Configuration Only**

```typescript
// Input: Only configuration files
const files = [
  'appsettings.json',
  'launchSettings.json',
  'web.config'
];

// Expected: Throw error (no reviewers applicable)
try {
  const reviewers = generateReviewerList(files, 'post-implementation');
} catch (error) {
  // Error: "No reviewers selected - all files are configuration/documentation"
}
```

**Edge Case 3: Pre-Commit Context Optimization**

```typescript
// Input: Mixed files with pre-commit context
const files = [
  'src/Services/AuthService.cs',
  'src/Tests/AuthServiceTests.cs'
];

// Base reviewers: ['code-style-reviewer', 'code-principles-reviewer', 'test-healer']
// Context: 'pre-commit'
// Adjustment: Remove test-healer for speed

const reviewers = generateReviewerList(files, 'pre-commit');
// Result: ['code-principles-reviewer', 'code-style-reviewer']
// Benefit: ~33% faster (2 reviewers instead of 3)
```

**Edge Case 4: Explicit Reviewer Override**

```typescript
// Input: User explicitly requests only code-style-reviewer
const files = [
  'src/Services/AuthService.cs',
  'src/Tests/AuthServiceTests.cs'
];

const reviewers = generateReviewerList(
  files,
  'post-implementation',
  ['code-style-reviewer']  // Explicit override
);

// Result: ['code-style-reviewer']
// Rationale: User override takes precedence over automatic selection
```

### Performance Considerations

**Selection Performance**:
- File type detection: O(n) where n = number of files
- Reviewer set operations: O(r) where r = number of reviewers (~3)
- Total: O(n) - linear time complexity
- Expected time: <10ms for typical file sets (10-50 files)

**Caching Strategy** (Future Enhancement):
```typescript
// Cache reviewer selection for identical file lists
const reviewerCache = new Map<string, string[]>();

function generateReviewerListCached(
  files: string[],
  context: ReviewContext
): string[] {
  const cacheKey = `${files.sort().join('|')}:${context}`;

  if (reviewerCache.has(cacheKey)) {
    return reviewerCache.get(cacheKey)!;
  }

  const reviewers = generateReviewerList(files, context);
  reviewerCache.set(cacheKey, reviewers);
  return reviewers;
}
```

### Integration with Parallel Execution

**Complete Workflow**:

```typescript
// STEP 1: Generate reviewer list
const reviewers = generateReviewerList(files, context, explicitReviewers);

// STEP 2: Build parallel Task calls
const tasks = reviewers.map(reviewerType => ({
  tool: "Task",
  params: {
    subagent_type: reviewerType,
    description: `Review ${files.length} files for ${reviewerType} issues`,
    prompt: generateReviewPrompt(reviewerType, files),
    timeout: getTimeoutForContext(context)
  }
}));

// STEP 3: Execute all tasks in parallel
const results = await executeTasks(tasks);

function getTimeoutForContext(context: ReviewContext): number {
  switch (context) {
    case 'technical-debt': return 600000;  // 10 minutes
    default: return 300000;                // 5 minutes
  }
}
```

### Validation Rules

**Pre-Execution Validation**:

```typescript
/**
 * Validates reviewer selection before execution
 * @param reviewers Selected reviewer IDs
 * @param files Files to review
 * @throws Error if validation fails
 */
function validateReviewerSelection(reviewers: string[], files: string[]): void {
  // Rule 1: At least one reviewer required
  if (reviewers.length === 0) {
    throw new Error('No reviewers selected - cannot proceed with empty reviewer list');
  }

  // Rule 2: All reviewers must be valid
  const validReviewers = ['code-style-reviewer', 'code-principles-reviewer', 'test-healer'];
  for (const reviewer of reviewers) {
    if (!validReviewers.includes(reviewer)) {
      throw new Error(`Invalid reviewer: ${reviewer}`);
    }
  }

  // Rule 3: Maximum 5 reviewers (prevent excessive parallelization)
  if (reviewers.length > 5) {
    throw new Error(`Too many reviewers: ${reviewers.length} (maximum: 5)`);
  }

  // Rule 4: At least one reviewable file
  const reviewableFiles = files.filter(f =>
    !f.match(/\.(json|xml|yaml|yml|config|md)$/)
  );

  if (reviewableFiles.length === 0) {
    throw new Error('No reviewable files - all files are configuration/documentation');
  }
}
```

### Examples

**Example 1: Production Code Only**
```typescript
Input:
  files: ['Services/AuthService.cs', 'Controllers/AuthController.cs']
  context: 'post-implementation'

Output:
  reviewers: ['code-principles-reviewer', 'code-style-reviewer']
  rationale: No test files detected, skip test-healer
```

**Example 2: Mixed Code and Tests**
```typescript
Input:
  files: [
    'Services/AuthService.cs',
    'Tests/Services/AuthServiceTests.cs',
    'Controllers/AuthController.cs'
  ]
  context: 'post-implementation'

Output:
  reviewers: ['code-principles-reviewer', 'code-style-reviewer', 'test-healer']
  rationale: Test files detected, include all reviewers
```

**Example 3: Pre-Commit Optimization**
```typescript
Input:
  files: [
    'Services/AuthService.cs',
    'Tests/Services/AuthServiceTests.cs'
  ]
  context: 'pre-commit'

Output:
  reviewers: ['code-principles-reviewer', 'code-style-reviewer']
  rationale: Pre-commit context - skip test-healer for speed (2-3 min instead of 5 min)
```

---

## Result Collection Interfaces

**Purpose**: Define standardized data structures for collecting and storing reviewer outputs

**Design Philosophy**:
- **Uniform**: All reviewers return ReviewResult regardless of internal format
- **Extensible**: Metadata field allows reviewer-specific data
- **Type-safe**: Explicit status enum prevents invalid states
- **Traceable**: Each issue has unique ID for deduplication tracking

### ReviewResult Interface

**Core Data Structure** for storing individual reviewer outputs:

```typescript
/**
 * Standard result structure returned by each reviewer
 * @interface ReviewResult
 */
interface ReviewResult {
  // Reviewer Identification
  reviewer_name: string;              // e.g., "code-style-reviewer"

  // Execution Metadata
  execution_time_ms: number;          // Time taken by reviewer (milliseconds)
  status: ReviewStatus;               // Execution outcome

  // Issues Found
  issues: Issue[];                    // Array of detected issues

  // Quality Metrics
  confidence: number;                 // Overall reviewer confidence (0.0-1.0)

  // Additional Context
  metadata: ReviewMetadata;           // Reviewer-specific metadata

  // Error Information (optional)
  error?: string;                     // Error message if status is 'error' or 'partial'
}

/**
 * Execution status enum
 */
type ReviewStatus =
  | 'success'    // Reviewer completed successfully
  | 'timeout'    // Reviewer exceeded time limit
  | 'error'      // Reviewer failed with error
  | 'partial';   // Reviewer returned partial results (some parsing failed)

/**
 * Reviewer-specific metadata
 */
interface ReviewMetadata {
  files_reviewed: number;             // Number of files analyzed
  rules_applied: number;              // Number of rules checked
  cache_hit: boolean;                 // Whether result came from cache
  version: string;                    // Reviewer version (e.g., "1.0")

  // Optional fields for specific reviewers
  test_coverage?: number;             // test-healer: coverage percentage
  architecture_score?: number;        // architecture-documenter: score
  [key: string]: any;                 // Allow additional metadata
}
```

**ReviewResult Examples**:

**Example 1: Successful Review**
```typescript
const successResult: ReviewResult = {
  reviewer_name: 'code-style-reviewer',
  execution_time_ms: 4200,
  status: 'success',
  issues: [
    /* array of issues */
  ],
  confidence: 0.95,
  metadata: {
    files_reviewed: 5,
    rules_applied: 12,
    cache_hit: false,
    version: '1.0'
  }
};
```

**Example 2: Timeout Result**
```typescript
const timeoutResult: ReviewResult = {
  reviewer_name: 'code-principles-reviewer',
  execution_time_ms: 300000,  // 5 minutes
  status: 'timeout',
  issues: [],
  confidence: 0.0,
  metadata: {
    files_reviewed: 0,
    rules_applied: 0,
    cache_hit: false,
    version: '1.0'
  },
  error: 'Reviewer exceeded 5 minute timeout'
};
```

**Example 3: Partial Result (Parse Error)**
```typescript
const partialResult: ReviewResult = {
  reviewer_name: 'test-healer',
  execution_time_ms: 3800,
  status: 'partial',
  issues: [
    /* issues that could be parsed */
  ],
  confidence: 0.5,  // Lower confidence due to partial parsing
  metadata: {
    files_reviewed: 3,  // Only 3/5 files parsed successfully
    rules_applied: 5,
    cache_hit: false,
    version: '1.0'
  },
  error: 'XML parse error on TestResults.xml, recovered partial results'
};
```

### Issue Interface

**Core Data Structure** for representing individual code issues:

```typescript
/**
 * Standardized issue structure used across all reviewers
 * @interface Issue
 */
interface Issue {
  // Unique Identification
  id: string;                         // Hash for deduplication (generated from file+line+category)

  // Location Information
  file: string;                       // Relative file path (e.g., "Services/AuthService.cs")
  line: number;                       // Line number where issue occurs
  column?: number;                    // Optional column number (if available)

  // Classification
  severity: IssueSeverity;            // Priority level (P0/P1/P2)
  category: string;                   // Issue category (e.g., "naming_convention")
  rule: string;                       // Rule identifier (e.g., "csharp-naming-PascalCase")

  // Description
  message: string;                    // Human-readable issue description
  suggestion?: string;                // Optional fix suggestion

  // Quality Metrics
  confidence: number;                 // Reviewer confidence in this issue (0.0-1.0)

  // Attribution
  reviewer: string;                   // Reviewer that detected this issue

  // Optional Context
  code_snippet?: string;              // Optional code snippet showing issue
  fix_example?: string;               // Optional example of corrected code
}

/**
 * Issue severity enum
 */
type IssueSeverity =
  | 'P0'    // Critical - Must fix immediately (blocks commit)
  | 'P1'    // Important - Should fix before merge (warnings)
  | 'P2';   // Informational - Nice to fix (improvements)

/**
 * Category constants for common issue types
 */
const IssueCategory = {
  // Code Style
  NAMING_CONVENTION: 'naming_convention',
  FORMATTING: 'formatting',
  BRACES: 'mandatory_braces',
  DOCUMENTATION: 'xml_documentation',

  // Code Principles
  SOLID_SRP: 'solid_srp',
  SOLID_OCP: 'solid_ocp',
  SOLID_LSP: 'solid_lsp',
  SOLID_ISP: 'solid_isp',
  SOLID_DIP: 'solid_dip',
  DRY_VIOLATION: 'dry_violation',
  COMPLEXITY: 'complexity',

  // Testing
  TEST_COVERAGE: 'test_coverage',
  TEST_QUALITY: 'test_quality',
  MISSING_TEST: 'missing_test',
  TEST_SMELL: 'test_smell',

  // Architecture
  CIRCULAR_DEPENDENCY: 'circular_dependency',
  ARCHITECTURAL_VIOLATION: 'architectural_violation',

  // General
  ERROR_HANDLING: 'error_handling',
  SECURITY: 'security',
  PERFORMANCE: 'performance'
} as const;
```

**Issue Hash Generation**:

```typescript
/**
 * Generates unique hash for issue deduplication
 * @param file File path
 * @param line Line number
 * @param category Issue category
 * @returns SHA-256 hash string
 */
function generateIssueHash(file: string, line: number, category: string): string {
  const composite = `${file}:${line}:${category}`;
  return sha256(composite).substring(0, 16);  // First 16 chars of hash
}

// Example usage
const issueId = generateIssueHash('Services/AuthService.cs', 42, 'naming_convention');
// Result: "a3f2c8e91b4d7f6a"
```

**Issue Examples**:

**Example 1: Style Issue (P1)**
```typescript
const styleIssue: Issue = {
  id: 'a3f2c8e91b4d7f6a',
  file: 'Services/AuthService.cs',
  line: 42,
  column: 12,
  severity: 'P1',
  category: IssueCategory.NAMING_CONVENTION,
  rule: 'csharp-naming-PascalCase',
  message: 'Variable "x" should use descriptive name following camelCase convention',
  suggestion: 'Rename to "userRequest" or similar descriptive name',
  confidence: 0.85,
  reviewer: 'code-style-reviewer',
  code_snippet: 'var x = GetUserRequest();',
  fix_example: 'var userRequest = GetUserRequest();'
};
```

**Example 2: Principle Violation (P0)**
```typescript
const principleIssue: Issue = {
  id: 'b7d4e2f8c9a1e3b5',
  file: 'Services/AuthService.cs',
  line: 85,
  severity: 'P0',
  category: IssueCategory.SOLID_DIP,
  rule: 'dependency-inversion',
  message: 'Class depends on concrete implementation instead of abstraction, violates Dependency Inversion Principle',
  suggestion: 'Inject IAuthRepository interface instead of concrete AuthRepository',
  confidence: 0.92,
  reviewer: 'code-principles-reviewer',
  code_snippet: 'private AuthRepository _repo = new AuthRepository();',
  fix_example: 'private readonly IAuthRepository _repo;\npublic AuthService(IAuthRepository repo) { _repo = repo; }'
};
```

**Example 3: Test Coverage Issue (P1)**
```typescript
const testIssue: Issue = {
  id: 'c8e5f1a9d2b7c4e6',
  file: 'Services/AuthService.cs',
  line: 120,
  severity: 'P1',
  category: IssueCategory.MISSING_TEST,
  rule: 'test-coverage-required',
  message: 'Method ProcessAuthenticationRequest has zero test coverage',
  suggestion: 'Add unit tests covering success case, failure cases, and edge cases',
  confidence: 0.88,
  reviewer: 'test-healer'
};
```

### Issue Deduplication ID Strategy

**Composite Key Design**:

The issue ID is generated from three components to enable both exact match and semantic deduplication:

```typescript
/**
 * Issue deduplication strategy
 *
 * EXACT MATCH: Uses (file, line, category) composite key
 * - Same file, same line, same category → DUPLICATE
 * - Hash ensures fast lookup in deduplication map
 *
 * SEMANTIC MATCH: Compares message similarity if exact match fails
 * - Levenshtein distance ≥80% → POTENTIAL DUPLICATE
 * - Requires additional context checks (file proximity, line proximity)
 */

// Exact match key generation
function generateExactMatchKey(issue: Issue): string {
  return generateIssueHash(issue.file, issue.line, issue.category);
}

// Semantic similarity key generation (for fallback)
function generateSemanticKey(issue: Issue): string {
  // Normalize message for comparison
  const normalized = issue.message
    .toLowerCase()
    .replace(/[^\w\s]/g, '')  // Remove punctuation
    .replace(/\s+/g, ' ')      // Normalize whitespace
    .trim();

  return sha256(normalized).substring(0, 16);
}
```

**Deduplication Example**:

```typescript
// Three reviewers report similar issues
const issue1: Issue = {
  id: generateIssueHash('AuthService.cs', 42, 'naming_convention'),
  file: 'AuthService.cs',
  line: 42,
  category: 'naming_convention',
  message: 'Variable "x" should be renamed',
  reviewer: 'code-style-reviewer',
  // ...
};

const issue2: Issue = {
  id: generateIssueHash('AuthService.cs', 42, 'naming_convention'),  // SAME ID
  file: 'AuthService.cs',
  line: 42,
  category: 'naming_convention',
  message: 'Variable "x" violates naming convention',
  reviewer: 'code-principles-reviewer',
  // ...
};

// Deduplication algorithm recognizes these as duplicates via exact match
// Result: Single merged issue with reviewers: ['code-style-reviewer', 'code-principles-reviewer']
```

### Interface Validation

**Validation Rules** for ReviewResult and Issue:

```typescript
/**
 * Validates ReviewResult structure
 * @throws Error if validation fails
 */
function validateReviewResult(result: ReviewResult): void {
  // Required fields
  if (!result.reviewer_name || typeof result.reviewer_name !== 'string') {
    throw new Error('Invalid ReviewResult: reviewer_name required');
  }

  if (typeof result.execution_time_ms !== 'number' || result.execution_time_ms < 0) {
    throw new Error('Invalid ReviewResult: execution_time_ms must be non-negative number');
  }

  if (!['success', 'timeout', 'error', 'partial'].includes(result.status)) {
    throw new Error(`Invalid ReviewResult: status must be success|timeout|error|partial, got ${result.status}`);
  }

  if (!Array.isArray(result.issues)) {
    throw new Error('Invalid ReviewResult: issues must be array');
  }

  if (typeof result.confidence !== 'number' || result.confidence < 0 || result.confidence > 1) {
    throw new Error('Invalid ReviewResult: confidence must be between 0 and 1');
  }

  // Validate each issue
  result.issues.forEach((issue, index) => {
    try {
      validateIssue(issue);
    } catch (error) {
      throw new Error(`Invalid issue at index ${index}: ${error.message}`);
    }
  });
}

/**
 * Validates Issue structure
 * @throws Error if validation fails
 */
function validateIssue(issue: Issue): void {
  if (!issue.id || typeof issue.id !== 'string') {
    throw new Error('Invalid Issue: id required');
  }

  if (!issue.file || typeof issue.file !== 'string') {
    throw new Error('Invalid Issue: file required');
  }

  if (typeof issue.line !== 'number' || issue.line < 1) {
    throw new Error('Invalid Issue: line must be positive number');
  }

  if (!['P0', 'P1', 'P2'].includes(issue.severity)) {
    throw new Error(`Invalid Issue: severity must be P0|P1|P2, got ${issue.severity}`);
  }

  if (!issue.category || typeof issue.category !== 'string') {
    throw new Error('Invalid Issue: category required');
  }

  if (!issue.message || typeof issue.message !== 'string') {
    throw new Error('Invalid Issue: message required');
  }

  if (typeof issue.confidence !== 'number' || issue.confidence < 0 || issue.confidence > 1) {
    throw new Error('Invalid Issue: confidence must be between 0 and 1');
  }

  if (!issue.reviewer || typeof issue.reviewer !== 'string') {
    throw new Error('Invalid Issue: reviewer required');
  }
}
```

---

## Result Caching System

**Purpose**: Cache reviewer results to avoid re-running expensive reviews for unchanged files

**Design Philosophy**:
- **Fast lookup**: O(1) access via Map-based storage
- **Automatic expiry**: 15-minute TTL prevents stale results
- **File-based keys**: Hash file contents for cache invalidation on changes
- **Memory efficient**: In-memory only (no persistence overhead)

### ResultCache Class

**Core Caching Implementation**:

```typescript
/**
 * In-memory cache for reviewer results with automatic TTL expiry
 * @class ResultCache
 */
class ResultCache {
  // Private storage
  private cache: Map<string, CachedResult>;
  private readonly TTL: number;

  /**
   * Constructor
   * @param ttl Time-to-live in milliseconds (default: 900000 = 15 minutes)
   */
  constructor(ttl: number = 900000) {
    this.cache = new Map<string, CachedResult>();
    this.TTL = ttl;

    // Start automatic cleanup interval (every 5 minutes)
    this.startCleanupInterval();
  }

  /**
   * Stores reviewer result in cache
   * @param key Cache key (generated from files + reviewer)
   * @param result ReviewResult to cache
   */
  store(key: string, result: ReviewResult): void {
    const now = Date.now();

    this.cache.set(key, {
      result: result,
      timestamp: now,
      expires: now + this.TTL
    });

    console.log(`[Cache] Stored result for key: ${key} (expires in ${this.TTL / 1000}s)`);
  }

  /**
   * Retrieves reviewer result from cache
   * @param key Cache key
   * @returns ReviewResult if cached and not expired, null otherwise
   */
  retrieve(key: string): ReviewResult | null {
    const cached = this.cache.get(key);

    if (!cached) {
      console.log(`[Cache] MISS for key: ${key}`);
      return null;
    }

    const now = Date.now();

    // Check if expired
    if (now > cached.expires) {
      console.log(`[Cache] EXPIRED for key: ${key} (age: ${(now - cached.timestamp) / 1000}s)`);
      this.cache.delete(key);
      return null;
    }

    console.log(`[Cache] HIT for key: ${key} (age: ${(now - cached.timestamp) / 1000}s)`);

    // Update cache hit flag in metadata
    const result = { ...cached.result };
    result.metadata = { ...result.metadata, cache_hit: true };

    return result;
  }

  /**
   * Generates cache key from file list and reviewer ID
   * @param files Array of file paths
   * @param reviewer Reviewer ID
   * @returns Cache key string
   */
  getCacheKey(files: string[], reviewer: string): string {
    // Sort files for consistent key generation
    const sortedFiles = [...files].sort();

    // Generate file hash (combined hash of all file contents)
    const fileHash = this.hashFiles(sortedFiles);

    // Combine with reviewer ID
    return `${reviewer}:${fileHash}`;
  }

  /**
   * Hashes file list for cache key generation
   * @param files Sorted array of file paths
   * @returns SHA-256 hash of file paths and modification times
   */
  private hashFiles(files: string[]): string {
    // In real implementation, hash file contents + modification time
    // For specification, we use file paths + timestamps
    const composite = files.map(file => {
      // Include file path and last modified time
      const mtime = getFileModificationTime(file);
      return `${file}:${mtime}`;
    }).join('|');

    return sha256(composite).substring(0, 16);
  }

  /**
   * Invalidates cache entries for specific files
   * @param files Array of file paths that changed
   */
  invalidate(files: string[]): void {
    let invalidated = 0;

    // Find and delete cache entries containing these files
    for (const [key, cached] of this.cache.entries()) {
      // Check if key contains any of the changed files
      // (Keys are format: "reviewer:file_hash")
      const fileHash = key.split(':')[1];

      // Re-generate hash for changed files
      const newFileHash = this.hashFiles(files);

      if (fileHash === newFileHash) {
        this.cache.delete(key);
        invalidated++;
      }
    }

    if (invalidated > 0) {
      console.log(`[Cache] Invalidated ${invalidated} entries due to file changes`);
    }
  }

  /**
   * Clears all cache entries
   */
  clear(): void {
    const size = this.cache.size;
    this.cache.clear();
    console.log(`[Cache] Cleared ${size} entries`);
  }

  /**
   * Gets current cache statistics
   * @returns Cache statistics object
   */
  getStats(): CacheStats {
    const now = Date.now();
    let expired = 0;
    let valid = 0;

    for (const [key, cached] of this.cache.entries()) {
      if (now > cached.expires) {
        expired++;
      } else {
        valid++;
      }
    }

    return {
      total_entries: this.cache.size,
      valid_entries: valid,
      expired_entries: expired,
      ttl_ms: this.TTL,
      memory_estimate_kb: this.estimateMemoryUsage()
    };
  }

  /**
   * Estimates memory usage of cache
   * @returns Estimated memory in KB
   */
  private estimateMemoryUsage(): number {
    // Rough estimate: 10KB per cached result
    return this.cache.size * 10;
  }

  /**
   * Starts automatic cleanup interval
   * Removes expired entries every 5 minutes
   */
  private startCleanupInterval(): void {
    setInterval(() => {
      this.cleanup();
    }, 300000);  // 5 minutes
  }

  /**
   * Removes expired entries from cache
   */
  private cleanup(): void {
    const now = Date.now();
    let removed = 0;

    for (const [key, cached] of this.cache.entries()) {
      if (now > cached.expires) {
        this.cache.delete(key);
        removed++;
      }
    }

    if (removed > 0) {
      console.log(`[Cache] Cleanup removed ${removed} expired entries`);
    }
  }
}

/**
 * Cached result with expiration metadata
 */
interface CachedResult {
  result: ReviewResult;
  timestamp: number;  // When cached (milliseconds since epoch)
  expires: number;    // Expiration time (milliseconds since epoch)
}

/**
 * Cache statistics structure
 */
interface CacheStats {
  total_entries: number;
  valid_entries: number;
  expired_entries: number;
  ttl_ms: number;
  memory_estimate_kb: number;
}

/**
 * Helper function to get file modification time
 * (Placeholder - implementation depends on environment)
 */
function getFileModificationTime(file: string): number {
  // In Node.js: fs.statSync(file).mtime.getTime()
  // In browser: Not applicable (no file system access)
  // For specification: return mock timestamp
  return Date.now();
}
```

### Cache Integration with Parallel Execution

**Complete Workflow with Caching**:

```typescript
// Global cache instance
const reviewerCache = new ResultCache(900000);  // 15-minute TTL

/**
 * Launches parallel reviews with cache checking
 * @param files Files to review
 * @param reviewers Reviewer IDs to invoke
 * @returns Promise resolving to reviewer results
 */
async function launchParallelReviewsWithCache(
  files: string[],
  reviewers: string[]
): Promise<ReviewResult[]> {

  const results: ReviewResult[] = [];
  const reviewersToRun: string[] = [];

  // STEP 1: Check cache for each reviewer
  for (const reviewer of reviewers) {
    const cacheKey = reviewerCache.getCacheKey(files, reviewer);
    const cachedResult = reviewerCache.retrieve(cacheKey);

    if (cachedResult) {
      // Cache hit - use cached result
      console.log(`✅ Cache HIT for ${reviewer} (saved ~4-5 minutes)`);
      results.push(cachedResult);
    } else {
      // Cache miss - need to run reviewer
      console.log(`❌ Cache MISS for ${reviewer} (will execute)`);
      reviewersToRun.push(reviewer);
    }
  }

  // STEP 2: Run reviewers that had cache misses
  if (reviewersToRun.length > 0) {
    console.log(`\nRunning ${reviewersToRun.length}/${reviewers.length} reviewers (${reviewers.length - reviewersToRun.length} cached)\n`);

    const newResults = await launchParallelReviews(files, reviewersToRun);

    // STEP 3: Store new results in cache
    for (const result of newResults) {
      const cacheKey = reviewerCache.getCacheKey(files, result.reviewer_name);
      reviewerCache.store(cacheKey, result);
    }

    results.push(...newResults);
  } else {
    console.log(`\n✅ All ${reviewers.length} reviewers served from cache (0 executions needed)\n`);
  }

  return results;
}
```

### Cache Performance Benefits

**Cache Hit Scenarios**:

**Scenario 1: Re-review After Small Change**
```typescript
// Initial review (all cache misses)
const files = ['AuthService.cs', 'UserService.cs', 'TokenService.cs'];
const reviewers = ['code-style-reviewer', 'code-principles-reviewer', 'test-healer'];

// Time: 5 minutes (parallel execution)
await launchParallelReviewsWithCache(files, reviewers);

// User fixes one issue in AuthService.cs
// File hash changes for AuthService.cs

// Re-review (cache invalidation for affected reviewers)
const results = await launchParallelReviewsWithCache(files, reviewers);

// Cache behavior:
// - AuthService.cs changed → cache keys invalidated
// - UserService.cs, TokenService.cs unchanged → cache keys still valid
// - Result: Partial cache hit (some reviewers served from cache)
// - Time: 2-3 minutes (only re-review changed files)
```

**Scenario 2: Multiple Reviews of Same Files**
```typescript
// First review (all cache misses)
await launchParallelReviewsWithCache(files, reviewers);  // 5 minutes

// Second review within 15 minutes (all cache hits)
await launchParallelReviewsWithCache(files, reviewers);  // <1 second

// Cache savings: 99.7% reduction (5 minutes → <1 second)
```

**Scenario 3: Different Reviewers, Same Files**
```typescript
// Initial review with 2 reviewers
await launchParallelReviewsWithCache(files, ['code-style-reviewer', 'code-principles-reviewer']);

// Later review with 3 reviewers (add test-healer)
await launchParallelReviewsWithCache(files, ['code-style-reviewer', 'code-principles-reviewer', 'test-healer']);

// Cache behavior:
// - code-style-reviewer: CACHE HIT
// - code-principles-reviewer: CACHE HIT
// - test-healer: CACHE MISS (first time running)
// - Time: ~2 minutes (only test-healer runs)
```

### Cache Invalidation Strategies

**File Change Detection**:

```typescript
/**
 * Watches files for changes and invalidates cache
 * @param files Files to watch
 */
function watchFilesForChanges(files: string[]): void {
  // Use file system watcher (e.g., fs.watch in Node.js)
  for (const file of files) {
    watchFile(file, (event) => {
      if (event === 'change') {
        console.log(`[Cache] File changed: ${file}, invalidating cache`);
        reviewerCache.invalidate([file]);
      }
    });
  }
}

/**
 * Manual cache invalidation for specific reviewers
 * @param files Files that changed
 * @param reviewers Reviewers to invalidate (optional, default: all)
 */
function invalidateCacheForFiles(files: string[], reviewers?: string[]): void {
  if (reviewers) {
    // Invalidate specific reviewers
    for (const reviewer of reviewers) {
      const cacheKey = reviewerCache.getCacheKey(files, reviewer);
      reviewerCache.cache.delete(cacheKey);
    }
  } else {
    // Invalidate all reviewers for these files
    reviewerCache.invalidate(files);
  }
}
```

**Time-Based Expiration**:

```typescript
// Cache entries automatically expire after 15 minutes (TTL)
// Rationale:
// - Files may change without file system events (git operations, external edits)
// - Code rules may be updated (new rules in .cursor/rules/*.mdc)
// - 15 minutes balances freshness vs performance

// Cleanup runs every 5 minutes to remove expired entries
// Memory usage: ~10KB per cached result × ~10 reviewers × 3 results = ~300KB
```

### Cache Statistics and Monitoring

**Usage Example**:

```typescript
// Get cache statistics
const stats = reviewerCache.getStats();

console.log(`
Cache Statistics:
  Total entries: ${stats.total_entries}
  Valid entries: ${stats.valid_entries}
  Expired entries: ${stats.expired_entries}
  TTL: ${stats.ttl_ms / 1000} seconds
  Memory usage: ~${stats.memory_estimate_kb} KB
`);

// Example output:
// Cache Statistics:
//   Total entries: 9
//   Valid entries: 9
//   Expired entries: 0
//   TTL: 900 seconds
//   Memory usage: ~90 KB
```

**Cache Hit Rate Tracking**:

```typescript
/**
 * Tracks cache hit rate over time
 */
class CacheMetrics {
  private hits = 0;
  private misses = 0;

  recordHit(): void {
    this.hits++;
  }

  recordMiss(): void {
    this.misses++;
  }

  getHitRate(): number {
    const total = this.hits + this.misses;
    return total > 0 ? this.hits / total : 0;
  }

  reset(): void {
    this.hits = 0;
    this.misses = 0;
  }

  getStats(): { hits: number; misses: number; hitRate: number } {
    return {
      hits: this.hits,
      misses: this.misses,
      hitRate: this.getHitRate()
    };
  }
}

// Global metrics tracker
const cacheMetrics = new CacheMetrics();

// Update retrieve method to track metrics
retrieve(key: string): ReviewResult | null {
  const cached = this.cache.get(key);

  if (!cached || Date.now() > cached.expires) {
    cacheMetrics.recordMiss();
    return null;
  }

  cacheMetrics.recordHit();
  return cached.result;
}

// Display metrics
const metrics = cacheMetrics.getStats();
console.log(`Cache Hit Rate: ${(metrics.hitRate * 100).toFixed(1)}% (${metrics.hits} hits, ${metrics.misses} misses)`);
// Output: "Cache Hit Rate: 75.0% (6 hits, 2 misses)"
```

### Performance Impact

**Expected Cache Performance**:

| Scenario | Without Cache | With Cache | Speedup |
|----------|--------------|------------|---------|
| All hits (no file changes) | 5 minutes | <1 second | 300x |
| Partial hit (1/3 files changed) | 5 minutes | ~2 minutes | 2.5x |
| All misses (all files changed) | 5 minutes | 5 minutes | 1x (no benefit) |

**Memory Usage**:

| Cache Size | Entries | Memory | Notes |
|------------|---------|--------|-------|
| Small | 10 entries | ~100 KB | Typical for single-feature reviews |
| Medium | 50 entries | ~500 KB | Multi-feature concurrent reviews |
| Large | 100 entries | ~1 MB | Large-scale refactoring reviews |

**Cleanup Overhead**:

- Cleanup runs every 5 minutes
- Time complexity: O(n) where n = cache size
- Typical cleanup time: <10ms for 100 entries
- Memory freed: ~10KB per expired entry

---

## Algorithm Components

### 1. Issue Deduplication

**Purpose**: Eliminate duplicate issues reported by multiple reviewers

**Algorithm**:

```
INPUT: List of issues from all reviewers
  Issue = {
    file_path: string,
    line_number: int,
    issue_type: string,
    description: string,
    priority: P0|P1|P2,
    confidence: float (0.0-1.0),
    reviewer_id: string
  }

OUTPUT: Deduplicated list of issues with aggregated metadata

STEP 1: Exact Match Deduplication (Fast Path)
  - Group issues by composite key: (file_path, line_number, issue_type)
  - Issues with identical keys are exact duplicates
  - Time complexity: O(n) using HashMap
  - Expected reduction: 40-50% of duplicates

STEP 2: Semantic Similarity Deduplication (Slow Path)
  - For remaining issues, compute pairwise similarity
  - Similarity metric: Levenshtein distance on description field
  - Threshold: ≥80% similarity → consider duplicate
  - Time complexity: O(n²) with early exit optimization
  - Expected reduction: 10-20% additional duplicates

STEP 3: Duplicate Grouping
  - Merge duplicate issues into single entry
  - Preserve all reviewer IDs (for confidence calculation)
  - Preserve highest priority (for priority aggregation)
  - Merge descriptions (concatenate unique descriptions)
```

**Exact Match Implementation**:

```python
def exact_match_deduplicate(issues):
    exact_match_map = {}

    for issue in issues:
        key = (issue.file_path, issue.line_number, issue.issue_type)

        if key in exact_match_map:
            # Duplicate found - merge metadata
            exact_match_map[key].reviewers.append(issue.reviewer_id)
            exact_match_map[key].priorities.append(issue.priority)
            exact_match_map[key].confidences.append(issue.confidence)
            if issue.description not in exact_match_map[key].descriptions:
                exact_match_map[key].descriptions.append(issue.description)
        else:
            # First occurrence - create new entry
            exact_match_map[key] = {
                'file_path': issue.file_path,
                'line_number': issue.line_number,
                'issue_type': issue.issue_type,
                'reviewers': [issue.reviewer_id],
                'priorities': [issue.priority],
                'confidences': [issue.confidence],
                'descriptions': [issue.description]
            }

    return exact_match_map.values()
```

**Semantic Similarity Implementation**:

```python
def semantic_similarity_deduplicate(issues, threshold=0.80):
    deduplicated = []

    for issue in issues:
        is_duplicate = False

        for existing in deduplicated:
            similarity = levenshtein_similarity(issue.description, existing.description)

            if similarity >= threshold:
                # Semantic duplicate found - merge
                existing.reviewers.append(issue.reviewer_id)
                existing.priorities.append(issue.priority)
                existing.confidences.append(issue.confidence)
                if issue.description not in existing.descriptions:
                    existing.descriptions.append(issue.description)
                is_duplicate = True
                break

        if not is_duplicate:
            deduplicated.append(issue)

    return deduplicated

def levenshtein_similarity(str1, str2):
    distance = levenshtein_distance(str1, str2)
    max_length = max(len(str1), len(str2))
    return 1.0 - (distance / max_length)
```

**Performance Optimization**:

- **Hash-based indexing**: O(1) lookup for exact matches
- **Early exit**: Stop Levenshtein calculation if distance exceeds threshold
- **Batch processing**: Process issues in batches of 100 to reduce memory overhead
- **Caching**: Cache Levenshtein results for repeated comparisons

**Expected Performance**:
- Exact match: <50ms for 500 issues
- Semantic similarity: <1.5s for 500 issues (with optimizations)
- Total deduplication: <2s for typical 3-reviewer report (~150 issues)

---

### 2. Priority Aggregation

**Purpose**: Determine final priority for deduplicated issues based on reviewer consensus

**Algorithm**:

```
INPUT: Deduplicated issue with multiple priority ratings
  Issue = {
    priorities: [P0, P1, P2, ...],  # From all reviewers
    reviewers: [reviewer_id1, reviewer_id2, ...]
  }

OUTPUT: Single aggregated priority (P0, P1, or P2)

LOGIC:
  IF ANY(priorities == P0):
    RETURN P0  # Critical issue - ANY reviewer marking P0 escalates

  ELSE IF COUNT(priorities == P1) >= LEN(reviewers) / 2:
    RETURN P1  # Majority consensus - ≥50% reviewers agree on P1

  ELSE:
    RETURN P2  # Default - informational/low-priority
```

**Priority Rules**:

| Condition | Aggregated Priority | Rationale |
|-----------|---------------------|-----------|
| ANY reviewer marks P0 | **P0** | Critical issues cannot be ignored - single reviewer veto |
| ≥50% reviewers mark P1 | **P1** | Majority consensus indicates importance |
| Otherwise | **P2** | Default to low priority (informational) |

**Implementation**:

```python
def aggregate_priority(priorities, reviewers):
    # Rule 1: ANY P0 → escalate to P0
    if any(p == 'P0' for p in priorities):
        return 'P0'

    # Rule 2: Majority P1 → aggregate to P1
    p1_count = sum(1 for p in priorities if p == 'P1')
    if p1_count >= len(reviewers) / 2:
        return 'P1'

    # Rule 3: Default to P2
    return 'P2'
```

**Examples**:

| Reviewers | Priorities | Aggregated | Explanation |
|-----------|------------|------------|-------------|
| 3 reviewers | [P0, P1, P2] | **P0** | ANY P0 → escalate to P0 |
| 3 reviewers | [P1, P1, P2] | **P1** | 2/3 (66%) agree on P1 → P1 |
| 3 reviewers | [P1, P2, P2] | **P2** | Only 1/3 (33%) mark P1 → default P2 |
| 2 reviewers | [P1, P2] | **P1** | 1/2 (50%) mark P1 → P1 (tie-breaker) |
| 1 reviewer | [P2] | **P2** | Single reviewer → use their priority |

**Edge Cases**:

- **Single reviewer**: Use their priority directly (no aggregation needed)
- **Tie-breaker (50% split)**: Round up to higher priority (P1 > P2)
- **Empty priorities**: Default to P2, log warning
- **Invalid priorities**: Reject issue, log error

---

### 3. Confidence Calculation

**Purpose**: Calculate weighted confidence score for deduplicated issues

**Algorithm**:

```
INPUT: Deduplicated issue with multiple confidence scores
  Issue = {
    confidences: [0.85, 0.92, 0.78, ...],  # From all reviewers
    reviewers: [reviewer_id1, reviewer_id2, ...]
  }

OUTPUT: Single weighted confidence score (0.0-1.0)

FORMULA:
  weighted_confidence = Σ(reviewer_confidence × reviewer_weight) / Σ(reviewer_weight)

WEIGHTS:
  - test-healer: 1.2  (expertise in test analysis)
  - code-style-reviewer: 1.0  (baseline weight)
  - code-principles-reviewer: 1.0  (baseline weight)
  - default: 1.0  (unknown reviewers)
```

**Implementation**:

```python
REVIEWER_WEIGHTS = {
    'test-healer': 1.2,
    'code-style-reviewer': 1.0,
    'code-principles-reviewer': 1.0,
    'architecture-documenter': 1.0,
    'default': 1.0
}

def calculate_weighted_confidence(confidences, reviewers):
    if len(confidences) == 0:
        return 0.0  # No confidence data

    weighted_sum = 0.0
    weight_sum = 0.0

    for confidence, reviewer_id in zip(confidences, reviewers):
        weight = REVIEWER_WEIGHTS.get(reviewer_id, REVIEWER_WEIGHTS['default'])
        weighted_sum += confidence * weight
        weight_sum += weight

    return weighted_sum / weight_sum
```

**Examples**:

**Example 1: Single reviewer**
```
Input:
  confidences: [0.85]
  reviewers: ['code-style-reviewer']

Calculation:
  weighted_sum = 0.85 × 1.0 = 0.85
  weight_sum = 1.0
  confidence = 0.85 / 1.0 = 0.85

Output: 0.85
```

**Example 2: Multiple reviewers, equal weights**
```
Input:
  confidences: [0.85, 0.92, 0.78]
  reviewers: ['code-style-reviewer', 'code-principles-reviewer', 'architecture-documenter']

Calculation:
  weighted_sum = (0.85×1.0) + (0.92×1.0) + (0.78×1.0) = 2.55
  weight_sum = 1.0 + 1.0 + 1.0 = 3.0
  confidence = 2.55 / 3.0 = 0.85

Output: 0.85
```

**Example 3: Multiple reviewers, test-healer weighted higher**
```
Input:
  confidences: [0.85, 0.95]
  reviewers: ['code-style-reviewer', 'test-healer']

Calculation:
  weighted_sum = (0.85×1.0) + (0.95×1.2) = 0.85 + 1.14 = 1.99
  weight_sum = 1.0 + 1.2 = 2.2
  confidence = 1.99 / 2.2 = 0.905

Output: 0.91 (rounded)
```

**Confidence Interpretation**:

| Confidence Range | Interpretation | Action |
|------------------|----------------|--------|
| 0.90-1.00 | Very High | Include in report, high priority |
| 0.80-0.89 | High | Include in report |
| 0.60-0.79 | Medium | Include with caveat |
| 0.40-0.59 | Low | Consider filtering (optional) |
| 0.00-0.39 | Very Low | Filter from final report |

---

### 4. Recommendation Synthesis

**Purpose**: Group and rank reviewer recommendations by theme and frequency

**Algorithm**:

```
INPUT: List of recommendations from all reviewers
  Recommendation = {
    text: string,
    reviewer_id: string,
    confidence: float (0.0-1.0)
  }

OUTPUT: Synthesized, ranked recommendations grouped by theme

STEP 1: Keyword Extraction
  - Extract keywords from recommendation text
  - Patterns: "refactor", "extract method", "add tests", "improve naming"
  - Use NLP or regex-based extraction

STEP 2: Theme Grouping
  - Group recommendations with similar keywords into themes
  - Themes: "Refactoring", "Testing", "Naming", "Architecture", etc.

STEP 3: Frequency Counting
  - Count how many reviewers recommend each theme
  - Frequency = number of reviewers suggesting theme

STEP 4: Confidence Filtering
  - Filter out recommendations with confidence <60%
  - Low-confidence recommendations are not actionable

STEP 5: Ranking
  - Rank themes by frequency (descending)
  - Tie-breaker: Average confidence (higher confidence wins)
  - Return top 5-10 themes
```

**Keyword Extraction Implementation**:

```python
KEYWORD_PATTERNS = {
    'refactoring': r'\b(refactor|extract|simplify|reduce complexity)\b',
    'testing': r'\b(test|coverage|assert|mock)\b',
    'naming': r'\b(rename|naming|identifier|variable name)\b',
    'architecture': r'\b(architecture|design|pattern|structure)\b',
    'performance': r'\b(performance|optimize|cache|efficiency)\b',
    'security': r'\b(security|authentication|authorization|validation)\b',
    'documentation': r'\b(document|comment|xml doc|readme)\b',
    'error_handling': r'\b(error|exception|try-catch|validation)\b'
}

def extract_keywords(recommendation_text):
    keywords = []
    for theme, pattern in KEYWORD_PATTERNS.items():
        if re.search(pattern, recommendation_text, re.IGNORECASE):
            keywords.append(theme)
    return keywords
```

**Theme Grouping Implementation**:

```python
def group_by_theme(recommendations):
    themes = {}

    for rec in recommendations:
        # Filter low-confidence recommendations
        if rec.confidence < 0.60:
            continue

        keywords = extract_keywords(rec.text)

        for keyword in keywords:
            if keyword not in themes:
                themes[keyword] = {
                    'recommendations': [],
                    'reviewers': set(),
                    'confidences': []
                }

            themes[keyword]['recommendations'].append(rec.text)
            themes[keyword]['reviewers'].add(rec.reviewer_id)
            themes[keyword]['confidences'].append(rec.confidence)

    return themes
```

**Ranking Implementation**:

```python
def rank_themes(themes):
    ranked = []

    for theme_name, theme_data in themes.items():
        frequency = len(theme_data['reviewers'])
        avg_confidence = sum(theme_data['confidences']) / len(theme_data['confidences'])

        ranked.append({
            'theme': theme_name,
            'frequency': frequency,
            'avg_confidence': avg_confidence,
            'recommendations': theme_data['recommendations']
        })

    # Sort by frequency (descending), then by confidence (descending)
    ranked.sort(key=lambda x: (x['frequency'], x['avg_confidence']), reverse=True)

    # Return top 5-10 themes
    return ranked[:10]
```

**Output Format**:

```markdown
## Synthesized Recommendations

### Top Themes (by frequency)

1. **Refactoring** (3 reviewers, 87% confidence)
   - Extract method for complex conditional logic (code-principles-reviewer)
   - Reduce cyclomatic complexity in ProcessRequest method (code-style-reviewer)
   - Simplify nested if-statements (test-healer)

2. **Testing** (3 reviewers, 82% confidence)
   - Add unit tests for edge cases (test-healer)
   - Increase code coverage to ≥80% (code-principles-reviewer)
   - Mock external dependencies in tests (test-healer)

3. **Naming** (2 reviewers, 75% confidence)
   - Rename variable 'x' to 'userRequest' (code-style-reviewer)
   - Use descriptive method names instead of abbreviations (code-principles-reviewer)

4. **Error Handling** (2 reviewers, 71% confidence)
   - Add try-catch for database exceptions (code-principles-reviewer)
   - Validate input parameters before processing (test-healer)

5. **Documentation** (1 reviewer, 68% confidence)
   - Add XML documentation to public methods (code-style-reviewer)
```

**Filtering Logic**:

- **Confidence threshold**: 60% (configurable)
- **Minimum frequency**: 1 reviewer (no minimum, all themes included)
- **Maximum themes**: 10 (configurable)

---

## Performance Considerations

### Time Complexity

| Step | Algorithm | Complexity | Typical Time |
|------|-----------|------------|--------------|
| Exact Match Deduplication | HashMap grouping | O(n) | <50ms |
| Semantic Similarity | Levenshtein pairwise | O(n²) | <1.5s |
| Priority Aggregation | Linear scan | O(n) | <10ms |
| Confidence Calculation | Linear scan | O(n) | <10ms |
| Recommendation Synthesis | Keyword extraction + grouping | O(m×k) | <100ms |
| **TOTAL** | - | **O(n²)** | **<2s** |

**Where**:
- n = number of issues (~150 for 3 reviewers)
- m = number of recommendations (~30 for 3 reviewers)
- k = number of keyword patterns (~8)

### Space Complexity

| Data Structure | Size | Memory |
|----------------|------|--------|
| Issue HashMap (exact match) | O(n) | ~50KB |
| Levenshtein cache | O(n²) | ~2MB (with cache) |
| Theme grouping | O(m) | ~10KB |
| **TOTAL** | **O(n²)** | **~2MB** |

### Optimization Strategies

**1. Early Exit for Levenshtein**:
```python
def levenshtein_distance_optimized(str1, str2, max_distance):
    if abs(len(str1) - len(str2)) > max_distance:
        return max_distance + 1  # Early exit - strings too different

    # Standard Levenshtein calculation
    # ...
```

**2. Batch Processing**:
```python
def deduplicate_in_batches(issues, batch_size=100):
    batches = [issues[i:i+batch_size] for i in range(0, len(issues), batch_size)]

    deduplicated = []
    for batch in batches:
        deduplicated.extend(semantic_similarity_deduplicate(batch))

    return deduplicated
```

**3. Caching Similarity Scores**:
```python
similarity_cache = {}

def cached_similarity(str1, str2):
    key = (min(str1, str2), max(str1, str2))  # Order-independent key

    if key not in similarity_cache:
        similarity_cache[key] = levenshtein_similarity(str1, str2)

    return similarity_cache[key]
```

**Expected Deduplication Rates**:
- Exact match: 40-50% reduction
- Semantic similarity: 10-20% additional reduction
- Total: 60-70% reduction in issue count

**Example**:
```
Input: 150 issues (3 reviewers × 50 issues each)
After exact match: 90 issues (40% reduction)
After semantic similarity: 50 issues (additional 44% reduction)
Total reduction: 67%
```

---

## Edge Cases

### Edge Case 1: Single Reviewer

**Scenario**: Only one reviewer provided a report (no aggregation needed)

**Handling**:
```python
def consolidate(reviews):
    if len(reviews) == 1:
        # No aggregation needed - return report as-is
        return {
            'issues': reviews[0].issues,
            'recommendations': reviews[0].recommendations,
            'note': 'Single reviewer - no aggregation performed'
        }

    # Standard consolidation for multiple reviewers
    # ...
```

**Output**:
```markdown
## Consolidated Report

**Note**: Only one reviewer (code-style-reviewer) provided a report. No aggregation performed.

### Issues (12 total)
[All issues from code-style-reviewer]

### Recommendations (5 total)
[All recommendations from code-style-reviewer]
```

### Edge Case 2: Conflicting Priorities

**Scenario**: One reviewer marks issue as P0 (critical), another as P2 (informational)

**Handling**:
```python
def aggregate_priority_with_note(priorities, reviewers):
    aggregated = aggregate_priority(priorities, reviewers)

    # Detect conflicts (P0 from one reviewer, P2 from another)
    if 'P0' in priorities and 'P2' in priorities:
        note = f"Priority conflict detected: {priorities}. Escalated to {aggregated} per ANY P0 rule."
        return aggregated, note

    return aggregated, None
```

**Output**:
```markdown
### Issue: Missing null check in ProcessRequest method

**Priority**: P0 (Critical)
**Note**: Priority conflict detected: [P0, P2, P2]. Escalated to P0 per ANY P0 rule.

**Reviewers**: code-principles-reviewer (P0), code-style-reviewer (P2), test-healer (P2)
```

**Rationale**: P0 always wins - critical issues cannot be ignored, even if other reviewers disagree.

### Edge Case 3: Empty Reports

**Scenario**: One or more reviewers returned empty reports (no issues found)

**Handling**:
```python
def consolidate(reviews):
    non_empty_reviews = [r for r in reviews if len(r.issues) > 0]

    if len(non_empty_reviews) == 0:
        # All reviewers found no issues
        return {
            'issues': [],
            'recommendations': [],
            'note': 'All reviewers found no issues - code quality excellent!'
        }

    # Consolidate non-empty reviews only
    # ...
```

**Output**:
```markdown
## Consolidated Report

**Reviews Received**: 3
- code-style-reviewer: 0 issues
- code-principles-reviewer: 12 issues
- test-healer: 8 issues

### Consolidated Issues (15 total after deduplication)
[Issues from code-principles-reviewer and test-healer]
```

### Edge Case 4: Identical Issues from All Reviewers

**Scenario**: All reviewers report the exact same issue (100% consensus)

**Handling**:
```python
def consolidate_with_consensus_tracking(issues):
    deduplicated = exact_match_deduplicate(issues)

    for issue in deduplicated:
        if len(issue.reviewers) == len(all_reviewers):
            issue.consensus = True  # 100% consensus
            issue.confidence = 1.0  # Maximum confidence
```

**Output**:
```markdown
### Issue: Missing null check in ProcessRequest method

**Priority**: P0 (Critical)
**Confidence**: 1.00 (100% consensus)
**Reviewers**: code-style-reviewer, code-principles-reviewer, test-healer

**Note**: All reviewers independently identified this issue - highest confidence.
```

**Rationale**: 100% consensus = maximum confidence, highest priority escalation.

### Edge Case 5: Semantic Similarity False Positives

**Scenario**: Two issues have similar descriptions but are actually different issues

**Example**:
```
Issue A: "Missing null check on userRequest parameter"
Issue B: "Missing null check on requestContext parameter"

Levenshtein similarity: 87% (above 80% threshold)
```

**Handling**:

```python
def semantic_similarity_with_context(issue1, issue2, threshold=0.80):
    # Check description similarity
    desc_similarity = levenshtein_similarity(issue1.description, issue2.description)

    if desc_similarity < threshold:
        return False  # Not similar

    # Additional context checks to avoid false positives
    if issue1.file_path != issue2.file_path:
        return False  # Different files - not duplicates

    if abs(issue1.line_number - issue2.line_number) > 10:
        return False  # Lines too far apart - likely different issues

    return True  # Similar and in same context
```

**Mitigation**:
- Add context checks: file path, line number proximity
- Lower similarity threshold to 75% (configurable)
- Manual review for borderline cases (80-85% similarity)

---

## Examples

### Example 1: Exact Match Deduplication

**Input (3 reviewers)**:
```json
[
  {
    "reviewer": "code-style-reviewer",
    "file": "Services/AuthService.cs",
    "line": 42,
    "issue_type": "naming_convention",
    "description": "Variable 'x' should be renamed to descriptive name",
    "priority": "P1",
    "confidence": 0.85
  },
  {
    "reviewer": "code-principles-reviewer",
    "file": "Services/AuthService.cs",
    "line": 42,
    "issue_type": "naming_convention",
    "description": "Variable 'x' violates naming convention",
    "priority": "P2",
    "confidence": 0.78
  },
  {
    "reviewer": "test-healer",
    "file": "Services/AuthService.cs",
    "line": 42,
    "issue_type": "naming_convention",
    "description": "Rename variable 'x' to 'userRequest'",
    "priority": "P1",
    "confidence": 0.92
  }
]
```

**Processing**:
```
1. Exact Match Grouping:
   Key: ("Services/AuthService.cs", 42, "naming_convention")
   Matches: All 3 issues

2. Priority Aggregation:
   Priorities: [P1, P2, P1]
   Count(P1): 2/3 (66%)
   Result: P1 (majority consensus)

3. Confidence Calculation:
   Weighted sum: (0.85×1.0) + (0.78×1.0) + (0.92×1.2) = 2.734
   Weight sum: 1.0 + 1.0 + 1.2 = 3.2
   Confidence: 2.734 / 3.2 = 0.854
```

**Output**:
```json
{
  "file": "Services/AuthService.cs",
  "line": 42,
  "issue_type": "naming_convention",
  "description": "Variable 'x' should be renamed to descriptive name (e.g., 'userRequest')",
  "priority": "P1",
  "confidence": 0.85,
  "reviewers": ["code-style-reviewer", "code-principles-reviewer", "test-healer"],
  "note": "All reviewers identified this issue - high confidence"
}
```

### Example 2: Semantic Similarity Detection

**Input**:
```json
[
  {
    "reviewer": "code-principles-reviewer",
    "file": "Services/AuthService.cs",
    "line": 85,
    "issue_type": "error_handling",
    "description": "Method does not handle database connection exceptions",
    "priority": "P0",
    "confidence": 0.88
  },
  {
    "reviewer": "test-healer",
    "file": "Services/AuthService.cs",
    "line": 87,
    "issue_type": "error_handling",
    "description": "Missing try-catch for database exceptions",
    "priority": "P1",
    "confidence": 0.82
  }
]
```

**Processing**:
```
1. Exact Match: No match (different line numbers)

2. Semantic Similarity:
   Levenshtein similarity: 84% (above 80% threshold)
   Context check: Same file, lines 85 and 87 (within 10 lines)
   Result: Semantic duplicate detected

3. Merge:
   Priority: [P0, P1] → P0 (ANY P0 rule)
   Confidence: (0.88×1.0 + 0.82×1.2) / (1.0+1.2) = 0.849
```

**Output**:
```json
{
  "file": "Services/AuthService.cs",
  "line": 85,
  "issue_type": "error_handling",
  "description": "Method does not handle database connection exceptions. Missing try-catch for database exceptions.",
  "priority": "P0",
  "confidence": 0.85,
  "reviewers": ["code-principles-reviewer", "test-healer"],
  "note": "Semantic duplicate detected - merged from lines 85, 87"
}
```

### Example 3: Priority Aggregation with Conflict

**Input**:
```json
[
  {
    "reviewer": "code-principles-reviewer",
    "priority": "P0",
    "confidence": 0.95
  },
  {
    "reviewer": "code-style-reviewer",
    "priority": "P2",
    "confidence": 0.65
  },
  {
    "reviewer": "test-healer",
    "priority": "P2",
    "confidence": 0.70
  }
]
```

**Processing**:
```
1. Priority Aggregation:
   Priorities: [P0, P2, P2]
   ANY P0 present → Escalate to P0

2. Conflict Detection:
   P0 from one reviewer, P2 from others → CONFLICT

3. Confidence Calculation:
   Weighted: (0.95×1.0 + 0.65×1.0 + 0.70×1.2) / 3.2 = 0.774
```

**Output**:
```json
{
  "priority": "P0",
  "confidence": 0.77,
  "reviewers": ["code-principles-reviewer", "code-style-reviewer", "test-healer"],
  "note": "Priority conflict: [P0, P2, P2]. Escalated to P0 per ANY P0 rule. Consider manual review."
}
```

---

## Integration Points

### Input Format

**Expected Input**: JSON array of reviewer reports

```json
{
  "reviews": [
    {
      "reviewer_id": "code-style-reviewer",
      "timestamp": "2025-10-16T10:30:00Z",
      "issues": [
        {
          "file_path": "Services/AuthService.cs",
          "line_number": 42,
          "issue_type": "naming_convention",
          "description": "Variable 'x' should be renamed",
          "priority": "P1",
          "confidence": 0.85
        }
      ],
      "recommendations": [
        {
          "text": "Refactor complex method into smaller methods",
          "confidence": 0.78
        }
      ]
    }
  ]
}
```

### Output Format

**Consolidated Report**: JSON structure with deduplicated issues and synthesized recommendations

```json
{
  "consolidation_timestamp": "2025-10-16T10:35:00Z",
  "reviewers": ["code-style-reviewer", "code-principles-reviewer", "test-healer"],
  "statistics": {
    "total_issues_before": 150,
    "total_issues_after": 50,
    "deduplication_rate": 0.67,
    "processing_time_ms": 1850
  },
  "issues": [
    {
      "file_path": "Services/AuthService.cs",
      "line_number": 42,
      "issue_type": "naming_convention",
      "description": "Variable 'x' should be renamed",
      "priority": "P0",
      "confidence": 0.85,
      "reviewers": ["code-style-reviewer", "code-principles-reviewer"],
      "note": "Priority conflict detected"
    }
  ],
  "recommendations": [
    {
      "theme": "refactoring",
      "frequency": 3,
      "avg_confidence": 0.87,
      "recommendations": [
        "Extract method for complex conditional logic",
        "Reduce cyclomatic complexity in ProcessRequest"
      ]
    }
  ]
}
```

### Error Handling

```python
class ConsolidationError(Exception):
    pass

def consolidate_reviews(reviews):
    try:
        # Validate input
        if not reviews or len(reviews) == 0:
            raise ConsolidationError("No reviews provided")

        # Deduplicate issues
        deduplicated = deduplicate_issues(reviews)

        # Aggregate priorities and confidence
        aggregated = aggregate_metadata(deduplicated)

        # Synthesize recommendations
        recommendations = synthesize_recommendations(reviews)

        return {
            'issues': aggregated,
            'recommendations': recommendations
        }

    except Exception as e:
        # Log error and return empty report
        log_error(f"Consolidation failed: {e}")
        return {
            'issues': [],
            'recommendations': [],
            'error': str(e)
        }
```

---

## Version History

**Version 1.0** (2025-10-16):
- Initial consolidation algorithm
- Four core components: Deduplication, Priority Aggregation, Confidence Calculation, Recommendation Synthesis
- Exact match + semantic similarity deduplication (60-70% reduction target)
- Weighted confidence calculation (test-healer weight: 1.2)
- Keyword-based recommendation synthesis with frequency ranking
- Edge case handling: single reviewer, conflicting priorities, empty reports

**Planned Enhancements (Post-MVP)**:
- Machine learning-based semantic similarity (replace Levenshtein with embeddings)
- Dynamic reviewer weight adjustment based on historical accuracy
- Custom priority rules per project type
- Real-time consolidation (streaming mode for large report sets)

---

**Algorithm Status**: ACTIVE
**Owner**: Development Team
**Last Updated**: 2025-10-16
**Related Documentation**:
- Agent Specification: `.cursor/agents/review-consolidator/agent.md`
- Prompt Template: `.cursor/agents/review-consolidator/prompt.md`
- Implementation Plan: `Docs/plans/Review-Consolidator-Implementation-Plan/phase-1-foundation.md`
