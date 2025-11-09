# API Documentation Plan - Architecture Diagram

## High-Level Documentation Architecture

```mermaid
graph TB
    subgraph "Source Code Analysis"
        SC[Source Code] --> Controllers[REST Controllers]
        SC --> Hubs[SignalR Hubs]
        SC --> Handlers[MediatR Handlers]

        Controllers --> TaskCtrl[TaskController]
        Controllers --> AgentCtrl[AgentsController]
        Controllers --> OrchCtrl[OrchestratorController]

        Hubs --> ChatHub[CoordinatorChatHub]
        Hubs --> InteractHub[AgentInteractionHub]
        Hubs --> CommHub[AgentCommunicationHub]

        Handlers --> Commands[Command Handlers]
        Handlers --> Queries[Query Handlers]
    end

    subgraph "Documentation Generation"
        TaskCtrl --> APIRef[API Reference]
        AgentCtrl --> APIRef
        OrchCtrl --> APIRef

        ChatHub --> SignalRRef[SignalR Reference]
        InteractHub --> SignalRRef
        CommHub --> SignalRRef

        Commands --> MediatRRef[MediatR Reference]
        Queries --> MediatRRef
    end

    subgraph "Documentation Outputs"
        APIRef --> Overview[API Overview]
        APIRef --> RestDocs[REST API Docs]
        SignalRRef --> HubDocs[Hub Documentation]
        MediatRRef --> PatternDocs[Pattern Documentation]

        Overview --> IntGuide[Integration Guide]
        RestDocs --> IntGuide
        HubDocs --> IntGuide
        PatternDocs --> IntGuide
    end

    subgraph "Integration Artifacts"
        IntGuide --> Postman[Postman Collection]
        IntGuide --> OpenAPI[OpenAPI Spec]
        IntGuide --> Examples[Code Examples]
        IntGuide --> SDK[Client SDKs]
    end
```

## Documentation Flow and Dependencies

```mermaid
flowchart LR
    subgraph "Phase 1: Analysis"
        A1[Controller Analysis] --> A2[Hub Analysis]
        A2 --> A3[MediatR Analysis]
        A3 --> A4[Structure Planning]
    end

    subgraph "Phase 2: REST API"
        A4 --> R1[API Overview]
        R1 --> R2[Endpoint Docs]
        R2 --> R3[Examples]
    end

    subgraph "Phase 3: SignalR"
        R3 --> S1[SignalR Overview]
        S1 --> S2[Hub Methods]
        S2 --> S3[Client Examples]
    end

    subgraph "Phase 4: MediatR"
        S3 --> M1[Pattern Docs]
        M1 --> M2[Command Catalog]
        M2 --> M3[Query Catalog]
    end

    subgraph "Phase 5: Integration"
        M3 --> I1[Scenarios]
        I1 --> I2[SDK Examples]
        I2 --> I3[Testing]
    end

    subgraph "Phase 6: Enhancement"
        I3 --> E1[XML Comments]
        E1 --> E2[Swagger Config]
        E2 --> E3[Validation]
    end
```

## Component Interaction Documentation

```mermaid
sequenceDiagram
    participant Client
    participant API as REST API
    participant Hub as SignalR Hub
    participant MediatR
    participant Handler
    participant DB as Database

    Note over Client,DB: Task Creation Flow (REST + SignalR)

    Client->>API: POST /api/tasks
    API->>MediatR: Send(CreateTaskCommand)
    MediatR->>Handler: Handle(command)
    Handler->>DB: Insert Task
    Handler-->>MediatR: TaskDto
    MediatR-->>API: TaskDto
    API-->>Client: 201 Created

    Note over Hub,Client: Real-time Update
    Handler->>Hub: Broadcast TaskCreated
    Hub-->>Client: OnTaskCreated event
```

## Documentation Structure

```mermaid
graph TD
    subgraph "Docs/API/"
        Root[API Documentation Root]

        Root --> Overview[API-OVERVIEW.md]
        Root --> Reference[API-REFERENCE.md]
        Root --> SignalR[SIGNALR-REFERENCE.md]
        Root --> MediatR[MEDIATR-REFERENCE.md]
        Root --> Integration[INTEGRATION-GUIDE.md]

        Root --> Examples[Examples/]
        Examples --> CSharp[CSharp/]
        Examples --> JS[JavaScript/]
        Examples --> Python[Python/]
        Examples --> Curl[Curl/]

        Root --> Postman[Postman/]
        Postman --> Collection[orchestra-api.postman_collection.json]
        Postman --> Env[orchestra-api.postman_environment.json]

        Root --> OpenAPI[OpenAPI/]
        OpenAPI --> Spec[openapi.json]
        OpenAPI --> Schema[schemas/]
    end
```

## API Endpoint Matrix

```mermaid
graph LR
    subgraph "TaskController"
        T1[GET /api/tasks]
        T2[GET /api/tasks/{id}]
        T3[POST /api/tasks]
        T4[PUT /api/tasks/{id}]
        T5[DELETE /api/tasks/{id}]
        T6[POST /api/tasks/batch]
    end

    subgraph "AgentsController"
        A1[GET /api/agents]
        A2[GET /api/agents/{id}]
        A3[POST /api/agents]
        A4[PUT /api/agents/{id}]
        A5[DELETE /api/agents/{id}]
        A6[GET /api/agents/{id}/tasks]
    end

    subgraph "OrchestratorController"
        O1[GET /orchestrator/status]
        O2[POST /orchestrator/start]
        O3[POST /orchestrator/stop]
        O4[GET /orchestrator/metrics]
    end
```

## SignalR Hub Methods

```mermaid
graph TD
    subgraph "CoordinatorChatHub"
        CH1[SendMessage]
        CH2[JoinGroup]
        CH3[LeaveGroup]
        CH4[OnConnectedAsync]
        CH5[OnDisconnectedAsync]
    end

    subgraph "AgentInteractionHub"
        AI1[RegisterAgent]
        AI2[SendCommand]
        AI3[ReceiveResponse]
        AI4[UpdateStatus]
        AI5[StreamOutput]
    end

    subgraph "AgentCommunicationHub"
        AC1[BroadcastMessage]
        AC2[SendToAgent]
        AC3[RequestTask]
        AC4[ReportProgress]
        AC5[CompleteTask]
    end
```

## MediatR Command/Query Flow

```mermaid
flowchart TB
    subgraph "Commands"
        CreateTask[CreateTaskCommand]
        UpdateTask[UpdateTaskCommand]
        DeleteTask[DeleteTaskCommand]
        AssignTask[AssignTaskCommand]
        CompleteTask[CompleteTaskCommand]
    end

    subgraph "Queries"
        GetTask[GetTaskByIdQuery]
        ListTasks[ListTasksQuery]
        GetAgent[GetAgentByIdQuery]
        ListAgents[ListAgentsQuery]
        GetMetrics[GetMetricsQuery]
    end

    subgraph "Handlers"
        CreateTask --> CTH[CreateTaskHandler]
        UpdateTask --> UTH[UpdateTaskHandler]
        DeleteTask --> DTH[DeleteTaskHandler]

        GetTask --> GTH[GetTaskHandler]
        ListTasks --> LTH[ListTasksHandler]
    end

    subgraph "Events"
        CTH --> TaskCreatedEvent
        UTH --> TaskUpdatedEvent
        DTH --> TaskDeletedEvent
    end
```

## Documentation Generation Pipeline

```mermaid
flowchart LR
    subgraph "Input"
        Code[Source Code]
        XML[XML Comments]
        Attributes[Data Annotations]
    end

    subgraph "Processing"
        Code --> Analyzer[Code Analyzer]
        XML --> Parser[XML Parser]
        Attributes --> Extractor[Attribute Extractor]

        Analyzer --> Generator[Doc Generator]
        Parser --> Generator
        Extractor --> Generator
    end

    subgraph "Output"
        Generator --> Markdown[Markdown Files]
        Generator --> OpenAPIGen[OpenAPI Spec]
        Generator --> PostmanGen[Postman Collection]

        Markdown --> Validation[Validation]
        OpenAPIGen --> Validation
        PostmanGen --> Validation
    end
```

## Integration Testing Flow

```mermaid
sequenceDiagram
    participant Test as Test Suite
    participant Postman
    participant API
    participant SignalR
    participant DB

    Test->>Postman: Load Collection
    Postman->>API: Execute Requests
    API->>DB: CRUD Operations
    API-->>Postman: Responses

    Test->>SignalR: Connect Client
    SignalR->>Test: Connection Established
    Test->>SignalR: Subscribe Events
    API->>SignalR: Broadcast Changes
    SignalR-->>Test: Event Received

    Test->>Test: Validate Results
```

## Dependencies Between Documentation Sections

```mermaid
graph TD
    Overview[API Overview] --> Authentication[Authentication Section]
    Overview --> ErrorHandling[Error Handling]
    Overview --> RateLimiting[Rate Limiting]

    Authentication --> RestAPI[REST API Docs]
    Authentication --> SignalRDocs[SignalR Docs]

    RestAPI --> Controllers[Controller Docs]
    Controllers --> Endpoints[Endpoint Details]
    Endpoints --> Examples[Request/Response Examples]

    SignalRDocs --> Hubs[Hub Documentation]
    Hubs --> Methods[Method Details]
    Methods --> ClientExamples[Client Examples]

    MediatR[MediatR Patterns] --> Commands[Command Docs]
    MediatR --> Queries[Query Docs]
    Commands --> Handlers[Handler Patterns]

    Integration[Integration Guide] --> Scenarios[Use Cases]
    Scenarios --> CodeExamples[Code Samples]
    CodeExamples --> SDKs[Client SDKs]
```

## Documentation Quality Metrics

```mermaid
graph LR
    subgraph "Coverage Metrics"
        EP[Endpoints Documented: 100%]
        HM[Hub Methods Documented: 100%]
        CMD[Commands Documented: 100%]
        QRY[Queries Documented: 100%]
    end

    subgraph "Example Metrics"
        CE[Code Examples: 20+]
        PC[Postman Requests: 30+]
        SC[Scenarios Covered: 5+]
        Lang[Languages: 4+]
    end

    subgraph "Quality Metrics"
        XML[XML Comments: 100%]
        SW[Swagger Enhanced: Yes]
        VAL[Examples Validated: 100%]
        REV[Peer Reviewed: Yes]
    end
```