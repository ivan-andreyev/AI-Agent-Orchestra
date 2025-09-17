# AI Agent Orchestra - API Testing Script

$apiUrl = "http://localhost:5002"
$success = 0
$failed = 0

function Test-Endpoint {
    param($Method, $Url, $Body = $null, $Description)

    Write-Host "`n🔹 Testing: $Description" -ForegroundColor Cyan
    Write-Host "   $Method $Url" -ForegroundColor Gray

    try {
        if ($Body) {
            $response = Invoke-RestMethod -Uri $Url -Method $Method -Body $Body -ContentType "application/json"
        } else {
            $response = Invoke-RestMethod -Uri $Url -Method $Method
        }

        Write-Host "   ✅ SUCCESS" -ForegroundColor Green
        if ($response) {
            $responseJson = $response | ConvertTo-Json -Depth 3
            if ($responseJson.Length -gt 200) {
                Write-Host "   Response: $($responseJson.Substring(0, 200))..." -ForegroundColor Gray
            } else {
                Write-Host "   Response: $responseJson" -ForegroundColor Gray
            }
        }
        return $true
    }
    catch {
        Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

Write-Host "=== AI Agent Orchestra API Tests ===" -ForegroundColor Yellow
Write-Host "Testing API at: $apiUrl" -ForegroundColor Yellow

# Test 1: Get initial state
if (Test-Endpoint "GET" "$apiUrl/state" -Description "Get orchestrator state") { $success++ } else { $failed++ }

# Test 2: Get agents
if (Test-Endpoint "GET" "$apiUrl/agents" -Description "Get all agents") { $success++ } else { $failed++ }

# Test 3: Register new agent
$agentBody = @{
    Id = "test-agent-ps"
    Name = "PowerShell Test Agent"
    Type = "claude-code"
    RepositoryPath = "C:\TestRepo"
} | ConvertTo-Json

if (Test-Endpoint "POST" "$apiUrl/agents/register" -Body $agentBody -Description "Register new agent") { $success++ } else { $failed++ }

# Test 4: Update agent status
$pingBody = @{
    Status = 1
    CurrentTask = "Running PowerShell tests"
} | ConvertTo-Json

if (Test-Endpoint "POST" "$apiUrl/agents/test-agent-ps/ping" -Body $pingBody -Description "Update agent status") { $success++ } else { $failed++ }

# Test 5: Queue a task
$taskBody = @{
    Command = "Run PowerShell test suite"
    RepositoryPath = "C:\TestRepo"
    Priority = 2
} | ConvertTo-Json

if (Test-Endpoint "POST" "$apiUrl/tasks/queue" -Body $taskBody -Description "Queue new task") { $success++ } else { $failed++ }

# Test 6: Get next task for agent
if (Test-Endpoint "GET" "$apiUrl/agents/test-agent-ps/next-task" -Description "Get next task for agent") { $success++ } else { $failed++ }

# Test 7: Get final state
if (Test-Endpoint "GET" "$apiUrl/state" -Description "Get final orchestrator state") { $success++ } else { $failed++ }

Write-Host "`n=== Test Results ===" -ForegroundColor Yellow
Write-Host "✅ Passed: $success" -ForegroundColor Green
Write-Host "❌ Failed: $failed" -ForegroundColor Red
Write-Host "🔍 Total:  $($success + $failed)" -ForegroundColor Blue

if ($failed -eq 0) {
    Write-Host "`n🎉 All tests passed! API is working correctly." -ForegroundColor Green
} else {
    Write-Host "`n⚠️  Some tests failed. Check API connectivity and logs." -ForegroundColor Yellow
}