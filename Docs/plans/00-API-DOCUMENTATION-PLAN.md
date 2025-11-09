# API Documentation Generation Work Plan

**Plan Type**: Documentation Enhancement
**Estimated Effort**: 6-10 hours (1.5-2.5 days)
**Priority**: High - Critical for external integrations
**Architecture Documentation**: [00-API-DOCUMENTATION-PLAN-Architecture.md](./00-API-DOCUMENTATION-PLAN-Architecture.md)

## Executive Summary

Comprehensive API documentation generation for AI Agent Orchestra, covering REST API endpoints, SignalR hubs, MediatR patterns, and integration examples. The documentation will serve as the primary reference for developers integrating with the orchestration platform.

## Context and Requirements

### Current State
The AI Agent Orchestra project has:
1. **REST API Controllers** (MediatR-based):
   - TaskController (`/api/tasks`) - Task management
   - AgentsController (`/api/agents`) - Agent management
   - OrchestratorController (root endpoints) - Legacy orchestration

2. **SignalR Hubs**:
   - CoordinatorChatHub (`/coordinatorHub`) - Interactive chat
   - AgentInteractionHub (`/hubs/agent-interaction`) - Real-time agent interaction
   - AgentCommunicationHub (`/agentHub`) - Agent communication

3. **Technology Stack**:
   - ASP.NET Core with OpenAPI/Swagger
   - MediatR 11.1.0 for CQRS
   - SignalR for real-time communication
   - Entity Framework Core with SQLite/PostgreSQL

### Documentation Requirements
- **Overview Document** - Architecture, authentication, error handling
- **REST API Reference** - All endpoints with examples
- **SignalR Hub Reference** - Hub methods with connection examples
- **MediatR Commands/Queries Reference** - Internal patterns
- **Integration Examples** - Common scenarios with code samples
- **Enhanced Swagger Configuration** - XML comments and annotations

## Success Criteria
- [ ] All API endpoints documented with request/response examples
- [ ] SignalR hub methods documented with JavaScript client examples
- [ ] MediatR patterns documented for internal development
- [ ] Postman collection generated for testing
- [ ] Swagger enhanced with detailed descriptions
- [ ] Integration guide covers 5+ common scenarios

## Phase 1: Analysis and Preparation (1-2 hours)

### 1.1 API Inventory and Analysis
**Estimated Time**: 30-45 minutes

#### 1.1A: Analyze REST API Controllers
- [ ] Read TaskController.cs and extract all endpoints
- [ ] Read AgentsController.cs and extract all endpoints
- [ ] Read OrchestratorController.cs and identify legacy patterns
- [ ] Document HTTP methods, routes, and parameters

#### 1.1B: Analyze SignalR Hubs
- [ ] Read CoordinatorChatHub.cs and extract hub methods
- [ ] Read AgentInteractionHub.cs and extract hub methods
- [ ] Read AgentCommunicationHub.cs and extract hub methods
- [ ] Document hub endpoints and message formats

#### 1.1C: Analyze MediatR Patterns
- [ ] Identify all Command classes in Orchestra.Core
- [ ] Identify all Query classes in Orchestra.Core
- [ ] Document request/response patterns
- [ ] Extract validation rules from handlers

### 1.2 Documentation Structure Planning
**Estimated Time**: 30-45 minutes

#### 1.2A: Create Documentation Hierarchy
- [ ] Create Docs/API/ directory structure
- [ ] Define document templates for consistency
- [ ] Plan cross-referencing strategy
- [ ] Setup version control for API docs

#### 1.2B: Prepare Code Examples Repository
- [ ] Create Examples/ subdirectory
- [ ] Prepare C# client examples structure
- [ ] Prepare JavaScript/TypeScript examples structure
- [ ] Prepare curl/Postman examples structure

## Phase 2: REST API Documentation (2-3 hours)

### 2.1 API Overview Document
**Estimated Time**: 45-60 minutes

#### 2.1A: Create API-OVERVIEW.md
```markdown
# AI Agent Orchestra API Overview

## Base URL
- Development: http://localhost:5000
- Production: https://api.orchestrator.example.com

## Authentication
[Authentication details from Startup.cs]

## Error Handling
[Standard error response format]

## Rate Limiting
[Rate limiting configuration]

## Versioning Strategy
[API versioning approach]
```

#### 2.1B: Document Common Patterns
- [ ] Request/Response formats
- [ ] Pagination patterns
- [ ] Filtering and sorting
- [ ] Error response structure

### 2.2 REST API Reference
**Estimated Time**: 75-90 minutes

#### 2.2A: Document TaskController Endpoints
```markdown
## Task Management API

### Create Task
POST /api/tasks
Creates a new task in the orchestration queue

Request:
{
  "name": "string",
  "description": "string",
  "priority": "number",
  "assignedAgentId": "string"
}

Response: 201 Created
{
  "taskId": "guid",
  "status": "string",
  "createdAt": "datetime"
}
```

#### 2.2B: Document AgentsController Endpoints
- [ ] GET /api/agents - List all agents
- [ ] GET /api/agents/{id} - Get agent details
- [ ] POST /api/agents - Register new agent
- [ ] PUT /api/agents/{id} - Update agent
- [ ] DELETE /api/agents/{id} - Unregister agent

#### 2.2C: Document OrchestratorController Endpoints
- [ ] Identify and document legacy endpoints
- [ ] Mark deprecated endpoints
- [ ] Provide migration guidance

## Phase 3: SignalR Documentation (2-3 hours)

### 3.1 SignalR Overview
**Estimated Time**: 30-45 minutes

#### 3.1A: Create SIGNALR-OVERVIEW.md
```markdown
# SignalR Real-time Communication

## Connection Setup
JavaScript client connection example

## Authentication
SignalR authentication flow

## Reconnection Strategy
Automatic reconnection patterns

## Message Formats
Standard message structure
```

### 3.2 Hub Documentation
**Estimated Time**: 90-120 minutes

#### 3.2A: Document CoordinatorChatHub
```javascript
// Connection example
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/coordinatorHub")
    .withAutomaticReconnect()
    .build();

// Method documentation
connection.on("ReceiveMessage", (user, message) => {
    // Handle incoming message
});

connection.invoke("SendMessage", user, message);
```

#### 3.2B: Document AgentInteractionHub
- [ ] Document connection setup
- [ ] List all hub methods
- [ ] Provide TypeScript interfaces
- [ ] Include error handling examples

#### 3.2C: Document AgentCommunicationHub
- [ ] Document agent-to-agent communication
- [ ] Provide subscription examples
- [ ] Document broadcast patterns
- [ ] Include group management

### 3.3 SignalR Integration Examples
**Estimated Time**: 30-45 minutes

#### 3.3A: Create JavaScript Client Examples
- [ ] Basic connection setup
- [ ] Authentication flow
- [ ] Message sending/receiving
- [ ] Error handling and reconnection

#### 3.3B: Create C# Client Examples
- [ ] Console application example
- [ ] WPF/WinForms integration
- [ ] Background service integration

## Phase 4: MediatR Patterns Documentation (1-1.5 hours)

### 4.1 CQRS Pattern Documentation
**Estimated Time**: 30-45 minutes

#### 4.1A: Create MEDIATR-PATTERNS.md
```csharp
// Command Pattern Example
public record CreateTaskCommand(
    string Name,
    string Description) : IRequest<TaskDto>;

// Query Pattern Example
public record GetTaskByIdQuery(
    Guid TaskId) : IRequest<TaskDto>;

// Handler Implementation Pattern
public class CreateTaskCommandHandler :
    IRequestHandler<CreateTaskCommand, TaskDto>
{
    // Implementation
}
```

### 4.2 Command/Query Catalog
**Estimated Time**: 30-45 minutes

#### 4.2A: Document All Commands
- [ ] List all command classes
- [ ] Document parameters and validation
- [ ] Provide usage examples
- [ ] Link to corresponding API endpoints

#### 4.2B: Document All Queries
- [ ] List all query classes
- [ ] Document filtering capabilities
- [ ] Provide pagination examples
- [ ] Document performance considerations

## Phase 5: Integration Guide (1-1.5 hours)

### 5.1 Common Integration Scenarios
**Estimated Time**: 45-60 minutes

#### 5.1A: Create INTEGRATION-GUIDE.md
```markdown
# Integration Guide

## Scenario 1: Task Submission and Monitoring
Step-by-step guide for submitting tasks and monitoring progress

## Scenario 2: Agent Registration and Management
Complete agent lifecycle management

## Scenario 3: Real-time Dashboard Integration
Building a real-time monitoring dashboard

## Scenario 4: Batch Processing
Submitting and managing batch operations

## Scenario 5: Error Recovery
Handling failures and retries
```

### 5.2 SDK Examples
**Estimated Time**: 15-30 minutes

#### 5.2A: Create Client SDK Examples
- [ ] C# SDK usage examples
- [ ] JavaScript/TypeScript examples
- [ ] Python client examples
- [ ] curl/bash script examples

## Phase 6: Swagger Enhancement (0.5-1 hour)

### 6.1 XML Documentation Comments
**Estimated Time**: 20-30 minutes

#### 6.1A: Enhance Controller Documentation
```csharp
/// <summary>
/// Управление задачами оркестратора
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TaskController : ControllerBase
{
    /// <summary>
    /// Создает новую задачу в очереди оркестратора
    /// </summary>
    /// <param name="command">Данные новой задачи</param>
    /// <returns>Созданная задача с присвоенным ID</returns>
    /// <response code="201">Задача успешно создана</response>
    /// <response code="400">Некорректные данные запроса</response>
    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<TaskDto>> CreateTask(
        [FromBody] CreateTaskCommand command)
    {
        // Implementation
    }
}
```

### 6.2 Swagger Configuration
**Estimated Time**: 10-20 minutes

#### 6.2A: Update Swagger Configuration
- [ ] Configure XML documentation inclusion
- [ ] Add API versioning to Swagger
- [ ] Configure authentication in Swagger UI
- [ ] Add example requests/responses

## Deliverables

### Primary Documentation Files
- [ ] `Docs/API/API-OVERVIEW.md` - API overview and architecture
- [ ] `Docs/API/API-REFERENCE.md` - Complete REST API reference
- [ ] `Docs/API/SIGNALR-REFERENCE.md` - SignalR hubs documentation
- [ ] `Docs/API/MEDIATR-REFERENCE.md` - MediatR patterns guide
- [ ] `Docs/API/INTEGRATION-GUIDE.md` - Integration scenarios

### Supporting Materials
- [ ] `Docs/API/Examples/` - Code examples in multiple languages
- [ ] `Docs/API/Postman/` - Postman collection and environment
- [ ] Enhanced Swagger configuration in Startup.cs
- [ ] XML documentation comments in all controllers

### Integration Artifacts
- [ ] Postman collection (orchestra-api.postman_collection.json)
- [ ] OpenAPI specification (openapi.json)
- [ ] Client SDK examples

## Testing and Validation

### Documentation Validation
- [ ] All endpoints documented and tested
- [ ] Code examples compile and run
- [ ] Postman collection executes successfully
- [ ] Swagger UI displays all documentation

### Review Checklist
- [ ] Technical accuracy verified
- [ ] Examples are practical and useful
- [ ] No missing endpoints or methods
- [ ] Consistent formatting and structure

## Timeline

### Day 1 (3-4 hours)
- Phase 1: Analysis and Preparation (1-2h)
- Phase 2: REST API Documentation (2-3h)

### Day 2 (3-4 hours)
- Phase 3: SignalR Documentation (2-3h)
- Phase 4: MediatR Patterns (1-1.5h)

### Day 3 (2-3 hours)
- Phase 5: Integration Guide (1-1.5h)
- Phase 6: Swagger Enhancement (0.5-1h)
- Final review and validation (0.5h)

## Risk Mitigation

### Potential Risks
1. **Undocumented legacy code** - May require code archaeology
2. **Complex SignalR patterns** - May need additional examples
3. **MediatR pattern complexity** - Requires clear explanation

### Mitigation Strategies
- Collaborate with original developers for legacy code
- Test all examples in actual environment
- Provide progressive complexity in examples

## Dependencies

### Technical Dependencies
- Access to all source code
- Running instance of the API
- Test environment for validation

### Knowledge Dependencies
- Understanding of MediatR patterns
- SignalR client library knowledge
- OpenAPI/Swagger specifications

## Notes

- All documentation should follow project's Russian XML comment convention
- Maintain consistency with existing CLAUDE.md guidelines
- Consider future API versioning in documentation structure
- Documentation should be LLM-friendly for future AI assistance