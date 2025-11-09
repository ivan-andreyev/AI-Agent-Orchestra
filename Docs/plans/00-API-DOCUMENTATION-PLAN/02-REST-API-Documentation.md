# Phase 2: REST API Documentation

**Parent Plan**: [00-API-DOCUMENTATION-PLAN.md](../00-API-DOCUMENTATION-PLAN.md)

**Estimated Time**: 2-3 hours
**Dependencies**: Phase 1 analysis completed

## Objectives
- Create comprehensive REST API documentation
- Document all endpoints with examples
- Generate Postman collection
- Enhance with OpenAPI annotations

## Task 2.1: API Overview Document (60 minutes)

### 2.1A: Create API-OVERVIEW.md
**Technical Steps**:
```markdown
# File: Docs/API/API-OVERVIEW.md
# Content structure:
- Base URLs and environments
- Authentication mechanisms
- Error handling patterns
- Rate limiting configuration
- Versioning strategy
- Common headers
```

**Integration Requirements**:
- [ ] Extract authentication config from Startup.cs
- [ ] Document JWT token structure and claims
- [ ] Define standard error response format
- [ ] Document rate limiting rules from appsettings.json
- [ ] Include CORS configuration

**Code Template**:
```csharp
// Standard error response
public class ApiErrorResponse
{
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public Dictionary<string, string[]> ValidationErrors { get; set; }
    public string TraceId { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### 2.1B: Document Common Patterns
**Technical Steps**:
```csharp
// Pagination pattern
public class PagedRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; }
    public bool SortDescending { get; set; }
}

// Filtering pattern
public class FilterRequest
{
    public Dictionary<string, string> Filters { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
```

**Integration Requirements**:
- [ ] Document request/response envelope patterns
- [ ] Define pagination response structure
- [ ] Document filtering and sorting conventions
- [ ] Include batch operation patterns
- [ ] Define standard status codes usage

## Task 2.2: REST API Reference (90 minutes)

### 2.2A: Document TaskController Endpoints
**Technical Steps**:
```csharp
/// <summary>
/// Управление задачами оркестратора
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TaskController : ControllerBase
{
    // Document each endpoint with:
    // - HTTP method and route
    // - Request body/parameters
    // - Response structure
    // - Status codes
    // - Example requests/responses
}
```

**Endpoint Documentation Template**:
```markdown
## Create Task
`POST /api/tasks`

Creates a new task in the orchestration queue.

### Request
**Headers:**
- `Content-Type: application/json`
- `Authorization: Bearer {token}`

**Body:**
```json
{
  "name": "Process Data",
  "description": "Process customer data batch",
  "priority": 1,
  "assignedAgentId": "agent-123",
  "metadata": {
    "batchSize": 1000,
    "timeout": 300
  }
}
```

### Response
**Status:** `201 Created`
**Headers:**
- `Location: /api/tasks/task-456`

**Body:**
```json
{
  "taskId": "task-456",
  "status": "Queued",
  "createdAt": "2025-10-26T10:30:00Z",
  "estimatedCompletion": "2025-10-26T10:35:00Z"
}
```

### Error Responses
- `400 Bad Request` - Invalid request data
- `401 Unauthorized` - Missing or invalid token
- `403 Forbidden` - Insufficient permissions
- `422 Unprocessable Entity` - Validation failed
```

**Integration Requirements**:
- [ ] Document all CRUD operations for tasks
- [ ] Include batch operations endpoints
- [ ] Document task status transitions
- [ ] Add webhook/callback endpoints
- [ ] Include task cancellation endpoints

### 2.2B: Document AgentsController Endpoints
**Technical Documentation**:
```csharp
// GET /api/agents
// List all registered agents with filtering
[HttpGet]
public async Task<ActionResult<PagedResponse<AgentDto>>> GetAgents(
    [FromQuery] PagedRequest paging,
    [FromQuery] AgentFilter filter)

// GET /api/agents/{id}
// Get specific agent details including capabilities
[HttpGet("{id}")]
public async Task<ActionResult<AgentDto>> GetAgent(string id)

// POST /api/agents
// Register a new agent with the orchestrator
[HttpPost]
public async Task<ActionResult<AgentDto>> RegisterAgent(
    [FromBody] RegisterAgentCommand command)

// PUT /api/agents/{id}
// Update agent configuration or status
[HttpPut("{id}")]
public async Task<ActionResult<AgentDto>> UpdateAgent(
    string id,
    [FromBody] UpdateAgentCommand command)

// DELETE /api/agents/{id}
// Unregister an agent from the system
[HttpDelete("{id}")]
public async Task<ActionResult> UnregisterAgent(string id)

// GET /api/agents/{id}/tasks
// Get tasks assigned to specific agent
[HttpGet("{id}/tasks")]
public async Task<ActionResult<List<TaskDto>>> GetAgentTasks(
    string id,
    [FromQuery] TaskStatusFilter filter)
```

**Integration Requirements**:
- [ ] Document agent lifecycle management
- [ ] Include capability querying endpoints
- [ ] Document health check endpoints
- [ ] Add performance metrics endpoints
- [ ] Include agent grouping/tagging

### 2.2C: Document OrchestratorController Endpoints
**Technical Documentation**:
```csharp
// Legacy endpoints - mark as deprecated
[Obsolete("Use /api/tasks for task management")]
[HttpPost("orchestrator/execute")]
public async Task<IActionResult> ExecuteLegacy(
    [FromBody] LegacyTaskRequest request)

// Migration guidance documentation
// OLD: POST /orchestrator/execute
// NEW: POST /api/tasks with MediatR command
```

**Integration Requirements**:
- [ ] Mark all deprecated endpoints clearly
- [ ] Provide migration guides for each endpoint
- [ ] Document removal timeline
- [ ] Include compatibility notes
- [ ] Provide transformation examples

## Task 2.3: OpenAPI/Swagger Enhancement (30 minutes)

### 2.3A: Add OpenAPI Annotations
**Technical Steps**:
```csharp
[HttpPost]
[ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[SwaggerOperation(
    Summary = "Create a new task",
    Description = "Creates a new task in the orchestration queue and returns the task details",
    OperationId = "CreateTask",
    Tags = new[] { "Tasks" }
)]
public async Task<ActionResult<TaskDto>> CreateTask(
    [FromBody, SwaggerRequestBody("Task creation details", Required = true)]
    CreateTaskCommand command)
```

**Integration Requirements**:
- [ ] Add XML documentation to all controllers
- [ ] Configure Swagger in Startup.cs for XML comments
- [ ] Add example values using SwaggerExample
- [ ] Configure security definitions
- [ ] Add schema filters for complex types

### 2.3B: Generate OpenAPI Specification
**Technical Steps**:
```csharp
// In Startup.cs
services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI Agent Orchestra API",
        Version = "v1",
        Description = "Orchestration platform for AI agents",
        Contact = new OpenApiContact
        {
            Name = "Development Team",
            Email = "dev@orchestra.ai"
        }
    });

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    // Add security definition
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
});
```

**Integration Requirements**:
- [ ] Configure XML documentation output in project
- [ ] Add authentication requirements to endpoints
- [ ] Generate openapi.json file
- [ ] Validate against OpenAPI 3.0 spec
- [ ] Test with external tools (Postman, Insomnia)

## Deliverables

### Documentation Files
- [ ] `Docs/API/API-OVERVIEW.md` - Complete API overview
- [ ] `Docs/API/API-REFERENCE.md` - All REST endpoints documented
- [ ] `Docs/API/MIGRATION-GUIDE.md` - Legacy endpoint migration

### OpenAPI Artifacts
- [ ] `Docs/API/OpenAPI/openapi.json` - OpenAPI 3.0 specification
- [ ] `Docs/API/OpenAPI/schemas/*.json` - Component schemas
- [ ] Enhanced Swagger UI configuration

### Postman Collection
- [ ] `Docs/API/Postman/orchestra-api.postman_collection.json`
- [ ] `Docs/API/Postman/orchestra-api.postman_environment.json`
- [ ] Pre-request scripts for authentication

## Success Criteria
- [ ] All REST endpoints documented with examples
- [ ] OpenAPI specification validates successfully
- [ ] Postman collection executes without errors
- [ ] Swagger UI displays complete documentation
- [ ] Migration guides for deprecated endpoints

## Validation Checklist
- [ ] Every endpoint has request/response examples
- [ ] All status codes documented
- [ ] Authentication requirements clear
- [ ] Rate limiting documented
- [ ] Error responses standardized