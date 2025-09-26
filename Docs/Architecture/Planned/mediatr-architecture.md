# MediatR CQRS Architecture - Planned Design

**Created**: 2025-09-26
**Status**: ✅ IMPLEMENTED (see [mediatr-implementation.md](../Actual/mediatr-implementation.md))
**Planning Phase**: Pre-implementation design document

## Architectural Vision

The planned MediatR integration aimed to transform AI Agent Orchestra from direct service calls to a Command Query Responsibility Segregation (CQRS) pattern for improved LLM-friendly development.

## Original Requirements

### Primary Goals
1. **LLM-Friendly Patterns**: Predictable, consistent operation patterns
2. **Separation of Concerns**: Clear boundaries between read/write operations
3. **Event-Driven Architecture**: Loose coupling through domain events
4. **Framework-First Approach**: Extensible foundation for multiple AI agent types
5. **Clean Architecture**: Proper layering and dependency management

### Technical Requirements
- MediatR library integration for .NET CQRS implementation
- Command/Query/Event pattern with proper handlers
- Domain event publishing for component communication
- Compile-time type safety for all operations
- Dependency injection integration
- API controller integration replacing direct service calls

## Planned Architecture Components

### 1. Core Pattern Structure

**Commands (Write Operations)**:
```csharp
// Planned naming convention
public record {Action}{Entity}Command(...) : ICommand<TResult>;
public class {Action}{Entity}CommandHandler : IRequestHandler<TCommand, TResult>;
```

**Queries (Read Operations)**:
```csharp
// Planned naming convention
public record {Action}{Entity}Query(...) : IQuery<TResult>;
public class {Action}{Entity}QueryHandler : IRequestHandler<TQuery, TResult>;
```

**Events (Domain Notifications)**:
```csharp
// Planned naming convention
public record {Entity}{Action}Event(...) : IEvent;
public class {Entity}{Action}EventHandler : INotificationHandler<TEvent>;
```

### 2. Planned Task Domain Operations

**Task Management Commands**:
- `CreateTaskCommand` - Queue new tasks for execution
- `UpdateTaskStatusCommand` - Modify task execution status
- `AssignTaskCommand` - Assign tasks to specific agents
- `CancelTaskCommand` - Cancel pending or running tasks

**Task Query Operations**:
- `GetTaskByIdQuery` - Retrieve specific task details
- `GetTaskQueueQuery` - Get all queued tasks
- `GetTasksByAgentQuery` - Get tasks assigned to specific agent
- `GetNextTaskForAgentQuery` - Priority-based task assignment

**Task Domain Events**:
- `TaskCreatedEvent` - New task added to queue
- `TaskAssignedEvent` - Task assigned to agent
- `TaskStartedEvent` - Agent began task execution
- `TaskCompletedEvent` - Task finished successfully
- `TaskFailedEvent` - Task execution failed

### 3. Agent Management Operations

**Agent Commands**:
- `RegisterAgentCommand` - Add new agent to system
- `UpdateAgentStatusCommand` - Update agent availability
- `ConfigureAgentCommand` - Modify agent settings
- `DeactivateAgentCommand` - Remove agent from rotation

**Agent Queries**:
- `GetAvailableAgentsQuery` - Find agents ready for work
- `GetAgentStatusQuery` - Check specific agent status
- `GetAgentPerformanceQuery` - Retrieve agent metrics

**Agent Events**:
- `AgentRegisteredEvent` - New agent joined system
- `AgentStatusChangedEvent` - Agent availability updated
- `AgentPerformanceUpdatedEvent` - Metrics refreshed

### 4. Repository Operations

**Repository Commands**:
- `ScanRepositoryCommand` - Discover repository structure
- `UpdateRepositoryInfoCommand` - Refresh repository metadata

**Repository Queries**:
- `GetRepositoryInfoQuery` - Retrieve repository details
- `GetRepositoriesQuery` - List all managed repositories

## Implementation Strategy

### Phase 1: Foundation
1. Install MediatR NuGet packages
2. Configure dependency injection
3. Create base interfaces (ICommand, IQuery, IEvent)
4. Establish naming conventions

### Phase 2: Task Domain
1. Implement core task commands/queries
2. Create task domain events
3. Build command/query handlers
4. Replace direct TaskRepository calls

### Phase 3: API Integration
1. Update controllers to use IMediator
2. Remove direct service dependencies
3. Maintain API contract compatibility
4. Add proper error handling

### Phase 4: Event Infrastructure
1. Implement event handlers for logging
2. Set up real-time notifications via SignalR
3. Add background job integration
4. Create audit trail system

## Expected Benefits

### For LLM Development
- **Predictable Patterns**: Every operation follows same Command/Query/Event flow
- **Clear Contracts**: Explicit interfaces for all business operations
- **Easy Discovery**: Consistent naming makes operations findable
- **Type Safety**: Compile-time validation of all requests/responses

### For Architecture Quality
- **Separation of Concerns**: Clear boundaries between different operation types
- **Testability**: Each handler unit testable in isolation
- **Extensibility**: New operations require only new Command/Handler pairs
- **Maintainability**: Centralized business logic through handlers

### For System Integration
- **Event-Driven**: Loose coupling between components via domain events
- **Performance**: Async operations with proper cancellation support
- **Monitoring**: Centralized place for cross-cutting concerns
- **Scalability**: Handler pattern supports distributed processing

## Risk Mitigation

### Technical Risks
- **Learning Curve**: Team unfamiliarity with CQRS patterns
  - *Mitigation*: Clear documentation and examples
- **Over-Engineering**: Too many abstractions for simple operations
  - *Mitigation*: Start simple, add complexity as needed
- **Performance Overhead**: Additional layer might slow operations
  - *Mitigation*: MediatR has minimal overhead, measure before optimizing

### Implementation Risks
- **Breaking Changes**: API changes during refactoring
  - *Mitigation*: Maintain backward compatibility during transition
- **Incomplete Migration**: Some operations remain as direct calls
  - *Mitigation*: Complete systematic migration of all operations
- **Event Ordering**: Event handlers might execute in wrong order
  - *Mitigation*: Design events to be order-independent where possible

## Success Metrics

### Implementation Completeness
- [ ] All business operations use Command/Query pattern
- [ ] No direct service calls in controllers
- [ ] All handlers properly registered via DI
- [ ] Domain events published for all state changes

### Code Quality Improvements
- [ ] Reduced coupling between controllers and services
- [ ] Improved testability of business logic
- [ ] Consistent error handling across operations
- [ ] Clear separation of read/write operations

### LLM Development Experience
- [ ] Predictable patterns for all new operations
- [ ] Easy to discover existing commands/queries
- [ ] Clear documentation of all operation contracts
- [ ] Simplified testing of business logic

## Integration Points

### Existing Systems
- **TaskRepository**: Becomes data access layer only
- **Controllers**: Use IMediator instead of direct service calls
- **SignalR Hubs**: Subscribe to domain events for real-time updates
- **Background Jobs**: Triggered by command handlers

### Future Enhancements
- **Validation Pipeline**: Add FluentValidation for request validation
- **Caching**: Add response caching for query handlers
- **Metrics**: Add performance monitoring for all handlers
- **Audit Trail**: Log all commands for compliance

## Architecture Decision Rationale

### Why MediatR?
- Industry-standard CQRS implementation for .NET
- Minimal performance overhead
- Excellent dependency injection integration
- Strong community support and documentation

### Why CQRS Pattern?
- Natural fit for AI agent orchestration (commands vs queries)
- Enables different optimization strategies for reads vs writes
- Supports event-driven architecture for loose coupling
- Improves testability through isolated handlers

### Why Event-Driven?
- AI agent operations need loose coupling
- Multiple components need to react to state changes
- Enables real-time updates via SignalR
- Supports audit trails and monitoring

**Planning Status**: ✅ COMPLETE - All planned components successfully implemented