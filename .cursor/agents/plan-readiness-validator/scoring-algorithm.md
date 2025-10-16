# LLM Readiness Scoring Algorithm

**Version**: 1.0
**Date**: 2025-10-14
**Purpose**: Define objective, reproducible scoring methodology for work plan quality assessment

---

## Overview

The LLM Readiness Score is a comprehensive quality metric (0-100 scale) that assesses whether a work plan is ready for LLM-based execution. Plans scoring ≥90% are considered READY for execution, while plans <90% require improvement before execution can proceed safely.

**Design Philosophy**:
- **Objective**: Minimize subjective judgment, maximize reproducibility
- **Actionable**: Each point deduction has specific remediation guidance
- **Balanced**: No single dimension dominates (4 dimensions, weighted appropriately)
- **Pragmatic**: 90% threshold allows minor issues while maintaining quality

---

## Scoring Dimensions

### Dimension Weights

| Dimension | Points | Weight | Focus |
|-----------|--------|--------|-------|
| **Task Specificity** | 0-30 | 30% | Concrete details for execution |
| **Technical Completeness** | 0-30 | 30% | Integration steps, not just implementation |
| **Execution Clarity** | 0-20 | 20% | Step-by-step decomposition, dependencies |
| **Structure Compliance** | 0-20 | 20% | Catalogization, file size, references |
| **TOTAL** | **100** | **100%** | **Overall readiness** |

**Pass Threshold**: ≥90 points = READY, <90 points = REQUIRES_IMPROVEMENT

**Rationale for Weights**:
- Technical completeness and task specificity are most critical (30% each) because missing integration steps or vague tasks cause execution failures
- Execution clarity is important but less critical (20%) because LLMs can infer some implicit dependencies
- Structure compliance is important for maintainability but doesn't block execution (20%)

---

## Dimension 1: Task Specificity (0-30 points)

**Purpose**: Ensure tasks contain concrete, unambiguous details that LLMs can execute without guesswork

### Sub-Criteria

#### 1.1 Concrete File Paths (0-10 points)

**Measurement**:
```
For each task (### X.Y):
  - Check for file path pattern: /path/to/file.ext or path\to\file.ext
  - Count tasks with explicit file paths vs. total tasks
  - Calculate: (tasks_with_paths / total_tasks) × 10

Score = (tasks_with_paths / total_tasks) × 10
```

**Examples**:

| Description | Score | Rationale |
|-------------|-------|-----------|
| `**File**: src/Orchestra.Core/Services/AuthService.cs` | 10/10 | Explicit, absolute file path |
| `Create AuthService in Services folder` | 5/10 | Directory mentioned but not full path |
| `Implement authentication service` | 0/10 | No file path specified |

**Common Issues**:
- Generic descriptions: "Create service" without file location
- Relative paths unclear: "in the Services folder" (which Services folder?)
- Missing file extensions: "AuthService" instead of "AuthService.cs"

**Remediation**:
```markdown
❌ BEFORE:
### 2.1 Create Authentication Service
Implement the authentication service with login/logout methods.

✅ AFTER:
### 2.1 Create Authentication Service
**File**: `src/Orchestra.Core/Services/AuthService.cs`

- [ ] Create AuthService class implementing IAuthenticationService
- [ ] Add methods: LoginAsync(), LogoutAsync(), ValidateTokenAsync()
```

---

#### 1.2 Specific Class/Interface Names (0-10 points)

**Measurement**:
```
For each technical task (creates code):
  - Check for specific class/interface names (PascalCase identifiers)
  - Count tasks with specific names vs. total technical tasks
  - Calculate: (tasks_with_names / total_technical_tasks) × 10

Score = (tasks_with_names / total_technical_tasks) × 10
```

**Examples**:

| Description | Score | Rationale |
|-------------|-------|-----------|
| `Create IAuthenticationService interface` | 10/10 | Specific interface name |
| `Create authentication interface` | 5/10 | Type mentioned but name generic |
| `Create service interface` | 0/10 | Completely generic |

**Common Issues**:
- Generic names: "create service" without specific identifier
- Missing naming convention: "authentication service" instead of "AuthenticationService"
- Unclear type: "create authentication" (class? interface? both?)

**Remediation**:
```markdown
❌ BEFORE:
### 2.2 Create Service Interface
Define the interface for authentication operations.

✅ AFTER:
### 2.2 Create IAuthenticationService Interface
**File**: `src/Orchestra.Core/Abstractions/IAuthenticationService.cs`

- [ ] Create IAuthenticationService interface
- [ ] Define methods: Task<AuthResult> LoginAsync(string username, string password)
- [ ] Define methods: Task LogoutAsync(string token)
- [ ] Define methods: Task<bool> ValidateTokenAsync(string token)
```

---

#### 1.3 Clear Acceptance Criteria (0-10 points)

**Measurement**:
```
For each task:
  - Check for explicit acceptance criteria section OR checklist items
  - Patterns: "**Acceptance Criteria**:", "- [ ]" checkboxes, "**Expected:**"
  - Count tasks with criteria vs. total tasks
  - Calculate: (tasks_with_criteria / total_tasks) × 10

Score = (tasks_with_criteria / total_tasks) × 10
```

**Examples**:

| Task Structure | Score | Rationale |
|----------------|-------|-----------|
| Task with "**Acceptance Criteria**:" section and 3+ items | 10/10 | Explicit, comprehensive |
| Task with "- [ ]" checklist items (5+ items) | 8/10 | Implicit criteria via checkboxes |
| Task with vague "complete when..." statement | 3/10 | Ambiguous criteria |
| Task with no completion guidance | 0/10 | No criteria specified |

**Common Issues**:
- Implied criteria: "implement service" without defining what "complete" means
- Vague criteria: "when authentication works" (how to verify?)
- Missing test criteria: no mention of test pass requirements

**Remediation**:
```markdown
❌ BEFORE:
### 2.3 Implement AuthService
Implement the authentication service with all required methods.

✅ AFTER:
### 2.3 Implement AuthService
**File**: `src/Orchestra.Core/Services/AuthService.cs`

**Acceptance Criteria**:
- [ ] AuthService class implements IAuthenticationService
- [ ] All methods throw NotImplementedException() with TODO comments
- [ ] Constructor accepts IUserRepository via DI
- [ ] Class registered in DI container (Program.cs updated)
- [ ] Code compiles without errors
- [ ] Unit test file created (AuthServiceTests.cs) with test stubs
```

---

### Task Specificity Scoring Table

| Total Score | Tasks with Paths | Tasks with Names | Tasks with Criteria | Assessment |
|-------------|------------------|------------------|---------------------|------------|
| 28-30 | ≥90% | ≥90% | ≥90% | Excellent specificity |
| 24-27 | 80-89% | 80-89% | 80-89% | Good specificity, minor gaps |
| 18-23 | 60-79% | 60-79% | 60-79% | Moderate specificity, improvement needed |
| <18 | <60% | <60% | <60% | Poor specificity, major improvement needed |

---

## Dimension 2: Technical Completeness (0-30 points)

**Purpose**: Ensure technical tasks include ALL integration steps, not just implementation code

### Sub-Criteria

#### 2.1 Integration Steps (0-15 points)

**Measurement**:
```
For each technical task type:
  - Entity/Model tasks: Check for DbContext, Entity config, Migration
  - Service tasks: Check for Interface, DI registration, Program.cs update
  - API tasks: Check for Authorization, Middleware, Routing

Count tasks with ALL integration steps vs. total technical tasks
Calculate: (complete_tasks / total_technical_tasks) × 15

Score = (complete_tasks / total_technical_tasks) × 15
```

**Entity/Model Task Checklist** (ALL required):
- [ ] Entity class creation
- [ ] DbContext.DbSet registration (`DbSet<EntityName>`)
- [ ] Entity configuration in `OnModelCreating` (indexes, relationships)
- [ ] Database migration creation (`dotnet ef migrations add ...`)

**Service Task Checklist** (ALL required):
- [ ] Interface definition (`I{ServiceName}`)
- [ ] Implementation class (`{ServiceName}`)
- [ ] DI registration in `Program.cs` or `Startup.cs`
- [ ] Dependencies specified (constructor injection)

**API Task Checklist** (ALL required):
- [ ] Controller class with action methods
- [ ] Request/Response models (DTOs)
- [ ] Authorization attributes (`[Authorize]`, `[AllowAnonymous]`)
- [ ] Middleware configuration (`UseAuthentication`, `UseAuthorization`)
- [ ] Routing setup (explicit or convention-based)

**Scoring**:

| Completeness | Score | Description |
|--------------|-------|-------------|
| 100% of tasks have ALL steps | 15/15 | Perfect integration planning |
| 90-99% of tasks complete | 13-14/15 | Minor gaps, mostly complete |
| 80-89% of tasks complete | 11-12/15 | Some missing steps |
| 70-79% of tasks complete | 9-10/15 | Significant gaps |
| <70% of tasks complete | <9/15 | Major integration issues |

**Common Issues**:

**Entity Task Missing Integration**:
```markdown
❌ INCOMPLETE:
### 2.1 Create User Entity
- [ ] Create User.cs with properties
- [ ] Add validation attributes

✅ COMPLETE:
### 2.1A: Create User Entity Class
- [ ] Create Core/Models/User.cs
- [ ] Define properties: Id, Username, Email, PasswordHash, Salt
- [ ] Add validation attributes: [Required], [MaxLength(100)]

### 2.1B: Integrate with Entity Framework
- [ ] Add DbSet<User> in ApplicationDbContext
- [ ] Configure entity in OnModelCreating:
  - Add unique index on Username
  - Add index on Email
  - Set PasswordHash and Salt as required
- [ ] Create migration: `dotnet ef migrations add AddUserTable`

### 2.1C: Verify Integration
- [ ] Code compiles without errors
- [ ] Apply migration to dev database
- [ ] Verify User table created with correct schema
```

**Service Task Missing DI**:
```markdown
❌ INCOMPLETE:
### 3.1 Implement AuthService
- [ ] Create AuthService class
- [ ] Implement LoginAsync method
- [ ] Implement LogoutAsync method

✅ COMPLETE:
### 3.1A: Create IAuthenticationService Interface
- [ ] Create Core/Abstractions/IAuthenticationService.cs
- [ ] Define method signatures with XML documentation

### 3.1B: Implement AuthService
- [ ] Create Core/Services/AuthService.cs implementing IAuthenticationService
- [ ] Add constructor with dependencies: IUserRepository, ILogger<AuthService>
- [ ] Implement methods with TODO comments and NotImplementedException

### 3.1C: Register in DI Container
- [ ] Update Program.cs or Startup.cs
- [ ] Add: `services.AddScoped<IAuthenticationService, AuthService>()`
- [ ] Verify dependencies resolve: IUserRepository, ILogger registered

### 3.1D: Verify DI Resolution
- [ ] Code compiles without errors
- [ ] Start application and verify no DI resolution errors
- [ ] Create unit test to verify DI registration
```

**API Task Missing Middleware**:
```markdown
❌ INCOMPLETE:
### 4.1 Create Auth API Controller
- [ ] Create AuthController with Login endpoint
- [ ] Add Logout endpoint

✅ COMPLETE:
### 4.1A: Create AuthController
- [ ] Create API/Controllers/AuthController.cs
- [ ] Add [ApiController] and [Route("api/[controller]")] attributes
- [ ] Add constructor with IAuthenticationService dependency

### 4.1B: Implement Endpoints
- [ ] Create Login endpoint: POST /api/auth/login
  - Request model: LoginRequest (username, password)
  - Response model: LoginResponse (token, expiration)
- [ ] Create Logout endpoint: POST /api/auth/logout
  - Authorization: [Authorize] attribute
  - Request model: LogoutRequest (token)

### 4.1C: Configure Middleware and Routing
- [ ] Update Program.cs to add authentication middleware:
  - `app.UseAuthentication();`
  - `app.UseAuthorization();`
- [ ] Ensure middleware order: Authentication → Authorization → MapControllers
- [ ] Configure CORS for API endpoints (if needed)

### 4.1D: Verify API Configuration
- [ ] Code compiles without errors
- [ ] Start application and verify endpoints registered (Swagger UI)
- [ ] Test Login endpoint returns 200 OK or 401 Unauthorized
```

---

#### 2.2 Migration Workflow (0-10 points)

**Measurement**:
```
For tasks involving database changes:
  - Check for migration creation command (dotnet ef migrations add)
  - Check for migration application step (apply to dev DB)
  - Check for rollback consideration (optional but good practice)

Count tasks with migration workflow vs. total DB tasks
Calculate: (tasks_with_migrations / total_db_tasks) × 10

Score = (tasks_with_migrations / total_db_tasks) × 10
```

**Migration Workflow Checklist**:
- [ ] Migration creation command specified
- [ ] Migration application step included
- [ ] Verification step (check DB schema)
- [ ] (Optional) Rollback strategy mentioned

**Scoring**:

| Workflow Coverage | Score | Description |
|-------------------|-------|-------------|
| All DB tasks have migration workflow | 10/10 | Complete migration planning |
| 80-99% have workflow | 8-9/10 | Minor gaps |
| 60-79% have workflow | 6-7/10 | Significant gaps |
| <60% have workflow | <6/10 | Major migration issues |

**Example**:
```markdown
✅ COMPLETE MIGRATION WORKFLOW:
### 2.1B: Integrate with Entity Framework
- [ ] Add DbSet<User> in ApplicationDbContext
- [ ] Configure entity in OnModelCreating (indexes, constraints)
- [ ] Create migration: `dotnet ef migrations add AddUserTable --project src/Orchestra.Core`
- [ ] Review migration file for correctness
- [ ] Apply migration to dev database: `dotnet ef database update --project src/Orchestra.API`
- [ ] Verify User table exists with correct schema (pgAdmin/SSMS)
- [ ] (Rollback if needed): `dotnet ef database update PreviousMigration`
```

---

#### 2.3 Error Handling Patterns (0-5 points)

**Measurement**:
```
For technical tasks:
  - Check for error handling mentions (try-catch, validation, error responses)
  - Check for logging patterns (ILogger usage)
  - Count tasks with error handling vs. total technical tasks

Calculate: (tasks_with_error_handling / total_technical_tasks) × 5

Score = (tasks_with_error_handling / total_technical_tasks) × 5
```

**Error Handling Indicators**:
- try-catch patterns mentioned
- Validation logic specified (FluentValidation, DataAnnotations)
- Error response models (ErrorResponse, ValidationError)
- Logging patterns (ILogger<T> usage)

**Scoring**:

| Coverage | Score | Description |
|----------|-------|-------------|
| ≥80% tasks mention error handling | 5/5 | Comprehensive error planning |
| 60-79% tasks mention error handling | 4/5 | Good coverage |
| 40-59% tasks mention error handling | 3/5 | Moderate coverage |
| <40% tasks mention error handling | <3/5 | Insufficient error planning |

**Example**:
```markdown
✅ WITH ERROR HANDLING:
### 3.1B: Implement AuthService
- [ ] Implement LoginAsync method:
  - Validate input (username and password not null/empty)
  - Try-catch for database exceptions
  - Log authentication attempts (success/failure)
  - Return AuthResult with success/failure status
- [ ] Error cases:
  - User not found → return Failed("Invalid credentials")
  - Password mismatch → return Failed("Invalid credentials")
  - Database error → log exception, return Failed("Authentication error")
```

---

### Technical Completeness Scoring Table

| Total Score | Integration Steps | Migration Workflow | Error Handling | Assessment |
|-------------|-------------------|--------------------|--------------------|------------|
| 28-30 | 14-15/15 | 9-10/10 | 5/5 | Excellent completeness |
| 24-27 | 12-13/15 | 7-8/10 | 4/5 | Good completeness, minor gaps |
| 18-23 | 9-11/15 | 6/10 | 3/5 | Moderate completeness, improvement needed |
| <18 | <9/15 | <6/10 | <3/5 | Poor completeness, major gaps |

---

## Dimension 3: Execution Clarity (0-20 points)

**Purpose**: Ensure tasks are decomposed into clear, executable steps with explicit dependencies

### Sub-Criteria

#### 3.1 Step-by-Step Decomposition (0-10 points)

**Measurement**:
```
For each task:
  - Check for numbered steps, bullet points, or checklist items
  - Count depth of decomposition (1 level = basic, 2+ levels = detailed)
  - Count tasks with clear step breakdown vs. total tasks

Calculate: (tasks_with_steps / total_tasks) × 10

Score = (tasks_with_steps / total_tasks) × 10
```

**Decomposition Levels**:

| Level | Description | Example |
|-------|-------------|---------|
| **0** | No decomposition | "Implement authentication" |
| **1** | Basic list | "1. Create service, 2. Add methods, 3. Test" |
| **2** | Detailed steps | "1.1 Create interface, 1.2 Implement class, 1.3 Add method A, 1.4 Add method B" |
| **3** | Sub-task files | "See 01-service-interface.md, 02-service-implementation.md" |

**Scoring**:

| Decomposition Quality | Score | Description |
|-----------------------|-------|-------------|
| All tasks Level 2+ decomposition | 10/10 | Excellent clarity |
| 80-99% tasks Level 2+ | 8-9/10 | Good clarity, minor gaps |
| 60-79% tasks Level 1+ | 6-7/10 | Basic clarity, improvement needed |
| <60% tasks with decomposition | <6/10 | Poor clarity, major gaps |

**Example**:
```markdown
❌ LEVEL 0 (0 points):
### 2.1 Authentication Feature
Implement authentication with login and logout.

✅ LEVEL 2 (10 points):
### 2.1 Authentication Feature

#### 2.1.1 Create User Entity
- [ ] Create Core/Models/User.cs with properties
- [ ] Add validation attributes

#### 2.1.2 Integrate Entity Framework
- [ ] Add DbSet<User> in ApplicationDbContext
- [ ] Create migration

#### 2.1.3 Create AuthService
- [ ] Create IAuthenticationService interface
- [ ] Implement AuthService class
- [ ] Register in DI

#### 2.1.4 Create API Controller
- [ ] Create AuthController with Login/Logout
- [ ] Add authorization attributes
```

---

#### 3.2 Dependencies Clearly Identified (0-10 points)

**Measurement**:
```
For each task:
  - Check for dependency keywords: "After", "Requires", "Depends on", "Prerequisite"
  - Check for explicit task references: "After 2.1 completes", "Requires 1.2"
  - Count tasks with explicit dependencies vs. total tasks (exclude truly independent tasks)

Calculate: (tasks_with_dependencies / tasks_needing_dependencies) × 10

Score = (tasks_with_dependencies / tasks_needing_dependencies) × 10
```

**Dependency Indicators**:
- **Explicit**: "**Dependencies**: 2.1, 2.2 must be complete"
- **Implicit**: "After entity creation, implement repository"
- **None**: No dependency information (problematic if dependencies exist)

**Scoring**:

| Dependency Clarity | Score | Description |
|--------------------|-------|-------------|
| All dependent tasks have explicit dependencies | 10/10 | Perfect dependency management |
| 80-99% have explicit dependencies | 8-9/10 | Good dependency clarity |
| 60-79% have dependencies | 6-7/10 | Some missing dependencies |
| <60% have dependencies | <6/10 | Poor dependency management |

**Example**:
```markdown
❌ NO DEPENDENCIES (0 points):
### 3.1 Create Repository
Implement UserRepository for database operations.

### 3.2 Create Service
Implement AuthService using the repository.

✅ EXPLICIT DEPENDENCIES (10 points):
### 3.1 Create UserRepository
**Dependencies**: None (can start immediately)
**Blocks**: 3.2 (AuthService needs UserRepository)

- [ ] Create IUserRepository interface
- [ ] Implement UserRepository class
- [ ] Register in DI

### 3.2 Create AuthService
**Dependencies**:
- CRITICAL: 3.1 UserRepository must be complete (interface needed)
- CRITICAL: 2.1B User entity must be in DbContext

**Blocks**: 4.1 (AuthController needs AuthService)

- [ ] Create IAuthenticationService interface
- [ ] Implement AuthService (inject IUserRepository)
- [ ] Register in DI
```

---

### Execution Clarity Deductions

**Complexity Violations** (-5 to -15 points):

| Violation | Deduction | Rationale |
|-----------|-----------|-----------|
| 1 task >30 tool calls | -5 | Single over-complex task |
| 2-3 tasks >30 tool calls | -10 | Multiple over-complex tasks |
| 4+ tasks >30 tool calls | -15 | Systemic complexity issue |

**Example**:
```
Plan has 12 tasks total:
- 10 tasks estimated at 15-25 tool calls each ✅
- 2 tasks estimated at 35-40 tool calls each ❌

Deduction: -10 points (2 over-complex tasks)
```

---

### Execution Clarity Scoring Table

| Total Score | Step Decomposition | Dependencies | Complexity | Assessment |
|-------------|--------------------|--------------|-----------||------------|
| 18-20 | 9-10/10 | 9-10/10 | 0 violations | Excellent clarity |
| 15-17 | 8/10 | 8/10 | 0-1 violations | Good clarity |
| 12-14 | 6-7/10 | 6-7/10 | 2-3 violations | Moderate clarity, improvement needed |
| <12 | <6/10 | <6/10 | 4+ violations | Poor clarity, major issues |

---

## Dimension 4: Structure Compliance (0-20 points)

**Purpose**: Ensure plan follows catalogization rules, file size limits, and reference integrity

### Sub-Criteria

#### 4.1 Catalogization Rules (0-10 points)

**Measurement**:
```
Check GOLDEN RULES:
  - RULE #1: Directory name matches file name (without .md)
  - RULE #2: Coordinator file outside its directory

Count violations:
  - 0 violations → 10 points
  - 1-2 violations → 5 points
  - 3+ violations → 0 points

Score = max(0, 10 - (violations × 5))
```

**GOLDEN RULE #1 Validation**:
```
For each coordinator file (parent):
  - Extract name without .md: "01-Data-Models.md" → "01-Data-Models"
  - Check if directory exists: "01-Data-Models/"
  - If directory exists BUT name doesn't match → VIOLATION

Examples:
✅ VALID: 01-Data-Models.md → 01-Data-Models/
❌ INVALID: 01-Data-Models.md → 01-architecture/
❌ INVALID: 01-Data-Models.md → 01-data-models/ (case mismatch)
```

**GOLDEN RULE #2 Validation**:
```
For each directory:
  - Check if directory contains file with same name
  - If file inside directory matches directory name → VIOLATION

Examples:
✅ VALID:
  01-Data-Models.md (outside)
  01-Data-Models/ (directory)
    ├── 01-api-models.md
    └── 02-db-entities.md

❌ INVALID:
  01-Data-Models/ (directory)
    └── 01-Data-Models.md (coordinator inside directory)
```

**Scoring**:

| Violations | Score | Assessment |
|------------|-------|------------|
| 0 | 10/10 | Perfect catalogization |
| 1-2 | 5/10 | Minor violations, fixable |
| 3+ | 0/10 | Major violations, requires restructuring |

---

#### 4.2 File Size Limits (0-5 points)

**Measurement**:
```
For each .md file in plan:
  - Count lines (wc -l or similar)
  - Check against limits:
    - ≤400 lines → PASS
    - 251-400 lines → WARNING (acceptable but monitor)
    - >400 lines → VIOLATION

Calculate:
  - 0 violations → 5 points
  - 1-2 violations → 3 points
  - 3+ violations → 0 points

Score = max(0, 5 - (violations × 1.5))
```

**File Size Categories**:

| Line Count | Status | Action |
|------------|--------|--------|
| 0-250 | ✅ GOOD | Ideal size for single context |
| 251-400 | ⚠️ WARNING | Acceptable, monitor for decomposition |
| 401-500 | ❌ VIOLATION | Exceeds limit, decompose required |
| 500+ | ❌ CRITICAL | Severely oversized, immediate decomposition |

**Scoring**:

| File Size Violations | Score | Assessment |
|----------------------|-------|------------|
| All files ≤400 lines | 5/5 | Perfect file sizing |
| 1-2 files >400 lines | 3/5 | Minor violations |
| 3+ files >400 lines | 0/5 | Major size issues |

---

#### 4.3 Reference Integrity (0-5 points)

**Measurement**:
```
For each parent-child relationship:
  - Parent must link to child: "- [ ] [/child-file.md]"
  - Child must link to parent: "**Родительский план**: [parent.md]"
  - File paths must be valid (file exists)

Count broken references:
  - 0 broken → 5 points
  - 1-2 broken → 3 points
  - 3+ broken → 0 points

Score = max(0, 5 - (broken_refs × 1.5))
```

**Reference Patterns**:

**Parent → Child**:
```markdown
# Parent Plan

## Subtasks
- [ ] [/01-subtask-a.md](./01-Parent/01-subtask-a.md) Task A description
- [ ] [/02-subtask-b.md](./01-Parent/02-subtask-b.md) Task B description
```

**Child → Parent**:
```markdown
# Subtask A

**Родительский план**: [01-Parent.md](../01-Parent.md)

## Task details
...
```

**Common Issues**:
- Broken file paths (file doesn't exist)
- Missing parent reference in child
- Missing child reference in parent
- Circular references (A → B → A)

**Scoring**:

| Broken References | Score | Assessment |
|-------------------|-------|------------|
| 0 | 5/5 | Perfect reference integrity |
| 1-2 | 3/5 | Minor broken links |
| 3+ | 0/5 | Major reference issues |

---

### Structure Compliance Scoring Table

| Total Score | Catalogization | File Size | References | Assessment |
|-------------|----------------|-----------|------------|------------|
| 18-20 | 10/10 | 5/5 | 4-5/5 | Excellent structure |
| 15-17 | 10/10 | 3-5/5 | 3/5 | Good structure, minor issues |
| 10-14 | 5-10/10 | 0-3/5 | 0-3/5 | Moderate structure issues |
| <10 | 0-5/10 | 0/5 | 0/5 | Major structure problems |

---

## Score Aggregation and Interpretation

### Final Score Calculation

```
LLM Readiness Score = Task Specificity + Technical Completeness + Execution Clarity + Structure Compliance

Example:
  Task Specificity:        28/30
  Technical Completeness:  30/30
  Execution Clarity:       18/20
  Structure Compliance:    18/20
  --------------------------------
  TOTAL:                   94/100 ✅ READY
```

### Pass/Fail Determination

```
IF score ≥90:
  Status = READY
  Next Action = plan-task-executor (begin execution)

ELSE IF score <90:
  Status = REQUIRES_IMPROVEMENT
  Next Action = work-plan-architect (fix issues)
```

### Score Ranges and Interpretation

| Score Range | Status | Interpretation | Recommended Action |
|-------------|--------|----------------|--------------------|
| **90-100** | ✅ READY | Excellent quality, minimal issues | Proceed to execution |
| **80-89** | ⚠️ BORDERLINE | Good quality, some improvements needed | Address major issues, consider re-validation |
| **70-79** | ❌ REQUIRES_IMPROVEMENT | Moderate quality, significant gaps | Major revisions needed, re-validate |
| **<70** | ❌ CRITICAL | Poor quality, fundamental issues | Restart planning process |

### Dimension-Specific Thresholds

**Critical Dimensions** (must score ≥27/30 for READY):
- Technical Completeness: Missing integration steps block execution
- Task Specificity: Vague tasks cause execution ambiguity

**Important Dimensions** (must score ≥18/20 for READY):
- Execution Clarity: Unclear steps slow execution, may cause errors
- Structure Compliance: Poor structure affects maintainability, not execution

**Recommended Minimums for READY Status**:
- Task Specificity: ≥27/30 (90%)
- Technical Completeness: ≥27/30 (90%)
- Execution Clarity: ≥18/20 (90%)
- Structure Compliance: ≥18/20 (90%)

---

## Edge Cases and Special Handling

### Edge Case 1: Plan with No Tasks (Coordinator Only)

**Scenario**: Plan file exists but contains only overview, no executable tasks

**Handling**:
```
Task Specificity: 0/30 (no tasks to evaluate)
Technical Completeness: 0/30 (no technical tasks)
Execution Clarity: 0/20 (no execution steps)
Structure Compliance: [evaluate normally]

Final Score: 0-20/100 → REQUIRES_IMPROVEMENT

Recommendation: Add executable tasks to plan before re-validation
```

### Edge Case 2: Plan with Circular Dependencies

**Scenario**: Task A depends on Task B, Task B depends on Task A

**Handling**:
```
Execution Clarity: -10 points (dependency cycle detected)

Error in Report:
❌ CRITICAL: Circular dependency detected
  - Task 2.1 depends on 2.2
  - Task 2.2 depends on 2.1

Recommendation: Break circular dependency by:
  1. Splitting one task into pre-requisite and dependent parts
  2. Reordering tasks to remove cycle
```

### Edge Case 3: Plan Exceeding 400 Lines But Well-Decomposed

**Scenario**: Plan file >400 lines but has proper child files with detailed decomposition

**Handling**:
```
Structure Compliance: Deduct 3 points (file size violation)
BUT: If child files exist and are properly referenced → Add note in report

Note in Report:
⚠️ File size violation (523 lines) but plan is well-decomposed into child files.
Recommendation: Move more content to child files to reduce coordinator size to <400 lines.

Impact: Minor (does not block execution)
```

### Edge Case 4: Plan with TODO Markers Everywhere

**Scenario**: Plan heavily uses TODO comments (architectural planning)

**Handling**:
```
This is GOOD, not bad (per "Plan ≠ Realization" principle)

Task Specificity: NO deduction (TODO markers indicate architecture, not missing details)

Note in Report:
✅ Plan correctly uses TODO markers for architectural planning (25 TODOs detected)
This follows "Plan ≠ Realization" principle - TODOs indicate future implementation, not missing plan details.
```

### Edge Case 5: Plan with Full Implementation Code

**Scenario**: Plan contains complete method implementations, LINQ queries, business logic

**Handling**:
```
Task Specificity: -10 points (violates "Plan ≠ Realization")
Execution Clarity: -5 points (plan too detailed, execution ambiguous)

Error in Report:
❌ CRITICAL: Plan contains full implementation code
  - File: 02-service-implementation.md:78
  - Issue: Complete method implementation with business logic
  - Expected: Architecture only (method signatures + TODO comments)

Recommendation: Replace implementation code with:
  1. Method signatures only
  2. TODO comments indicating future implementation
  3. throw new NotImplementedException() placeholders
```

---

## Calibration and Validation

### Scoring Algorithm Validation

**Method**: Compare automated scores with manual systematic-plan-reviewer results

**Target**: ≥95% agreement (score within ±3 points)

**Validation Dataset**: 20+ work plans with manual review scores

**Calibration Process**:
1. Run validator on 20 manually reviewed plans
2. Calculate agreement rate: `(scores_within_3_points / total_plans) × 100`
3. If agreement <95% → Analyze discrepancies
4. Adjust weights or criteria as needed
5. Re-validate and repeat until ≥95% agreement

### Periodic Re-Calibration

**Frequency**: Quarterly or after major rule changes

**Triggers**:
- New planning rules added to common-plan-generator.mdc
- Consistent score discrepancies reported
- Change in plan structure patterns

**Process**:
1. Collect 20 recent plans with manual review scores
2. Run validator and compare scores
3. Identify systematic biases (e.g., always over-scoring structure compliance)
4. Adjust scoring weights or criteria
5. Document calibration changes in version history

---

## Version History

**Version 1.0** (2025-10-14):
- Initial scoring algorithm
- 4 dimensions: Task Specificity (30%), Technical Completeness (30%), Execution Clarity (20%), Structure Compliance (20%)
- Pass threshold: ≥90%
- Integration with agent transition matrix
- "Plan ≠ Realization" validation

**Planned Enhancements (Post-MVP)**:
- Machine learning-based scoring (train on validated plans)
- Custom rubric support (project-specific weights)
- Continuous calibration based on execution success rates

---

**Algorithm Status**: ACTIVE
**Owner**: Development Team
**Last Updated**: 2025-10-14
**Related Documentation**:
- Agent Specification: `.cursor/agents/plan-readiness-validator/agent.md`
- Prompt Template: `.cursor/agents/plan-readiness-validator/prompt.md`
- Implementation Plan: `Docs/plans/Plan-Readiness-Validator-Implementation-Plan.md`
