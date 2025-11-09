# Phase 1: Analysis and Preparation

**Parent Plan**: [00-API-DOCUMENTATION-PLAN.md](../00-API-DOCUMENTATION-PLAN.md)

**Estimated Time**: 1-2 hours
**Dependencies**: Access to source code, running API instance

## Objectives
- Complete inventory of all API endpoints and SignalR hubs
- Analyze MediatR command/query patterns
- Establish documentation structure and templates

## Task 1.1: API Inventory and Analysis (45 minutes)

### 1.1A: Analyze REST API Controllers
**Technical Steps**:
```bash
# Read and analyze controller files
Read src/Orchestra.API/Controllers/TaskController.cs
Read src/Orchestra.API/Controllers/AgentsController.cs
Read src/Orchestra.API/Controllers/OrchestratorController.cs
```

**Integration Requirements**:
- [ ] Extract all HTTP methods and routes
- [ ] Document route parameters and constraints
- [ ] Identify request/response DTOs
- [ ] Note authorization requirements
- [ ] Document any custom action filters

**Output**: `analysis/rest-api-inventory.md` with complete endpoint listing

### 1.1B: Analyze SignalR Hubs
**Technical Steps**:
```bash
# Read and analyze hub files
Read src/Orchestra.API/Hubs/CoordinatorChatHub.cs
Read src/Orchestra.API/Hubs/AgentInteractionHub.cs
Read src/Orchestra.API/Services/AgentCommunicationHub.cs
```

**Integration Requirements**:
- [ ] Extract all hub methods (client-callable and server-side)
- [ ] Document hub routes and connection requirements
- [ ] Identify message contracts and DTOs
- [ ] Note authentication/authorization setup
- [ ] Document group management patterns

**Output**: `analysis/signalr-hub-inventory.md` with hub method catalog

### 1.1C: Analyze MediatR Patterns
**Technical Steps**:
```bash
# Find all command and query classes
Glob pattern: src/**/*Command.cs
Glob pattern: src/**/*Query.cs
Glob pattern: src/**/*Handler.cs
```

**Integration Requirements**:
- [ ] Catalog all IRequest<T> implementations
- [ ] Map commands/queries to their handlers
- [ ] Document validation rules in handlers
- [ ] Identify domain events published
- [ ] Note pipeline behaviors (validation, logging)

**Output**: `analysis/mediatr-pattern-inventory.md` with CQRS catalog

## Task 1.2: Documentation Structure Planning (45 minutes)

### 1.2A: Create Documentation Hierarchy
**Technical Steps**:
```bash
# Create directory structure
mkdir -p Docs/API/Examples/CSharp
mkdir -p Docs/API/Examples/JavaScript
mkdir -p Docs/API/Examples/Python
mkdir -p Docs/API/Examples/Curl
mkdir -p Docs/API/Postman
mkdir -p Docs/API/OpenAPI/schemas
```

**Integration Requirements**:
- [ ] Setup directory structure in file system
- [ ] Create README.md in each directory
- [ ] Define naming conventions for files
- [ ] Establish versioning strategy
- [ ] Configure .gitignore for generated files

**Validation**:
- Directory structure created successfully
- All README files in place
- Naming conventions documented

### 1.2B: Prepare Code Examples Repository
**Technical Steps**:
```csharp
// Create example template files
// CSharp/ApiClientExample.cs
public class ApiClientExample
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "http://localhost:5000";

    // TODO: Add example methods
}

// JavaScript/signalr-client.js
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/coordinatorHub")
    .build();

// TODO: Add connection examples
```

**Integration Requirements**:
- [ ] Create base client classes for each language
- [ ] Setup dependency management (package.json, requirements.txt)
- [ ] Configure build/test scripts
- [ ] Add .editorconfig for consistent formatting
- [ ] Create example configuration files

**Validation**:
- Example projects compile/run
- Dependencies properly declared
- Configuration templates work

## Deliverables

### Analysis Documents
- [ ] `analysis/rest-api-inventory.md` - Complete REST API catalog
- [ ] `analysis/signalr-hub-inventory.md` - SignalR hub method listing
- [ ] `analysis/mediatr-pattern-inventory.md` - Command/Query patterns

### Structure Artifacts
- [ ] `Docs/API/` directory hierarchy created
- [ ] README files in all directories
- [ ] Example project templates initialized
- [ ] Configuration files prepared

## Success Criteria
- [ ] All API endpoints identified and cataloged
- [ ] All SignalR hub methods documented
- [ ] All MediatR patterns analyzed
- [ ] Documentation structure established
- [ ] Example repositories initialized

## Risk Mitigation
- **Undocumented endpoints**: Use reflection to discover all controllers
- **Complex hub patterns**: Test hub methods with SignalR client
- **Missing DTOs**: Generate from controller signatures

## Notes
- Use automated tools where possible (Swashbuckle, DocFX)
- Maintain consistency with existing documentation
- Consider future API versioning requirements