# Phase 3: Test Factory Refactoring

**Parent Plan**: [00-HANGFIRE-DI-REFACTORING.md](../00-HANGFIRE-DI-REFACTORING.md)

## Objective

Refactor TestWebApplicationFactory and RealEndToEndTestFactory to use dependency injection for JobStorage instead of the global singleton.

## Tasks

### 3.1A: Update TestWebApplicationFactory - Service Registration
- Modify `src/Orchestra.Tests/TestWebApplicationFactory.cs`
- Add isolated JobStorage registration in ConfigureServices

**Add after line 96 in ConfigureServices**:
```csharp
// Register isolated JobStorage for this test instance
// CRITICAL: Do NOT set JobStorage.Current to avoid conflicts!
services.AddSingleton<JobStorage>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var hangfireConnection = configuration["HANGFIRE_CONNECTION"];

    // Create isolated storage instance for this test collection
    var storage = new SQLiteStorage(hangfireConnection, new SQLiteStorageOptions
    {
        QueuePollInterval = TimeSpan.FromMilliseconds(100),
        JobExpirationCheckInterval = TimeSpan.FromSeconds(30)
    });

    // IMPORTANT: Do NOT set JobStorage.Current = storage
    // This is the root cause of the race condition!

    return storage;
});

// Register the wrapper service
services.AddSingleton<IHangfireStorageService, HangfireStorageService>();
```

### 3.1B: Update TestWebApplicationFactory - CleanupHangfireData Method
- Replace JobStorage.Current usage at line 154
- Use DI to get IHangfireStorageService

**Replace line 154**:
```csharp
// OLD: var monitoringApi = JobStorage.Current.GetMonitoringApi();
// NEW:
var storageService = scope.ServiceProvider.GetRequiredService<IHangfireStorageService>();
var monitoringApi = storageService.GetMonitoringApi();
```

### 3.1C: Add Required Using Statement
```csharp
using Orchestra.Core.Services;
```

### 3.2A: Update RealEndToEndTestFactory - Service Registration
- Modify `src/Orchestra.Tests/RealEndToEndTestFactory.cs`
- Apply same pattern as TestWebApplicationFactory

**Add in ConfigureServices after line 73**:
```csharp
// Register isolated JobStorage for real E2E tests
services.AddSingleton<JobStorage>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var hangfireConnection = configuration["HANGFIRE_CONNECTION"];

    var storage = new SQLiteStorage(hangfireConnection, new SQLiteStorageOptions
    {
        QueuePollInterval = TimeSpan.FromMilliseconds(100),
        JobExpirationCheckInterval = TimeSpan.FromSeconds(30)
    });

    // Do NOT set JobStorage.Current
    return storage;
});

services.AddSingleton<IHangfireStorageService, HangfireStorageService>();
```

### 3.2B: Handle Any JobStorage.Current References
- Search for any JobStorage.Current usage in factory
- Replace with DI approach if found

### 3.3A: Create Storage Disposal Helper
- Ensure proper cleanup of storage instances
- Add disposal in factory Dispose method

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        // Get storage service and dispose if it implements IDisposable
        using (var scope = Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetService<JobStorage>();
            if (storage is IDisposable disposableStorage)
            {
                try
                {
                    disposableStorage.Dispose();
                }
                catch
                {
                    // Ignore disposal errors during cleanup
                }
            }
        }

        // Continue with existing cleanup...
    }

    base.Dispose(disposing);
}
```

### 3.3B: Verify Isolation
- Ensure no cross-factory storage references
- Validate each factory gets unique storage instance
- Confirm JobStorage.Current remains null in tests

## Acceptance Criteria

- [ ] TestWebApplicationFactory uses DI for JobStorage
- [ ] RealEndToEndTestFactory uses DI for JobStorage
- [ ] JobStorage.Current NOT set in test factories
- [ ] CleanupHangfireData uses IHangfireStorageService
- [ ] Storage properly disposed on factory disposal
- [ ] Compilation successful

## Dependencies

- Phase 1: Core Services completed
- Phase 2: Service Registration completed

## Critical Points

⚠️ **NEVER set JobStorage.Current in test factories** - This is the root cause of the race condition
⚠️ **Each factory must create its own isolated storage instance**
⚠️ **Storage instances must not be shared between test collections**

## Estimated Time: 1 hour

## Verification Steps

1. Compile both factories
2. Run single test from each collection
3. Verify JobStorage.Current is null in tests
4. Check storage isolation with debugger

## Notes

- This is the most critical phase for fixing the race condition
- Test factories must be completely isolated
- Consider adding logging to verify isolation