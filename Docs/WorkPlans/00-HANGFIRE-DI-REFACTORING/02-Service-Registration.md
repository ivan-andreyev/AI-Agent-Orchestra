# Phase 2: Service Registration Configuration

**Parent Plan**: [00-HANGFIRE-DI-REFACTORING.md](../00-HANGFIRE-DI-REFACTORING.md)

## Objective

Configure dependency injection registration for JobStorage and IHangfireStorageService in both production and test environments.

## Tasks

### 2.1A: Update Startup.cs - Production Registration ✅ COMPLETE
- [x] Modify `src/Orchestra.API/Startup.cs`
- [x] Add JobStorage registration after Hangfire configuration
- [x] Register IHangfireStorageService

**Location**: After line 141 in ConfigureServices method (now lines 143-158)

**Implementation Notes (2025-01-03)**:
- Added JobStorage singleton registration that retrieves from JobStorage.Current
- Added validation check with InvalidOperationException if JobStorage.Current is null
- Registered IHangfireStorageService as singleton wrapping JobStorage
- Maintains backward compatibility by using JobStorage.Current in production
- Build validation: Orchestra.API compiles successfully with 0 errors
- Registration order verified: AddHangfire → JobStorage registration → AddHangfireServer

```csharp
// Register JobStorage for dependency injection
// In production, we get it from JobStorage.Current for compatibility
services.AddSingleton<JobStorage>(provider =>
{
    // JobStorage.Current is set by AddHangfire configuration above
    // We expose it for DI while maintaining backward compatibility
    if (JobStorage.Current == null)
    {
        throw new InvalidOperationException(
            "JobStorage.Current is not initialized. Ensure AddHangfire is called before this registration.");
    }
    return JobStorage.Current;
});

// Register the wrapper service for clean DI access
services.AddSingleton<IHangfireStorageService, HangfireStorageService>();
```

### 2.1B: Add Using Statement
- Add to top of Startup.cs if not present:
```csharp
using Orchestra.Core.Services;
```

### 2.1C: Verify Registration Order
- Ensure AddHangfire() is called before JobStorage registration
- Confirm HangfireServer registration comes after storage setup
- Validate service lifecycle compatibility (Singleton)

### 2.2A: Create Test Storage Factory
- Create helper for test storage creation
- File: `src/Orchestra.Tests/Infrastructure/TestStorageFactory.cs`

```csharp
namespace Orchestra.Tests.Infrastructure;

/// <summary>
/// Factory for creating isolated Hangfire storage instances for tests.
/// </summary>
public static class TestStorageFactory
{
    /// <summary>
    /// Creates an isolated SQLite storage instance for a test collection.
    /// </summary>
    public static JobStorage CreateIsolatedStorage(string connectionString)
    {
        var options = new SQLiteStorageOptions
        {
            QueuePollInterval = TimeSpan.FromMilliseconds(100),
            // Fast polling for tests
            JobExpirationCheckInterval = TimeSpan.FromSeconds(30),
            CountersAggregateInterval = TimeSpan.FromSeconds(30)
        };

        return new SQLiteStorage(connectionString, options);
    }

    /// <summary>
    /// Creates an in-memory storage instance for unit tests.
    /// </summary>
    public static JobStorage CreateInMemoryStorage()
    {
        // Configure in-memory storage for fast unit tests
        return new InMemoryStorage(new InMemoryStorageOptions
        {
            // TODO: Configure options
        });
    }
}
```

### 2.2B: Integration Points Verification
- Check HangfireOrchestrator compatibility
- Verify TaskExecutionJob access patterns
- Ensure BackgroundJobClient still works

### 2.2C: Compile and Basic Test
- Build solution without errors
- Run a simple integration test
- Verify services resolve correctly

## Acceptance Criteria

- [ ] JobStorage registered in DI container
- [ ] IHangfireStorageService registered and resolvable
- [ ] Production maintains JobStorage.Current for compatibility
- [ ] Test factory creates isolated instances
- [ ] No compilation errors
- [ ] Basic service resolution test passes

## Dependencies

- Phase 1: Core Services must be completed
- Hangfire configuration must be in place

## Risk Mitigation

- Keep JobStorage.Current in production for backward compatibility
- Use factory pattern for test isolation
- Maintain same service lifetimes (Singleton)

## Estimated Time: 45 minutes

## Notes

- Production still sets JobStorage.Current for dashboard compatibility
- Tests will NOT set JobStorage.Current to avoid conflicts
- Consider logging registration success for debugging