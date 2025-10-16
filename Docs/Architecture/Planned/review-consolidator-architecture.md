# Review Consolidator - Architecture Diagram

## High-Level Architecture

```mermaid
graph TB
    subgraph "Input Layer"
        CF[Code Files]
        RT[Review Types]
    end

    subgraph "Review Consolidator Core"
        PE[Parallel Execution Engine]
        RC[Result Collector]
        CA[Consolidation Algorithm]
        RG[Report Generator]
    end

    subgraph "Parallel Review Army"
        CSR[code-style-reviewer]
        CPR[code-principles-reviewer]
        TH[test-healer]
    end

    subgraph "Output Layer"
        MR[Master Report]
        AI[Action Items]
        APP[Reviewer Appendices]
    end

    CF --> PE
    RT --> PE
    PE -->|Launch Parallel| CSR
    PE -->|Launch Parallel| CPR
    PE -->|Launch Parallel| TH

    CSR -->|Results| RC
    CPR -->|Results| RC
    TH -->|Results| RC

    RC --> CA
    CA -->|Deduplicated Issues| RG
    CA -->|Priority Aggregation| RG
    CA -->|Recommendations| RG

    RG --> MR
    RG --> AI
    RG --> APP
```

## Detailed Component Architecture

```mermaid
flowchart LR
    subgraph "Parallel Execution Engine"
        RS[Reviewer Selector]
        PL[Parallel Launcher]
        TM[Timeout Monitor]
        FB[Fallback Handler]

        RS --> PL
        PL --> TM
        TM --> FB
    end

    subgraph "Consolidation Algorithm"
        DD[Deduplication Engine]
        PA[Priority Aggregator]
        CW[Confidence Weighting]
        RS2[Recommendation Synthesis]

        DD --> PA
        PA --> CW
        CW --> RS2
    end

    subgraph "Report Generation"
        ES[Executive Summary]
        IG[Issue Grouping]
        TG[Theme Generator]
        MF[Markdown Formatter]

        ES --> MF
        IG --> MF
        TG --> MF
    end
```

## Data Flow Architecture

```mermaid
sequenceDiagram
    participant User
    participant RC as Review Consolidator
    participant PE as Parallel Engine
    participant CSR as code-style-reviewer
    participant CPR as code-principles-reviewer
    participant TH as test-healer
    participant CA as Consolidation Algorithm
    participant RG as Report Generator

    User->>RC: Initiate Review(files[])
    RC->>PE: Launch Parallel Reviews

    par Parallel Execution
        PE->>CSR: Review Files (Task tool)
        CSR-->>PE: Style Issues[]
    and
        PE->>CPR: Review Files (Task tool)
        CPR-->>PE: Principle Violations[]
    and
        PE->>TH: Analyze Tests (Task tool)
        TH-->>PE: Test Issues[]
    end

    PE->>CA: Consolidate Results
    CA->>CA: Deduplicate Issues
    CA->>CA: Aggregate Priorities
    CA->>CA: Calculate Confidence
    CA->>RG: Consolidated Data

    RG->>RG: Generate Master Report
    RG->>RG: Create Action Items
    RG->>RG: Format Appendices

    RG-->>User: Consolidated Report
```

## Cycle Protection Architecture

```mermaid
stateDiagram-v2
    [*] --> InitialReview
    InitialReview --> IssuesFound: P0/P1 Issues
    InitialReview --> Complete: No Issues

    IssuesFound --> FixIssues: Execute Fixes
    FixIssues --> SecondReview: Re-review

    SecondReview --> IssuesResolved: Issues Fixed
    SecondReview --> PersistentIssues: Issues Remain

    IssuesResolved --> Complete
    PersistentIssues --> Escalation: Max 2 Cycles

    Escalation --> UserIntervention: Manual Review
    UserIntervention --> Complete

    Complete --> [*]
```

## Issue Deduplication Flow

```mermaid
graph TD
    subgraph "Issue Collection"
        I1[Issue from CSR]
        I2[Issue from CPR]
        I3[Issue from TH]
    end

    subgraph "Deduplication Process"
        EM[Exact Match Check]
        SS[Semantic Similarity]
        GR[Group Related]
        MG[Merge Groups]
    end

    subgraph "Priority Calculation"
        P0[P0: Critical]
        P1[P1: Warning]
        P2[P2: Improvement]
    end

    I1 --> EM
    I2 --> EM
    I3 --> EM

    EM -->|No Match| SS
    EM -->|Match Found| MG

    SS -->|>80% Similar| GR
    SS -->|<80% Similar| P0

    GR --> MG
    MG --> P0
    MG --> P1
    MG --> P2

    P0 -->|ANY reviewer| Critical
    P1 -->|MAJORITY| Warning
    P2 -->|DEFAULT| Improvement
```

## Integration Points

```mermaid
graph LR
    subgraph "Upstream Agents"
        PTE[plan-task-executor]
        PTC[plan-task-completer]
        USR[User Request]
    end

    subgraph "Review Consolidator"
        RC[review-consolidator]
    end

    subgraph "Downstream Agents"
        PTE2[plan-task-executor]
        PCV[pre-completion-validator]
        GWM[git-workflow-manager]
    end

    PTE -->|After Code Written| RC
    PTC -->|Before Completion| RC
    USR -->|Manual Review| RC

    RC -->|P0 Issues Found| PTE2
    RC -->|All Clear| PCV
    RC -->|Ready to Commit| GWM
```

## Performance Optimization Architecture

```mermaid
graph TB
    subgraph "Caching Layer"
        CC[Result Cache<br/>TTL: 15min]
        HC[Hash Cache<br/>for Deduplication]
    end

    subgraph "Resource Management"
        PL[Parallel Limiter<br/>Max: 3-5]
        TM[Timeout Manager<br/>5min/reviewer]
        ET[Early Termination<br/>on P0]
    end

    subgraph "Optimization Strategies"
        LA[Lazy Loading]
        PS[Progressive Streaming]
        BA[Batch Processing]
    end

    CC --> LA
    HC --> BA
    PL --> PS
    TM --> ET
```

## Report Structure Hierarchy

```mermaid
graph TD
    MR[Master Report]

    MR --> ES[Executive Summary]
    MR --> CI[Critical Issues P0]
    MR --> WI[Warnings P1]
    MR --> RI[Recommendations P2]
    MR --> CT[Common Themes]
    MR --> AI[Action Items]
    MR --> APP[Appendices]

    CI --> CID[Issue Details]
    CI --> CIF[Immediate Actions]

    WI --> WID[Warning Details]
    WI --> WIF[Recommended Fixes]

    RI --> RID[Improvement Details]
    RI --> RIF[Optional Enhancements]

    CT --> TF[Theme Frequency]
    CT --> TR[Theme Recommendations]

    AI --> AIP[Prioritized Tasks]
    AI --> AIE[Effort Estimates]

    APP --> CSA[Code Style Appendix]
    APP --> CPA[Code Principles Appendix]
    APP --> THA[Test Healer Appendix]
```

## Dependencies Between Plan Sections

### Phase Dependencies
- Phase 1 (Foundation) → Phase 2 (Parallel Execution): Agent specs required for implementation
- Phase 2 → Phase 3 (Consolidation): Need results to consolidate
- Phase 3 → Phase 4 (Report Generation): Consolidated data required for reporting
- Phase 4 → Phase 5 (Cycle Protection): Report format needed for cycle tracking
- Phase 5 → Phase 6 (Testing): Complete system needed for integration tests

### Cross-Component Dependencies
- Parallel Execution Engine ← Individual Reviewer Specifications
- Consolidation Algorithm ← Result Collection Framework
- Report Generator ← Consolidation Algorithm + Report Templates
- Cycle Protection ← All Core Components

### External Dependencies
- Task tool (Claude Code infrastructure)
- Existing review rules (*.mdc files)
- AGENTS_ARCHITECTURE.md standards
- common-plan-generator.mdc guidelines

## Error Handling & Recovery

```mermaid
flowchart TD
    Start([Review Request]) --> PE{Parallel Execution}

    PE -->|Success| RC[Collect Results]
    PE -->|Timeout| PT[Partial Results]
    PE -->|Failure| FB[Fallback Sequential]

    RC --> CA[Consolidate]
    PT --> CA
    FB --> SE[Sequential Execution]
    SE --> CA

    CA -->|Success| RG[Generate Report]
    CA -->|Error| ER[Error Recovery]

    ER --> LOG[Log Error]
    LOG --> PR[Partial Report]

    RG --> Complete([Report Delivered])
    PR --> Complete
```

## Scalability Considerations

### Current Design (v1.0)
- **Files**: Up to 100 files per review
- **Reviewers**: 3-5 parallel reviewers
- **Issues**: Up to 1000 issues before consolidation
- **Performance**: <6 minutes total review time

### Future Scalability (v2.0)
- **Files**: 1000+ files with progressive loading
- **Reviewers**: 10+ with queue management
- **Issues**: 10,000+ with streaming consolidation
- **Performance**: <10 minutes with caching

### Bottleneck Mitigation
- Parallel execution prevents sequential bottleneck
- Caching reduces repeated analysis
- Early termination on critical issues
- Progressive report streaming for large results