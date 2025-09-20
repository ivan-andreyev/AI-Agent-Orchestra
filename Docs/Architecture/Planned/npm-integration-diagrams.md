# NPM Integration Component Diagrams

**Type**: Planned Architecture Diagrams
**Plan Reference**: [Phase 3B NPM Package Management](../npm-package-management-architecture.md)
**Last Updated**: 2025-09-20
**Status**: Foundation Phase Architecture

## System-Level NPM Integration Architecture

```mermaid
graph TB
    subgraph "Development Environment"
        DEV[Developer Machine]
        IDE[IDE/Editor]
        CLI[Command Line]
    end

    subgraph "NPM Ecosystem"
        NPM[NPM CLI 10.9.2]
        REG[NPM Registry]
        PKG[Package Cache]
    end

    subgraph "AI Agent Orchestra Project"
        ROOT[Root package.json]
        WEB[Web package.json]
        NM[node_modules]

        subgraph "Build Pipeline"
            WP[Webpack]
            TS[TypeScript]
            BB[Babel]
        end
    end

    subgraph "Blazor Server Application"
        BA[Blazor App]
        SA[Static Assets wwwroot]
        RF[React Flow Components]
        WB[Workflow Builder UI]
    end

    subgraph "Browser Runtime"
        BR[Browser]
        JS[JavaScript Engine]
        DOM[DOM Rendering]
    end

    DEV --> CLI
    CLI --> NPM
    NPM --> REG
    REG --> PKG
    PKG --> NM

    ROOT --> NM
    WEB --> NM
    NM --> WP
    WP --> TS
    WP --> BB

    WP --> SA
    SA --> BA
    BA --> BR
    BR --> JS
    JS --> DOM

    RF --> WB
    SA --> RF
```

## Package Management Layer Architecture

```mermaid
graph TD
    subgraph "Package.json Configuration Layer"
        ROOT_PKG[Root package.json<br/>Project Coordination]
        WEB_PKG[Web package.json<br/>Frontend Dependencies]
    end

    subgraph "Dependency Resolution Layer"
        NPM_CLI[NPM CLI<br/>Package Manager]
        LOCK[package-lock.json<br/>Version Lock]
        NODE_MOD[node_modules<br/>Installed Packages]
    end

    subgraph "React Flow Ecosystem"
        REACT[react ^18.2.0]
        REACT_DOM[react-dom ^18.2.0]
        REACT_FLOW[react-flow-renderer ^10.3.17]
        TYPES_REACT[@types/react ^18.2.0]
        TYPES_FLOW[@types/react-flow-renderer ^1.0.0]
    end

    subgraph "Build Tool Ecosystem"
        WEBPACK[webpack ^5.88.0]
        WEBPACK_CLI[webpack-cli ^5.1.0]
        BABEL_LOADER[babel-loader ^9.1.0]
        BABEL_CORE[@babel/core ^7.22.0]
        BABEL_REACT[@babel/preset-react ^7.22.0]
        BABEL_ENV[@babel/preset-env ^7.22.0]
        TYPESCRIPT[typescript ^5.1.0]
        TS_LOADER[ts-loader ^9.4.0]
    end

    ROOT_PKG --> NPM_CLI
    WEB_PKG --> NPM_CLI
    NPM_CLI --> LOCK
    NPM_CLI --> NODE_MOD

    NODE_MOD --> REACT
    NODE_MOD --> REACT_DOM
    NODE_MOD --> REACT_FLOW
    NODE_MOD --> TYPES_REACT
    NODE_MOD --> TYPES_FLOW

    NODE_MOD --> WEBPACK
    NODE_MOD --> WEBPACK_CLI
    NODE_MOD --> BABEL_LOADER
    NODE_MOD --> BABEL_CORE
    NODE_MOD --> BABEL_REACT
    NODE_MOD --> BABEL_ENV
    NODE_MOD --> TYPESCRIPT
    NODE_MOD --> TS_LOADER

    style ROOT_PKG fill:#e1f5fe
    style WEB_PKG fill:#e1f5fe
    style REACT_FLOW fill:#fff3e0
    style WEBPACK fill:#f3e5f5
```

## Build Pipeline Integration Flow

```mermaid
sequenceDiagram
    participant Dev as Developer
    participant NPM as NPM CLI
    participant FS as File System
    participant WP as Webpack
    participant BA as Blazor App
    participant BR as Browser

    Note over Dev,BR: NPM Package Management Integration Flow

    Dev->>NPM: npm install
    NPM->>FS: Read package.json
    FS->>NPM: Return dependency list
    NPM->>FS: Download & install to node_modules
    FS->>NPM: Installation complete

    Dev->>NPM: npm run build
    NPM->>WP: Execute webpack build
    WP->>FS: Read React Flow sources
    FS->>WP: Return component files
    WP->>FS: Write bundled JS to wwwroot

    Dev->>BA: dotnet run
    BA->>FS: Serve static assets from wwwroot
    FS->>BA: Return bundled JavaScript files
    BA->>BR: Deliver React Flow components
    BR->>Dev: Render workflow builder UI
```

## Component Interaction Patterns

### Pattern 1: Development Workflow Integration

```mermaid
graph LR
    subgraph "Developer Actions"
        A1[Add NPM Dependency]
        A2[Update package.json]
        A3[Run npm install]
        A4[Import in TypeScript]
        A5[Build with webpack]
    end

    subgraph "System Response"
        S1[Dependency Downloaded]
        S2[Types Available]
        S3[IntelliSense Active]
        S4[Bundle Created]
        S5[Ready for Browser]
    end

    A1 --> A2
    A2 --> A3
    A3 --> S1
    S1 --> S2
    A4 --> S3
    S3 --> A5
    A5 --> S4
    S4 --> S5

    style A1 fill:#e8f5e8
    style S5 fill:#fff3e0
```

### Pattern 2: React Flow Component Loading

```mermaid
graph TD
    subgraph "NPM Layer"
        NPM_RF[react-flow-renderer<br/>in node_modules]
        NPM_TYPES[TypeScript Definitions<br/>@types packages]
    end

    subgraph "Build Layer"
        WP_ENTRY[Webpack Entry Point<br/>workflow-builder.js]
        WP_BUNDLE[Webpack Bundle<br/>compiled output]
    end

    subgraph "Blazor Layer"
        BL_STATIC[Static File Serving<br/>wwwroot assets]
        BL_INTEROP[JavaScript Interop<br/>IJSRuntime]
    end

    subgraph "Browser Layer"
        BR_ENGINE[JavaScript Engine<br/>V8/SpiderMonkey]
        BR_REACT[React Rendering<br/>Virtual DOM]
        BR_FLOW[React Flow Canvas<br/>SVG/Canvas]
    end

    NPM_RF --> WP_ENTRY
    NPM_TYPES --> WP_ENTRY
    WP_ENTRY --> WP_BUNDLE
    WP_BUNDLE --> BL_STATIC
    BL_STATIC --> BL_INTEROP
    BL_INTEROP --> BR_ENGINE
    BR_ENGINE --> BR_REACT
    BR_REACT --> BR_FLOW

    style NPM_RF fill:#fff3e0
    style BR_FLOW fill:#e8f5e8
```

## Data Flow Architecture

### NPM Dependency Resolution Flow

```mermaid
flowchart TD
    START([npm install command]) --> READ_PKG{Read package.json}
    READ_PKG --> RESOLVE[Resolve Dependencies]
    RESOLVE --> CHECK_CACHE{Check NPM Cache}
    CHECK_CACHE -->|Hit| LINK[Link from Cache]
    CHECK_CACHE -->|Miss| DOWNLOAD[Download from Registry]
    DOWNLOAD --> EXTRACT[Extract Package]
    EXTRACT --> INSTALL[Install to node_modules]
    LINK --> INSTALL
    INSTALL --> VALIDATE[Validate Installation]
    VALIDATE --> SUCCESS([Installation Complete])

    style START fill:#e8f5e8
    style SUCCESS fill:#e8f5e8
    style DOWNLOAD fill:#fff3e0
```

### Build Asset Flow

```mermaid
flowchart TD
    SRC[TypeScript Source Files] --> WP_LOAD[Webpack Loader]
    WP_LOAD --> TS_COMPILE[TypeScript Compilation]
    TS_COMPILE --> BABEL_TRANSFORM[Babel Transformation]
    BABEL_TRANSFORM --> BUNDLE[Bundle Creation]
    BUNDLE --> MINIFY[Minification]
    MINIFY --> OUTPUT[Output to wwwroot]
    OUTPUT --> BLAZOR_SERVE[Blazor Static Serving]
    BLAZOR_SERVE --> BROWSER[Browser Execution]

    style SRC fill:#e1f5fe
    style BROWSER fill:#e8f5e8
```

## Integration Boundary Architecture

```mermaid
graph TB
    subgraph "NPM Ecosystem Boundary"
        direction TB
        A1[NPM Registry]
        A2[Package Cache]
        A3[CLI Tools]
    end

    subgraph "Project Boundary"
        direction TB
        B1[package.json Files]
        B2[node_modules]
        B3[Build Configuration]
    end

    subgraph ".NET Ecosystem Boundary"
        direction TB
        C1[Blazor Server]
        C2[Static File Middleware]
        C3[IJSRuntime]
    end

    subgraph "Browser Ecosystem Boundary"
        direction TB
        D1[JavaScript Engine]
        D2[React Runtime]
        D3[React Flow Canvas]
    end

    A1 -.->|Package Download| B2
    A2 -.->|Cache Access| B2
    A3 -.->|Command Execution| B1

    B1 -->|Configuration| B3
    B2 -->|Dependencies| B3
    B3 -->|Build Output| C2

    C1 -->|Hosting| C2
    C2 -->|Asset Serving| D1
    C3 -->|Interop| D1

    D1 -->|Runtime| D2
    D2 -->|Rendering| D3

    style A1 fill:#ffecb3
    style B1 fill:#e1f5fe
    style C1 fill:#e8f5e8
    style D1 fill:#fce4ec
```

## Error Handling & Fallback Patterns

### NPM Installation Error Flow

```mermaid
graph TD
    INSTALL[npm install] --> CHECK{Installation Success?}
    CHECK -->|Success| VALIDATE[Validate node_modules]
    CHECK -->|Failure| ERROR_HANDLER[Error Handler]

    ERROR_HANDLER --> NETWORK_CHECK{Network Available?}
    NETWORK_CHECK -->|No| OFFLINE_MODE[Offline Mode]
    NETWORK_CHECK -->|Yes| RETRY{Retry Count < 3?}

    RETRY -->|Yes| CLEAR_CACHE[Clear NPM Cache]
    CLEAR_CACHE --> INSTALL
    RETRY -->|No| MANUAL_INTERVENTION[Manual Intervention Required]

    VALIDATE --> BUILD_READY[Ready for Build]
    OFFLINE_MODE --> CACHED_DEPS[Use Cached Dependencies]
    CACHED_DEPS --> BUILD_READY

    style ERROR_HANDLER fill:#ffcdd2
    style BUILD_READY fill:#c8e6c9
```

## Legend

```mermaid
graph TD
    A[NPM Ecosystem Component] --> B[Build Pipeline Component]
    B --> C[Blazor/.NET Component]
    C --> D[Browser/JavaScript Component]

    style A fill:#fff3e0
    style B fill:#f3e5f5
    style C fill:#e8f5e8
    style D fill:#e1f5fe
```

**Color Coding:**
- 🟡 **NPM Ecosystem** (#fff3e0): Package management, registry, CLI
- 🟣 **Build Pipeline** (#f3e5f5): Webpack, TypeScript, Babel
- 🟢 **Blazor/.NET** (#e8f5e8): Server application, static serving
- 🔵 **Browser/JavaScript** (#e1f5fe): Runtime, React, React Flow

---

These diagrams provide comprehensive visualization of NPM package management integration within the AI Agent Orchestra architecture, showing clear interaction patterns and data flows between all system components.