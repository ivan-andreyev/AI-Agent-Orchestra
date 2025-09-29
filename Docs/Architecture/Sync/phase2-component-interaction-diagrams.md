# Phase 2 Agent Infrastructure Component Interaction Diagrams
**Document Type**: Component Interaction Analysis
**Architecture Reference**: [Phase 2 Agent Infrastructure Architecture](../Planned/phase2-agent-infrastructure-architecture.md)
**Implementation**: [Phase 2 Agent Infrastructure Implementation](../Actual/phase2-agent-infrastructure-implementation.md)
**Last Updated**: 2025-09-27
**Status**: Complete System Interaction Documentation

## System-Level Architecture

```mermaid
graph TB
    subgraph "External Systems"
        CAI[Claude AI API]
        CLI[Claude CLI]
        PROC[System Processes]
    end

    subgraph "Agent Infrastructure Layer"
        ADS[AgentDiscoveryService]
        AHS[AgentHealthCheckService]
        EXE[ClaudeCodeExecutor]
    end

    subgraph "MediatR CQRS Layer"
        CMD[Commands]
        QRY[Queries]
        EVT[Events]
        HDLR[Handlers]
    end

    subgraph "API Layer"
        CTRL[AgentsController]
        REST[REST Endpoints]
    end

    subgraph "Data Layer"
        ENT[Agent Entity]
        CTX[DbContext]
        DB[(Database)]
    end

    %% External connections
    ADS --> CAI
    ADS --> PROC
    EXE --> CAI
    EXE --> CLI

    %% Service layer connections
    ADS --> CMD
    AHS --> CMD
    AHS --> QRY

    %% API layer connections
    CTRL --> CMD
    CTRL --> QRY
    REST --> CTRL

    %% MediatR flow
    CMD --> HDLR
    QRY --> HDLR
    HDLR --> EVT
    HDLR --> CTX

    %% Data layer
    CTX --> ENT
    CTX --> DB

    style CAI fill:#e1f5fe
    style CLI fill:#e1f5fe
    style ADS fill:#f3e5f5
    style AHS fill:#f3e5f5
    style CMD fill:#e8f5e8
    style QRY fill:#e8f5e8
    style EVT fill:#fff3e0
    style DB fill:#ffebee
```

## Component-Level Interaction Flows

### 1. Agent Discovery and Registration Flow

```mermaid
sequenceDiagram
    participant Timer as Background Timer
    participant ADS as AgentDiscoveryService
    participant HTTP as HttpClient
    participant API as Claude Code API
    participant MED as IMediator
    participant HDL as RegisterAgentCommandHandler
    participant CTX as OrchestraDbContext
    participant EVT as Event Bus

    Timer->>ADS: Trigger Discovery (Every 2 minutes)
    ADS->>ADS: Create HttpClient with 5s timeout

    loop For each port [3001, 3000, 8080, 55001]
        ADS->>HTTP: GET localhost:{port}/health
        HTTP->>API: Health Check Request

        alt API Available
            API-->>HTTP: 200 OK
            HTTP-->>ADS: API Available

            ADS->>HTTP: GET localhost:{port}/info
            HTTP->>API: Agent Info Request
            API-->>HTTP: Agent Details JSON
            HTTP-->>ADS: Agent Information

            ADS->>ADS: Parse Agent Info
            ADS->>MED: Send RegisterAgentCommand

            MED->>HDL: Handle RegisterAgentCommand
            HDL->>HDL: Validate Request
            HDL->>CTX: Check Existing Agent
            CTX-->>HDL: Existing Agent or null

            alt New Agent
                HDL->>CTX: Add New Agent
                HDL->>CTX: SaveChangesAsync()
                CTX-->>HDL: Agent Saved
                HDL->>EVT: Publish AgentRegisteredEvent
            else Existing Agent
                HDL->>CTX: Update Agent Properties
                HDL->>CTX: SaveChangesAsync()
                CTX-->>HDL: Agent Updated
                HDL->>EVT: Publish AgentUpdatedEvent
            end

            HDL-->>MED: RegisterAgentResult(Success=true)
            MED-->>ADS: Command Result
            ADS->>ADS: Add to _knownAgents

        else API Unavailable
            API-->>HTTP: Connection Failed
            HTTP-->>ADS: Port Not Available
        end
    end

    ADS->>ADS: Log Discovery Results
```

### 2. Agent Health Monitoring Flow

```mermaid
sequenceDiagram
    participant Timer as Background Timer
    participant AHS as AgentHealthCheckService
    participant MED as IMediator
    participant GAQ as GetAllAgentsQueryHandler
    participant CTX as OrchestraDbContext
    participant UAS as UpdateAgentStatusCommandHandler
    participant EVT as Event Bus

    Timer->>AHS: Trigger Health Check (Every 1 minute)
    AHS->>MED: Send GetAllAgentsQuery(ActiveOnly=true)

    MED->>GAQ: Handle GetAllAgentsQuery
    GAQ->>CTX: Query Active Agents
    CTX-->>GAQ: List<Agent>
    GAQ-->>MED: Active Agents
    MED-->>AHS: Agent List

    par For Each Agent
        AHS->>AHS: Calculate TimeSinceLastPing
        AHS->>AHS: DetermineExpectedStatus()

        alt Status Change Required
            Note over AHS: LastPing > AgentTimeout OR Recovery conditions
            AHS->>MED: Send UpdateAgentStatusCommand

            MED->>UAS: Handle UpdateAgentStatusCommand
            UAS->>CTX: Find Agent by ID
            CTX-->>UAS: Agent Entity
            UAS->>UAS: Store Previous Status
            UAS->>CTX: Update Agent Status
            UAS->>CTX: SaveChangesAsync()
            CTX-->>UAS: Status Updated

            UAS->>EVT: Publish AgentStatusChangedEvent
            Note over EVT: Event contains AgentId, PreviousStatus, NewStatus

            UAS-->>MED: UpdateAgentStatusResult(Success=true)
            MED-->>AHS: Command Result

        else No Status Change
            Note over AHS: Agent status remains unchanged
        end
    end

    AHS->>AHS: Log Health Check Cycle Completion
```

### 3. Command Execution with HTTP/CLI Fallback

```mermaid
sequenceDiagram
    participant CLIENT as API Client
    participant CTRL as AgentsController
    participant EXE as ClaudeCodeExecutor
    participant SEM as SemaphoreSlim
    participant HTTP as HttpClient
    participant API as Claude Code API
    participant PROC as Process
    participant CLI as Claude CLI

    CLIENT->>CTRL: POST /api/agents/{id}/execute
    CTRL->>EXE: ExecuteCommandAsync(command, workingDir)

    EXE->>SEM: WaitAsync() [Concurrency Control: 3 max]
    SEM-->>EXE: Semaphore Acquired

    EXE->>EXE: Start Timing

    %% HTTP API Attempt
    EXE->>HTTP: TryExecuteViaHttpApi()

    loop For each port [3001, 3000, 8080, 55001]
        HTTP->>API: GET localhost:{port}/health

        alt API Available
            API-->>HTTP: 200 OK
            HTTP->>API: POST localhost:{port}/execute
            Note over HTTP,API: {"command": "...", "workingDirectory": "...", "timeout": 300000}
            API-->>HTTP: Execution Result JSON
            HTTP-->>EXE: AgentExecutionResult(Success=true)
            EXE->>EXE: Create Success Response
            Note over EXE: Method: "HTTP API", Port: {port}
            EXE->>SEM: Release()
            EXE-->>CTRL: AgentExecutionResponse
            CTRL-->>CLIENT: 200 OK with results
        else API Unavailable
            API-->>HTTP: Connection Failed/Timeout
        end
    end

    %% CLI Fallback
    alt All HTTP APIs Failed
        EXE->>EXE: ExecuteViaCli()
        EXE->>EXE: PrepareCliArguments()

        EXE->>PROC: Start Process(claude.exe)
        Note over EXE,PROC: Arguments: --command "..." --working-directory "..." --output-format json

        PROC->>CLI: Execute Command
        CLI-->>PROC: Command Output + Exit Code
        PROC-->>EXE: Process Result

        EXE->>EXE: Parse CLI Output
        EXE->>EXE: Create Response
        Note over EXE: Method: "CLI", ExitCode: 0/1

        EXE->>SEM: Release()
        EXE-->>CTRL: AgentExecutionResponse
        CTRL-->>CLIENT: 200 OK with results
    end

    %% Error Handling
    alt Execution Failed
        Note over EXE: Exception caught
        EXE->>EXE: Create Error Response
        Note over EXE: Method: "Failed", Exception details
        EXE->>SEM: Release()
        EXE-->>CTRL: AgentExecutionResponse(Success=false)
        CTRL-->>CLIENT: 500 Internal Server Error
    end
```

### 4. MediatR Command/Query Processing Flow

```mermaid
sequenceDiagram
    participant API as REST API
    participant CTRL as Controller
    participant MED as IMediator
    participant VAL as Validator
    participant HDL as Command Handler
    participant CTX as DbContext
    participant ENT as Entity
    participant EVT as Event Publisher
    participant LOG as ILogger

    API->>CTRL: HTTP Request
    CTRL->>CTRL: Map Request to Command/Query

    CTRL->>MED: Send(command/query)
    MED->>HDL: Route to Handler

    HDL->>LOG: Log Operation Start

    alt Command Validation
        HDL->>VAL: ValidateRequest()
        VAL->>VAL: Check Required Fields
        VAL->>VAL: Validate Business Rules

        alt Validation Failed
            VAL-->>HDL: Validation Errors
            HDL->>LOG: Log Validation Failure
            HDL-->>MED: Result(Success=false, Errors)
            MED-->>CTRL: Failed Result
            CTRL-->>API: 400 Bad Request
        end
    end

    HDL->>CTX: Database Operation

    alt Query Operation
        CTX->>ENT: Find/Query Entities
        ENT-->>CTX: Query Results
        CTX-->>HDL: Entity Data
        HDL->>LOG: Log Query Success
        HDL-->>MED: Query Result
        MED-->>CTRL: Data Response
        CTRL-->>API: 200 OK with Data

    else Command Operation
        CTX->>ENT: Create/Update/Delete
        ENT-->>CTX: Operation Result
        CTX->>CTX: SaveChangesAsync()
        CTX-->>HDL: Changes Saved

        HDL->>EVT: Publish Domain Event
        Note over EVT: AgentRegisteredEvent, AgentUpdatedEvent, etc.

        HDL->>LOG: Log Command Success
        HDL-->>MED: Command Result
        MED-->>CTRL: Success Response
        CTRL-->>API: 200 OK/201 Created
    end

    alt Database Error
        CTX-->>HDL: Exception
        HDL->>LOG: Log Database Error
        HDL-->>MED: Result(Success=false, Error)
        MED-->>CTRL: Failed Result
        CTRL-->>API: 500 Internal Server Error
    end
```

### 5. Event-Driven Architecture Flow

```mermaid
sequenceDiagram
    participant CMD as Command Handler
    participant MED as IMediator
    participant EVT1 as Event Handler 1
    participant EVT2 as Event Handler 2
    participant EVT3 as Event Handler 3
    participant LOG as Logging System
    participant METR as Metrics System
    participant NOTIF as Notification System

    CMD->>MED: Publish(AgentRegisteredEvent)

    Note over MED: Event Bus distributes to all handlers

    par Event Handler Execution
        MED->>EVT1: Handle(AgentRegisteredEvent)
        EVT1->>LOG: Log Agent Registration
        Note over EVT1,LOG: "Agent {AgentId} registered successfully"
        EVT1-->>MED: Handled
    and
        MED->>EVT2: Handle(AgentRegisteredEvent)
        EVT2->>METR: Update Registration Metrics
        Note over EVT2,METR: Increment: total_agents_registered
        EVT2-->>MED: Handled
    and
        MED->>EVT3: Handle(AgentRegisteredEvent)
        EVT3->>NOTIF: Send Registration Notification
        Note over EVT3,NOTIF: Dashboard update, alerts, etc.
        EVT3-->>MED: Handled
    end

    MED-->>CMD: All Handlers Completed

    Note over MED: Similar pattern for AgentUpdatedEvent, AgentStatusChangedEvent
```

### 6. Database Entity Relationships

```mermaid
erDiagram
    Agent ||--o{ TaskRecord : "assigned_to"
    Agent ||--o{ PerformanceMetric : "belongs_to"
    Agent }o--|| Repository : "works_in"

    Agent {
        string Id PK
        string Name
        string Type
        string RepositoryPath
        AgentStatus Status
        datetime LastPing
        string CurrentTask
        string SessionId
        int MaxConcurrentTasks
        string ConfigurationJson
        int TotalTasksCompleted
        int TotalTasksFailed
        timespan TotalExecutionTime
        double AverageExecutionTime
        datetime CreatedAt
        datetime UpdatedAt
        bool IsDeleted
        string RepositoryId FK
    }

    Repository {
        string Id PK
        string Name
        string Path
        RepositoryType Type
        bool IsActive
        timespan TotalExecutionTime
        datetime CreatedAt
        datetime UpdatedAt
        bool IsDeleted
    }

    TaskRecord {
        string Id PK
        string Command
        string WorkingDirectory
        TaskStatus Status
        string AssignedAgentId FK
        datetime CreatedAt
        datetime CompletedAt
        string Result
        string ErrorMessage
    }

    PerformanceMetric {
        string Id PK
        string AgentId FK
        string MetricName
        double Value
        string Unit
        datetime RecordedAt
        string Metadata
    }
```

### 7. Configuration and Dependency Injection Flow

```mermaid
graph TB
    subgraph "Startup Configuration"
        CONF[Configuration System]
        DI[DI Container]
        OPTS[Options Pattern]
    end

    subgraph "Background Services"
        ADS[AgentDiscoveryService]
        AHS[AgentHealthCheckService]
    end

    subgraph "Service Dependencies"
        MED[IMediator]
        CTX[OrchestraDbContext]
        LOG[ILogger]
        HTTP[HttpClient]
    end

    subgraph "Configuration Objects"
        ADSO[AgentDiscoveryOptions]
        AHSO[AgentHealthCheckOptions]
        CCFG[ClaudeCodeConfiguration]
    end

    %% Configuration flow
    CONF --> OPTS
    OPTS --> ADSO
    OPTS --> AHSO
    OPTS --> CCFG

    %% Service registration
    DI --> ADS
    DI --> AHS
    DI --> MED
    DI --> CTX
    DI --> LOG
    DI --> HTTP

    %% Dependency injection
    ADSO --> ADS
    AHSO --> AHS
    CCFG --> EXE[ClaudeCodeExecutor]

    MED --> ADS
    MED --> AHS
    CTX --> MED
    LOG --> ADS
    LOG --> AHS
    HTTP --> ADS

    style CONF fill:#e3f2fd
    style DI fill:#f3e5f5
    style OPTS fill:#e8f5e8
```

## Cross-Service Communication Patterns

### 1. Service-to-Service Communication

```mermaid
graph LR
    subgraph "Background Services"
        ADS[AgentDiscoveryService]
        AHS[AgentHealthCheckService]
    end

    subgraph "MediatR Bus"
        MED[IMediator]
    end

    subgraph "Command Handlers"
        REG[RegisterAgentCommandHandler]
        UPD[UpdateAgentStatusCommandHandler]
    end

    subgraph "Query Handlers"
        GET[GetAgentByIdQueryHandler]
        ALL[GetAllAgentsQueryHandler]
    end

    ADS -->|RegisterAgentCommand| MED
    AHS -->|UpdateAgentStatusCommand| MED
    AHS -->|GetAllAgentsQuery| MED

    MED --> REG
    MED --> UPD
    MED --> GET
    MED --> ALL

    style MED fill:#fff3e0
    style ADS fill:#e8f5e8
    style AHS fill:#e8f5e8
```

### 2. Error Propagation Flow

```mermaid
sequenceDiagram
    participant SVC as Service Layer
    participant HDL as Handler
    participant CTX as DbContext
    participant LOG as Logger
    participant API as API Response

    SVC->>HDL: Execute Operation

    alt Database Error
        HDL->>CTX: Database Operation
        CTX-->>HDL: SqlException
        HDL->>LOG: LogError(exception, context)
        HDL-->>SVC: Result(Success=false, ErrorMessage)
        SVC-->>API: 500 Internal Server Error

    else Validation Error
        HDL->>HDL: Validate Input
        HDL->>LOG: LogWarning(validation errors)
        HDL-->>SVC: Result(Success=false, ValidationErrors)
        SVC-->>API: 400 Bad Request

    else Business Logic Error
        HDL->>HDL: Execute Business Logic
        HDL->>LOG: LogWarning(business rule violation)
        HDL-->>SVC: Result(Success=false, BusinessError)
        SVC-->>API: 422 Unprocessable Entity

    else Success
        HDL->>CTX: Successful Operation
        HDL->>LOG: LogInformation(success)
        HDL-->>SVC: Result(Success=true, Data)
        SVC-->>API: 200 OK
    end
```

### 3. Concurrency Control Flow

```mermaid
sequenceDiagram
    participant REQ1 as Request 1
    participant REQ2 as Request 2
    participant REQ3 as Request 3
    participant REQ4 as Request 4
    participant SEM as SemaphoreSlim(3,3)
    participant EXE as ClaudeCodeExecutor

    par Concurrent Requests
        REQ1->>SEM: WaitAsync()
        REQ2->>SEM: WaitAsync()
        REQ3->>SEM: WaitAsync()
        REQ4->>SEM: WaitAsync()
    end

    SEM-->>REQ1: Acquired (1/3)
    SEM-->>REQ2: Acquired (2/3)
    SEM-->>REQ3: Acquired (3/3)
    Note over REQ4,SEM: REQ4 waits - semaphore full

    par Execution
        REQ1->>EXE: Execute Command
        REQ2->>EXE: Execute Command
        REQ3->>EXE: Execute Command
    end

    REQ1->>SEM: Release()
    Note over REQ4,SEM: REQ4 can now acquire
    SEM-->>REQ4: Acquired (3/3)
    REQ4->>EXE: Execute Command

    par Completion
        REQ2->>SEM: Release()
        REQ3->>SEM: Release()
        REQ4->>SEM: Release()
    end
```

## Performance and Monitoring

### 1. Performance Metrics Collection

```mermaid
graph TB
    subgraph "Performance Sources"
        EXE[Command Execution]
        ADS[Agent Discovery]
        AHS[Health Checks]
        API[API Endpoints]
    end

    subgraph "Metrics Collection"
        TIMER[Execution Timers]
        COUNT[Operation Counters]
        MEM[Memory Usage]
        DB[Database Metrics]
    end

    subgraph "Storage & Analysis"
        PM[PerformanceMetric Entity]
        LOG[Structured Logs]
        DASH[Dashboard Metrics]
    end

    EXE --> TIMER
    ADS --> COUNT
    AHS --> COUNT
    API --> TIMER

    TIMER --> PM
    COUNT --> PM
    MEM --> LOG
    DB --> LOG

    PM --> DASH
    LOG --> DASH

    style TIMER fill:#e8f5e8
    style COUNT fill:#e8f5e8
    style PM fill:#ffebee
```

### 2. Health Check Monitoring

```mermaid
stateDiagram-v2
    [*] --> Discovering: Service Start

    Discovering --> Healthy: Agents Found
    Discovering --> NoAgents: No Agents Found

    Healthy --> Monitoring: Health Check Active
    NoAgents --> Discovering: Retry Discovery

    Monitoring --> Healthy: All Agents Responding
    Monitoring --> Degraded: Some Agents Offline
    Monitoring --> Failed: All Agents Offline

    Degraded --> Healthy: Agents Recovered
    Degraded --> Failed: More Agents Failed

    Failed --> Degraded: Some Agents Recovered
    Failed --> Discovering: System Reset

    Healthy --> [*]: Service Stop
    Degraded --> [*]: Service Stop
    Failed --> [*]: Service Stop
```

---

## Summary

This document provides comprehensive interaction diagrams for all major components in the Phase 2 agent infrastructure. The diagrams show:

1. **System-level architecture** with all major components and their relationships
2. **Detailed sequence flows** for each major operation (discovery, health checks, command execution)
3. **MediatR CQRS patterns** showing command/query/event flows
4. **Database relationships** and entity interactions
5. **Configuration and dependency injection** patterns
6. **Error handling and concurrency control** mechanisms
7. **Performance monitoring** and health check flows

All diagrams reflect the actual implemented architecture and can be used for:
- **Developer onboarding** and system understanding
- **Architecture reviews** and design decisions
- **Troubleshooting** and debugging complex flows
- **Performance analysis** and optimization planning
- **Documentation** for stakeholders and maintenance teams

The implementation perfectly matches the planned architecture with enhancements in error handling, performance monitoring, and operational resilience.