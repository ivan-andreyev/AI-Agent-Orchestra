# Phase 2: Core Validation Logic Implementation

**Phase Duration**: 8-10 hours
**Phase Status**: PENDING
**Dependencies**: Phase 1 (Foundation) must be complete

## Overview

This phase implements the core validation components that assess plan quality across three dimensions: structure, technical completeness, and execution complexity. Each component operates independently but contributes to the overall LLM readiness score.

**Phase Objectives**:
- Implement plan structure validator (GOLDEN RULES compliance)
- Create technical completeness validator (Entity/Service/API patterns)
- Build execution complexity analyzer (tool call estimation)
- Validate "Plan ≠ Realization" principle enforcement

**Phase Deliverables**:
- Structure validation logic in prompt.md
- Technical completeness validation logic in prompt.md
- Execution complexity analysis logic in prompt.md
- Integration with scoring algorithm from Phase 1

## Task 2.1: Plan Structure Validator Component

### Objectives

Implement comprehensive structure validation that ensures plans follow catalogization rules, file size limits, and reference integrity standards. This validator detects all GOLDEN RULE violations and provides specific file/line references for corrections.

### Responsibilities

1. **File Naming Convention Validation**
   - Verify coordinator file naming (descriptive, outside directory)
   - Validate phase file naming (XX-Name.md format)
   - Check consistency across file names

2. **Catalogization Rules Validation (GOLDEN RULES #1 and #2)**
   - RULE #1: Coordinator file outside its content directory
   - RULE #2: Child files inside dedicated subdirectory
   - Detect and report violations with file paths

3. **Coordinator Placement Validation**
   - Verify coordinator is not inside its own directory
   - Check proper parent/child relationship
   - Validate directory structure

4. **File Size Limit Validation (≤400 lines)**
   - Count lines per file using Bash tool
   - Flag files exceeding 400-line limit
   - Calculate percentage over limit for severity

5. **Parent/Child Reference Integrity Validation**
   - Parse markdown links in coordinator file
   - Verify all linked phase files exist
   - Check phase files link back to coordinator
   - Detect circular references
   - Identify broken links

### Implementation Approach

**Tool Usage**:
- **Glob**: Discover all files in plan directory
- **Read**: Access plan files for link parsing
- **Bash**: Execute line count commands (wc -l or equivalent)
- **Grep**: Search for link patterns in markdown files

**Algorithm**:

```
1. Load plan_file path (coordinator)
2. Extract plan_directory from plan_file path
3. Use Glob to discover all .md files in plan_directory
4. For each file:
   a. Check naming convention (XX-Name.md for phase files)
   b. Count lines using Bash (flag if >400)
   c. Parse markdown links using Read + regex
5. Validate coordinator placement:
   a. Check coordinator is NOT inside plan_directory
   b. Flag GOLDEN RULE #1 violation if inside
6. Validate reference integrity:
   a. Extract all links from coordinator
   b. Verify each linked file exists in plan_directory
   c. Parse phase files for back-references to coordinator
   d. Flag broken links or missing references
7. Calculate Structure Compliance Score (0-20 points)
```

**Scoring Contribution**:
- Catalogization rules compliance: 10 points
  - GOLDEN RULE #1 followed: 5 points
  - GOLDEN RULE #2 followed: 5 points
  - Deduct 5 points per violation
- File size limits compliance: 5 points
  - All files ≤400 lines: 5 points
  - Deduct 1 point per file exceeding limit (min 0)
- Reference integrity: 5 points
  - No broken links: 3 points
  - All referenced files exist: 2 points

### Deliverables

#### 2.1.1 Structure Validation Logic

**Pseudo-code** (to be implemented in prompt.md):

```
Function ValidatePlanStructure(plan_file, plan_directory):
    violations = []
    structure_score = 20

    # Step 1: Discover files
    coordinator_file = plan_file
    phase_files = Glob(pattern="*.md", path=plan_directory)

    # Step 2: Validate coordinator placement (GOLDEN RULE #1)
    coordinator_dir = GetParentDirectory(coordinator_file)
    if coordinator_file is inside plan_directory:
        violations.append({
            type: "GOLDEN_RULE_1_VIOLATION",
            severity: "CRITICAL",
            file: coordinator_file,
            message: "Coordinator file must be outside its content directory",
            recommendation: f"Move {coordinator_file} to {coordinator_dir}"
        })
        structure_score -= 5

    # Step 3: Validate file naming
    for phase_file in phase_files:
        if not MatchesPattern(phase_file, "phase-\d+-[a-z-]+\.md"):
            violations.append({
                type: "NAMING_VIOLATION",
                severity: "IMPORTANT",
                file: phase_file,
                message: "Phase file must follow 'phase-{N}-{title}.md' format",
                recommendation: f"Rename to match pattern (e.g., phase-1-foundation.md)"
            })

    # Step 4: Validate file size limits
    all_files = [coordinator_file] + phase_files
    for file in all_files:
        line_count = BashCommand(f"wc -l {file}").split()[0]
        if line_count > 400:
            percentage_over = ((line_count - 400) / 400) * 100
            violations.append({
                type: "FILE_SIZE_VIOLATION",
                severity: "CRITICAL" if percentage_over > 25 else "IMPORTANT",
                file: file,
                line_count: line_count,
                percentage_over: percentage_over,
                message: f"File exceeds 400-line limit ({line_count} lines, {percentage_over}% over)",
                recommendation: "Decompose into smaller files or trim redundant content"
            })
            structure_score -= 1

    structure_score = max(0, structure_score)  # Floor at 0

    # Step 5: Validate reference integrity
    coordinator_content = Read(coordinator_file)
    coordinator_links = ExtractMarkdownLinks(coordinator_content)

    for link in coordinator_links:
        linked_file = ResolvePath(link, base=coordinator_dir)
        if not FileExists(linked_file):
            violations.append({
                type: "BROKEN_REFERENCE",
                severity: "CRITICAL",
                file: coordinator_file,
                linked_file: link,
                message: f"Coordinator references non-existent file: {link}",
                recommendation: f"Create {linked_file} or fix link"
            })
            structure_score -= 2

    # Step 6: Validate back-references
    for phase_file in phase_files:
        phase_content = Read(phase_file)
        phase_links = ExtractMarkdownLinks(phase_content)
        has_back_reference = any(ResolveLink(link) == coordinator_file for link in phase_links)

        if not has_back_reference:
            violations.append({
                type: "MISSING_BACK_REFERENCE",
                severity: "RECOMMENDATION",
                file: phase_file,
                message: f"Phase file should link back to coordinator",
                recommendation: f"Add link to {coordinator_file} at top of {phase_file}"
            })

    return {
        score: structure_score,
        violations: violations
    }
```

#### 2.1.2 Error Message Templates

**Template 1: GOLDEN RULE #1 Violation**
```markdown
**CRITICAL**: GOLDEN RULE #1 Violation - {file_path}
- **Issue**: Coordinator file located inside its content directory
- **Current Location**: {current_path}
- **Expected Location**: {expected_path}
- **Recommendation**: Move coordinator outside directory (e.g., Docs/plans/Plan-Name.md)
- **Impact**: -5 points on Structure Compliance Score
```

**Template 2: File Size Violation**
```markdown
**{severity}**: File Size Violation - {file_path}:{line_count} lines
- **Issue**: File exceeds 400-line limit ({percentage_over}% over)
- **Current Size**: {line_count} lines
- **Maximum Allowed**: 400 lines
- **Recommendation**: Decompose into smaller phase files or trim redundant content
- **Impact**: -1 point on Structure Compliance Score per violation
```

**Template 3: Broken Reference**
```markdown
**CRITICAL**: Broken Reference - {coordinator_file}:L{line_number}
- **Issue**: Coordinator references non-existent file: {linked_file}
- **Referenced File**: {linked_file}
- **File Status**: NOT FOUND
- **Recommendation**: Create {linked_file} or update link to existing file
- **Impact**: -2 points on Structure Compliance Score
```

#### 2.1.3 Integration with Overall Score

**Score Calculation**:
```
Structure Compliance Score = Base Score (20) - Violation Penalties
```

**Violation Penalties**:
- GOLDEN RULE violation: -5 points each
- File size violation: -1 point per file (min 0)
- Broken reference: -2 points each
- Missing back-reference: No penalty (RECOMMENDATION only)

**Score Floor**: Minimum score is 0 points (no negative scores)

### Acceptance Criteria

- [ ] Detects all GOLDEN RULE #1 violations (coordinator placement)
- [ ] Identifies file size violations for all files >400 lines
- [ ] Validates file naming conventions (XX-Name.md format)
- [ ] Detects broken references (coordinator → phase files)
- [ ] Identifies missing back-references (phase files → coordinator)
- [ ] Provides specific file paths and line numbers for all violations
- [ ] Calculates structure compliance score (0-20 points)
- [ ] Error messages include actionable recommendations

### Technical Notes

**File Path Resolution**:
Use platform-independent path handling (forward slashes) for cross-platform compatibility. Convert Windows paths to Unix-style for consistency.

**Line Count Command**:
```bash
# Unix/Linux/Mac
wc -l file.md

# Windows (if wc not available)
powershell -Command "(Get-Content file.md | Measure-Object -Line).Lines"
```

**Markdown Link Extraction Regex**:
```regex
\[([^\]]+)\]\(([^)]+)\)
```
Captures: [link text](file.md) → groups: text="link text", path="file.md"

---

## Task 2.2: Technical Completeness Validator Component

### Objectives

Validate that technical tasks include all necessary integration steps for Entity, Service, and API implementations. This validator ensures plans are not just architecturally sound but also execution-ready with complete integration workflows.

### Responsibilities

1. **Entity/Model Implementation Completeness**
   - Verify entity class creation
   - Check DbContext.DbSet<T> addition
   - Validate entity configuration (OnModelCreating)
   - Ensure database migration workflow (add, update)

2. **Service Layer Implementation Completeness**
   - Verify interface definition (I{ServiceName})
   - Check implementation class creation
   - Validate DI registration in Program.cs
   - Ensure dependency injection in constructor

3. **API Controller Implementation Completeness**
   - Verify controller class creation
   - Check action methods with HTTP verbs
   - Validate request/response models
   - Ensure authorization/authentication setup
   - Verify middleware configuration (if needed)

### Implementation Approach

**Tool Usage**:
- **Read**: Access plan files to parse task content
- **Grep**: Search for technical keywords (Entity, DbContext, Service, Controller)
- **Pattern Matching**: Detect completeness checklist items

**Detection Patterns**:

**Entity Implementation Pattern**:
```
Keywords: "Entity", "DbContext", "migration", "EF Core"
Required Checklist Items:
- [ ] Entity class creation (e.g., "Create src/...Entity.cs")
- [ ] DbContext.DbSet<Entity> addition
- [ ] Entity configuration in OnModelCreating
- [ ] dotnet ef migrations add command
- [ ] dotnet ef database update command
```

**Service Layer Pattern**:
```
Keywords: "Service", "Interface", "DI", "dependency injection"
Required Checklist Items:
- [ ] Interface definition (e.g., "Create IService.cs")
- [ ] Implementation class (e.g., "Create Service.cs")
- [ ] DI registration (builder.Services.AddScoped<I, Impl>)
- [ ] Constructor injection of dependencies
```

**API Controller Pattern**:
```
Keywords: "Controller", "API", "endpoint", "action"
Required Checklist Items:
- [ ] Controller class creation
- [ ] Action methods with [HttpGet], [HttpPost], etc.
- [ ] Request/Response model definitions
- [ ] [Authorize] or [AllowAnonymous] attributes
- [ ] Middleware configuration (if custom middleware)
```

**Algorithm**:

```
Function ValidateTechnicalCompleteness(plan_files):
    completeness_score = 30
    violations = []

    # Step 1: Identify technical tasks
    entity_tasks = []
    service_tasks = []
    api_tasks = []

    for file in plan_files:
        content = Read(file)
        tasks = ExtractTaskSections(content)  # ### X.Y sections

        for task in tasks:
            if ContainsKeywords(task, ["Entity", "DbContext", "migration"]):
                entity_tasks.append({file: file, task: task})
            if ContainsKeywords(task, ["Service", "Interface", "DI"]):
                service_tasks.append({file: file, task: task})
            if ContainsKeywords(task, ["Controller", "API", "endpoint"]):
                api_tasks.append({file: file, task: task})

    # Step 2: Validate Entity tasks
    for entity_task in entity_tasks:
        missing_items = []

        if not Contains(entity_task.content, "DbContext.DbSet"):
            missing_items.append("DbContext.DbSet<T> addition")
        if not Contains(entity_task.content, "OnModelCreating"):
            missing_items.append("Entity configuration in OnModelCreating")
        if not Contains(entity_task.content, "dotnet ef migrations add"):
            missing_items.append("Database migration creation command")
        if not Contains(entity_task.content, "dotnet ef database update"):
            missing_items.append("Database update command")

        if missing_items:
            violations.append({
                type: "INCOMPLETE_ENTITY_TASK",
                severity: "CRITICAL",
                file: entity_task.file,
                task: entity_task.task.title,
                missing_items: missing_items,
                message: f"Entity task missing {len(missing_items)} integration step(s)",
                recommendation: f"Add missing steps: {', '.join(missing_items)}"
            })
            completeness_score -= 5

    # Step 3: Validate Service tasks
    for service_task in service_tasks:
        missing_items = []

        if not Contains(service_task.content, "interface I"):
            missing_items.append("Interface definition (I{ServiceName})")
        if not Contains(service_task.content, "builder.Services.Add") and not Contains(service_task.content, "services.Add"):
            missing_items.append("DI registration in Program.cs")

        if missing_items:
            violations.append({
                type: "INCOMPLETE_SERVICE_TASK",
                severity: "IMPORTANT",
                file: service_task.file,
                task: service_task.task.title,
                missing_items: missing_items,
                message: f"Service task missing {len(missing_items)} integration step(s)",
                recommendation: f"Add missing steps: {', '.join(missing_items)}"
            })
            completeness_score -= 3

    # Step 4: Validate API tasks
    for api_task in api_tasks:
        missing_items = []

        if not Contains(api_task.content, "[Http") and not Contains(api_task.content, "HttpGet"):
            missing_items.append("HTTP verb attributes ([HttpGet], [HttpPost], etc.)")
        if not Contains(api_task.content, "[Authorize]") and not Contains(api_task.content, "[AllowAnonymous]"):
            missing_items.append("Authorization attributes")

        if missing_items:
            violations.append({
                type: "INCOMPLETE_API_TASK",
                severity: "IMPORTANT",
                file: api_task.file,
                task: api_task.task.title,
                missing_items: missing_items,
                message: f"API task missing {len(missing_items)} integration step(s)",
                recommendation: f"Add missing steps: {', '.join(missing_items)}"
            })
            completeness_score -= 3

    completeness_score = max(0, completeness_score)

    return {
        score: completeness_score,
        violations: violations
    }
```

### Deliverables

#### 2.2.1 Technical Task Pattern Detection Logic

**Entity Pattern Detection**:
```
Trigger Keywords: "Entity", "Model", "DbContext", "EF Core", "database"
Required Patterns:
1. Class creation: "Create {path}/...Entity.cs" or "Implement {EntityName} entity"
2. DbSet addition: "DbContext.DbSet<{EntityName}>" or "Add DbSet to context"
3. Configuration: "OnModelCreating" or "entity configuration"
4. Migration: "dotnet ef migrations add" or "create migration"
5. Update: "dotnet ef database update" or "apply migration"
```

**Service Pattern Detection**:
```
Trigger Keywords: "Service", "Interface", "business logic", "application layer"
Required Patterns:
1. Interface: "interface I{ServiceName}" or "Create I{ServiceName}.cs"
2. Implementation: "class {ServiceName} : I{ServiceName}" or "Implement {ServiceName}"
3. DI registration: "builder.Services.AddScoped<I, Impl>" or "register in Program.cs"
4. Constructor injection: "constructor parameters" or "inject I{Dependency}"
```

**API Pattern Detection**:
```
Trigger Keywords: "Controller", "API", "endpoint", "action", "HTTP"
Required Patterns:
1. Controller class: "class {Name}Controller : ControllerBase"
2. Action methods: "[HttpGet]", "[HttpPost]", etc.
3. Models: "Request", "Response", "DTO"
4. Authorization: "[Authorize]" or "[AllowAnonymous]"
```

#### 2.2.2 Completeness Checklist Validation

**Entity Completeness Checklist**:
- [ ] Entity class creation (explicit file path)
- [ ] DbContext.DbSet<T> addition (line in DbContext)
- [ ] Entity configuration in OnModelCreating (fluent API)
- [ ] Migration creation command (dotnet ef migrations add)
- [ ] Database update command (dotnet ef database update)

**Service Completeness Checklist**:
- [ ] Interface definition with method signatures
- [ ] Implementation class with interface inheritance
- [ ] DI registration in Program.cs or Startup.cs
- [ ] Constructor with dependency injection parameters

**API Completeness Checklist**:
- [ ] Controller class inheriting from ControllerBase
- [ ] Action methods with HTTP verb attributes
- [ ] Request/Response models defined
- [ ] Authorization attributes ([Authorize] or [AllowAnonymous])
- [ ] (Optional) Middleware configuration if custom logic needed

#### 2.2.3 Missing Integration Step Identification

**Error Reporting Template**:
```markdown
**{severity}**: Incomplete {type} Task - {file}:{task_title}
- **Issue**: Missing {count} integration step(s) for {task_type} implementation
- **Missing Steps**:
  - {missing_item_1}
  - {missing_item_2}
  - ...
- **Recommendation**: Add missing integration steps to task deliverables
- **Example**:
  ```markdown
  - [ ] Add {EntityName} to DbContext as DbSet<{EntityName}>
  - [ ] Configure {EntityName} in OnModelCreating method
  - [ ] Run: dotnet ef migrations add Add{EntityName}Table
  - [ ] Run: dotnet ef database update
  ```
- **Impact**: -{points} points on Technical Completeness Score
```

### Acceptance Criteria

- [ ] Detects incomplete Entity implementations (missing DbContext, migrations)
- [ ] Identifies missing DI registrations for Services
- [ ] Flags missing middleware setup for APIs
- [ ] Provides specific missing integration steps for each violation
- [ ] Calculates technical completeness score (0-30 points)
- [ ] Distinguishes between CRITICAL (Entity) and IMPORTANT (Service/API) violations
- [ ] Error messages include code examples for fixes

### Technical Notes

**Keyword Detection Strategy**:
Use case-insensitive pattern matching to catch variations:
- "DbContext", "dbContext", "DbContext.DbSet", "database context"
- "interface IService", "Interface IService", "I{Name} interface"

**False Positive Mitigation**:
Require multiple keywords to confirm task type:
- Entity task: Must contain "Entity" + ("DbContext" OR "migration")
- Service task: Must contain "Service" + ("interface" OR "DI")

---

## Task 2.3: Execution Complexity Analyzer Component

### Objectives

Estimate execution complexity for each task and flag tasks exceeding 30 tool calls. Validate that plans follow "Plan ≠ Realization" principle (architecture only, no full implementation code).

### Responsibilities

1. **Tool Call Estimation per Task**
   - Parse task sections (### X.Y)
   - Count TODO items, code blocks, file operations
   - Apply heuristic estimation formulas
   - Flag tasks >30 tool calls

2. **"Plan ≠ Realization" Principle Validation**
   - Detect TODO markers (architecture indicator)
   - Detect full implementation code (violation indicator)
   - Distinguish architecture from implementation
   - Flag implementation code in plans

3. **Task Decomposition Recommendations**
   - Suggest task splitting for high complexity
   - Provide subtask breakdowns
   - Optimize for LLM execution (≤30 tool calls per task)

### Implementation Approach

**Tool Usage**:
- **Read**: Access plan files to parse tasks
- **Grep**: Search for implementation code patterns (class bodies, methods)
- **Pattern Matching**: Count TODO items, code blocks, file references

**Heuristic Estimation Formula**:

```
Tool Calls per Task =
    (File Creations × 1) +
    (File Edits × 2) +
    (Command Executions × 1) +
    (Search Operations × 1) +
    (Code Block Complexity Factor × 0.5)

Code Block Complexity Factor =
    Number of code blocks × Average lines per block / 50
```

**Examples**:

**Low Complexity Task (Estimated: 8 tool calls)**:
```markdown
### Task 1.1: Create Agent Specification
- [ ] Create agent.md with frontmatter (1 Write)
- [ ] Add purpose and responsibilities section (1 Edit)
- [ ] Document input/output specs (1 Edit)
- [ ] Add integration points (1 Edit)

Total: 1 creation + 3 edits = 1 + (3 × 2) = 7 tool calls
```

**Medium Complexity Task (Estimated: 22 tool calls)**:
```markdown
### Task 2.1: Implement Structure Validator
- [ ] Create PlanStructureValidator.cs (1 Write)
- [ ] Implement IValidator interface (1 Edit)
- [ ] Add file size validation method (1 Edit)
- [ ] Add reference integrity validation method (1 Edit)
- [ ] Add unit tests (1 Write)
- [ ] Add integration tests (1 Write)
- [ ] Register in DI (1 Edit of Program.cs)
- [ ] Run tests (1 Bash)

Total: 3 creations + 4 edits + 1 command = 3 + (4 × 2) + 1 = 12 tool calls
Plus 10 code blocks averaging 20 lines = 10 × 20 / 50 × 0.5 = 2
Total: 14 tool calls (well under 30)
```

**High Complexity Task (Estimated: 45 tool calls) - FLAGGED**:
```markdown
### Task 3.1: Implement Complete Validation System
- [ ] Create 5 validator classes (5 Write)
- [ ] Implement 15 validation methods (15 Edit)
- [ ] Add 20 unit tests (20 Write/Edit)
- [ ] Create 3 integration test suites (3 Write)
- [ ] Register all validators in DI (1 Edit)
- [ ] Run full test suite (1 Bash)

Total: 8 creations + 16 edits + 1 command = 8 + (16 × 2) + 1 = 41 tool calls
**FLAGGED**: Exceeds 30 tool call threshold - RECOMMEND DECOMPOSITION
```

**Algorithm**:

```
Function AnalyzeExecutionComplexity(plan_files):
    complexity_violations = []
    plan_realization_violations = []
    execution_score = 20

    for file in plan_files:
        content = Read(file)
        tasks = ExtractTaskSections(content)

        for task in tasks:
            # Estimate tool calls
            file_creations = CountPattern(task.content, r"\[ \] Create .+\.(cs|md|json)")
            file_edits = CountPattern(task.content, r"\[ \] (Update|Edit|Add to|Modify) .+\.(cs|md|json)")
            commands = CountPattern(task.content, r"\[ \] Run:|dotnet |pwsh |bash")
            searches = CountPattern(task.content, r"\[ \] Search|Find|Grep")
            code_blocks = CountCodeBlocks(task.content)

            tool_calls = (file_creations × 1) + (file_edits × 2) + (commands × 1) + (searches × 1)
            code_complexity = (code_blocks.count × code_blocks.avg_lines / 50) × 0.5
            estimated_calls = tool_calls + code_complexity

            if estimated_calls > 30:
                complexity_violations.append({
                    type: "HIGH_EXECUTION_COMPLEXITY",
                    severity: "IMPORTANT",
                    file: file,
                    task: task.title,
                    estimated_calls: estimated_calls,
                    threshold: 30,
                    message: f"Task estimated at {estimated_calls} tool calls (exceeds 30-call threshold)",
                    recommendation: "Decompose into 2-3 subtasks, each under 30 tool calls"
                })
                execution_score -= 2

            # Validate "Plan ≠ Realization" principle
            has_todos = CountPattern(task.content, r"TODO:|TBD:|To be implemented")
            has_interfaces = CountPattern(task.content, r"interface I\w+")
            has_implementation = CountPattern(task.content, r"(public|private) \w+ \w+\([^)]*\)\s*\{")

            implementation_ratio = has_implementation / max(1, has_todos + has_interfaces)

            if implementation_ratio > 0.5 and has_implementation > 3:
                plan_realization_violations.append({
                    type: "PLAN_VS_REALIZATION_VIOLATION",
                    severity: "IMPORTANT",
                    file: file,
                    task: task.title,
                    implementation_count: has_implementation,
                    message: f"Task contains {has_implementation} implementation code blocks (violates 'Plan ≠ Realization')",
                    recommendation: "Remove implementation details, keep architecture and TODO markers only"
                })
                execution_score -= 3

    execution_score = max(0, execution_score)

    return {
        score: execution_score,
        complexity_violations: complexity_violations,
        plan_realization_violations: plan_realization_violations
    }
```

### Deliverables

#### 2.3.1 Task Complexity Estimation Algorithm

**Estimation Formula** (detailed):

```
Base Tool Calls:
- File creation (Write tool): 1 call per file
- File edit (Read + Edit tools): 2 calls per file
- Command execution (Bash tool): 1 call per command
- Search operation (Grep/Glob tool): 1 call per search

Code Block Complexity Adjustment:
- Count code blocks in task (```language blocks)
- Calculate average lines per code block
- Complexity factor = (block_count × avg_lines) / 50 × 0.5
- Add to base tool calls

Total = Base Tool Calls + Code Block Complexity Factor
```

**Complexity Thresholds**:
- Low: ≤15 tool calls (simple tasks, single file operations)
- Medium: 16-30 tool calls (typical tasks, multiple file operations)
- High: >30 tool calls (complex tasks, REQUIRES DECOMPOSITION)

#### 2.3.2 Threshold Enforcement (30 tool calls)

**Enforcement Policy**:
- Tasks ≤30 tool calls: PASS (no flag)
- Tasks 31-40 tool calls: WARN (recommend decomposition)
- Tasks >40 tool calls: CRITICAL (must decompose)

**Error Reporting Template**:
```markdown
**IMPORTANT**: High Execution Complexity - {file}:{task_title}
- **Issue**: Task estimated at {estimated_calls} tool calls (threshold: 30)
- **Breakdown**:
  - File creations: {file_creations} (× 1 = {file_creations} calls)
  - File edits: {file_edits} (× 2 = {file_edits * 2} calls)
  - Commands: {commands} (× 1 = {commands} calls)
  - Code complexity: {code_complexity} calls
- **Recommendation**: Decompose into {suggested_subtask_count} subtasks:
  - Subtask {N}.1: {suggested_breakdown_1}
  - Subtask {N}.2: {suggested_breakdown_2}
  - ...
- **Impact**: -{points} points on Execution Clarity Score
```

#### 2.3.3 "Plan vs. Implementation" Validation Logic

**Architecture Indicators (GOOD)**:
- TODO markers: "TODO: Implement...", "TBD:", "To be implemented"
- Interface definitions: "interface IService { MethodSignature(); }"
- High-level descriptions: "Validator checks plan structure for compliance"
- Abstract patterns: "Follow Entity-Service-Repository pattern"

**Implementation Indicators (BAD)**:
- Full method bodies: "public void Method() { /* 20 lines of code */ }"
- Specific values: "const int THRESHOLD = 90;", "private string _value = \"test\";"
- Detailed algorithms: "for (int i = 0; i < count; i++) { ... }"
- Exception handling: "try { ... } catch (Exception ex) { Log(ex); }"

**Violation Detection**:
```
If (implementation_code_blocks > 3 AND implementation_ratio > 0.5):
    FLAG as "Plan ≠ Realization" violation
    RECOMMEND: Remove implementation details, keep architecture only
```

**Error Reporting Template**:
```markdown
**IMPORTANT**: Plan vs. Realization Violation - {file}:{task_title}
- **Issue**: Task contains {implementation_count} full implementation code blocks
- **Principle**: Plans should describe WHAT and WHY, not HOW (implementation details)
- **Detected Patterns**:
  - Full method implementations: {method_count}
  - Specific algorithm details: {algorithm_count}
  - Hardcoded values: {hardcoded_count}
- **Recommendation**:
  - Replace implementation code with TODO markers
  - Keep interface definitions and architecture descriptions
  - Example:
    ```markdown
    BEFORE (Implementation):
    public int CalculateScore() {
        int score = 0;
        foreach (var item in items) {
            score += item.Value;
        }
        return score;
    }

    AFTER (Architecture):
    TODO: Implement CalculateScore() method
    - Iterate through validation items
    - Sum individual scores
    - Return total score (0-100 range)
    ```
- **Impact**: -{points} points on Execution Clarity Score
```

### Acceptance Criteria

- [ ] Accurately estimates task complexity within ±5 tool calls
- [ ] Flags tasks exceeding 30 tool calls for decomposition
- [ ] Distinguishes architecture from implementation code
- [ ] Detects "Plan ≠ Realization" violations (≥3 implementation blocks)
- [ ] Provides specific decomposition recommendations for complex tasks
- [ ] Calculates execution clarity score (0-20 points)
- [ ] Error messages include before/after examples for fixes

### Technical Notes

**Code Block Detection**:
Use regex to match markdown code blocks:
```regex
```(\w+)?\n([\s\S]+?)\n```
```
Captures language (optional) and content for complexity analysis.

**Heuristic Calibration**:
Validate estimation accuracy against 5+ completed tasks:
- Estimated: 25 tool calls vs. Actual: 28 tool calls (88% accuracy)
- Adjust formula weights if accuracy <85%

---

## Phase 2 Completion Criteria

### All Tasks Complete When:

- [ ] Structure validation logic implemented in prompt.md
- [ ] Technical completeness validation logic implemented in prompt.md
- [ ] Execution complexity analyzer logic implemented in prompt.md
- [ ] All error message templates defined
- [ ] Scoring contribution formulas integrated with Phase 1 algorithm
- [ ] All acceptance criteria for Phase 2 met

### Quality Gates:

- [ ] Structure validator detects all GOLDEN RULE violations (100% recall)
- [ ] Technical completeness validator identifies missing integration steps (≥90% accuracy)
- [ ] Complexity analyzer estimates tool calls within ±5 (≥85% accuracy)
- [ ] "Plan ≠ Realization" validator distinguishes architecture from implementation

### Next Phase:

After Phase 2 completion, proceed to **Phase 3: Scoring and Reporting Engine**. See `phase-3-scoring-reporting.md` for detailed tasks.

---

**Phase Status**: PENDING
**Last Updated**: 2025-10-14
**Next Review**: After Task 2.3 completion
