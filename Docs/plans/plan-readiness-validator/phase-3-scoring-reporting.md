# Phase 3: Scoring and Reporting Engine

**Phase Duration**: 6-8 hours
**Phase Status**: PENDING
**Dependencies**: Phase 1 (Foundation), Phase 2 (Validation Logic)

## Overview

This phase implements the scoring aggregation engine and comprehensive reporting system. It combines scores from all validation components (structure, technical, execution, specificity) into a final LLM readiness score, generates agent transition recommendations, and produces actionable validation reports.

**Phase Objectives**:
- Aggregate component scores into final LLM readiness score
- Implement agent transition recommendation engine
- Create comprehensive validation report generator
- Ensure report format compatible with downstream agents

**Phase Deliverables**:
- Score aggregation logic in prompt.md
- Agent transition recommendation engine in prompt.md
- Validation report templates (READY and REQUIRES_IMPROVEMENT)
- Integration with TodoWrite tool for tracking

## Task 3.1: LLM Readiness Score Calculator

### Objectives

Aggregate scores from all validation components (Task Specificity, Technical Completeness, Execution Clarity, Structure Compliance) into a single, reproducible LLM readiness score. Apply ≥90% pass/fail threshold and generate transparent score breakdown.

### Responsibilities

1. **Score Aggregation from Validation Components**
   - Collect Task Specificity Score (0-30 points) from manual analysis
   - Collect Technical Completeness Score (0-30 points) from Task 2.2
   - Collect Execution Clarity Score (0-20 points) from Task 2.3
   - Collect Structure Compliance Score (0-20 points) from Task 2.1

2. **Weighted Final Score Calculation**
   - Sum all component scores
   - Total Score = Task Specificity + Technical Completeness + Execution Clarity + Structure Compliance
   - Maximum: 100 points

3. **Pass/Fail Threshold Application**
   - READY: Total Score ≥ 90 points
   - REQUIRES_IMPROVEMENT: Total Score < 90 points

4. **Score Breakdown Report Generation**
   - Show individual component scores
   - Highlight components below expected thresholds
   - Provide score interpretation guidance

### Implementation Approach

**Algorithm**:

```
Function CalculateLLMReadinessScore(plan_file, plan_directory):
    # Step 1: Execute all validation components
    structure_result = ValidatePlanStructure(plan_file, plan_directory)
    technical_result = ValidateTechnicalCompleteness(plan_files)
    complexity_result = AnalyzeExecutionComplexity(plan_files)
    specificity_result = AnalyzeTaskSpecificity(plan_files)

    # Step 2: Collect component scores
    scores = {
        task_specificity: specificity_result.score,       # 0-30 points
        technical_completeness: technical_result.score,   # 0-30 points
        execution_clarity: complexity_result.score,       # 0-20 points
        structure_compliance: structure_result.score      # 0-20 points
    }

    # Step 3: Calculate total score
    total_score = sum(scores.values())

    # Step 4: Determine status
    status = "READY" if total_score >= 90 else "REQUIRES_IMPROVEMENT"

    # Step 5: Generate score breakdown
    breakdown = {
        total: total_score,
        max: 100,
        percentage: (total_score / 100) * 100,
        status: status,
        components: [
            {
                name: "Task Specificity",
                score: scores.task_specificity,
                max: 30,
                percentage: (scores.task_specificity / 30) * 100,
                status: "✅" if scores.task_specificity >= 27 else "⚠️" if scores.task_specificity >= 24 else "❌"
            },
            {
                name: "Technical Completeness",
                score: scores.technical_completeness,
                max: 30,
                percentage: (scores.technical_completeness / 30) * 100,
                status: "✅" if scores.technical_completeness >= 27 else "⚠️" if scores.technical_completeness >= 24 else "❌"
            },
            {
                name: "Execution Clarity",
                score: scores.execution_clarity,
                max: 20,
                percentage: (scores.execution_clarity / 20) * 100,
                status: "✅" if scores.execution_clarity >= 18 else "⚠️" if scores.execution_clarity >= 16 else "❌"
            },
            {
                name: "Structure Compliance",
                score: scores.structure_compliance,
                max: 20,
                percentage: (scores.structure_compliance / 20) * 100,
                status: "✅" if scores.structure_compliance >= 18 else "⚠️" if scores.structure_compliance >= 16 else "❌"
            }
        ]
    }

    # Step 6: Aggregate all violations
    all_violations = (
        structure_result.violations +
        technical_result.violations +
        complexity_result.complexity_violations +
        complexity_result.plan_realization_violations +
        specificity_result.violations
    )

    return {
        total_score: total_score,
        status: status,
        breakdown: breakdown,
        violations: all_violations
    }
```

### Deliverables

#### 3.1.1 Score Aggregation Logic

**Component Score Collection**:

```python
# Pseudo-code for score collection
component_scores = {
    "Task Specificity": AnalyzeTaskSpecificity(plan_files),
    "Technical Completeness": ValidateTechnicalCompleteness(plan_files),
    "Execution Clarity": AnalyzeExecutionComplexity(plan_files),
    "Structure Compliance": ValidatePlanStructure(plan_file, plan_directory)
}

# Extract scores
task_specificity_score = component_scores["Task Specificity"].score
technical_completeness_score = component_scores["Technical Completeness"].score
execution_clarity_score = component_scores["Execution Clarity"].score
structure_compliance_score = component_scores["Structure Compliance"].score
```

**Task Specificity Analysis** (New Component):

This component analyzes task descriptions for specificity (concrete file paths, class names, acceptance criteria).

```
Function AnalyzeTaskSpecificity(plan_files):
    specificity_score = 30
    violations = []

    for file in plan_files:
        content = Read(file)
        tasks = ExtractTaskSections(content)

        for task in tasks:
            specificity_issues = []

            # Check for concrete file paths
            has_file_paths = CountPattern(task.content, r"`[^`]+\.(cs|md|json)`")
            if has_file_paths == 0:
                specificity_issues.append("Missing concrete file paths")
                specificity_score -= 2

            # Check for specific class/interface names
            has_type_names = CountPattern(task.content, r"(class|interface) [A-Z]\w+")
            if has_type_names == 0:
                specificity_issues.append("Missing specific class/interface names")
                specificity_score -= 2

            # Check for clear acceptance criteria
            has_acceptance_criteria = Contains(task.content, "Acceptance Criteria:")
            if not has_acceptance_criteria:
                specificity_issues.append("Missing clear acceptance criteria")
                specificity_score -= 1

            if specificity_issues:
                violations.append({
                    type: "LOW_TASK_SPECIFICITY",
                    severity: "RECOMMENDATION",
                    file: file,
                    task: task.title,
                    issues: specificity_issues,
                    message: f"Task lacks specificity in {len(specificity_issues)} area(s)",
                    recommendation: "Add concrete file paths, specific type names, and measurable acceptance criteria"
                })

    specificity_score = max(0, specificity_score)

    return {
        score: specificity_score,
        violations: violations
    }
```

**Total Score Calculation**:

```python
total_score = (
    task_specificity_score +        # 0-30 points
    technical_completeness_score +   # 0-30 points
    execution_clarity_score +        # 0-20 points
    structure_compliance_score       # 0-20 points
)
# Total: 0-100 points
```

#### 3.1.2 Score Breakdown Report Template

**Template**:

```markdown
## LLM Readiness Score Breakdown

**Total Score**: {total_score}/100 ({percentage}%)
**Status**: {status_icon} {status}

### Component Scores

| Component                | Score | Max | Percentage | Status |
|-------------------------|-------|-----|------------|--------|
| Task Specificity        | {ts_score} | 30  | {ts_pct}% | {ts_status} |
| Technical Completeness  | {tc_score} | 30  | {tc_pct}% | {tc_status} |
| Execution Clarity       | {ec_score} | 20  | {ec_pct}% | {ec_status} |
| Structure Compliance    | {sc_score} | 20  | {sc_pct}% | {sc_status} |
| **TOTAL**               | **{total_score}** | **100** | **{total_pct}%** | **{total_status}** |

### Score Interpretation

- ✅ Excellent (≥90%): Component meets quality standards
- ⚠️ Adequate (80-89%): Component functional but has minor issues
- ❌ Needs Improvement (<80%): Component requires significant work

### Threshold Status

{if total_score >= 90}
✅ **READY**: Plan meets ≥90% LLM readiness threshold and is approved for execution.
{else}
⚠️ **REQUIRES_IMPROVEMENT**: Plan below 90% threshold. Address issues before execution.
{endif}
```

**Example - High Score (95/100, READY)**:

```markdown
## LLM Readiness Score Breakdown

**Total Score**: 95/100 (95%)
**Status**: ✅ READY

### Component Scores

| Component                | Score | Max | Percentage | Status |
|-------------------------|-------|-----|------------|--------|
| Task Specificity        | 28    | 30  | 93%       | ✅     |
| Technical Completeness  | 30    | 30  | 100%      | ✅     |
| Execution Clarity       | 19    | 20  | 95%       | ✅     |
| Structure Compliance    | 18    | 20  | 90%       | ✅     |
| **TOTAL**               | **95** | **100** | **95%** | **✅** |

### Score Interpretation

- ✅ Excellent (≥90%): Component meets quality standards
- ⚠️ Adequate (80-89%): Component functional but has minor issues
- ❌ Needs Improvement (<80%): Component requires significant work

### Threshold Status

✅ **READY**: Plan meets ≥90% LLM readiness threshold and is approved for execution.
```

**Example - Low Score (72/100, REQUIRES_IMPROVEMENT)**:

```markdown
## LLM Readiness Score Breakdown

**Total Score**: 72/100 (72%)
**Status**: ⚠️ REQUIRES_IMPROVEMENT

### Component Scores

| Component                | Score | Max | Percentage | Status |
|-------------------------|-------|-----|------------|--------|
| Task Specificity        | 18    | 30  | 60%       | ❌     |
| Technical Completeness  | 22    | 30  | 73%       | ❌     |
| Execution Clarity       | 15    | 20  | 75%       | ❌     |
| Structure Compliance    | 17    | 20  | 85%       | ⚠️     |
| **TOTAL**               | **72** | **100** | **72%** | **⚠️** |

### Score Interpretation

- ✅ Excellent (≥90%): Component meets quality standards
- ⚠️ Adequate (80-89%): Component functional but has minor issues
- ❌ Needs Improvement (<80%): Component requires significant work

### Threshold Status

⚠️ **REQUIRES_IMPROVEMENT**: Plan below 90% threshold. Address {violation_count} issues before execution.
```

#### 3.1.3 Pass/Fail Determination Logic

**Decision Algorithm**:

```python
def determine_status(total_score):
    if total_score >= 90:
        return {
            status: "READY",
            icon: "✅",
            message: "Plan meets LLM readiness threshold and is approved for execution.",
            next_action: "Proceed to plan-task-executor or parallel-plan-optimizer"
        }
    else:
        gap = 90 - total_score
        return {
            status: "REQUIRES_IMPROVEMENT",
            icon: "⚠️",
            message: f"Plan below 90% threshold by {gap} points. Address issues before execution.",
            next_action: "Invoke work-plan-architect for plan revision"
        }
```

**Edge Cases**:

1. **Score exactly 90**: Status = READY (inclusive threshold)
2. **Score 0**: Status = REQUIRES_IMPROVEMENT, flag as "Critical - comprehensive revision needed"
3. **Score 89**: Status = REQUIRES_IMPROVEMENT, flag as "Close to threshold - minor fixes may suffice"

### Acceptance Criteria

- [ ] Score calculation reproducible (same plan → same score ±1 point)
- [ ] All component scores properly aggregated
- [ ] Pass/fail threshold applied correctly (≥90 = READY)
- [ ] Breakdown shows contribution of each component
- [ ] Status clear and unambiguous (READY or REQUIRES_IMPROVEMENT)
- [ ] Score interpretation guide provides actionable feedback
- [ ] Edge cases handled (score 0, score exactly 90, score 89)

### Technical Notes

**Reproducibility**:
Ensure validation order doesn't affect scores. All validation components should be stateless and order-independent.

**Precision**:
Use integer scores throughout (no floating point) to avoid rounding inconsistencies.

---

## Task 3.2: Recommendation Engine for Next Agents

### Objectives

Analyze plan characteristics and validation results to generate contextual recommendations for next agents in the workflow. Apply agent transition matrix rules to determine CRITICAL, RECOMMENDED, and OPTIONAL agent invocations.

### Responsibilities

1. **CRITICAL Path Recommendations**
   - If score <90: Recommend work-plan-architect for revision
   - If architectural components detected: Recommend architecture-documenter
   - If score ≥90: Recommend plan-task-executor for execution

2. **RECOMMENDED Path Recommendations**
   - If >5 tasks detected: Recommend parallel-plan-optimizer
   - Conditional based on plan characteristics

3. **OPTIONAL Path Recommendations**
   - Manual validation: work-plan-reviewer
   - Additional quality checks as needed

4. **Agent Transition Matrix Compliance**
   - Follow predefined CRITICAL/RECOMMENDED paths
   - Support conditional agent invocation
   - Provide parameters for downstream agents

### Implementation Approach

**Algorithm**:

```
Function GenerateAgentRecommendations(validation_result, plan_characteristics):
    recommendations = {
        critical: [],
        recommended: [],
        optional: []
    }

    # CRITICAL Path Logic

    # If plan requires improvement, revision is CRITICAL
    if validation_result.status == "REQUIRES_IMPROVEMENT":
        recommendations.critical.append({
            agent: "work-plan-architect",
            reason: f"Plan requires revision to address {len(validation_result.violations)} issues before execution",
            command: "Use Task tool with subagent_type: \"work-plan-architect\"",
            parameters: {
                plan_file: plan_characteristics.plan_file,
                issues_list: validation_result.violations,
                target_score: 90
            },
            priority: 1
        })

    # If architectural components detected, documentation is CRITICAL
    if plan_characteristics.has_architectural_components:
        recommendations.critical.append({
            agent: "architecture-documenter",
            reason: f"Plan contains {plan_characteristics.component_count} architectural components requiring documentation",
            command: "Use Task tool with subagent_type: \"architecture-documenter\"",
            parameters: {
                plan_file: plan_characteristics.plan_file,
                type: "planned"
            },
            priority: 2
        })

    # If plan is ready, execution is next step
    if validation_result.status == "READY":
        recommendations.critical.append({
            agent: "plan-task-executor",
            reason: "Plan meets LLM readiness threshold (≥90%), ready for execution",
            command: "Use Task tool with subagent_type: \"plan-task-executor\"",
            parameters: {
                plan_file: plan_characteristics.plan_file
            },
            priority: 3
        })

    # RECOMMENDED Path Logic

    # If >5 tasks, parallel optimization is RECOMMENDED
    if plan_characteristics.task_count > 5:
        time_reduction = min(50, (plan_characteristics.task_count - 5) * 5)  # Estimate 5% per task, cap at 50%
        recommendations.recommended.append({
            agent: "parallel-plan-optimizer",
            reason: f"Plan has {plan_characteristics.task_count} tasks - parallel execution could reduce time by {time_reduction}%",
            command: "Use Task tool with subagent_type: \"parallel-plan-optimizer\"",
            parameters: {
                plan_file: plan_characteristics.plan_file
            },
            priority: 1
        })

    # OPTIONAL Path Logic

    # Manual validation is always optional
    recommendations.optional.append({
        agent: "work-plan-reviewer",
        reason: "Manual validation alongside automated assessment",
        command: "Use Task tool with subagent_type: \"work-plan-reviewer\"",
        parameters: {
            plan_file: plan_characteristics.plan_file
        },
        priority: 1
    })

    return recommendations
```

**Plan Characteristics Detection**:

```
Function AnalyzePlanCharacteristics(plan_files):
    characteristics = {
        plan_file: coordinator_file,
        task_count: 0,
        has_architectural_components: false,
        component_count: 0,
        component_types: []
    }

    for file in plan_files:
        content = Read(file)

        # Count tasks (### X.Y sections)
        tasks = ExtractTaskSections(content)
        characteristics.task_count += len(tasks)

        # Detect architectural components
        if ContainsKeywords(content, ["Entity", "DbContext", "Service", "Controller", "API"]):
            characteristics.has_architectural_components = true

            if Contains(content, "Entity"):
                characteristics.component_types.append("Entity")
                characteristics.component_count += CountPattern(content, "Entity")

            if Contains(content, "Service"):
                characteristics.component_types.append("Service")
                characteristics.component_count += CountPattern(content, "Service")

            if Contains(content, "Controller"):
                characteristics.component_types.append("Controller")
                characteristics.component_count += CountPattern(content, "Controller")

    return characteristics
```

### Deliverables

#### 3.2.1 Agent Transition Logic

**Transition Rules** (from agent transition matrix):

```yaml
CRITICAL Paths:
  - condition: status == "REQUIRES_IMPROVEMENT"
    agent: work-plan-architect
    reason: Plan revision required
    max_iterations: 3

  - condition: has_architectural_components == true
    agent: architecture-documenter
    reason: Architectural documentation required
    max_iterations: 1

  - condition: status == "READY"
    agent: plan-task-executor
    reason: Plan ready for execution
    max_iterations: N/A (execution)

RECOMMENDED Paths:
  - condition: task_count > 5
    agent: parallel-plan-optimizer
    reason: Parallel execution optimization opportunity
    max_iterations: 1

OPTIONAL Paths:
  - condition: always
    agent: work-plan-reviewer
    reason: Manual validation alongside automated
    max_iterations: 1
```

#### 3.2.2 Recommendation Output Format

**Template**:

```markdown
## Recommended Next Actions

{if has_critical_recommendations}
### CRITICAL

{for recommendation in critical_recommendations}
{priority}. **{agent_name}**
   - **Reason**: {reason}
   - **Command**: {command}
   - **Parameters**: {parameters}
   - **Priority**: {priority} (lower number = higher priority)
{endfor}
{endif}

{if has_recommended_recommendations}
### RECOMMENDED

{for recommendation in recommended_recommendations}
{priority}. **{agent_name}**
   - **Reason**: {reason}
   - **Command**: {command}
   - **Parameters**: {parameters}
{endfor}
{endif}

{if has_optional_recommendations}
### OPTIONAL

{for recommendation in optional_recommendations}
{priority}. **{agent_name}**
   - **Reason**: {reason}
   - **Command**: {command}
   - **Parameters**: {parameters}
{endfor}
{endif}
```

**Example - REQUIRES_IMPROVEMENT Plan**:

```markdown
## Recommended Next Actions

### CRITICAL

1. **work-plan-architect**
   - **Reason**: Plan requires revision to address 12 issues before execution (target score: ≥90/100)
   - **Command**: Use Task tool with subagent_type: "work-plan-architect"
   - **Parameters**:
     - plan_file: "Docs/plans/Plan-Readiness-Validator-Implementation-Plan.md"
     - issues_list: [See Issues Found section above]
     - target_score: 90
   - **Priority**: 1 (highest)

2. **architecture-documenter**
   - **Reason**: Plan contains 5 architectural components (Entity, Service, Controller) requiring documentation
   - **Command**: Use Task tool with subagent_type: "architecture-documenter"
   - **Parameters**:
     - plan_file: "Docs/plans/Plan-Readiness-Validator-Implementation-Plan.md"
     - type: "planned"
   - **Priority**: 2 (after plan revision)

### RECOMMENDED

1. **parallel-plan-optimizer**
   - **Reason**: Plan has 15 tasks - parallel execution could reduce time by 40%
   - **Command**: Use Task tool with subagent_type: "parallel-plan-optimizer"
   - **Parameters**:
     - plan_file: "Docs/plans/Plan-Readiness-Validator-Implementation-Plan.md"

### OPTIONAL

1. **work-plan-reviewer**
   - **Reason**: Manual validation alongside automated assessment
   - **Command**: Use Task tool with subagent_type: "work-plan-reviewer"
   - **Parameters**:
     - plan_file: "Docs/plans/Plan-Readiness-Validator-Implementation-Plan.md"
```

**Example - READY Plan**:

```markdown
## Recommended Next Actions

### CRITICAL

1. **plan-task-executor**
   - **Reason**: Plan meets LLM readiness threshold (95/100, ≥90%), ready for execution
   - **Command**: Use Task tool with subagent_type: "plan-task-executor"
   - **Parameters**:
     - plan_file: "Docs/plans/Feature-Implementation-Plan.md"
   - **Priority**: 1 (highest)

### RECOMMENDED

1. **parallel-plan-optimizer**
   - **Reason**: Plan has 8 tasks - parallel execution could reduce time by 25%
   - **Command**: Use Task tool with subagent_type: "parallel-plan-optimizer"
   - **Parameters**:
     - plan_file: "Docs/plans/Feature-Implementation-Plan.md"

### OPTIONAL

1. **work-plan-reviewer**
   - **Reason**: Manual validation alongside automated assessment
   - **Command**: Use Task tool with subagent_type: "work-plan-reviewer"
   - **Parameters**:
     - plan_file: "Docs/plans/Feature-Implementation-Plan.md"
```

### Acceptance Criteria

- [ ] Recommendations align with agent transition matrix rules
- [ ] CRITICAL paths always included (work-plan-architect, architecture-documenter, plan-task-executor)
- [ ] Conditional RECOMMENDED paths handled correctly (parallel-plan-optimizer if >5 tasks)
- [ ] Recommendations prioritized by workflow logic (revision before execution)
- [ ] Parameters provided for each agent invocation
- [ ] Output format consistent and machine-parsable

### Technical Notes

**Architectural Component Detection**:
Use keyword-based detection with context:
- "Entity" + "DbContext" → Entity component
- "Service" + "interface" → Service component
- "Controller" + "API" → Controller component

**Priority Assignment**:
- Priority 1 (CRITICAL): Blocking issues (revision needed)
- Priority 2 (CRITICAL): Non-blocking but required (documentation)
- Priority 3 (CRITICAL): Next step (execution)
- Priority 1 (RECOMMENDED): Performance optimization
- Priority 1 (OPTIONAL): Manual validation

---

## Task 3.3: Validation Report Generator

### Objectives

Generate comprehensive validation reports in consistent markdown format. Include readiness score, detailed issue list, and agent recommendations. Ensure reports are actionable with file/line references.

### Responsibilities

1. **Report Structure and Formatting**
   - Standard markdown format
   - Sections: Header, Score Breakdown, Issues, Recommendations
   - Color-coded severity (CRITICAL, IMPORTANT, RECOMMENDATIONS)

2. **Issue Aggregation and Categorization**
   - Collect all violations from validation components
   - Categorize by severity: CRITICAL, IMPORTANT, RECOMMENDATION
   - Sort by severity and file location

3. **Actionable Feedback Generation**
   - Include file paths and line numbers for each issue
   - Provide specific fix recommendations
   - Include code examples where applicable

4. **Report Output Integration**
   - Save report to file (optional)
   - Display in console output
   - Support TodoWrite tool integration for tracking

### Implementation Approach

**Algorithm**:

```
Function GenerateValidationReport(validation_result, agent_recommendations, plan_file):
    report = {
        header: GenerateReportHeader(validation_result, plan_file),
        score_breakdown: GenerateScoreBreakdown(validation_result),
        issues: GenerateIssuesSummary(validation_result.violations),
        recommendations: GenerateAgentRecommendations(agent_recommendations)
    }

    # Format report as markdown
    markdown_report = FormatAsMarkdown(report)

    return markdown_report
```

### Deliverables

#### 3.3.1 Report Template

**Full Report Template**:

```markdown
# Plan Readiness Validation Report

**Plan**: {plan_file}
**Validation Date**: {timestamp}
**LLM Readiness Score**: {total_score}/100 ({percentage}%)
**Status**: {status_icon} {status}

---

{include: Score Breakdown section from Task 3.1}

---

## Issues Found ({total_issue_count})

{if has_critical_issues}
### CRITICAL Issues ({critical_count})

{for issue in critical_issues}
{index}. **{issue_type}** - `{file_path}`{if line_number}`:L{line_number}`{endif}
   - **Description**: {issue_description}
   - **Impact**: {impact_on_score}
   - **Recommendation**: {fix_recommendation}
   {if has_code_example}
   - **Example**:
     ```markdown
     {code_example}
     ```
   {endif}

{endfor}
{endif}

{if has_important_issues}
### IMPORTANT Issues ({important_count})

{for issue in important_issues}
[Similar format to CRITICAL]
{endfor}
{endif}

{if has_recommendation_issues}
### RECOMMENDATIONS ({recommendation_count})

{for issue in recommendation_issues}
[Similar format to CRITICAL, but lower severity]
{endfor}
{endif}

{if no_issues}
✅ **No issues found**. Plan meets all quality standards.
{endif}

---

{include: Recommended Next Actions from Task 3.2}

---

## Validation Summary

- **Total Issues**: {total_issue_count}
- **Critical Issues**: {critical_count}
- **Important Issues**: {important_count}
- **Recommendations**: {recommendation_count}

{if status == "READY"}
✅ **Plan Approved**: This plan meets the ≥90% LLM readiness threshold and is ready for execution. Proceed with recommended next actions.
{else}
⚠️ **Plan Requires Improvement**: Address {critical_count + important_count} critical and important issues before execution. Estimated score improvement: +{estimated_improvement} points.
{endif}

---

**Report Generated**: {timestamp}
**Validator Version**: 1.0.0
```

#### 3.3.2 Example Reports

**Example 1: READY Report (Score: 95/100)**

```markdown
# Plan Readiness Validation Report

**Plan**: Docs/plans/Feature-Authentication-Implementation.md
**Validation Date**: 2025-10-14 15:30:00 UTC
**LLM Readiness Score**: 95/100 (95%)
**Status**: ✅ READY

---

## LLM Readiness Score Breakdown

**Total Score**: 95/100 (95%)
**Status**: ✅ READY

### Component Scores

| Component                | Score | Max | Percentage | Status |
|-------------------------|-------|-----|------------|--------|
| Task Specificity        | 28    | 30  | 93%       | ✅     |
| Technical Completeness  | 30    | 30  | 100%      | ✅     |
| Execution Clarity       | 19    | 20  | 95%       | ✅     |
| Structure Compliance    | 18    | 20  | 90%       | ✅     |
| **TOTAL**               | **95** | **100** | **95%** | **✅** |

---

## Issues Found (2)

### RECOMMENDATIONS (2)

1. **Low Task Specificity** - `Docs/plans/feature-authentication-implementation/phase-2-implementation.md`
   - **Description**: Task 2.3 lacks specific file path for TokenValidator class
   - **Impact**: -2 points on Task Specificity Score
   - **Recommendation**: Add explicit file path (e.g., `src/Orchestra.Core/Validators/TokenValidator.cs`)

2. **Missing Back-Reference** - `Docs/plans/feature-authentication-implementation/phase-3-testing.md`
   - **Description**: Phase file should link back to coordinator
   - **Impact**: No score impact (best practice recommendation)
   - **Recommendation**: Add link to main plan at top of phase file

---

## Recommended Next Actions

### CRITICAL

1. **plan-task-executor**
   - **Reason**: Plan meets LLM readiness threshold (95/100, ≥90%), ready for execution
   - **Command**: Use Task tool with subagent_type: "plan-task-executor"
   - **Parameters**:
     - plan_file: "Docs/plans/Feature-Authentication-Implementation.md"
   - **Priority**: 1 (highest)

### RECOMMENDED

1. **parallel-plan-optimizer**
   - **Reason**: Plan has 12 tasks - parallel execution could reduce time by 35%
   - **Command**: Use Task tool with subagent_type: "parallel-plan-optimizer"
   - **Parameters**:
     - plan_file: "Docs/plans/Feature-Authentication-Implementation.md"

---

## Validation Summary

- **Total Issues**: 2
- **Critical Issues**: 0
- **Important Issues**: 0
- **Recommendations**: 2

✅ **Plan Approved**: This plan meets the ≥90% LLM readiness threshold and is ready for execution. Proceed with recommended next actions.

---

**Report Generated**: 2025-10-14 15:30:00 UTC
**Validator Version**: 1.0.0
```

**Example 2: REQUIRES_IMPROVEMENT Report (Score: 72/100)**

[See full example in main plan at line 337 - THIS IS THE MISSING CROSS-REFERENCE TO FIX]

#### 3.3.3 Integration with TodoWrite Tool

**TodoWrite Integration** (optional feature):

When validation finds issues, optionally create TODO items for tracking:

```python
if validation_result.status == "REQUIRES_IMPROVEMENT":
    todos = []

    for issue in validation_result.violations:
        if issue.severity in ["CRITICAL", "IMPORTANT"]:
            todos.append({
                content: f"Fix {issue.type}: {issue.message}",
                status: "pending",
                activeForm: f"Fixing {issue.type}: {issue.message}",
                metadata: {
                    file: issue.file,
                    line: issue.line_number if exists else None,
                    severity: issue.severity,
                    recommendation: issue.recommendation
                }
            })

    # Invoke TodoWrite tool
    TodoWrite(todos=todos)
```

### Acceptance Criteria

- [ ] Report readable and actionable for humans
- [ ] Issues include file paths and line numbers
- [ ] Recommendations prioritized by severity (CRITICAL → IMPORTANT → RECOMMENDATION)
- [ ] Report format consistent across READY and REQUIRES_IMPROVEMENT status
- [ ] Score breakdown clearly presented
- [ ] Agent recommendations properly formatted
- [ ] Optional TodoWrite integration functional

### Technical Notes

**Markdown Formatting**:
Use consistent indentation (3 spaces for nested lists) and code block formatting (triple backticks with language).

**Timestamp Format**:
Use ISO 8601 format: `YYYY-MM-DD HH:MM:SS UTC`

---

## Phase 3 Completion Criteria

### All Tasks Complete When:

- [ ] Score calculator implemented with aggregation logic
- [ ] Recommendation engine implemented with agent transition rules
- [ ] Report generator implemented with READY and REQUIRES_IMPROVEMENT templates
- [ ] Score breakdown formatting complete
- [ ] Issue categorization and sorting implemented
- [ ] Agent recommendations properly structured
- [ ] All acceptance criteria for Phase 3 met

### Quality Gates:

- [ ] Score calculation reproducible (same plan → same score)
- [ ] Recommendations align with agent transition matrix
- [ ] Reports are actionable with specific file/line references
- [ ] Output format validated against sample plans

### Next Phase:

After Phase 3 completion, proceed to **Phase 4: Testing and Validation**. See `phase-4-testing.md` for detailed tasks.

---

**Phase Status**: PENDING
**Last Updated**: 2025-10-14
**Next Review**: After Task 3.3 completion
