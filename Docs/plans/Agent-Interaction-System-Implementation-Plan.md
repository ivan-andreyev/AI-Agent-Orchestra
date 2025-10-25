# Agent Interaction System Implementation Plan

## Executive Summary

**Goal**: Implement bidirectional real-time communication between Orchestra and external agents (Claude Code, Cursor) through their native interaction interfaces.

**Architecture Document**: [Docs/Architecture/Planned/agent-interaction-system.md](../../Architecture/Planned/agent-interaction-system.md)

**Total Estimate**: 5-7 days (40-56 hours)

**Priority**: P1 (High)

**Status**: 🟡 In Progress - Phase 1.1 COMPLETE (Core Interfaces)

## Current State Assessment

### ✅ Completed (Phase 1.1)
- Core interfaces: `IAgentConnector`, `AgentConnectionParams`, `ConnectionStatus`
- Models: `ConnectionResult`, `CommandResult`, `DisconnectionResult`
- Event args: `ConnectionStatusChangedEventArgs`
- Files created in `Orchestra.Core.Services.Connectors` namespace

### ❌ Not Implemented
- TerminalAgentConnector (named pipes/unix sockets)
- AgentSessionManager
- IAgentOutputBuffer/AgentOutputBuffer
- AgentInteractionHub (SignalR)
- AgentTerminalComponent (Blazor frontend)
- Integration tests
- Documentation

## Success Criteria

### Functional Requirements
- ✅ Connect to existing Claude Code terminal session
- ✅ Real-time output streaming through SignalR (<100ms latency)
- ✅ Send commands to terminal from UI
- ✅ Auto-disconnect on terminal closure
- ✅ Support multiple simultaneous connections

### Non-Functional Requirements
- ✅ Output latency <100ms (stdout to UI)
- ✅ Throughput >1000 lines/second
- ✅ Memory usage <100MB for 10 sessions
- ✅ Code coverage >80%
- ✅ Zero memory leaks

## Risk Assessment

### Technical Risks
1. **Cross-platform IPC complexity** (Medium)
   - Mitigation: Use Unix Domain Sockets (cross-platform since Windows 10)

2. **Real-time streaming performance** (Low)
   - Mitigation: SignalR IAsyncEnumerable proven for terminal streaming

3. **Memory management for buffers** (Medium)
   - Mitigation: Circular buffer with fixed size (10,000 lines)

## Implementation Phases

### Phase 1: Core Infrastructure (2-3 days) - PARTIAL COMPLETE

#### ✅ Phase 1.1: Core Interfaces (COMPLETE)
**Status**: ✅ COMPLETE (commit 8ce7e5e)
- Created `IAgentConnector` interface
- Created supporting models and enums
- Created event args classes

#### Phase 1.2: TerminalAgentConnector Implementation (8-10 hours)
[Details in phase-1-core-infrastructure.md](./phase-1-core-infrastructure.md#phase-12)
- **Task 1.2B Decomposed**: [task-1.2b-terminal-connector.md](./phase-1-core-infrastructure/task-1.2b-terminal-connector.md)

#### Phase 1.3: AgentSessionManager Implementation (6-8 hours)
[Details in phase-1-core-infrastructure.md](./phase-1-core-infrastructure.md#phase-13)

#### Phase 1.4: Output Buffer Implementation (4-6 hours)
[Details in phase-1-core-infrastructure.md](./phase-1-core-infrastructure.md#phase-14)

#### Phase 1.5: Unit Tests for Core Components (4-5 hours)
[Details in phase-1-core-infrastructure.md](./phase-1-core-infrastructure.md#phase-15)

**Phase 1 Total**: 22-29 hours (Phase 1.1 complete, 1.2-1.5 remaining)

### Phase 2: SignalR Integration (1-2 days)

#### Phase 2.1: AgentInteractionHub Creation (4-5 hours)
[Details in phase-2-signalr-integration.md](./phase-2-signalr-integration.md#phase-21)

#### Phase 2.2: Streaming Implementation (3-4 hours)
[Details in phase-2-signalr-integration.md](./phase-2-signalr-integration.md#phase-22)

#### Phase 2.3: Integration Tests (3-4 hours)
[Details in phase-2-signalr-integration.md](./phase-2-signalr-integration.md#phase-23)

**Phase 2 Total**: 10-13 hours

### Phase 3: Frontend Component (1-2 days)

#### Phase 3.1: Terminal Component Creation (4-5 hours)
[Details in phase-3-frontend-component.md](./phase-3-frontend-component.md#phase-31)
- **Task 3.1B Decomposed**: [task-3.1b-terminal-styling.md](./phase-3-frontend-component/task-3.1b-terminal-styling.md)

#### Phase 3.2: SignalR Client Integration (3-4 hours)
[Details in phase-3-frontend-component.md](./phase-3-frontend-component.md#phase-32)
- **Task 3.2B Decomposed**: [task-3.2b-output-streaming.md](./phase-3-frontend-component/task-3.2b-output-streaming.md)

#### Phase 3.3: UI Polish & Styling (3-4 hours)
[Details in phase-3-frontend-component.md](./phase-3-frontend-component.md#phase-33)

**Phase 3 Total**: 10-13 hours

### Phase 4: Testing & Documentation (1 day)

#### Phase 4.1: End-to-End Testing (3-4 hours)
[Details in phase-4-testing-documentation.md](./phase-4-testing-documentation.md#phase-41)
- **Task 4.1B Decomposed**: [task-4.1b-e2e-tests.md](./phase-4-testing-documentation/task-4.1b-e2e-tests.md)

#### Phase 4.2: Performance Testing (2-3 hours)
[Details in phase-4-testing-documentation.md](./phase-4-testing-documentation.md#phase-42)

#### Phase 4.3: Documentation (3-4 hours)
[Details in phase-4-testing-documentation.md](./phase-4-testing-documentation.md#phase-43)

**Phase 4 Total**: 8-11 hours

## Dependencies & Prerequisites

### Technical Dependencies
- .NET 9.0
- ASP.NET Core SignalR
- Blazor WebAssembly
- xUnit + Moq + FluentAssertions

### External Dependencies
- Claude Code agent with terminal capability
- Windows 10+ or Linux/macOS for Unix Domain Sockets

## Validation Checkpoints

### Phase 1 Checkpoint
- [ ] TerminalAgentConnector can connect via UDS
- [ ] SessionManager tracks active sessions
- [ ] OutputBuffer handles 1000+ lines/sec
- [ ] Unit tests >80% coverage

### Phase 2 Checkpoint
- [ ] SignalR hub accepts connections
- [ ] Streaming output works with backpressure
- [ ] Commands sent successfully
- [ ] Integration tests pass

### Phase 3 Checkpoint
- [ ] Terminal UI renders output
- [ ] Auto-scroll works
- [ ] Commands can be entered
- [ ] Connection status visible

### Phase 4 Checkpoint
- [ ] E2E test with real agent passes
- [ ] Performance meets targets
- [ ] Documentation complete
- [ ] Zero memory leaks

## Technical Notes

### Cross-Platform IPC Decision
**Chosen**: Unix Domain Sockets (UDS)
- ✅ Cross-platform (Windows 10+, Linux, macOS)
- ✅ Better performance than TCP
- ✅ File-based security model
- ✅ .NET support since Core 2.0

**Alternative Considered**: Named Pipes
- ❌ Windows-only in traditional form
- ❌ Complex cross-platform code

### SignalR Streaming Pattern
Using `IAsyncEnumerable<string>` for output streaming:
- Automatic backpressure handling
- Efficient memory usage
- Native cancellation support

## Related Documentation

- [Architecture Document](../../Architecture/Planned/agent-interaction-system.md) - Full technical architecture
- [SignalR Streaming Guide](https://learn.microsoft.com/en-us/aspnet/core/signalr/streaming) - Microsoft docs
- [Unix Domain Sockets in .NET](https://andrewlock.net/using-unix-domain-sockets-with-aspnetcore-and-httpclient/) - Implementation guide

## Progress Tracking

### Overall Progress: ~17% Complete

| Phase | Status | Progress | Hours Used | Hours Remaining |
|-------|--------|----------|------------|-----------------|
| Phase 1.1 | ✅ COMPLETE | 100% | ~2h | 0h |
| Phase 1.2 | ❌ Not Started | 0% | 0h | 8-10h |
| Phase 1.3 | ❌ Not Started | 0% | 0h | 6-8h |
| Phase 1.4 | ❌ Not Started | 0% | 0h | 4-6h |
| Phase 1.5 | ❌ Not Started | 0% | 0h | 4-5h |
| Phase 2 | ❌ Not Started | 0% | 0h | 10-13h |
| Phase 3 | ❌ Not Started | 0% | 0h | 10-13h |
| Phase 4 | ❌ Not Started | 0% | 0h | 8-11h |

**Total Hours Used**: ~2h
**Total Hours Remaining**: 48-63h

## Next Steps

1. **Immediate** (Phase 1.2): Implement TerminalAgentConnector with UDS
   - Create cross-platform socket handling
   - Implement IAgentConnector interface
   - Add connection state management

2. **Short-term** (Phase 1.3-1.5): Complete core infrastructure
   - SessionManager for lifecycle
   - OutputBuffer for streaming
   - Comprehensive unit tests

3. **Mid-term** (Phase 2-3): SignalR and UI
   - Real-time hub implementation
   - Blazor terminal component
   - Integration testing

## Post-MVP Enhancements

1. **TabBasedAgentConnector** for Cursor integration
2. **Advanced Terminal Features**: syntax highlighting, autocomplete
3. **Agent Discovery**: auto-detect running agents
4. **Analytics**: command usage, performance metrics

---

**Plan Created**: 2025-10-25
**Last Updated**: 2025-10-25
**Author**: work-plan-architect
**Review Status**: Pending work-plan-reviewer validation