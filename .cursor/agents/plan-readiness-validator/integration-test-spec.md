# Plan Readiness Validator - Integration Test Specification

**Document Type**: Integration Test Specification
**Purpose**: Define comprehensive integration testing methodology for plan-readiness-validator agent interactions with upstream and downstream agents
**Target Coverage**: 5 integration scenarios with 100% agent transition matrix compliance
**Testing Approach**: Specification-based (document HOW to test, not execute live tests)

---

## Integration Test Overview

### Testing Objectives

1. **Agent Handoff Validation**: Verify smooth parameter passing and file path resolution between agents
2. **Transition Matrix Compliance**: Ensure CRITICAL/RECOMMENDED recommendations follow architecture rules
3. **Context Propagation**: Validate that plan characteristics (tasks count, architectural components) propagate correctly
4. **Error Handling**: Confirm graceful failure modes and escalation paths
5. **Iteration Cycles**: Test validation → architect → validation feedback loops (max 3 iterations)

### Test Scope

**Upstream Agents (input to plan-readiness-validator)**:
- work-plan-architect (plan creation)
- work-plan-reviewer (manual validation)
- systematic-plan-reviewer (automated validation)

**Downstream Agents (output from plan-readiness-validator)**:
- plan-task-executor (if READY)
- architecture-documenter (if architectural components detected)
- parallel-plan-optimizer (if >5 tasks)
- work-plan-architect (if REQUIRES_IMPROVEMENT - iteration)

### Success Criteria

| Integration Aspect | Target | Description |
|--------------------|--------|-------------|
| Handoff Success Rate | 100% | All agent handoffs complete without errors |
| Parameter Compatibility | 100% | All parameters pass correctly between agents |
| Recommendation Accuracy | 100% | CRITICAL/RECOMMENDED recommendations match transition matrix |
| File Path Resolution | 100% | All file paths resolve correctly across agent boundaries |
| Error Propagation | 100% | Validation errors propagate to downstream agents correctly |
| Iteration Limit Enforcement | 100% | Max 3 iterations enforced for architect ↔ validator cycles |

---

## Integration Test Scenarios

### Scenario 4.2.1: work-plan-architect → plan-readiness-validator Flow

**Test ID**: INTEGRATION-4.2.1
**Purpose**: Validate that plans created by work-plan-architect are correctly validated by plan-readiness-validator
**Upstream Agent**: work-plan-architect
**Downstream Agent**: plan-readiness-validator
**Agent Transition Matrix Reference**: Lines 171, 174 (work-plan-architect → plan-readiness-validator RECOMMENDED)

#### Test Setup

**Preconditions**:
- work-plan-architect agent specification exists
- plan-readiness-validator agent specification complete
- Test directory created: `Docs/plans/test-integration/`
- Clean state (no previous test artifacts)

**Test Plan File**:
- Path: `Docs/plans/test-integration/scenario-4.2.1-architect-output.md`
- Characteristics:
  - Created by work-plan-architect
  - 5 tasks (standard plan size)
  - 2 architectural components (UserService, AuthController)
  - Expected score: 88-92/100 (READY)

#### Test Steps

**Step 1: Invoke work-plan-architect**

```markdown
Action: Use Task tool to invoke work-plan-architect
Command: Task({
  subagent_type: "work-plan-architect",
  prompt: "Create authentication feature plan with User entity, UserService, and AuthController"
})

Expected Output:
- Plan file created at `Docs/plans/test-integration/scenario-4.2.1-architect-output.md`
- Plan contains 5+ tasks
- Plan includes architectural components
- work-plan-architect recommends plan-readiness-validator as RECOMMENDED
```

**Step 2: Extract plan file path from architect output**

```markdown
Action: Parse architect completion message for plan file path
Expected Format: "Plan created at: Docs/plans/test-integration/scenario-4.2.1-architect-output.md"

Validation:
- File path is absolute or relative to project root
- File path format compatible with plan-readiness-validator input
```

**Step 3: Invoke plan-readiness-validator with architect output**

```markdown
Action: Use Task tool to invoke plan-readiness-validator
Command: Task({
  subagent_type: "plan-readiness-validator",
  prompt: "Validate plan at Docs/plans/test-integration/scenario-4.2.1-architect-output.md"
})

Expected Input Acceptance:
- Validator receives plan file path correctly
- Validator reads plan coordinator file successfully
- Validator discovers phase files (if any) via Glob
```

**Step 4: Verify validator reads plan files successfully**

```markdown
Action: Monitor validator execution for file access
Expected Behavior:
- Validator reads coordinator file (Read tool)
- Validator globs for phase files (Glob tool with pattern `scenario-4.2.1-architect-output/**/*.md`)
- Validator reads all phase files found
- No file access errors reported
```

**Step 5: Verify validator generates validation report**

```markdown
Action: Check validator output for complete validation report
Expected Output Format:
---
## Validation Report

**Plan**: Docs/plans/test-integration/scenario-4.2.1-architect-output.md
**Status**: READY | REQUIRES_IMPROVEMENT
**LLM Readiness Score**: {score}/100

### Score Breakdown
- Task Specificity: {ts}/30
- Technical Completeness: {tc}/30
- Execution Clarity: {ec}/20
- Structure Compliance: {sc}/20

### Issues Found
{issue_list or "No critical issues"}

### Recommendations
{next_agent_recommendations}
---

Validation Checklist:
- [ ] Status field present (READY or REQUIRES_IMPROVEMENT)
- [ ] LLM Readiness Score calculated (0-100)
- [ ] Score breakdown includes all 4 components
- [ ] Recommendations section includes next agents
```

#### Expected Outcome

**Handoff Success**: YES
- Plan created by architect successfully passed to validator
- File path format compatible
- Validator reads plan without errors

**Parameter Compatibility**: YES
- Plan file path parameter accepted by validator
- File path resolution works correctly
- No parameter format issues

**Validation Completeness**: YES
- Validator generates complete validation report
- Status determination correct (READY expected)
- Recommendations include next agents

#### Integration Issues to Check

**Issue 1: File Path Format Incompatibility**

**Symptom**: Validator cannot find plan file
**Root Cause**: Architect outputs relative path, validator expects absolute path (or vice versa)
**Detection**:
```
Error: File not found: scenario-4.2.1-architect-output.md
Validator received: "scenario-4.2.1-architect-output.md"
Expected format: "Docs/plans/test-integration/scenario-4.2.1-architect-output.md"
```
**Resolution**: Standardize on absolute paths or project-relative paths across all agents

**Issue 2: Phase File Discovery Failure**

**Symptom**: Validator only reads coordinator, misses phase files
**Root Cause**: Glob pattern mismatch (architect creates `01-setup/`, validator searches `scenario-4.2.1-architect-output/01-setup/`)
**Detection**:
```
Validator reads: scenario-4.2.1-architect-output.md (coordinator only)
Phase files exist: scenario-4.2.1-architect-output/01-setup.md
Glob pattern used: scenario-4.2.1-architect-output/**/*.md
Match result: 0 files found (expected 1+)
```
**Resolution**: Align Glob patterns with catalogization rules (GOLDEN RULES #1 and #2)

**Issue 3: Coordinator Outside Directory Not Handled**

**Symptom**: Validator fails when coordinator file outside its directory
**Root Cause**: Validator assumes coordinator inside directory (violates GOLDEN RULE #2)
**Detection**:
```
File structure:
- Docs/plans/test-integration/scenario-4.2.1-architect-output.md (coordinator)
- Docs/plans/test-integration/scenario-4.2.1-architect-output/01-setup.md (phase)

Validator expects:
- Docs/plans/test-integration/scenario-4.2.1-architect-output/scenario-4.2.1-architect-output.md

Result: File not found or misread
```
**Resolution**: Implement correct catalogization logic per common-plan-generator.mdc

#### Test Result Template

```markdown
## Integration Test Result: Scenario 4.2.1

**Test Date**: {timestamp}
**Tester**: {tester_name}
**Test Status**: PASS | FAIL | BLOCKED

### Test Execution Summary

**Step 1: work-plan-architect invocation**: {PASS/FAIL}
- Plan created: {yes/no}
- Plan file path: {actual_path}

**Step 2: File path extraction**: {PASS/FAIL}
- Extracted path: {path}
- Format compatible: {yes/no}

**Step 3: plan-readiness-validator invocation**: {PASS/FAIL}
- Validator received path: {yes/no}
- Validator read plan: {yes/no}

**Step 4: File access verification**: {PASS/FAIL}
- Coordinator read: {yes/no}
- Phase files discovered: {count}
- File access errors: {count}

**Step 5: Validation report generation**: {PASS/FAIL}
- Report generated: {yes/no}
- Report complete: {yes/no}
- Status: {READY/REQUIRES_IMPROVEMENT}
- Score: {score}/100

### Integration Issues Encountered

{if issues_found}
1. **Issue**: {issue_description}
   - **Root Cause**: {root_cause}
   - **Impact**: {impact_level}
   - **Resolution**: {resolution_action}
{else}
✅ No integration issues found
{endif}

### Overall Test Result

**Status**: {PASS/FAIL}
**Handoff Success**: {yes/no}
**Parameter Compatibility**: {yes/no}
**Validation Completeness**: {yes/no}

**Recommendation**: {proceed/fix_issues/escalate}
```

---

### Scenario 4.2.2: plan-readiness-validator → plan-task-executor Flow

**Test ID**: INTEGRATION-4.2.2
**Purpose**: Validate that READY plans approved by validator are correctly executed by plan-task-executor
**Upstream Agent**: plan-readiness-validator
**Downstream Agent**: plan-task-executor
**Agent Transition Matrix Reference**: Lines 174-175 (plan-readiness-validator → plan-task-executor RECOMMENDED if ≥90%)

#### Test Setup

**Preconditions**:
- High-quality plan available (expected score ≥90%)
- plan-readiness-validator agent functional
- plan-task-executor agent specification exists
- Test directory: `Docs/plans/test-integration/`

**Test Plan File**:
- Path: `Docs/plans/test-integration/scenario-4.2.2-ready-plan.md`
- Characteristics:
  - Well-structured plan (expected READY)
  - 3 tasks (small plan for test speed)
  - Complete integration steps
  - Expected score: 93/100

#### Test Steps

**Step 1: Run validator on high-quality plan (expected READY)**

```markdown
Action: Invoke plan-readiness-validator
Command: Task({
  subagent_type: "plan-readiness-validator",
  prompt: "Validate plan at Docs/plans/test-integration/scenario-4.2.2-ready-plan.md"
})

Expected Output:
- Status: READY
- LLM Readiness Score: ≥90/100
- Recommendations section includes plan-task-executor
```

**Step 2: Verify validator recommends plan-task-executor as CRITICAL/RECOMMENDED**

```markdown
Action: Parse validator output for recommendations
Expected Recommendation Format:
---
🔄 Recommended Next Actions:

1. ⚠️ RECOMMENDED: plan-task-executor
   Reason: Plan validated as READY (score: 93/100)
   Command: Use Task tool with subagent_type: "plan-task-executor"
   Parameters:
     plan_file: "Docs/plans/test-integration/scenario-4.2.2-ready-plan.md"
---

Validation Checklist:
- [ ] plan-task-executor listed as RECOMMENDED
- [ ] Priority level appropriate (RECOMMENDED, not CRITICAL)
- [ ] plan_file parameter included in recommendations
- [ ] File path matches validated plan path
```

**Step 3: Invoke plan-task-executor with recommended parameters**

```markdown
Action: Use Task tool to invoke plan-task-executor
Command: Task({
  subagent_type: "plan-task-executor",
  prompt: "Execute plan at Docs/plans/test-integration/scenario-4.2.2-ready-plan.md"
})

Expected Input Acceptance:
- Executor receives plan_file parameter
- Executor reads plan successfully
- Executor identifies first task to execute
```

**Step 4: Verify executor receives correct plan path**

```markdown
Action: Monitor executor execution for plan file access
Expected Behavior:
- Executor reads plan coordinator file
- Executor parses task structure (### X.Y sections)
- Executor identifies deepest uncompleted task
- No file path resolution errors
```

**Step 5: Verify executor begins task execution**

```markdown
Action: Check executor output for task execution start
Expected Output Format:
---
## Task Execution: {task_name}

**Task Path**: {plan_file}#{task_section}
**Task Status**: EXECUTING

[Implementation work begins]
---

Validation Checklist:
- [ ] Executor identifies correct first task
- [ ] Executor begins implementation work
- [ ] No errors related to plan file reading
```

#### Expected Outcome

**Handoff Success**: YES
- Validator → Executor handoff smooth
- Status interpretation correct (READY → execute)
- No blocking errors

**Parameter Compatibility**: YES
- plan_file parameter format accepted
- File path resolution works
- Task identification successful

**Execution Start**: YES
- Executor begins task execution
- Correct task identified (deepest first)
- Implementation work starts

#### Integration Issues to Check

**Issue 1: Status Interpretation Mismatch**

**Symptom**: Executor receives READY plan but doesn't start execution
**Root Cause**: Executor expects different status format (READY vs. Ready vs. ready)
**Detection**:
```
Validator output: Status: READY
Executor input: Status: {parsed_status}
Executor logic: if status == "ready" then execute (case-sensitive mismatch)
Result: Executor skips execution
```
**Resolution**: Standardize status format (uppercase READY) or implement case-insensitive parsing

**Issue 2: Parameter Format Incompatibility**

**Symptom**: Executor cannot parse plan_file parameter
**Root Cause**: Validator recommends `plan_file`, executor expects `plan_path`
**Detection**:
```
Validator recommendation:
  Parameters:
    plan_file: "Docs/plans/test-integration/scenario-4.2.2-ready-plan.md"

Executor receives:
  plan_path: undefined
  plan_file: "Docs/plans/test-integration/scenario-4.2.2-ready-plan.md"

Executor logic: reads plan_path parameter only
Result: File not found error
```
**Resolution**: Align parameter naming across all agents (standardize on `plan_file` or `plan_path`)

**Issue 3: Recommendation Priority Confusion**

**Symptom**: Validator recommends executor as RECOMMENDED but should be CRITICAL
**Root Cause**: READY plans MUST be executed, so priority should be CRITICAL, not RECOMMENDED
**Detection**:
```
Validator recommendation:
  ⚠️ RECOMMENDED: plan-task-executor

Transition Matrix Rule (line 174):
  plan-readiness-validator → plan-task-executor (if ≥90%)
  Priority: Not explicitly stated, but execution is mandatory

Result: Priority ambiguity
```
**Resolution**: Clarify transition matrix - READY plans → executor should be CRITICAL

#### Test Result Template

```markdown
## Integration Test Result: Scenario 4.2.2

**Test Date**: {timestamp}
**Test Status**: {PASS/FAIL/BLOCKED}

### Test Execution Summary

**Step 1: Validator invocation**: {PASS/FAIL}
- Status: {READY/REQUIRES_IMPROVEMENT}
- Score: {score}/100
- Expected: READY (≥90%)

**Step 2: Recommendation verification**: {PASS/FAIL}
- plan-task-executor recommended: {yes/no}
- Priority level: {CRITICAL/RECOMMENDED}
- plan_file parameter included: {yes/no}

**Step 3: Executor invocation**: {PASS/FAIL}
- Executor received plan_file: {yes/no}
- Executor read plan: {yes/no}

**Step 4: Plan path resolution**: {PASS/FAIL}
- File path resolved: {yes/no}
- Task structure parsed: {yes/no}

**Step 5: Execution start**: {PASS/FAIL}
- First task identified: {task_name}
- Execution began: {yes/no}

### Integration Issues Encountered

{issues_list}

### Overall Test Result

**Status**: {PASS/FAIL}
**Handoff Success**: {yes/no}
**Status Interpretation Correct**: {yes/no}
**Execution Started**: {yes/no}
```

---

### Scenario 4.2.3: plan-readiness-validator → architecture-documenter Flow

**Test ID**: INTEGRATION-4.2.3
**Purpose**: Validate that plans with architectural components trigger architecture-documenter correctly
**Upstream Agent**: plan-readiness-validator
**Downstream Agent**: architecture-documenter
**Agent Transition Matrix Reference**: Lines 171, 181 (plan-readiness-validator → architecture-documenter CRITICAL if arch changes)

#### Test Setup

**Preconditions**:
- Plan with architectural components available
- plan-readiness-validator architectural component detection functional
- architecture-documenter agent specification exists
- Test directory: `Docs/plans/test-integration/`

**Test Plan File**:
- Path: `Docs/plans/test-integration/scenario-4.2.3-architecture-plan.md`
- Characteristics:
  - Contains Entity pattern: UserEntity → DbContext → OnModelCreating → Migration
  - Contains Service pattern: IAuthService → AuthService → DI registration
  - Contains Controller pattern: AuthController → Authorization → Middleware
  - Expected architectural components: 5 (UserEntity, IAuthService, AuthService, AuthController, AuthMiddleware)
  - Expected score: 91/100 (READY)

#### Test Steps

**Step 1: Run validator on plan with Entity/Service/Controller components**

```markdown
Action: Invoke plan-readiness-validator
Command: Task({
  subagent_type: "plan-readiness-validator",
  prompt: "Validate plan at Docs/plans/test-integration/scenario-4.2.3-architecture-plan.md"
})

Expected Architectural Component Detection:
- Entity pattern detected: UserEntity (task includes DbContext, OnModelCreating, migration)
- Service pattern detected: IAuthService + AuthService (interface + implementation + DI)
- Controller pattern detected: AuthController (controller + authorization + middleware)
```

**Step 2: Verify validator detects architectural components (keyword detection)**

```markdown
Action: Check validator output for architectural component detection
Expected Output Section:
---
### Architectural Components Detected

**Count**: 5 components
**Components**:
1. UserEntity (Entity pattern)
2. IAuthService (Interface)
3. AuthService (Service implementation)
4. AuthController (API Controller)
5. AuthMiddleware (Middleware)

**Detection Criteria**:
- Entity pattern: Task mentions "DbContext", "OnModelCreating", "migration"
- Service pattern: Task mentions "interface", "implementation", "DI registration"
- Controller pattern: Task mentions "controller", "authorization", "middleware"
---

Validation Checklist:
- [ ] Component count correct (expected 5)
- [ ] Entity pattern detected via keywords
- [ ] Service pattern detected via keywords
- [ ] Controller pattern detected via keywords
```

**Step 3: Verify validator recommends architecture-documenter as CRITICAL**

```markdown
Action: Parse validator recommendations
Expected Recommendation Format:
---
🔄 Recommended Next Actions:

1. 🚨 CRITICAL: architecture-documenter
   Reason: Plan contains 5 architectural components requiring planned documentation
   Command: Use Task tool with subagent_type: "architecture-documenter"
   Parameters:
     type: "planned"
     plan_file: "Docs/plans/test-integration/scenario-4.2.3-architecture-plan.md"
     components: ["UserEntity", "IAuthService", "AuthService", "AuthController", "AuthMiddleware"]
---

Validation Checklist:
- [ ] architecture-documenter listed as CRITICAL
- [ ] type parameter set to "planned"
- [ ] plan_file parameter included
- [ ] components list provided
```

**Step 4: Invoke architecture-documenter with recommended parameters**

```markdown
Action: Use Task tool to invoke architecture-documenter
Command: Task({
  subagent_type: "architecture-documenter",
  prompt: `Document planned architecture from plan at Docs/plans/test-integration/scenario-4.2.3-architecture-plan.md
           Type: planned
           Components: UserEntity, IAuthService, AuthService, AuthController, AuthMiddleware`
})

Expected Input Acceptance:
- Documenter receives type="planned" parameter
- Documenter receives plan_file parameter
- Documenter receives components list
- Documenter reads plan to extract architectural context
```

**Step 5: Verify documenter creates planned architecture docs**

```markdown
Action: Check documenter output for created documentation files
Expected Output:
- Docs/Architecture/Planned/UserEntity.md (Entity architecture)
- Docs/Architecture/Planned/IAuthService.md (Interface architecture)
- Docs/Architecture/Planned/AuthService.md (Service architecture)
- Docs/Architecture/Planned/AuthController.md (Controller architecture)
- Docs/Architecture/Planned/AuthMiddleware.md (Middleware architecture)

File Content Template:
---
# {Component} - Planned Architecture

**Source Plan**: {plan_file}
**Component Type**: {Entity/Service/Controller/Middleware}
**Status**: Planned (not yet implemented)

## Architectural Context
[Extracted from plan]

## Integration Points
[Extracted from plan]

## Implementation Notes
[Extracted from plan TODOs]
---

Validation Checklist:
- [ ] 5 documentation files created
- [ ] Files located in Docs/Architecture/Planned/
- [ ] Content extracted from plan correctly
```

#### Expected Outcome

**Handoff Success**: YES
- Validator → Documenter handoff smooth
- Architectural component detection accurate
- Documentation files created

**Parameter Compatibility**: YES
- type="planned" parameter passed correctly
- plan_file parameter accepted
- components list parsed successfully

**Documentation Created**: YES
- 5 architecture docs created
- Files in correct location (Docs/Architecture/Planned/)
- Content extracted from plan

#### Integration Issues to Check

**Issue 1: Architectural Component Detection Accuracy**

**Symptom**: Validator detects wrong number of components (e.g., 3 instead of 5)
**Root Cause**: Keyword detection too strict or too loose
**Detection**:
```
Plan contains:
- UserEntity task with "DbContext", "OnModelCreating", "migration" keywords ✓
- IAuthService task with "interface" keyword only (missing "implementation", "DI") ❌
- AuthService task with "implementation", "DI" keywords only (missing "interface") ❌

Validator logic:
- Service pattern requires ALL keywords: "interface" + "implementation" + "DI"
- IAuthService missing "implementation", "DI" → not detected
- AuthService missing "interface" → not detected

Result: 3 components detected (UserEntity, AuthController, AuthMiddleware), 2 missed (IAuthService, AuthService)
```
**Resolution**: Adjust keyword detection logic to OR patterns, not strict AND

**Issue 2: Parameter Passing Format Mismatch**

**Symptom**: Documenter receives components list but cannot parse it
**Root Cause**: Validator passes array, documenter expects comma-separated string
**Detection**:
```
Validator recommendation:
  components: ["UserEntity", "IAuthService", "AuthService", "AuthController", "AuthMiddleware"]

Documenter receives:
  components: "['UserEntity', 'IAuthService', 'AuthService', 'AuthController', 'AuthMiddleware']" (string representation)

Documenter logic: splits by comma inside string, fails to parse
Result: Documenter processes 1 component instead of 5
```
**Resolution**: Standardize parameter format (use comma-separated string or ensure JSON array parsing)

**Issue 3: type="planned" Not Propagated**

**Symptom**: Documenter creates docs in Docs/Architecture/Implemented/ instead of /Planned/
**Root Cause**: type parameter not passed or defaulted to "implemented"
**Detection**:
```
Validator recommendation:
  type: "planned"

Documenter receives:
  type: undefined (parameter not propagated)

Documenter logic: if type undefined, default to "implemented"
Result: Files created in wrong directory
```
**Resolution**: Ensure all parameters from validator recommendations propagate to Task tool invocation

#### Test Result Template

```markdown
## Integration Test Result: Scenario 4.2.3

**Test Date**: {timestamp}
**Test Status**: {PASS/FAIL/BLOCKED}

### Test Execution Summary

**Step 1: Validator invocation**: {PASS/FAIL}
- Architectural components detected: {count} (expected 5)
- Components: {components_list}

**Step 2: Component detection verification**: {PASS/FAIL}
- Entity pattern detected: {yes/no}
- Service pattern detected: {yes/no}
- Controller pattern detected: {yes/no}
- Detection accuracy: {percentage}%

**Step 3: Recommendation verification**: {PASS/FAIL}
- architecture-documenter recommended: {yes/no}
- Priority: {CRITICAL/RECOMMENDED} (expected CRITICAL)
- type parameter: {planned/implemented} (expected planned)
- components list included: {yes/no}

**Step 4: Documenter invocation**: {PASS/FAIL}
- type parameter received: {yes/no}
- plan_file parameter received: {yes/no}
- components list parsed: {count} components

**Step 5: Documentation creation**: {PASS/FAIL}
- Files created: {count} (expected 5)
- Files location: {actual_directory} (expected Docs/Architecture/Planned/)
- Content quality: {good/poor}

### Integration Issues Encountered

{issues_list}

### Overall Test Result

**Status**: {PASS/FAIL}
**Component Detection Accurate**: {yes/no}
**Parameter Compatibility**: {yes/no}
**Documentation Created**: {yes/no}
```

---

### Scenario 4.2.4: plan-readiness-validator → parallel-plan-optimizer Flow

**Test ID**: INTEGRATION-4.2.4
**Purpose**: Validate that plans with >5 tasks trigger parallel-plan-optimizer recommendation
**Upstream Agent**: plan-readiness-validator
**Downstream Agent**: parallel-plan-optimizer
**Agent Transition Matrix Reference**: Lines 171, 182 (plan-readiness-validator → parallel-plan-optimizer RECOMMENDED if >5 tasks)

#### Test Setup

**Preconditions**:
- Plan with >5 tasks available
- plan-readiness-validator task counting logic functional
- parallel-plan-optimizer agent specification exists
- Test directory: `Docs/plans/test-integration/`

**Test Plan File**:
- Path: `Docs/plans/test-integration/scenario-4.2.4-large-plan.md`
- Characteristics:
  - 8 tasks total (exceeds 5-task threshold)
  - Well-structured (expected score 90/100)
  - Mix of independent and dependent tasks
  - Expected parallelization opportunity: 3-4 tasks can run in parallel

#### Test Steps

**Step 1: Run validator on plan with 8+ tasks**

```markdown
Action: Invoke plan-readiness-validator
Command: Task({
  subagent_type: "plan-readiness-validator",
  prompt: "Validate plan at Docs/plans/test-integration/scenario-4.2.4-large-plan.md"
})

Expected Task Counting:
- Validator uses Grep or pattern matching to find ### X.Y sections
- Pattern: ^### \d+\.\d+ (regex for task sections)
- Expected count: 8 tasks
```

**Step 2: Verify validator counts tasks correctly (### X.Y sections)**

```markdown
Action: Check validator output for task count
Expected Output Section:
---
### Task Analysis

**Total Tasks**: 8
**Task Sections Found**:
- ### 1.1 Setup Database Schema
- ### 1.2 Create Entity Classes
- ### 2.1 Implement UserService
- ### 2.2 Implement AuthService
- ### 3.1 Create API Controllers
- ### 3.2 Add Authentication Middleware
- ### 4.1 Write Unit Tests
- ### 4.2 Write Integration Tests

**Task Pattern**: ^### \d+\.\d+
**Threshold for Parallelization**: >5 tasks
**Parallelization Triggered**: YES (8 > 5)
---

Validation Checklist:
- [ ] Task count accurate (expected 8)
- [ ] Pattern matching correct (### X.Y format)
- [ ] Threshold comparison correct (8 > 5)
```

**Step 3: Verify validator recommends parallel-plan-optimizer as RECOMMENDED**

```markdown
Action: Parse validator recommendations
Expected Recommendation Format:
---
🔄 Recommended Next Actions:

2. ⚠️ RECOMMENDED: parallel-plan-optimizer
   Reason: Plan contains 8 tasks (exceeds 5-task threshold for parallelization)
   Command: Use Task tool with subagent_type: "parallel-plan-optimizer"
   Parameters:
     plan_file: "Docs/plans/test-integration/scenario-4.2.4-large-plan.md"
     task_count: 8
   Estimated Time Reduction: 40% (sequential: 120 min → parallel: 72 min)
---

Validation Checklist:
- [ ] parallel-plan-optimizer listed as RECOMMENDED
- [ ] Priority level: RECOMMENDED (not CRITICAL - parallelization is optional)
- [ ] task_count parameter included
- [ ] Time reduction estimate provided
```

**Step 4: Invoke parallel-plan-optimizer with recommended parameters**

```markdown
Action: Use Task tool to invoke parallel-plan-optimizer
Command: Task({
  subagent_type: "parallel-plan-optimizer",
  prompt: `Analyze plan at Docs/plans/test-integration/scenario-4.2.4-large-plan.md for parallelization opportunities
           Task count: 8`
})

Expected Input Acceptance:
- Optimizer receives plan_file parameter
- Optimizer receives task_count parameter
- Optimizer reads plan and analyzes task dependencies
```

**Step 5: Verify optimizer analyzes plan for parallelization opportunities**

```markdown
Action: Check optimizer output for parallel execution strategy
Expected Output Format:
---
## Parallel Execution Strategy

**Plan**: Docs/plans/test-integration/scenario-4.2.4-large-plan.md
**Total Tasks**: 8
**Sequential Execution Time**: ~120 minutes
**Parallel Execution Time**: ~72 minutes
**Time Reduction**: 48 minutes (40%)

### Parallel Execution Groups

**Group 1 (Parallel)**:
- Task 1.1: Setup Database Schema
- Task 1.2: Create Entity Classes

**Group 2 (Sequential after Group 1)**:
- Task 2.1: Implement UserService (depends on 1.2)
- Task 2.2: Implement AuthService (depends on 1.2)

**Group 3 (Parallel)**:
- Task 3.1: Create API Controllers (depends on 2.1, 2.2)
- Task 3.2: Add Authentication Middleware (depends on 2.2)

**Group 4 (Sequential after Group 3)**:
- Task 4.1: Write Unit Tests (depends on all above)
- Task 4.2: Write Integration Tests (depends on all above)
---

Validation Checklist:
- [ ] Dependency analysis performed
- [ ] Parallel execution groups identified
- [ ] Time reduction calculated
- [ ] Strategy actionable
```

#### Expected Outcome

**Handoff Success**: YES
- Validator → Optimizer handoff smooth
- Task count propagated correctly
- Optimizer generates strategy

**Task Counting Accuracy**: YES
- 8 tasks counted correctly
- Pattern matching works (### X.Y)
- Threshold comparison correct (8 > 5)

**Parallelization Strategy Generated**: YES
- Optimizer analyzes dependencies
- Parallel groups identified
- Time reduction estimated

#### Integration Issues to Check

**Issue 1: Task Counting Accuracy**

**Symptom**: Validator counts wrong number of tasks (e.g., 6 instead of 8)
**Root Cause**: Pattern matching misses nested tasks or false positives
**Detection**:
```
Plan structure:
- ### 1.1 Setup Database Schema ✓
- ### 1.2 Create Entity Classes ✓
- #### 1.2.1 Create UserEntity (nested, should NOT count as separate task) ❌
- ### 2.1 Implement UserService ✓
- ## 3.0 Phase 3 Overview (### pattern but not a task, should NOT count) ❌

Validator pattern: ^### \d+\.\d+
Validator count: 6 tasks (includes 1.2.1, excludes 3.0)

Correct count: 8 tasks (exclude nested ####, exclude non-task ###)
```
**Resolution**: Refine pattern to match ONLY top-level task sections (### X.Y format exactly)

**Issue 2: Time Reduction Estimation Formula**

**Symptom**: Time reduction estimate inaccurate (50% instead of 40%)
**Root Cause**: Validator uses simple formula without considering dependencies
**Detection**:
```
Validator formula: time_reduction = (task_count - 5) / task_count
Validator estimate: (8 - 5) / 8 = 37.5% ≈ 40% ✓

Optimizer analysis: Only 3 tasks can run in parallel (out of 8)
Optimizer estimate: 3 parallel + 5 sequential = 25% time reduction

Discrepancy: Validator 40%, Optimizer 25%
```
**Resolution**: Document that validator estimate is approximate, optimizer provides accurate analysis

**Issue 3: Recommendation Priority Confusion**

**Symptom**: Validator recommends optimizer as CRITICAL instead of RECOMMENDED
**Root Cause**: Misinterpretation of transition matrix rule
**Detection**:
```
Validator recommendation: 🚨 CRITICAL: parallel-plan-optimizer

Transition Matrix Rule (line 171):
  work-plan-architect → parallel-plan-optimizer (>5 tasks) RECOMMENDED

Correct priority: RECOMMENDED (parallelization is optimization, not mandatory)
```
**Resolution**: Clarify that parallelization is RECOMMENDED, not CRITICAL

#### Test Result Template

```markdown
## Integration Test Result: Scenario 4.2.4

**Test Date**: {timestamp}
**Test Status**: {PASS/FAIL/BLOCKED}

### Test Execution Summary

**Step 1: Validator invocation**: {PASS/FAIL}
- Task count detected: {count} (expected 8)
- Threshold check: {count} > 5 = {yes/no}

**Step 2: Task counting verification**: {PASS/FAIL}
- Pattern used: {pattern}
- Tasks matched: {task_list}
- Counting accuracy: {percentage}%

**Step 3: Recommendation verification**: {PASS/FAIL}
- parallel-plan-optimizer recommended: {yes/no}
- Priority: {CRITICAL/RECOMMENDED} (expected RECOMMENDED)
- task_count parameter included: {yes/no}
- Time reduction estimate: {percentage}%

**Step 4: Optimizer invocation**: {PASS/FAIL}
- plan_file received: {yes/no}
- task_count received: {count}

**Step 5: Strategy generation**: {PASS/FAIL}
- Dependency analysis performed: {yes/no}
- Parallel groups identified: {count} groups
- Time reduction calculated: {percentage}%

### Integration Issues Encountered

{issues_list}

### Overall Test Result

**Status**: {PASS/FAIL}
**Task Counting Accurate**: {yes/no}
**Recommendation Priority Correct**: {yes/no}
**Strategy Generated**: {yes/no}
```

---

### Scenario 4.2.5: Iteration Cycle - plan-readiness-validator ↔ work-plan-architect (REQUIRES_IMPROVEMENT)

**Test ID**: INTEGRATION-4.2.5
**Purpose**: Validate that failed validation cycles back to architect for revision with correct iteration limit enforcement
**Agents**: plan-readiness-validator ↔ work-plan-architect (bidirectional iteration)
**Agent Transition Matrix Reference**: Lines 171-173 (validator → architect if <90%, architect → validator after revision)
**Cycle Protection Reference**: AGENTS_ARCHITECTURE.md lines 339-349 (Quality Cycle: max 3 iterations)

#### Test Setup

**Preconditions**:
- Problematic plan available (expected score <90%)
- plan-readiness-validator issue detection functional
- work-plan-architect revision capability exists
- Test directory: `Docs/plans/test-integration/`

**Test Plan File (Initial)**:
- Path: `Docs/plans/test-integration/scenario-4.2.5-iteration-v1.md`
- Characteristics:
  - Missing DbContext integration for Entity tasks
  - Some tasks vague (no file paths)
  - Expected score: 72/100 (REQUIRES_IMPROVEMENT)

#### Test Steps

**Iteration 1: Validator → Architect (First Failure)**

**Step 1.1: Run validator on problematic plan (expected REQUIRES_IMPROVEMENT)**

```markdown
Action: Invoke plan-readiness-validator
Command: Task({
  subagent_type: "plan-readiness-validator",
  prompt: "Validate plan at Docs/plans/test-integration/scenario-4.2.5-iteration-v1.md",
  cycle_tracking: {
    cycle_id: "validator-architect-test-4.2.5",
    iteration_count: 1,
    max_iterations: 3
  }
})

Expected Output:
- Status: REQUIRES_IMPROVEMENT
- Score: 70-75/100 (below 90% threshold)
- Detailed issues list generated
```

**Step 1.2: Verify validator generates detailed issue list**

```markdown
Action: Check validator output for issues
Expected Issues List Format:
---
### Issues Found

**Total Issues**: 8
**Breakdown**:
- CRITICAL: 3
- IMPORTANT: 5

**Issue Details**:

1. 🔴 CRITICAL: Task 2.1 missing DbContext integration
   - **Location**: scenario-4.2.5-iteration-v1.md line 78
   - **Task**: "Create UserEntity class"
   - **Problem**: No DbContext.DbSet registration mentioned
   - **Fix**: Add step for DbContext.Users DbSet registration

2. 🔴 CRITICAL: Task 2.1 missing OnModelCreating configuration
   - **Location**: scenario-4.2.5-iteration-v1.md line 78
   - **Problem**: No Entity configuration in OnModelCreating
   - **Fix**: Add OnModelCreating configuration step

3. 🔴 CRITICAL: Task 2.1 missing migration command
   - **Location**: scenario-4.2.5-iteration-v1.md line 78
   - **Problem**: No EF Core migration creation command
   - **Fix**: Add "dotnet ef migrations add CreateUserEntity" command

[... additional issues ...]
---

Validation Checklist:
- [ ] Issue count accurate
- [ ] Each issue has location (file:line)
- [ ] Each issue has problem description
- [ ] Each issue has fix recommendation
- [ ] Issues prioritized (CRITICAL/IMPORTANT)
```

**Step 1.3: Verify validator recommends work-plan-architect with issues_list parameter**

```markdown
Action: Parse validator recommendations
Expected Recommendation Format:
---
🔄 Recommended Next Actions:

1. 🚨 CRITICAL: work-plan-architect
   Reason: Plan scored 72/100 (below 90% threshold) - requires revision
   Command: Use Task tool with subagent_type: "work-plan-architect"
   Parameters:
     revision_mode: true
     plan_file: "Docs/plans/test-integration/scenario-4.2.5-iteration-v1.md"
     issues_list: [
       "Task 2.1 missing DbContext integration",
       "Task 2.1 missing OnModelCreating configuration",
       "Task 2.1 missing migration command",
       ...
     ]
   Cycle Tracking:
     cycle_id: "validator-architect-test-4.2.5"
     iteration_count: 1
     max_iterations: 3

🔄 Cycle Tracking: Iteration 1/3 (validator → architect)
---

Validation Checklist:
- [ ] work-plan-architect recommended as CRITICAL
- [ ] revision_mode parameter set to true
- [ ] issues_list parameter included
- [ ] Cycle tracking metadata present
- [ ] Iteration count: 1/3
```

**Step 1.4: Invoke work-plan-architect with issues for revision**

```markdown
Action: Use Task tool to invoke work-plan-architect for revision
Command: Task({
  subagent_type: "work-plan-architect",
  prompt: `Revise plan at Docs/plans/test-integration/scenario-4.2.5-iteration-v1.md
           Address issues:
           - Task 2.1 missing DbContext integration
           - Task 2.1 missing OnModelCreating configuration
           - Task 2.1 missing migration command
           [... all issues from validator]`,
  cycle_tracking: {
    cycle_id: "validator-architect-test-4.2.5",
    iteration_count: 1,
    max_iterations: 3
  }
})

Expected Input Acceptance:
- Architect receives revision_mode=true
- Architect receives issues_list
- Architect reads original plan
- Architect applies fixes
```

**Step 1.5: Verify architect revises plan addressing issues**

```markdown
Action: Check architect output for revised plan
Expected Revision Actions:
- Architect reads scenario-4.2.5-iteration-v1.md
- Architect identifies problematic sections (line 78: Task 2.1)
- Architect adds missing steps:
  - DbContext.Users DbSet registration
  - OnModelCreating configuration for UserEntity
  - Migration command: dotnet ef migrations add CreateUserEntity
- Architect saves revised plan as scenario-4.2.5-iteration-v2.md

Validation Checklist:
- [ ] Revised plan file created (v2.md)
- [ ] All CRITICAL issues addressed
- [ ] Most IMPORTANT issues addressed (≥80%)
- [ ] No new violations introduced
```

**Iteration 2: Validator → Architect (Partial Improvement)**

**Step 2.1: Run validator again on revised plan**

```markdown
Action: Invoke plan-readiness-validator on revised plan
Command: Task({
  subagent_type: "plan-readiness-validator",
  prompt: "Validate plan at Docs/plans/test-integration/scenario-4.2.5-iteration-v2.md",
  cycle_tracking: {
    cycle_id: "validator-architect-test-4.2.5",
    iteration_count: 2,
    max_iterations: 3
  }
})

Expected Score Improvement:
- Original score: 72/100
- Revised score: 85-88/100 (improved but still <90%)
- Status: Still REQUIRES_IMPROVEMENT (borderline)
```

**Step 2.2: Verify score improvement after revision**

```markdown
Action: Compare scores between iterations
Expected Score Progression:
---
### Iteration Score Tracking

| Iteration | Score | Status | Issues Found | Issues Fixed |
|-----------|-------|--------|--------------|--------------|
| 1         | 72/100| REQUIRES_IMPROVEMENT | 8 | 0 |
| 2         | 87/100| REQUIRES_IMPROVEMENT | 3 | 5 |

**Score Improvement**: +15 points (72 → 87)
**Issues Resolved**: 5/8 (62.5%)
**Remaining Issues**: 3 (all IMPORTANT, no CRITICAL)
---

Validation Checklist:
- [ ] Score improved (72 → 87)
- [ ] All CRITICAL issues resolved
- [ ] Some IMPORTANT issues remain
- [ ] Still below 90% threshold
```

**Step 2.3: Verify cycle continues (iteration 2 → 3)**

```markdown
Action: Check validator recommendations for iteration 2
Expected Recommendation:
---
🔄 Recommended Next Actions:

1. 🚨 CRITICAL: work-plan-architect
   Reason: Plan scored 87/100 (below 90% threshold) - requires minor revision
   Cycle Tracking:
     iteration_count: 2
     max_iterations: 3

   🔄 Cycle Tracking: Iteration 2/3 (validator → architect)
   ⚠️ Approaching iteration limit - escalation at 3 iterations

   Issues Progress:
   - Iteration 1: 8 issues found → 0 fixed (0% resolution)
   - Iteration 2: 3 issues remaining → 5 fixed (62.5% resolution)
   - **Remaining**: 3 unresolved issues
---

Validation Checklist:
- [ ] Iteration count incremented (1 → 2)
- [ ] Progress tracking shown
- [ ] Warning about approaching limit
```

**Iteration 3: Validator → Pass (Success)**

**Step 3.1: Run validator after final revision**

```markdown
Action: Invoke plan-readiness-validator on final revision
Command: Task({
  subagent_type: "plan-readiness-validator",
  prompt: "Validate plan at Docs/plans/test-integration/scenario-4.2.5-iteration-v3.md",
  cycle_tracking: {
    cycle_id: "validator-architect-test-4.2.5",
    iteration_count: 3,
    max_iterations: 3
  }
})

Expected Final Score:
- Revised score: 91-93/100 (above 90% threshold)
- Status: READY
- Cycle completed successfully
```

**Step 3.2: Verify cycle completes successfully or escalates**

```markdown
Action: Check validator output for cycle completion
Expected Output (Success Case):
---
## Validation Report

**Status**: READY
**Score**: 92/100

### Cycle Completion

**Cycle ID**: validator-architect-test-4.2.5
**Total Iterations**: 3
**Final Status**: SUCCESS
**Score Progression**: 72 → 87 → 92 (+20 points)

✅ Plan now meets quality threshold (≥90%)

🔄 Recommended Next Actions:

1. ⚠️ RECOMMENDED: plan-task-executor
   Reason: Plan validated as READY
---

Expected Output (Escalation Case):
---
⚠️ CYCLE LIMIT REACHED - ESCALATION TO USER ⚠️

Cycle: validator ↔ architect
Iterations completed: 3/3 (limit reached)
Duration: 45 minutes
Final Score: 88/100 (still below 90% threshold)

UNRESOLVED ISSUES:
- Issue 1: Task decomposition still too high (35 tool calls, limit 30)
  Attempted fixes: Split task into 2 subtasks
  Why failed: Subtasks still complex, may need 3-way split

RECOMMENDED ACTIONS:
- Manual intervention required for task decomposition
- Consider alternative approach: Simplify feature scope
- Consult with architect for complexity reduction strategy
---

Validation Checklist:
- [ ] Cycle completion status clear
- [ ] Success: recommends plan-task-executor
- [ ] Escalation: provides detailed unresolved issues report
- [ ] Escalation: provides actionable recommendations
```

#### Expected Outcome

**Iteration Cycle Success**: YES
- Validator → Architect → Validator cycle works
- Score improves with each iteration (72 → 87 → 92)
- Cycle completes within 3 iterations

**Issue List Compatibility**: YES
- Architect can parse issues_list
- Architect addresses issues correctly
- Issue count decreases with iterations

**Cycle Limit Enforcement**: YES
- Max 3 iterations enforced
- Escalation triggered if limit reached
- User receives actionable report

#### Integration Issues to Check

**Issue 1: Issue List Format Incompatibility**

**Symptom**: Architect cannot parse issues_list from validator
**Root Cause**: Validator provides structured object, architect expects plain text
**Detection**:
```
Validator issues_list format:
  [
    {
      "location": "scenario-4.2.5-iteration-v1.md:78",
      "severity": "CRITICAL",
      "problem": "Missing DbContext integration",
      "fix": "Add DbContext.Users DbSet registration"
    },
    ...
  ]

Architect receives:
  issues_list: "[{\"location\":\"scenario-4.2.5-iteration-v1.md:78\",...}]" (JSON string)

Architect logic: expects plain text list, cannot parse JSON
Result: Architect does not address issues correctly
```
**Resolution**: Standardize issues_list format (use plain text list or ensure JSON parsing)

**Issue 2: Iteration Limit Not Enforced**

**Symptom**: Cycle continues beyond 3 iterations (4th, 5th iteration)
**Root Cause**: Cycle tracking metadata not propagated or not checked
**Detection**:
```
Iteration 3 completes with score 88/100 (still REQUIRES_IMPROVEMENT)

Validator recommendation:
  🚨 CRITICAL: work-plan-architect (iteration 4)

Expected behavior: Escalation at iteration 3
Actual behavior: Cycle continues to iteration 4

Root cause: Validator does not check iteration_count against max_iterations
```
**Resolution**: Implement explicit iteration limit check in validator before recommending architect

**Issue 3: Score Convergence Failure**

**Symptom**: Score does not improve or worsens with revisions
**Root Cause**: Architect fixes introduce new violations or misinterpret issues
**Detection**:
```
Score progression:
- Iteration 1: 72/100 (8 issues)
- Iteration 2: 70/100 (10 issues) → WORSE!
- Iteration 3: 68/100 (12 issues) → WORSE!

Root cause:
- Architect adds DbContext integration but introduces new catalogization violations
- Each fix creates 1-2 new issues

Result: Cycle never converges to ≥90%
```
**Resolution**: Architect must validate fixes do not introduce new violations before saving

#### Test Result Template

```markdown
## Integration Test Result: Scenario 4.2.5

**Test Date**: {timestamp}
**Test Status**: {PASS/FAIL/BLOCKED}

### Test Execution Summary

**Iteration 1**:
- Validator score: {score}/100 (expected <90%)
- Issues found: {count}
- Architect revision: {success/failure}
- Issues addressed: {count}/{total}

**Iteration 2**:
- Validator score: {score}/100
- Score improvement: {delta} points
- Issues remaining: {count}
- Architect revision: {success/failure}

**Iteration 3**:
- Validator score: {score}/100
- Final status: {READY/REQUIRES_IMPROVEMENT}
- Cycle result: {SUCCESS/ESCALATION}

### Cycle Metrics

**Total Iterations**: {count} (limit: 3)
**Score Progression**: {score1} → {score2} → {score3}
**Total Score Improvement**: {delta} points
**Issues Resolved**: {count}/{total} ({percentage}%)

### Integration Issues Encountered

{issues_list}

### Overall Test Result

**Status**: {PASS/FAIL}
**Iteration Cycle Works**: {yes/no}
**Issue List Compatible**: {yes/no}
**Cycle Limit Enforced**: {yes/no}
**Score Convergence**: {yes/no}
```

---

## Agent Handoff Verification Checklist

### File Path and Parameter Compatibility

- [ ] **Absolute vs. Relative Path Handling**
  - [ ] All agents accept both absolute and relative paths
  - [ ] Path resolution consistent (relative to project root)
  - [ ] Path format standardized (use forward slashes or backslashes consistently)

- [ ] **Parameter Naming Consistency**
  - [ ] `plan_file` parameter used uniformly (not `plan_path`, `file`, `plan`)
  - [ ] `issues_list` parameter format agreed (array vs. string)
  - [ ] `type` parameter standardized (`planned` vs. `Planned` vs. `PLANNED`)
  - [ ] `task_count` parameter format consistent (integer vs. string)

- [ ] **Glob Pattern Consistency**
  - [ ] Phase file discovery patterns align with catalogization rules
  - [ ] Glob patterns match GOLDEN RULES #1 and #2 (coordinator outside directory)
  - [ ] Pattern: `{plan-directory}/**/*.md` works correctly

### Status and Recommendation Compatibility

- [ ] **Status Format Standardization**
  - [ ] READY status uppercase consistently
  - [ ] REQUIRES_IMPROVEMENT status uppercase consistently
  - [ ] Case-sensitive status comparison avoided (use case-insensitive)

- [ ] **Recommendation Priority Levels**
  - [ ] CRITICAL recommendations clearly marked (🚨)
  - [ ] RECOMMENDED recommendations clearly marked (⚠️)
  - [ ] OPTIONAL recommendations clearly marked (💡)
  - [ ] Priority interpretation consistent across agents

- [ ] **Recommendation Format Parsing**
  - [ ] Agents can extract next agent name from recommendations
  - [ ] Agents can extract parameters from recommendations
  - [ ] Command format standardized (`Use Task tool with subagent_type: "{agent-name}"`)

### Context Propagation

- [ ] **Plan Characteristics Propagation**
  - [ ] Task count propagates correctly (validator → optimizer)
  - [ ] Architectural component count propagates (validator → documenter)
  - [ ] Component list propagates correctly (validator → documenter)

- [ ] **Cycle Tracking Metadata**
  - [ ] cycle_id propagates through iteration cycles
  - [ ] iteration_count increments correctly
  - [ ] max_iterations enforced at limit

- [ ] **Issue List Propagation**
  - [ ] issues_list format compatible (validator → architect)
  - [ ] Issue details preserved (location, severity, problem, fix)
  - [ ] Issue count tracked across iterations

### Error Handling

- [ ] **File Access Errors**
  - [ ] Clear error message if plan file not found
  - [ ] Clear error message if phase files missing
  - [ ] Escalation to user if file access fails

- [ ] **Parameter Missing Errors**
  - [ ] Clear error message if required parameter missing
  - [ ] Default values used appropriately
  - [ ] Escalation to user if critical parameter missing

- [ ] **Validation Failure Errors**
  - [ ] REQUIRES_IMPROVEMENT status propagates correctly
  - [ ] Issue list propagates to architect for revision
  - [ ] Escalation to user if max iterations reached

---

## Transition Matrix Validation Checklist

### CRITICAL Paths (Must Be Implemented)

**From plan-readiness-validator:**

- [ ] **validator → plan-task-executor (if READY)**
  - [ ] Condition: Plan score ≥90%
  - [ ] Priority: RECOMMENDED (execution is next logical step)
  - [ ] Parameters: `plan_file`
  - [ ] Test: Scenario 4.2.2 validates this path

- [ ] **validator → work-plan-architect (if REQUIRES_IMPROVEMENT)**
  - [ ] Condition: Plan score <90%
  - [ ] Priority: CRITICAL (revision mandatory before execution)
  - [ ] Parameters: `revision_mode`, `plan_file`, `issues_list`
  - [ ] Iteration limit: Max 3 iterations
  - [ ] Test: Scenario 4.2.5 validates this path

- [ ] **validator → architecture-documenter (if architectural components)**
  - [ ] Condition: Plan contains Entity/Service/Controller patterns
  - [ ] Priority: CRITICAL (architectural planning mandatory)
  - [ ] Parameters: `type="planned"`, `plan_file`, `components`
  - [ ] Detection: Keyword-based (DbContext, DI, controller, etc.)
  - [ ] Test: Scenario 4.2.3 validates this path

**To plan-readiness-validator:**

- [ ] **work-plan-architect → validator**
  - [ ] Condition: Always after plan creation
  - [ ] Priority: RECOMMENDED (validation recommended before execution)
  - [ ] Parameters: `plan_file`
  - [ ] Test: Scenario 4.2.1 validates this path

### RECOMMENDED Paths (Should Be Implemented)

- [ ] **validator → parallel-plan-optimizer (if >5 tasks)**
  - [ ] Condition: Plan has >5 tasks
  - [ ] Priority: RECOMMENDED (optimization optional)
  - [ ] Parameters: `plan_file`, `task_count`
  - [ ] Threshold: >5 tasks triggers recommendation
  - [ ] Test: Scenario 4.2.4 validates this path

### OPTIONAL Paths (Future Implementation)

- [ ] **validator → work-plan-reviewer (manual validation)**
  - [ ] Condition: User requests manual review
  - [ ] Priority: OPTIONAL (automated validation sufficient)
  - [ ] Parameters: `plan_file`
  - [ ] Not tested in current integration test suite

### Cycle Protection Validation

**Quality Cycle: architect ↔ validator**

- [ ] **Max Iterations: 3**
  - [ ] Iteration 1: Initial validation fails → architect revises
  - [ ] Iteration 2: Validation improves but <90% → architect revises again
  - [ ] Iteration 3: Final validation attempt
  - [ ] Escalation: If iteration 3 fails, escalate to user with detailed report

- [ ] **Iteration Tracking**
  - [ ] cycle_id maintained across iterations
  - [ ] iteration_count increments correctly (1 → 2 → 3)
  - [ ] max_iterations checked before recommending next iteration
  - [ ] Warning shown at iteration 2 (approaching limit)

- [ ] **Escalation Mechanism**
  - [ ] Detailed unresolved issues report generated
  - [ ] Actionable recommendations provided
  - [ ] User intervention requested explicitly
  - [ ] Cycle history preserved for analysis

### Transition Matrix Compliance Summary

| From Agent | To Agent | Condition | Priority | Parameters | Test Coverage |
|------------|----------|-----------|----------|------------|---------------|
| work-plan-architect | plan-readiness-validator | Always | RECOMMENDED | plan_file | Scenario 4.2.1 |
| plan-readiness-validator | plan-task-executor | Score ≥90% | RECOMMENDED | plan_file | Scenario 4.2.2 |
| plan-readiness-validator | architecture-documenter | Arch components | CRITICAL | type, plan_file, components | Scenario 4.2.3 |
| plan-readiness-validator | parallel-plan-optimizer | >5 tasks | RECOMMENDED | plan_file, task_count | Scenario 4.2.4 |
| plan-readiness-validator | work-plan-architect | Score <90% | CRITICAL | revision_mode, plan_file, issues_list | Scenario 4.2.5 |

**Coverage**: 5/5 critical paths tested (100%)

---

## Test Execution Procedure

### Preparation Phase

**1. Environment Setup**

```bash
# Create test directory
mkdir -p Docs/plans/test-integration

# Verify agent specifications exist
ls .cursor/agents/plan-readiness-validator/agent.md
ls .cursor/agents/work-plan-architect/agent.md  # if exists
ls .cursor/agents/plan-task-executor/agent.md   # if exists

# Clean previous test artifacts
rm -rf Docs/plans/test-integration/scenario-*
```

**2. Test Plan Creation**

Create test plans for each scenario:
- scenario-4.2.1-architect-output.md (created by architect during test)
- scenario-4.2.2-ready-plan.md (high-quality, score ≥90%)
- scenario-4.2.3-architecture-plan.md (with Entity/Service/Controller)
- scenario-4.2.4-large-plan.md (8 tasks)
- scenario-4.2.5-iteration-v1.md (problematic, score <90%)

**3. Manual Baseline Creation**

Manually validate test plans to establish expected scores:
- scenario-4.2.2-ready-plan.md: Expected 93/100 (READY)
- scenario-4.2.3-architecture-plan.md: Expected 91/100 (READY, 5 components)
- scenario-4.2.4-large-plan.md: Expected 90/100 (READY, 8 tasks)
- scenario-4.2.5-iteration-v1.md: Expected 72/100 (REQUIRES_IMPROVEMENT)

### Execution Phase

**For Each Scenario:**

1. **Pre-Test Checklist**
   - [ ] Test plan file exists
   - [ ] Expected outcome documented
   - [ ] Clean state (no previous run artifacts)

2. **Execute Test Steps**
   - Follow test steps from scenario specification
   - Use Task tool to invoke agents
   - Monitor agent output for errors
   - Capture all agent outputs for analysis

3. **Record Results**
   - Fill in test result template for scenario
   - Document any integration issues encountered
   - Mark test as PASS/FAIL/BLOCKED

4. **Issue Resolution**
   - If integration issue found, document root cause
   - Propose resolution
   - Re-run test after fix (if applicable)

### Analysis Phase

**1. Aggregate Results**

Create summary table:

| Scenario | Test ID | Status | Handoff Success | Parameter Compatibility | Issues Found |
|----------|---------|--------|-----------------|-------------------------|--------------|
| 4.2.1    | INTEGRATION-4.2.1 | {PASS/FAIL} | {yes/no} | {yes/no} | {count} |
| 4.2.2    | INTEGRATION-4.2.2 | {PASS/FAIL} | {yes/no} | {yes/no} | {count} |
| 4.2.3    | INTEGRATION-4.2.3 | {PASS/FAIL} | {yes/no} | {yes/no} | {count} |
| 4.2.4    | INTEGRATION-4.2.4 | {PASS/FAIL} | {yes/no} | {yes/no} | {count} |
| 4.2.5    | INTEGRATION-4.2.5 | {PASS/FAIL} | {yes/no} | {yes/no} | {count} |

**2. Calculate Metrics**

- Handoff success rate: {passing_handoffs} / {total_handoffs} × 100%
- Parameter compatibility rate: {compatible_parameters} / {total_parameters} × 100%
- Error rate: {errors_encountered} / {total_test_steps} × 100%

**3. Identify Patterns**

- Common integration issues across scenarios
- File path resolution patterns
- Parameter format issues
- Recommendation format parsing issues

**4. Generate Integration Test Report**

Use Final Integration Test Report Template (see below).

---

## Final Integration Test Report Template

```markdown
# Plan Readiness Validator - Integration Test Report

**Test Date**: {timestamp}
**Tester**: {tester_name}
**Agent Version**: plan-readiness-validator v1.0
**Test Coverage**: 5 integration scenarios

---

## Executive Summary

**Overall Test Result**: {PASS if all 5 scenarios pass, FAIL otherwise}

**Key Metrics**:
- Handoff Success Rate: {percentage}% (target: 100%)
- Parameter Compatibility Rate: {percentage}% (target: 100%)
- Integration Issues Found: {count}
- Critical Issues: {count}

**Recommendation**: {proceed_to_phase_5 / fix_issues_before_phase_5}

---

## Scenario Results Summary

| Scenario | Agent Flow | Status | Handoff Success | Integration Issues |
|----------|-----------|--------|-----------------|-------------------|
| 4.2.1 | architect → validator | {PASS/FAIL} | {yes/no} | {count} |
| 4.2.2 | validator → executor | {PASS/FAIL} | {yes/no} | {count} |
| 4.2.3 | validator → documenter | {PASS/FAIL} | {yes/no} | {count} |
| 4.2.4 | validator → optimizer | {PASS/FAIL} | {yes/no} | {count} |
| 4.2.5 | validator ↔ architect (cycle) | {PASS/FAIL} | {yes/no} | {count} |

---

## Detailed Scenario Results

### Scenario 4.2.1: work-plan-architect → plan-readiness-validator

**Test Status**: {PASS/FAIL}

**Handoff Success**: {yes/no}
**Parameter Compatibility**: {yes/no}
**File Path Resolution**: {yes/no}

**Issues Encountered**: {count}
{if issues}
1. {issue_description}
   - Root Cause: {root_cause}
   - Resolution: {resolution}
{endif}

---

[Repeat for Scenarios 4.2.2, 4.2.3, 4.2.4, 4.2.5]

---

## Integration Issues Analysis

### Critical Issues (Blocking)

{if critical_issues}
1. **Issue**: {issue_description}
   - **Impact**: {impact}
   - **Affected Scenarios**: {scenario_list}
   - **Root Cause**: {root_cause}
   - **Resolution Required**: {resolution}
   - **Status**: {open/resolved}
{else}
✅ No critical integration issues found
{endif}

### Important Issues (Non-Blocking)

{if important_issues}
1. **Issue**: {issue_description}
   - **Impact**: {impact}
   - **Workaround**: {workaround}
   - **Recommended Fix**: {fix}
{else}
✅ No important integration issues found
{endif}

---

## Transition Matrix Validation Results

**CRITICAL Paths Validated**:
- [ ] architect → validator (Scenario 4.2.1)
- [ ] validator → executor (if READY) (Scenario 4.2.2)
- [ ] validator → documenter (if arch components) (Scenario 4.2.3)
- [ ] validator → architect (if REQUIRES_IMPROVEMENT) (Scenario 4.2.5)

**RECOMMENDED Paths Validated**:
- [ ] validator → optimizer (if >5 tasks) (Scenario 4.2.4)

**Cycle Protection Validated**:
- [ ] architect ↔ validator iteration limit (max 3) (Scenario 4.2.5)
- [ ] Escalation mechanism (Scenario 4.2.5)

**Transition Matrix Compliance**: {percentage}% (target: 100%)

---

## Agent Handoff Checklist Results

### File Path and Parameter Compatibility

- [ ] Absolute vs. relative path handling: {PASS/FAIL}
- [ ] Parameter naming consistency: {PASS/FAIL}
- [ ] Glob pattern consistency: {PASS/FAIL}

### Status and Recommendation Compatibility

- [ ] Status format standardization: {PASS/FAIL}
- [ ] Recommendation priority levels: {PASS/FAIL}
- [ ] Recommendation format parsing: {PASS/FAIL}

### Context Propagation

- [ ] Plan characteristics propagation: {PASS/FAIL}
- [ ] Cycle tracking metadata: {PASS/FAIL}
- [ ] Issue list propagation: {PASS/FAIL}

### Error Handling

- [ ] File access errors: {PASS/FAIL}
- [ ] Parameter missing errors: {PASS/FAIL}
- [ ] Validation failure errors: {PASS/FAIL}

**Overall Checklist Compliance**: {percentage}% (target: 100%)

---

## Performance Observations

**Agent Invocation Times**:
- work-plan-architect: ~{time} seconds
- plan-readiness-validator: ~{time} seconds (target: <60s)
- plan-task-executor: ~{time} seconds
- architecture-documenter: ~{time} seconds
- parallel-plan-optimizer: ~{time} seconds

**Total Integration Test Duration**: {duration} minutes

**Performance Assessment**: {acceptable / needs_optimization}

---

## Recommendations

### For Immediate Action (Before Phase 5)

{if critical_issues}
1. **Fix Critical Issue**: {issue_name}
   - Action: {specific_action}
   - Estimated Effort: {effort_estimate}
{else}
✅ No critical issues requiring immediate action
{endif}

### For Future Improvement (Post-MVP)

1. {improvement_suggestion}
   - Benefit: {benefit}
   - Effort: {effort_estimate}

---

## Conclusion

**Integration Test Result**: {PASS/FAIL}

{if PASS}
✅ **All integration tests passed**
- All agent handoffs work smoothly
- Parameter compatibility confirmed
- Transition matrix compliance achieved
- Cycle protection functional

**Recommendation**: Proceed to Phase 5 (Documentation and Integration)
{else}
⚠️ **Integration tests failed**
- {count} critical issues found
- {count} scenarios failed

**Recommendation**: Address critical issues before proceeding to Phase 5
{endif}

---

**Report Author**: {tester_name}
**Test Date**: {timestamp}
**Next Review**: After issue resolution (if applicable)
```

---

## Acceptance Criteria for Task 4.2

### All Acceptance Criteria Met When:

- [ ] Integration test specification document created (this document)
- [ ] 5 integration scenarios fully documented (4.2.1 - 4.2.5)
- [ ] Agent handoff verification checklist complete
- [ ] Transition matrix validation checklist complete
- [ ] Test execution procedure documented
- [ ] Final integration test report template provided
- [ ] All critical agent paths covered (architect → validator, validator → executor, validator → documenter, validator → optimizer, validator ↔ architect cycle)

### Quality Gates:

- [ ] Each scenario includes test setup, steps, expected outcome, integration issues to check
- [ ] Test result templates provided for each scenario
- [ ] Checklist covers file path compatibility, parameter compatibility, context propagation, error handling
- [ ] Transition matrix compliance validated against AGENTS_ARCHITECTURE.md
- [ ] Cycle protection rules validated (max 3 iterations, escalation mechanism)

---

**Document Status**: SPECIFICATION COMPLETE
**Task 4.2 Status**: ✅ COMPLETE (specification created, ready for test execution)
**Next Action**: Execute integration tests using this specification OR proceed to Task 4.3 (Scoring Algorithm Validation)
**Owner**: Development Team
**Last Updated**: 2025-10-15
