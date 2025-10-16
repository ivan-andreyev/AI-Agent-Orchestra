# Phase 1: Agent Specification and Design

**Phase Duration**: 6-8 hours
**Phase Status**: PENDING
**Dependencies**: None (Foundation phase)

## Overview

This phase establishes the foundational specification for the plan-readiness-validator agent. The agent will validate work plans for LLM execution readiness using a ≥90% threshold scoring system. This phase creates three critical specification files: agent.md (metadata and responsibilities), prompt.md (validation workflow), and scoring-algorithm.md (scoring rubric).

**Phase Objectives**:
- Define agent metadata, purpose, and scope
- Design comprehensive validation workflow
- Create objective, reproducible scoring algorithm
- Establish integration points with agent transition matrix

**Phase Deliverables**:
- `.cursor/agents/plan-readiness-validator/agent.md` - Agent specification with frontmatter
- `.cursor/agents/plan-readiness-validator/prompt.md` - Prompt template with validation workflow
- `.cursor/agents/plan-readiness-validator/scoring-algorithm.md` - Detailed scoring rubric

## Task 1.1: Agent Specification File Creation

**File**: `.cursor/agents/plan-readiness-validator/agent.md`

### Objectives

Create comprehensive agent specification following the universal agent template. This document defines the agent's purpose, responsibilities, and integration points within the broader agent ecosystem.

### Deliverables

#### 1.1.1 Frontmatter Specification

**Required Metadata**:
```yaml
---
name: "plan-readiness-validator"
description: "Validates work plan readiness for LLM execution with ≥90% threshold"
type: "validation"
category: "pre-execution"
priority: "P0"
version: "1.0.0"
tools:
  - "Read"
  - "Bash"
  - "Glob"
  - "Grep"
max_iterations: 1
timeout: "60s"
---
```

**Key Metadata Decisions**:
- **max_iterations: 1** - Validation is single-pass, no iterative fixing
- **timeout: 60s** - Performance target for typical plans (5-15 files)
- **type: validation** - Distinguishes from review/fixing agents
- **priority: P0** - Critical for MVP completion

#### 1.1.2 Agent Purpose and Responsibilities

**Primary Purpose**:
Automated validation of work plans to ensure LLM execution readiness before task execution begins. Acts as quality gate between plan creation (work-plan-architect) and plan execution (plan-task-executor).

**Core Responsibilities**:
1. **LLM Readiness Scoring**
   - Calculate objective readiness score (0-100 scale)
   - Apply ≥90% pass/fail threshold
   - Generate detailed score breakdown
   - Identify specific improvement areas

2. **Plan Structure Validation**
   - Validate against common-plan-generator.mdc rules
   - Check catalogization compliance (GOLDEN RULES #1, #2)
   - Verify file size limits (≤400 lines)
   - Validate parent/child reference integrity
   - Ensure proper coordinator placement

3. **Execution Readiness Assessment**
   - Verify technical task decomposition (Entity, Service, API patterns)
   - Validate execution complexity (≤30 tool calls per task)
   - Check integration completeness (DbContext, DI, migrations)
   - Ensure "Plan ≠ Realization" principle adherence

4. **Agent Transition Recommendations**
   - Output CRITICAL next agent recommendations
   - Identify RECOMMENDED parallel execution opportunities
   - Flag OPTIONAL improvements
   - Support conditional agent invocation

**Out of Scope**:
- Fixing plan issues (use work-plan-architect for revisions)
- Domain logic validation (requires manual review)
- Business requirement validation (requires stakeholder input)

#### 1.1.3 Input/Output Specifications

**Input Parameters**:
- `plan_file` (required): Path to main plan file (coordinator)
- `plan_directory` (optional): Directory containing phase files, defaults to same directory as plan_file
- `score_threshold` (optional): Pass/fail threshold, defaults to 90%
- `verbose` (optional): Include detailed scoring breakdown, defaults to true

**Output Format**:
```markdown
# Plan Readiness Validation Report

**Plan**: {plan_file}
**Validation Date**: {timestamp}
**LLM Readiness Score**: {score}/100
**Status**: READY | REQUIRES_IMPROVEMENT

## Score Breakdown
- Task Specificity: {score}/30
- Technical Completeness: {score}/30
- Execution Clarity: {score}/20
- Structure Compliance: {score}/20

## Issues Found ({count})
[Detailed list with file paths and line numbers]

## Recommendations
### CRITICAL
- {agent_name}: {reason}

### RECOMMENDED
- {agent_name}: {reason}

### OPTIONAL
- {agent_name}: {reason}
```

**Status Definitions**:
- **READY**: Score ≥90%, plan can proceed to execution
- **REQUIRES_IMPROVEMENT**: Score <90%, plan needs revision via work-plan-architect

#### 1.1.4 Success Criteria and Failure Modes

**Success Criteria**:
- [ ] Validation completes within 60 seconds (performance target)
- [ ] Score calculation is reproducible and transparent
- [ ] All GOLDEN RULE violations detected (100% recall)
- [ ] Technical completeness issues identified with ≥90% accuracy
- [ ] Execution complexity estimation within ±5 tool calls
- [ ] Agent transition recommendations align with agent transition matrix

**Failure Modes and Handling**:

1. **File Access Errors**
   - Scenario: Plan file not found or inaccessible
   - Handling: Return error status, suggest file path correction
   - Example: "ERROR: Plan file not found at {path}. Verify file exists and path is correct."

2. **Malformed Plan Structure**
   - Scenario: Missing required sections, broken references
   - Handling: Flag as structure violation, score 0 on structure compliance
   - Example: "CRITICAL: Missing required section '## Work Breakdown Structure'"

3. **Timeout Exceeded**
   - Scenario: Validation exceeds 60-second timeout
   - Handling: Return partial results, flag timeout in report
   - Example: "WARNING: Validation timeout exceeded. Partial results provided."

4. **Invalid Input Parameters**
   - Scenario: Invalid score_threshold or missing plan_file
   - Handling: Return parameter validation error
   - Example: "ERROR: score_threshold must be between 0 and 100"

#### 1.1.5 Integration Points with Other Agents

**Upstream Agents (Input from)**:
- **work-plan-architect**: Primary source of plans to validate
- **work-plan-reviewer**: May trigger validation after manual review

**Downstream Agents (Output to)**:
- **plan-task-executor**: Receives READY plans for execution
- **work-plan-architect**: Receives REQUIRES_IMPROVEMENT plans for revision
- **architecture-documenter**: Triggered for plans with architectural components
- **parallel-plan-optimizer**: Triggered for plans with >5 tasks

**Agent Transition Matrix Integration**:
```
CRITICAL Paths:
- work-plan-architect → plan-readiness-validator → plan-task-executor (if READY)
- work-plan-architect → plan-readiness-validator → work-plan-architect (if REQUIRES_IMPROVEMENT, max 3 iterations)
- plan-readiness-validator → architecture-documenter (if architectural changes detected)

RECOMMENDED Paths:
- plan-readiness-validator → parallel-plan-optimizer (if >5 tasks detected)

OPTIONAL Paths:
- plan-readiness-validator → work-plan-reviewer (for manual validation alongside automated)
```

#### 1.1.6 Limitations and Edge Cases

**Known Limitations**:
1. **Business Logic Validation**: Cannot validate domain-specific correctness or business requirement alignment
2. **Heuristic Complexity Estimation**: Tool call estimation is approximate, not exact execution count
3. **Context-Dependent Scoring**: Cannot assess stakeholder-specific quality preferences
4. **Language Limitations**: English-only validation (Russian content not analyzed)

**Edge Cases**:

1. **Empty Plans (Coordinator-Only)**
   - Detection: No task sections (### X.Y) found
   - Handling: Flag as REQUIRES_IMPROVEMENT, score 0 on execution clarity
   - Recommendation: work-plan-architect to add task decomposition

2. **Plans Exceeding 400 Lines But Properly Decomposed**
   - Detection: Main file >400 lines but has child phase files
   - Handling: Structure violation flagged but may still pass if child files ≤400 lines
   - Recommendation: Move content from coordinator to phase files

3. **Plans with Broken Parent/Child References**
   - Detection: Links to non-existent files or circular references
   - Handling: Structure compliance score deduction, detailed error messages
   - Recommendation: work-plan-architect to fix reference integrity

4. **Plans with Extreme Complexity (>50 tool calls per task)**
   - Detection: Heuristic estimation exceeds 50 tool calls
   - Handling: Flag as execution complexity violation, score deduction
   - Recommendation: work-plan-architect to further decompose tasks

5. **Plans with Mixed Architecture and Implementation**
   - Detection: Full implementation code in plan (violates "Plan ≠ Realization")
   - Handling: Flag as execution clarity violation, score deduction
   - Recommendation: Remove implementation details, keep architecture only

### Acceptance Criteria

- [ ] Frontmatter follows universal agent template format
- [ ] Agent metadata complete and accurate (name, description, tools, max_iterations)
- [ ] Purpose and responsibilities clearly delineated
- [ ] Validation scope explicitly defined (no fixing, only assessment)
- [ ] Input/output specifications comprehensive and unambiguous
- [ ] Integration with agent transition matrix documented
- [ ] Success criteria measurable and objective
- [ ] Failure modes identified with handling strategies
- [ ] Limitations and edge cases explicitly documented
- [ ] File validates against YAML schema (if applicable)

### Technical Notes

**Tool Usage**:
- **Read**: Access plan files, rule files (common-plan-generator.mdc, common-plan-reviewer.mdc)
- **Glob**: Discover phase files in plan directory
- **Grep**: Search for pattern violations (e.g., implementation code snippets)
- **Bash**: Execute file size checks, reference validation

**Performance Considerations**:
- Cache rule file contents (read once per validation session)
- Use Glob to batch-discover files before reading
- Limit Grep searches to known violation patterns
- Optimize for typical plan size (5-15 files, 200-300 lines each)

---

## Task 1.2: Prompt Template Development

**File**: `.cursor/agents/plan-readiness-validator/prompt.md`

### Objectives

Create comprehensive prompt template that guides the LLM through the validation workflow. This template defines the structured validation process, scoring algorithm application, and report generation.

### Deliverables

#### 1.2.1 Structured Validation Workflow

**Workflow Overview**:
The validation workflow consists of 5 sequential steps, each building upon the previous step's results. The workflow ensures comprehensive plan assessment across structure, technical quality, and execution readiness.

**Step 1: Load Validation Rules from Rule Files**

**Objective**: Load and parse validation standards from project rule files.

**Actions**:
1. Read `.cursor/rules/common-plan-generator.mdc`
   - Extract plan structure requirements
   - Identify mandatory sections (Executive Summary, Work Breakdown, Acceptance Criteria)
   - Load task decomposition patterns (Entity, Service, API templates)
   - Parse catalogization rules (GOLDEN RULES #1, #2)

2. Read `.cursor/rules/common-plan-reviewer.mdc`
   - Extract quality standards and review criteria
   - Load LLM readiness indicators
   - Parse integration completeness checklist
   - Identify common violation patterns

3. Read `.cursor/rules/catalogization-rules.mdc`
   - Load file structure rules (coordinator placement, naming conventions)
   - Extract file size limits (≤400 lines)
   - Parse parent/child reference standards
   - Load directory structure requirements

**Output**: Validation ruleset loaded into memory for subsequent steps

**Step 2: Perform LLM Readiness Scoring**

**Objective**: Calculate objective readiness score across 4 dimensions.

**Scoring Dimensions**:

1. **Task Specificity Score (0-30 points)**
   - Concrete file paths: 10 points
     - Check for explicit file paths in task descriptions
     - Example: "Create `src/Orchestra.Core/Validators/PlanValidator.cs`" (good)
     - Example: "Create validator class" (bad, no path)
   - Specific class/interface names: 10 points
     - Check for explicit type names
     - Example: "Implement IPlanReadinessValidator interface" (good)
     - Example: "Implement validator interface" (bad, no specific name)
   - Clear acceptance criteria: 10 points
     - Check for measurable, testable criteria per task
     - Example: "Score calculation reproducible within ±1 point" (good)
     - Example: "Should work correctly" (bad, vague)

2. **Technical Completeness Score (0-30 points)**
   - Integration steps (DbContext, DI): 15 points
     - Entity tasks include DbContext.DbSet addition: 5 points
     - Service tasks include DI registration in Program.cs: 5 points
     - API tasks include middleware configuration: 5 points
   - Migration workflow: 10 points
     - Entity tasks include `dotnet ef migrations add` command: 5 points
     - Entity tasks include `dotnet ef database update` command: 5 points
   - Error handling patterns: 5 points
     - Tasks include try-catch or validation logic: 5 points

3. **Execution Clarity Score (0-20 points)**
   - Step-by-step decomposition: 10 points
     - Tasks broken into numbered subtasks: 5 points
     - Dependencies explicitly listed: 5 points
   - Dependencies clearly identified: 10 points
     - "Depends on: Task X.Y" notation present: 5 points
     - Sequential vs. parallel execution clarified: 5 points

4. **Structure Compliance Score (0-20 points)**
   - Catalogization rules: 10 points
     - Coordinator outside directory: 3 points
     - File naming follows XX-Name.md: 3 points
     - Parent/child references valid: 4 points
   - File size limits: 5 points
     - All files ≤400 lines: 5 points
     - Deduct 1 point per file exceeding limit (min 0)
   - Reference integrity: 5 points
     - No broken links: 3 points
     - All referenced files exist: 2 points

**Scoring Calculation**:
- Sum scores from all 4 dimensions
- Total Score = Task Specificity + Technical Completeness + Execution Clarity + Structure Compliance
- Maximum Score: 100 points
- Pass Threshold: ≥90 points

**Output**: Detailed score breakdown with justification for each dimension

**Step 3: Validate Plan Structure Against Catalogization Rules**

**Objective**: Ensure plan adheres to file structure and naming conventions.

**Validation Checks**:

1. **Coordinator Placement (GOLDEN RULE #1)**
   - Check: Coordinator file located outside its content directory
   - Example: `Docs/plans/Plan-Readiness-Validator-Implementation-Plan.md` (coordinator)
   - Example: `Docs/plans/plan-readiness-validator/phase-1-foundation.md` (child)
   - Violation: Coordinator inside directory (e.g., `Docs/plans/plan-readiness-validator/Plan-Readiness-Validator-Implementation-Plan.md`)

2. **File Naming Convention**
   - Check: Phase files follow XX-Name.md format
   - Example: `phase-1-foundation.md` (correct)
   - Example: `foundation.md` (incorrect, no phase prefix)
   - Violation: Inconsistent naming or missing phase numbering

3. **File Size Limits**
   - Check: All files ≤400 lines
   - Method: Use Bash to count lines per file
   - Violation: Any file exceeding 400 lines
   - Exception: Test validation documents may exceed 400 lines if justified

4. **Parent/Child Reference Integrity**
   - Check: Coordinator links to all phase files
   - Check: Phase files link back to coordinator
   - Method: Parse markdown links, verify file existence
   - Violation: Broken links, circular references, missing cross-references

**Output**: List of structure violations with file paths and line numbers

**Step 4: Assess Execution Readiness**

**Objective**: Validate technical task quality and execution complexity.

**Assessment Criteria**:

1. **Technical Task Decomposition**
   - Entity Implementation Pattern:
     - [ ] Entity class creation
     - [ ] DbContext.DbSet<T> addition
     - [ ] Entity configuration in OnModelCreating
     - [ ] Database migration creation (`dotnet ef migrations add`)
     - [ ] Database update (`dotnet ef database update`)

   - Service Layer Pattern:
     - [ ] Interface definition (I{ServiceName})
     - [ ] Implementation class ({ServiceName})
     - [ ] DI registration in Program.cs (builder.Services.AddScoped<I, Impl>)
     - [ ] Dependencies injection in constructor

   - API Controller Pattern:
     - [ ] Controller class creation
     - [ ] Action methods with HTTP verbs
     - [ ] Request/Response models
     - [ ] Authorization/Authentication attributes
     - [ ] Middleware configuration (if needed)

2. **Execution Complexity Estimation**
   - Parse task sections (### X.Y)
   - Count TODO items, code blocks, technical operations per task
   - Estimate tool calls using heuristics:
     - Each file creation: 1 tool call (Write)
     - Each file edit: 2 tool calls (Read + Edit)
     - Each command execution: 1 tool call (Bash)
     - Each search operation: 1 tool call (Grep or Glob)
   - Flag tasks exceeding 30 tool calls
   - Recommendation: Decompose flagged tasks into subtasks

3. **"Plan ≠ Realization" Principle Validation**
   - Good Indicators (Architecture):
     - TODO markers: "TODO: Implement validation logic"
     - High-level descriptions: "Validation service checks plan structure"
     - Interface definitions: "interface IPlanValidator { ... }"
   - Bad Indicators (Implementation):
     - Full implementation code: "public class PlanValidator { /* 50 lines of code */ }"
     - Detailed algorithms: "Loop through each task, calculate score..."
     - Specific values: "const int THRESHOLD = 90;"
   - Violation: Plan contains implementation-level details
   - Recommendation: Remove implementation code, keep architecture only

**Output**: Execution readiness assessment with flagged issues

**Step 5: Generate Recommendations for Next Agents**

**Objective**: Determine appropriate next agents based on plan characteristics and validation results.

**Recommendation Logic**:

1. **If Score <90% (REQUIRES_IMPROVEMENT)**:
   - CRITICAL: work-plan-architect
   - Reason: "Plan requires revision to address {issue_count} issues before execution"
   - Parameters: plan_file, issues_list

2. **If Architectural Components Detected**:
   - Detection: Keywords in plan (Entity, DbContext, Service, API Controller, Architecture)
   - CRITICAL: architecture-documenter
   - Reason: "Plan contains {component_count} architectural components requiring documentation"
   - Parameters: plan_file, type="planned"

3. **If >5 Tasks Detected**:
   - Detection: Count task sections (### X.Y)
   - RECOMMENDED: parallel-plan-optimizer
   - Reason: "Plan has {task_count} tasks - parallel execution could reduce time by 40-50%"
   - Parameters: plan_file

4. **If Score ≥90% (READY)**:
   - Next: plan-task-executor
   - Reason: "Plan meets LLM readiness threshold (≥90%), ready for execution"
   - Parameters: plan_file

**Output Format**:
```markdown
## Recommended Next Actions

### CRITICAL
1. {agent_name}
   Reason: {reason}
   Command: Use Task tool with subagent_type: "{agent_name}"
   Parameters: {parameters}

### RECOMMENDED
1. {agent_name}
   Reason: {reason}
   Command: Use Task tool with subagent_type: "{agent_name}"
   Parameters: {parameters}

### OPTIONAL
1. {agent_name}
   Reason: {reason}
   Command: Use Task tool with subagent_type: "{agent_name}"
```

#### 1.2.2 LLM Readiness Scoring Algorithm Specification

**Algorithm Overview**: The LLM readiness score is calculated as a weighted sum of 4 dimensions, each contributing to the overall readiness assessment. The algorithm is designed to be objective, reproducible, and aligned with common-plan-reviewer.mdc standards.

**Scoring Formula**:
```
Total Score = Task Specificity Score + Technical Completeness Score + Execution Clarity Score + Structure Compliance Score
```

**Dimension Weights**:
- Task Specificity: 30% (30 points max)
- Technical Completeness: 30% (30 points max)
- Execution Clarity: 20% (20 points max)
- Structure Compliance: 20% (20 points max)

**Pass/Fail Threshold**:
- READY: Total Score ≥ 90 points
- REQUIRES_IMPROVEMENT: Total Score < 90 points

**Detailed Scoring Criteria**: (See Step 2 above for full breakdown)

#### 1.2.3 Output Format Templates

**Template 1: READY Status Report**

```markdown
# Plan Readiness Validation Report

**Plan**: {plan_file}
**Validation Date**: {timestamp}
**LLM Readiness Score**: {score}/100 (≥90% threshold)
**Status**: ✅ READY

## Score Breakdown
- Task Specificity: {score}/30 ✅
- Technical Completeness: {score}/30 ✅
- Execution Clarity: {score}/20 ✅
- Structure Compliance: {score}/20 ✅

## Validation Summary
Plan meets LLM readiness threshold and is approved for execution. All validation checks passed.

## Recommended Next Actions

### Next: plan-task-executor
Reason: Plan ready for automated task execution
Command: Use Task tool with subagent_type: "plan-task-executor"
Parameters: plan_file="{plan_file}"

### RECOMMENDED: parallel-plan-optimizer (if applicable)
Reason: Plan has {task_count} tasks - parallel execution could reduce time by 40-50%
Command: Use Task tool with subagent_type: "parallel-plan-optimizer"
```

**Template 2: REQUIRES_IMPROVEMENT Report**

```markdown
# Plan Readiness Validation Report

**Plan**: {plan_file}
**Validation Date**: {timestamp}
**LLM Readiness Score**: {score}/100 (<90% threshold)
**Status**: ⚠️ REQUIRES_IMPROVEMENT

## Score Breakdown
- Task Specificity: {score}/30 {status_icon}
- Technical Completeness: {score}/30 {status_icon}
- Execution Clarity: {score}/20 {status_icon}
- Structure Compliance: {score}/20 {status_icon}

## Issues Found ({issue_count})

### CRITICAL Issues ({critical_count})
1. **{issue_type}** - {file_path}:L{line_number}
   - Description: {issue_description}
   - Impact: {impact_on_score}
   - Recommendation: {fix_recommendation}

### IMPORTANT Issues ({important_count})
[Similar format]

### RECOMMENDATIONS ({recommendation_count})
[Similar format]

## Recommended Next Actions

### CRITICAL: work-plan-architect
Reason: Plan requires revision to address {issue_count} issues before execution
Command: Use Task tool with subagent_type: "work-plan-architect"
Parameters: plan_file="{plan_file}", issues_list=[see above]

### RECOMMENDED: {other_agents} (if applicable)
[Similar format]
```

#### 1.2.4 Example Validations

**Example 1: High-Quality Plan (Score: 95/100, READY)**

**Plan Characteristics**:
- Clear file structure with coordinator and 5 phase files
- All files ≤400 lines
- Specific file paths and class names in all tasks
- Complete integration steps (DbContext, DI, migrations)
- Execution complexity ≤30 tool calls per task
- No implementation code (architecture only)

**Validation Result**:
- Task Specificity: 28/30 (minor: 1 task missing specific file path)
- Technical Completeness: 30/30 (all integration steps present)
- Execution Clarity: 19/20 (minor: 1 dependency not explicitly listed)
- Structure Compliance: 18/20 (1 broken reference fixed)
- **Total: 95/100 - READY**

**Example 2: Problematic Plan (Score: 72/100, REQUIRES_IMPROVEMENT)**

**Plan Characteristics**:
- Monolithic structure (main file 650 lines, no phase decomposition)
- Vague task descriptions ("Implement validator", "Add tests")
- Missing integration steps (no DbContext additions for Entity tasks)
- High execution complexity (estimated 45 tool calls per task)
- Contains full implementation code (violates "Plan ≠ Realization")

**Validation Result**:
- Task Specificity: 15/30 (many tasks lack specific file paths and names)
- Technical Completeness: 18/30 (missing DbContext, DI registrations)
- Execution Clarity: 12/20 (high complexity, unclear dependencies)
- Structure Compliance: 7/20 (file size violation, no decomposition)
- **Total: 72/100 - REQUIRES_IMPROVEMENT**

**Issues Found**:
1. CRITICAL: File size violation - main file 650 lines (62.5% over limit)
2. CRITICAL: Missing DbContext integration for 3 Entity tasks
3. IMPORTANT: High execution complexity in tasks 2.1, 2.3, 3.1 (>40 tool calls)
4. IMPORTANT: Implementation code in tasks 2.2, 3.2 (violates "Plan ≠ Realization")
5. RECOMMENDATION: Add specific file paths for tasks 1.1, 1.2, 2.3

### Acceptance Criteria

- [ ] Validation workflow structured in 5 clear steps
- [ ] Each step has specific actions and output format
- [ ] Scoring algorithm mathematically sound and reproducible
- [ ] Validation steps align with common-plan-reviewer.mdc standards
- [ ] Output format templates comprehensive (READY and REQUIRES_IMPROVEMENT)
- [ ] Example validations demonstrate scoring consistency
- [ ] Agent transition recommendations follow agent transition matrix
- [ ] Prompt template uses available tools effectively (Read, Glob, Grep, Bash)

### Technical Notes

**Prompt Template Structure**:
The prompt template follows a structured methodology that guides the LLM through validation without ambiguity. Each step builds upon the previous step's output, ensuring comprehensive assessment.

**Tool Usage in Prompt**:
- Step 1: Read tool for rule files
- Step 2: Grep tool for pattern detection (file paths, class names)
- Step 3: Bash tool for file size checks, Glob for file discovery
- Step 4: Grep tool for code pattern detection ("Plan ≠ Realization")
- Step 5: Logic-based recommendations (no tools)

---

## Task 1.3: LLM Readiness Scoring Algorithm Design

**File**: `.cursor/agents/plan-readiness-validator/scoring-algorithm.md`

### Objectives

Define detailed scoring rubric with objective criteria and examples. This document serves as the canonical reference for score calculation, ensuring consistency and reproducibility across validation sessions.

### Deliverables

#### 1.3.1 Detailed Scoring Rubric with Examples

**Rubric Structure**: Each scoring dimension is broken down into measurable criteria with clear examples demonstrating full credit, partial credit, and no credit scenarios.

**Task Specificity Score (0-30 points)**

**Sub-dimension 1: Concrete File Paths (0-10 points)**

**Criteria**:
- 10 points: All tasks include explicit, absolute or project-relative file paths
- 7 points: Most tasks (≥80%) include file paths, some are vague
- 5 points: Some tasks (50-80%) include file paths
- 2 points: Few tasks (<50%) include file paths
- 0 points: No file paths or all paths are vague placeholders

**Examples**:

*Full Credit (10 points)*:
```markdown
### Task 2.1: Implement PlanValidator Service
Create validation service at `src/Orchestra.Core/Validators/PlanValidator.cs`
Implement interface at `src/Orchestra.Core/Interfaces/IPlanValidator.cs`
Register in DI at `src/Orchestra.API/Program.cs`
```

*Partial Credit (5 points)*:
```markdown
### Task 2.1: Implement PlanValidator Service
Create validation service in Core project
Implement interface in Interfaces directory
Register in DI
```

*No Credit (0 points)*:
```markdown
### Task 2.1: Implement PlanValidator Service
Create validation service
Implement interface
Register in DI
```

**Sub-dimension 2: Specific Class/Interface Names (0-10 points)**

**Criteria**:
- 10 points: All types explicitly named with full namespaces
- 7 points: Most types (≥80%) named, some are generic
- 5 points: Some types (50-80%) named
- 2 points: Few types (<50%) named
- 0 points: No specific type names, all generic references

**Examples**:

*Full Credit (10 points)*:
```markdown
Implement `Orchestra.Core.Validators.PlanValidator : IPlanValidator`
Create `Orchestra.Core.Models.ValidationResult` class
Use `Orchestra.Core.Services.IFileService` for file access
```

*Partial Credit (5 points)*:
```markdown
Implement PlanValidator class implementing IPlanValidator
Create ValidationResult model
Use IFileService for file access
```

*No Credit (0 points)*:
```markdown
Implement validator class
Create result model
Use file service
```

**Sub-dimension 3: Clear Acceptance Criteria (0-10 points)**

**Criteria**:
- 10 points: All tasks have measurable, testable acceptance criteria
- 7 points: Most tasks (≥80%) have clear criteria
- 5 points: Some tasks (50-80%) have clear criteria
- 2 points: Few tasks (<50%) have criteria
- 0 points: No acceptance criteria or all are vague

**Examples**:

*Full Credit (10 points)*:
```markdown
**Acceptance Criteria**:
- [ ] Score calculation reproducible within ±1 point across runs
- [ ] Validation completes in <60 seconds for plans with ≤15 files
- [ ] All GOLDEN RULE violations detected (100% recall)
- [ ] False positive rate <10%
```

*Partial Credit (5 points)*:
```markdown
**Acceptance Criteria**:
- Score calculation is consistent
- Validation is fast
- Violations are detected
```

*No Credit (0 points)*:
```markdown
**Acceptance Criteria**:
- Works correctly
- Performs well
```

**(Continue to next file due to length...)**

### Acceptance Criteria

- [ ] Each scoring dimension has clear, objective criteria
- [ ] Examples demonstrate full credit, partial credit, and no credit scenarios
- [ ] Scoring methodology is reproducible (two reviewers should get same score ±2 points)
- [ ] 90% threshold justified with rationale
- [ ] Score interpretation guide provides actionable feedback
- [ ] Rubric aligned with common-plan-reviewer.mdc standards

### Technical Notes

**Scoring Methodology**:
The scoring algorithm prioritizes objective, pattern-based criteria that can be automatically detected using Grep and Read tools. Subjective criteria (e.g., "quality of writing") are avoided in favor of measurable indicators.

**Threshold Justification (90%)**:
- Below 90%: Significant risk of execution failures, manual intervention recommended
- 90-95%: High-quality plans, minor improvements may be needed
- 95-100%: Exceptional plans, ready for immediate execution

---

## Phase 1 Completion Criteria

### All Tasks Complete When:

- [ ] agent.md exists with complete frontmatter specification
- [ ] agent.md documents purpose, responsibilities, integration points
- [ ] agent.md includes input/output specifications
- [ ] agent.md identifies limitations and edge cases
- [ ] prompt.md exists with 5-step validation workflow
- [ ] prompt.md includes LLM readiness scoring algorithm
- [ ] prompt.md defines output format templates (READY, REQUIRES_IMPROVEMENT)
- [ ] prompt.md includes example validations (good and bad plans)
- [ ] scoring-algorithm.md exists with detailed rubric
- [ ] scoring-algorithm.md includes examples for each scoring dimension
- [ ] scoring-algorithm.md justifies 90% threshold
- [ ] All files reviewed for consistency and completeness

### Quality Gates:

- [ ] Agent specification follows universal agent template
- [ ] Validation workflow aligned with common-plan-reviewer.mdc
- [ ] Scoring algorithm mathematically sound and reproducible
- [ ] Examples demonstrate scoring consistency
- [ ] Integration points with agent transition matrix documented

### Next Phase:

After Phase 1 completion, proceed to **Phase 2: Core Validation Logic Implementation**. See `phase-2-validation-logic.md` for detailed tasks.

---

**Phase Status**: PENDING
**Last Updated**: 2025-10-14
**Next Review**: After Task 1.3 completion
