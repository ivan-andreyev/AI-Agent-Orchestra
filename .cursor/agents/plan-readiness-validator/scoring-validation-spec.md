# Scoring Algorithm Validation Specification

**Version**: 1.0
**Date**: 2025-10-15
**Purpose**: Define comprehensive methodology for validating scoring algorithm accuracy against approved work plans
**Status**: ACTIVE (Task 4.3 deliverable)

---

## Executive Summary

This document specifies the complete validation methodology for the plan-readiness-validator scoring algorithm. The validation process compares automated scores against manual baseline assessments across 5+ sample plans, ensuring ≥95% agreement (≤5 point average difference) between automated and manual scoring.

**Validation Goals**:
- Verify scoring algorithm accuracy against approved plans
- Identify systematic biases or discrepancies
- Calibrate scoring weights if necessary (avg difference >5 points)
- Document scoring accuracy for production readiness

**Success Criteria**:
- Average score difference ≤5 points
- Status agreement rate 100% (READY vs. REQUIRES_IMPROVEMENT)
- Component-level accuracy ≥90% per dimension
- No systematic biases detected

---

## Sample Plan Selection

### Validation Dataset (5+ Plans)

#### Category 1: Approved High-Quality Plans (3 samples)

**Sample 1: actions-block-refactoring-workplan.md**
- **Location**: `Docs/plans/actions-block-refactoring-workplan.md`
- **Expected Characteristics**:
  - Well-structured with proper decomposition
  - Complete integration steps (Entity, Service patterns)
  - Clear task decomposition with specific file paths
  - Approved plan with production execution
- **Expected Score**: 93/100 (READY)
  - Task Specificity: 28/30 (concrete paths, minor improvements possible)
  - Technical Completeness: 30/30 (all integration steps present)
  - Execution Clarity: 18/20 (clear decomposition, minor complexity issues)
  - Structure Compliance: 17/20 (minor file size or reference issues)
- **Expected Status**: READY (score ≥90%)

**Sample 2: UI-Fixes-WorkPlan-2024-09-18.md**
- **Location**: `Docs/plans/UI-Fixes-WorkPlan-2024-09-18.md`
- **Expected Characteristics**:
  - Focused on UI fixes (less architectural complexity)
  - May have some task specificity issues
  - Approved plan, executed successfully
- **Expected Score**: 90/100 (READY, borderline)
  - Task Specificity: 26/30 (some paths, some generic descriptions)
  - Technical Completeness: 28/30 (UI patterns, minor integration gaps)
  - Execution Clarity: 19/20 (UI tasks clearer, fewer dependencies)
  - Structure Compliance: 17/20 (minor structure issues)
- **Expected Status**: READY (score ≥90%)

**Sample 3: Remove-HangfireServer-Tests-Plan-REVISED.md**
- **Location**: `Docs/WorkPlans/Remove-HangfireServer-Tests-Plan-REVISED.md`
- **Expected Characteristics**:
  - REVISED status indicates quality improvement cycle completed
  - Removal/refactoring plan (different pattern than creation)
  - Approved after revision
- **Expected Score**: 92/100 (READY)
  - Task Specificity: 27/30 (specific test files, clear targets)
  - Technical Completeness: 29/30 (test removal patterns complete)
  - Execution Clarity: 19/20 (clear removal steps)
  - Structure Compliance: 17/20 (minor issues)
- **Expected Status**: READY (score ≥90%)

#### Category 2: Synthetic Problematic Plans (2 samples)

**Sample 4: Synthetic Incomplete Plan**
- **Location**: To be created at `.cursor/agents/plan-readiness-validator/test-samples/incomplete-plan-sample.md`
- **Expected Characteristics**:
  - Missing integration steps (Entity without DbContext)
  - Vague task descriptions (no file paths)
  - Missing acceptance criteria
  - No dependency information
- **Expected Score**: 68/100 (REQUIRES_IMPROVEMENT)
  - Task Specificity: 15/30 (missing paths, generic names)
  - Technical Completeness: 18/30 (missing DbContext, DI, migrations)
  - Execution Clarity: 12/20 (no dependencies, basic decomposition)
  - Structure Compliance: 18/20 (structure OK but content incomplete)
- **Expected Status**: REQUIRES_IMPROVEMENT (score <90%)

**Sample 5: Synthetic Over-Complex Plan**
- **Location**: To be created at `.cursor/agents/plan-readiness-validator/test-samples/overcomplex-plan-sample.md`
- **Expected Characteristics**:
  - High execution complexity (>30 tool calls per task)
  - Implementation code in plan (violates "Plan ≠ Realization")
  - Monolithic tasks without decomposition
  - Over-specified with LINQ queries, business logic
- **Expected Score**: 75/100 (REQUIRES_IMPROVEMENT)
  - Task Specificity: 18/30 (too much detail, loses architectural focus)
  - Technical Completeness: 25/30 (integration present but buried in code)
  - Execution Clarity: 10/20 (complexity deductions -10, monolithic -5)
  - Structure Compliance: 17/20 (likely >400 lines)
- **Expected Status**: REQUIRES_IMPROVEMENT (score <90%)

### Sample Plan Justification

**Why these plans?**:
1. **Diversity**: Creation (actions-block), UI (UI-Fixes), Removal (HangfireServer), Incomplete (synthetic), Over-complex (synthetic)
2. **Known Quality**: 3 approved plans with execution history provide reliable baseline
3. **Edge Cases**: Synthetic plans test algorithm boundaries (low quality, high complexity)
4. **Production Relevance**: All patterns represent real-world plan scenarios
5. **Score Distribution**: Covers entire range (68-93) including READY/REQUIRES_IMPROVEMENT boundary

---

## Part 1: Manual Scoring Methodology

### Overview

Manual scoring establishes baseline "ground truth" scores for comparison. Two independent reviewers score each plan using the scoring rubric, then average results to minimize individual bias.

### Manual Scoring Process

#### Step 1: Reviewer Selection

**Requirements**:
- 2 independent reviewers per plan
- Reviewers familiar with:
  - `.cursor/rules/common-plan-generator.mdc` rules
  - `.cursor/rules/common-plan-reviewer.mdc` criteria
  - `.cursor/rules/catalogization-rules.mdc` structure rules
- No prior knowledge of expected scores (blind review)

**Reviewers**: Development team members or experienced plan architects

#### Step 2: Rubric Application

**For each plan, each reviewer applies the scoring rubric manually**:

**Dimension 1: Task Specificity (0-30 points)**

**Sub-criteria 1.1: Concrete File Paths (0-10 points)**
```
Manual Process:
1. Count total tasks (### X.Y sections) in plan
2. Count tasks with explicit file paths (pattern: /path/to/file.ext or **File**: path)
3. Calculate: (tasks_with_paths / total_tasks) × 10
4. Record score

Example:
  Plan has 15 tasks total
  12 tasks have explicit file paths
  Score = (12/15) × 10 = 8.0 points
```

**Sub-criteria 1.2: Specific Class/Interface Names (0-10 points)**
```
Manual Process:
1. Count total technical tasks (creates code/config)
2. Count tasks with specific class/interface names (PascalCase identifiers)
3. Calculate: (tasks_with_names / total_technical_tasks) × 10
4. Record score

Example:
  Plan has 12 technical tasks
  10 tasks have specific names (IAuthService, AuthController, etc.)
  Score = (10/12) × 10 = 8.3 points
```

**Sub-criteria 1.3: Clear Acceptance Criteria (0-10 points)**
```
Manual Process:
1. Count total tasks
2. Count tasks with explicit acceptance criteria:
   - "**Acceptance Criteria**:" section OR
   - "- [ ]" checklist items (5+ items) OR
   - "**Expected:**" outcomes
3. Calculate: (tasks_with_criteria / total_tasks) × 10
4. Record score

Example:
  Plan has 15 tasks total
  14 tasks have acceptance criteria
  Score = (14/15) × 10 = 9.3 points
```

**Dimension 1 Total**: Sum of 1.1 + 1.2 + 1.3 (max 30 points)

---

**Dimension 2: Technical Completeness (0-30 points)**

**Sub-criteria 2.1: Integration Steps (0-15 points)**
```
Manual Process:
1. Identify technical task types:
   - Entity/Model tasks: Need DbContext, Entity config, Migration
   - Service tasks: Need Interface, DI registration, Program.cs
   - API tasks: Need Authorization, Middleware, Routing

2. For each task type, check if ALL integration steps present:
   Entity: [x] class, [x] DbContext, [x] config, [x] migration → COMPLETE
   Entity: [x] class, [ ] DbContext, [x] config, [ ] migration → INCOMPLETE

3. Count complete tasks vs. total technical tasks
4. Calculate: (complete_tasks / total_technical_tasks) × 15
5. Record score

Example:
  Plan has 8 technical tasks (3 Entity, 3 Service, 2 API)
  7 tasks have all integration steps (1 Entity missing migration)
  Score = (7/8) × 15 = 13.1 points
```

**Sub-criteria 2.2: Migration Workflow (0-10 points)**
```
Manual Process:
1. Count tasks involving database changes
2. Check each for migration workflow:
   - [ ] Migration creation command (dotnet ef migrations add)
   - [ ] Migration application step (dotnet ef database update)
   - [ ] Verification step (check schema)

3. Count tasks with complete workflow vs. total DB tasks
4. Calculate: (tasks_with_migrations / total_db_tasks) × 10
5. Record score

Example:
  Plan has 3 DB tasks
  3 tasks have complete migration workflow
  Score = (3/3) × 10 = 10.0 points
```

**Sub-criteria 2.3: Error Handling Patterns (0-5 points)**
```
Manual Process:
1. Count technical tasks
2. Count tasks mentioning error handling:
   - try-catch patterns
   - Validation logic (FluentValidation, DataAnnotations)
   - Error response models
   - Logging patterns (ILogger)

3. Calculate: (tasks_with_error_handling / total_technical_tasks) × 5
4. Record score

Example:
  Plan has 12 technical tasks
  9 tasks mention error handling
  Score = (9/12) × 5 = 3.75 points
```

**Dimension 2 Total**: Sum of 2.1 + 2.2 + 2.3 (max 30 points)

---

**Dimension 3: Execution Clarity (0-20 points)**

**Sub-criteria 3.1: Step-by-Step Decomposition (0-10 points)**
```
Manual Process:
1. Count total tasks
2. Assess decomposition level for each:
   Level 0: No decomposition (0 points)
   Level 1: Basic list (5 points)
   Level 2: Detailed steps (10 points)
   Level 3: Sub-task files (10 points)

3. Calculate average decomposition level
4. Calculate: (avg_level / max_level) × 10
5. Record score

Example:
  Plan has 15 tasks
  3 tasks Level 3 (10 pts each) = 30
  10 tasks Level 2 (10 pts each) = 100
  2 tasks Level 1 (5 pts each) = 10
  Total = 140 / 150 possible = 93.3%
  Score = 0.933 × 10 = 9.3 points
```

**Sub-criteria 3.2: Dependencies Clearly Identified (0-10 points)**
```
Manual Process:
1. Count tasks that SHOULD have dependencies (not first tasks)
2. Count tasks with explicit dependencies:
   - "**Dependencies**:" section
   - "After X.Y completes"
   - "Requires X.Y"
   - "Prerequisite: X.Y"

3. Calculate: (tasks_with_dependencies / tasks_needing_dependencies) × 10
4. Record score

Example:
  Plan has 15 tasks, 12 should have dependencies (3 are independent)
  10 tasks have explicit dependencies
  Score = (10/12) × 10 = 8.3 points
```

**Complexity Deductions**:
```
Manual Process:
1. Estimate tool calls per task (rough estimate):
   - File creation: 1 tool call (Write)
   - File edit: 2 tool calls (Read + Edit)
   - Test run: 1 tool call (Bash)
   - Complex task: sum all operations

2. Count tasks >30 tool calls
3. Apply deductions:
   - 1 task >30 calls: -5 points
   - 2-3 tasks >30 calls: -10 points
   - 4+ tasks >30 calls: -15 points

Example:
  Plan has 15 tasks
  2 tasks estimated at 35-40 tool calls each
  Deduction = -10 points
```

**Dimension 3 Total**: (3.1 + 3.2) - complexity_deductions (max 20 points, min 0)

---

**Dimension 4: Structure Compliance (0-20 points)**

**Sub-criteria 4.1: Catalogization Rules (0-10 points)**
```
Manual Process:
1. Check GOLDEN RULE #1: Directory name matches file name
   - List all coordinator files (files with child directories)
   - For each: Verify directory name = filename without .md
   - Count violations

2. Check GOLDEN RULE #2: Coordinator file outside directory
   - For each directory: Check if it contains file with same name
   - Count violations

3. Calculate score:
   - 0 violations: 10 points
   - 1-2 violations: 5 points
   - 3+ violations: 0 points

Example:
  Plan has 3 directories
  0 GOLDEN RULE violations
  Score = 10 points
```

**Sub-criteria 4.2: File Size Limits (0-5 points)**
```
Manual Process:
1. Count lines in each .md file (use wc -l or editor line count)
2. Check against limit: >400 lines = VIOLATION
3. Count violations:
   - 0 violations: 5 points
   - 1-2 violations: 3 points
   - 3+ violations: 0 points

Example:
  Plan has 5 files
  1 file has 523 lines (violation)
  Score = 3 points
```

**Sub-criteria 4.3: Reference Integrity (0-5 points)**
```
Manual Process:
1. Check parent → child references:
   - For each parent file: Verify all child links valid (files exist)

2. Check child → parent references:
   - For each child file: Verify parent link exists and valid

3. Count broken references:
   - 0 broken: 5 points
   - 1-2 broken: 3 points
   - 3+ broken: 0 points

Example:
  Plan has 8 parent-child relationships
  0 broken references
  Score = 5 points
```

**Dimension 4 Total**: Sum of 4.1 + 4.2 + 4.3 (max 20 points)

---

#### Step 3: Score Recording

**Manual Scoring Template (per reviewer)**:

```markdown
## Manual Scoring Results: [Plan Name]

**Reviewer**: [Name]
**Date**: [YYYY-MM-DD]
**Plan**: [path/to/plan.md]

### Dimension 1: Task Specificity

| Sub-Criteria | Score | Calculation | Notes |
|--------------|-------|-------------|-------|
| 1.1 File Paths | X.X/10 | (N/M) × 10 | N tasks with paths, M total |
| 1.2 Class Names | X.X/10 | (N/M) × 10 | N tasks with names, M technical |
| 1.3 Acceptance Criteria | X.X/10 | (N/M) × 10 | N tasks with criteria, M total |
| **Dimension 1 Total** | **XX.X/30** | | |

### Dimension 2: Technical Completeness

| Sub-Criteria | Score | Calculation | Notes |
|--------------|-------|-------------|-------|
| 2.1 Integration Steps | X.X/15 | (N/M) × 15 | N complete, M technical tasks |
| 2.2 Migration Workflow | X.X/10 | (N/M) × 10 | N with migrations, M DB tasks |
| 2.3 Error Handling | X.X/5 | (N/M) × 5 | N with error handling, M technical |
| **Dimension 2 Total** | **XX.X/30** | | |

### Dimension 3: Execution Clarity

| Sub-Criteria | Score | Calculation | Notes |
|--------------|-------|-------------|-------|
| 3.1 Decomposition | X.X/10 | (avg_level/max) × 10 | Decomposition levels assessed |
| 3.2 Dependencies | X.X/10 | (N/M) × 10 | N with deps, M needing deps |
| Complexity Deductions | -X | N tasks >30 calls | Deduction applied |
| **Dimension 3 Total** | **XX.X/20** | | |

### Dimension 4: Structure Compliance

| Sub-Criteria | Score | Calculation | Notes |
|--------------|-------|-------------|-------|
| 4.1 Catalogization | X/10 | 10 - (violations × 5) | N violations detected |
| 4.2 File Size | X/5 | 5 - (violations × 1.5) | N files >400 lines |
| 4.3 Reference Integrity | X/5 | 5 - (broken × 1.5) | N broken refs |
| **Dimension 4 Total** | **XX/20** | | |

### Final Manual Score

| Dimension | Score |
|-----------|-------|
| Task Specificity | XX.X/30 |
| Technical Completeness | XX.X/30 |
| Execution Clarity | XX.X/20 |
| Structure Compliance | XX/20 |
| **TOTAL SCORE** | **XX.X/100** |

**Status**: [READY if ≥90, REQUIRES_IMPROVEMENT if <90]

### Detailed Notes

[Any observations, edge cases, or scoring rationale]
```

#### Step 4: Reviewer Score Averaging

**Averaging Process**:
```
For each plan:
1. Collect scores from Reviewer 1 and Reviewer 2
2. Average each dimension:
   Dimension 1 = (R1_D1 + R2_D1) / 2
   Dimension 2 = (R1_D2 + R2_D2) / 2
   Dimension 3 = (R1_D3 + R2_D3) / 2
   Dimension 4 = (R1_D4 + R2_D4) / 2
3. Calculate final manual score: D1 + D2 + D3 + D4
4. Determine consensus status (READY if ≥90, REQUIRES_IMPROVEMENT if <90)

If reviewers disagree by >5 points:
- Third reviewer (tie-breaker) scores the plan
- Use median of 3 scores as final manual score
```

**Manual Baseline Template**:

```markdown
## Manual Baseline: [Plan Name]

**Plan**: [path/to/plan.md]
**Reviewers**: [R1 Name], [R2 Name]
**Date**: [YYYY-MM-DD]

### Reviewer Scores

| Dimension | Reviewer 1 | Reviewer 2 | Average |
|-----------|-----------|-----------|---------|
| Task Specificity | XX.X/30 | XX.X/30 | **XX.X/30** |
| Technical Completeness | XX.X/30 | XX.X/30 | **XX.X/30** |
| Execution Clarity | XX.X/20 | XX.X/20 | **XX.X/20** |
| Structure Compliance | XX/20 | XX/20 | **XX/20** |
| **TOTAL** | **XX.X/100** | **XX.X/100** | **XX.X/100** |

**Score Difference**: |R1 - R2| = X.X points

**Consensus Status**: [READY / REQUIRES_IMPROVEMENT]

**Inter-Rater Reliability**: [Good if difference ≤5, Review Needed if >5]
```

---

## Part 2: Automated Scoring Procedure

### Overview

Automated scoring runs the plan-readiness-validator agent against the same sample plans, collecting scores and comparing against manual baselines.

### Automated Scoring Process

#### Step 1: Prepare Test Environment

**Setup**:
```bash
# Create isolated test directory
mkdir -p .cursor/agents/plan-readiness-validator/validation-results

# Copy sample plans to test directory (or use in-place)
# For synthetic plans, create them first
```

#### Step 2: Invoke plan-readiness-validator Agent

**Invocation Method** (using Task tool):

```typescript
// For each sample plan
{
  "subagent_type": "plan-readiness-validator",
  "parameters": {
    "plan_file": "path/to/sample-plan.md",
    "output_format": "detailed" // Request full score breakdown
  }
}
```

**Alternative (if agent accepts direct file input)**:

```bash
# Run validator via command line (if CLI exists)
plan-readiness-validator validate --plan "path/to/sample-plan.md" --output-json
```

#### Step 3: Capture Automated Results

**Expected Output Format** (from agent validation report):

```markdown
## LLM Readiness Validation Report

**Plan**: path/to/sample-plan.md
**Validation Date**: YYYY-MM-DD HH:MM:SS
**Validator Version**: 1.0

### LLM Readiness Score: XX/100

**Status**: [READY / REQUIRES_IMPROVEMENT]

#### Score Breakdown

| Dimension | Score | Details |
|-----------|-------|---------|
| **Task Specificity** | XX/30 | |
| - File Paths | X/10 | N/M tasks have explicit paths |
| - Class Names | X/10 | N/M technical tasks have specific names |
| - Acceptance Criteria | X/10 | N/M tasks have clear criteria |
| **Technical Completeness** | XX/30 | |
| - Integration Steps | X/15 | N/M tasks complete |
| - Migration Workflow | X/10 | N/M DB tasks complete |
| - Error Handling | X/5 | N/M tasks mention error handling |
| **Execution Clarity** | XX/20 | |
| - Decomposition | X/10 | Avg level X.X |
| - Dependencies | X/10 | N/M tasks have deps |
| - Complexity Deductions | -X | N tasks >30 calls |
| **Structure Compliance** | XX/20 | |
| - Catalogization | X/10 | N violations |
| - File Size | X/5 | N violations |
| - Reference Integrity | X/5 | N broken refs |

### Issues Found: N

[List of specific issues with severity]

### Recommendations

[Agent transition recommendations]
```

#### Step 4: Parse and Record Automated Scores

**Automated Score Template**:

```markdown
## Automated Scoring Results: [Plan Name]

**Plan**: [path/to/plan.md]
**Validator Version**: 1.0
**Execution Date**: [YYYY-MM-DD]

### Score Breakdown

| Dimension | Score | Sub-Criteria Breakdown |
|-----------|-------|------------------------|
| Task Specificity | XX.X/30 | Paths: X/10, Names: X/10, Criteria: X/10 |
| Technical Completeness | XX.X/30 | Integration: X/15, Migrations: X/10, Errors: X/5 |
| Execution Clarity | XX.X/20 | Decomp: X/10, Deps: X/10, Complexity: -X |
| Structure Compliance | XX/20 | Catalog: X/10, Size: X/5, Refs: X/5 |
| **TOTAL SCORE** | **XX.X/100** | |

**Status**: [READY / REQUIRES_IMPROVEMENT]

### Issues Detected

[Number and types of issues]

### Execution Time

**Validation Time**: X.XX seconds
**Performance Target**: <60 seconds
**Status**: [PASS / FAIL]
```

---

## Part 3: Score Comparison Methodology

### Overview

Compare automated scores against manual baselines to calculate agreement metrics and identify discrepancies.

### Comparison Process

#### Step 1: Collect All Scores

**Master Score Table**:

```markdown
## Scoring Validation Results

| Plan | Manual Score | Automated Score | Difference | Status Match | Component Differences |
|------|-------------|----------------|-----------|--------------|----------------------|
| actions-block-refactoring | 93/100 | {auto}/100 | {diff} | {yes/no} | TS:{diff}, TC:{diff}, EC:{diff}, SC:{diff} |
| UI-Fixes-WorkPlan | 90/100 | {auto}/100 | {diff} | {yes/no} | TS:{diff}, TC:{diff}, EC:{diff}, SC:{diff} |
| Remove-HangfireServer-Tests | 92/100 | {auto}/100 | {diff} | {yes/no} | TS:{diff}, TC:{diff}, EC:{diff}, SC:{diff} |
| Incomplete Plan (Synthetic) | 68/100 | {auto}/100 | {diff} | {yes/no} | TS:{diff}, TC:{diff}, EC:{diff}, SC:{diff} |
| Over-Complex Plan (Synthetic) | 75/100 | {auto}/100 | {diff} | {yes/no} | TS:{diff}, TC:{diff}, EC:{diff}, SC:{diff} |

**Average Score Difference**: {avg_diff} points (target: ≤5 points)
**Status Agreement Rate**: {agreement_rate}% (target: 100%)
**Validation Result**: [PASS if avg_diff ≤5 AND agreement=100%, FAIL otherwise]
```

#### Step 2: Calculate Agreement Metrics

**Overall Agreement Metrics**:

```python
# Pseudo-code for metric calculation

# 1. Average Score Difference
score_differences = [abs(manual - automated) for each plan]
avg_diff = sum(score_differences) / len(score_differences)

# 2. Maximum Score Difference
max_diff = max(score_differences)

# 3. Minimum Score Difference
min_diff = min(score_differences)

# 4. Standard Deviation
std_dev = standard_deviation(score_differences)

# 5. Status Agreement Rate
status_matches = [1 if manual_status == automated_status else 0 for each plan]
status_agreement = (sum(status_matches) / len(status_matches)) × 100

# 6. Component-Level Differences
for dimension in [TS, TC, EC, SC]:
    component_diffs = [abs(manual_dim - automated_dim) for each plan]
    avg_component_diff = sum(component_diffs) / len(component_diffs)
```

**Agreement Metrics Template**:

```markdown
## Scoring Accuracy Analysis

### Overall Accuracy

- **Average Score Difference**: {avg_diff} points (target: ≤5 points)
- **Maximum Score Difference**: {max_diff} points
- **Minimum Score Difference**: {min_diff} points
- **Standard Deviation**: {std_dev} points
- **Status Agreement Rate**: {agreement_rate}% (target: 100%)

**Result**: [PASS if avg_diff ≤5 AND agreement=100%, FAIL otherwise]

### Component-Level Accuracy

| Component | Avg Difference | Max Difference | Accuracy |
|-----------|---------------|----------------|----------|
| Task Specificity | {avg_diff}/30 | {max_diff}/30 | {accuracy}% |
| Technical Completeness | {avg_diff}/30 | {max_diff}/30 | {accuracy}% |
| Execution Clarity | {avg_diff}/20 | {max_diff}/20 | {accuracy}% |
| Structure Compliance | {avg_diff}/20 | {max_diff}/20 | {accuracy}% |

**Component Accuracy Calculation**: 100% - (avg_diff / max_points × 100%)
```

#### Step 3: Identify Discrepancy Patterns

**Pattern Detection**:

```markdown
### Discrepancy Patterns

{if patterns detected}

#### Pattern 1: [Pattern Name]

**Description**: {describe systematic bias or pattern}

**Examples**:
- Plan: [name], Manual: X, Automated: Y, Diff: Z
- Plan: [name], Manual: X, Automated: Y, Diff: Z

**Root Cause Analysis**:
- **Hypothesis**: {why this pattern occurs}
- **Affected Component**: {which dimension}
- **Frequency**: {how many plans affected}

**Calibration Needed**: [YES / NO]

---

{repeat for each pattern}

{else}

✅ No systematic patterns detected. Discrepancies appear random and within acceptable variance.

{endif}
```

**Common Pattern Types**:

1. **Over-scoring Pattern**: Automated consistently scores higher than manual
   - Example: Automated gives 9/10 for file paths, manual gives 7/10
   - Root Cause: Pattern detection too lenient (accepts relative paths)
   - Calibration: Tighten file path regex to require absolute paths

2. **Under-scoring Pattern**: Automated consistently scores lower than manual
   - Example: Automated gives 12/15 integration steps, manual gives 14/15
   - Root Cause: Automated misses implicit integration steps
   - Calibration: Add pattern for implicit DI registration mentions

3. **Component-Specific Bias**: One dimension consistently off
   - Example: Structure compliance always differs by 3-5 points
   - Root Cause: Manual reviewers interpret GOLDEN RULES differently
   - Calibration: Clarify rule interpretation in manual scoring guide

4. **Status Boundary Issues**: Plans near 90% threshold scored inconsistently
   - Example: Manual: 91/100 (READY), Automated: 89/100 (REQUIRES_IMPROVEMENT)
   - Root Cause: Rounding differences or interpretation at boundary
   - Calibration: Review threshold sensitivity, consider ±1 point tolerance

---

## Part 4: Calibration Process

### Overview

If average score difference >5 points OR status agreement <100%, calibration adjustments are required to improve scoring accuracy.

### Calibration Decision Tree

```
START: Validation Results

├─ Is avg_diff ≤5 AND status_agreement = 100%?
│  ├─ YES → Skip Calibration, Document Success
│  └─ NO → Proceed to Calibration

├─ Identify Discrepancy Source:
│  ├─ Which component has largest avg difference?
│  │  ├─ Task Specificity → Review file path/class name/criteria patterns
│  │  ├─ Technical Completeness → Review integration step/migration/error patterns
│  │  ├─ Execution Clarity → Review decomposition/dependency detection
│  │  └─ Structure Compliance → Review catalogization/file size/reference checks
│  │
│  ├─ Are discrepancies systematic or random?
│  │  ├─ Systematic (pattern detected) → Adjust detection logic or weights
│  │  └─ Random (no pattern) → Review manual scoring consistency
│  │
│  └─ Are discrepancies due to edge cases?
│     ├─ YES → Add edge case handling
│     └─ NO → Adjust core algorithm

├─ Make Calibration Adjustments:
│  ├─ Pattern Detection: Tighten/loosen regex patterns
│  ├─ Weights: Adjust sub-criteria weights within dimensions
│  ├─ Thresholds: Adjust penalty/deduction thresholds
│  └─ Edge Cases: Add special handling for identified patterns

├─ Re-run Validation:
│  ├─ Apply adjusted algorithm to same sample plans
│  ├─ Calculate new metrics
│  └─ Compare to previous iteration

└─ Convergence Check:
   ├─ Is avg_diff ≤5 AND status_agreement = 100%?
   │  ├─ YES → Calibration Complete
   │  └─ NO → Iterate (max 5 iterations)
   │
   └─ If not converged after 5 iterations:
      ├─ Escalate to manual review
      ├─ Consider fundamental algorithm redesign
      └─ Document limitations
```

### Calibration Process Steps

#### Step 1: Identify Discrepancy Source

**Analysis Questions**:

1. **Which component is most discrepant?**
   ```
   Calculate component-level avg differences:
   - Task Specificity: avg |manual_TS - automated_TS| = X.X points
   - Technical Completeness: avg |manual_TC - automated_TC| = X.X points
   - Execution Clarity: avg |manual_EC - automated_EC| = X.X points
   - Structure Compliance: avg |manual_SC - automated_SC| = X.X points

   Identify: Component with largest avg difference
   ```

2. **Is the bias systematic?**
   ```
   For identified component:
   - If automated consistently > manual → Over-scoring bias
   - If automated consistently < manual → Under-scoring bias
   - If differences random → No systematic bias
   ```

3. **What is the root cause?**
   ```
   Over-scoring causes:
   - Pattern detection too lenient (accepts partial matches)
   - Weights too generous (small improvements = big score gains)
   - Missing penalty cases (edge cases not handled)

   Under-scoring causes:
   - Pattern detection too strict (rejects valid patterns)
   - Weights too harsh (small issues = big score losses)
   - Over-penalizing edge cases
   ```

#### Step 2: Design Calibration Adjustments

**Calibration Types**:

**Type 1: Pattern Detection Adjustment**

```markdown
### Pattern Detection Calibration

**Component**: Task Specificity → File Paths (1.1)

**Issue**: Automated accepts relative paths, manual requires absolute
**Impact**: Automated over-scores by avg 2.3 points

**Current Pattern**:
```regex
/\w+\/\w+\.\w+/ or \w+\\\w+\.\w+
```

**Adjusted Pattern**:
```regex
(src|Docs|\.cursor)/[\w/\\]+\.\w+  # Must start with project root directory
```

**Expected Impact**: Reduce file path scores by ~2 points avg
```

**Type 2: Weight Adjustment**

```markdown
### Weight Adjustment Calibration

**Component**: Technical Completeness → Error Handling (2.3)

**Issue**: Automated under-scores error handling
**Impact**: Automated under-scores by avg 1.5 points

**Current Weight**: (tasks_with_error_handling / total_technical_tasks) × 5
**Issue**: 5 points max too low for importance of error handling

**Adjusted Weight**: (tasks_with_error_handling / total_technical_tasks) × 7
**Compensation**: Reduce Integration Steps from 15 to 13 (keep dimension total 30)

**Expected Impact**: Increase error handling scores by ~1.5 points avg
```

**Type 3: Threshold Adjustment**

```markdown
### Threshold Calibration

**Component**: Execution Clarity → Complexity Deductions

**Issue**: Automated applies -10 points too aggressively
**Impact**: Automated under-scores borderline complex plans

**Current Threshold**:
- 1 task >30 tool calls: -5 points
- 2-3 tasks >30 tool calls: -10 points
- 4+ tasks >30 tool calls: -15 points

**Adjusted Threshold**:
- 1 task 30-35 tool calls: -3 points
- 1 task >35 tool calls: -5 points
- 2-3 tasks >30 tool calls: -8 points (reduced from -10)
- 4+ tasks >30 tool calls: -15 points (unchanged)

**Expected Impact**: Reduce over-penalization by ~2 points avg
```

**Type 4: Edge Case Handling**

```markdown
### Edge Case Calibration

**Component**: Structure Compliance → File Size (4.2)

**Issue**: Plans with 401-420 lines penalized same as 500+ lines
**Impact**: Automated over-penalizes borderline large files

**Current Logic**:
```python
if lines > 400:
    violations += 1
```

**Adjusted Logic**:
```python
if lines > 450:  # Increased threshold slightly
    violations += 1
elif lines > 400:
    violations += 0.5  # Partial violation for borderline cases
```

**Expected Impact**: Reduce structure score variance by ~1 point
```

#### Step 3: Document Calibration Changes

**Calibration Log Template**:

```markdown
## Calibration Log

### Iteration {N}

**Date**: YYYY-MM-DD
**Trigger**: {avg_diff > 5 / status_agreement < 100%}
**Root Cause**: {description of identified issue}

#### Changes Made

1. **Component**: {component name}
   - **Change Type**: {Pattern / Weight / Threshold / Edge Case}
   - **Change Description**: {what was adjusted}
   - **Rationale**: {why this adjustment}
   - **Expected Impact**: {predicted improvement}

2. {repeat for each change}

#### Validation Results (Pre-Calibration)

| Metric | Value |
|--------|-------|
| Average Score Difference | {pre_avg_diff} points |
| Status Agreement Rate | {pre_agreement}% |
| Problematic Component | {component} (avg diff: {diff}) |

#### Validation Results (Post-Calibration)

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Average Score Difference | {pre_avg_diff} | {post_avg_diff} | {improvement} |
| Status Agreement Rate | {pre_agreement}% | {post_agreement}% | {improvement} |
| Component Accuracy | {pre_component}% | {post_component}% | {improvement} |

#### Assessment

**Convergence Status**: {CONVERGED / NOT CONVERGED}
**Next Action**: {COMPLETE / ITERATE / ESCALATE}

**Notes**: {any observations or recommendations}

---
```

#### Step 4: Re-run Validation

**Re-validation Process**:

1. **Apply calibrated algorithm** to same 5+ sample plans
2. **Collect new automated scores** using adjusted logic
3. **Compare against original manual baselines** (manual scores unchanged)
4. **Recalculate agreement metrics** with new automated scores
5. **Check convergence**: avg_diff ≤5 AND status_agreement = 100%

**Iteration Limit**: Maximum 5 calibration iterations
- If not converged after 5 iterations → Escalate to algorithm redesign

#### Step 5: Finalize Calibration

**Calibration Complete When**:
- Average score difference ≤5 points
- Status agreement rate = 100%
- No systematic biases detected
- Component-level accuracy ≥90% for all dimensions

**Final Calibration Summary**:

```markdown
## Final Calibration Summary

**Total Iterations**: {iteration_count}
**Final Metrics**:
- Average Score Difference: {final_avg_diff} points (target: ≤5)
- Status Agreement Rate: {final_agreement}% (target: 100%)
- Component Accuracy: All ≥90%

**Calibration Changes Applied**:
1. {change summary}
2. {change summary}
...

**Validation Result**: ✅ PASS (algorithm ready for production)

**Algorithm Version**: 1.1 (post-calibration)
**Calibration Date**: YYYY-MM-DD
**Approved By**: {reviewer name}

**Documentation Updates Required**:
- [ ] Update scoring-algorithm.md with version 1.1 changes
- [ ] Document calibration history in version control
- [ ] Update agent.md with calibrated algorithm reference
- [ ] Update test-validation.md with final validation results
```

---

## Part 5: Validation Results Template

### Overview

The validation results template consolidates all scoring data, comparisons, and calibration outcomes into a single comprehensive report.

### Master Validation Report Template

```markdown
# Scoring Algorithm Validation Report

**Validation Date**: YYYY-MM-DD
**Algorithm Version**: {initial_version} → {final_version}
**Validator**: {person/team name}
**Status**: {IN_PROGRESS / COMPLETE / FAILED}

---

## Executive Summary

**Validation Objective**: Verify plan-readiness-validator scoring accuracy against manual baseline

**Sample Size**: {N} plans ({X} approved, {Y} synthetic)

**Key Results**:
- **Final Average Score Difference**: {final_diff} points (target: ≤5)
- **Status Agreement Rate**: {final_agreement}% (target: 100%)
- **Calibration Iterations**: {iteration_count}
- **Validation Result**: {PASS / FAIL}

**Conclusion**: {summary of validation outcome}

---

## Section 1: Sample Plan Details

### Sample Plan Selection

| # | Plan Name | Type | Location | Expected Score | Expected Status |
|---|-----------|------|----------|----------------|-----------------|
| 1 | actions-block-refactoring | Approved | Docs/plans/... | 93/100 | READY |
| 2 | UI-Fixes-WorkPlan | Approved | Docs/plans/... | 90/100 | READY |
| 3 | Remove-HangfireServer-Tests | Approved | Docs/WorkPlans/... | 92/100 | READY |
| 4 | Incomplete Plan (Synthetic) | Synthetic | test-samples/... | 68/100 | REQUIRES_IMPROVEMENT |
| 5 | Over-Complex Plan (Synthetic) | Synthetic | test-samples/... | 75/100 | REQUIRES_IMPROVEMENT |

**Sample Diversity**:
- Plan Types: {creation, UI, removal, incomplete, over-complex}
- Score Range: {min} - {max} (covers READY and REQUIRES_IMPROVEMENT)
- Technical Patterns: {Entity/Service/API, UI components, test removal, etc.}

---

## Section 2: Manual Scoring Baselines

### Manual Scoring Summary

| Plan | Reviewer 1 | Reviewer 2 | Average (Baseline) | Status | Inter-Rater Diff |
|------|-----------|-----------|-------------------|--------|------------------|
| actions-block | X/100 | Y/100 | **Z/100** | READY | ±N |
| UI-Fixes | X/100 | Y/100 | **Z/100** | READY | ±N |
| Remove-HangfireServer | X/100 | Y/100 | **Z/100** | READY | ±N |
| Incomplete (Synth) | X/100 | Y/100 | **Z/100** | REQUIRES_IMPROVEMENT | ±N |
| Over-Complex (Synth) | X/100 | Y/100 | **Z/100** | REQUIRES_IMPROVEMENT | ±N |

**Inter-Rater Reliability**: {assessment}
- Average difference between reviewers: {avg_diff} points
- Maximum difference: {max_diff} points
- Assessment: {GOOD if <5, REVIEW NEEDED if ≥5}

### Component-Level Manual Scores

| Plan | TS (30) | TC (30) | EC (20) | SC (20) | Total |
|------|---------|---------|---------|---------|-------|
| actions-block | XX | XX | XX | XX | **93** |
| UI-Fixes | XX | XX | XX | XX | **90** |
| Remove-HangfireServer | XX | XX | XX | XX | **92** |
| Incomplete (Synth) | XX | XX | XX | XX | **68** |
| Over-Complex (Synth) | XX | XX | XX | XX | **75** |

---

## Section 3: Automated Scoring Results

### Initial Automated Scores (Pre-Calibration)

| Plan | Automated Score | Manual Baseline | Difference | Status | Status Match |
|------|----------------|----------------|-----------|--------|--------------|
| actions-block | {auto}/100 | 93/100 | {diff} | {status} | {yes/no} |
| UI-Fixes | {auto}/100 | 90/100 | {diff} | {status} | {yes/no} |
| Remove-HangfireServer | {auto}/100 | 92/100 | {diff} | {status} | {yes/no} |
| Incomplete (Synth) | {auto}/100 | 68/100 | {diff} | {status} | {yes/no} |
| Over-Complex (Synth) | {auto}/100 | 75/100 | {diff} | {status} | {yes/no} |

**Initial Metrics**:
- Average Score Difference: {avg_diff} points
- Maximum Difference: {max_diff} points
- Status Agreement Rate: {agreement}%
- **Result**: {PASS / NEEDS CALIBRATION}

### Component-Level Automated Scores

| Plan | TS (30) | TC (30) | EC (20) | SC (20) | Total |
|------|---------|---------|---------|---------|-------|
| actions-block | XX | XX | XX | XX | {auto} |
| UI-Fixes | XX | XX | XX | XX | {auto} |
| Remove-HangfireServer | XX | XX | XX | XX | {auto} |
| Incomplete (Synth) | XX | XX | XX | XX | {auto} |
| Over-Complex (Synth) | XX | XX | XX | XX | {auto} |

### Component-Level Discrepancies

| Plan | TS Diff | TC Diff | EC Diff | SC Diff |
|------|---------|---------|---------|---------|
| actions-block | ±X | ±X | ±X | ±X |
| UI-Fixes | ±X | ±X | ±X | ±X |
| Remove-HangfireServer | ±X | ±X | ±X | ±X |
| Incomplete (Synth) | ±X | ±X | ±X | ±X |
| Over-Complex (Synth) | ±X | ±X | ±X | ±X |
| **Avg Difference** | **±X** | **±X** | **±X** | **±X** |

---

## Section 4: Discrepancy Analysis

### Overall Discrepancy Summary

**Average Score Difference**: {avg_diff} points
- Target: ≤5 points
- Status: {PASS / FAIL}

**Status Agreement Rate**: {agreement}%
- Target: 100%
- Status: {PASS / FAIL}

**Component with Largest Discrepancy**: {component_name}
- Average difference: {avg_diff} points
- Pattern: {over-scoring / under-scoring / random}

### Identified Patterns

{if patterns found}

#### Pattern 1: {Pattern Name}

**Description**: {systematic bias description}

**Affected Plans**: {list of plans showing this pattern}

**Component**: {dimension or sub-criteria}

**Root Cause Hypothesis**: {explanation}

**Calibration Required**: {YES / NO}

---

{repeat for each pattern}

{else}

✅ No systematic patterns detected. Discrepancies appear random within acceptable variance.

{endif}

### Discrepancy Details by Plan

{for each plan with significant discrepancy (>5 points)}

#### Plan: {plan_name}

**Score Difference**: {diff} points (Manual: {manual}, Automated: {auto})

**Component Breakdown**:
- Task Specificity: Manual {M}, Automated {A}, Diff {D}
- Technical Completeness: Manual {M}, Automated {A}, Diff {D}
- Execution Clarity: Manual {M}, Automated {A}, Diff {D}
- Structure Compliance: Manual {M}, Automated {A}, Diff {D}

**Analysis**:
{explain why discrepancy occurred, which component drove it, and proposed fix}

---

{end for}

---

## Section 5: Calibration History

{if calibration performed}

### Calibration Required: YES

**Trigger**: {avg_diff > 5 / status_agreement < 100%}

**Calibration Iterations**: {N}

---

{for each iteration}

### Calibration Iteration {N}

**Date**: YYYY-MM-DD

#### Pre-Calibration Metrics

| Metric | Value |
|--------|-------|
| Average Score Difference | {pre_diff} points |
| Status Agreement Rate | {pre_agreement}% |
| Problematic Component | {component} |

#### Changes Applied

1. **Component**: {component_name}
   - **Change Type**: {Pattern / Weight / Threshold / Edge Case}
   - **Description**: {what changed}
   - **Rationale**: {why}

{repeat for each change}

#### Post-Calibration Metrics

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Avg Score Difference | {before} | {after} | {delta} |
| Status Agreement | {before}% | {after}% | {delta}% |
| Component Accuracy | {before}% | {after}% | {delta}% |

#### Iteration Result

**Convergence**: {YES / NO}
**Next Action**: {COMPLETE / ITERATE / ESCALATE}

---

{end for}

### Final Calibration Result

**Total Iterations**: {N}
**Convergence Achieved**: {YES / NO}
**Final Average Difference**: {final_diff} points
**Final Status Agreement**: {final_agreement}%

**Calibration Assessment**: {SUCCESSFUL / NEEDS FURTHER WORK}

{else}

### Calibration Required: NO

✅ Initial automated scores met accuracy targets without calibration.

{endif}

---

## Section 6: Final Validation Results

### Final Metrics

| Metric | Target | Achieved | Status |
|--------|--------|----------|--------|
| Average Score Difference | ≤5 points | {final_diff} points | {PASS/FAIL} |
| Maximum Score Difference | ≤8 points | {max_diff} points | {PASS/FAIL} |
| Status Agreement Rate | 100% | {agreement}% | {PASS/FAIL} |
| Component Accuracy (all) | ≥90% | {min_accuracy}% | {PASS/FAIL} |

### Final Score Comparison

| Plan | Manual | Automated | Difference | Status Match |
|------|--------|-----------|-----------|--------------|
| actions-block | 93 | {final_auto} | {diff} | ✅ |
| UI-Fixes | 90 | {final_auto} | {diff} | ✅ |
| Remove-HangfireServer | 92 | {final_auto} | {diff} | ✅ |
| Incomplete (Synth) | 68 | {final_auto} | {diff} | ✅ |
| Over-Complex (Synth) | 75 | {final_auto} | {diff} | ✅ |
| **AVERAGE DIFFERENCE** | | | **{avg_diff}** | **{agreement}%** |

### Component-Level Final Accuracy

| Component | Avg Difference | Max Difference | Accuracy | Status |
|-----------|---------------|----------------|----------|--------|
| Task Specificity | {avg}/30 | {max}/30 | {acc}% | {PASS/FAIL} |
| Technical Completeness | {avg}/30 | {max}/30 | {acc}% | {PASS/FAIL} |
| Execution Clarity | {avg}/20 | {max}/20 | {acc}% | {PASS/FAIL} |
| Structure Compliance | {avg}/20 | {max}/20 | {acc}% | {PASS/FAIL} |

---

## Section 7: Algorithm Production Readiness

### Validation Outcome

**Overall Result**: {✅ PASS / ❌ FAIL}

**Pass Criteria**:
- [x] Average score difference ≤5 points: {achieved}
- [x] Status agreement rate 100%: {achieved}
- [x] All components ≥90% accuracy: {achieved}
- [x] No systematic biases: {confirmed}

### Algorithm Status

**Version**: {final_version}
**Status**: {PRODUCTION_READY / NEEDS_IMPROVEMENT}
**Approval Date**: YYYY-MM-DD
**Approved By**: {name}

### Recommendations

{if PASS}

✅ **Scoring algorithm validated and approved for production use**

**Next Steps**:
1. Update scoring-algorithm.md with version {final_version}
2. Document calibration changes in version history
3. Integrate validated algorithm into plan-readiness-validator agent
4. Update test-validation.md with final results
5. Proceed to Phase 5: Documentation and Integration

{else}

❌ **Scoring algorithm requires further refinement**

**Issues**:
- {list remaining issues}

**Recommended Actions**:
1. {action to address issue}
2. {action to address issue}

**Re-validation Required**: YES
**Estimated Timeline**: {timeline}

{endif}

---

## Section 8: Appendices

### Appendix A: Raw Manual Scoring Data

{include detailed manual scoring sheets from both reviewers}

### Appendix B: Raw Automated Scoring Output

{include full validator output for each plan}

### Appendix C: Calibration Code Changes

{include diffs or descriptions of algorithm changes made during calibration}

### Appendix D: Test Sample Plans

{include or reference synthetic plan content used for validation}

---

**Report Status**: {DRAFT / FINAL}
**Last Updated**: YYYY-MM-DD
**Document Owner**: {team/person}
**Related Documents**:
- scoring-algorithm.md (algorithm specification)
- test-validation.md (broader test validation)
- phase-4-testing.md (task specification)
```

---

## Validation Execution Timeline

### Estimated Timeline

| Phase | Duration | Activities |
|-------|----------|------------|
| **Manual Scoring** | 4-6 hours | 2 reviewers × 5 plans × ~30 min per plan |
| **Automated Scoring** | 1 hour | Run validator on 5 plans, collect results |
| **Comparison Analysis** | 2 hours | Calculate metrics, identify patterns |
| **Calibration (if needed)** | 2-4 hours | Adjust algorithm, re-run, iterate |
| **Documentation** | 2 hours | Complete validation report |
| **TOTAL** | **11-15 hours** | Complete Task 4.3 |

### Critical Path

1. **Manual Scoring** (blocking) → Must complete before automated scoring comparison
2. **Automated Scoring** (blocking) → Must complete before comparison
3. **Comparison Analysis** (blocking) → Must complete before calibration decision
4. **Calibration** (conditional) → Only if metrics fail
5. **Documentation** (final) → Consolidate all results

---

## Success Criteria Checklist

Task 4.3 complete when:

- [ ] 5+ sample plans selected and documented
- [ ] Manual scoring baseline created (2 reviewers per plan)
- [ ] Automated scoring executed for all sample plans
- [ ] Score comparison performed with metrics calculated
- [ ] Average score difference ≤5 points achieved
- [ ] Status agreement rate 100% achieved
- [ ] Discrepancy patterns analyzed (if any)
- [ ] Calibration performed (if needed) and documented
- [ ] Final validation report completed
- [ ] Scoring algorithm approved for production (or remediation plan created)

---

## Related Documentation

**Primary References**:
- `.cursor/agents/plan-readiness-validator/scoring-algorithm.md` - Algorithm specification
- `Docs/plans/plan-readiness-validator/phase-4-testing.md` - Task 4.3 specification (lines 558-796)
- `.cursor/agents/plan-readiness-validator/test-validation.md` - Overall test validation (Task 4.1)
- `.cursor/agents/plan-readiness-validator/integration-test-spec.md` - Integration testing (Task 4.2)

**Supporting Documents**:
- `.cursor/rules/common-plan-generator.mdc` - Plan structure rules
- `.cursor/rules/common-plan-reviewer.mdc` - Review criteria
- `.cursor/rules/catalogization-rules.mdc` - Structure rules

---

**Document Status**: ✅ COMPLETE (Task 4.3 Deliverable)
**Version**: 1.0
**Date**: 2025-10-15
**Owner**: Development Team
**Next Steps**: Execute validation using this specification, complete validation report
