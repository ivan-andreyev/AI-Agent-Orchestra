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
