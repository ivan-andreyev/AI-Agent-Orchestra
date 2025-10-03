# Phase 5: Testing and Validation Strategy

**Parent Plan**: [00-HANGFIRE-DI-REFACTORING.md](../00-HANGFIRE-DI-REFACTORING.md)

## Objective

Comprehensive testing to validate that the DI refactoring successfully eliminates race conditions and ObjectDisposedException errors.

## Tasks

### 5.1A: Create Isolation Verification Test
- Create `src/Orchestra.Tests/Integration/HangfireIsolationTests.cs`
- Test that storage instances are properly isolated

```csharp
namespace Orchestra.Tests.Integration;

[Collection("Integration")]
public class HangfireIsolationTests : IntegrationTestBase
{
    public HangfireIsolationTests(TestWebApplicationFactory<Program> factory, ITestOutputHelper output)
        : base(factory, output) { }

    [Fact]
    public void JobStorage_Current_Should_Be_Null_In_Tests()
    {
        // Verify global singleton is not set
        Assert.Null(JobStorage.Current);
    }

    [Fact]
    public void Storage_Should_Be_Available_Through_DI()
    {
        using var scope = _factory.Services.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<IHangfireStorageService>();

        Assert.NotNull(storageService);
        Assert.NotNull(storageService.Storage);
        Assert.IsType<SQLiteStorage>(storageService.Storage);
    }

    [Fact]
    public void Multiple_Scopes_Should_Get_Same_Storage_Instance()
    {
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        var storage1 = scope1.ServiceProvider.GetRequiredService<IHangfireStorageService>().Storage;
        var storage2 = scope2.ServiceProvider.GetRequiredService<IHangfireStorageService>().Storage;

        // Should be same instance (singleton)
        Assert.Same(storage1, storage2);
    }
}
```

### 5.1B: Run Individual Collection Tests
- Test Integration collection in isolation
- Test RealE2E collection in isolation

```bash
# Test Integration collection alone
dotnet test --filter "FullyQualifiedName~Integration" --logger "console;verbosity=detailed"

# Test RealE2E collection alone
dotnet test --filter "FullyQualifiedName~RealEndToEnd" --logger "console;verbosity=detailed"
```

### 5.1C: Document Baseline Results
- Record test pass/fail counts
- Note any ObjectDisposedException occurrences
- Capture execution times

### 5.2A: Execute Parallel Test Run
- Run both collections in parallel
- Monitor for race conditions

```bash
# Run all tests in parallel (default behavior)
dotnet test --logger "console;verbosity=detailed" 2>&1 | tee test-results.log

# Check for disposal errors
grep -i "ObjectDisposedException" test-results.log
grep -i "Cannot access a disposed" test-results.log
```

### 5.2B: Create Stress Test Script
- Create `scripts/stress-test-hangfire.ps1`

```powershell
param(
    [int]$Iterations = 10,
    [string]$Filter = ""
)

Write-Host "Starting Hangfire DI Stress Test - $Iterations iterations" -ForegroundColor Cyan

$failureCount = 0
$disposalErrors = 0

for ($i = 1; $i -le $Iterations; $i++) {
    Write-Host "`nIteration $i/$Iterations" -ForegroundColor Yellow

    if ($Filter) {
        $output = dotnet test --no-build --filter $Filter 2>&1
    } else {
        $output = dotnet test --no-build 2>&1
    }

    $failed = $output | Select-String "Failed:\s+(\d+)" | ForEach-Object { $_.Matches[0].Groups[1].Value }
    $disposed = $output | Select-String "ObjectDisposedException|Cannot access a disposed"

    if ($failed -and [int]$failed -gt 0) {
        $failureCount++
        Write-Host "  Tests failed: $failed" -ForegroundColor Red
    }

    if ($disposed) {
        $disposalErrors++
        Write-Host "  Disposal errors detected!" -ForegroundColor Red
    }

    if (-not $failed -and -not $disposed) {
        Write-Host "  All tests passed!" -ForegroundColor Green
    }
}

Write-Host "`n=== Stress Test Results ===" -ForegroundColor Cyan
Write-Host "Total iterations: $Iterations"
Write-Host "Iterations with failures: $failureCount"
Write-Host "Iterations with disposal errors: $disposalErrors"
Write-Host "Success rate: $([math]::Round((($Iterations - $failureCount) / $Iterations) * 100, 2))%"

if ($failureCount -eq 0 -and $disposalErrors -eq 0) {
    Write-Host "`nSUCCESS: All iterations passed without errors!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`nFAILURE: Some iterations had errors" -ForegroundColor Red
    exit 1
}
```

### 5.2C: Run Stress Test
```bash
# Run stress test
PowerShell -ExecutionPolicy Bypass -File scripts/stress-test-hangfire.ps1 -Iterations 10
```

### 5.3A: Performance Validation
- Compare execution times before/after refactoring
- Monitor memory usage

```csharp
// Add performance test
[Fact]
public async Task Storage_Access_Performance_Should_Be_Acceptable()
{
    var sw = Stopwatch.StartNew();

    for (int i = 0; i < 100; i++)
    {
        using var scope = _factory.Services.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<IHangfireStorageService>();
        var api = storageService.GetMonitoringApi();
        var stats = api.GetStatistics();
    }

    sw.Stop();
    _output.WriteLine($"100 storage accesses took {sw.ElapsedMilliseconds}ms");

    Assert.True(sw.ElapsedMilliseconds < 1000, "Storage access too slow");
}
```

### 5.3B: Verify Production Behavior
- Ensure production still works with JobStorage.Current
- Test Hangfire Dashboard functionality
- Verify background job execution

### 5.3C: Create Validation Checklist
- Document all validation steps
- Create repeatable test procedure

## Acceptance Criteria

- [ ] All 582 tests passing
- [ ] Zero ObjectDisposedException errors
- [ ] Parallel execution successful
- [ ] Stress test passes 10 iterations
- [ ] Performance acceptable (< 10% degradation)
- [ ] Production behavior unchanged
- [ ] Dashboard still functional

## Success Metrics

| Metric | Target | Actual |
|--------|--------|--------|
| Test Pass Rate | 100% (582/582) | TBD |
| Disposal Errors | 0 | TBD |
| Parallel Success | 100% | TBD |
| Stress Test Pass | 10/10 | TBD |
| Performance Impact | < 10% | TBD |

## Dependencies

- All previous phases completed
- Test infrastructure refactored
- DI properly configured

## Estimated Time: 1 hour

## Risk Mitigation

- Keep original code in version control
- Test incrementally
- Monitor production closely after deployment

## Notes

- Run tests multiple times to catch intermittent issues
- Document any unexpected behavior
- Consider adding telemetry for production monitoring