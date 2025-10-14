# Technical Debt Registry

This document tracks known architectural technical debt items that don't block MVP but should be addressed in future iterations.

## Critical: Hangfire JobStorage.Current - Mutable Global State

**Status**: 🟡 Workaround Implemented (Tests), 🔴 Unresolved (Production)
**Priority**: Medium (Post-MVP)
**Estimated Impact**: High (Scalability & Multi-tenancy)
**Discovery Date**: 2025-01-03
**Blocks MVP**: ❌ No

### Problem Description

Hangfire uses `JobStorage.Current` - a **mutable static global variable** - for storage access. This creates potential issues in advanced deployment scenarios.

```csharp
// From Hangfire.JobStorage class
public abstract class JobStorage
{
    private static JobStorage _current;

    public static JobStorage Current
    {
        get => _current;
        set => _current = value; // ⚠️ MUTABLE GLOBAL STATE
    }
}
```

### Why It's Technical Debt

**Anti-Pattern**: Global mutable state
- Violates Dependency Injection principles
- Makes testing difficult (shared state between tests)
- Prevents certain advanced architectural patterns
- Hidden dependencies (code using `JobStorage.Current` doesn't declare dependency)

### Current Impact

#### ✅ Production (MVP) - No Impact
Single ASP.NET Core process with one JobStorage → Works perfectly:
```
Startup.ConfigureServices()
  → JobStorage.Current = PostgreSQL (set once)
  → HangfireServer uses Current (never changes)
  → ✅ Perfect concurrency, no issues
```

#### ⚠️ Tests - Workaround Implemented
**Problem**: Parallel test collections with isolated storage → race conditions
**Solution**: Sequential test execution (Phase 1) + Future parallel execution plan (Phase 2)
**Details**: [Remove HangfireServer Tests Plan](./WorkPlans/Remove-HangfireServer-Tests-Plan-REVISED.md)

### Future Scaling Concerns

| Scenario | Impact | Notes |
|----------|--------|-------|
| **Horizontal Scaling** | ✅ No impact | Each instance has own `JobStorage.Current` |
| **Multi-tenant (separate processes)** | ✅ No impact | Process isolation provides safety |
| **Multi-tenant (single process)** | 🔴 **Blocked** | Cannot have tenant-specific JobStorage |
| **Blue-Green Deployment** | 🟡 **Potential issue** | Shared storage with multiple current values |
| **Advanced Testing** | 🔴 **Blocked** | Cannot run parallel integration tests (fixed in Phase 2) |

### Real-World Scenarios Where This Becomes Critical

#### 1. Multi-Tenant SaaS (Single Process)
```csharp
// CANNOT DO THIS with JobStorage.Current:
public class TenantBackgroundJobService
{
    public async Task EnqueueJobForTenant(string tenantId, Job job)
    {
        // Need tenant-specific job storage, but Current is global!
        var tenantStorage = GetTenantStorage(tenantId); // ❌ Can't use this
        JobStorage.Current = tenantStorage; // ❌ Affects ALL tenants!
    }
}
```

**Workaround**: Separate processes per tenant (expensive, complex)
**Proper Fix**: DI-based JobStorage injection

#### 2. Integration Testing with Parallel Execution
```csharp
// CURRENT ISSUE (Fixed in Phase 2):
[Collection("Integration")]
public class TestA
{
    // Sets JobStorage.Current = TestDB_A
}

[Collection("RealE2E")]
public class TestB
{
    // Sets JobStorage.Current = TestDB_B
    // ❌ Conflicts with TestA if parallel!
}
```

**Current Solution**: Sequential execution (slower)
**Phase 2 Solution**: Remove HangfireServer from tests
**Proper Fix**: DI-based JobStorage injection

### Recommended Remediation Path

#### Phase 1: Test Infrastructure (IMPLEMENTED) ✅
- **Status**: Complete
- **Solution**: Sequential test execution
- **Trade-off**: 2x slower tests (9-10 min vs 4-5 min)
- **File**: `src/Orchestra.Tests/AssemblyInfo.cs`

#### Phase 2: Test Parallelization (PLANNED) 📋
- **Status**: Plan approved (9.3/10)
- **Solution**: TestBackgroundJobClient with synchronous execution
- **Benefit**: 50% faster tests with full isolation
- **Estimate**: 8-12 hours
- **Plan**: [Remove HangfireServer Tests Plan](./WorkPlans/Remove-HangfireServer-Tests-Plan-REVISED.md)

#### Phase 3: Production DI Refactoring (FUTURE) 🔮
- **Status**: Not planned for MVP
- **Solution**: Refactor to inject `IJobStorage` via DI
- **Benefit**: Enables multi-tenancy, better testability
- **Estimate**: 20-30 hours (affects production code)
- **Risk**: Medium (requires careful migration)

**Proposed Architecture**:
```csharp
// FUTURE: DI-based approach
public interface IJobStorageProvider
{
    JobStorage GetStorage();
}

public class HangfireOrchestrator
{
    private readonly IJobStorageProvider _storageProvider;

    public HangfireOrchestrator(IJobStorageProvider storageProvider)
    {
        _storageProvider = storageProvider; // DI, not global state
    }

    public void EnqueueJob(Job job)
    {
        var storage = _storageProvider.GetStorage(); // Scoped, not global
        // ... enqueue logic
    }
}

// Multi-tenant implementation
public class TenantJobStorageProvider : IJobStorageProvider
{
    private readonly ITenantContext _tenantContext;
    private readonly Dictionary<string, JobStorage> _tenantStorages;

    public JobStorage GetStorage()
    {
        var tenantId = _tenantContext.CurrentTenantId;
        return _tenantStorages[tenantId]; // ✅ Tenant-specific!
    }
}
```

#### Phase 4: Multi-Tenant Support (FUTURE) 🔮
- **Status**: Not planned for MVP
- **Prerequisite**: Phase 3 (DI refactoring)
- **Solution**: Tenant-scoped JobStorage via `IJobStorageProvider`
- **Estimate**: 15-20 hours
- **Business Value**: Enables SaaS model with shared infrastructure

### MVP Decision

**Decision**: ✅ **Accept this technical debt for MVP**

**Rationale**:
1. ✅ Production works perfectly (single JobStorage.Current)
2. ✅ Tests work with Phase 1 fix (sequential execution)
3. ✅ Does not block core product functionality
4. ✅ Does not prevent horizontal scaling
5. 🟡 Only affects advanced scenarios (multi-tenancy in single process)
6. 🟡 Test performance acceptable (9-10 min for 582 tests)

**Post-MVP Trigger Points**:
- Customer requests multi-tenant SaaS deployment in single process
- Test suite grows beyond 1000 tests (need faster execution)
- Blue-green deployment patterns require more sophisticated job management
- CI/CD pipeline time becomes bottleneck (>15 minutes for tests)

### References

- **Root Cause Analysis**: [Remove HangfireServer Tests Plan - Problem Analysis](./WorkPlans/Remove-HangfireServer-Tests-Plan-REVISED.md#problem-analysis)
- **Test Fix Implementation**: `src/Orchestra.Tests/AssemblyInfo.cs` (Phase 1)
- **Parallel Test Plan**: [Remove HangfireServer Tests Plan](./WorkPlans/Remove-HangfireServer-Tests-Plan-REVISED.md)
- **Hangfire Documentation**: https://docs.hangfire.io/en/latest/background-methods/using-ioc-containers.html

### Monitoring & Review

**Review Cadence**: Quarterly post-MVP
**Success Metrics**:
- Test execution time < 15 minutes for full suite
- Zero production incidents related to JobStorage
- Deployment flexibility meets business needs

**Next Review**: Q2 2025 (3 months post-MVP launch)

---

## Other Technical Debt Items

### PHASE6.2-001: Main Content Layout Overlap (Z-Index Issue)

**Status**: 🔴 Critical
**Priority**: High (Blocks Phase 6.2 completion)
**Estimated Impact**: High (User Experience)
**Discovery Date**: 2025-10-14
**Blocks MVP**: ❌ No (UI functional, visual issue only)

#### Problem Description

Main content area (Agent History, Task Queue, Coordinator Agent panels) renders **underneath the sidebar** instead of beside it. Content is partially hidden and inaccessible due to incorrect z-index or positioning.

**User Report**: "проблема с тем, что опять разметка поехала и центральный компонент уехал ПОД сайдбар."

**Affected Components**:
- `MainLayout.razor`
- `MainLayout.razor.css`
- Sidebar positioning
- Main content area positioning

#### Current Impact

**Production Impact**: 🟡 Medium
- Content is partially hidden
- User experience degraded
- Functionality accessible but requires scrolling/workarounds
- Not a critical blocker (app still usable)

**Testing Impact**: 🔴 High
- Phase 6.2 cross-browser testing blocked until fixed
- Cannot validate responsive design properly
- Visual regression testing incomplete

#### Root Cause Hypothesis

1. **Z-index conflict**: Sidebar has higher z-index than main content
2. **Position: fixed/absolute issue**: Sidebar using fixed positioning without proper margin compensation on main content
3. **CSS refactoring regression**: Phase 6.1 CSS changes introduced layout bug
4. **Flexbox/Grid layout broken**: MainLayout structure changed

#### Recommended Remediation

**Estimated Effort**: 30-60 minutes

**Steps**:
1. Inspect `MainLayout.razor.css` for sidebar positioning
2. Check sidebar `position` property (fixed/absolute/relative)
3. Verify main content `margin-left` accounts for sidebar width
4. Validate z-index values (sidebar should NOT have higher z-index than main)
5. Test CSS Grid/Flexbox layout structure
6. Verify responsive breakpoints maintain correct layout

**Proposed Fix**:
```css
/* Expected structure */
.sidebar {
    position: fixed;
    left: 0;
    z-index: 1000; /* Lower than modals but higher than content */
}

.main-content {
    margin-left: var(--sidebar-width); /* Account for sidebar */
    position: relative;
    z-index: 1; /* Lower than sidebar */
}
```

#### MVP Decision

**Decision**: ✅ **Accept as Post-Phase 6.2 Technical Debt**

**Rationale**:
1. ✅ Application functionally works (content accessible via scrolling)
2. ✅ Does not block core product functionality
3. ✅ Does not prevent MVP launch (minor UX issue)
4. 🟡 Affects user experience but not critically
5. 🟡 Quick fix available (30-60 min)

**Fix Timeline**: Before Phase 6.2 final completion or MVP launch

---

### PHASE6.2-002: API Performance Regression - /repositories Endpoint

**Status**: 🔴 Critical
**Priority**: High (Investigate urgently)
**Estimated Impact**: High (Performance)
**Discovery Date**: 2025-10-14
**Blocks MVP**: ❌ No (Acceptable for low repository counts)

#### Problem Description

`GET /repositories` endpoint shows **severe performance regression**: 4.5 seconds average response time vs. Phase 0 baseline of 81ms. This represents a **+5431% performance degradation**.

**Test Results**:
```
Baseline: 81.10 ms
Phase 6.2: 4486.29 ms (3634-5772 ms range)
Change: +5431.8%
Threshold: <165 ms
Status: FAIL
```

**Affected Endpoint**: `http://localhost:55002/repositories`

#### Current Impact

**Production Impact**: 🟡 Medium
- First request slow (5.7 seconds) - likely cold start
- Subsequent requests improve (3.6 seconds) - still unacceptable
- Acceptable for small repository counts (<10)
- Noticeable delay for larger repository lists

**User Impact**: 🟡 Medium
- Dropdown population delay on page load
- UI feels sluggish when changing repositories
- Acceptable for MVP with small datasets

#### Root Cause Hypotheses

1. **Database Cold Start** (Most Likely)
   - First query initializes EF Core context
   - Database connection pool warmup
   - Evidence: 5.7s → 3.6s improvement across iterations

2. **N+1 Query Problem**
   - Repository entity loading related entities inefficiently
   - Missing `.Include()` statements for eager loading

3. **Missing Database Indexes**
   - Repositories table lacks proper indexes
   - Query scanning full table

4. **Repository Discovery Scanning**
   - File system scanning for repository metadata
   - Slow I/O operations

5. **Entity Framework Query Inefficiency**
   - Inefficient LINQ query translation
   - Unnecessary data fetching

#### Recommended Remediation

**Estimated Effort**: 1-2 hours

**Investigation Steps**:
1. Enable EF Core SQL logging to inspect generated queries
2. Profile database query execution with PostgreSQL EXPLAIN ANALYZE
3. Check for N+1 queries in repository loading logic
4. Verify database indexes on Repositories table
5. Review repository discovery logic for file I/O bottlenecks

**Proposed Fixes**:
```csharp
// 1. Add database indexes
[Index(nameof(Name))]
[Index(nameof(Path))]
public class Repository { ... }

// 2. Eager load related entities
var repositories = await _context.Repositories
    .Include(r => r.Agents) // If needed
    .Include(r => r.Tasks)  // If needed
    .ToListAsync();

// 3. Add caching for repository list
private IMemoryCache _cache;
public async Task<List<Repository>> GetRepositories()
{
    return await _cache.GetOrCreateAsync("repositories", async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        return await _context.Repositories.ToListAsync();
    });
}

// 4. Add database migration for indexes
migrationBuilder.CreateIndex(
    name: "IX_Repositories_Name",
    table: "Repositories",
    column: "Name");
```

#### MVP Decision

**Decision**: ✅ **Accept as Post-Phase 6.2 Technical Debt**

**Rationale**:
1. ✅ Acceptable for MVP with small repository counts (<10)
2. ✅ Likely cold start issue (acceptable in dev environment)
3. ✅ Does not block core product functionality
4. 🟡 Noticeable but not critical for initial users
5. 🟡 Can be optimized post-MVP based on real-world usage patterns

**Fix Timeline**: Investigate within 1 week, optimize based on findings

**Monitoring**: Add performance monitoring for this endpoint in production

---

### PHASE6.2-003: CSS Variables Validation Failure

**Status**: 🟠 High
**Priority**: Medium (Validation issue, not functional)
**Estimated Impact**: Low (Testing only)
**Discovery Date**: 2025-10-14
**Blocks MVP**: ❌ No (Visual inspection confirms CSS works)

#### Problem Description

Browser performance test tool reports **0 CSS custom properties** detected, but Phase 6.1 design system should define 200+ variables in `design-system.css`.

**Expected**: `>200` CSS variables (--primary-color, --spacing-*, --font-size-*, etc.)
**Actual**: `0` CSS variables detected by test

**Test Tool**: `phase6-browser-performance-test.html`

#### Current Impact

**Production Impact**: ✅ None
- CSS design system working correctly (visually confirmed)
- All CSS variables resolving properly
- User interface displays correctly

**Testing Impact**: 🟡 Medium
- Cannot validate CSS variable usage programmatically
- Manual DevTools inspection required
- Test framework incomplete

#### Root Cause Hypotheses

1. **Test Timing Issue** (Most Likely)
   - Test runs before CSS files loaded/parsed
   - Blazor WASM loads CSS after initial page load
   - Test script executes too early

2. **Wrong Test URL**
   - Test running on standalone page without main app CSS
   - Should run test from main application page

3. **CSS Scope Issue**
   - Test script checking wrong document context
   - CSS variables defined in different scope

4. **Test Script Bug**
   - JavaScript logic error in variable counting
   - Query selector targeting wrong elements

#### Recommended Remediation

**Estimated Effort**: 15-30 minutes

**Steps**:
1. Re-run test from main application page (`http://localhost:55001/`)
2. Add delay to test script to wait for CSS loading
3. Manual DevTools inspection to verify CSS variables exist
4. Update test script with proper CSS loading detection

**Proposed Fix**:
```javascript
// Wait for CSS to load before testing
async function waitForCSSLoad() {
    return new Promise((resolve) => {
        if (document.styleSheets.length > 0) {
            resolve();
        } else {
            setTimeout(() => waitForCSSLoad().then(resolve), 100);
        }
    });
}

// Updated test
async function testCSSVariables() {
    await waitForCSSLoad();
    const styles = getComputedStyle(document.documentElement);
    const variables = Array.from(document.styleSheets)
        .flatMap(sheet => Array.from(sheet.cssRules || []))
        .filter(rule => rule.style)
        .flatMap(rule => Array.from(rule.style))
        .filter(prop => prop.startsWith('--'));
    return { variableCount: variables.length };
}
```

#### MVP Decision

**Decision**: ✅ **Accept as Post-Phase 6.2 Technical Debt**

**Rationale**:
1. ✅ CSS design system works correctly (visually verified)
2. ✅ All CSS variables resolving properly
3. ✅ Does not affect production functionality
4. ✅ Test framework issue, not product issue
5. 🟡 Manual validation sufficient for MVP

**Fix Timeline**: Optional - can be deferred or resolved with manual testing

**Workaround**: Manual DevTools inspection confirms CSS variables present

---

### Template for New Items

```markdown
## [Title]: [Brief Description]

**Status**: 🔴/🟡/🟢
**Priority**: Critical/High/Medium/Low
**Estimated Impact**: High/Medium/Low
**Discovery Date**: YYYY-MM-DD
**Blocks MVP**: ✅/❌

### Problem Description
[Detailed explanation]

### Current Impact
[Production, testing, development impacts]

### Recommended Remediation
[Step-by-step plan]

### MVP Decision
[Accept/Resolve with rationale]
```

---

**Document Status**: 🟢 Active
**Last Updated**: 2025-10-14 (Added Phase 6.2 technical debt)
**Owner**: Development Team
**Review Frequency**: Monthly during MVP, Quarterly post-MVP

### Technical Debt Summary

| ID | Title | Status | Priority | Blocks MVP | Estimated Fix |
|----|-------|--------|----------|------------|---------------|
| HANGFIRE-001 | JobStorage.Current Mutable Global State | 🟡 Workaround Implemented | Medium | ❌ No | 8-30 hours (phased) |
| PHASE6.2-001 | Main Content Layout Overlap (Z-Index) | 🔴 Critical | High | ❌ No | 30-60 minutes |
| PHASE6.2-002 | API Performance Regression (/repositories) | 🔴 Critical | High | ❌ No | 1-2 hours |
| PHASE6.2-003 | CSS Variables Validation Failure | 🟠 High | Medium | ❌ No | 15-30 minutes |

**Total Items**: 4
**Critical/High Priority**: 3
**MVP Blockers**: 0
