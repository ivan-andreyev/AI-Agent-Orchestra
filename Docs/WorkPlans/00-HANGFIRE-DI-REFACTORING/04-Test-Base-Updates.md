# Phase 4: Test Base Class Updates

**Parent Plan**: [00-HANGFIRE-DI-REFACTORING.md](../00-HANGFIRE-DI-REFACTORING.md)

## Objective

Update test base classes to use dependency injection for accessing Hangfire storage instead of JobStorage.Current.

## Tasks

### 4.1A: Update IntegrationTestBase - WaitForHangfireJobCompletion Method
- Modify `src/Orchestra.Tests/Integration/IntegrationTestBase.cs`
- Replace JobStorage.Current usage at line 178

**Current code at line 178**:
```csharp
var monitoringApi = JobStorage.Current.GetMonitoringApi();
```

**Replace with**:
```csharp
// Use DI to get storage service from the test factory
using (var scope = _factory.Services.CreateScope())
{
    var storageService = scope.ServiceProvider.GetRequiredService<IHangfireStorageService>();
    var monitoringApi = storageService.GetMonitoringApi();

    // Rest of the monitoring logic remains the same...
}
```

### 4.1B: Add Required Using Statement
```csharp
using Orchestra.Core.Services;
```

### 4.1C: Update Error Handling
- Add null checking for storage service
- Provide helpful error messages if DI not configured

```csharp
var storageService = scope.ServiceProvider.GetService<IHangfireStorageService>();
if (storageService == null)
{
    throw new InvalidOperationException(
        "IHangfireStorageService not registered. Ensure test factory configures Hangfire storage correctly.");
}
```

### 4.2A: Update RealEndToEndTests - Diagnostic Method
- Modify `src/Orchestra.Tests/RealEndToEndTests.cs`
- Replace JobStorage.Current usage at line 358

**Current code at line 358**:
```csharp
var storage = JobStorage.Current;
_output.WriteLine($"[DIAG] Hangfire Storage: {storage?.GetType().Name ?? "NULL"}");
```

**Replace with**:
```csharp
// Get storage through DI for diagnostics
using (var scope = _factory.Services.CreateScope())
{
    var storageService = scope.ServiceProvider.GetService<IHangfireStorageService>();
    var storage = storageService?.Storage;
    _output.WriteLine($"[DIAG] Hangfire Storage (DI): {storage?.GetType().Name ?? "NULL"}");
    _output.WriteLine($"[DIAG] JobStorage.Current (should be null): {JobStorage.Current?.GetType().Name ?? "NULL"}");
}
```

### 4.2B: Add Isolation Verification
- Add diagnostic to confirm JobStorage.Current is null
- Verify each test gets correct isolated storage

```csharp
// Add diagnostic method to verify isolation
private void VerifyStorageIsolation()
{
    Assert.Null(JobStorage.Current); // Should be null in tests

    using (var scope = _factory.Services.CreateScope())
    {
        var storageService = scope.ServiceProvider.GetRequiredService<IHangfireStorageService>();
        Assert.NotNull(storageService);
        Assert.NotNull(storageService.Storage);

        _output.WriteLine($"Storage isolation verified - using {storageService.Storage.GetType().Name}");
    }
}
```

### 4.3A: Create Test Helper Extensions
- Create `src/Orchestra.Tests/Extensions/HangfireTestExtensions.cs`
- Add helper methods for common operations

```csharp
namespace Orchestra.Tests.Extensions;

public static class HangfireTestExtensions
{
    /// <summary>
    /// Gets the monitoring API from the test factory's DI container.
    /// </summary>
    public static IMonitoringApi GetMonitoringApi(this WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<IHangfireStorageService>();
        return storageService.GetMonitoringApi();
    }

    /// <summary>
    /// Waits for all Hangfire jobs to complete.
    /// </summary>
    public static async Task WaitForJobsAsync(
        this WebApplicationFactory<Program> factory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var monitoringApi = factory.GetMonitoringApi();

        // TODO: Implement wait logic using monitoringApi
        await Task.Delay(100, cancellationToken);
    }
}
```

### 4.3B: Update All Test Classes
- Search for any remaining JobStorage.Current usages
- Replace with DI approach or extension methods

## Acceptance Criteria

- [ ] IntegrationTestBase uses DI for storage access
- [ ] RealEndToEndTests uses DI for diagnostics
- [ ] JobStorage.Current verified as null in tests
- [ ] Helper extensions created for common operations
- [ ] All tests compile without errors
- [ ] No remaining JobStorage.Current references in test code

## Dependencies

- Phase 3: Test Factory Refactoring completed
- IHangfireStorageService available in DI

## Validation Steps

1. Run IntegrationTestBase tests individually
2. Verify WaitForHangfireJobCompletion works
3. Check RealEndToEndTests diagnostics output
4. Confirm JobStorage.Current is null

## Estimated Time: 45 minutes

## Risk Points

- Scope lifetime management in async methods
- Null reference exceptions if DI not configured
- Timing issues in job completion detection

## Notes

- Use service scope for each storage access
- Consider caching monitoring API for performance
- Add logging for debugging isolation issues