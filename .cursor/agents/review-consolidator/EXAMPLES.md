# review-consolidator Usage Examples

This document provides detailed examples of using review-consolidator in various scenarios.

---

## Example 1: Simple Consolidation (3 reviewers, 10 files)

### Scenario
You've implemented a new authentication service with 10 C# files (services, controllers, tests). You want to review the code quality before committing.

### Files to Review
```
src/Services/AuthService.cs (250 lines)
src/Services/TokenService.cs (180 lines)
src/Services/UserService.cs (220 lines)
src/Controllers/AuthController.cs (150 lines)
src/Controllers/UserController.cs (130 lines)
src/Models/UserModel.cs (80 lines)
src/Models/TokenModel.cs (60 lines)
src/Tests/AuthServiceTests.cs (320 lines)
src/Tests/TokenServiceTests.cs (180 lines)
src/Tests/UserServiceTests.cs (240 lines)
```

### Invocation
```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Review authentication feature implementation",
  prompt: `Review the authentication feature implementation for code quality issues.

Files (10 total, 1,810 lines):
- src/Services/AuthService.cs
- src/Services/TokenService.cs
- src/Services/UserService.cs
- src/Controllers/AuthController.cs
- src/Controllers/UserController.cs
- src/Models/UserModel.cs
- src/Models/TokenModel.cs
- src/Tests/AuthServiceTests.cs
- src/Tests/TokenServiceTests.cs
- src/Tests/UserServiceTests.cs

Review types: code-style-reviewer, code-principles-reviewer, test-healer

Focus areas:
- SOLID principles adherence
- Dependency injection patterns
- Test coverage and quality
- Naming conventions and code style
`,
  context: {
    reviewTypes: [
      'code-style-reviewer',
      'code-principles-reviewer',
      'test-healer'
    ],
    files: [
      'src/Services/AuthService.cs',
      'src/Services/TokenService.cs',
      'src/Services/UserService.cs',
      'src/Controllers/AuthController.cs',
      'src/Controllers/UserController.cs',
      'src/Models/UserModel.cs',
      'src/Models/TokenModel.cs',
      'src/Tests/AuthServiceTests.cs',
      'src/Tests/TokenServiceTests.cs',
      'src/Tests/UserServiceTests.cs'
    ]
  }
})
```

### Expected Console Output
```
🔄 review-consolidator: Starting parallel review with 3 reviewers

📋 Review Configuration:
   - Files: 10 (1,810 lines)
   - Reviewers: 3 (code-style-reviewer, code-principles-reviewer, test-healer)
   - Timeout: 5 minutes per reviewer

⏱️ [00:00] Launching reviewers in parallel...
   ✅ code-style-reviewer (started)
   ✅ code-principles-reviewer (started)
   ✅ test-healer (started)

⏱️ [02:15] code-style-reviewer completed (42 issues found)
⏱️ [02:48] code-principles-reviewer completed (38 issues found)
⏱️ [03:12] test-healer completed (47 issues found)

📊 Consolidation Phase:
   - Raw issues: 127 (42 + 38 + 47)
   - Deduplication algorithm: exact match + semantic similarity (threshold: 0.80)
   - Processing time: 12s

✅ Consolidation Complete:
   - Consolidated issues: 35 (72% reduction)
   - Priority breakdown: 8 P0, 15 P1, 12 P2
   - Common themes: 3 identified

📝 Reports Generated:
   ✅ Master Report: Docs/reviews/review-report-20250125123045.md
   ✅ Appendices:
      - Docs/reviews/appendices/code-style-reviewer-20250125123045.md
      - Docs/reviews/appendices/code-principles-reviewer-20250125123045.md
      - Docs/reviews/appendices/test-healer-20250125123045.md
   ✅ Traceability Matrix: Docs/reviews/review-traceability-20250125123045.md

⏱️ Total Duration: 3m 27s
   - Parallel execution: 3m 12s (3 reviewers)
   - Sequential equivalent: ~8m 45s
   - Time saved: 5m 18s (61% speedup)

🔄 Recommended Next Actions:

1. 🚨 CRITICAL: plan-task-executor
   Reason: Fix 8 critical P0 issues immediately
   Command: Use Task tool with subagent_type: "plan-task-executor"
   Parameters:
     task: "Fix P0 issues from authentication review"
     cycleId: "consolidator-executor-1706178645000"
     iteration: 2
     issues: [
       "P0: Missing null check for ILogger in AuthService.cs:42",
       "P0: DI violation - new TokenService() in AuthController.cs:67",
       ... (6 more P0 issues)
     ]
```

### Master Report Generated
**Location**: `Docs/reviews/review-report-20250125123045.md`

**Excerpt**:
```markdown
# Code Review Report - 2025-01-25 12:30:45

## Executive Summary
- **Total Issues**: 35 (8 P0, 15 P1, 12 P2)
- **Files Reviewed**: 10 (1,810 lines)
- **Reviewers**: 3 (code-style-reviewer, code-principles-reviewer, test-healer)
- **Review Duration**: 3m 27s
- **Deduplication**: 127 raw issues → 35 consolidated (72% reduction)
- **Parallel Speedup**: 61% (3m 27s vs 8m 45s sequential)

## Critical Issues (P0) - IMMEDIATE ACTION REQUIRED

### P0-1: Missing null check for ILogger dependency
**File**: `src/Services/AuthService.cs:42`
**Severity**: CRITICAL
**Description**: ILogger parameter not validated for null before use. Will throw NullReferenceException if DI fails.

**Code**:
```csharp
public AuthService(ILogger<AuthService> logger)
{
    _logger = logger; // ❌ No null check
    _logger.LogInformation("AuthService initialized");
}
```

**Recommendation**: Add null check with ArgumentNullException
```csharp
public AuthService(ILogger<AuthService> logger)
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _logger.LogInformation("AuthService initialized");
}
```

**Sources**: code-principles-reviewer (Issue #3), code-style-reviewer (Issue #5)

### P0-2: Dependency Injection violation
**File**: `src/Controllers/AuthController.cs:67`
**Severity**: CRITICAL
**Description**: Direct instantiation of TokenService using `new` instead of constructor injection. Violates SOLID (D) and breaks testability.

**Code**:
```csharp
public async Task<IActionResult> Login(LoginRequest request)
{
    var tokenService = new TokenService(); // ❌ DI violation
    var token = await tokenService.GenerateToken(user);
}
```

**Recommendation**: Use constructor injection
```csharp
// In constructor:
private readonly ITokenService _tokenService;
public AuthController(ITokenService tokenService)
{
    _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
}

// In method:
public async Task<IActionResult> Login(LoginRequest request)
{
    var token = await _tokenService.GenerateToken(user);
}
```

**Sources**: code-principles-reviewer (Issue #12), code-style-reviewer (Issue #18)

... (6 more P0 issues)

## Warnings (P1) - RECOMMENDED FIXES

### P1-1: Inconsistent error handling pattern
**File**: `src/Services/AuthService.cs:120, 145, 167`
**Severity**: WARNING
**Description**: Mixed error handling approaches - some methods throw exceptions, others return null. Should use consistent pattern.

**Recommendation**: Standardize on Result<T> pattern or consistent exception throwing

**Sources**: code-style-reviewer (Issue #8), code-principles-reviewer (Issue #22)

... (14 more P1 issues)

## Improvements (P2) - OPTIONAL ENHANCEMENTS

### P2-1: XML documentation missing for public methods
**File**: `src/Services/UserService.cs` (8 methods)
**Severity**: IMPROVEMENT
**Description**: Public API methods lack XML documentation comments

**Recommendation**: Add XML comments for IntelliSense and generated docs

**Sources**: code-style-reviewer (Issue #35)

... (11 more P2 issues)

## Common Themes

### Theme 1: Dependency Injection Violations (8 occurrences)
Files affected:
- src/Controllers/AuthController.cs (3 violations)
- src/Controllers/UserController.cs (2 violations)
- src/Services/AuthService.cs (2 violations)
- src/Services/TokenService.cs (1 violation)

**Pattern**: Direct instantiation using `new` instead of constructor injection

**Recommendation**: Comprehensive DI refactoring - pass all dependencies through constructors

### Theme 2: Missing Null Checks (5 occurrences)
Files affected:
- src/Services/AuthService.cs (2 violations)
- src/Services/UserService.cs (2 violations)
- src/Controllers/AuthController.cs (1 violation)

**Pattern**: Constructor parameters not validated for null

**Recommendation**: Add null checks with ArgumentNullException for all injected dependencies

### Theme 3: Test Coverage Gaps (4 occurrences)
Files affected:
- src/Tests/AuthServiceTests.cs (missing error path tests)
- src/Tests/UserServiceTests.cs (missing edge cases)

**Pattern**: Happy path tested, error scenarios untested

**Recommendation**: Add negative test cases and edge case coverage

## Prioritized Action Items

1. **Fix all P0 issues** (8 items) - IMMEDIATE
   - Add null checks for injected dependencies (3 items)
   - Replace `new` with constructor injection (5 items)

2. **Address P1 DI violations** (5 items) - THIS SPRINT
   - Standardize error handling pattern
   - Fix inconsistent naming conventions

3. **Add missing test coverage** (4 items) - THIS SPRINT
   - Error path tests for AuthService
   - Edge case tests for UserService

4. **Improve documentation** (P2 items) - BACKLOG
   - XML comments for public APIs
   - README updates

## Metadata
- **Review Start**: 2025-01-25 12:30:00
- **Review End**: 2025-01-25 12:33:27
- **Total Duration**: 3m 27s
- **Parallel Execution Time**: 3m 12s (code-style-reviewer: 2m 15s, code-principles-reviewer: 2m 48s, test-healer: 3m 12s)
- **Sequential Equivalent**: 8m 45s (sum of individual times)
- **Time Saved**: 5m 18s (61% speedup)
- **Deduplication Stats**:
  - Raw issues: 127 (code-style: 42, code-principles: 38, test-healer: 47)
  - Exact matches eliminated: 68 (54%)
  - Semantic similarities merged: 24 (19%)
  - Consolidated issues: 35 (72% reduction)
- **Cache Performance**:
  - Files cached: 3/10 (30% hit rate)
  - Cache time saved: 8s
```

---

## Example 2: Large Codebase Review (100+ files)

### Scenario
Full codebase review before major v2.0 release. Need to review 127 C# files across entire solution.

### Invocation
```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Full codebase review for v2.0 release",
  prompt: `Comprehensive code quality review before v2.0 release.

Files: All C# source and test files in solution
Pattern: src/**/*.cs (127 files, ~18,500 lines)

Review types: code-style-reviewer, code-principles-reviewer, test-healer

Focus areas:
- SOLID principles
- Code style consistency
- Test coverage (target: >80%)
- Architecture violations
`,
  context: {
    reviewTypes: [
      'code-style-reviewer',
      'code-principles-reviewer',
      'test-healer'
    ],
    filePattern: 'src/**/*.cs'
  }
})
```

### Console Output (Abbreviated)
```
🔄 review-consolidator: Starting parallel review with 3 reviewers

📋 Review Configuration:
   - Files: 127 (18,547 lines)
   - Reviewers: 3 (code-style-reviewer, code-principles-reviewer, test-healer)
   - Timeout: 10 minutes per reviewer (auto-adjusted for large codebase)

⏱️ [00:00] Launching reviewers in parallel...

⏱️ [04:32] code-style-reviewer completed (185 issues found)
⏱️ [05:18] code-principles-reviewer completed (162 issues found)
⏱️ [05:42] test-healer completed (143 issues found)

📊 Consolidation Phase:
   - Raw issues: 490 (185 + 162 + 143)
   - Deduplication: 42s
   - Cache hits: 38/127 files (30%)

✅ Consolidation Complete:
   - Consolidated issues: 98 (80% reduction)
   - Priority breakdown: 22 P0, 45 P1, 31 P2

⏱️ Total Duration: 6m 24s
   - Sequential equivalent: ~17m 15s
   - Time saved: 10m 51s (63% speedup)
```

### Performance Metrics
| Metric | Value | Target | Status |
|--------|-------|--------|--------|
| Total Review Time | 6m 24s | <10 min | ✅ PASS |
| Parallel Speedup | 63% | >60% | ✅ PASS |
| Issues Found (Raw) | 490 | N/A | - |
| Issues After Dedup | 98 | N/A | - |
| Deduplication Ratio | 80% | >70% | ✅ PASS |
| Memory Usage (Peak) | 486 MB | <500 MB | ✅ PASS |
| Cache Hit Rate | 30% | >25% | ✅ PASS |

### Report Summary
**Location**: `Docs/reviews/review-report-20250125140530.md`

**Key Findings**:
- **22 P0 issues**: 15 DI violations, 5 null check missing, 2 security vulnerabilities
- **45 P1 issues**: 28 code style inconsistencies, 12 naming violations, 5 test gaps
- **31 P2 issues**: 18 missing XML docs, 8 refactoring opportunities, 5 performance suggestions

**Common Themes**:
1. **Dependency Injection violations** (15 P0 + 8 P1 = 23 total)
2. **Inconsistent naming conventions** (12 P1 occurrences across Controllers/)
3. **Test coverage gaps** (5 P1 + 3 P2 = 8 files missing tests)

---

## Example 3: Cycle Protection with Escalation

### Scenario
Implementing a complex refactoring. First review finds 20 issues. After fixes, second review still finds 12 P0 issues (low improvement). review-consolidator escalates.

### Cycle 1: Initial Review

**Invocation**:
```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Review DI refactoring (Cycle 1)",
  prompt: `Review dependency injection refactoring.

Files:
- src/Services/*.cs (5 files refactored)
- src/Controllers/*.cs (3 files refactored)

Review types: code-style-reviewer, code-principles-reviewer
`,
  context: {
    reviewTypes: ['code-style-reviewer', 'code-principles-reviewer'],
    files: [
      'src/Services/AuthService.cs',
      'src/Services/UserService.cs',
      'src/Services/TokenService.cs',
      'src/Services/ConfigService.cs',
      'src/Services/LoggingService.cs',
      'src/Controllers/AuthController.cs',
      'src/Controllers/UserController.cs',
      'src/Controllers/AdminController.cs'
    ]
  }
})
```

**Output (Cycle 1)**:
```
✅ review-consolidator completed (Cycle 1)

📊 Results:
   - Total Issues: 27 (12 P0, 10 P1, 5 P2)
   - Cycle ID: consolidator-executor-1706182345000
   - Iteration: 1

🔄 Recommended Next Actions:

1. 🚨 CRITICAL: plan-task-executor
   Reason: Fix 12 P0 issues before proceeding
   Command: Use Task tool with subagent_type: "plan-task-executor"
   Parameters:
     cycleId: "consolidator-executor-1706182345000"
     iteration: 2
     p0Count: 12
```

### Fix Attempt Between Cycles
User invokes plan-task-executor, which fixes 5/12 P0 issues (42% improvement).

### Cycle 2: Re-review After Fixes

**Invocation**:
```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Re-review DI refactoring after P0 fixes (Cycle 2)",
  prompt: `Re-review after fixing P0 issues from Cycle 1.

Cycle ID: consolidator-executor-1706182345000
Iteration: 2
Previous P0 count: 12

Files: [same 8 files]
`,
  context: {
    cycleId: "consolidator-executor-1706182345000",
    iteration: 2,
    previousIssueCount: 27,
    previousP0Count: 12,
    files: [/* same 8 files */]
  }
})
```

**Output (Cycle 2 - ESCALATION)**:
```
⚠️ review-consolidator: Cycle protection triggered

📊 Cycle 2 Results:
   - Total Issues: 18 (7 P0, 8 P1, 3 P2)
   - Issues fixed: 9/27 (33% improvement)
   - P0 issues fixed: 5/12 (42% improvement)
   - Persistent P0: 7 issues

🚨 ESCALATION TRIGGERED

Trigger: Max cycles reached (2/2) with persistent P0 issues

Cycle History:
- Cycle 1: 27 issues (12 P0, 10 P1, 5 P2)
- Cycle 2: 18 issues (7 P0, 8 P1, 3 P2)
- Net improvement: 33% (below 50% threshold)
- P0 improvement: 42% (5/12 fixed, 7 persist)

📝 Escalation Report Generated:
   ✅ Docs/reviews/escalation-report-20250125152030.md

🔄 Recommended Next Actions:

1. 🚨 CRITICAL: USER INTERVENTION REQUIRED
   Reason: Automated fix cycle failed to resolve P0 issues
   Action: Review escalation report for root cause analysis
   Report: Docs/reviews/escalation-report-20250125152030.md

   Next Steps:
   - Manual review of 7 persistent P0 issues
   - Architecture consultation (complex DI issues)
   - Consider incremental refactoring approach
```

### Escalation Report Generated
**Location**: `Docs/reviews/escalation-report-20250125152030.md`

**Excerpt**:
```markdown
# Escalation Report - DI Refactoring Review

**Generated**: 2025-01-25 15:20:30
**Cycle ID**: consolidator-executor-1706182345000

## Escalation Trigger
**Primary**: Max cycles reached (2/2) with 7 persistent P0 issues
**Secondary**: Low improvement rate (33% overall, 42% P0)

## Cycle History

### Cycle 1 (2025-01-25 14:15:00)
- **Total Issues**: 27 (12 P0, 10 P1, 5 P2)
- **Files Reviewed**: 8
- **Review Duration**: 2m 45s

### Cycle 2 (2025-01-25 15:18:00)
- **Total Issues**: 18 (7 P0, 8 P1, 3 P2)
- **Fixed Issues**: 9/27 (33%)
- **Fixed P0**: 5/12 (42%)
- **New Issues**: 0
- **Persistent P0**: 7
- **Review Duration**: 2m 38s

## Progress Analysis
- **Overall Improvement**: 33% (9/27 issues fixed)
- **P0 Improvement**: 42% (5/12 fixed) ✅ Above threshold
- **P1/P2 Improvement**: 27% (4/15 fixed) ⚠️ Below threshold
- **Net Assessment**: LOW IMPROVEMENT (threshold: 50%)

## Root Cause Analysis

### Root Cause 1: Complex DI Container Misconfiguration
**Severity**: HIGH
**Affected Issues**: P0-1, P0-2, P0-5 (3/7 persistent issues)

**Problem**: Circular dependency in DI container between AuthService ↔ UserService. Not detectable at compile-time, only at runtime initialization.

**Evidence**:
- AuthService depends on IUserService
- UserService depends on IAuthService
- DI container throws InvalidOperationException at startup

**Why Automated Fix Failed**:
- Requires architectural redesign (break circular dependency)
- Simple null checks or injection fixes don't resolve root cause
- Needs domain knowledge of authentication/user management interaction

### Root Cause 2: Missing Domain Knowledge (OAuth2 Flow)
**Severity**: MEDIUM
**Affected Issues**: P0-3, P0-6 (2/7 persistent issues)

**Problem**: Incorrect token refresh flow implementation violates OAuth2 spec.

**Evidence**:
- Refresh token reused after exchange (should be one-time use)
- No token expiration validation before refresh attempt

**Why Automated Fix Failed**:
- Requires understanding of OAuth2 RFC 6749
- Automated reviewer can detect "pattern violation" but not "protocol violation"
- Needs security domain expertise

### Root Cause 3: Test Infrastructure Limitations
**Severity**: LOW
**Affected Issues**: P0-4, P0-7 (2/7 persistent issues)

**Problem**: Async DI tests fail due to synchronization context issues in xUnit.

**Evidence**:
- `await` in test setup causes deadlock
- xUnit's default synchronization context incompatible with async DI initialization

**Why Automated Fix Failed**:
- Requires test infrastructure upgrade (xUnit → NUnit or custom sync context)
- Not a code quality issue - infrastructure limitation

## Persistent P0 Issues (Detailed)

### P0-1: Circular DI dependency - AuthService ↔ UserService
**File**: src/Services/AuthService.cs:15, src/Services/UserService.cs:18
**Root Cause**: Architecture (circular dependency)
**Automated Fix Attempts**: 2 (both failed)
**Manual Action Required**: Break circular dependency via interface extraction or mediator pattern

### P0-2: DI container registration missing for ITokenService
**File**: Program.cs:45
**Root Cause**: Configuration (container setup)
**Automated Fix Attempts**: 1 (failed - wrong service lifetime)
**Manual Action Required**: Register as Singleton (not Scoped) to share token cache

... (5 more P0 issues)

## Manual Intervention Recommendations

### Recommendation 1: Architecture Review Session
**Priority**: CRITICAL
**Estimated Time**: 2-3 hours
**Participants**: Senior Developer + Architect

**Agenda**:
1. Resolve circular dependency (AuthService ↔ UserService)
   - Option A: Extract IAuthenticationValidator interface
   - Option B: Introduce Mediator pattern (MediatR)
   - Option C: Combine services into AuthenticationService

2. Fix DI container configuration
   - Review service lifetimes (Singleton vs Scoped)
   - Add validation at startup (`ValidateOnBuild()`)

### Recommendation 2: OAuth2 Knowledge Transfer
**Priority**: HIGH
**Estimated Time**: 1-2 hours
**Resources**:
- RFC 6749 (OAuth 2.0 Authorization Framework)
- Microsoft Identity Platform documentation
- Security team consultation

**Focus Areas**:
- Token refresh flow (one-time use tokens)
- Token expiration validation
- Refresh token rotation

### Recommendation 3: Test Infrastructure Upgrade
**Priority**: MEDIUM
**Estimated Time**: 3-4 hours

**Options**:
- Option A: Migrate to NUnit (better async support)
- Option B: Implement custom xUnit synchronization context
- Option C: Use `ConfigureAwait(false)` in service initialization

## Alternative Approaches

### Approach 1: Incremental Refactoring
**Strategy**: Split P0 issues into smaller, independent tasks

**Benefits**:
- Reduce cognitive load (1 issue at a time)
- Easier to test and validate
- Lower risk of regressions

**Drawbacks**:
- Longer total time (4-6 hours vs 2-3 hours)
- More cycles (4-5 iterations)

### Approach 2: Pair Programming Session
**Strategy**: Senior + Junior developer pair to fix issues together

**Benefits**:
- Knowledge transfer (junior learns DI patterns)
- Real-time validation (senior reviews as junior codes)
- Higher fix success rate (80-90% vs 40-50% solo)

**Drawbacks**:
- Requires availability of both developers
- Higher cost (2 developers vs 1)

### Approach 3: External Consultation
**Strategy**: Bring in DI/architecture expert for consultation

**Benefits**:
- Expert guidance (avoid common pitfalls)
- Best practices implementation
- Long-term architecture improvement

**Drawbacks**:
- Higher cost ($200-500 for 2-hour session)
- Scheduling delay (1-2 days)

## Next Steps

1. **IMMEDIATE** (Today):
   - Schedule architecture review session
   - Assign P0-1 and P0-2 to senior developer
   - Document circular dependency in architecture decision record

2. **THIS WEEK**:
   - Complete OAuth2 knowledge transfer
   - Fix P0-3 and P0-6 (token refresh flow)
   - Evaluate test infrastructure options (NUnit vs custom sync context)

3. **NEXT SPRINT**:
   - Implement test infrastructure upgrade
   - Fix P0-4 and P0-7 (async test issues)
   - Re-review entire DI refactoring (aim for 0 P0 issues)

---

**Escalation Status**: OPEN
**Assigned To**: [Senior Developer Name]
**Follow-up Date**: 2025-01-27 (2 days)
```

---

## Example 4: Timeout Handling (Partial Results)

### Scenario
One reviewer (code-principles-reviewer) times out after 5 minutes due to large file complexity. review-consolidator continues with partial results from 2/3 reviewers.

### Invocation
```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Review complex service refactoring",
  prompt: `Review complex refactoring of PaymentService.

Files:
- src/Services/PaymentService.cs (1,850 lines - very complex)
- src/Services/PaymentGatewayService.cs (920 lines)
- src/Tests/PaymentServiceTests.cs (1,200 lines)

Review types: code-style-reviewer, code-principles-reviewer, test-healer
`,
  context: {
    reviewTypes: [
      'code-style-reviewer',
      'code-principles-reviewer',
      'test-healer'
    ],
    files: [
      'src/Services/PaymentService.cs',
      'src/Services/PaymentGatewayService.cs',
      'src/Tests/PaymentServiceTests.cs'
    ]
  }
})
```

### Console Output
```
🔄 review-consolidator: Starting parallel review with 3 reviewers

📋 Review Configuration:
   - Files: 3 (3,970 lines)
   - Reviewers: 3 (code-style-reviewer, code-principles-reviewer, test-healer)
   - Timeout: 5 minutes per reviewer

⏱️ [00:00] Launching reviewers in parallel...
   ✅ code-style-reviewer (started)
   ✅ code-principles-reviewer (started)
   ✅ test-healer (started)

⏱️ [03:45] code-style-reviewer completed (28 issues found)
⏱️ [04:30] test-healer completed (22 issues found)

⏱️ [05:00] ⚠️ TIMEOUT WARNING: code-principles-reviewer (5m 00s)
   - Reviewer exceeded timeout limit
   - Terminating reviewer process
   - Continuing with partial results (2/3 reviewers)

📊 Consolidation Phase (Partial Results):
   - Completed reviewers: 2/3 (code-style-reviewer, test-healer)
   - Timed out: code-principles-reviewer
   - Raw issues: 50 (28 + 22)
   - Deduplication: 8s

✅ Consolidation Complete (Partial):
   - Consolidated issues: 35 (30% reduction)
   - Priority breakdown: 5 P0, 18 P1, 12 P2
   - ⚠️ Incomplete: SOLID principles review missing

⚠️ PARTIAL RESULTS WARNING:
   - code-principles-reviewer timed out
   - SOLID principle violations NOT detected
   - Recommend: Manual review of PaymentService.cs complexity

📝 Reports Generated:
   ✅ Master Report: Docs/reviews/review-report-20250125165530.md (PARTIAL)
   ✅ Appendices:
      - Docs/reviews/appendices/code-style-reviewer-20250125165530.md
      - Docs/reviews/appendices/test-healer-20250125165530.md
      - ⚠️ code-principles-reviewer appendix NOT generated (timeout)
   ✅ Traceability Matrix: Docs/reviews/review-traceability-20250125165530.md (PARTIAL)

⏱️ Total Duration: 5m 12s
   - Partial execution: 2/3 reviewers completed

🔄 Recommended Next Actions:

1. ⚠️ RECOMMENDED: manual-review
   Reason: code-principles-reviewer timed out - SOLID violations not detected
   Action: Manual review of PaymentService.cs (1,850 lines, high complexity)
   Focus: Dependency injection, Single Responsibility Principle

2. 💡 OPTIONAL: review-consolidator (retry)
   Reason: Retry with increased timeout for complex files
   Action: Increase timeout to 10 minutes, retry code-principles-reviewer only
```

### Master Report (Partial Results)
**Location**: `Docs/reviews/review-report-20250125165530.md`

**Header**:
```markdown
# Code Review Report - 2025-01-25 16:55:30

⚠️ **PARTIAL RESULTS WARNING**
This report is based on partial review results. code-principles-reviewer timed out after 5 minutes and did not complete.

**Impact**:
- SOLID principle violations NOT detected
- Dependency injection issues may be missing
- Manual review recommended for: src/Services/PaymentService.cs (1,850 lines)

## Executive Summary
- **Total Issues**: 35 (5 P0, 18 P1, 12 P2)
- **Files Reviewed**: 3 (3,970 lines)
- **Reviewers**: 2/3 completed (code-style-reviewer ✅, test-healer ✅, code-principles-reviewer ❌ TIMEOUT)
- **Review Duration**: 5m 12s
- **Deduplication**: 50 raw issues → 35 consolidated (30% reduction)
- **Completeness**: PARTIAL (code-principles-reviewer results missing)

⚠️ **Recommendation**: Manual review of PaymentService.cs for SOLID principle violations
```

**Additional Notes Section**:
```markdown
## Reviewer Timeout Details

### code-principles-reviewer (TIMEOUT)
- **Timeout**: 5m 00s (limit reached)
- **Status**: INCOMPLETE
- **Reason**: PaymentService.cs complexity (1,850 lines, cyclomatic complexity >50)

**Missing Analysis**:
- SOLID principle adherence (Single Responsibility, Dependency Inversion)
- Dependency injection pattern validation
- Complex method refactoring recommendations

**Manual Review Recommended**:
- File: src/Services/PaymentService.cs
- Focus: Break down into smaller services (SRP violation likely)
- Estimated Time: 30-45 minutes manual review

**Alternative Approach**:
- Retry review with increased timeout (10 minutes)
- Split PaymentService.cs into smaller files before review
- Use code-complexity-analyzer agent (future) to identify hot spots
```

---

## Example 5: Zero Issues (Clean Code)

### Scenario
Review well-written code with excellent quality. No issues found.

### Invocation
```typescript
Task({
  subagent_type: "review-consolidator",
  description: "Review clean utility module",
  prompt: `Review StringHelpers utility module.

Files:
- src/Utils/StringHelpers.cs (200 lines, well-tested)
- src/Tests/StringHelpersTests.cs (350 lines, 100% coverage)

Review types: code-style-reviewer, code-principles-reviewer, test-healer
`,
  context: {
    reviewTypes: ['code-style-reviewer', 'code-principles-reviewer', 'test-healer'],
    files: ['src/Utils/StringHelpers.cs', 'src/Tests/StringHelpersTests.cs']
  }
})
```

### Console Output
```
🔄 review-consolidator: Starting parallel review with 3 reviewers

⏱️ [00:42] All reviewers completed
   - code-style-reviewer: 0 issues
   - code-principles-reviewer: 0 issues
   - test-healer: 0 issues

✅ Consolidation Complete: NO ISSUES FOUND

📊 Results:
   - Total Issues: 0
   - Code Quality: EXCELLENT ✅
   - All reviewers: PASS

📝 Report: Docs/reviews/review-report-20250125170530.md (NO ISSUES)

🔄 Recommended Next Actions:

1. 🚨 CRITICAL: pre-completion-validator
   Reason: No P0 issues - proceed to completion validation
   Command: Use Task tool with subagent_type: "pre-completion-validator"

2. 💡 OPTIONAL: git-workflow-manager
   Reason: Code quality excellent - ready to commit
   Command: Use Task tool with subagent_type: "git-workflow-manager"
```

---

## Summary

These examples demonstrate:

1. **Example 1**: Standard workflow with 3 reviewers, deduplication, actionable reports
2. **Example 2**: Scalability to 100+ files with performance metrics
3. **Example 3**: Cycle protection with automatic escalation and root cause analysis
4. **Example 4**: Graceful degradation on timeout with partial results
5. **Example 5**: Clean code scenario with zero issues

For more details, see:
- **[README.md](./README.md)** - Full agent documentation
- **[SPEC.md](./SPEC.md)** - Technical specification
- **[review-consolidator-architecture.md](../../../Docs/Architecture/Planned/review-consolidator-architecture.md)** - Architecture design
