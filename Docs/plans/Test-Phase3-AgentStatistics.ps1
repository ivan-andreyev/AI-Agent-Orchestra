# Phase 3.2: Agent Detail Statistics - Automated Testing Script
# Purpose: Automate agent registration, status updates, and API validation
# Version: 1.0
# Date: 2025-10-14

param(
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$CleanupOnly,
    [switch]$SkipCleanup,
    [int]$TestAgentCount = 10
)

# Color output functions
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Error { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }
function Write-Info { param($Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Warning { param($Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }

# Test result tracking
$script:TestResults = @{
    Total = 0
    Passed = 0
    Failed = 0
    Details = @()
}

function Add-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Message = "",
        [hashtable]$Details = @{}
    )

    $script:TestResults.Total++
    if ($Passed) {
        $script:TestResults.Passed++
        Write-Success "$TestName - PASSED"
    } else {
        $script:TestResults.Failed++
        Write-Error "$TestName - FAILED: $Message"
    }

    $script:TestResults.Details += @{
        TestName = $TestName
        Passed = $Passed
        Message = $Message
        Details = $Details
        Timestamp = Get-Date
    }
}

# API helper functions
function Invoke-OrchestratorAPI {
    param(
        [string]$Endpoint,
        [string]$Method = "GET",
        [object]$Body = $null
    )

    $uri = "$BaseUrl/api/orchestrator/$Endpoint"
    $headers = @{
        "Content-Type" = "application/json"
    }

    try {
        $params = @{
            Uri = $uri
            Method = $Method
            Headers = $headers
        }

        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }

        $response = Invoke-RestMethod @params
        return @{ Success = $true; Data = $response }
    }
    catch {
        return @{ Success = $false; Error = $_.Exception.Message; StatusCode = $_.Exception.Response.StatusCode }
    }
}

# Cleanup existing test agents
function Remove-TestAgents {
    Write-Info "Cleaning up existing test agents..."

    $state = Invoke-OrchestratorAPI -Endpoint "state"
    if (-not $state.Success) {
        Write-Warning "Could not retrieve orchestrator state for cleanup"
        return
    }

    $testAgents = $state.Data.agents.PSObject.Properties | Where-Object { $_.Name -like "test-agent-*" }
    $cleanedCount = 0

    foreach ($agentProp in $testAgents) {
        $agentId = $agentProp.Name
        $result = Invoke-OrchestratorAPI -Endpoint "agent/$agentId" -Method "DELETE"
        if ($result.Success) {
            $cleanedCount++
        }
    }

    if ($cleanedCount -gt 0) {
        Write-Success "Cleaned up $cleanedCount test agents"
    } else {
        Write-Info "No test agents found to clean up"
    }
}

# Test 1: API Availability
function Test-APIAvailability {
    Write-Info "Test 1: API Availability Check..."

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $result = Invoke-OrchestratorAPI -Endpoint "state"
    $stopwatch.Stop()

    if ($result.Success) {
        Add-TestResult -TestName "API Availability" -Passed $true -Details @{
            ResponseTime = "$($stopwatch.ElapsedMilliseconds)ms"
            BaseUrl = $BaseUrl
        }
    } else {
        Add-TestResult -TestName "API Availability" -Passed $false -Message "API not reachable at $BaseUrl"
    }
}

# Test 2: Agent Registration
function Test-AgentRegistration {
    Write-Info "Test 2: Agent Registration..."

    $testAgents = @(
        @{ Id = "test-agent-001"; Name = "Test Agent Alpha"; Type = "claude-code"; Repository = "C:\TestRepo1"; Status = "Working" }
        @{ Id = "test-agent-002"; Name = "Test Agent Beta"; Type = "claude-code"; Repository = "C:\TestRepo1"; Status = "Idle" }
        @{ Id = "test-agent-003"; Name = "Test Agent Gamma"; Type = "github-copilot"; Repository = "C:\TestRepo2"; Status = "Working" }
        @{ Id = "test-agent-004"; Name = "Test Agent Delta"; Type = "claude-code"; Repository = "C:\TestRepo2"; Status = "Error" }
        @{ Id = "test-agent-005"; Name = "Test Agent Epsilon"; Type = "cursor-ai"; Repository = "C:\TestRepo3"; Status = "Offline" }
    )

    $successCount = 0
    $failCount = 0

    foreach ($agent in $testAgents) {
        $body = @{
            Id = $agent.Id
            Name = $agent.Name
            Type = $agent.Type
            RepositoryPath = $agent.Repository
        }

        $result = Invoke-OrchestratorAPI -Endpoint "agent/register" -Method "POST" -Body $body

        if ($result.Success) {
            # Update status after registration
            $statusResult = Invoke-OrchestratorAPI -Endpoint "agent/$($agent.Id)/status" -Method "POST" -Body @{
                Status = $agent.Status
                CurrentTask = if ($agent.Status -eq "Working") { "Test task for $($agent.Name)" } else { $null }
            }

            if ($statusResult.Success) {
                $successCount++
            } else {
                $failCount++
            }
        } else {
            $failCount++
        }
    }

    $passed = ($failCount -eq 0)
    Add-TestResult -TestName "Agent Registration" -Passed $passed -Message "Registered: $successCount, Failed: $failCount" -Details @{
        SuccessCount = $successCount
        FailCount = $failCount
        TotalAttempted = $testAgents.Count
    }
}

# Test 3: Agent State Retrieval
function Test-AgentStateRetrieval {
    Write-Info "Test 3: Agent State Retrieval..."

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $result = Invoke-OrchestratorAPI -Endpoint "state"
    $stopwatch.Stop()

    if (-not $result.Success) {
        Add-TestResult -TestName "Agent State Retrieval" -Passed $false -Message "Failed to retrieve state"
        return
    }

    $agentCount = ($result.Data.agents.PSObject.Properties | Measure-Object).Count
    $testAgentCount = ($result.Data.agents.PSObject.Properties | Where-Object { $_.Name -like "test-agent-*" }).Count

    $passed = ($testAgentCount -ge 5)
    Add-TestResult -TestName "Agent State Retrieval" -Passed $passed -Details @{
        ResponseTime = "$($stopwatch.ElapsedMilliseconds)ms"
        TotalAgents = $agentCount
        TestAgents = $testAgentCount
    }
}

# Test 4: Agent Data Accuracy
function Test-AgentDataAccuracy {
    Write-Info "Test 4: Agent Data Accuracy..."

    $result = Invoke-OrchestratorAPI -Endpoint "state"
    if (-not $result.Success) {
        Add-TestResult -TestName "Agent Data Accuracy" -Passed $false -Message "Failed to retrieve state"
        return
    }

    $testAgent = $result.Data.agents.PSObject.Properties | Where-Object { $_.Name -eq "test-agent-001" } | Select-Object -First 1

    if (-not $testAgent) {
        Add-TestResult -TestName "Agent Data Accuracy" -Passed $false -Message "Test agent not found"
        return
    }

    $agent = $testAgent.Value

    $checks = @{
        "ID matches" = ($agent.id -eq "test-agent-001")
        "Name matches" = ($agent.name -eq "Test Agent Alpha")
        "Type matches" = ($agent.type -eq "claude-code")
        "Repository matches" = ($agent.repositoryPath -eq "C:\TestRepo1")
        "Status matches" = ($agent.status -eq "Working")
        "Has LastPing" = ($null -ne $agent.lastPing)
    }

    $passedChecks = ($checks.Values | Where-Object { $_ -eq $true }).Count
    $totalChecks = $checks.Count
    $passed = ($passedChecks -eq $totalChecks)

    Add-TestResult -TestName "Agent Data Accuracy" -Passed $passed -Message "Passed $passedChecks/$totalChecks checks" -Details $checks
}

# Test 5: Status Update
function Test-StatusUpdate {
    Write-Info "Test 5: Status Update..."

    # Update agent from Idle to Working
    $result = Invoke-OrchestratorAPI -Endpoint "agent/test-agent-002/status" -Method "POST" -Body @{
        Status = "Working"
        CurrentTask = "Updated test task"
    }

    if (-not $result.Success) {
        Add-TestResult -TestName "Status Update" -Passed $false -Message "Failed to update status"
        return
    }

    # Verify update
    Start-Sleep -Milliseconds 500
    $state = Invoke-OrchestratorAPI -Endpoint "state"

    if (-not $state.Success) {
        Add-TestResult -TestName "Status Update" -Passed $false -Message "Failed to verify status update"
        return
    }

    $agent = $state.Data.agents.PSObject.Properties | Where-Object { $_.Name -eq "test-agent-002" } | Select-Object -First 1

    if ($agent -and $agent.Value.status -eq "Working") {
        Add-TestResult -TestName "Status Update" -Passed $true -Details @{
            PreviousStatus = "Idle"
            NewStatus = "Working"
            CurrentTask = $agent.Value.currentTask
        }
    } else {
        Add-TestResult -TestName "Status Update" -Passed $false -Message "Status did not update correctly"
    }
}

# Test 6: Performance Metrics Range Validation
function Test-PerformanceMetricsRange {
    Write-Info "Test 6: Performance Metrics Range Validation..."

    # Note: AgentSidebar calculates metrics client-side, so we validate the API data structure
    $result = Invoke-OrchestratorAPI -Endpoint "state"

    if (-not $result.Success) {
        Add-TestResult -TestName "Performance Metrics Range" -Passed $false -Message "Failed to retrieve state"
        return
    }

    $testAgents = $result.Data.agents.PSObject.Properties | Where-Object { $_.Name -like "test-agent-*" }

    $validationResults = @()
    foreach ($agentProp in $testAgents) {
        $agent = $agentProp.Value

        $validation = @{
            AgentId = $agent.id
            HasId = ($null -ne $agent.id -and $agent.id -ne "")
            HasStatus = ($null -ne $agent.status)
            HasLastPing = ($null -ne $agent.lastPing)
            HasType = ($null -ne $agent.type)
            StatusValid = ($agent.status -in @("Idle", "Working", "Error", "Offline"))
        }

        $validationResults += $validation
    }

    $allValid = ($validationResults | Where-Object { $_.StatusValid -eq $false }).Count -eq 0

    Add-TestResult -TestName "Performance Metrics Range" -Passed $allValid -Details @{
        ValidatedAgents = $validationResults.Count
        AllValidStatuses = $allValid
    }
}

# Test 7: Real-time Update Simulation
function Test-RealtimeUpdateSimulation {
    Write-Info "Test 7: Real-time Update Simulation..."

    Write-Info "Simulating status changes over 3 cycles..."

    $updates = @(
        @{ Id = "test-agent-001"; Status = "Idle"; Delay = 800 }
        @{ Id = "test-agent-002"; Status = "Error"; Delay = 800 }
        @{ Id = "test-agent-003"; Status = "Idle"; Delay = 800 }
    )

    $successCount = 0
    $totalUpdates = $updates.Count

    foreach ($update in $updates) {
        $result = Invoke-OrchestratorAPI -Endpoint "agent/$($update.Id)/status" -Method "POST" -Body @{
            Status = $update.Status
        }

        if ($result.Success) {
            $successCount++
        }

        Start-Sleep -Milliseconds $update.Delay
    }

    $passed = ($successCount -eq $totalUpdates)
    Add-TestResult -TestName "Real-time Update Simulation" -Passed $passed -Message "Successfully simulated $successCount/$totalUpdates status changes" -Details @{
        TotalUpdates = $totalUpdates
        SuccessfulUpdates = $successCount
        AverageInterval = "800ms"
    }
}

# Test 8: API Performance
function Test-APIPerformance {
    Write-Info "Test 8: API Performance (10 requests)..."

    $measurements = @()
    $targetTime = 100 # ms

    for ($i = 1; $i -le 10; $i++) {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $result = Invoke-OrchestratorAPI -Endpoint "state"
        $stopwatch.Stop()

        if ($result.Success) {
            $measurements += $stopwatch.ElapsedMilliseconds
        }
    }

    if ($measurements.Count -gt 0) {
        $avgTime = ($measurements | Measure-Object -Average).Average
        $maxTime = ($measurements | Measure-Object -Maximum).Maximum
        $minTime = ($measurements | Measure-Object -Minimum).Minimum

        $passed = ($avgTime -le $targetTime)

        Add-TestResult -TestName "API Performance" -Passed $passed -Message "Avg: $([math]::Round($avgTime, 1))ms (target: <${targetTime}ms)" -Details @{
            AverageMs = [math]::Round($avgTime, 1)
            MinMs = $minTime
            MaxMs = $maxTime
            TargetMs = $targetTime
            Measurements = $measurements
        }
    } else {
        Add-TestResult -TestName "API Performance" -Passed $false -Message "No successful measurements"
    }
}

# Main execution
function Start-Testing {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Phase 3.2: Agent Statistics Testing" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan

    Write-Info "Base URL: $BaseUrl"
    Write-Info "Test Agent Count: $TestAgentCount"
    Write-Host ""

    # Cleanup if requested
    if ($CleanupOnly) {
        Remove-TestAgents
        return
    }

    # Pre-test cleanup
    if (-not $SkipCleanup) {
        Remove-TestAgents
        Start-Sleep -Seconds 1
    }

    # Run tests
    Test-APIAvailability
    Test-AgentRegistration
    Test-AgentStateRetrieval
    Test-AgentDataAccuracy
    Test-StatusUpdate
    Test-PerformanceMetricsRange
    Test-RealtimeUpdateSimulation
    Test-APIPerformance

    # Post-test cleanup
    if (-not $SkipCleanup) {
        Write-Host ""
        Remove-TestAgents
    }

    # Summary
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Test Results Summary" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan

    Write-Host "Total Tests: $($script:TestResults.Total)" -ForegroundColor White
    Write-Success "Passed: $($script:TestResults.Passed)"

    if ($script:TestResults.Failed -gt 0) {
        Write-Error "Failed: $($script:TestResults.Failed)"
    } else {
        Write-Host "Failed: $($script:TestResults.Failed)" -ForegroundColor White
    }

    $passRate = if ($script:TestResults.Total -gt 0) {
        [math]::Round(($script:TestResults.Passed / $script:TestResults.Total) * 100, 1)
    } else {
        0
    }

    Write-Host "`nPass Rate: $passRate%" -ForegroundColor $(if ($passRate -ge 80) { "Green" } else { "Red" })

    # Detailed results
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Detailed Test Results" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan

    foreach ($detail in $script:TestResults.Details) {
        $statusIcon = if ($detail.Passed) { "✅" } else { "❌" }
        $statusColor = if ($detail.Passed) { "Green" } else { "Red" }

        Write-Host "$statusIcon $($detail.TestName)" -ForegroundColor $statusColor
        if ($detail.Message) {
            Write-Host "   Message: $($detail.Message)" -ForegroundColor Gray
        }
        if ($detail.Details.Count -gt 0) {
            foreach ($key in $detail.Details.Keys) {
                Write-Host "   $key : $($detail.Details[$key])" -ForegroundColor Gray
            }
        }
        Write-Host ""
    }

    # Overall verdict
    Write-Host "========================================" -ForegroundColor Cyan
    if ($script:TestResults.Failed -eq 0) {
        Write-Success "ALL TESTS PASSED! Phase 3.2 API validation successful."
    } else {
        Write-Error "SOME TESTS FAILED. Review results above for details."
    }
    Write-Host "========================================`n" -ForegroundColor Cyan
}

# Execute
Start-Testing
