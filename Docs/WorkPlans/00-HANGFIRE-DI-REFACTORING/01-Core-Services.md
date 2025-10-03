# Phase 1: Core Services Implementation

**Parent Plan**: [00-HANGFIRE-DI-REFACTORING.md](../00-HANGFIRE-DI-REFACTORING.md)

## Objective

Create the abstraction layer for Hangfire storage access through dependency injection, replacing direct usage of JobStorage.Current.

## Tasks

### 1.1A: Create IHangfireStorageService Interface ✅ COMPLETE
- [x] Create file: `src/Orchestra.Core/Services/IHangfireStorageService.cs`
- [x] Define interface with essential methods
- [x] Add XML documentation (в русской локализации согласно стандартам проекта)

```csharp
namespace Orchestra.Core.Services;

/// <summary>
/// Abstraction for Hangfire storage access through dependency injection.
/// Replaces direct usage of JobStorage.Current singleton.
/// </summary>
public interface IHangfireStorageService
{
    /// <summary>
    /// Gets the underlying JobStorage instance.
    /// </summary>
    JobStorage Storage { get; }

    /// <summary>
    /// Gets the monitoring API for job statistics and information.
    /// </summary>
    IMonitoringApi GetMonitoringApi();

    /// <summary>
    /// Gets a storage connection for advanced operations.
    /// </summary>
    IStorageConnection GetConnection();
}
```

### 1.1B: Implement HangfireStorageService Class ✅ COMPLETE
- [x] Create file: `src/Orchestra.Core/Services/HangfireStorageService.cs`
- [x] Implement interface wrapping JobStorage instance
- [x] Add null checking and error handling
- [x] Add XML documentation in Russian

**Implementation Notes (2025-01-03)**:
- Created `HangfireStorageService` class implementing `IHangfireStorageService`
- Constructor validates storage parameter with `ArgumentNullException`
- All interface members implemented: `Storage` property, `GetMonitoringApi()`, `GetConnection()`
- XML documentation provided in Russian for all public members
- Simplified implementation without disposal tracking (JobStorage lifetime managed by DI container)
- Build validation: Orchestra.Core and Orchestra.API compile successfully with 0 errors

```csharp
namespace Orchestra.Core.Services;

/// <summary>
/// Реализация по умолчанию интерфейса IHangfireStorageService.
/// Оборачивает экземпляр JobStorage для внедрения зависимостей.
/// </summary>
public class HangfireStorageService : IHangfireStorageService
{
    private readonly JobStorage _storage;

    /// <summary>
    /// Инициализирует новый экземпляр класса HangfireStorageService.
    /// </summary>
    /// <param name="storage">Экземпляр хранилища Hangfire.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если storage равен null.</exception>
    public HangfireStorageService(JobStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// Получает базовый экземпляр JobStorage.
    /// </summary>
    public JobStorage Storage => _storage;

    /// <summary>
    /// Получает API мониторинга для статистики и информации о заданиях.
    /// </summary>
    /// <returns>Экземпляр IMonitoringApi для доступа к информации о заданиях.</returns>
    public IMonitoringApi GetMonitoringApi()
    {
        return _storage.GetMonitoringApi();
    }

    /// <summary>
    /// Получает соединение с хранилищем для расширенных операций.
    /// </summary>
    /// <returns>Экземпляр IStorageConnection для выполнения операций с хранилищем.</returns>
    public IStorageConnection GetConnection()
    {
        return _storage.GetConnection();
    }
}
```

### 1.1C: Add Required Using Statements ✅ COMPLETE
- [x] Update Orchestra.Core.csproj to reference Hangfire.Core
- [x] Ensure proper package versions (1.8.17)

```xml
<PackageReference Include="Hangfire.Core" Version="1.8.17" />
```

### 1.1D: Integration Validation ✅ COMPLETE
- [x] Compile project successfully (Orchestra.Core и Orchestra.API собраны без ошибок)
- [x] Verify no namespace conflicts (только warnings CS1998 из существующего кода)
- [x] Ensure interface accessible from other projects (Orchestra.API успешно компилируется с референсом на Orchestra.Core)

## Acceptance Criteria

- [x] Interface defined with all required methods
- [x] Implementation wraps JobStorage correctly (Task 1.1B - ✅ COMPLETE)
- [x] Proper null checking and error handling (Task 1.1B - ✅ COMPLETE)
- [x] XML documentation complete
- [x] Compiles without errors

## Execution Notes (2025-01-03)

**Task 1.1A Completed**:
- Created `C:\Users\mrred\RiderProjects\AI-Agent-Orchestra\src\Orchestra.Core\Services\IHangfireStorageService.cs`
- Interface defines 3 core members: `Storage` property, `GetMonitoringApi()`, `GetConnection()`
- XML documentation provided in Russian as per project standards
- Added `Hangfire.Core 1.8.17` package reference to `Orchestra.Core.csproj`
- Build validation: Orchestra.Core and Orchestra.API compile successfully with 0 errors
- Interface is accessible from Orchestra.API project

**Stopped After Task 1.1A**: Following plan-executor rules, executed ONLY the deepest uncompleted task (1.1A) and stopped immediately. Task 1.1B (implementation) remains for next execution cycle.

**Task 1.1B Completed (2025-01-03)**:
- Created `C:\Users\mrred\RiderProjects\AI-Agent-Orchestra\src\Orchestra.Core\Services\HangfireStorageService.cs`
- Implements `IHangfireStorageService` with proper dependency injection pattern
- Constructor validates `JobStorage` parameter with `ArgumentNullException`
- All interface members fully implemented:
  - `Storage` property (returns wrapped JobStorage instance)
  - `GetMonitoringApi()` method (delegates to storage)
  - `GetConnection()` method (delegates to storage)
- Comprehensive XML documentation in Russian for all public members
- Simplified implementation without disposal tracking (lifetime managed by DI container)
- Build validation: 0 errors in both Orchestra.Core and Orchestra.API
- **EXECUTION STOPPED** - Task 1.1B complete, ready for REVIEW_ITERATION mode

## Dependencies

- Hangfire.Core package
- Orchestra.Core project structure

## Estimated Time: 30 minutes

## Notes

- Keep interface minimal - only essential methods
- Consider future extensibility
- Maintain backward compatibility