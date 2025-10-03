# Hangfire DI Refactoring - Architecture Diagram

## High-Level Architecture Transformation

```mermaid
graph TB
    subgraph "BEFORE: Singleton Pattern (PROBLEM)"
        style BEFORE fill:#ffcccc

        GlobalSingleton[JobStorage.Current - Global Static]

        TestFactory1[TestWebApplicationFactory] --> Storage1[SQLiteStorage Instance 1]
        Storage1 -.->|Sets| GlobalSingleton

        TestFactory2[RealEndToEndTestFactory] --> Storage2[SQLiteStorage Instance 2]
        Storage2 -.->|Overwrites!| GlobalSingleton

        IntegrationTests[Integration Tests] --> GlobalSingleton
        RealE2ETests[Real E2E Tests] --> GlobalSingleton

        GlobalSingleton --> Disposed[ObjectDisposedException]

        note1[Race Condition: Last writer wins]
    end

    subgraph "AFTER: Dependency Injection (SOLUTION)"
        style AFTER fill:#ccffcc

        DIContainer[DI Container - IServiceProvider]

        IHangfireStorage[IHangfireStorageService]
        DIContainer --> IHangfireStorage

        TestFactory3[TestWebApplicationFactory] --> DIContainer
        TestFactory4[RealEndToEndTestFactory] --> DIContainer

        DIContainer --> IsolatedStorage1[Scoped JobStorage 1]
        DIContainer --> IsolatedStorage2[Scoped JobStorage 2]

        IntegrationTests2[Integration Tests] --> IsolatedStorage1
        RealE2ETests2[Real E2E Tests] --> IsolatedStorage2

        note2[Complete Isolation: No shared state]
    end
```

## Component Architecture

```mermaid
classDiagram
    class IHangfireStorageService {
        <<interface>>
        +JobStorage Storage
        +GetMonitoringApi() IMonitoringApi
        +GetConnection() IStorageConnection
    }

    class HangfireStorageService {
        -JobStorage _storage
        +HangfireStorageService(JobStorage storage)
        +JobStorage Storage
        +GetMonitoringApi() IMonitoringApi
        +GetConnection() IStorageConnection
    }

    class JobStorage {
        <<abstract>>
        +static JobStorage Current
        +GetMonitoringApi() IMonitoringApi
        +GetConnection() IStorageConnection
    }

    class SQLiteStorage {
        +SQLiteStorage(string connectionString)
        +GetMonitoringApi() IMonitoringApi
        +GetConnection() IStorageConnection
        +Dispose()
    }

    class TestWebApplicationFactory {
        -string _testInstanceId
        -string _hangfireDbName
        +ConfigureServices(IServiceCollection)
        +CreateStorage() JobStorage
    }

    class RealEndToEndTestFactory {
        -string _testInstanceId
        -string _hangfireDbName
        +ConfigureServices(IServiceCollection)
        +CreateStorage() JobStorage
    }

    IHangfireStorageService <|.. HangfireStorageService
    HangfireStorageService --> JobStorage
    JobStorage <|-- SQLiteStorage
    TestWebApplicationFactory --> IHangfireStorageService
    RealEndToEndTestFactory --> IHangfireStorageService
```

## Sequence Diagram - Test Execution Flow

```mermaid
sequenceDiagram
    participant T1 as Test Collection 1
    participant TF1 as TestWebApplicationFactory
    participant DI1 as DI Container 1
    participant S1 as SQLiteStorage 1
    participant T2 as Test Collection 2
    participant TF2 as RealEndToEndFactory
    participant DI2 as DI Container 2
    participant S2 as SQLiteStorage 2

    Note over T1,S2: Parallel Execution Starts

    par Collection 1 Execution
        T1->>TF1: Create Factory
        TF1->>DI1: Configure Services
        DI1->>S1: Create Isolated Storage
        TF1->>DI1: Register IHangfireStorageService
        T1->>DI1: Request IHangfireStorageService
        DI1-->>T1: Return Service (wrapping S1)
        T1->>S1: Use GetMonitoringApi()
        Note over S1: Storage 1 Active
    and Collection 2 Execution
        T2->>TF2: Create Factory
        TF2->>DI2: Configure Services
        DI2->>S2: Create Isolated Storage
        TF2->>DI2: Register IHangfireStorageService
        T2->>DI2: Request IHangfireStorageService
        DI2-->>T2: Return Service (wrapping S2)
        T2->>S2: Use GetMonitoringApi()
        Note over S2: Storage 2 Active
    end

    Note over S1,S2: No Interference - Complete Isolation

    T1->>S1: Dispose
    Note over S1: Storage 1 Disposed
    T2->>S2: Continue Using
    Note over S2: Storage 2 Unaffected
```

## Data Flow Architecture

```mermaid
flowchart LR
    subgraph Production Environment
        ProdStartup[Startup.cs]
        ProdDI[Production DI Container]
        ProdStorage[PostgreSQL Storage]
        GlobalCurrent[JobStorage.Current]

        ProdStartup -->|AddHangfire| ProdDI
        ProdDI -->|Creates| ProdStorage
        ProdStorage -->|Sets for compatibility| GlobalCurrent
        ProdDI -->|Registers| IHangfireStorageService1[IHangfireStorageService]
    end

    subgraph Test Environment - Collection 1
        Test1[Integration Tests]
        TestDI1[Test DI Container 1]
        TestStorage1[SQLite Storage 1]
        Service1[IHangfireStorageService]

        Test1 -->|Creates| TestDI1
        TestDI1 -->|Creates| TestStorage1
        TestDI1 -->|Registers| Service1
        Service1 -->|Wraps| TestStorage1
        Test1 -->|Uses| Service1
    end

    subgraph Test Environment - Collection 2
        Test2[E2E Tests]
        TestDI2[Test DI Container 2]
        TestStorage2[SQLite Storage 2]
        Service2[IHangfireStorageService]

        Test2 -->|Creates| TestDI2
        TestDI2 -->|Creates| TestStorage2
        TestDI2 -->|Registers| Service2
        Service2 -->|Wraps| TestStorage2
        Test2 -->|Uses| Service2
    end

    Note1[No Cross-Collection References]
```

## Storage Lifecycle Management

```mermaid
stateDiagram-v2
    [*] --> Created: Factory.ConfigureServices()

    Created --> Registered: services.AddSingleton<JobStorage>()
    Registered --> Wrapped: services.AddSingleton<IHangfireStorageService>()
    Wrapped --> Injected: GetRequiredService<IHangfireStorageService>()

    Injected --> Active: Test Execution
    Active --> Active: Multiple Test Methods

    Active --> Disposing: Test Collection Complete
    Disposing --> Disposed: storage.Dispose()
    Disposed --> [*]

    note right of Active
        Isolated per test collection
        No global state mutation
    end note

    note right of Disposed
        Other collections unaffected
        Clean disposal, no leaks
    end note
```

## Dependency Resolution Flow

```mermaid
graph TD
    subgraph Service Registration Phase
        AddHangfire[services.AddHangfire()]
        CreateStorage[Create JobStorage Instance]
        RegisterStorage[services.AddSingleton JobStorage]
        RegisterWrapper[services.AddSingleton IHangfireStorageService]

        AddHangfire --> CreateStorage
        CreateStorage --> RegisterStorage
        RegisterStorage --> RegisterWrapper
    end

    subgraph Resolution Phase
        RequestService[GetRequiredService IHangfireStorageService]
        ResolveStorage[Resolve JobStorage dependency]
        CreateWrapper[new HangfireStorageService(storage)]
        ReturnService[Return IHangfireStorageService]

        RequestService --> ResolveStorage
        ResolveStorage --> CreateWrapper
        CreateWrapper --> ReturnService
    end

    subgraph Usage Phase
        UseService[storageService.GetMonitoringApi()]
        DelegateToStorage[_storage.GetMonitoringApi()]
        ReturnApi[Return IMonitoringApi]

        UseService --> DelegateToStorage
        DelegateToStorage --> ReturnApi
    end
```

## Migration Path

```mermaid
graph LR
    subgraph Phase 1 - Current State
        Current[JobStorage.Current everywhere]
    end

    subgraph Phase 2 - Wrapper Introduction
        Wrapper[IHangfireStorageService wrapper]
        Current2[JobStorage.Current in production]
        DITest[DI in tests]
    end

    subgraph Phase 3 - Test Migration
        WrapperProd[Wrapper in production]
        DITestComplete[All tests using DI]
        CurrentCompat[JobStorage.Current for compatibility]
    end

    subgraph Phase 4 - Future State
        FullDI[Full DI everywhere]
        NoGlobal[No JobStorage.Current]
    end

    Current --> Wrapper
    Wrapper --> DITest
    DITest --> DITestComplete
    DITestComplete --> FullDI

    style Phase 1 fill:#ffcccc
    style Phase 2 fill:#ffffcc
    style Phase 3 fill:#ccffcc
    style Phase 4 fill:#ccccff
```

## Key Architectural Decisions

1. **Wrapper Pattern**: Create IHangfireStorageService to abstract JobStorage access
2. **Gradual Migration**: Keep JobStorage.Current in production for backward compatibility
3. **Test Isolation First**: Focus on fixing test infrastructure before production changes
4. **Scoped Registration**: Use appropriate DI lifetimes for test vs production scenarios
5. **No Breaking Changes**: Ensure all existing code continues to work during migration