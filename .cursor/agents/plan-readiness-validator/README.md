# Plan Readiness Validator - Usage Guide

**Version**: 1.0
**Last Updated**: 2025-10-15
**Purpose**: Comprehensive usage guide for the plan-readiness-validator agent

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [Core Features](#core-features)
3. [Usage Guide](#usage-guide)
4. [Validation Workflow](#validation-workflow)
5. [Scoring System](#scoring-system)
6. [Output Interpretation](#output-interpretation)
7. [Troubleshooting](#troubleshooting)
8. [Integration Points](#integration-points)
9. [Related Documentation](#related-documentation)

---

## Quick Start

**Time to Onboard**: 2-3 minutes

### Prerequisites

- Work plan file created (`.md` format)
- Plan follows basic structure from `common-plan-generator.mdc`
- Validation rule files accessible (`.cursor/rules/*.mdc`)

### Basic Usage

```markdown
# Invoke validator via Task tool
subagent_type: "plan-readiness-validator"
parameters:
  plan_file: "C:\path\to\your-work-plan.md"
```

### Expected Output

**If READY (score ≥90%)**:
```markdown
✅ READY - LLM Readiness Score: 93/100

Next Actions:
- plan-task-executor: Begin execution
- architecture-documenter: Document 3 new components
```

**If REQUIRES_IMPROVEMENT (score <90%)**:
```markdown
❌ REQUIRES_IMPROVEMENT - LLM Readiness Score: 76/100

Critical Issues:
- Task 2.1 missing DbContext integration
- Task 3.1 complexity exceeds 30 tool calls

Next Actions:
- work-plan-architect: Fix issues and re-validate
```

---

## Core Features

### 1. LLM Readiness Scoring

Generates a comprehensive readiness score (0-100 scale) based on four dimensions:

**Scoring Dimensions**:
- **Task Specificity (30%)**: Concrete file paths, class names, acceptance criteria
- **Technical Completeness (30%)**: Integration steps (DbContext, DI, migrations)
- **Execution Clarity (20%)**: Step-by-step decomposition, clear dependencies
- **Structure Compliance (20%)**: Catalogization rules, file size limits, reference integrity

**Pass Threshold**: ≥90 points = READY, <90 points = REQUIRES_IMPROVEMENT

**Why This Matters**:
- **90%+ scores** → Plan is production-ready for LLM execution with minimal ambiguity
- **80-89% scores** → Borderline quality, address major issues before execution
- **<80% scores** → Significant revisions needed to avoid execution failures

### 2. Plan Structure Validation

Validates compliance with catalogization and structural rules:

**GOLDEN RULES Validation**:
- **RULE #1**: Directory name matches file name (without .md)
  - ✅ Valid: `01-Data-Models.md` → `01-Data-Models/`
  - ❌ Invalid: `01-Data-Models.md` → `01-architecture/`
- **RULE #2**: Coordinator files placed outside their directories
  - ✅ Valid: `plan.md` (outside) + `plan/` (directory with children)
  - ❌ Invalid: `plan/plan.md` (coordinator inside directory)

**File Size Limits**:
- ≤400 lines per file (optimal for LLM context)
- 251-400 lines = WARNING (monitor for decomposition)
- >400 lines = VIOLATION (decomposition required)

**Reference Integrity**:
- Parent → Child links valid and bidirectional
- No broken file paths
- No circular dependencies

### 3. Technical Completeness Assessment

Checks technical task decomposition completeness with pattern-based detection:

**Entity/Model Tasks** (ALL required):
- Entity class creation
- DbContext.DbSet registration
- Entity configuration (OnModelCreating)
- Database migration creation

**Service Layer Tasks** (ALL required):
- Interface definition
- Implementation class
- DI registration in Program.cs/Startup
- Dependencies resolution

**API Controller Tasks** (ALL required):
- Controller action methods
- Request/Response models and validation
- Authorization/Authentication setup
- Middleware and routing configuration

**Detection Method**: Keyword scanning for patterns like "DbContext", "DI", "migration", "interface", etc.

### 4. Execution Complexity Analysis

Estimates task complexity and validates execution feasibility:

**Tool Call Estimation**:
- Counts operations per task (### X.Y sections)
- Complexity Threshold: Flags tasks >30 tool calls for decomposition

**Heuristic Formula**:
```
Estimated Tool Calls =
  (TODO items × 2) +
  (code blocks × 3) +
  (file operations × 1) +
  (technical operations × 2) +
  (test scenarios × 3)
```

**"Plan ≠ Realization" Validation**:
- ✅ TODO markers are GOOD (architectural placeholders)
- ❌ Full implementation code is BAD (belongs in execution, not planning)

**Validation Criteria**:
- >20 TODOs → ✅ GOOD (architectural planning)
- <5 TODOs → ❌ BAD (likely full implementation)
- LINQ queries, loops, detailed error handling → ❌ BAD (implementation code)

### 5. Agent Transition Recommendations

Generates recommendations for next agents based on plan analysis:

**CRITICAL Paths** (always recommended):
- **work-plan-architect**: If score <90% (REQUIRES_IMPROVEMENT)
- **plan-task-executor**: If score ≥90% (READY for execution)
- **architecture-documenter**: If plan contains architectural components

**RECOMMENDED Paths** (conditional):
- **parallel-plan-optimizer**: If plan has >5 tasks (parallelization opportunities)

---

## Usage Guide

### Scenario 1: Standalone Validation

**When to Use**: After creating a work plan, before execution begins

**Process**:
1. Create work plan using work-plan-architect or manually
2. Invoke plan-readiness-validator with plan file path
3. Review validation report
4. If READY → proceed to execution
5. If REQUIRES_IMPROVEMENT → fix issues and re-validate

**Example**:
```markdown
# Step 1: Create plan (manual or via architect)
Plan created: Docs/plans/feature-auth-plan.md

# Step 2: Validate plan
subagent_type: "plan-readiness-validator"
parameters:
  plan_file: "Docs/plans/feature-auth-plan.md"

# Step 3: Review report
Status: ✅ READY
Score: 92/100

# Step 4: Proceed to execution
subagent_type: "plan-task-executor"
parameters:
  plan_file: "Docs/plans/feature-auth-plan.md"
```

### Scenario 2: Post-Architect Validation

**When to Use**: After work-plan-architect creates or revises a plan

**Process**:
1. work-plan-architect creates plan
2. Architect recommends plan-readiness-validator (automatic)
3. Validator runs immediately after architect completes
4. If READY → architect workflow complete
5. If REQUIRES_IMPROVEMENT → architect revises (max 3 iterations)

**Example**:
```markdown
# Step 1: Architect creates plan
subagent_type: "work-plan-architect"
prompt: "Create authentication feature plan with User entity, UserService, AuthController"

# Architect output includes recommendation:
🔄 Recommended: plan-readiness-validator
  Parameters: plan_file: "Docs/plans/authentication-feature.md"

# Step 2: Validator runs automatically
Status: ❌ REQUIRES_IMPROVEMENT
Score: 78/100
Issues:
- Task 2.1 missing DbContext integration
- Task 3.1 complexity exceeds 30 tool calls

# Step 3: Architect revises (iteration 1)
subagent_type: "work-plan-architect"
parameters:
  revision_mode: true
  plan_file: "Docs/plans/authentication-feature.md"
  issues_list: ["Task 2.1 missing DbContext integration", ...]

# Step 4: Validator re-runs (iteration 2)
Status: ✅ READY
Score: 91/100

# Workflow complete - proceed to execution
```

### Scenario 3: Blocking Validation

**When to Use**: As a quality gate before execution (CI/CD pipeline)

**Process**:
1. Plan created and committed to repository
2. CI/CD pipeline invokes plan-readiness-validator
3. If score <90% → pipeline fails, execution blocked
4. Developer fixes issues and re-commits
5. Validator re-runs, pipeline passes if score ≥90%

**Example (CI/CD Integration)**:
```yaml
# .github/workflows/plan-validation.yml
steps:
  - name: Validate Work Plan
    run: |
      # Invoke validator
      plan-readiness-validator --plan-file="Docs/plans/feature-plan.md" --threshold=90

      # Exit code 0 if READY (≥90%), exit code 1 if REQUIRES_IMPROVEMENT
    continue-on-error: false  # Block execution if validation fails
```

---

## Validation Workflow

**6-Step Process** (from `prompt.md`):

### STEP 1: Load Validation Rules and Context

**Objective**: Load all validation rules and understand plan context

**Actions**:
1. Read validation rule files (MANDATORY):
   - `.cursor/rules/common-plan-generator.mdc`
   - `.cursor/rules/common-plan-reviewer.mdc`
   - `.cursor/rules/catalogization-rules.mdc`
2. Identify plan scope (single file or directory structure)
3. Extract plan metadata (tasks count, architectural components, dependencies)

**Output**: Internal understanding of plan structure and validation rules

### STEP 2: LLM Readiness Scoring

**Objective**: Calculate comprehensive LLM readiness score (0-100 scale)

**Dimensions**:

#### Task Specificity (0-30 points)
- **Concrete File Paths (10 points)**: All tasks specify exact file paths
- **Specific Class/Interface Names (10 points)**: All technical tasks name specific classes/interfaces
- **Clear Acceptance Criteria (10 points)**: Every task has explicit completion criteria

#### Technical Completeness (0-30 points)
- **Integration Steps (15 points)**: Entity/Service/API tasks include ALL integration steps
- **Migration Workflow (10 points)**: Database changes include migration creation commands
- **Error Handling Patterns (5 points)**: Error handling patterns specified

#### Execution Clarity (0-20 points)
- **Step-by-Step Decomposition (10 points)**: Tasks decomposed into clear, sequential steps
- **Dependencies Clearly Identified (10 points)**: All task dependencies explicitly stated

#### Structure Compliance (0-20 points)
- **Catalogization Rules (10 points)**: GOLDEN RULES #1 and #2 followed
- **File Size Limits (5 points)**: All files ≤400 lines
- **Reference Integrity (5 points)**: All parent/child references valid and bidirectional

**Output**: Numerical score (0-100) and dimension breakdown

### STEP 3: Execution Complexity Analysis

**Objective**: Ensure tasks are not too complex for single execution (≤30 tool calls per task)

**Method**:
```
For each task (### X.Y section):
  1. Count TODO items and checklist boxes
  2. Count code blocks (each = 2-5 tool calls)
  3. Count file operations (create, edit, read)
  4. Count technical operations (DbContext, DI, migration, API call)
  5. Count test scenarios

  Estimate = (TODO items × 2) + (code blocks × 3) + (file ops × 1) +
             (tech ops × 2) + (tests × 3)

  If estimate >30 tool calls → Flag for decomposition
```

**Complexity Heuristics**:
- Simple task (entity class only): 5-10 tool calls ✅
- Medium task (service with DI): 15-20 tool calls ✅
- Complex task (API with auth): 25-30 tool calls ✅
- Over-complex task (full feature): 40-60 tool calls ❌ FLAG THIS

**Deduction Rules**:
- 1 task >30 tool calls → -5 points (Execution Clarity)
- 2-3 tasks >30 tool calls → -10 points
- 4+ tasks >30 tool calls → -15 points (CRITICAL issue)

### STEP 4: "Plan ≠ Realization" Validation

**Objective**: Ensure plan contains architecture, not full implementation

**What Plans SHOULD Contain** (✅ GOOD):
- Interface definitions with method signatures
- Class structures with public properties
- Constructor signatures with dependencies
- TODO markers indicating future implementation
- `throw new NotImplementedException()` placeholders

**What Plans SHOULD NOT Contain** (❌ BAD):
- Full method implementations with business logic
- LINQ queries, loops, conditionals inside methods
- Detailed error handling code
- Concrete string constants and magic numbers

**Validation Method**:
1. Count TODO markers in plan:
   - >20 TODOs → ✅ GOOD (architectural planning)
   - 5-20 TODOs → ⚠️ ACCEPTABLE
   - <5 TODOs → ❌ BAD (likely full implementation)
2. Check for implementation patterns (LINQ, loops, try-catch) → ❌ BAD
3. Check for architectural patterns (TODO comments, NotImplementedException) → ✅ GOOD

**Scoring Impact**:
- Plan with 20+ TODOs, architecture only → No deduction
- Plan with <5 TODOs, some implementation → -5 points (Task Specificity)
- Plan with full implementation → -10 points (Task Specificity), -5 points (Execution Clarity)

### STEP 5: Generate Agent Transition Recommendations

**Objective**: Determine next agents based on validation results and plan characteristics

**Decision Tree**:
```
IF score <90:
  CRITICAL: work-plan-architect (fix issues and re-validate)
  OUTPUT: "Status: REQUIRES_IMPROVEMENT"

ELSE IF score ≥90:
  RECOMMENDED: plan-task-executor (begin execution)

  IF plan contains architectural components (new classes, interfaces, services):
    CRITICAL: architecture-documenter (create planned architecture docs)

  IF plan has >5 tasks:
    RECOMMENDED: parallel-plan-optimizer (analyze parallelization opportunities)

  OUTPUT: "Status: READY"
```

**Architectural Component Detection**:
- Scan plan for indicators: "Create I{Name} interface", "Implement {Name}Service", "Create {Name} entity"
- If 1+ architectural components detected → Recommend architecture-documenter

**Task Count Detection**:
- Count tasks (### X.Y sections) in plan
- If count >5 → Recommend parallel-plan-optimizer

### STEP 6: Generate Validation Report

**Objective**: Produce comprehensive, actionable validation report

**Report Structure**:
```markdown
# Plan Readiness Validation Report

**Plan**: [plan-name]
**Date**: [YYYY-MM-DD]
**Status**: [✅ READY | ❌ REQUIRES_IMPROVEMENT]
**LLM Readiness Score**: [score]/100

---

## Score Breakdown

### Task Specificity: [score]/30
[Specific findings with examples]

### Technical Completeness: [score]/30
[Specific findings with examples]

### Execution Clarity: [score]/20
[Specific findings with examples]

### Structure Compliance: [score]/20
[Specific findings with examples]

---

## Validation Results

### ✅ Passed Checks
- [List of validations that passed]

### ❌ Failed Checks
- [List of validations that failed with file:line references]

---

## Recommendations

### CRITICAL Next Actions
- [Agent recommendations with rationale]

### RECOMMENDED Improvements
- [Optional agent recommendations]

---

## Detailed Issues

[Section-by-section breakdown of each issue found]
```

**Output**: Comprehensive validation report with actionable recommendations

---

### Validation Workflow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│ STEP 1: Load Validation Rules                                  │
│ - Read .cursor/rules/*.mdc files                               │
│ - Identify plan scope (file/directory)                         │
│ - Extract metadata (tasks, components)                         │
└─────────────────────┬───────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────────────┐
│ STEP 2: LLM Readiness Scoring (0-100)                          │
│ - Task Specificity (30%): File paths, class names, criteria    │
│ - Technical Completeness (30%): Integration steps, migrations  │
│ - Execution Clarity (20%): Decomposition, dependencies         │
│ - Structure Compliance (20%): Catalogization, file size        │
└─────────────────────┬───────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────────────┐
│ STEP 3: Execution Complexity Analysis                          │
│ - Estimate tool calls per task (heuristic)                     │
│ - Flag tasks >30 tool calls for decomposition                  │
│ - Deduct points from Execution Clarity (-5 to -15 points)      │
└─────────────────────┬───────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────────────┐
│ STEP 4: "Plan ≠ Realization" Validation                        │
│ - Count TODO markers (>20 GOOD, <5 BAD)                        │
│ - Detect implementation patterns (LINQ, loops → BAD)           │
│ - Detect architectural patterns (TODO, NotImplemented → GOOD)  │
│ - Deduct points if full implementation detected                │
└─────────────────────┬───────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────────────┐
│ STEP 5: Generate Agent Transition Recommendations              │
│ - IF score <90 → work-plan-architect (CRITICAL)                │
│ - IF score ≥90 → plan-task-executor (RECOMMENDED)              │
│ - IF arch components → architecture-documenter (CRITICAL)      │
│ - IF >5 tasks → parallel-plan-optimizer (RECOMMENDED)          │
└─────────────────────┬───────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────────────┐
│ STEP 6: Generate Validation Report                             │
│ - Comprehensive report with score breakdown                    │
│ - Passed/Failed checks with file:line references               │
│ - CRITICAL/RECOMMENDED recommendations                         │
│ - Detailed issues list with remediation guidance               │
└─────────────────────────────────────────────────────────────────┘
```

---

## Scoring System

### Four Dimensions of Quality

#### 1. Task Specificity (0-30 points)

**What It Measures**: Concreteness and unambiguity of task descriptions

**Sub-Criteria**:
- **Concrete File Paths (10 points)**:
  - 10 points: All tasks specify exact file paths (e.g., `src/Services/AuthService.cs`)
  - 5 points: Most tasks have file paths, some generic
  - 0 points: Few or no file paths
- **Specific Class/Interface Names (10 points)**:
  - 10 points: All technical tasks name specific classes/interfaces
  - 5 points: Most tasks have specific names
  - 0 points: Generic descriptions like "create service"
- **Clear Acceptance Criteria (10 points)**:
  - 10 points: Every task has explicit completion criteria
  - 5 points: Most tasks have criteria
  - 0 points: Few or no acceptance criteria

**Example**:
```markdown
❌ POOR (0/10 file paths):
### 2.1 Create Authentication Service
Implement the authentication service.

✅ EXCELLENT (10/10 file paths):
### 2.1 Create Authentication Service
**File**: `src/Orchestra.Core/Services/AuthService.cs`
- [ ] Create AuthService class implementing IAuthenticationService
- [ ] Add methods: LoginAsync(), LogoutAsync(), ValidateTokenAsync()
```

#### 2. Technical Completeness (0-30 points)

**What It Measures**: Presence of ALL integration steps, not just implementation code

**Sub-Criteria**:
- **Integration Steps (15 points)**:
  - Entity tasks: DbContext registration, Entity configuration, Migration creation
  - Service tasks: Interface definition, DI registration in Program.cs
  - API tasks: Authorization setup, Middleware configuration, Routing
- **Migration Workflow (10 points)**:
  - Database changes include migration creation commands
  - Migration application steps included
  - Verification steps present
- **Error Handling Patterns (5 points)**:
  - Error handling patterns specified (try-catch, validation)
  - Logging patterns mentioned (ILogger usage)

**Example**:
```markdown
❌ INCOMPLETE (0/15 integration steps):
### 2.1 Create User Entity
- [ ] Create User.cs with properties
- [ ] Add validation attributes

✅ COMPLETE (15/15 integration steps):
### 2.1A: Create User Entity Class
- [ ] Create Core/Models/User.cs
- [ ] Define properties: Id, Username, Email, PasswordHash
- [ ] Add validation attributes: [Required], [MaxLength(100)]

### 2.1B: Integrate with Entity Framework
- [ ] Add DbSet<User> in ApplicationDbContext
- [ ] Configure entity in OnModelCreating (indexes on Username, Email)
- [ ] Create migration: `dotnet ef migrations add AddUserTable`

### 2.1C: Verify Integration
- [ ] Code compiles without errors
- [ ] Apply migration to dev database
- [ ] Verify User table created with correct schema
```

#### 3. Execution Clarity (0-20 points)

**What It Measures**: How clearly tasks are decomposed and dependencies identified

**Sub-Criteria**:
- **Step-by-Step Decomposition (10 points)**:
  - Tasks decomposed into clear, sequential steps
  - Numbered or bulleted step lists present
  - Depth of decomposition (1 level basic, 2+ levels detailed)
- **Dependencies Clearly Identified (10 points)**:
  - All task dependencies explicitly stated
  - Dependency keywords used: "After", "Requires", "Depends on"
  - Explicit task references: "After 2.1 completes"

**Deductions**:
- 1 task >30 tool calls → -5 points
- 2-3 tasks >30 tool calls → -10 points
- 4+ tasks >30 tool calls → -15 points

**Example**:
```markdown
❌ POOR (0/10 decomposition):
### 3.1 Authentication
Build authentication system.

✅ EXCELLENT (10/10 decomposition):
### 3.1 Authentication Service Implementation
**Dependencies**: 2.1 User entity must be complete

Steps:
1. Create IAuthenticationService interface with methods
2. Implement AuthenticationService class
3. Add password hashing logic
4. Integrate with UserRepository
5. Register in DI container
```

#### 4. Structure Compliance (0-20 points)

**What It Measures**: Adherence to catalogization rules and structural standards

**Sub-Criteria**:
- **Catalogization Rules (10 points)**:
  - GOLDEN RULE #1: Directory name matches file name (without .md)
  - GOLDEN RULE #2: Coordinator outside directory
- **File Size Limits (5 points)**:
  - All files ≤400 lines: 5 points
  - 1-2 files >400 lines: 3 points
  - 3+ files >400 lines: 0 points
- **Reference Integrity (5 points)**:
  - All parent/child references valid and bidirectional
  - No broken file paths
  - No circular dependencies

**Example**:
```markdown
❌ GOLDEN RULE #1 VIOLATION:
File: 01-Data-Models.md
Directory: 01-architecture/  ← WRONG! Should be "01-Data-Models/"

❌ GOLDEN RULE #2 VIOLATION:
01-Data-Models/
  └── 01-Data-Models.md  ← WRONG! Coordinator inside directory

✅ CORRECT STRUCTURE:
01-Data-Models.md        ← Coordinator outside
01-Data-Models/          ← Directory matches file name
  ├── 01-api-models.md
  └── 02-db-entities.md
```

### Pass/Fail Threshold: 90%

**Why 90%?**
- **Pragmatic Balance**: Allows minor issues while maintaining quality
- **Execution Confidence**: 90%+ plans have high success rate in LLM execution
- **Avoids Perfectionism**: 100% score rare and unnecessary
- **Focus on Critical Issues**: Forces addressing technical completeness and execution clarity

**Score Range Interpretation**:
- **90-100**: ✅ READY - Proceed to execution with confidence
- **80-89**: ⚠️ BORDERLINE - Address major issues, consider re-validation
- **70-79**: ❌ REQUIRES_IMPROVEMENT - Significant gaps, major revisions needed
- **<70**: ❌ CRITICAL - Fundamentally incomplete, restart planning

**Dimension-Specific Thresholds** (for READY status):
- Technical Completeness: ≥27/30 (90%) - CRITICAL
- Task Specificity: ≥27/30 (90%) - CRITICAL
- Execution Clarity: ≥18/20 (90%) - IMPORTANT
- Structure Compliance: ≥18/20 (90%) - IMPORTANT

**Priority of Issues**:
1. **Technical Completeness** (highest priority) - Missing integration steps block execution
2. **Execution Clarity** (high priority) - Ambiguous tasks cause execution failures
3. **Structure Compliance** (medium priority) - Affects maintainability, not execution
4. **Task Specificity** (medium priority) - Reduces efficiency, doesn't block execution

---

## Output Interpretation

### READY Status (Score ≥90%)

**What It Means**:
- Plan is production-ready for LLM-based execution
- All critical integration steps present
- Task specificity sufficient for unambiguous execution
- Structure compliant with catalogization rules
- Execution can proceed with high confidence

**Example Output**:
```markdown
# Plan Readiness Validation Report

**Plan**: feature-authentication-workplan.md
**Date**: 2025-10-14
**Status**: ✅ READY
**LLM Readiness Score**: 94/100

---

## Score Breakdown

### Task Specificity: 28/30 (-2)
- All tasks specify concrete file paths ✅
- All tasks name specific classes/interfaces ✅
- Minor improvement: Task 3.2 could add more explicit acceptance criteria

### Technical Completeness: 30/30
- All Entity tasks include DbContext integration ✅
- All Service tasks include DI registration ✅
- All API tasks include middleware setup ✅
- Migration workflow complete ✅

### Execution Clarity: 18/20 (-2)
- Step-by-step decomposition present ✅
- Dependencies clearly identified ✅
- Task 4.1 complexity borderline (28 tool calls), but acceptable

### Structure Compliance: 18/20 (-2)
- GOLDEN RULES followed ✅
- All files ≤400 lines ✅
- 2 minor reference formatting issues (non-blocking)

---

## Recommendations

### CRITICAL Next Actions

1. **plan-task-executor**: Begin execution with task prioritization
   - **Rationale**: Plan score ≥90%, ready for execution
   - **Start with**: Phase 1 (highest priority tasks)

2. **architecture-documenter**: Document planned architecture
   - **Rationale**: 5 new components detected (AuthService, TokenValidator, UserRepository, AuthController, RefreshTokenEntity)
   - **Location**: `Docs/Architecture/Planned/feature-authentication-architecture.md`

### RECOMMENDED Improvements

3. **parallel-plan-optimizer**: Analyze for parallel execution opportunities
   - **Rationale**: Plan contains 12 tasks, parallelization could reduce time by 40-50%
   - **Expected Benefit**: 15-20 hours → 8-12 hours
```

**Next Actions**:
1. Invoke plan-task-executor to begin implementation
2. Invoke architecture-documenter to create planned architecture docs (BEFORE execution)
3. (Optional) Invoke parallel-plan-optimizer to identify parallelization opportunities

### REQUIRES_IMPROVEMENT Status (Score <90%)

**What It Means**:
- Plan has critical issues that will cause execution failures
- Missing integration steps, over-complex tasks, or structural violations detected
- Revision required before execution can proceed safely
- Issues list provided with file:line references for easy fixing

**Example Output**:
```markdown
# Plan Readiness Validation Report

**Plan**: actions-block-refactoring-phase3.md
**Date**: 2025-10-14
**Status**: ❌ REQUIRES_IMPROVEMENT
**LLM Readiness Score**: 76/100

---

## Score Breakdown

### Task Specificity: 18/30 (-12)
- Many tasks lack specific file paths (5/12 tasks generic)
- Class names present but inconsistent
- Acceptance criteria missing from 60% of tasks

### Technical Completeness: 20/30 (-10)
- Entity tasks missing DbContext integration steps
- Service tasks missing DI registration details
- API tasks incomplete (no middleware setup mentioned)
- Migration workflow partially defined

### Execution Clarity: 14/20 (-6)
- Some step-by-step decomposition, but inconsistent
- Dependencies implied but not explicit
- 3 tasks exceed 30 tool calls (complexity violation)

### Structure Compliance: 24/20 (+4 BONUS)
- GOLDEN RULES followed perfectly ✅
- All files ≤400 lines ✅
- Reference integrity excellent ✅

---

## Critical Issues (Must Fix Before Execution)

### Issue 1: Missing Integration Steps for Entity Tasks
**File**: actions-block-refactoring-phase3.md
**Lines**: 45-67
**Severity**: CRITICAL
**Category**: Technical Completeness

**Issue Description**:
Task 2.1 "Create WorkflowTemplate Entity" specifies entity creation but omits integration steps.

**Current**:
```markdown
### 2.1 Create WorkflowTemplate Entity
- [ ] Create WorkflowTemplate.cs with properties
- [ ] Add validation attributes
```

**Expected**:
```markdown
### 2.1A: Create WorkflowTemplate Entity Class
- [ ] Create Core/Models/WorkflowTemplate.cs
- [ ] Define properties: Id, Name, Steps, CreatedDate
- [ ] Add validation attributes: [Required], [MaxLength]

### 2.1B: Integrate with Entity Framework
- [ ] Add DbSet<WorkflowTemplate> in OrchestraDbContext
- [ ] Configure entity in OnModelCreating (indexes on Name, CreatedDate)
- [ ] Create migration: `dotnet ef migrations add AddWorkflowTemplateTable`

### 2.1C: Verify Integration
- [ ] Verify compilation (0 errors)
- [ ] Apply migration to dev database
- [ ] Test repository DI resolution
```

**Recommendation**: Decompose task 2.1 into 2.1A (Entity), 2.1B (EF Integration), 2.1C (Validation).

**Impact**: CRITICAL - Missing integration steps will cause execution failure (DbContext errors, DI resolution failures).

---

### Issue 2: Task Complexity Exceeds Limit
**File**: actions-block-refactoring-phase3.md
**Line**: 89
**Severity**: CRITICAL
**Category**: Execution Clarity

**Issue Description**:
Task 3.1 "Workflow Manager Implementation" estimated at 42 tool calls (exceeds 30 tool call limit).

**Complexity Breakdown**:
- Interface definition: 3 tool calls
- Service implementation: 10 tool calls
- Workflow execution logic: 12 tool calls
- State management: 8 tool calls
- DI registration: 2 tool calls
- Unit tests: 7 tool calls
**Total**: 42 tool calls ❌

**Expected**: Decompose into 4 subtasks:
- 3.1A: Workflow Manager Interface and State Models (8 tool calls)
- 3.1B: Workflow Manager Core Implementation (12 tool calls)
- 3.1C: Workflow Execution Engine (12 tool calls)
- 3.1D: DI Registration and Tests (10 tool calls)

**Recommendation**: Split task 3.1 into 3.1A-D to stay within 30 tool call limit per task.

**Impact**: CRITICAL - Over-complex tasks cause execution failures due to context overload.

---

## Recommendations

### CRITICAL Next Actions

1. **work-plan-architect**: Fix critical issues and re-validate
   - **Priority 1**: Add integration steps to all Entity tasks (Issues like Issue 1)
   - **Priority 2**: Decompose over-complex tasks (Issues like Issue 2)
   - **Priority 3**: Add file paths to all technical tasks
   - **Estimated Effort**: 2-3 hours to fix all critical issues
   - **Re-validation**: Submit to plan-readiness-validator after fixes
```

**Next Actions**:
1. Invoke work-plan-architect with issues list for revision
2. After architect fixes issues, re-validate with plan-readiness-validator
3. If score ≥90% after revision → proceed to plan-task-executor
4. If score still <90% after 3 iterations → escalate to user for manual intervention

---

## Troubleshooting

### Common Issue 1: Low Task Specificity Score (<24/30)

**Symptom**: Tasks are vague, missing file paths or class names

**Example**:
```markdown
### 2.1 Create Authentication Service
Implement the authentication service.
```

**Root Cause**:
- Generic task descriptions without concrete file paths
- Missing class/interface names
- No acceptance criteria specified

**Solution**:
Add concrete details to every task:
```markdown
### 2.1 Create Authentication Service
**File**: `src/Orchestra.Core/Services/AuthService.cs`

**Class**: `AuthService : IAuthenticationService`

**Acceptance Criteria**:
- [ ] AuthService class created in correct directory
- [ ] IAuthenticationService interface implemented
- [ ] Methods: LoginAsync(), LogoutAsync(), ValidateTokenAsync()
- [ ] Constructor accepts IUserRepository via DI
- [ ] All methods throw NotImplementedException() with TODO comments
- [ ] Code compiles without errors
```

**Expected Score Improvement**: +8 to +12 points (Task Specificity)

---

### Common Issue 2: Low Technical Completeness Score (<24/30)

**Symptom**: Entity/Service/API tasks missing integration steps

**Example**:
```markdown
### 2.1 Create User Entity
- [ ] Create User.cs with properties
```

**Root Cause**:
- Focus on implementation only, missing integration steps
- No DbContext registration, Entity configuration, or migration workflow
- No DI registration for services
- No middleware/authorization setup for APIs

**Solution**:
Decompose task into separate integration phases:
```markdown
### 2.1A: Create User Entity Class
- [ ] Create Core/Models/User.cs
- [ ] Define properties: Id, Username, Email, PasswordHash
- [ ] Add validation attributes: [Required], [MaxLength]

### 2.1B: Integrate with Entity Framework
- [ ] Add DbSet<User> in ApplicationDbContext
- [ ] Configure entity in OnModelCreating:
  - Add unique index on Username
  - Add index on Email
  - Set PasswordHash as required
- [ ] Create migration: `dotnet ef migrations add AddUserTable`

### 2.1C: Verify Integration
- [ ] Code compiles without errors
- [ ] Apply migration to dev database: `dotnet ef database update`
- [ ] Verify User table exists with correct schema
```

**Expected Score Improvement**: +10 to +15 points (Technical Completeness)

---

### Common Issue 3: Low Execution Clarity Score (<15/20)

**Symptom**: Tasks too complex (>30 tool calls), dependencies unclear

**Example**:
```markdown
### 3.1 Build Complete Authentication System
Implement user authentication with JWT tokens, refresh tokens, password hashing, authorization middleware, and API controllers.
```

**Root Cause**:
- Monolithic tasks combining multiple features
- No decomposition into subtasks
- No explicit dependencies stated

**Solution**:
1. **Decompose over-complex tasks** into subtasks (each ≤30 tool calls):
```markdown
### 3.1A: User Authentication Core
**Dependencies**: 2.1 User entity must be complete
**Estimated Complexity**: 18 tool calls

Steps:
- [ ] Create IAuthenticationService interface
- [ ] Implement AuthService with LoginAsync() method
- [ ] Add password hashing logic (use BCrypt)
- [ ] Register in DI: `services.AddScoped<IAuthenticationService, AuthService>()`

### 3.1B: JWT Token Management
**Dependencies**: 3.1A AuthService must be complete
**Estimated Complexity**: 22 tool calls

Steps:
- [ ] Create ITokenService interface
- [ ] Implement TokenService with GenerateToken() method
- [ ] Configure JWT settings in appsettings.json
- [ ] Register in DI: `services.AddScoped<ITokenService, TokenService>()`

### 3.1C: Authorization Middleware
**Dependencies**: 3.1B TokenService must be complete
**Estimated Complexity**: 15 tool calls

Steps:
- [ ] Configure authentication middleware in Program.cs
- [ ] Add `app.UseAuthentication()` and `app.UseAuthorization()`
- [ ] Create AuthController with Login endpoint
- [ ] Add [Authorize] attributes to protected endpoints
```

2. **Add explicit dependencies** to every task:
```markdown
**Dependencies**:
- CRITICAL: 2.1 User entity must be complete (interface needed)
- CRITICAL: 3.1A AuthService must be in DI container

**Blocks**: 4.1 (AuthController needs AuthService)
```

**Expected Score Improvement**: +5 to +10 points (Execution Clarity)

---

### Common Issue 4: Structure Compliance Violations (<15/20)

**Symptom**: GOLDEN RULE violations, file size exceeding 400 lines, broken references

**Example**:
```markdown
❌ GOLDEN RULE #1 VIOLATION:
File: 01-Data-Models.md
Directory: 01-architecture/  (should be "01-Data-Models/")

❌ File Size VIOLATION:
File: main-plan.md (650 lines, limit: 400)

❌ Broken Reference:
Parent: plan.md references "./plan/02-phase.md" (file not found)
```

**Root Cause**:
- Directory names don't match coordinator file names
- Coordinator file inside its directory
- Large files not decomposed into child files
- File paths incorrect or files missing

**Solution**:
1. **Fix GOLDEN RULE #1 violations**:
```markdown
Before:
01-Data-Models.md
01-architecture/  ← WRONG

After:
01-Data-Models.md
01-Data-Models/  ← CORRECT (matches file name)
```

2. **Fix GOLDEN RULE #2 violations**:
```markdown
Before:
01-Data-Models/
  └── 01-Data-Models.md  ← WRONG (coordinator inside directory)

After:
01-Data-Models.md  ← CORRECT (coordinator outside)
01-Data-Models/
  ├── 01-api-models.md
  └── 02-db-entities.md
```

3. **Fix file size violations**:
- Move content from large coordinator to child phase files
- Aim for ≤250 lines per file (optimal), max 400 lines
- Create 3-5 child files for large plans

4. **Fix broken references**:
- Verify all parent → child links exist
- Verify all child → parent links exist
- Use relative paths: `../parent.md`, `./child-dir/child.md`

**Expected Score Improvement**: +5 to +10 points (Structure Compliance)

---

### Common Issue 5: "Plan vs. Implementation" Confusion

**Symptom**: Plan contains full implementation code, few TODO markers

**Example**:
```markdown
### 2.1 Create AuthService

```csharp
public class AuthService : IAuthenticationService
{
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        // Full implementation with LINQ, loops, error handling
        var user = await _repository.GetByUsernameAsync(username);
        if (user == null) return AuthResult.Failed("Invalid credentials");

        var hash = HashPassword(password, user.Salt);
        if (hash != user.PasswordHash) return AuthResult.Failed("Invalid credentials");

        return AuthResult.Success(GenerateToken(user));
    }
}
```
```

**Root Cause**:
- Plan contains realization, not architecture
- Full business logic in plan (belongs in execution phase)
- Few TODO markers (<5 total)

**Solution**:
Replace implementation with architecture:
```markdown
### 2.1 Create AuthService

**File**: `src/Orchestra.Core/Services/AuthService.cs`

```csharp
/// <summary>
/// Authentication service interface
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticate user with credentials
    /// </summary>
    Task<AuthResult> LoginAsync(string username, string password);
}

public class AuthService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    // TODO: Implement authentication logic with password validation
    public Task<AuthResult> LoginAsync(string username, string password)
    {
        throw new NotImplementedException();
    }
}
```
```

**Expected Score Improvement**: +10 to +15 points (Task Specificity + Execution Clarity)

---

### Common Issue 6: Agent Transition Recommendations Missing

**Symptom**: Validator report doesn't include recommended next agents

**Root Cause**:
- Validator didn't detect architectural components
- Task count incorrect (didn't trigger parallel-plan-optimizer)
- Status determination logic error

**Solution**:
1. **Verify architectural component detection**:
   - Check plan for keywords: "DbContext", "interface", "DI", "controller", "middleware"
   - Ensure Entity/Service/Controller patterns present
   - Expected recommendation: architecture-documenter (CRITICAL)

2. **Verify task counting**:
   - Count ### X.Y sections manually
   - If >5 tasks, parallel-plan-optimizer should be recommended
   - Pattern: `^### \d+\.\d+` (regex)

3. **Verify status logic**:
   - Score ≥90% → plan-task-executor recommended
   - Score <90% → work-plan-architect recommended
   - Both should be present in recommendations

**Debugging Commands**:
```bash
# Count tasks manually
grep -E "^### \d+\.\d+" plan.md | wc -l

# Check for architectural keywords
grep -E "(DbContext|interface|DI|controller|middleware)" plan.md

# Verify status determination
# If score = 92/100 → expect plan-task-executor recommendation
```

---

### Common Issue 7: False Negative (READY but should be REQUIRES_IMPROVEMENT)

**Symptom**: Plan flagged as READY (≥90%) but has critical issues

**Root Cause**:
- Scoring algorithm too lenient
- Pattern detection missed critical gaps
- Edge case not handled

**Solution**:
1. **Manual Review** of flagged plan:
   - Check Entity tasks for DbContext integration (line-by-line)
   - Check Service tasks for DI registration
   - Check API tasks for middleware setup
   - Verify all acceptance criteria present

2. **Report False Negative** for calibration:
   - Document plan path and score
   - Document specific issues missed by validator
   - Submit for algorithm recalibration

3. **Temporary Workaround**:
   - Use stricter threshold (95% instead of 90%)
   - Add manual review step before execution
   - Cross-reference with systematic-plan-reviewer results

**Calibration Process** (Post-MVP):
- Collect 20+ manually reviewed plans
- Compare automated vs. manual scores
- Adjust weights or detection patterns
- Target: ≥95% agreement, <5% false negative rate

---

## Integration Points

### Agent Transition Matrix

The plan-readiness-validator integrates with multiple agents based on validation results:

**Upstream Agents** (input to validator):
- **work-plan-architect**: Creates plans that validator checks
  - Handoff: architect creates plan → validator validates
  - Iteration: If REQUIRES_IMPROVEMENT → back to architect (max 3 iterations)

**Downstream Agents** (output from validator):

#### CRITICAL Transitions (Always Recommended)

1. **plan-task-executor** (if READY - score ≥90%):
   - **Condition**: Plan validated as READY
   - **Priority**: RECOMMENDED (execution is next logical step)
   - **Parameters**: `plan_file`
   - **Example**:
     ```markdown
     🔄 Recommended Next Actions:

     1. ⚠️ RECOMMENDED: plan-task-executor
        Reason: Plan validated as READY (score: 93/100)
        Command: Use Task tool with subagent_type: "plan-task-executor"
        Parameters:
          plan_file: "Docs/plans/feature-auth-plan.md"
     ```

2. **work-plan-architect** (if REQUIRES_IMPROVEMENT - score <90%):
   - **Condition**: Plan has critical issues
   - **Priority**: CRITICAL (revision mandatory before execution)
   - **Parameters**: `revision_mode`, `plan_file`, `issues_list`
   - **Iteration Limit**: Max 3 iterations (escalation after 3 failures)
   - **Example**:
     ```markdown
     🔄 Recommended Next Actions:

     1. 🚨 CRITICAL: work-plan-architect
        Reason: Plan scored 76/100 (below 90% threshold) - requires revision
        Command: Use Task tool with subagent_type: "work-plan-architect"
        Parameters:
          revision_mode: true
          plan_file: "Docs/plans/feature-auth-plan.md"
          issues_list: [
            "Task 2.1 missing DbContext integration",
            "Task 3.1 complexity exceeds 30 tool calls",
            ...
          ]
        Cycle Tracking:
          cycle_id: "validator-architect-feature-auth"
          iteration_count: 1
          max_iterations: 3
     ```

3. **architecture-documenter** (if architectural components detected):
   - **Condition**: Plan contains Entity/Service/Controller patterns
   - **Priority**: CRITICAL (architectural planning mandatory before execution)
   - **Parameters**: `type="planned"`, `plan_file`, `components`
   - **Detection Method**: Keyword-based (DbContext, DI, controller, interface, etc.)
   - **Example**:
     ```markdown
     🔄 Recommended Next Actions:

     2. 🚨 CRITICAL: architecture-documenter
        Reason: Plan contains 5 architectural components requiring planned documentation
        Command: Use Task tool with subagent_type: "architecture-documenter"
        Parameters:
          type: "planned"
          plan_file: "Docs/plans/feature-auth-plan.md"
          components: ["AuthService", "TokenValidator", "UserRepository", "AuthController", "RefreshTokenEntity"]
        Location: Docs/Architecture/Planned/feature-auth-architecture.md
     ```

#### RECOMMENDED Transitions (Conditional)

4. **parallel-plan-optimizer** (if >5 tasks):
   - **Condition**: Plan has >5 tasks (parallelization opportunities)
   - **Priority**: RECOMMENDED (optimization optional but beneficial)
   - **Parameters**: `plan_file`, `task_count`
   - **Threshold**: >5 tasks triggers recommendation
   - **Expected Benefit**: 40-50% time reduction
   - **Example**:
     ```markdown
     🔄 Recommended Next Actions:

     3. ⚠️ RECOMMENDED: parallel-plan-optimizer
        Reason: Plan contains 12 tasks (exceeds 5-task threshold for parallelization)
        Command: Use Task tool with subagent_type: "parallel-plan-optimizer"
        Parameters:
          plan_file: "Docs/plans/feature-auth-plan.md"
          task_count: 12
        Estimated Time Reduction: 40% (sequential: 20 hours → parallel: 12 hours)
     ```

### Iteration Cycle Protection (CRITICAL)

**Quality Cycle: architect ↔ validator**

**Max Iterations**: 3
- **Iteration 1**: Initial validation fails → architect revises
- **Iteration 2**: Validation improves but <90% → architect revises again
- **Iteration 3**: Final validation attempt
- **Escalation**: If iteration 3 fails, escalate to user with detailed report

**Iteration Tracking**:
- `cycle_id`: Maintained across iterations (e.g., "validator-architect-feature-auth")
- `iteration_count`: Increments correctly (1 → 2 → 3)
- `max_iterations`: Checked before recommending next iteration (3)
- **Warning**: Shown at iteration 2 (approaching limit)

**Escalation Mechanism**:
```markdown
⚠️ CYCLE LIMIT REACHED - ESCALATION TO USER ⚠️

Cycle: validator ↔ architect
Iterations completed: 3/3 (limit reached)
Final Score: 88/100 (still below 90% threshold)

UNRESOLVED ISSUES:
- Issue 1: Task decomposition still too high (35 tool calls, limit 30)
  Attempted fixes: Split task into 2 subtasks
  Why failed: Subtasks still complex, may need 3-way split

RECOMMENDED ACTIONS:
- Manual intervention required for task decomposition
- Consider alternative approach: Simplify feature scope
- Consult with architect for complexity reduction strategy
```

**Example Iteration Cycle**:
```markdown
Iteration 1:
- Validator score: 72/100 (REQUIRES_IMPROVEMENT)
- Issues: 8 found (3 CRITICAL, 5 IMPORTANT)
- Architect fixes: Adds DbContext integration, updates file paths
- Re-validation triggered

Iteration 2:
- Validator score: 87/100 (REQUIRES_IMPROVEMENT - improved but still <90%)
- Issues: 3 remaining (all IMPORTANT, no CRITICAL)
- Progress: 5/8 issues resolved (62.5%)
- Architect fixes: Decomposes over-complex tasks, adds acceptance criteria
- Re-validation triggered

Iteration 3:
- Validator score: 92/100 (READY - threshold exceeded!)
- Issues: 0 critical, 1 minor (non-blocking)
- Progress: 8/8 critical issues resolved (100%)
- Cycle complete - recommend plan-task-executor
```

### Integration with Other Agents

**systematic-plan-reviewer** (Complementary Validation):
- **Relationship**: systematic-plan-reviewer focuses on structure, plan-readiness-validator focuses on LLM execution readiness
- **Usage**: Can run both in parallel for comprehensive validation
- **Comparison**: plan-readiness-validator is automated (60s), systematic-plan-reviewer is manual (30-60 min)

**test-healer** (Post-Execution):
- **Relationship**: plan-readiness-validator validates plan quality, test-healer validates test quality after execution
- **Usage**: Sequential - validator before execution, test-healer after execution
- **Scope**: Validator checks plan has test tasks, test-healer checks tests pass

---

## Related Documentation

### Agent Specification Files

- **agent.md** (546 lines): Complete agent specification
  - Purpose and agent classification
  - Core capabilities (LLM scoring, structure validation, complexity analysis)
  - Input/output specifications
  - Integration points and agent transition matrix
  - Success criteria and failure modes
  - Usage examples and best practices

- **prompt.md** (1,199 lines): 6-step validation workflow
  - STEP 1: Load validation rules and context
  - STEP 2: LLM readiness scoring (0-100 scale)
  - STEP 3: Execution complexity analysis
  - STEP 4: "Plan ≠ Realization" validation
  - STEP 5: Generate agent transition recommendations
  - STEP 6: Generate validation report
  - Tool usage guidelines
  - Common validation scenarios
  - Error handling

- **scoring-algorithm.md** (964 lines): Detailed scoring rubric
  - Four dimensions with exact point allocations
  - Task Specificity (0-30 points)
  - Technical Completeness (0-30 points)
  - Execution Clarity (0-20 points)
  - Structure Compliance (0-20 points)
  - Score aggregation and interpretation
  - Edge cases and special handling
  - Calibration and validation methodology

### Test and Integration Documentation

- **test-validation.md** (1,090 lines): 10+ sample plans with expected scores
  - 5 READY plans (expected score ≥90%)
  - 5 REQUIRES_IMPROVEMENT plans (expected score <90%)
  - Edge case scenarios (empty plans, circular references, etc.)
  - Performance benchmarks (small, medium, large, very large plans)
  - Test execution methodology
  - Metrics calculation (agreement rate, false positive/negative, recall, precision)

- **integration-test-spec.md** (1,975 lines): 5 integration scenarios
  - Scenario 4.2.1: work-plan-architect → plan-readiness-validator
  - Scenario 4.2.2: plan-readiness-validator → plan-task-executor
  - Scenario 4.2.3: plan-readiness-validator → architecture-documenter
  - Scenario 4.2.4: plan-readiness-validator → parallel-plan-optimizer
  - Scenario 4.2.5: Iteration cycle (validator ↔ architect, max 3 iterations)
  - Agent handoff verification checklist
  - Transition matrix validation checklist

- **scoring-validation-spec.md** (1,487 lines): Scoring algorithm validation
  - Scoring reproducibility tests
  - Dimension weight validation
  - Threshold calibration methodology
  - False positive/negative analysis
  - Calibration adjustment procedures

### Validation Rule Files

- **`.cursor/rules/common-plan-generator.mdc`**: Plan structure rules
  - Task numbering conventions (### X.Y format)
  - File naming conventions (XX-Name.md)
  - Catalogization rules (GOLDEN RULES #1 and #2)
  - File size limits (≤400 lines)
  - Integration step requirements (Entity/Service/API patterns)

- **`.cursor/rules/common-plan-reviewer.mdc`**: Plan quality standards
  - Systematic review methodology
  - Quality checklist (structure, completeness, clarity)
  - Issue severity levels (CRITICAL, IMPORTANT, MINOR)
  - Remediation guidance

- **`.cursor/rules/catalogization-rules.mdc`**: Plan file organization
  - Directory structure requirements
  - Parent/child relationship rules
  - Reference integrity validation
  - File naming conventions

### Implementation Plan

- **`Docs/plans/Plan-Readiness-Validator-Implementation-Plan.md`**: Complete implementation roadmap
  - Phase 1: Core specification and scoring algorithm (COMPLETE)
  - Phase 2: Prompt engineering and workflow design (COMPLETE)
  - Phase 3: Architectural component detection (COMPLETE)
  - Phase 4: Test validation and integration testing (COMPLETE)
  - Phase 5: Documentation and integration (IN PROGRESS)
  - Success metrics and acceptance criteria
  - Risk mitigation strategies

---

## Appendix: Quick Reference

### Validation Command Template

```markdown
# Validate plan
subagent_type: "plan-readiness-validator"
parameters:
  plan_file: "C:\path\to\your-plan.md"

# Optional: Custom threshold (default: 90)
  validation_config:
    threshold: 95  # Stricter validation
    strict_mode: true  # Enforce 100% structure compliance
```

### Status Determination Logic

```
IF score ≥90:
  Status = READY
  Recommend: plan-task-executor

ELSE IF score <90:
  Status = REQUIRES_IMPROVEMENT
  Recommend: work-plan-architect (revision)

IF architectural components detected:
  Recommend: architecture-documenter (CRITICAL)

IF task count >5:
  Recommend: parallel-plan-optimizer (RECOMMENDED)
```

### Scoring Quick Reference

| Dimension | Points | Focus | Common Issues |
|-----------|--------|-------|---------------|
| Task Specificity | 0-30 | Concrete file paths, class names, acceptance criteria | Generic descriptions, missing paths |
| Technical Completeness | 0-30 | Integration steps (DbContext, DI, migrations) | Missing Entity/Service/API integration |
| Execution Clarity | 0-20 | Step-by-step decomposition, clear dependencies | Over-complex tasks (>30 tool calls) |
| Structure Compliance | 0-20 | GOLDEN RULES, file size (≤400 lines), references | Catalogization violations, broken links |

### Performance Targets

| Plan Size | Files | Total Lines | Target Time |
|-----------|-------|-------------|-------------|
| Small | 1-3 | <500 | <15 seconds |
| Medium | 4-7 | 500-2000 | <30 seconds |
| Large | 8-15 | 2000-5000 | <60 seconds |
| Very Large | >15 | >5000 | <90 seconds |

### Common Workflows

**Workflow 1: Standalone Validation**
```
User creates plan → Validate → Review report → Fix issues → Re-validate → Execute
```

**Workflow 2: Post-Architect Validation**
```
Architect creates plan → Validator runs → If READY: execute | If REQUIRES_IMPROVEMENT: architect revises → Validator re-runs (max 3 iterations)
```

**Workflow 3: CI/CD Blocking Validation**
```
Plan committed → CI/CD invokes validator → If <90%: pipeline fails → Developer fixes → Re-commit → Validator re-runs → If ≥90%: pipeline passes
```

---

**README Version**: 1.0
**Last Updated**: 2025-10-15
**Maintained By**: Development Team
**Questions/Issues**: Submit to main agent for escalation
