# Phase 1: Foundation & Specifications

**Parent Plan**: [Review-Consolidator-Implementation-Plan.md](../Review-Consolidator-Implementation-Plan.md)

**Duration**: Day 1 (8-10 hours)
**Dependencies**: None (can start immediately)
**Deliverables**: 8 specification files, 1 architecture document

---

## Task 1.1: Agent Specifications (3-4 hours)

### 1.1A: Create review-consolidator agent specification
**File**: `.cursor/agents/review-consolidator/agent.md`
**Size**: 300-400 lines
**Complexity**: 15-20 tool calls

**Requirements**:
```yaml
---
name: review-consolidator
description: "Coordinate parallel review army and consolidate feedback into unified report"
tools: Task, Bash, Glob, Grep, Read, Write, Edit, TodoWrite
model: opus
color: blue
---
```

**Integration Steps** (concrete execution):
- [ ] Create directory: `.cursor/agents/review-consolidator/` using Bash (mkdir -p)
- [ ] Create file: `.cursor/agents/review-consolidator/agent.md` using Write tool
- [ ] Add frontmatter with name, description, tools, model, color (as above)
- [ ] Test invocation: Use Task tool with `subagent_type: "review-consolidator"` to verify agent loads

**Sections to implement**:
- [ ] НАЗНАЧЕНИЕ: Coordinate code-style-reviewer, code-principles-reviewer, test-healer in parallel
- [ ] ИНСТРУМЕНТЫ: Detailed usage of each tool
- [ ] WORKFLOW: 5-step process (select → launch → collect → consolidate → report)
- [ ] АВТОМАТИЧЕСКИЕ РЕКОМЕНДАЦИИ: Transitions to executor/validator/git agents
- [ ] МЕТРИКИ УСПЕХА: <6 min review, >70% deduplication
- [ ] ИНТЕГРАЦИЯ: Upstream/downstream agents
- [ ] ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ: 3 scenarios
- [ ] ОСОБЫЕ СЛУЧАИ: Timeouts, partial results

### 1.1B: Create consolidation algorithm document
**File**: `.cursor/agents/review-consolidator/consolidation-algorithm.md`
**Size**: 200-300 lines
**Complexity**: 10-15 tool calls

**Integration Steps** (concrete execution):
- [ ] Verify directory exists: `.cursor/agents/review-consolidator/` (from 1.1A)
- [ ] Create file: `.cursor/agents/review-consolidator/consolidation-algorithm.md` using Write tool
- [ ] Validate: Read file back to ensure proper formatting and completeness

**Algorithm Components**:
```markdown
## Issue Deduplication
- Exact match: file + line + issue_type
- Semantic similarity: Levenshtein distance >80%
- Hash-based grouping for performance

## Priority Aggregation
- P0 = ANY(reviewers.priority == P0)
- P1 = COUNT(reviewers.priority == P1) >= reviewers.length/2
- P2 = DEFAULT

## Confidence Calculation
- weighted_confidence = Σ(reviewer.confidence * reviewer.weight) / Σ(reviewer.weight)
- Weights: test-healer=1.2, others=1.0

## Recommendation Synthesis
- Group by theme using keyword extraction
- Rank by frequency across reviewers
- Filter confidence <60%
```

### 1.1C: Create prompt template
**File**: `.cursor/agents/review-consolidator/prompt.md`
**Size**: 400-500 lines
**Complexity**: 15-20 tool calls

**Integration Steps** (concrete execution):
- [ ] Verify directory exists: `.cursor/agents/review-consolidator/` (from 1.1A)
- [ ] Create file: `.cursor/agents/review-consolidator/prompt.md` using Write tool
- [ ] Validate: Read file to verify all sections present and examples complete

**Template Structure**:
```markdown
## Input Parameters
- code_files: string[] // Files to review
- review_types: string[] // Which reviewers to invoke
- options: {
    parallel: boolean = true
    timeout: number = 300000 // 5 minutes
    cache_ttl: number = 900000 // 15 minutes
  }

## Parallel Execution Instructions
Use single message with multiple Task tool calls:
[
  Task({ subagent_type: "code-style-reviewer", ... }),
  Task({ subagent_type: "code-principles-reviewer", ... }),
  Task({ subagent_type: "test-healer", ... })
]

## Output Format
{
  "executive_summary": "...",
  "critical_issues": [...],
  "warnings": [...],
  "recommendations": [...],
  "common_themes": [...],
  "action_items": [...],
  "metadata": {
    "total_issues_before": 45,
    "total_issues_after": 12,
    "deduplication_ratio": 0.73,
    "review_time_ms": 245000
  }
}
```

---

## Task 1.2: Individual Reviewer Specifications (3-4 hours)

### 1.2A: Create code-style-reviewer agent
**File**: `.cursor/agents/code-style-reviewer/agent.md`
**Size**: 200-250 lines
**Complexity**: 10-12 tool calls

**Integration Steps** (concrete execution):
- [ ] Create directory: `.cursor/agents/code-style-reviewer/` using Bash (mkdir -p)
- [ ] Create file: `.cursor/agents/code-style-reviewer/agent.md` using Write tool
- [ ] Verify rules exist: Read `.cursor/rules/csharp-codestyle.mdc` to confirm
- [ ] Test invocation: Use Task tool with `subagent_type: "code-style-reviewer"` to verify

**Implementation**:
```yaml
---
name: code-style-reviewer
description: "Validate code against style rules (formatting, naming, structure)"
tools: Read, Grep, TodoWrite
model: sonnet
color: green
---
```

**Core Logic**:
```markdown
## WORKFLOW
1. Load style rules from .cursor/rules/csharp-codestyle.mdc
2. Parse target files using Read tool
3. Apply rule patterns using Grep tool
4. Generate issues with confidence scores
5. Output structured JSON report

## Issue Format
{
  "file": "path/to/file.cs",
  "line": 42,
  "column": 15,
  "rule": "mandatory-braces",
  "severity": "P1",
  "confidence": 0.95,
  "message": "Single-line if statement must use braces",
  "suggestion": "Add braces around statement"
}
```

### 1.2B: Create code-principles-reviewer agent
**File**: `.cursor/agents/code-principles-reviewer/agent.md`
**Size**: 200-250 lines
**Complexity**: 10-12 tool calls

**Integration Steps** (concrete execution):
- [ ] Create directory: `.cursor/agents/code-principles-reviewer/` using Bash (mkdir -p)
- [ ] Create file: `.cursor/agents/code-principles-reviewer/agent.md` using Write tool
- [ ] Verify rules exist: Read `.cursor/rules/code-principles.mdc` to confirm
- [ ] Test invocation: Use Task tool with `subagent_type: "code-principles-reviewer"` to verify

**Implementation Focus**:
- SOLID principles validation
- DRY (Don't Repeat Yourself) detection
- KISS (Keep It Simple) analysis
- Dependency injection patterns
- Interface segregation checks

**Rules Reference**:
- `.cursor/rules/code-principles.mdc`
- `.cursor/rules/csharp-principles.mdc`

### 1.2C: Create test-healer agent
**File**: `.cursor/agents/test-healer/agent.md`
**Size**: 250-300 lines
**Complexity**: 12-15 tool calls

**Integration Steps** (concrete execution):
- [ ] Create directory: `.cursor/agents/test-healer/` using Bash (mkdir -p)
- [ ] Create file: `.cursor/agents/test-healer/agent.md` using Write tool
- [ ] Verify rules exist: Read `.cursor/rules/test-healing-principles.mdc` if exists
- [ ] Test invocation: Use Task tool with `subagent_type: "test-healer"` to verify
- [ ] Integration test: Run with sample failing test to verify analysis workflow

**Test Analysis Workflow**:
```markdown
1. Discover test files (*Tests.cs, *.Tests.cs)
2. Run tests using Bash: dotnet test
3. Parse test results for failures
4. Analyze failure patterns:
   - Assertion failures
   - Null reference exceptions
   - Dependency injection issues
   - Timeout failures
5. Generate fix recommendations
6. Output healing report with confidence
```

---

## Task 1.3: Architecture Documentation (2 hours)

### Create Planned Architecture Document
**File**: `Docs/Architecture/Planned/review-consolidator-architecture.md`
**Size**: 400-500 lines
**Complexity**: 8-10 tool calls

**Required Diagrams**:
1. Component relationships (mermaid graph)
2. Data flow sequence diagram
3. Parallel execution pattern
4. Consolidation pipeline
5. Report generation flow

**Integration Points**:
- Task tool for parallel invocation
- File system for report storage
- Caching layer for performance
- Cycle tracking metadata

---

## Validation Checklist

### Technical Completeness
- [ ] All agent specifications include frontmatter
- [ ] Tools explicitly listed for each agent
- [ ] Workflow steps ≤30 tool calls per task
- [ ] Output formats standardized (JSON)

### Integration Readiness
- [ ] Upstream agents identified
- [ ] Downstream transitions defined
- [ ] Parameter passing specified
- [ ] Error handling documented

### Documentation Quality
- [ ] Examples provided for each agent
- [ ] Edge cases documented
- [ ] Performance targets specified
- [ ] Architecture diagrams complete

---

## Next Phase Prerequisites

Before proceeding to Phase 2:
- [ ] All 8 specification files created
- [ ] Architecture document complete
- [ ] Review by work-plan-reviewer passed
- [ ] No blocking issues identified

---

**Status**: READY FOR IMPLEMENTATION
**Estimated Completion**: 8-10 hours
**Risk Level**: Low (specifications only, no code execution)