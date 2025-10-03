# Remove HangfireServer Tests Plan - Architecture

## High-Level Architecture Transformation

### Current Architecture (Phase 1 - Sequential)
```mermaid
graph TB
    subgraph "Test Execution (Sequential)"
        IT[Integration Tests Collection]
        RE[RealE2E Tests Collection]
        IT -->|completes| RE
    end

    subgraph "Shared Infrastructure"
        HS[HangfireServer - Singleton]
        IT --> HS
        RE --> HS
    end

    subgraph "Isolated Storage"
        S1[SQLiteStorage-Integration]
        S2[SQLiteStorage-RealE2E]
        HS -->|polls| S1
        HS -->|polls| S2
    end

    style IT fill:#f9f,stroke:#333,stroke-width:2px
    style RE fill:#f9f,stroke:#333,stroke-width:2px
    style HS fill:#ff9,stroke:#333,stroke-width:4px
```

### Target Architecture (Phase 2 - Parallel with Synchronous Execution)
```mermaid
graph TB
    subgraph "Test Execution (Parallel)"
        IT[Integration Tests Collection]
        RE[RealE2E Tests Collection]
    end

    subgraph "Test-Specific Infrastructure"
        TBC1[TestBackgroundJobClient-1]
        TBC2[TestBackgroundJobClient-2]
        IT --> TBC1
        RE --> TBC2
    end

    subgraph "Direct Execution"
        TEJ1[TaskExecutionJob-1]
        TEJ2[TaskExecutionJob-2]
        TBC1 -->|synchronous| TEJ1
        TBC2 -->|synchronous| TEJ2
    end

    subgraph "Isolated Execution"
        MAE1[MockAgentExecutor]
        CCE2[ClaudeCodeExecutor]
        TEJ1 --> MAE1
        TEJ2 --> CCE2
    end

    style IT fill:#9f9,stroke:#333,stroke-width:2px
    style RE fill:#9f9,stroke:#333,stroke-width:2px
    style TBC1 fill:#9ff,stroke:#333,stroke-width:2px
    style TBC2 fill:#9ff,stroke:#333,stroke-width:2px
```

## Detailed Component Architecture

### TestBackgroundJobClient Implementation Flow
```mermaid
flowchart LR
    subgraph "IBackgroundJobClient Interface"
        E[Enqueue Method]
        S[Schedule Method]
        D[Delete Method]
    end

    subgraph "TestBackgroundJobClient"
        EP[Expression Parser]
        JR[Job Resolver]
        SE[Synchronous Executor]
        JH[Job History Tracker]
    end

    subgraph "Execution Pipeline"
        DI[DI Container]
        JI[Job Instance]
        PC[PerformContext]
        EX[Execute Method]
    end

    E --> EP
    S --> EP
    EP --> JR
    JR --> DI
    DI --> JI
    JR --> PC
    JI --> SE
    PC --> SE
    SE --> EX
    SE --> JH
```

### Job Execution Sequence
```mermaid
sequenceDiagram
    participant Test
    participant HangfireOrchestrator
    participant TestBackgroundJobClient
    participant DIContainer
    participant TaskExecutionJob
    participant IAgentExecutor

    Test->>HangfireOrchestrator: QueueTaskAsync()
    HangfireOrchestrator->>TestBackgroundJobClient: Enqueue<TaskExecutionJob>()

    Note over TestBackgroundJobClient: Parse Expression Tree
    TestBackgroundJobClient->>DIContainer: Resolve TaskExecutionJob
    DIContainer-->>TestBackgroundJobClient: Job Instance

    Note over TestBackgroundJobClient: Create TestPerformContext
    TestBackgroundJobClient->>TaskExecutionJob: ExecuteAsync(params, context)

    TaskExecutionJob->>IAgentExecutor: ExecuteCommandAsync()
    IAgentExecutor-->>TaskExecutionJob: Result
    TaskExecutionJob-->>TestBackgroundJobClient: Complete

    Note over TestBackgroundJobClient: Track in Job History
    TestBackgroundJobClient-->>HangfireOrchestrator: JobId
    HangfireOrchestrator-->>Test: TaskId
```

## Key Architectural Components

### 1. TestBackgroundJobClient Class Structure
```mermaid
classDiagram
    class IBackgroundJobClient {
        <<interface>>
        +Create(Job job, IState state) string
        +Enqueue(Expression methodCall) string
        +Schedule(Expression methodCall, TimeSpan delay) string
        +Delete(string jobId) bool
        +Requeue(string jobId) bool
    }

    class TestBackgroundJobClient {
        -IServiceProvider serviceProvider
        -ILogger logger
        -Dictionary jobHistory
        +Enqueue(Expression methodCall) string
        +Schedule(Expression methodCall, TimeSpan delay) string
        -ExecuteJobSynchronously(Expression) Task
        -CreatePerformContext(jobId) PerformContext
        -TrackJobExecution(JobExecutionInfo) void
        +GetJobHistory() IReadOnlyList
    }

    class JobExecutionInfo {
        +string JobId
        +Type JobType
        +string MethodName
        +object[] Arguments
        +DateTime EnqueuedAt
        +DateTime ExecutedAt
        +JobExecutionStatus Status
        +Exception Exception
        +object Result
    }

    IBackgroundJobClient <|.. TestBackgroundJobClient
    TestBackgroundJobClient --> JobExecutionInfo
```

### 2. Expression Parsing Architecture
```mermaid
graph TD
    EX[Expression Tree]

    subgraph "Expression Types"
        ME[MethodCallExpression]
        LE[LambdaExpression]
        CE[ConstantExpression]
    end

    subgraph "Extraction"
        JT[Job Type]
        MN[Method Name]
        AR[Arguments Array]
    end

    EX --> ME
    EX --> LE
    ME --> JT
    ME --> MN
    ME --> CE
    CE --> AR

    JT --> RES[Resolution]
    MN --> RES
    AR --> RES
    RES --> EXEC[Execution]
```

### 3. Dependency Injection Scoping
```mermaid
graph TB
    subgraph "Root Service Provider"
        RSP[IServiceProvider]
        SING[Singleton Services]
    end

    subgraph "Job Execution Scope"
        SCOPE[IServiceScope]
        SCOPED[Scoped Services]
        JOB[Job Instance]
    end

    RSP -->|CreateScope| SCOPE
    SCOPE --> SCOPED
    SCOPED --> JOB
    JOB -->|Execute| RESULT[Result]
    RESULT -->|Dispose| SCOPE
```

## Integration Points

### Test Factory Configuration Changes
```mermaid
graph LR
    subgraph "Current Configuration"
        HS[AddHangfireServer]
        BG[IBackgroundJobClient]
        JS[JobStorage]
    end

    subgraph "New Configuration"
        TBC[TestBackgroundJobClient]
        TJS[Test JobStorage]
        NHS[No HangfireServer]
    end

    HS -->|Remove| NHS
    BG -->|Replace| TBC
    JS -->|Keep for Compatibility| TJS
```

### Parallel Execution Isolation
```mermaid
graph TB
    subgraph "Collection 1 - Integration"
        T1[Test 1] --> F1[TestFactory 1]
        F1 --> TBC1[TestBackgroundJobClient 1]
        TBC1 --> ISO1[Isolated Execution]
    end

    subgraph "Collection 2 - RealE2E"
        T2[Test 2] --> F2[TestFactory 2]
        F2 --> TBC2[TestBackgroundJobClient 2]
        TBC2 --> ISO2[Isolated Execution]
    end

    ISO1 -.NO SHARED STATE.-> ISO2
```

## Data Flow Architecture

### Job State Tracking
```mermaid
stateDiagram-v2
    [*] --> Enqueued: Enqueue()
    Enqueued --> Executing: Begin Execution
    Executing --> Succeeded: No Errors
    Executing --> Failed: Exception Thrown
    Succeeded --> [*]
    Failed --> [*]

    note right of Executing
        Synchronous execution
        No polling required
    end note

    note right of Failed
        Exception captured
        in JobExecutionInfo
    end note
```

### Test Assertion Flow
```mermaid
graph LR
    TEST[Test Code]
    EXEC[Execute Operation]
    TBC[TestBackgroundJobClient]
    HIST[Job History]
    ASSERT[Assert Job State]

    TEST --> EXEC
    EXEC --> TBC
    TBC --> HIST
    TEST --> ASSERT
    ASSERT --> HIST

    HIST -->|Verify| PASS[Test Pass]
    HIST -->|Verify| FAIL[Test Fail]
```

## Performance Characteristics

### Sequential vs Parallel Execution
```mermaid
gantt
    title Test Execution Timeline
    dateFormat mm:ss
    section Sequential (Current)
        Integration Tests    :done, int1, 00:00, 4m
        RealE2E Tests       :done, re1, after int1, 5m
        Total Time          :crit, 00:00, 9m

    section Parallel (Target)
        Integration Tests    :active, int2, 00:00, 4m
        RealE2E Tests       :active, re2, 00:00, 5m
        Total Time          :crit, 00:00, 5m
```

## Error Handling Architecture

### Exception Propagation
```mermaid
flowchart TD
    JOB[Job Execution]

    JOB --> TRY{Try Execute}
    TRY -->|Success| RES[Return Result]
    TRY -->|Exception| CATCH[Catch Exception]

    CATCH --> LOG[Log Error]
    CATCH --> TRACK[Track in History]
    CATCH --> PROP[Propagate to Test]

    PROP --> ASSERT[Test Assertion]
    ASSERT -->|Verify Exception| VALIDATE[Validate Error Type]
```

## Migration Path

### Phase-by-Phase Transformation
```mermaid
graph LR
    subgraph "Phase 1 (Current)"
        SEQ[Sequential Tests]
        HS1[HangfireServer]
    end

    subgraph "Phase 2 (Transition)"
        IMPL[Implement TestBackgroundJobClient]
        INT[Integration Tests]
    end

    subgraph "Phase 3 (Target)"
        PAR[Parallel Tests]
        NO_HS[No HangfireServer]
    end

    SEQ --> IMPL
    HS1 --> INT
    IMPL --> PAR
    INT --> NO_HS
```

## Dependencies Between Plan Sections

- **Phase 1** → **Phase 2**: Test infrastructure must be complete before integration
- **Phase 2** → **Phase 3**: Test factories must be updated before job execution changes
- **Phase 3** → **Phase 4**: Job execution must work before fixing compatibility
- **Phase 4** → **Phase 5**: All tests must pass before enabling parallelization
- **Phase 5** → **Phase 6**: Parallel execution must be stable before final validation