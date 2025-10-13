# Test-Phase6-Performance.ps1
# Performance validation script for Phase 6.2 testing
# Compares current performance against Phase 0 baseline thresholds

param(
    [string]$ApiBaseUrl = "http://localhost:5002",
    [string]$WebBaseUrl = "http://localhost:5001",
    [int]$Iterations = 3,
    [string]$BaselineFile = "../Docs/plans/phase-0-performance-baseline.md"
)

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "  Phase 6.2: Performance Validation Test" -ForegroundColor Cyan
Write-Host "  Comparing against Phase 0 Baseline" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host ""

# Phase 0 Baseline Thresholds
$baseline = @{
    StateEndpoint = @{ Baseline = 78.33; Threshold = 165; Name = "GET /state" }
    AgentsEndpoint = @{ Baseline = 64.72; Threshold = 130; Name = "GET /agents" }
    RepositoriesEndpoint = @{ Baseline = 81.10; Threshold = 165; Name = "GET /repositories" }
    TaskQueueEndpoint = @{ Baseline = 106.30; Threshold = 215; Name = "POST /tasks/queue" }
}

$results = @()
$allPassed = $true

# Function to test API endpoint performance
function Test-ApiEndpoint {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [string]$Name,
        [double]$BaselineMs,
        [double]$ThresholdMs,
        [int]$Iterations
    )

    Write-Host "Testing: $Name" -ForegroundColor Yellow
    Write-Host "  URL: $Url" -ForegroundColor Gray
    Write-Host "  Baseline: $BaselineMs ms | Threshold: $ThresholdMs ms" -ForegroundColor Gray

    $times = @()

    for ($i = 1; $i -le $Iterations; $i++) {
        try {
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

            if ($Method -eq "GET") {
                $response = Invoke-WebRequest -Uri $Url -Method $Method -UseBasicParsing -ErrorAction Stop
            }
            else {
                # POST request with minimal body for task queue test
                $body = @{
                    Description = "Phase 6.2 performance test"
                    Priority = 1
                } | ConvertTo-Json

                $response = Invoke-WebRequest -Uri $Url -Method $Method -Body $body -ContentType "application/json" -UseBasicParsing -ErrorAction Stop
            }

            $stopwatch.Stop()
            $elapsedMs = $stopwatch.Elapsed.TotalMilliseconds
            $times += $elapsedMs

            Write-Host "    Iteration $i`: $([Math]::Round($elapsedMs, 2)) ms" -ForegroundColor Gray
        }
        catch {
            Write-Host "    Iteration $i`: ERROR - $($_.Exception.Message)" -ForegroundColor Red
            $times += [double]::MaxValue  # Treat as failure
        }
    }

    if ($times.Count -eq 0) {
        Write-Host "  Result: NO MEASUREMENTS" -ForegroundColor Red
        return $null
    }

    $avgMs = ($times | Measure-Object -Average).Average
    $minMs = ($times | Measure-Object -Minimum).Minimum
    $maxMs = ($times | Measure-Object -Maximum).Maximum

    $changePercent = [Math]::Round((($avgMs - $BaselineMs) / $BaselineMs) * 100, 2)
    $passed = $avgMs -le $ThresholdMs

    $status = if ($passed) { "PASS" } else { "FAIL" }
    $statusColor = if ($passed) { "Green" } else { "Red" }

    Write-Host "  Average: $([Math]::Round($avgMs, 2)) ms (min: $([Math]::Round($minMs, 2)) ms, max: $([Math]::Round($maxMs, 2)) ms)" -ForegroundColor Cyan
    Write-Host "  Change from baseline: $changePercent%" -ForegroundColor $(if ($changePercent -le 10) { "Green" } else { "Yellow" })
    Write-Host "  Status: $status" -ForegroundColor $statusColor
    Write-Host ""

    return @{
        Name = $Name
        Baseline = $BaselineMs
        Threshold = $ThresholdMs
        Average = $avgMs
        Min = $minMs
        Max = $maxMs
        ChangePercent = $changePercent
        Passed = $passed
        Status = $status
    }
}

# Test API endpoints
Write-Host "API PERFORMANCE TESTING" -ForegroundColor Cyan
Write-Host "-------------------------" -ForegroundColor Cyan
Write-Host ""

# Test GET /state
$stateResult = Test-ApiEndpoint `
    -Url "$ApiBaseUrl/state" `
    -Name $baseline.StateEndpoint.Name `
    -BaselineMs $baseline.StateEndpoint.Baseline `
    -ThresholdMs $baseline.StateEndpoint.Threshold `
    -Iterations $Iterations

if ($stateResult) {
    $results += $stateResult
    $allPassed = $allPassed -and $stateResult.Passed
}

# Test GET /agents
$agentsResult = Test-ApiEndpoint `
    -Url "$ApiBaseUrl/agents" `
    -Name $baseline.AgentsEndpoint.Name `
    -BaselineMs $baseline.AgentsEndpoint.Baseline `
    -ThresholdMs $baseline.AgentsEndpoint.Threshold `
    -Iterations $Iterations

if ($agentsResult) {
    $results += $agentsResult
    $allPassed = $allPassed -and $agentsResult.Passed
}

# Test GET /repositories
$repositoriesResult = Test-ApiEndpoint `
    -Url "$ApiBaseUrl/repositories" `
    -Name $baseline.RepositoriesEndpoint.Name `
    -BaselineMs $baseline.RepositoriesEndpoint.Baseline `
    -ThresholdMs $baseline.RepositoriesEndpoint.Threshold `
    -Iterations $Iterations

if ($repositoriesResult) {
    $results += $repositoriesResult
    $allPassed = $allPassed -and $repositoriesResult.Passed
}

# Summary Report
Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "  PERFORMANCE TEST SUMMARY" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "| Endpoint                  | Baseline  | Threshold | Measured  | Change    | Status |" -ForegroundColor White
Write-Host "|---------------------------|-----------|-----------|-----------|-----------|--------|" -ForegroundColor White

foreach ($result in $results) {
    $statusSymbol = if ($result.Passed) { "" } else { "" }
    $statusColor = if ($result.Passed) { "Green" } else { "Red" }

    $line = "| {0,-25} | {1,7} ms | {2,7} ms | {3,7} ms | {4,7}% | {5} |" -f `
        $result.Name.PadRight(25), `
        [Math]::Round($result.Baseline, 2), `
        [Math]::Round($result.Threshold, 2), `
        [Math]::Round($result.Average, 2), `
        [Math]::Round($result.ChangePercent, 2), `
        "$statusSymbol $($result.Status)"

    Write-Host $line -ForegroundColor $statusColor
}

Write-Host ""
Write-Host "Performance Regression Detection:" -ForegroundColor Cyan
Write-Host "  <10% change from baseline: Acceptable performance" -ForegroundColor Green
Write-Host "  10-100% change: Warning - investigate potential regression" -ForegroundColor Yellow
Write-Host "  >200% threshold: FAIL - exceeds Phase 0 threshold" -ForegroundColor Red
Write-Host ""

# Final result
if ($allPassed) {
    Write-Host " OVERALL RESULT: PASS" -ForegroundColor Green -BackgroundColor Black
    Write-Host "All API endpoints perform within Phase 0 baseline thresholds." -ForegroundColor Green
    exit 0
}
else {
    Write-Host " OVERALL RESULT: FAIL" -ForegroundColor Red -BackgroundColor Black
    Write-Host "One or more API endpoints exceed Phase 0 baseline thresholds." -ForegroundColor Red
    Write-Host "Review performance regressions before proceeding with Phase 6.2 completion." -ForegroundColor Yellow
    exit 1
}
