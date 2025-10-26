# ProcessDiscoveryService Fix Work Plan

## Overview

**Problem**: ProcessDiscoveryService fails to find Claude Code processes for valid SessionIds, blocking the Agent Terminal feature.
**Goal**: Fix process discovery logic and add UI fallback for manual input.
**Estimate**: 3-4 days (24-32 hours total)
**Priority**: P0 (Critical - blocks core functionality)
**Architecture**: See [ProcessDiscoveryService-Fix-WorkPlan-Architecture.md](ProcessDiscoveryService-Fix-WorkPlan-Architecture.md)

## Success Criteria

1. ✅ ProcessDiscoveryService successfully discovers Claude Code processes with SessionId
2. ✅ UI provides fallback for manual ProcessId/SocketPath input
3. ✅ User can connect to Claude Code terminal in both auto and manual modes
4. ✅ Robust error handling and diagnostics
5. ✅ 95%+ test coverage for process discovery logic

## Technical Context

**Root Cause Hypotheses**:
1. SessionId extraction regex too strict or misses formats
2. Claude Code processes may not have SessionId in command line
3. Stale SessionId in database vs actual running processes
4. Process command-line extraction failures on certain systems
5. Cache returning outdated data

**Constraints**:
- Must maintain backward compatibility
- Must work on Windows and Linux
- Discovery must complete within 1-2 seconds
- Must handle process exit/restart scenarios

---

## Phase 1: Diagnostic Investigation & Root Cause Analysis

**Duration**: 4-6 hours
**Goal**: Identify exact failure points and gather diagnostic data

### 1.1 Enhanced Diagnostic Logging [x] ✅ COMPLETE

**Completed**: 2025-10-26
**Duration**: ~4 hours (including review cycles)
**Review Confidence**: 95% (pre-completion-validator: APPROVED)

**DIAGNOSTIC FINDINGS**:
- **Discovered**: 6 Claude Code processes running
- **Problem Confirmed**: 0 processes have SessionId extracted (ProcessesWithSessionId: 0)
- **Command-line pattern**: All processes use `--resume` or no arguments
- **ROOT CAUSE IDENTIFIED**: SessionId is NOT in command-line arguments
- **Snapshot saved**: `docs/diagnostics/process-discovery-snapshot.json`
- **Analysis document**: `docs/diagnostics/phase-1.1-diagnostic-analysis.md`

**Key Insight**: SessionId is NOT passed via command-line arguments. Need to investigate:
1. Working directory (process started in session-specific directory?)
2. Configuration files (.claude/session.json or similar)
3. Environment variables (though Windows WMI doesn't provide them easily)

**Deliverables Created**:
- DiagnosticsController.cs (323 lines, 3 endpoints)
- process-discovery-snapshot.json (raw diagnostic data)
- phase-1.1-diagnostic-analysis.md (comprehensive analysis)

**Review Feedback**:
- code-principles-reviewer: Minor DRY/KISS issues (acceptable for diagnostic tool)
- code-style-reviewer: Minor file organization issues (acceptable for diagnostic tool)

#### 1.1A: Add Detailed Process Discovery Logging
- [x] File: `src/Orchestra.Core/Services/ProcessDiscoveryService.cs` (already has logging)
- [x] Add logging for each discovered process (already implemented)
- [x] Log SessionId extraction attempts and failures (already implemented)
- [ ] Log cache hit/miss statistics (future enhancement)
- [ ] Add timing measurements (future enhancement)

#### 1.1B: Create Diagnostic Endpoint ✅
- [x] File: `src/Orchestra.API/Controllers/DiagnosticsController.cs` (created)
- [x] Create `GET /api/diagnostics/processes` endpoint (working)
- [x] Return all discovered processes with details (working)
- [x] Include SessionId extraction results (working, shows null SessionIds)
- [x] Add cache clear endpoint (`POST /api/diagnostics/cache/clear`)

#### 1.1C: Integration and Testing ✅
- [x] Register DiagnosticsController in DI (automatic via controller discovery)
- [x] Test endpoint with curl (working at http://localhost:55002/api/diagnostics/processes)
- [x] Verify logging output (verified in BashOutput)

### 1.2 Real Claude Code Process Analysis

#### 1.2A: Manual Process Investigation
- [ ] Find actual Claude Code processes
- [ ] Document command-line formats
- [ ] Identify SessionId locations
- [ ] Create test data set

#### 1.2B: Create Process Format Documentation
- [ ] File: `docs/Technical/claude-code-process-formats.md`
- [ ] Document all command-line formats
- [ ] Map SessionId locations
- [ ] Include OS-specific examples

### 1.3 SessionId Correlation Analysis

#### 1.3A: Database SessionId Audit
- [ ] Query stored SessionIds
- [ ] Compare with running processes
- [ ] Identify mismatch patterns
- [ ] Document SessionId lifecycle

#### 1.3B: Create SessionId Mapping Service
- [ ] File: `src/Orchestra.Core/Services/SessionIdMappingService.cs`
- [ ] Map database SessionIds to processes
- [ ] Handle SessionId rotation
- [ ] Add fallback strategies

**Validation Checkpoint**: Verify root cause identified before proceeding

---

## Phase 2: ProcessDiscoveryService Core Fixes

**Duration**: 8-10 hours
**Goal**: Fix discovery logic to handle all process formats

### 2.1 Enhanced Process Detection

#### 2.1A: Expand Process Name Detection
- [ ] File: `src/Orchestra.Core/Services/ProcessDiscoveryService.cs`
- [ ] Add detection for electron.exe, claude.exe, python.exe
- [ ] Support additional Node.js entry points
- [ ] Implement process name patterns

#### 2.1B: Improve Command Line Extraction
- [ ] Enhance `GetCommandLine` method
- [ ] Add fallback for access denied
- [ ] Implement retry with elevated permissions
- [ ] Add platform optimizations

#### 2.1C: Testing Process Detection
- [ ] Create unit tests for each type
- [ ] Test with mocked data
- [ ] Validate cross-platform

### 2.2 Robust SessionId Extraction (Split into 2.2A and 2.2B)

#### 2.2A: Multi-Pattern SessionId Extraction (≤15 tool calls)
- [ ] File: `src/Orchestra.Core/Services/ProcessDiscoveryService.cs`
- [ ] Method: `ExtractSessionId`
- [ ] Implement extraction strategies:
  - Direct UUID in command line
  - --session-id parameter
  - Environment variables
- [ ] Add logging for each attempt

#### 2.2B: SessionId Extraction Pipeline Implementation (≤15 tool calls)
- [ ] File: `src/Orchestra.Core/Services/SessionIdExtractionPipeline.cs`
- [ ] Implement chain of responsibility
- [ ] Create separate extractors:
  - WorkingDirectoryExtractor
  - ConfigurationFileExtractor
- [ ] Configure priority order
- [ ] Register pipeline in DI

### 2.3 Intelligent Cache Management

#### 2.3A: Smart Cache Invalidation
- [ ] File: `src/Orchestra.Core/Services/ProcessDiscoveryService.cs`
- [ ] Detect process changes
- [ ] Per-SessionId cache entries
- [ ] Sliding expiration
- [ ] Cache statistics

#### 2.3B: Cache Monitoring
- [ ] Add hit/miss metrics
- [ ] Log invalidation events
- [ ] Expose via diagnostics
- [ ] Manual clear capability

**Validation Checkpoint**: Test discovery with known processes

---

## Phase 3: UI Fallback Implementation

**Duration**: 6-8 hours
**Goal**: Add manual input UI for ProcessId/SocketPath

### 3.1 Enhanced Connection Dialog

#### 3.1A: Update Connection Dialog UI
- [ ] File: `src/Orchestra.Web/Components/AgentTerminal/AgentTerminalComponent.razor`
- [ ] Add "Advanced Options" section
- [ ] Add ProcessId and SocketPath inputs
- [ ] Add Working Directory field
- [ ] Add "Auto-Detect" retry button

#### 3.1B: Input Validation
- [ ] Validate ProcessId exists
- [ ] Validate SocketPath format
- [ ] Real-time feedback
- [ ] Add tooltips and examples

#### 3.1C: Style and UX
- [ ] File: `AgentTerminalComponent.razor.css`
- [ ] Style advanced options
- [ ] Add loading indicators
- [ ] Improve error display

### 3.2 Process Browser Component

#### 3.2A: Create Process List
- [ ] File: `src/Orchestra.Web/Components/AgentTerminal/ProcessBrowser.razor`
- [ ] Display Claude processes
- [ ] Show process details
- [ ] Allow selection
- [ ] Add refresh

#### 3.2B: Process Service Client
- [ ] File: `src/Orchestra.Web/Services/ProcessDiscoveryClient.cs`
- [ ] Call diagnostics API
- [ ] Handle responses
- [ ] Error handling
- [ ] Add caching

#### 3.2C: Integration
- [ ] Add to connection dialog
- [ ] Wire selection events
- [ ] Pass to connection
- [ ] Update parameters

### 3.3 Connection Persistence

#### 3.3A: Save Parameters
- [ ] Store successful connections
- [ ] Associate with AgentId
- [ ] Local storage persistence
- [ ] "Use Last Known" option

#### 3.3B: Connection History
- [ ] Track last 5 connections
- [ ] Dropdown selection
- [ ] Include timestamps
- [ ] Clear history option

**Validation Checkpoint**: Test manual and auto modes

---

## Phase 4: Testing & Validation

**Duration**: 4-6 hours
**Goal**: Comprehensive testing across platforms

### 4.1 Unit Testing

#### 4.1A: ProcessDiscoveryService Tests
- [ ] File: `src/Orchestra.Tests/Services/ProcessDiscoveryServiceTests.cs`
- [ ] Test all process formats
- [ ] Test extraction strategies
- [ ] Test cache behavior
- [ ] 95%+ coverage

#### 4.1B: Pipeline Tests
- [ ] File: `src/Orchestra.Tests/Services/SessionIdExtractionPipelineTests.cs`
- [ ] Test each extractor
- [ ] Test ordering
- [ ] Test fallback
- [ ] Test performance

### 4.2 Integration Testing

#### 4.2A: Hub Tests
- [ ] File: `src/Orchestra.Tests/Hubs/AgentInteractionHubTests.cs`
- [ ] Test auto-discovery
- [ ] Test manual flow
- [ ] Test error handling
- [ ] Test SignalR groups

#### 4.2B: UI Tests
- [ ] Test connection dialog
- [ ] Test process browser
- [ ] Test manual input
- [ ] Test persistence

### 4.3 Platform Testing

#### 4.3A: Windows Testing
- [ ] Test Windows 10/11
- [ ] Different Node versions
- [ ] Multiple instances
- [ ] Permission scenarios

#### 4.3B: Linux Testing
- [ ] Test Ubuntu/Debian
- [ ] Test /proc access
- [ ] Different shells
- [ ] Permissions

**Validation Checkpoint**: All tests passing on both platforms

---

## Phase 5: Documentation & Monitoring

**Duration**: 2-4 hours
**Goal**: Document algorithm and add monitoring

### 5.1 Technical Documentation

#### 5.1A: Algorithm Documentation
- [ ] File: `docs/Technical/process-discovery-algorithm.md`
- [ ] Document strategies
- [ ] Add flowcharts
- [ ] Troubleshooting guide
- [ ] Configuration options

#### 5.1B: API Documentation
- [ ] Update Swagger
- [ ] Document endpoints
- [ ] Add examples
- [ ] Error reference

### 5.2 User Documentation

#### 5.2A: Connection Guide
- [ ] File: `docs/User/terminal-connection-guide.md`
- [ ] Step-by-step guide
- [ ] Troubleshooting
- [ ] Manual input guide
- [ ] FAQ section

#### 5.2B: Update README
- [ ] Add feature description
- [ ] Quick start guide
- [ ] Requirements
- [ ] Screenshots

### 5.3 Monitoring

#### 5.3A: Metrics Collection
- [ ] Track success rate
- [ ] Monitor connections
- [ ] Log failures
- [ ] Performance metrics

#### 5.3B: Health Check
- [ ] File: `src/Orchestra.API/HealthChecks/ProcessDiscoveryHealthCheck.cs`
- [ ] Check service status
- [ ] Verify cache
- [ ] Test permissions
- [ ] Report health

**Final Validation**: Complete end-to-end testing

---

## Risk Mitigation

### Technical Risks

1. **Process Access Denied**
   - Mitigation: Graceful degradation to manual input

2. **SessionId Format Changes**
   - Mitigation: Extensible extraction pipeline

3. **Performance Impact**
   - Mitigation: Aggressive caching, async operations

### Operational Risks

1. **Breaking Changes**
   - Mitigation: Feature flag for new behavior

2. **Cross-Platform Issues**
   - Mitigation: Platform-specific implementations

## Success Metrics

- Discovery success rate > 90%
- Connection time < 2 seconds
- Manual fallback usage < 10%
- Zero critical bugs in first week
- User satisfaction score > 4.5/5

## Follow-up Items

- [ ] Process health monitoring
- [ ] Process restart capability
- [ ] Connection pooling
- [ ] Telemetry and analytics