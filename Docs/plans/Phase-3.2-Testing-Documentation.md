# Phase 3.2: Agent Detail Statistics & Testing - Testing Documentation

**Date Created**: 2025-10-14
**Phase**: Phase 3.2 - Agent Detail Statistics & Testing
**Test Framework Version**: 1.0
**Related Plan**: UI-Fixes-WorkPlan-2024-09-18.md

---

## 🎯 TESTING OBJECTIVES

Phase 3.2 requires validation of the **expanded agent statistics** implementation in `AgentSidebar.razor`. This document provides comprehensive testing procedures to validate:

1. **Data Accuracy**: Agent performance metrics calculated correctly
2. **Real-time Updates**: Updates occurring within <1s requirement
3. **Display Consistency**: Visual presentation correct across all agent states

---

## 📋 ACCEPTANCE CRITERIA VALIDATION

### Acceptance Criteria from Plan
From UI-Fixes-WorkPlan-2024-09-18.md Phase 3.2:
> **Acceptance Criteria**: Detailed agent information visible, performance data displayed, real-time updates functional

### Testing Mapping
| Criterion | Test Scenario | Success Metric |
|-----------|---------------|----------------|
| Detailed agent information visible | Test 1.1-1.3 | All agent details render correctly |
| Performance data displayed | Test 2.1-2.3 | Metrics calculated accurately |
| Real-time updates functional | Test 3.1-3.3 | Updates occur within <1s |

---

## 🧪 TEST SUITE 1: Data Accuracy Testing

### Test 1.1: Agent Basic Information Display
**Objective**: Verify all basic agent information fields are displayed correctly

**Test Steps**:
1. Start Orchestra.API and Orchestra.Web
2. Navigate to dashboard home page
3. Register test agent with known values:
   ```
   Agent ID: test-agent-001
   Name: Test Agent Alpha
   Type: claude-code
   Repository: C:\TestRepo
   Status: Idle
   ```
4. Locate agent in sidebar
5. Verify displayed information:
   - ✅ Agent name: "Test Agent Alpha" (or "Test Agent Alpha..." if truncated at 25 chars)
   - ✅ Type: "claude-code"
   - ✅ Repository: "C:\TestRepo" or "N/A"
   - ✅ Status icon: 🟡 (yellow circle for Idle)
   - ✅ Last Activity: "Just now" or "<1m ago"
   - ✅ Last Ping timestamp: Current time displayed

**Expected Result**: All fields display accurately with correct formatting

**Validation Method**: Visual inspection + manual comparison with registered agent data

---

### Test 1.2: Agent Performance Metrics Calculation
**Objective**: Validate performance metrics are calculated correctly

**Implementation Details** (from AgentSidebar.razor lines 142-160):
- Metrics calculated in `UpdateAgentMetrics()` (lines 430-464)
- Initial values generated in `GenerateRealisticTaskCount/Time/SuccessRate` (lines 470-521)

**Test Steps**:
1. Register multiple agents with different statuses:
   - Agent A: Status = Working, LastPing = Now
   - Agent B: Status = Idle, LastPing = Now - 5 minutes
   - Agent C: Status = Error, LastPing = Now - 1 hour
   - Agent D: Status = Offline, LastPing = Now - 1 day
2. Wait for auto-refresh cycle (800ms)
3. Expand each agent card
4. Verify performance metrics section displays for each agent:
   - ✅ **Tasks Completed**: Integer value > 0 for active agents
   - ✅ **Avg Time**: Float value in seconds (format: "XX.Xs")
   - ✅ **Success Rate**: Percentage 0-100% (format: "XX.X%")

**Expected Values by Status**:
| Status | Tasks Completed | Avg Time (s) | Success Rate (%) |
|--------|----------------|--------------|------------------|
| Working | 50-300 (scaled by activity) | 15-45 | 85-100 |
| Idle | 20-150 | 20-45 | 80-100 |
| Error | 0-45 | 30-90 | 40-70 |
| Offline | 0-15 | 0.0 | 0.0 |

**Expected Result**: Metrics display within expected ranges for each status

**Validation Method**: Automated script or manual verification with status-based range checking

---

### Test 1.3: Current Task Display
**Objective**: Verify current task information displays correctly for working agents

**Implementation Details** (from AgentSidebar.razor lines 162-169):
- Current task displayed only if `!string.IsNullOrEmpty(agent.CurrentTask)`
- Task content truncated at 30 characters
- Duration calculated from `agent.TaskStartTime`

**Test Steps**:
1. Create agent with status = Working
2. Update agent with current task:
   ```
   CurrentTask: "Implementing feature XYZ for user authentication module"
   TaskStartTime: DateTime.Now - TimeSpan.FromMinutes(5)
   ```
3. Locate agent in sidebar
4. Verify current task section:
   - ✅ Task header: "Current Task:"
   - ✅ Task content: "Implementing feature XYZ for us..." (truncated at 30 chars)
   - ✅ Task duration: "Running: 5m 0s" or "Running: 5m Xs"

**Test Steps for Idle Agent**:
1. Create agent with status = Idle, CurrentTask = null
2. Verify current task section does NOT display

**Expected Result**: Current task displays for working agents, hidden for idle/offline agents

**Validation Method**: Visual inspection + DOM verification

---

## 🧪 TEST SUITE 2: Real-time Updates Testing

### Test 2.1: Auto-refresh Interval Validation
**Objective**: Verify updates occur within <1s requirement (800ms configured)

**Implementation Details** (from AgentSidebar.razor lines 217-223):
- Auto-refresh configured: `SetRefreshInterval(TimeSpan.FromMilliseconds(800))`
- Refresh method: `RefreshDataAsync()` (lines 229-244)

**Test Steps**:
1. Start Orchestra.API and Orchestra.Web
2. Open browser developer tools (F12)
3. Navigate to dashboard
4. In console, add timing measurement:
   ```javascript
   let lastUpdate = Date.now();
   setInterval(() => {
       const statsElement = document.querySelector('.agent-summary-stats .stat-value');
       if (statsElement) {
           const now = Date.now();
           const elapsed = now - lastUpdate;
           if (elapsed >= 800 && elapsed <= 900) {
               console.log(`✅ Update interval: ${elapsed}ms (within tolerance)`);
           } else if (elapsed > 900) {
               console.warn(`⚠️ Update interval: ${elapsed}ms (slower than expected)`);
           } else {
               console.log(`Update interval: ${elapsed}ms`);
           }
           lastUpdate = now;
       }
   }, 100);
   ```
5. Monitor console for 10 update cycles (8 seconds)
6. Verify update intervals are consistently 800-900ms

**Expected Result**: Updates occur every 800ms ± 100ms (within <1s requirement)

**Validation Method**: Automated browser-based timing measurement

---

### Test 2.2: Performance Metrics Update
**Objective**: Verify performance metrics update dynamically for working agents

**Test Steps**:
1. Register agent with status = Working
2. Note initial metrics:
   - Tasks Completed: X
   - Avg Time: Y
   - Success Rate: Z
3. Wait for 3-4 refresh cycles (2.4-3.2 seconds)
4. Verify metrics change:
   - ✅ Tasks Completed: May increase by 0-6 (Random.Shared.Next(0, 2) per cycle)
   - ✅ Avg Time: May fluctuate slightly
   - ✅ Success Rate: May fluctuate slightly
   - ✅ LastUpdated timestamp advances

**Expected Result**: Working agent metrics update dynamically, idle/offline agents remain static

**Validation Method**: Manual observation over 3-4 refresh cycles

---

### Test 2.3: Status Change Propagation
**Objective**: Verify status changes propagate to UI within <1s

**Test Steps**:
1. Register agent with status = Idle
2. Note current UI display (🟡 Idle icon)
3. Via API, update agent status to Working:
   ```
   POST /api/orchestrator/agent/test-agent-001/status
   Body: { "status": "Working", "currentTask": "Test task" }
   ```
4. Start timer
5. Observe UI until status icon changes to 🟢 (Working)
6. Stop timer
7. Verify elapsed time < 1000ms

**Expected Result**: Status change visible in UI within <1s of API call

**Validation Method**: Manual timing or automated test with API + UI state verification

---

## 🧪 TEST SUITE 3: Display Consistency Testing

### Test 3.1: Agent Filtering Consistency
**Objective**: Verify filtering maintains correct agent display and counts

**Implementation Details** (from AgentSidebar.razor lines 271-304):
- Filter dropdown: lines 72-78
- Filter logic: `FilterAgents()` method
- Status priority sorting: `GetStatusPriority()` (lines 319-329)

**Test Steps**:
1. Register agents with various statuses:
   - 3 Working agents
   - 2 Idle agents
   - 1 Error agent
   - 1 Offline agent
2. Verify "All" filter shows all 7 agents with correct counts: "All (7)"
3. Select "Working" filter
4. Verify:
   - ✅ Only 3 agents displayed
   - ✅ All displayed agents have 🟢 status icon
   - ✅ Filter dropdown shows "Working (3)"
5. Repeat for "Idle", "Error", "Offline" filters
6. Verify each filter displays correct subset

**Expected Result**: Filter displays correct agent subset with accurate counts

**Validation Method**: Manual verification or automated DOM inspection

---

### Test 3.2: Agent Sorting Order
**Objective**: Verify agents are sorted by status priority then by activity

**Implementation Details** (from AgentSidebar.razor lines 297-300):
- Primary sort: `GetStatusPriority(a.Status)` (Working=1, Idle=2, Error=3, Offline=4)
- Secondary sort: `LastPing DESC` (most recent first)

**Test Steps**:
1. Register agents with specific LastPing times:
   ```
   Agent A: Working, LastPing = Now - 10 min
   Agent B: Working, LastPing = Now - 5 min
   Agent C: Idle, LastPing = Now - 1 min
   Agent D: Error, LastPing = Now - 30 min
   Agent E: Offline, LastPing = Now - 1 day
   ```
2. Verify display order in sidebar (top to bottom):
   - ✅ Position 1: Agent B (Working, most recent)
   - ✅ Position 2: Agent A (Working, less recent)
   - ✅ Position 3: Agent C (Idle)
   - ✅ Position 4: Agent D (Error)
   - ✅ Position 5: Agent E (Offline)

**Expected Result**: Agents sorted by status priority (Working > Idle > Error > Offline), then by LastPing DESC

**Validation Method**: Visual inspection or DOM order verification

---

### Test 3.3: Repository Context Statistics
**Objective**: Verify system-wide performance metrics display correctly

**Implementation Details** (from AgentSidebar.razor lines 40-70):
- Repository display: lines 44-47
- Last Update: lines 50-53
- Performance metrics: lines 56-68 (calculated in `UpdatePerformanceMetrics()` lines 397-423)

**Test Steps**:
1. Register 5 agents with mixed statuses:
   - 2 Working
   - 2 Idle
   - 1 Error
2. Verify sidebar header displays:
   - ✅ **Repository**: "All" or specific repository name
   - ✅ **Last Update**: "Just now" or "<1m ago"
   - ✅ **Avg Response**: <1000ms (capped at 1000ms)
   - ✅ **Success Rate**: ~80% ((4 working/idle) / 5 total * 100)
   - ✅ **Tasks/min**: ~5.0 (2 working agents * 2.5 tasks/min estimate)

**Expected Result**: System metrics calculated from all agents and displayed correctly

**Validation Method**: Manual calculation vs displayed values

---

## 🛠️ AUTOMATED TESTING TOOLS

### Tool 1: PowerShell API Testing Script
**File**: `Test-Phase3-AgentStatistics.ps1`

**Purpose**: Automate agent registration, status updates, and API validation

**Features**:
- Register test agents with various statuses
- Update agent statuses via API
- Verify API responses match expected values
- Measure API response times

**Usage**:
```powershell
cd C:\Users\mrred\RiderProjects\AI-Agent-Orchestra\Docs\plans
.\Test-Phase3-AgentStatistics.ps1
```

**Test Scenarios**:
1. Register 10 test agents with varied statuses
2. Verify GET /api/orchestrator/state returns correct agent data
3. Update agent statuses and verify changes propagate
4. Measure API performance (<100ms for state retrieval)
5. Clean up test agents after completion

---

### Tool 2: Browser-based Real-time Monitoring
**File**: `phase3-realtime-monitor.html`

**Purpose**: Interactive browser tool for manual real-time updates testing

**Features**:
- Auto-refresh interval measurement
- Status change timing
- Performance metrics tracking
- Visual pass/fail indicators

**Usage**:
1. Open file in browser
2. Enter Orchestra.Web base URL (e.g., http://localhost:5001)
3. Click "Start Monitoring"
4. Observe real-time metrics and timing data

**Metrics Displayed**:
- Update interval (target: 800ms ± 100ms)
- Status change latency (target: <1000ms)
- Metrics update frequency
- UI responsiveness

---

## 📊 TEST EXECUTION SUMMARY

### Pre-Execution Checklist
- [ ] Orchestra.API running (default: http://localhost:5000)
- [ ] Orchestra.Web running (default: http://localhost:5001 or https://localhost:5002)
- [ ] Database initialized (migrations applied)
- [ ] Browser developer tools available
- [ ] Test scripts downloaded and accessible

### Execution Order
1. **Data Accuracy Tests** (Suite 1): ~15-20 minutes
   - Test 1.1: Basic information display
   - Test 1.2: Performance metrics calculation
   - Test 1.3: Current task display
2. **Real-time Updates Tests** (Suite 2): ~10-15 minutes
   - Test 2.1: Auto-refresh interval
   - Test 2.2: Performance metrics update
   - Test 2.3: Status change propagation
3. **Display Consistency Tests** (Suite 3): ~10-15 minutes
   - Test 3.1: Agent filtering
   - Test 3.2: Agent sorting
   - Test 3.3: Repository context statistics

**Total Estimated Time**: 35-50 minutes

### Success Criteria
- ✅ All Test Suite 1 tests pass (100% data accuracy)
- ✅ All Test Suite 2 tests pass (100% real-time updates functional)
- ✅ All Test Suite 3 tests pass (100% display consistency)
- ✅ Zero regressions in existing agent display functionality
- ✅ Performance metrics update within <1s requirement
- ✅ All acceptance criteria validated

---

## 📝 TEST RESULTS TEMPLATE

```markdown
## Phase 3.2 Testing Results

**Tester**: [Name]
**Date**: [YYYY-MM-DD]
**Environment**: [Development/Staging/Production]
**Browser**: [Chrome/Firefox/Edge] [Version]

### Test Suite 1: Data Accuracy
- Test 1.1: Basic Information Display: ✅ PASS / ❌ FAIL
  - Notes: [Any observations]
- Test 1.2: Performance Metrics Calculation: ✅ PASS / ❌ FAIL
  - Notes: [Any observations]
- Test 1.3: Current Task Display: ✅ PASS / ❌ FAIL
  - Notes: [Any observations]

### Test Suite 2: Real-time Updates
- Test 2.1: Auto-refresh Interval: ✅ PASS / ❌ FAIL
  - Measured interval: [XXXms]
  - Notes: [Any observations]
- Test 2.2: Performance Metrics Update: ✅ PASS / ❌ FAIL
  - Notes: [Any observations]
- Test 2.3: Status Change Propagation: ✅ PASS / ❌ FAIL
  - Measured latency: [XXXms]
  - Notes: [Any observations]

### Test Suite 3: Display Consistency
- Test 3.1: Agent Filtering: ✅ PASS / ❌ FAIL
  - Notes: [Any observations]
- Test 3.2: Agent Sorting: ✅ PASS / ❌ FAIL
  - Notes: [Any observations]
- Test 3.3: Repository Context Statistics: ✅ PASS / ❌ FAIL
  - Notes: [Any observations]

### Overall Result
- Total Tests: 9
- Passed: [X]
- Failed: [Y]
- Pass Rate: [X/9 * 100]%

### Acceptance Criteria Validation
- ✅ Detailed agent information visible: [PASS/FAIL]
- ✅ Performance data displayed: [PASS/FAIL]
- ✅ Real-time updates functional: [PASS/FAIL]

### Conclusion
[Overall assessment of Phase 3.2 implementation quality]

### Issues Found
[List any bugs, inconsistencies, or performance issues]

### Recommendations
[Any suggestions for improvements]
```

---

## 🔧 TROUBLESHOOTING

### Issue: Auto-refresh not working
**Symptoms**: UI not updating, "Last Update" timestamp frozen
**Diagnosis**:
1. Check browser console for JavaScript errors
2. Verify `AutoRefreshComponent` base class functioning
3. Check network tab for failed API calls

**Solution**:
- Clear browser cache and reload
- Restart Orchestra.Web application
- Verify inheritance chain: AgentSidebar → AutoRefreshComponent

---

### Issue: Performance metrics showing 0 or NaN
**Symptoms**: Metrics display "0", "0.0", "NaN", or "Infinity"
**Diagnosis**:
1. Check agent LastPing values (should not be default DateTime)
2. Verify agent status is valid enum value
3. Check _agentMetrics dictionary population

**Solution**:
- Re-register agents with valid LastPing timestamps
- Wait for initial auto-refresh cycle (800ms)
- Check AgentSidebar.razor UpdateAgentMetrics() execution

---

### Issue: Agents not filtering correctly
**Symptoms**: Wrong agents displayed after filter selection
**Diagnosis**:
1. Check _statusFilter value in component state
2. Verify Enum.TryParse<AgentStatus>() succeeds
3. Check FilterAgents() method execution

**Solution**:
- Verify status values match AgentStatus enum exactly
- Check for case sensitivity issues
- Restart component to reset filter state

---

## 📚 REFERENCES

### Implementation Files
- **AgentSidebar.razor**: Main component (C:\Users\mrred\RiderProjects\AI-Agent-Orchestra\src\Orchestra.Web\Components\AgentSidebar.razor)
- **AgentInfo.cs**: Model definition (C:\Users\mrred\RiderProjects\AI-Agent-Orchestra\src\Orchestra.Web\Models\AgentInfo.cs)
- **AutoRefreshComponent.cs**: Base class (C:\Users\mrred\RiderProjects\AI-Agent-Orchestra\src\Orchestra.Web\Components\Base\AutoRefreshComponent.cs)

### Related Documentation
- **UI-Fixes-WorkPlan-2024-09-18.md**: Main work plan (C:\Users\mrred\RiderProjects\AI-Agent-Orchestra\Docs\plans\UI-Fixes-WorkPlan-2024-09-18.md)
- **Phase 0.1 Performance Baseline**: Performance thresholds (C:\Users\mrred\RiderProjects\AI-Agent-Orchestra\Docs\phase-0-performance-baseline.md)

### API Endpoints
- `GET /api/orchestrator/state` - Get orchestrator state with all agents
- `POST /api/orchestrator/agent/{id}/status` - Update agent status
- `POST /api/orchestrator/agent/register` - Register new agent
- `DELETE /api/orchestrator/agent/{id}` - Delete agent

---

## ✅ COMPLETION CHECKLIST

Phase 3.2 is considered **COMPLETE** when:

- [ ] All 9 test scenarios executed successfully
- [ ] Test results documented in results template
- [ ] All acceptance criteria validated and passed
- [ ] Zero critical bugs identified
- [ ] Performance requirements met (<1s updates)
- [ ] Display consistency verified across all agent states
- [ ] Test documentation reviewed and approved
- [ ] Phase 3.2 marked as ✅ COMPLETED in work plan

**Estimated Completion Time**: 35-50 minutes (manual testing)

---

**Document Version**: 1.0
**Last Updated**: 2025-10-14
**Author**: plan-task-executor agent
**Status**: ✅ Ready for Execution
