# Phase 4: MediatR Patterns Documentation

**Parent Plan**: [00-API-DOCUMENTATION-PLAN.md](../00-API-DOCUMENTATION-PLAN.md)

**Estimated Time**: 1-1.5 hours
**Dependencies**: Phase 1 analysis, access to Orchestra.Core

## Objectives
- Document CQRS pattern implementation
- Catalog all commands and queries
- Explain pipeline behaviors
- Provide internal development guide

## Task 4.1: CQRS Pattern Documentation (45 minutes)

### 4.1A: Create MEDIATR-PATTERNS.md
**Technical Documentation Structure**:
```markdown
# MediatR CQRS Implementation Guide

## Overview
- Command/Query separation principle
- Request/Response patterns
- Pipeline behaviors
- Domain event handling

## Architecture
- IRequest<T> hierarchy
- IRequestHandler<TRequest, TResponse>
- INotification for events
- IPipelineBehavior for cross-cutting concerns
```

**Core Pattern Examples**:
```csharp
// Command Pattern (changes state)
public record CreateTaskCommand(
    string Name,
    string Description,
    int Priority,
    string? AssignedAgentId) : IRequest<TaskDto>
{
    // Validation attributes
    public class Validator : AbstractValidator<CreateTaskCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(1000);
            RuleFor(x => x.Priority).InclusiveBetween(1, 10);
        }
    }
}

// Query Pattern (reads state)
public record GetTaskByIdQuery(Guid TaskId) : IRequest<TaskDto>
{
    public class Validator : AbstractValidator<GetTaskByIdQuery>
    {
        public Validator()
        {
            RuleFor(x => x.TaskId).NotEmpty();
        }
    }
}

// Handler Implementation
public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, TaskDto>
{
    private readonly ITaskRepository _repository;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateTaskCommandHandler> _logger;

    public CreateTaskCommandHandler(
        ITaskRepository repository,
        IMediator mediator,
        ILogger<CreateTaskCommandHandler> logger)
    {
        _repository = repository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<TaskDto> Handle(
        CreateTaskCommand request,
        CancellationToken cancellationToken)
    {
        // Create entity
        var task = new TaskEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Priority = request.Priority,
            AssignedAgentId = request.AssignedAgentId,
            Status = TaskStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };

        // Persist
        await _repository.AddAsync(task, cancellationToken);

        // Publish domain event
        await _mediator.Publish(
            new TaskCreatedEvent(task.Id, task.Name),
            cancellationToken);

        // Return DTO
        return task.ToDto();
    }
}
```

### 4.1B: Document Pipeline Behaviors
**Pipeline Behavior Implementations**:
```csharp
// Validation Behavior
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count != 0)
                throw new ValidationException(failures);
        }

        return await next();
    }
}

// Logging Behavior
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestGuid = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Executing command {RequestName} ({RequestGuid})",
            requestName, requestGuid);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            stopwatch.Stop();

            _logger.LogInformation(
                "Command {RequestName} ({RequestGuid}) executed in {ElapsedMilliseconds}ms",
                requestName, requestGuid, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "Command {RequestName} ({RequestGuid}) failed after {ElapsedMilliseconds}ms",
                requestName, requestGuid, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
```

**Integration Requirements**:
- [ ] Document all pipeline behaviors in use
- [ ] Explain execution order of behaviors
- [ ] Document DI registration for behaviors
- [ ] Include error handling patterns
- [ ] Document transaction behavior if present

## Task 4.2: Command/Query Catalog (45 minutes)

### 4.2A: Document All Commands
**Command Catalog Structure**:
```markdown
## Task Management Commands

### CreateTaskCommand
- **Purpose**: Create new task in orchestration queue
- **Parameters**:
  - Name (string, required)
  - Description (string, optional)
  - Priority (int, 1-10)
  - AssignedAgentId (string, optional)
- **Response**: TaskDto
- **Validation**:
  - Name: Required, max 200 chars
  - Priority: Between 1-10
- **Events Published**: TaskCreatedEvent
- **API Endpoint**: POST /api/tasks

### UpdateTaskCommand
- **Purpose**: Update existing task
- **Parameters**:
  - TaskId (Guid, required)
  - Updates (TaskUpdateDto)
- **Response**: TaskDto
- **Validation**:
  - TaskId must exist
  - Status transitions validated
- **Events Published**: TaskUpdatedEvent
- **API Endpoint**: PUT /api/tasks/{id}

### AssignTaskCommand
- **Purpose**: Assign task to specific agent
- **Parameters**:
  - TaskId (Guid)
  - AgentId (string)
- **Response**: TaskDto
- **Validation**:
  - Task must be unassigned
  - Agent must be available
- **Events Published**: TaskAssignedEvent
- **API Endpoint**: POST /api/tasks/{id}/assign
```

**Integration Requirements**:
- [ ] List all commands with full signatures
- [ ] Document validation rules for each
- [ ] Map to corresponding API endpoints
- [ ] Document published events
- [ ] Include authorization requirements

### 4.2B: Document All Queries
**Query Catalog Structure**:
```markdown
## Task Queries

### GetTaskByIdQuery
- **Purpose**: Retrieve single task by ID
- **Parameters**:
  - TaskId (Guid)
- **Response**: TaskDto
- **Caching**: 5 minutes
- **Authorization**: Read permission
- **API Endpoint**: GET /api/tasks/{id}

### ListTasksQuery
- **Purpose**: List tasks with filtering
- **Parameters**:
  - PageNumber (int, default: 1)
  - PageSize (int, default: 20, max: 100)
  - Status (TaskStatus[], optional)
  - AssignedAgentId (string, optional)
  - Priority (int[], optional)
  - DateFrom (DateTime?, optional)
  - DateTo (DateTime?, optional)
  - SortBy (string, default: "CreatedAt")
  - SortDescending (bool, default: false)
- **Response**: PagedResponse<TaskDto>
- **Performance**: Indexed on Status, AssignedAgentId
- **API Endpoint**: GET /api/tasks

### GetTaskMetricsQuery
- **Purpose**: Get task execution metrics
- **Parameters**:
  - TimeRange (TimeRange enum)
  - GroupBy (MetricGrouping enum)
- **Response**: TaskMetricsDto
- **Caching**: 1 minute for real-time, 1 hour for historical
- **API Endpoint**: GET /api/tasks/metrics
```

**Integration Requirements**:
- [ ] Document all query parameters
- [ ] Include performance considerations
- [ ] Document caching strategies
- [ ] Note database indexes used
- [ ] Include pagination patterns

## Task 4.3: Domain Events Documentation (30 minutes)

### 4.3A: Document Event Patterns
**Event Implementation**:
```csharp
// Base domain event
public abstract record DomainEvent : INotification
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? UserId { get; init; }
}

// Specific events
public record TaskCreatedEvent(
    Guid TaskId,
    string TaskName,
    string? AssignedAgentId = null) : DomainEvent;

public record TaskStatusChangedEvent(
    Guid TaskId,
    TaskStatus OldStatus,
    TaskStatus NewStatus,
    string? Reason = null) : DomainEvent;

public record TaskCompletedEvent(
    Guid TaskId,
    TimeSpan ExecutionTime,
    bool Success,
    string? ErrorMessage = null) : DomainEvent;

// Event handler
public class TaskEventHandler :
    INotificationHandler<TaskCreatedEvent>,
    INotificationHandler<TaskStatusChangedEvent>,
    INotificationHandler<TaskCompletedEvent>
{
    private readonly IHubContext<CoordinatorChatHub> _hubContext;
    private readonly IMetricsService _metrics;

    public async Task Handle(
        TaskCreatedEvent notification,
        CancellationToken cancellationToken)
    {
        // Update metrics
        _metrics.IncrementTasksCreated();

        // Notify clients via SignalR
        await _hubContext.Clients.All.SendAsync(
            "TaskCreated",
            new { notification.TaskId, notification.TaskName },
            cancellationToken);
    }

    // Additional handlers...
}
```

**Integration Requirements**:
- [ ] Document all domain events
- [ ] Map events to their publishers
- [ ] List all event handlers
- [ ] Document SignalR notifications triggered
- [ ] Include event sourcing patterns if used

### 4.3B: DI Registration Documentation
**Startup Configuration**:
```csharp
// In Program.cs or Startup.cs
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateTaskCommand).Assembly);

    // Register behaviors in order of execution
    cfg.AddBehavior<IPipelineBehavior<,>, LoggingBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, ValidationBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, TransactionBehavior<,>>();
});

// Register validators
services.AddValidatorsFromAssembly(typeof(CreateTaskCommand).Assembly);

// Register repositories and services
services.AddScoped<ITaskRepository, TaskRepository>();
services.AddScoped<IAgentRepository, AgentRepository>();
services.AddScoped<IMetricsService, MetricsService>();
```

## Deliverables

### Documentation Files
- [ ] `Docs/API/MEDIATR-PATTERNS.md` - Pattern implementation guide
- [ ] `Docs/API/COMMAND-CATALOG.md` - All commands documented
- [ ] `Docs/API/QUERY-CATALOG.md` - All queries documented
- [ ] `Docs/API/EVENT-CATALOG.md` - Domain events reference

### Code Templates
- [ ] `Templates/CommandTemplate.cs` - Command boilerplate
- [ ] `Templates/QueryTemplate.cs` - Query boilerplate
- [ ] `Templates/HandlerTemplate.cs` - Handler boilerplate
- [ ] `Templates/EventTemplate.cs` - Event boilerplate

### Developer Guides
- [ ] How to add new command/query
- [ ] Testing MediatR handlers
- [ ] Debugging pipeline behaviors
- [ ] Performance optimization tips

## Success Criteria
- [ ] All commands cataloged with examples
- [ ] All queries documented with parameters
- [ ] Pipeline behaviors explained
- [ ] Event flow documented
- [ ] DI registration complete

## Validation Checklist
- [ ] Every command has validation rules
- [ ] Every query has performance notes
- [ ] All events have handlers documented
- [ ] Pipeline execution order verified
- [ ] Templates compile successfully