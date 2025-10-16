# Plan Readiness Validator - Test Validation Specification

**Document Type**: Test Specification
**Purpose**: Define comprehensive testing methodology for plan-readiness-validator agent validation
**Target Accuracy**: ≥95% agreement with manual systematic-plan-reviewer results
**Performance Target**: <60 seconds per typical plan (5-15 files)

---

## Test Plan Overview

### Validation Objectives

1. **Scoring Accuracy**: Verify LLM readiness scoring algorithm produces consistent, reproducible scores within ±5 points of manual review
2. **Status Determination**: Confirm READY/REQUIRES_IMPROVEMENT status matches manual review (100% agreement target)
3. **Issue Detection**: Validate comprehensive issue detection (≥90% recall, ≥85% precision)
4. **Performance**: Benchmark validation time across plan sizes (<60 seconds target for typical plans)
5. **Integration**: Verify smooth agent handoffs and correct transition matrix application
6. **Edge Cases**: Document edge case handling for empty plans, circular references, over-sized files

### Test Sample Composition

**Total Samples**: 10+ work plans
- **5 READY Plans**: Expected score ≥90%, should pass validation
- **5 REQUIRES_IMPROVEMENT Plans**: Expected score <90%, should require revision

**Sample Sources**:
- **Existing Approved Plans** (3): Real production plans with known quality
- **Synthetic Test Plans** (7+): Created specifically for testing edge cases

### Success Criteria

| Metric | Target | Description |
|--------|--------|-------------|
| Overall Agreement Rate | ≥95% | Matching READY/REQUIRES_IMPROVEMENT status with manual review |
| Average Score Difference | ≤5 points | Absolute difference between automated and manual scores |
| False Positive Rate | <10% | Plans flagged as REQUIRES_IMPROVEMENT but actually READY |
| False Negative Rate | <5% | Plans flagged as READY but actually REQUIRES_IMPROVEMENT (critical) |
| Issue Detection Recall | ≥90% | Percentage of manual issues detected by automation |
| Issue Detection Precision | ≥85% | Percentage of detected issues that are correct |
| Performance (Typical) | <60 seconds | Validation time for plans with 5-15 files, 2000-3000 total lines |

---

## Test Sample Definitions

### Category 1: READY Plans (Expected Score ≥90%)

#### Sample 1.1: High-Quality Approved Plan with Complete Integration

**Plan**: `Docs/plans/actions-block-refactoring-workplan.md`
**Source**: Existing approved production plan
**Expected Manual Score**: 93/100

**Characteristics**:
- Complete integration steps (Entity → DbContext → Migration workflow)
- Concrete file paths in all tasks
- Clear task decomposition with explicit dependencies
- Proper catalogization (GOLDEN RULES followed)
- File sizes within limits (≤400 lines)
- Architectural components present (BatchTaskExecutor, WorkflowTemplate)

**Expected Automated Score Breakdown**:
- Task Specificity: 28/30 (concrete paths, minor improvements possible)
- Technical Completeness: 30/30 (all integration steps present)
- Execution Clarity: 18/20 (clear decomposition, minor complexity issues)
- Structure Compliance: 17/20 (minor file size or reference issues)
- **Total**: 93/100 ✅ READY

**Test Validation Focus**:
- Verify Entity task integration detection (DbContext, OnModelCreating, migrations)
- Verify architectural component detection (BatchTaskExecutor, WorkflowTemplate)
- Verify agent recommendations: plan-task-executor (CRITICAL), architecture-documenter (CRITICAL)

---

#### Sample 1.2: UI-Focused Plan with Minimal Architecture

**Plan**: `Docs/plans/UI-Fixes-WorkPlan-2024-09-18.md`
**Source**: Existing approved production plan
**Expected Manual Score**: 90/100

**Characteristics**:
- Focused on UI fixes (less architectural complexity)
- Clear acceptance criteria with visual regression testing
- May have some task specificity variations (UI tasks vs. backend tasks)
- Should still meet 90% threshold

**Expected Automated Score Breakdown**:
- Task Specificity: 26/30 (some UI tasks less concrete)
- Technical Completeness: 28/30 (UI tasks have different integration patterns)
- Execution Clarity: 18/20 (clear UI testing criteria)
- Structure Compliance: 18/20 (good structure)
- **Total**: 90/100 ✅ READY

**Test Validation Focus**:
- Verify algorithm handles non-backend tasks appropriately
- Verify UI testing patterns recognized (visual regression, component testing)
- Verify no false architectural component detection (UI components ≠ services)

---

#### Sample 1.3: Removal/Refactoring Plan with Clean Structure

**Plan**: `Docs/plans/Remove-HangfireServer-Tests-Plan-REVISED.md`
**Source**: Existing approved production plan
**Expected Manual Score**: 92/100

**Characteristics**:
- Removal/refactoring tasks (different pattern from creation tasks)
- REVISED status indicates quality review passed
- Should score ≥90%

**Expected Automated Score Breakdown**:
- Task Specificity: 28/30 (concrete removal targets)
- Technical Completeness: 29/30 (removal includes verification steps)
- Execution Clarity: 18/20 (clear removal dependencies)
- Structure Compliance: 17/20 (good structure)
- **Total**: 92/100 ✅ READY

**Test Validation Focus**:
- Verify algorithm handles removal/refactoring tasks (not just creation)
- Verify verification steps recognized (test removal, dependency cleanup)
- Verify no over-penalization for "non-standard" task patterns

---

#### Sample 1.4: Synthetic High-Quality Plan with Architectural Components

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/synthetic-ready-architecture.md`
**Source**: Synthetic test plan (created for testing)
**Expected Manual Score**: 94/100

**Characteristics**:
- Perfect Entity pattern: Entity class → DbContext → OnModelCreating → Migration
- Perfect Service pattern: Interface → Implementation → DI registration
- Perfect API pattern: Controller → Authorization → Middleware → Routing
- 5 architectural components (AuthService, TokenValidator, UserRepository, AuthController, RefreshTokenEntity)
- All tasks have concrete file paths, class names, acceptance criteria
- All tasks ≤30 tool calls (well-decomposed)
- Proper TODO markers (architectural planning, not implementation)

**Expected Automated Score Breakdown**:
- Task Specificity: 30/30 (perfect specificity)
- Technical Completeness: 30/30 (all integration steps present)
- Execution Clarity: 18/20 (clear decomposition, minor dependency improvements)
- Structure Compliance: 16/20 (minor file size warnings)
- **Total**: 94/100 ✅ READY

**Test Validation Focus**:
- Verify perfect Entity/Service/API pattern recognition
- Verify architectural component detection (5 components)
- Verify agent recommendations: plan-task-executor, architecture-documenter, parallel-plan-optimizer (>5 tasks)

---

#### Sample 1.5: Synthetic Plan with >5 Tasks (Parallelization Opportunity)

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/synthetic-ready-parallel.md`
**Source**: Synthetic test plan (created for testing)
**Expected Manual Score**: 91/100

**Characteristics**:
- 12 tasks total (exceeds 5-task threshold)
- Well-structured with proper decomposition
- Should trigger parallel-plan-optimizer recommendation

**Expected Automated Score Breakdown**:
- Task Specificity: 28/30
- Technical Completeness: 28/30
- Execution Clarity: 18/20
- Structure Compliance: 17/20
- **Total**: 91/100 ✅ READY

**Test Validation Focus**:
- Verify task counting accuracy (### X.Y pattern matching)
- Verify parallel-plan-optimizer recommendation (RECOMMENDED priority)
- Verify time reduction estimation (40-50% reduction)

---

### Category 2: REQUIRES_IMPROVEMENT Plans (Expected Score <90%)

#### Sample 2.1: Monolithic Plan (File Size Violation)

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/synthetic-fail-monolithic.md`
**Source**: Synthetic test plan (created for testing)
**Expected Manual Score**: 68/100

**Characteristics**:
- Single file with 650 lines (exceeds 400-line limit)
- No decomposition into child files
- Tasks are present but not properly separated into phase files

**Expected Automated Score Breakdown**:
- Task Specificity: 20/30 (some tasks vague due to monolithic structure)
- Technical Completeness: 22/30 (integration steps present but incomplete)
- Execution Clarity: 12/20 (poor decomposition, complexity violations)
- Structure Compliance: 14/20 (file size violation -3, catalogization -3)
- **Total**: 68/100 ❌ REQUIRES_IMPROVEMENT

**Test Validation Focus**:
- Verify file size violation detection (650 lines > 400 limit)
- Verify deduction calculation (-3 to -5 points for size violation)
- Verify recommendation: work-plan-architect with "decompose into child files" guidance

**Expected Issues Detected**:
- CRITICAL: File size exceeds 400 lines (actual: 650)
- IMPORTANT: Plan lacks proper phase file decomposition
- IMPORTANT: Complexity violations (3 tasks >30 tool calls)

---

#### Sample 2.2: Incomplete Entity Integration (Missing DbContext)

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/synthetic-fail-incomplete-entity.md`
**Source**: Synthetic test plan (created for testing)
**Expected Manual Score**: 76/100

**Characteristics**:
- Entity tasks present but missing DbContext integration
- No DbSet registration mentioned
- No Entity configuration in OnModelCreating
- No migration creation commands

**Expected Automated Score Breakdown**:
- Task Specificity: 28/30 (concrete paths present)
- Technical Completeness: 18/30 (missing integration steps -12 points)
- Execution Clarity: 16/20 (clear steps but incomplete)
- Structure Compliance: 14/20 (minor issues)
- **Total**: 76/100 ❌ REQUIRES_IMPROVEMENT

**Test Validation Focus**:
- Verify Entity integration checklist detection (DbContext, OnModelCreating, migration)
- Verify specific issue reporting with file:line references
- Verify recommendation: work-plan-architect with "add integration steps" guidance

**Expected Issues Detected**:
- CRITICAL: Entity task missing DbContext.DbSet registration
- CRITICAL: Entity task missing OnModelCreating configuration
- CRITICAL: Entity task missing migration creation command
- IMPORTANT: No migration workflow defined

---

#### Sample 2.3: Over-Complex Tasks (>30 Tool Calls)

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/synthetic-fail-high-complexity.md`
**Source**: Synthetic test plan (created for testing)
**Expected Manual Score**: 74/100

**Characteristics**:
- 3 tasks estimated at 40-50 tool calls each (exceeds 30-call limit)
- Tasks are monolithic "implement entire feature" instead of decomposed steps

**Expected Automated Score Breakdown**:
- Task Specificity: 22/30 (vague due to monolithic tasks)
- Technical Completeness: 24/30 (integration steps present but complex)
- Execution Clarity: 8/20 (complexity deduction -10 points for 3 over-complex tasks)
- Structure Compliance: 20/20 (structure fine)
- **Total**: 74/100 ❌ REQUIRES_IMPROVEMENT

**Test Validation Focus**:
- Verify tool call estimation heuristic (TODO items × 2 + code blocks × 3 + file ops × 1 + tech ops × 2)
- Verify complexity violation detection (>30 tool calls)
- Verify deduction calculation (-10 points for 2-3 over-complex tasks)
- Verify recommendation: work-plan-architect with "decompose tasks" guidance

**Expected Issues Detected**:
- CRITICAL: Task 2.1 complexity exceeds 30 tool calls (estimated 42)
- CRITICAL: Task 3.1 complexity exceeds 30 tool calls (estimated 45)
- CRITICAL: Task 4.2 complexity exceeds 30 tool calls (estimated 38)
- Recommendation: Split each task into 3-4 subtasks

---

#### Sample 2.4: Implementation Code in Plan (Violates "Plan ≠ Realization")

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/synthetic-fail-implementation.md`
**Source**: Synthetic test plan (created for testing)
**Expected Manual Score**: 72/100

**Characteristics**:
- Plan contains full method implementations with business logic
- LINQ queries, loops, try-catch blocks with detailed handling
- Few or no TODO markers (<5 TODOs in entire plan)

**Expected Automated Score Breakdown**:
- Task Specificity: 18/30 (over-detailed, penalized -10 for implementation)
- Technical Completeness: 25/30 (integration present but overshadowed by implementation)
- Execution Clarity: 13/20 (implementation code creates execution ambiguity -5)
- Structure Compliance: 16/20 (structure acceptable)
- **Total**: 72/100 ❌ REQUIRES_IMPROVEMENT

**Test Validation Focus**:
- Verify TODO marker counting (<5 TODOs → BAD, >20 TODOs → GOOD)
- Verify implementation pattern detection (LINQ queries, loops, try-catch)
- Verify deduction calculation (-10 Task Specificity, -5 Execution Clarity)
- Verify recommendation: work-plan-architect with "replace implementation with architecture" guidance

**Expected Issues Detected**:
- CRITICAL: Plan contains full implementation code (violates "Plan ≠ Realization" principle)
- CRITICAL: Only 3 TODO markers detected (expected >20 for architectural planning)
- Issue examples: LINQ queries in lines 78, 92, 105; try-catch blocks in lines 112, 134
- Recommendation: Replace implementation with method signatures + TODO comments

---

#### Sample 2.5: GOLDEN RULE Violations (Catalogization)

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/synthetic-fail-golden-rules.md`
**Source**: Synthetic test plan (created for testing)
**Expected Manual Score**: 78/100

**Characteristics**:
- GOLDEN RULE #1 violation: Directory name doesn't match file name
  - File: `01-Data-Models.md`
  - Directory: `01-architecture/` (should be `01-Data-Models/`)
- GOLDEN RULE #2 violation: Coordinator file inside its directory
  - File: `02-Services/02-Services.md` (coordinator inside directory)
- 2 broken parent/child references

**Expected Automated Score Breakdown**:
- Task Specificity: 24/30
- Technical Completeness: 26/30
- Execution Clarity: 18/20
- Structure Compliance: 10/20 (catalogization -5, references -5)
- **Total**: 78/100 ❌ REQUIRES_IMPROVEMENT

**Test Validation Focus**:
- Verify GOLDEN RULE #1 detection (directory name match check)
- Verify GOLDEN RULE #2 detection (coordinator placement check)
- Verify reference integrity validation (parent/child link verification)
- Verify deduction calculation (0 violations → 10/10, 1-2 violations → 5/10, 3+ violations → 0/10)

**Expected Issues Detected**:
- CRITICAL: GOLDEN RULE #1 violation - File "01-Data-Models.md" has directory "01-architecture/" (should be "01-Data-Models/")
- CRITICAL: GOLDEN RULE #2 violation - Coordinator "02-Services.md" is inside its directory "02-Services/"
- IMPORTANT: Broken parent reference in "03-API.md" line 5 (file not found: "../02-services/service-implementation.md")
- IMPORTANT: Broken child reference in "01-Data-Models.md" line 12 (file not found: "./01-Data-Models/03-viewmodels.md")

---

## Edge Case Test Scenarios

### Edge Case 1: Empty Plan (Coordinator-Only, No Tasks)

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/edge-empty-plan.md`
**Expected Manual Score**: 40/100

**Characteristics**:
- Coordinator file with Executive Summary, Context, but NO task sections (### X.Y)
- No child phase files
- Valid markdown structure but no executable work

**Expected Automated Score Breakdown**:
- Task Specificity: 0/30 (no tasks to evaluate)
- Technical Completeness: 0/30 (no technical tasks)
- Execution Clarity: 0/20 (no execution steps)
- Structure Compliance: 10/20 (structure valid but incomplete)
- **Total**: 10/100 ❌ REQUIRES_IMPROVEMENT

**Test Validation Focus**:
- Verify zero-task detection (no ### X.Y sections found)
- Verify appropriate error messaging ("No tasks found in plan")
- Verify recommendation: work-plan-architect with "add Work Breakdown Structure" guidance

**Expected Error Message**:
```
❌ CRITICAL: No tasks found in plan
- Plan file contains overview sections but no executable tasks (### X.Y)
- Add Work Breakdown Structure with task sections
- Refer to common-plan-generator.mdc for task structure guidance
```

---

### Edge Case 2: Plan Exceeding 400 Lines But Well-Decomposed

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/edge-large-coordinator.md`
**Expected Manual Score**: 88/100 (borderline)

**Characteristics**:
- Main coordinator file: 450 lines (exceeds 400 limit)
- 5 child phase files, each ≤400 lines (properly decomposed)
- Plan content is appropriate, just coordinator size issue

**Expected Automated Score Breakdown**:
- Task Specificity: 28/30
- Technical Completeness: 28/30
- Execution Clarity: 18/20
- Structure Compliance: 14/20 (file size violation -3)
- **Total**: 88/100 ❌ REQUIRES_IMPROVEMENT (borderline)

**Test Validation Focus**:
- Verify file size violation detection (450 > 400)
- Verify deduction is proportional to violation (-3 points for 450 lines)
- Verify note about proper decomposition mitigating impact
- Verify recommendation: work-plan-architect with "reduce coordinator size" guidance

**Expected Warning Message**:
```
⚠️ File size violation: 450 lines (limit: 400)
File: Docs/plans/edge-large-coordinator.md

Note: Plan is well-decomposed into child files (5 phase files, all ≤400 lines).
Recommendation: Move more content from coordinator to phase files.
Impact: Minor (does not block execution, but affects maintainability)
```

---

### Edge Case 3: Broken References (Non-Existent Phase Files)

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/edge-broken-references.md`
**Expected Manual Score**: 82/100

**Characteristics**:
- Coordinator links to 3 phase files
- 2 of those phase files do not exist (file paths broken)
- Plan content otherwise acceptable

**Expected Automated Score Breakdown**:
- Task Specificity: 26/30
- Technical Completeness: 26/30
- Execution Clarity: 18/20
- Structure Compliance: 12/20 (reference integrity -3 per broken link × 2)
- **Total**: 82/100 ❌ REQUIRES_IMPROVEMENT

**Test Validation Focus**:
- Verify reference integrity validation (file existence check)
- Verify specific broken links reported with file paths
- Verify deduction calculation (-3 points per broken reference)
- Verify recommendation: work-plan-architect with "create missing files or fix links" guidance

**Expected Issues Detected**:
- IMPORTANT: Broken reference in "edge-broken-references.md" line 45
  - Link: `./edge-broken-references/02-validation-logic.md`
  - Issue: File does not exist
  - Recommendation: Create file or fix link path
- IMPORTANT: Broken reference in "edge-broken-references.md" line 67
  - Link: `./edge-broken-references/04-documentation.md`
  - Issue: File does not exist
  - Recommendation: Create file or fix link path

---

### Edge Case 4: Circular References Between Phase Files

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/edge-circular-references.md`
**Expected Manual Score**: 84/100

**Characteristics**:
- Phase file A links to phase file B
- Phase file B links back to phase file A (not through coordinator)
- Creates circular dependency

**Expected Automated Score Breakdown**:
- Task Specificity: 26/30
- Technical Completeness: 26/30
- Execution Clarity: 10/20 (circular dependency deduction -10)
- Structure Compliance: 12/20 (reference integrity issue)
- **Total**: 74/100 ❌ REQUIRES_IMPROVEMENT

**Test Validation Focus**:
- Verify circular reference detection (graph cycle detection)
- Verify Execution Clarity deduction (-10 points for circular dependency)
- Verify clear error messaging with cycle path
- Verify recommendation: work-plan-architect with "break circular dependency" guidance

**Expected Error Message**:
```
❌ CRITICAL: Circular dependency detected
- Phase file A (01-foundation.md) depends on Phase file B (02-implementation.md)
- Phase file B (02-implementation.md) depends on Phase file A (01-foundation.md)

Recommendation: Break circular dependency by:
1. Splitting one task into pre-requisite and dependent parts
2. Reordering tasks to remove cycle
3. Using coordinator for navigation instead of direct phase-to-phase links
```

---

### Edge Case 5: Mixed Architecture and Implementation

**Plan**: `.cursor/agents/plan-readiness-validator/test-samples/edge-mixed-architecture-implementation.md`
**Expected Manual Score**: 79/100

**Characteristics**:
- 50% of tasks have TODO markers and architectural planning (GOOD)
- 50% of tasks have full implementation code (BAD)
- Mixed quality creates partial "Plan ≠ Realization" violation

**Expected Automated Score Breakdown**:
- Task Specificity: 20/30 (partial implementation penalty -5)
- Technical Completeness: 26/30
- Execution Clarity: 15/20 (partial ambiguity -3)
- Structure Compliance: 18/20
- **Total**: 79/100 ❌ REQUIRES_IMPROVEMENT

**Test Validation Focus**:
- Verify TODO marker counting (10 TODOs detected)
- Verify implementation pattern detection (LINQ, loops in 5 tasks)
- Verify proportional deduction (50% implementation → -5 Task Specificity, -3 Execution Clarity)
- Verify recommendation: work-plan-architect with "convert implementation to architecture" guidance

**Expected Issues Detected**:
- IMPORTANT: Tasks 2.1, 2.3, 3.1, 4.2, 5.1 contain full implementation code
- IMPORTANT: Only 10 TODO markers detected (expected >20 for full architectural planning)
- Recommendation: Replace implementation code in flagged tasks with:
  - Method signatures only
  - TODO comments indicating future implementation
  - throw new NotImplementedException() placeholders

---

## Performance Benchmark Specification

### Performance Test Plans

#### Benchmark 1: Small Plans (1-3 files, <500 total lines)

**Target**: <15 seconds

**Test Samples**:
- `test-samples/perf-small-single-file.md` (1 file, 250 lines)
- `test-samples/perf-small-3-files.md` (1 coordinator + 2 phase files, 400 total lines)

**Measurement Method**:
1. Start timer before validator invocation
2. Run validator with plan file path
3. Stop timer after validation report generated
4. Record time in seconds

**Expected Results**:
- Single file (250 lines): 8-12 seconds
- 3 files (400 total lines): 12-15 seconds

---

#### Benchmark 2: Medium Plans (4-7 files, 500-2000 total lines)

**Target**: <30 seconds

**Test Samples**:
- `test-samples/perf-medium-5-files.md` (1 coordinator + 4 phase files, 1200 total lines)
- `test-samples/perf-medium-7-files.md` (1 coordinator + 6 phase files, 1800 total lines)

**Measurement Method**: Same as Benchmark 1

**Expected Results**:
- 5 files (1200 total lines): 20-25 seconds
- 7 files (1800 total lines): 25-30 seconds

---

#### Benchmark 3: Large Plans (8-15 files, 2000-5000 total lines)

**Target**: <60 seconds

**Test Samples**:
- `test-samples/perf-large-10-files.md` (1 coordinator + 9 phase files, 3000 total lines)
- `test-samples/perf-large-15-files.md` (1 coordinator + 14 phase files, 4500 total lines)

**Measurement Method**: Same as Benchmark 1

**Expected Results**:
- 10 files (3000 total lines): 40-50 seconds
- 15 files (4500 total lines): 50-60 seconds

---

#### Benchmark 4: Very Large Plans (>15 files, >5000 total lines)

**Target**: <90 seconds (acceptable with warning)

**Test Samples**:
- `test-samples/perf-very-large-20-files.md` (1 coordinator + 19 phase files, 6500 total lines)

**Measurement Method**: Same as Benchmark 1

**Expected Results**:
- 20 files (6500 total lines): 70-90 seconds

**Warning Condition**: If >90 seconds, validator should issue performance warning:
```
⚠️ Performance Warning: Validation took 95 seconds (target: <90s)
Plan size: 20 files, 6500 total lines (very large)
Recommendation: Consider plan decomposition or optimization opportunities
```

---

### Performance Benchmark Results Template

```markdown
## Performance Benchmark Results

| Plan Size | Files | Total Lines | Validation Time | Target | Status |
|-----------|-------|-------------|----------------|--------|--------|
| Small     | 1     | 250         | {time}s        | <15s   | {PASS/FAIL} |
| Small     | 3     | 400         | {time}s        | <15s   | {PASS/FAIL} |
| Medium    | 5     | 1200        | {time}s        | <30s   | {PASS/FAIL} |
| Medium    | 7     | 1800        | {time}s        | <30s   | {PASS/FAIL} |
| Large     | 10    | 3000        | {time}s        | <60s   | {PASS/FAIL} |
| Large     | 15    | 4500        | {time}s        | <60s   | {PASS/FAIL} |
| Very Large| 20    | 6500        | {time}s        | <90s   | {PASS/FAIL} |

**Average Validation Time**: {avg_time}s
**Median Validation Time**: {median_time}s
**95th Percentile**: {p95_time}s (target: <60s for typical plans)

**Performance Analysis**:
- Bottlenecks identified: {list_bottlenecks if applicable}
- Optimization opportunities: {list_optimizations if applicable}
- Acceptable performance: {yes/no}

**Conclusion**: {PASS if all targets met, FAIL otherwise}
```

---

## Test Execution Methodology

### Step 1: Baseline Creation (Manual Scoring)

**Purpose**: Establish ground truth for validation comparison

**Process**:
1. Select 10+ sample plans (5 READY, 5 REQUIRES_IMPROVEMENT)
2. Manually score each plan using scoring rubric from `scoring-algorithm.md`
3. Have 2 independent reviewers score each plan
4. Average reviewer scores for baseline
5. Document manual scores and rationale

**Output**: Manual scoring baseline table

```markdown
| Plan | Reviewer 1 | Reviewer 2 | Average | Status | Notes |
|------|-----------|-----------|---------|--------|-------|
| actions-block-refactoring | 94/100 | 92/100 | 93/100 | READY | Excellent integration steps |
| synthetic-fail-monolithic | 70/100 | 66/100 | 68/100 | REQUIRES_IMPROVEMENT | File size violation |
| ... | ... | ... | ... | ... | ... |
```

---

### Step 2: Automated Validation Execution

**Purpose**: Run plan-readiness-validator on all sample plans

**Process**:
1. For each sample plan:
   - Invoke plan-readiness-validator with plan file path
   - Record validation time (start to finish)
   - Capture validation report output
   - Extract score breakdown (Task Specificity, Technical Completeness, Execution Clarity, Structure Compliance)
   - Extract status (READY / REQUIRES_IMPROVEMENT)
   - Extract issues list
2. Record all results in automated validation table

**Output**: Automated validation results table

```markdown
| Plan | Automated Score | Status | Validation Time | Issues Count |
|------|----------------|--------|----------------|--------------|
| actions-block-refactoring | {score}/100 | {status} | {time}s | {count} |
| synthetic-fail-monolithic | {score}/100 | {status} | {time}s | {count} |
| ... | ... | ... | ... | ... |
```

---

### Step 3: Comparison and Metrics Calculation

**Purpose**: Compare automated vs. manual results and calculate accuracy metrics

**Process**:
1. **Overall Agreement Rate**:
   - Count matching status (READY ↔ READY, REQUIRES_IMPROVEMENT ↔ REQUIRES_IMPROVEMENT)
   - Calculate: (matching_results / total_samples) × 100%
   - Target: ≥95%

2. **Score Accuracy**:
   - Calculate absolute difference for each plan: |automated_score - manual_score|
   - Average differences: sum(differences) / total_samples
   - Target: ≤5 points

3. **False Positive Rate**:
   - False Positive: Plan flagged as REQUIRES_IMPROVEMENT but manual review = READY
   - Calculate: (false_positives / (false_positives + true_negatives)) × 100%
   - Target: <10%

4. **False Negative Rate**:
   - False Negative: Plan flagged as READY but manual review = REQUIRES_IMPROVEMENT (CRITICAL)
   - Calculate: (false_negatives / (false_negatives + true_positives)) × 100%
   - Target: <5%

5. **Issue Detection Recall**:
   - Recall: (detected_issues ∩ manual_issues) / manual_issues
   - Target: ≥90%

6. **Issue Detection Precision**:
   - Precision: (detected_issues ∩ manual_issues) / detected_issues
   - Target: ≥85%

**Output**: Metrics summary table

```markdown
| Metric                    | Value | Target | Status |
|--------------------------|-------|--------|--------|
| Overall Agreement Rate   | {value}% | ≥95% | {PASS/FAIL} |
| Average Score Difference | {value} pts | ≤5 pts | {PASS/FAIL} |
| False Positive Rate      | {value}% | <10% | {PASS/FAIL} |
| False Negative Rate      | {value}% | <5% | {PASS/FAIL} |
| Issue Detection Recall   | {value}% | ≥90% | {PASS/FAIL} |
| Issue Detection Precision| {value}% | ≥85% | {PASS/FAIL} |
```

---

### Step 4: Discrepancy Analysis

**Purpose**: Identify root causes of disagreements between manual and automated scoring

**Process**:
For each plan where |automated_score - manual_score| > 5:
1. Document score breakdown by dimension
2. Identify dimension with largest discrepancy
3. Analyze root cause:
   - Pattern detection issue? (algorithm missed or incorrectly detected pattern)
   - Weight imbalance? (dimension over- or under-weighted)
   - Edge case? (algorithm not designed to handle this scenario)
   - Manual reviewer error? (human inconsistency)
4. Determine if calibration adjustment needed
5. Document finding

**Output**: Discrepancy analysis report

```markdown
## Discrepancy Analysis

### Plan: {plan_name}
**Manual Score**: {manual_score}/100
**Automated Score**: {automated_score}/100
**Difference**: {difference} points

### Dimension Breakdown
| Dimension | Manual | Automated | Difference |
|-----------|--------|-----------|-----------|
| Task Specificity | {manual_ts} | {auto_ts} | {diff_ts} |
| Technical Completeness | {manual_tc} | {auto_tc} | {diff_tc} |
| Execution Clarity | {manual_ec} | {auto_ec} | {diff_ec} |
| Structure Compliance | {manual_sc} | {auto_sc} | {diff_sc} |

### Root Cause
**Primary Discrepancy**: {dimension_name} ({difference} points)
**Root Cause**: {detailed_explanation}
**Calibration Needed**: {yes/no}

### Recommended Action
{specific_calibration_adjustment_if_needed}
```

---

### Step 5: Calibration Adjustments (If Needed)

**Purpose**: Adjust scoring algorithm weights or thresholds if accuracy <95%

**Trigger**: Average score difference >5 points OR false negative rate ≥5%

**Process**:
1. Identify dimension(s) with consistent over- or under-scoring
2. Adjust weights or detection patterns:
   - **If Task Specificity consistently over-scored**: Increase penalties for missing file paths or class names
   - **If Technical Completeness consistently under-scored**: Adjust integration step detection patterns (keywords, structure)
   - **If Execution Clarity off**: Calibrate tool call estimation heuristics (adjust multipliers)
   - **If Structure Compliance off**: Adjust file size penalty weights or catalogization violation scoring
3. Document calibration changes in `scoring-algorithm.md` version history
4. Re-run automated validation on all sample plans
5. Recalculate metrics
6. Iterate until metrics meet targets

**Output**: Calibration log

```markdown
## Calibration Log

### Iteration 1
**Date**: {timestamp}
**Changes Made**:
- {adjustment_1_description}
- {adjustment_2_description}

**Results**:
- Average Score Difference: {before} → {after} points
- Overall Agreement Rate: {before}% → {after}%
- False Negative Rate: {before}% → {after}%

**Assessment**: {needs_further_calibration / acceptable}

---

[Repeat for additional iterations]

### Final Calibration
**Total Iterations**: {iteration_count}
**Final Average Difference**: {final_diff} points
**Final Agreement Rate**: {final_agreement}%
**Final False Negative Rate**: {final_fn_rate}%
**Result**: {PASS/FAIL}
```

---

## Test Sample Repository Structure

All test sample plans stored in: `.cursor/agents/plan-readiness-validator/test-samples/`

**Directory Structure**:
```
.cursor/agents/plan-readiness-validator/test-samples/
├── synthetic-ready-architecture.md          (Sample 1.4)
├── synthetic-ready-parallel.md              (Sample 1.5)
├── synthetic-fail-monolithic.md             (Sample 2.1)
├── synthetic-fail-incomplete-entity.md      (Sample 2.2)
├── synthetic-fail-high-complexity.md        (Sample 2.3)
├── synthetic-fail-implementation.md         (Sample 2.4)
├── synthetic-fail-golden-rules.md           (Sample 2.5)
├── edge-empty-plan.md                       (Edge Case 1)
├── edge-large-coordinator.md                (Edge Case 2)
├── edge-broken-references.md                (Edge Case 3)
├── edge-circular-references.md              (Edge Case 4)
├── edge-mixed-architecture-implementation.md (Edge Case 5)
├── perf-small-single-file.md                (Performance Benchmark 1)
├── perf-small-3-files.md                    (Performance Benchmark 1)
├── perf-medium-5-files.md                   (Performance Benchmark 2)
├── perf-medium-7-files.md                   (Performance Benchmark 2)
├── perf-large-10-files.md                   (Performance Benchmark 3)
├── perf-large-15-files.md                   (Performance Benchmark 3)
└── perf-very-large-20-files.md              (Performance Benchmark 4)
```

**Note**: Existing production plans (samples 1.1, 1.2, 1.3) remain in `Docs/plans/` and are referenced by path.

---

## Test Automation Script Specification

**Purpose**: Automate test execution and metrics calculation

**Script**: `.cursor/agents/plan-readiness-validator/run-test-validation.ps1` (PowerShell)

**Functionality**:
1. Load all test sample plans (from test-samples/ and Docs/plans/)
2. Run plan-readiness-validator on each sample
3. Capture validation reports and timing
4. Load manual baseline scores from configuration file
5. Calculate all metrics (agreement rate, score difference, false positive/negative, recall, precision)
6. Generate test results report (markdown format)
7. Output PASS/FAIL status

**Usage**:
```powershell
# Run test validation
.\run-test-validation.ps1

# Run with manual baseline file
.\run-test-validation.ps1 -BaselineFile "manual-scores.json"

# Run specific sample category
.\run-test-validation.ps1 -Category "READY"  # or "REQUIRES_IMPROVEMENT" or "EdgeCases"
```

**Output**: Test results report (`test-validation-results-{timestamp}.md`)

---

## Final Test Results Report Template

```markdown
# Plan Readiness Validator - Test Validation Results

**Test Date**: {timestamp}
**Validator Version**: v1.0
**Test Sample Count**: {total_samples}

---

## Executive Summary

**Overall Result**: {PASS if all metrics meet targets, FAIL otherwise}

**Key Metrics**:
- Overall Agreement Rate: {value}% (target: ≥95%) → {PASS/FAIL}
- Average Score Difference: {value} pts (target: ≤5 pts) → {PASS/FAIL}
- False Negative Rate: {value}% (target: <5%) → {PASS/FAIL}

---

## Test Sample Results

### READY Plans (Expected Score ≥90%)

| Sample | Manual Score | Automated Score | Difference | Status Match | Result |
|--------|-------------|----------------|-----------|--------------|--------|
| actions-block-refactoring | 93/100 | {auto}/100 | {diff} | {yes/no} | {PASS/FAIL} |
| UI-Fixes-WorkPlan | 90/100 | {auto}/100 | {diff} | {yes/no} | {PASS/FAIL} |
| Remove-HangfireServer-Tests | 92/100 | {auto}/100 | {diff} | {yes/no} | {PASS/FAIL} |
| synthetic-ready-architecture | 94/100 | {auto}/100 | {diff} | {yes/no} | {PASS/FAIL} |
| synthetic-ready-parallel | 91/100 | {auto}/100 | {diff} | {yes/no} | {PASS/FAIL} |

### REQUIRES_IMPROVEMENT Plans (Expected Score <90%)

| Sample | Manual Score | Automated Score | Difference | Status Match | Result |
|--------|-------------|----------------|-----------|--------------|--------|
| synthetic-fail-monolithic | 68/100 | {auto}/100 | {diff} | {yes/no} | {PASS/FAIL} |
| synthetic-fail-incomplete-entity | 76/100 | {auto}/100 | {diff} | {yes/no} | {PASS/FAIL} |
| synthetic-fail-high-complexity | 74/100 | {auto}/100 | {diff} | {yes/no} | {PASS/FAIL} |
| synthetic-fail-implementation | 72/100 | {auto}/100 | {diff} | {yes/no} | {PASS/FAIL} |
| synthetic-fail-golden-rules | 78/100 | {auto}/100 | {diff} | {yes/no} | {PASS/FAIL} |

---

## Accuracy Metrics

| Metric | Value | Target | Status |
|--------|-------|--------|--------|
| Overall Agreement Rate | {value}% | ≥95% | {PASS/FAIL} |
| Average Score Difference | {value} pts | ≤5 pts | {PASS/FAIL} |
| False Positive Rate | {value}% | <10% | {PASS/FAIL} |
| False Negative Rate | {value}% | <5% | {PASS/FAIL} |
| Issue Detection Recall | {value}% | ≥90% | {PASS/FAIL} |
| Issue Detection Precision | {value}% | ≥85% | {PASS/FAIL} |

---

## Edge Case Results

| Edge Case | Expected Behavior | Actual Behavior | Result |
|-----------|-------------------|----------------|--------|
| Empty Plan | Score <50, REQUIRES_IMPROVEMENT | {actual} | {PASS/FAIL} |
| Large Coordinator | Score ~88, borderline | {actual} | {PASS/FAIL} |
| Broken References | Score ~82, reference issues detected | {actual} | {PASS/FAIL} |
| Circular References | Score ~74, circular dependency detected | {actual} | {PASS/FAIL} |
| Mixed Architecture/Implementation | Score ~79, partial violation | {actual} | {PASS/FAIL} |

---

## Performance Benchmarks

| Plan Size | Files | Total Lines | Validation Time | Target | Status |
|-----------|-------|-------------|----------------|--------|--------|
| Small | 1 | 250 | {time}s | <15s | {PASS/FAIL} |
| Small | 3 | 400 | {time}s | <15s | {PASS/FAIL} |
| Medium | 5 | 1200 | {time}s | <30s | {PASS/FAIL} |
| Medium | 7 | 1800 | {time}s | <30s | {PASS/FAIL} |
| Large | 10 | 3000 | {time}s | <60s | {PASS/FAIL} |
| Large | 15 | 4500 | {time}s | <60s | {PASS/FAIL} |
| Very Large | 20 | 6500 | {time}s | <90s | {PASS/FAIL} |

**Average Validation Time**: {avg_time}s
**95th Percentile**: {p95_time}s

---

## Discrepancy Analysis

{if discrepancies exist}
### Significant Discrepancies (>5 points difference)

{for each discrepancy}
#### Plan: {plan_name}
**Manual Score**: {manual_score}/100
**Automated Score**: {automated_score}/100
**Difference**: {difference} points

**Root Cause**: {root_cause_explanation}
**Calibration Needed**: {yes/no}
{end for}
{else}
✅ No significant discrepancies found (all differences ≤5 points)
{endif}

---

## Calibration Log

{if calibration_performed}
### Calibration Iterations

{for each iteration}
#### Iteration {iteration_number}
**Changes Made**: {changes_description}
**Results**: Average Difference {before} → {after} points
**Assessment**: {assessment}
{end for}

**Final Calibration Result**: {PASS/FAIL}
{else}
✅ No calibration required (initial accuracy met targets)
{endif}

---

## Conclusion

**Test Validation Result**: {PASS if all metrics meet targets, FAIL otherwise}

**Key Findings**:
- {finding_1}
- {finding_2}
- {finding_3}

**Recommendations**:
{if PASS}
✅ plan-readiness-validator is production-ready
- Scoring accuracy meets ≥95% agreement target
- Performance meets <60 second target for typical plans
- All edge cases handled correctly
- Recommend proceeding to Phase 5 (Documentation and Integration)
{else}
⚠️ plan-readiness-validator requires further development
- {specific_issues_to_address}
- Recommend addressing issues before Phase 5
{endif}

---

**Test Validator**: {tester_name}
**Date**: {timestamp}
**Next Review**: After calibration adjustments (if applicable)
```

---

## Appendix: Manual Baseline Scoring Configuration

**File**: `.cursor/agents/plan-readiness-validator/test-samples/manual-baseline-scores.json`

**Purpose**: Store manual review scores for reproducible test execution

**Format**:
```json
{
  "test_samples": [
    {
      "plan_name": "actions-block-refactoring-workplan.md",
      "plan_path": "Docs/plans/actions-block-refactoring-workplan.md",
      "category": "READY",
      "manual_score": 93,
      "manual_breakdown": {
        "task_specificity": 28,
        "technical_completeness": 30,
        "execution_clarity": 18,
        "structure_compliance": 17
      },
      "manual_status": "READY",
      "reviewer_1": 94,
      "reviewer_2": 92,
      "notes": "Excellent integration steps, well-structured"
    },
    {
      "plan_name": "synthetic-fail-monolithic.md",
      "plan_path": ".cursor/agents/plan-readiness-validator/test-samples/synthetic-fail-monolithic.md",
      "category": "REQUIRES_IMPROVEMENT",
      "manual_score": 68,
      "manual_breakdown": {
        "task_specificity": 20,
        "technical_completeness": 22,
        "execution_clarity": 12,
        "structure_compliance": 14
      },
      "manual_status": "REQUIRES_IMPROVEMENT",
      "reviewer_1": 70,
      "reviewer_2": 66,
      "notes": "File size violation (650 lines), poor decomposition"
    }
  ]
}
```

---

**Document Status**: SPECIFICATION COMPLETE
**Next Action**: Execute test validation using this specification
**Owner**: Development Team
**Last Updated**: 2025-10-15
