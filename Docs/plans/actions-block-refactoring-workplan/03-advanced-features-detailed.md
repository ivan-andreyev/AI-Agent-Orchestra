# Phase 3: Advanced Features - Detailed Decomposition

**Родительский план**: [Actions Block Refactoring Workplan](../actions-block-refactoring-workplan.md) → Phase 3: Advanced Features (строки 280-521)

**Общая оценка**: 28-35 часов → Детализировано в 31 атомарную задачу по 1-3 часа каждая

---

## Phase 3 Overview

**АРХИТЕКТУРНОЕ РЕШЕНИЕ**: Workflow Builder интегрирует React Flow библиотеку для визуальных компонентов, custom логика для AI agent orchestration. Template Marketplace использует hybrid подход: local storage + JSON export/import для community sharing.

**КРИТИЧЕСКИЙ ПУТЬ**:
1. Workflow Manager Foundation (3A) →
2. Visual Workflow Builder Interface (3B) →
3. Template Marketplace (3C) || Advanced UI Features (3D)

**ОБЩИЕ ЗАВИСИМОСТИ**:
- Phase 2 полностью завершена
- Actions Block основная функциональность работает
- SQLite база данных настроена

---

## 3A. Workflow Manager Foundation (8-10 hours)

### 3A.1 WorkflowEngine Core Service (2.5 hours → 3 задачи)

#### 3A.1.1 WorkflowEngine Interface and Base Structure (1 hour)
**Приоритет**: Critical Path
**Зависимости**: Нет

**Техническая спецификация**:
- Создать `src/Orchestra.Core/Services/IWorkflowEngine.cs`
- Создать `src/Orchestra.Core/Models/Workflow/WorkflowExecutionResult.cs`
- Создать базовую структуру `src/Orchestra.Core/Services/WorkflowEngine.cs`

**Интерфейс**:
```csharp
public interface IWorkflowEngine
{
    Task<WorkflowExecutionResult> ExecuteAsync(WorkflowDefinition workflow, WorkflowContext context);
    Task<bool> ValidateWorkflowAsync(WorkflowDefinition workflow);
    Task PauseExecutionAsync(string executionId);
    Task ResumeExecutionAsync(string executionId);
}

public record WorkflowExecutionResult(
    string ExecutionId,
    WorkflowStatus Status,
    Dictionary<string, object> OutputVariables,
    List<WorkflowStepResult> StepResults,
    Exception? Error = null
);
```

**Deliverables**:
- Интерфейс IWorkflowEngine
- Базовые модели результатов
- Skeleton класс WorkflowEngine

**Acceptance Criteria**:
- [ ] Интерфейс компилируется без ошибок
- [ ] Базовая структура WorkflowEngine создана
- [ ] Модели результатов определены

**Unit Testing**:
- Тесты интерфейса (моки)
- Валидация базовых моделей

---

#### 3A.1.2 Workflow Execution State Machine (1 hour)
**Приоритет**: Critical Path
**Зависимости**: 3A.1.1

**Техническая спецификация**:
- Реализовать execution state machine в WorkflowEngine
- Создать enum WorkflowStatus: Pending, Running, Paused, Completed, Failed
- Реализовать state transitions и validation

**Алгоритм**:
```
ALGORITHM: ExecuteWorkflow(workflowDefinition, context)
1. VALIDATE workflow syntax using WorkflowValidator
2. BUILD execution graph from workflow steps
3. INITIALIZE execution context with variables and state
4. SET status = Running
5. RETURN WorkflowExecutionResult with initial state
```

**Deliverables**:
- WorkflowStatus enum
- State machine logic
- Execution context initialization

**Acceptance Criteria**:
- [ ] State transitions работают корректно
- [ ] Execution context properly initialized
- [ ] Error states handled correctly

**Unit Testing**:
- State transition tests
- Context initialization tests
- Error handling tests

---

#### 3A.1.3 Workflow Graph Execution Logic (30 minutes)
**Приоритет**: Critical Path
**Зависимости**: 3A.1.2, 3A.2.1

**Техническая спецификация**:
- Реализовать topological sort для execution order
- Добавить error handling и retry logic
- Реализовать variable mutation tracking

**Алгоритм**:
```
ALGORITHM: ExecuteSteps(executionGraph, context)
4. EXECUTE steps using topological sort order:
   - EVALUATE conditions (if/then/else, loops)
   - HANDLE branching and merging logic
   - TRACK variable mutations and scope
   - MANAGE error handling and retry logic
5. RETURN WorkflowExecutionResult with outputs
```

**Deliverables**:
- Topological sort implementation
- Step execution loop
- Variable tracking system

**Acceptance Criteria**:
- [ ] Steps execute in correct dependency order
- [ ] Variables passed between steps correctly
- [ ] Linear workflows execute successfully

**Unit Testing**:
- Execution order tests
- Variable passing tests
- Basic linear workflow tests

---

### 3A.2 Workflow Definition Models (1.5 hours → 2 задачи)

#### 3A.2.1 Core Workflow Models (1 hour)
**Приоритет**: Critical Path
**Зависимости**: Нет

**Техническая спецификация**:
- Создать `src/Orchestra.Core/Models/Workflow/WorkflowDefinition.cs`
- Создать `src/Orchestra.Core/Models/Workflow/WorkflowStep.cs`
- Создать `src/Orchestra.Core/Models/Workflow/VariableDefinition.cs`

**Ключевые модели**:
```csharp
public record WorkflowDefinition(
    string Id,
    string Name,
    List<WorkflowStep> Steps,
    Dictionary<string, VariableDefinition> Variables,
    WorkflowMetadata Metadata
);

public record WorkflowStep(
    string Id,
    WorkflowStepType Type, // Task, Condition, Loop, Parallel
    string Command,
    Dictionary<string, object> Parameters,
    List<string> DependsOn,
    ConditionalLogic? Condition,
    RetryPolicy? RetryPolicy
);

public enum WorkflowStepType
{
    Task,
    Condition,
    Loop,
    Parallel,
    Start,
    End
}
```

**Deliverables**:
- WorkflowDefinition record
- WorkflowStep record
- Supporting enums and types

**Acceptance Criteria**:
- [ ] Models compile without errors
- [ ] Records support immutability
- [ ] All required properties defined

**Unit Testing**:
- Model instantiation tests
- Record equality tests
- Enum validation tests

---

#### 3A.2.2 JSON Schema and Serialization (30 minutes)
**Приоритет**: High
**Зависимости**: 3A.2.1

**Техническая спецификация**:
- Создать JSON Schema для workflow validation
- Добавить System.Text.Json attributes для serialization
- Создать WorkflowSerializer utility class

**JSON Schema Features**:
- Required field validation
- Type validation (step types, parameter types)
- Dependency validation (no circular references)

**Deliverables**:
- workflow-schema.json
- Serialization attributes
- WorkflowSerializer class

**Acceptance Criteria**:
- [ ] Workflows serialize/deserialize correctly
- [ ] JSON schema validates workflow structure
- [ ] Complex workflows with conditions serialize properly

**Unit Testing**:
- Serialization round-trip tests
- JSON schema validation tests
- Invalid workflow rejection tests

---

### 3A.3 Conditional Logic Processor (2 hours → 2 задачи)

#### 3A.3.1 Expression Evaluator Core (1 hour)
**Приоритет**: High
**Зависимости**: 3A.2.1

**Техническая спецификация**:
- Создать `src/Orchestra.Core/Services/ExpressionEvaluator.cs`
- Реализовать safe expression evaluation
- Support для basic operators: ==, !=, >, <, contains, regex

**Алгоритм**:
```
ALGORITHM: EvaluateCondition(condition, context)
1. PARSE condition expression (supports: ==, !=, >, <, contains, regex)
2. RESOLVE variables from execution context
3. EVALUATE expression using safe expression evaluator
4. RETURN boolean result for workflow branching
```

**Поддерживаемые выражения**:
- Variable comparisons: `$variable1 == "value"`
- Task result checks: `$previousTask.success == true`
- Numeric comparisons: `$counter > 5`

**Deliverables**:
- ExpressionEvaluator class
- ConditionalLogic model
- Supported operators implementation

**Acceptance Criteria**:
- [ ] Basic comparisons work correctly
- [ ] Variables resolve from context
- [ ] No code injection possible

**Unit Testing**:
- Expression parsing tests
- Variable resolution tests
- Security tests (injection attempts)

---

#### 3A.3.2 Complex Boolean Logic (1 hour)
**Приоритет**: Medium
**Зависимости**: 3A.3.1

**Техническая спецификация**:
- Добавить support для AND, OR, NOT operators
- Реализовать parentheses grouping
- Добавить function calls (len, contains, regex)

**Complex Expression Examples**:
```
($task1.success == true) AND ($counter > 0)
NOT ($error_count > 5) OR ($force_continue == true)
len($file_list) > 0 AND contains($output, "SUCCESS")
```

**Deliverables**:
- Boolean logic operators
- Parentheses parsing
- Function call support

**Acceptance Criteria**:
- [ ] Complex boolean expressions evaluate correctly
- [ ] Parentheses grouping works
- [ ] Function calls execute safely

**Unit Testing**:
- Complex expression tests
- Operator precedence tests
- Function call tests

---

### 3A.4 Loop and Retry Mechanisms (2 hours → 2 задачи)

#### 3A.4.1 Loop Types Implementation (1 hour)
**Приоритет**: Medium
**Зависимости**: 3A.3.1

**Техническая спецификация**:
- Создать `src/Orchestra.Core/Models/Workflow/LoopDefinition.cs`
- Реализовать ForEach, While, Retry loop types
- Добавить infinite loop protection

**Loop Types**:
- **ForEach**: Iterate over collections (repositories, files, etc.)
- **While**: Continue while condition is true
- **Retry**: Retry failed tasks with exponential backoff

**Алгоритм**:
```
ALGORITHM: ExecuteLoop(loopStep, context)
1. INITIALIZE loop counter and collection iterator
2. WHILE loop condition is true:
   a. CREATE scoped context with loop variables
   b. EXECUTE nested steps in loop body
   c. HANDLE break/continue conditions
   d. UPDATE loop variables and counters
   e. CHECK for infinite loop protection (max iterations)
3. MERGE loop results into parent context
```

**Deliverables**:
- LoopDefinition models
- Loop execution logic
- Infinite loop protection

**Acceptance Criteria**:
- [ ] ForEach loops iterate over collections
- [ ] While loops respect conditions
- [ ] Loop protection prevents infinite loops

**Unit Testing**:
- ForEach iteration tests
- While condition tests
- Infinite loop protection tests

---

#### 3A.4.2 Retry Policies and Error Handling (1 hour)
**Приоритет**: Medium
**Зависимости**: 3A.4.1

**Техническая спецификация**:
- Создать `src/Orchestra.Core/Models/Workflow/RetryPolicy.cs`
- Реализовать exponential backoff strategy
- Добавить configurable retry limits

**Retry Policy Features**:
- Max retry count
- Exponential backoff delays
- Specific exception handling
- Retry condition evaluation

**Deliverables**:
- RetryPolicy model
- Exponential backoff implementation
- Retry execution logic

**Acceptance Criteria**:
- [ ] Failed tasks retry with exponential backoff
- [ ] Retry limits respected
- [ ] Specific exceptions can trigger retries

**Unit Testing**:
- Retry logic tests
- Backoff timing tests
- Exception handling tests

---

## 3B. Visual Workflow Builder Interface (10-12 hours)

### 3B.1 React Flow Integration (2.5 hours → 3 задачи)

#### 3B.1.1 React Flow Setup and Dependencies (1 hour)
**Приоритет**: Critical Path
**Зависимости**: 3A completed

**Техническая спецификация**:
- Install react-flow-renderer npm package
- Setup Blazor-React interop
- Создать базовую структуру компонентов

**Component Structure**:
```
src/Orchestra.Web/Components/WorkflowBuilder/
├── WorkflowBuilder.razor (main container)
├── WorkflowCanvas.razor (React Flow wrapper)
├── NodePalette.razor (draggable node types)
├── NodeProperties.razor (property editor)
└── WorkflowToolbar.razor (save/load/validate)
```

**Dependencies**:
- react-flow-renderer: ^11.x
- @types/react-flow-renderer
- Additional React dependencies

**Deliverables**:
- Package.json updates
- Blazor component structure
- React Flow basic setup

**Acceptance Criteria**:
- [ ] React Flow renders in Blazor app
- [ ] Component structure created
- [ ] No build errors

**Unit Testing**:
- Component rendering tests
- React interop tests

---

#### 3B.1.2 Custom Node Types Definition (1 hour)
**Приоритет**: Critical Path
**Зависимости**: 3B.1.1

**Техническая спецификация**:
- Создать custom node types для React Flow
- Implement TaskNode, ConditionNode, LoopNode, StartNode, EndNode
- Добавить node styling и icons

**Custom Node Types**:
```typescript
export const nodeTypes = {
  taskNode: TaskNode,
  conditionNode: ConditionNode,
  loopNode: LoopNode,
  startNode: StartNode,
  endNode: EndNode
};
```

**Node Styling**:
- Task nodes: Blue color, gear icon
- Condition nodes: Yellow color, diamond shape
- Loop nodes: Green color, circular arrow
- Start/End nodes: Gray color, specific shapes

**Deliverables**:
- Custom node components
- Node type definitions
- Node styling (CSS/Styled Components)

**Acceptance Criteria**:
- [ ] All node types render correctly
- [ ] Nodes have appropriate styling
- [ ] Node icons display properly

**Unit Testing**:
- Node rendering tests
- Node type validation tests

---

#### 3B.1.3 Basic Drag and Drop Functionality (30 minutes)
**Приоритет**: Critical Path
**Зависимости**: 3B.1.2

**Техническая спецификация**:
- Implement drag and drop from palette to canvas
- Setup node connection logic
- Add basic canvas interactions (zoom, pan)

**Drag and Drop Features**:
- Palette nodes draggable to canvas
- Automatic node positioning
- Connection handle visibility
- Canvas zoom and pan controls

**Deliverables**:
- Drag and drop implementation
- Node connection logic
- Canvas interaction controls

**Acceptance Criteria**:
- [ ] Can drag nodes from palette to canvas
- [ ] Nodes can be connected with edges
- [ ] Canvas supports zoom and pan

**Unit Testing**:
- Drag and drop tests
- Node connection tests
- Canvas interaction tests

---

### 3B.2 Node Property Editors (3 hours → 3 задачи)

#### 3B.2.1 Dynamic Property Form System (1 hour)
**Priоритет**: High
**Зависимости**: 3B.1.3

**Техническая спецификация**:
- Создать dynamic form system для node properties
- Implement form field types: text, dropdown, checkbox, number
- Add form validation framework

**Dynamic Form Features**:
- Schema-driven form generation
- Field type auto-detection
- Real-time validation
- Form state management

**Form Field Types**:
```csharp
public enum PropertyFieldType
{
    Text,
    Number,
    Boolean,
    Dropdown,
    MultiSelect,
    TextArea,
    FilePicker
}
```

**Deliverables**:
- Dynamic form generator
- Field type implementations
- Form validation system

**Acceptance Criteria**:
- [ ] Forms generate from schema
- [ ] All field types work correctly
- [ ] Validation provides real-time feedback

**Unit Testing**:
- Form generation tests
- Field type tests
- Validation tests

---

#### 3B.2.2 Node-Specific Property Editors (1.5 hours)
**Приоритет**: High
**Зависимости**: 3B.2.1

**Техническая спецификация**:
- Implement property editors для каждого node type
- Create command selection dropdown для TaskNode
- Implement expression builder для ConditionNode

**Node-Specific Editors**:
- **TaskNode**: Command selection, parameter inputs, repository picker
- **ConditionNode**: Expression builder with autocomplete
- **LoopNode**: Loop type selector, iteration parameters
- **StartNode**: Workflow metadata editor
- **EndNode**: Output variable mapping

**Expression Builder Algorithm**:
```
ALGORITHM: BuildExpressionForm(nodeType, availableVariables)
1. RENDER expression type selector (comparison, logical, function)
2. POPULATE variable dropdown with available variables and outputs
3. PROVIDE operator selection based on variable types
4. VALIDATE expression syntax in real-time
5. SHOW expression preview with sample data
```

**Deliverables**:
- TaskNode property editor
- ConditionNode expression builder
- LoopNode configuration editor

**Acceptance Criteria**:
- [ ] Each node type has appropriate property editor
- [ ] Expression builder supports autocomplete
- [ ] All property changes reflect in workflow definition

**Unit Testing**:
- Property editor tests for each node type
- Expression builder validation tests

---

#### 3B.2.3 Property Validation and Preview (30 minutes)
**Приоритет**: Medium
**Зависимости**: 3B.2.2

**Техническая спецификация**:
- Add real-time property validation
- Implement expression preview with sample data
- Add validation error highlighting

**Validation Features**:
- Required field checking
- Expression syntax validation
- Parameter type validation
- Cross-node dependency validation

**Deliverables**:
- Real-time validation system
- Expression preview functionality
- Error highlighting UI

**Acceptance Criteria**:
- [ ] Invalid properties highlighted immediately
- [ ] Expression preview shows expected results
- [ ] Validation errors provide helpful messages

**Unit Testing**:
- Validation rule tests
- Preview functionality tests
- Error message tests

---

### 3B.3 Workflow Canvas Logic (2.5 hours → 3 задачи)

#### 3B.3.1 Node and Edge Manipulation (1 hour)
**Приоритет**: High
**Зависимости**: 3B.2.1

**Техническая спецификация**:
- Implement node creation/deletion operations
- Add undo/redo functionality
- Implement edge connection validation

**Canvas Operations**:
- Node creation from palette
- Node deletion with confirmation
- Edge creation with drag and drop
- Undo/redo stack management

**Validation Logic**:
- Prevent circular dependencies
- Validate required connections
- Check node compatibility for connections

**Deliverables**:
- Node manipulation functions
- Undo/redo system
- Edge validation logic

**Acceptance Criteria**:
- [ ] Nodes can be created and deleted safely
- [ ] Undo/redo works for all operations
- [ ] Invalid connections are prevented

**Unit Testing**:
- Node manipulation tests
- Undo/redo tests
- Edge validation tests

---

#### 3B.3.2 Auto-Layout and Visualization (1 hour)
**Приоритет**: Medium
**Зависимости**: 3B.3.1

**Техническая спецификация**:
- Implement auto-layout algorithm for better visualization
- Add zoom and pan controls with limits
- Implement minimap for large workflows

**Auto-Layout Features**:
- Hierarchical layout for workflow steps
- Automatic spacing and alignment
- Layout preservation during editing
- Smart edge routing

**Visualization Controls**:
- Zoom controls with min/max limits
- Pan with bounds checking
- Minimap for navigation
- Fit-to-screen functionality

**Deliverables**:
- Auto-layout algorithm
- Zoom and pan controls
- Minimap component

**Acceptance Criteria**:
- [ ] Auto-layout creates readable workflows
- [ ] Zoom and pan work smoothly
- [ ] Minimap helps navigate large workflows

**Unit Testing**:
- Layout algorithm tests
- Zoom/pan functionality tests
- Minimap tests

---

#### 3B.3.3 Canvas State Management (30 minutes)
**Приоритет**: High
**Зависимости**: 3B.3.2

**Техническая спецификация**:
- Implement canvas state persistence
- Add workflow save/load functionality
- Create canvas dirty state tracking

**State Management Features**:
- Canvas state serialization
- Automatic save on changes
- Dirty state indicators
- Save conflict resolution

**Deliverables**:
- Canvas state manager
- Save/load functionality
- Dirty state tracking

**Acceptance Criteria**:
- [ ] Canvas state persists between sessions
- [ ] Save/load works reliably
- [ ] Users notified of unsaved changes

**Unit Testing**:
- State persistence tests
- Save/load tests
- Dirty state tests

---

### 3B.4 Workflow Serialization (2 hours → 2 задачи)

#### 3B.4.1 Visual to JSON Conversion (1.5 hours)
**Приоритет**: Critical Path
**Зависимости**: 3B.3.3, 3A.2.2

**Техническая спецификация**:
- Implement conversion from React Flow state to WorkflowDefinition
- Handle visual positioning and logical workflow mapping
- Validate workflow structure during conversion

**Conversion Algorithm**:
```
ALGORITHM: SerializeWorkflow(canvasState)
1. EXTRACT nodes and edges from React Flow state
2. CONVERT visual positions to logical workflow steps
3. BUILD dependency graph from edge connections
4. VALIDATE workflow structure and dependencies
5. GENERATE WorkflowDefinition JSON with proper ordering
```

**Conversion Features**:
- Node property extraction
- Edge dependency mapping
- Step ordering based on connections
- Validation during conversion

**Deliverables**:
- Canvas to JSON converter
- Dependency graph builder
- Workflow structure validator

**Acceptance Criteria**:
- [ ] Visual workflows convert to valid JSON
- [ ] All node properties preserved in conversion
- [ ] Edge connections become proper dependencies

**Unit Testing**:
- Conversion accuracy tests
- Property preservation tests
- Dependency mapping tests

---

#### 3B.4.2 JSON to Visual Loading (30 minutes)
**Приоритет**: High
**Зависимости**: 3B.4.1

**Техническая спецификация**:
- Implement JSON WorkflowDefinition loading to canvas
- Handle node positioning and layout
- Restore all node properties and connections

**Loading Features**:
- JSON parsing and validation
- Node positioning calculation
- Property restoration
- Edge recreation

**Deliverables**:
- JSON to canvas loader
- Node positioning algorithm
- Property restoration logic

**Acceptance Criteria**:
- [ ] JSON workflows load correctly to canvas
- [ ] Node positions calculated appropriately
- [ ] All properties and connections restored

**Unit Testing**:
- JSON loading tests
- Position calculation tests
- Property restoration tests

---

## 3C. Template Marketplace (6-8 hours)

### 3C.1 Template Import/Export System (2 hours → 2 задачи)

#### 3C.1.1 Template Export Functionality (1 hour)
**Приоритет**: High
**Зависимости**: 3B.4.1

**Техническая спецификация**:
- Создать template export system
- Bundle template with dependencies and metadata
- Generate shareable template files

**Export Features**:
- Template metadata extraction
- Dependency bundling
- Version information
- Export format standardization

**Export Algorithm**:
```
ALGORITHM: ExportTemplate(templateId, includeMetadata)
1. EXTRACT template workflow definition
2. GATHER template metadata (name, description, version)
3. BUNDLE dependencies and required parameters
4. GENERATE sharing URL or file
5. VALIDATE export completeness
```

**Deliverables**:
- Template export service
- Metadata bundling logic
- Export file format specification

**Acceptance Criteria**:
- [ ] Templates export with all required data
- [ ] Export includes proper metadata
- [ ] Generated files are shareable

**Unit Testing**:
- Export completeness tests
- Metadata bundling tests
- File format validation tests

---

#### 3C.1.2 Template Import and Validation (1 hour)
**Приоритет**: High
**Зависимости**: 3C.1.1

**Техническая спецификация**:
- Implement template import system
- Add security validation and sanitization
- Handle template conflicts and dependencies

**Import Algorithm**:
```
ALGORITHM: ImportTemplate(templateJson, source)
1. VALIDATE JSON schema and structure
2. CHECK for template conflicts (ID collision)
3. VERIFY template dependencies (required services, parameters)
4. SANITIZE template content (prevent malicious commands)
5. ADD to local template repository with metadata
```

**Security Features**:
- Command sanitization
- Parameter validation
- Safe defaults
- Malicious content detection

**Deliverables**:
- Template import service
- Security validation system
- Conflict resolution logic

**Acceptance Criteria**:
- [ ] Templates import safely with validation
- [ ] Malicious content blocked
- [ ] Import conflicts handled gracefully

**Unit Testing**:
- Import validation tests
- Security sanitization tests
- Conflict resolution tests

---

### 3C.2 Template Repository Storage (1.5 hours → 2 задачи)

#### 3C.2.1 Enhanced Storage Schema (1 hour)
**Приоритет**: Medium
**Зависимости**: 3C.1.2

**Техническая спецификация**:
- Design enhanced template storage with versioning
- Implement semantic versioning support
- Create upgrade/downgrade pathways

**Storage Schema**:
```json
{
  "templates": {
    "template-id": {
      "versions": {
        "1.0.0": { "templateData": {...}, "metadata": {...} },
        "1.1.0": { "templateData": {...}, "metadata": {...} }
      },
      "currentVersion": "1.1.0",
      "compatibilityMatrix": {...}
    }
  }
}
```

**Versioning Logic**:
- Semantic versioning (MAJOR.MINOR.PATCH)
- Compatibility checking
- Automatic migration support
- Version history tracking

**Deliverables**:
- Enhanced storage schema
- Versioning system
- Compatibility matrix

**Acceptance Criteria**:
- [ ] Templates support multiple versions
- [ ] Version compatibility checked
- [ ] Upgrade/downgrade paths work

**Unit Testing**:
- Versioning logic tests
- Compatibility tests
- Migration tests

---

#### 3C.2.2 Template Metadata Management (30 minutes)
**Приоритет**: Medium
**Зависимости**: 3C.2.1

**Техническая спецификация**:
- Enhance template metadata system
- Add categories, tags, ratings
- Implement search indexing

**Metadata Features**:
- Categories and tags
- Usage statistics
- Rating and review data
- Search keywords
- Installation history

**Deliverables**:
- Enhanced metadata model
- Search indexing system
- Statistics tracking

**Acceptance Criteria**:
- [ ] Templates have rich metadata
- [ ] Search indexing works efficiently
- [ ] Statistics track usage patterns

**Unit Testing**:
- Metadata model tests
- Search indexing tests
- Statistics tests

---

### 3C.3 Community Template Browser (2.5 hours → 3 задачи)

#### 3C.3.1 Template Search and Filtering (1 hour)
**Приоритет**: High
**Зависимости**: 3C.2.2

**Техническая спецификация**:
- Implement template search functionality
- Add filtering by category, tags, rating
- Create advanced search options

**Search Features**:
- Text search in name and description
- Category filtering
- Tag-based filtering
- Rating range filtering
- Author filtering

**UI Components**:
```
TemplateBrowser.razor
├── TemplateSearch.razor (search and filters)
├── TemplateCard.razor (template preview)
├── TemplateDetails.razor (full template info)
└── TemplateInstaller.razor (install progress)
```

**Deliverables**:
- Search functionality
- Filter system
- TemplateSearch component

**Acceptance Criteria**:
- [ ] Search returns relevant results
- [ ] Filters work correctly
- [ ] Search performance is acceptable

**Unit Testing**:
- Search functionality tests
- Filter tests
- Performance tests

---

#### 3C.3.2 Template Preview and Details (1 hour)
**Приоритет**: High
**Зависимости**: 3C.3.1

**Техническая спецификация**:
- Create template preview cards
- Implement template details modal/page
- Add rating and review display

**Preview Features**:
- Template thumbnail/icon
- Description preview
- Rating display
- Category badges
- Installation count

**Details Features**:
- Full description
- Screenshots/examples
- Version history
- Dependencies list
- User reviews

**Deliverables**:
- TemplateCard component
- TemplateDetails component
- Rating/review display

**Acceptance Criteria**:
- [ ] Template previews show key information
- [ ] Details provide comprehensive information
- [ ] Ratings and reviews display correctly

**Unit Testing**:
- Template card tests
- Details display tests
- Rating system tests

---

#### 3C.3.3 Template Installation Management (30 minutes)
**Приоритет**: High
**Зависимости**: 3C.3.2

**Техническая спецификация**:
- Implement template installation process
- Add progress tracking and error handling
- Create uninstall functionality

**Installation Features**:
- Progress indication
- Dependency resolution
- Error recovery
- Installation validation
- Uninstall option

**Deliverables**:
- TemplateInstaller component
- Installation progress tracking
- Uninstall functionality

**Acceptance Criteria**:
- [ ] Templates install with progress feedback
- [ ] Installation errors handled gracefully
- [ ] Templates can be uninstalled cleanly

**Unit Testing**:
- Installation process tests
- Progress tracking tests
- Uninstall tests

---

## 3D. Advanced UI Features (4-5 hours)

### 3D.1 Keyboard Shortcuts System (1.5 hours → 2 задачи)

#### 3D.1.1 Global Hotkey Management (1 hour)
**Приоритет**: Medium
**Зависимости**: Нет

**Техническая спецификация**:
- Implement global hotkey capture system
- Create keyboard shortcut registry
- Add JavaScript interop for key events

**Hotkey Bindings**:
- `Ctrl+N`: New workflow
- `Ctrl+S`: Save workflow
- `Ctrl+E`: Execute current template
- `Ctrl+Shift+P`: Open command palette
- `F5`: Refresh template list

**Implementation Details**:
- JavaScript interop for global key capture
- Hotkey conflict resolution
- Context-sensitive shortcuts
- Customizable key bindings

**Deliverables**:
- Hotkey management service
- JavaScript interop functions
- Keyboard shortcut registry

**Acceptance Criteria**:
- [ ] All major actions accessible via keyboard
- [ ] Shortcuts work globally in application
- [ ] No conflicts with browser shortcuts

**Unit Testing**:
- Hotkey registration tests
- JavaScript interop tests
- Conflict resolution tests

---

#### 3D.1.2 Command Palette Interface (30 minutes)
**Приоритет**: Medium
**Зависимости**: 3D.1.1

**Техническая спецификация**:
- Create command palette UI component
- Implement fuzzy search for commands
- Add keyboard navigation

**Command Palette Features**:
- Fuzzy search
- Keyboard navigation
- Recent commands
- Command descriptions
- Quick execution

**Deliverables**:
- CommandPalette component
- Fuzzy search implementation
- Keyboard navigation

**Acceptance Criteria**:
- [ ] Command palette opens with Ctrl+Shift+P
- [ ] Commands searchable with fuzzy matching
- [ ] Full keyboard navigation support

**Unit Testing**:
- Command palette tests
- Fuzzy search tests
- Keyboard navigation tests

---

### 3D.2 Custom Dashboard Layouts (2 hours → 2 задачи)

#### 3D.2.1 Draggable Panel Layout System (1.5 hours)
**Приоритет**: Low
**Зависимости**: Нет

**Техническая спецификация**:
- Implement CSS Grid based layout system
- Add drag-and-drop panel reordering
- Create responsive layout breakpoints

**Layout Engine Features**:
- CSS Grid with drag-and-drop
- Panel resize handles
- Layout persistence
- Responsive breakpoints

**Panel Types**:
- Templates panel
- Quick Actions panel
- Batch Operations panel
- History panel
- Workflow Builder panel

**Deliverables**:
- Draggable layout system
- Panel components
- Layout persistence

**Acceptance Criteria**:
- [ ] Panels can be dragged and reordered
- [ ] Layout saves and restores correctly
- [ ] Responsive design works on all screen sizes

**Unit Testing**:
- Drag and drop tests
- Layout persistence tests
- Responsive design tests

---

#### 3D.2.2 Layout Preferences and Presets (30 minutes)
**Приоритет**: Low
**Зависимости**: 3D.2.1

**Техническая спецификация**:
- Create layout presets (Developer, Manager, etc.)
- Implement layout import/export
- Add layout reset functionality

**Layout Preferences**:
- Predefined layout presets
- Custom layout saving
- Layout sharing between users
- Reset to default option

**Deliverables**:
- Layout presets
- Import/export functionality
- Reset functionality

**Acceptance Criteria**:
- [ ] Users can choose from layout presets
- [ ] Custom layouts can be saved and shared
- [ ] Reset option restores default layout

**Unit Testing**:
- Layout preset tests
- Import/export tests
- Reset functionality tests

---

### 3D.3 Task Execution Scheduling (1.5 hours → 2 задачи)

#### 3D.3.1 Cron-like Scheduling System (1 hour)
**Приоритет**: Low
**Зависимости**: 3A.1.3 (WorkflowEngine)

**Техническая спецификация**:
- Implement task scheduler interface
- Create cron expression support
- Add background service for execution

**Scheduler Components**:
```csharp
public interface ITaskScheduler
{
    Task ScheduleTemplateAsync(string templateId, CronExpression schedule);
    Task UnscheduleTemplateAsync(string scheduleId);
    Task<List<ScheduledTask>> GetScheduledTasksAsync();
}
```

**Scheduling Features**:
- Cron expression parsing
- Background execution service
- Schedule persistence
- Execution history tracking

**Deliverables**:
- ITaskScheduler implementation
- Cron expression parser
- Background execution service

**Acceptance Criteria**:
- [ ] Templates can be scheduled with cron expressions
- [ ] Scheduled tasks execute at correct times
- [ ] Schedule management works correctly

**Unit Testing**:
- Cron expression tests
- Scheduling logic tests
- Background service tests

---

#### 3D.3.2 Visual Schedule Editor (30 minutes)
**Приоритет**: Low
**Зависимости**: 3D.3.1

**Техническая спецификация**:
- Create visual cron expression builder
- Add schedule preview functionality
- Implement schedule validation

**Schedule Editor Features**:
- Visual cron builder
- Schedule preview with next run times
- Human-readable schedule descriptions
- Validation and error checking

**Deliverables**:
- Visual schedule editor component
- Schedule preview functionality
- Human-readable descriptions

**Acceptance Criteria**:
- [ ] Users can create schedules visually
- [ ] Schedule preview shows next execution times
- [ ] Invalid schedules are highlighted

**Unit Testing**:
- Schedule editor tests
- Preview functionality tests
- Validation tests

---

## Phase 3 Deliverables Summary

**Основные компоненты**:
1. **Workflow Manager Foundation** - Полнофункциональный workflow engine с поддержкой условий и циклов
2. **Visual Workflow Builder** - React Flow интеграция с полным набором visual editing tools
3. **Template Marketplace** - Система import/export и community sharing templates
4. **Advanced UI Features** - Keyboard shortcuts, custom layouts, task scheduling

**Критический путь завершения**:
1. Workflow Engine Core (3A.1) → Workflow Models (3A.2) → Conditional Logic (3A.3)
2. React Flow Integration (3B.1) → Property Editors (3B.2) → Canvas Logic (3B.3) → Serialization (3B.4)
3. Template System (3C) параллельно с Advanced UI (3D)

**Готовность к выполнению LLM**:
- ✅ Все задачи разбиты на 1-3 часовые атомарные части
- ✅ Четкие acceptance criteria для каждой задачи
- ✅ Подробные технические спецификации
- ✅ Unit testing requirements определены
- ✅ Зависимости между задачами mapped

**Общее время выполнения**: 28-35 часов (31 атомарная задача)