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

_(To be added as discovered)_

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
**Last Updated**: 2025-01-03
**Owner**: Development Team
**Review Frequency**: Monthly during MVP, Quarterly post-MVP
