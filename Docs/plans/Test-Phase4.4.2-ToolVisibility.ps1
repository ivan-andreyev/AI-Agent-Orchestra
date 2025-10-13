# Phase 4.4.2 Tool Visibility Cross-Platform Testing Script
# Automated responsive design validation and browser launching

param(
    [string]$WebBaseUrl = "http://localhost:5000",
    [string]$ApiBaseUrl = "http://localhost:55002",
    [string]$BrowserPath = "C:\Program Files\Google\Chrome\Application\chrome.exe",
    [switch]$SkipBrowserLaunch
)

$ErrorActionPreference = "Continue"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " Phase 4.4.2: Tool Visibility Testing" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Helper function to check if URL is accessible
function Test-UrlAccessible {
    param(
        [string]$Url,
        [string]$ServiceName
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec 5 -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            Write-Host "✅ $ServiceName is accessible" -ForegroundColor Green
            return $true
        }
    } catch {
        Write-Host "❌ $ServiceName is NOT accessible at $Url" -ForegroundColor Red
        Write-Host "   Error: $_" -ForegroundColor Gray
        return $false
    }
    return $false
}

# Helper function to launch browser with specific viewport
function Start-BrowserWithViewport {
    param(
        [string]$Url,
        [int]$Width,
        [int]$Height,
        [string]$DeviceName
    )

    if ($SkipBrowserLaunch) {
        Write-Host "⏭️  Skipping browser launch (SkipBrowserLaunch flag set)" -ForegroundColor Yellow
        return
    }

    if (-not (Test-Path $BrowserPath)) {
        Write-Host "⚠️  Browser not found at: $BrowserPath" -ForegroundColor Yellow
        Write-Host "   Please launch browser manually and resize to ${Width}x${Height}" -ForegroundColor Gray
        return
    }

    Write-Host "🌐 Launching browser: $DeviceName (${Width}x${Height})" -ForegroundColor Cyan

    $chromeArgs = @(
        "--new-window",
        "--window-size=${Width},${Height}",
        "--app=$Url"
    )

    try {
        Start-Process -FilePath $BrowserPath -ArgumentList $chromeArgs
        Start-Sleep -Seconds 2
    } catch {
        Write-Host "⚠️  Failed to launch browser: $_" -ForegroundColor Yellow
        Write-Host "   Please launch browser manually" -ForegroundColor Gray
    }
}

# Helper function to display test instructions
function Show-TestInstructions {
    param(
        [string]$Scenario,
        [string[]]$Instructions
    )

    Write-Host ""
    Write-Host "--- $Scenario ---" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "📋 Manual Verification Steps:" -ForegroundColor Cyan

    $stepNumber = 1
    foreach ($instruction in $Instructions) {
        Write-Host "   $stepNumber. $instruction" -ForegroundColor Gray
        $stepNumber++
    }

    Write-Host ""
}

# Pre-flight checks
Write-Host "🔍 Pre-flight Checks" -ForegroundColor Cyan
Write-Host "--------------------" -ForegroundColor Gray
Write-Host ""

$apiAccessible = Test-UrlAccessible -Url "$ApiBaseUrl/api/orchestrator/state" -ServiceName "Orchestra.API"
$webAccessible = Test-UrlAccessible -Url $WebBaseUrl -ServiceName "Orchestra.Web"

Write-Host ""

if (-not $apiAccessible) {
    Write-Host "⚠️  WARNING: Orchestra.API is not accessible" -ForegroundColor Yellow
    Write-Host "   Please start Orchestra.API before continuing" -ForegroundColor Gray
    Write-Host "   Command: cd src/Orchestra.API && dotnet run" -ForegroundColor Gray
    Write-Host ""
}

if (-not $webAccessible) {
    Write-Host "⚠️  WARNING: Orchestra.Web is not accessible" -ForegroundColor Yellow
    Write-Host "   Please start Orchestra.Web before continuing" -ForegroundColor Gray
    Write-Host "   Command: cd src/Orchestra.Web && dotnet run" -ForegroundColor Gray
    Write-Host ""
}

if ($apiAccessible -and $webAccessible) {
    Write-Host "✅ All services are ready for testing" -ForegroundColor Green
    Write-Host ""
} else {
    $continue = Read-Host "Do you want to continue anyway? (y/n)"
    if ($continue -ne 'y') {
        Write-Host "❌ Testing aborted" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Scenario 1: Desktop Full Visibility
Show-TestInstructions -Scenario "Scenario 1: Desktop Full Visibility (1920x1080)" -Instructions @(
    "Verify sidebar visible on left (col-md-3, ~25% width)",
    "Verify OrchestrationControlPanel card displayed",
    "Click 'Quick Actions' tab",
    "Verify all 3 dropdowns visible: Development, Analysis, Documentation",
    "Click 'Development' dropdown → Select 'Git Status'",
    "Verify success message appears",
    "Check TaskQueue component for queued task",
    "Test Custom Task input: Enter text, select priority, click 'Queue Task'",
    "Verify all emojis display correctly",
    "Open Browser DevTools (F12) → Inspect .orchestration-control-section",
    "Verify no CSS hiding rules: display should NOT be 'none'"
)

Start-BrowserWithViewport -Url $WebBaseUrl -Width 1920 -Height 1080 -DeviceName "Desktop (1920x1080)"

Write-Host "⏸️  Press Enter when Desktop testing is complete..." -ForegroundColor Yellow
Read-Host

# Scenario 2: Tablet Responsive Design
Show-TestInstructions -Scenario "Scenario 2: Tablet Responsive Design (768x1024)" -Instructions @(
    "Verify sidebar still visible (Bootstrap col-md-3 applies at ≥768px)",
    "Verify OrchestrationControlPanel accessible",
    "Navigate to Quick Actions tab",
    "Test all dropdowns (use DevTools touch simulation if available)",
    "Verify custom task input responsive",
    "Check for horizontal overflow (should be none)",
    "Verify no layout breaking at 768px breakpoint",
    "Test priority dropdown functionality"
)

Start-BrowserWithViewport -Url $WebBaseUrl -Width 768 -Height 1024 -DeviceName "Tablet (iPad)"

Write-Host "⏸️  Press Enter when Tablet testing is complete..." -ForegroundColor Yellow
Read-Host

# Scenario 3: Mobile Sidebar Toggle
Show-TestInstructions -Scenario "Scenario 3: Mobile Sidebar Toggle (375x667)" -Instructions @(
    "Verify mobile menu button visible in header (☰ Menu)",
    "Verify sidebar hidden by default (not visible)",
    "Click mobile menu button (☰ Menu)",
    "Verify sidebar slides in from left (smooth animation)",
    "Verify sidebar overlay appears (semi-transparent background)",
    "Verify OrchestrationControlPanel visible in mobile sidebar",
    "Navigate to Quick Actions tab",
    "Test all dropdowns on mobile (tap interaction)",
    "Test custom task input with mobile keyboard simulation",
    "Test priority dropdown on mobile",
    "Verify dropdown menus render correctly (no overflow)",
    "Tap outside sidebar (on overlay) to close",
    "Verify sidebar closes smoothly"
)

Start-BrowserWithViewport -Url $WebBaseUrl -Width 375 -Height 667 -DeviceName "Mobile (iPhone)"

Write-Host "⏸️  Press Enter when Mobile testing is complete..." -ForegroundColor Yellow
Read-Host

# Scenario 4: Mobile Landscape
Show-TestInstructions -Scenario "Scenario 4: Mobile Landscape (851x393)" -Instructions @(
    "Rotate device to landscape (or resize browser)",
    "Verify mobile menu button still visible",
    "Open mobile sidebar via menu button",
    "Verify sidebar behavior in landscape (may be narrower)",
    "Navigate to Quick Actions tab",
    "Test all dropdowns in landscape mode",
    "Verify no horizontal overflow",
    "Verify no layout breaking"
)

Start-BrowserWithViewport -Url $WebBaseUrl -Width 851 -Height 393 -DeviceName "Mobile Landscape (Pixel 5)"

Write-Host "⏸️  Press Enter when Mobile Landscape testing is complete..." -ForegroundColor Yellow
Read-Host

# Scenario 5: Functionality Deep Dive
Show-TestInstructions -Scenario "Scenario 5: Functionality Deep Dive" -Instructions @(
    "Development Dropdown: Verify 4 menu items (Git Status, Git Pull, Build, Run Tests)",
    "Analysis Dropdown: Verify 3 menu items (Code Review, Security Scan, Performance Check)",
    "Documentation Dropdown: Verify 3 menu items (Update README, API Docs, Add Comments)",
    "Custom Task: Test with empty input (button should be disabled)",
    "Custom Task: Enter text and verify button enabled",
    "Priority Dropdown: Verify 4 options (Normal, High, Critical, Low)",
    "Queue a task with Critical priority",
    "Verify success alert displays (green background, ✅)",
    "Verify error alert (if triggered) displays correctly (red background, ❌)",
    "Verify alerts auto-dismiss after 5 seconds",
    "Verify close buttons on alerts are functional",
    "Verify TaskQueue component reflects all queued tasks"
)

Write-Host ""
Write-Host "📋 Please perform Functionality Deep Dive in any viewport" -ForegroundColor Cyan
Write-Host ""

Write-Host "⏸️  Press Enter when Functionality testing is complete..." -ForegroundColor Yellow
Read-Host

# API Health Check
Write-Host ""
Write-Host "🔍 API Health Check" -ForegroundColor Cyan
Write-Host "-------------------" -ForegroundColor Gray
Write-Host ""

if ($apiAccessible) {
    try {
        $state = Invoke-RestMethod -Uri "$ApiBaseUrl/api/orchestrator/state" -Method GET
        Write-Host "✅ API accessible and responding" -ForegroundColor Green
        Write-Host "   Agents: $($state.Agents.Count)" -ForegroundColor Gray
        Write-Host "   Tasks in Queue: $($state.TaskQueue.Count)" -ForegroundColor Gray

        if ($state.TaskQueue.Count -gt 0) {
            Write-Host ""
            Write-Host "   Recent Tasks:" -ForegroundColor Cyan
            $recentTasks = $state.TaskQueue | Select-Object -First 5
            foreach ($task in $recentTasks) {
                $statusColor = switch ($task.Status) {
                    "Assigned" { "Green" }
                    "InProgress" { "Yellow" }
                    "Completed" { "Cyan" }
                    "Failed" { "Red" }
                    default { "Gray" }
                }
                Write-Host "      - $($task.Command) [$($task.Status)]" -ForegroundColor $statusColor
            }
        }
    } catch {
        Write-Host "❌ API error: $_" -ForegroundColor Red
    }
} else {
    Write-Host "⚠️  API not accessible, skipping health check" -ForegroundColor Yellow
}

Write-Host ""

# Test Summary
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " Test Summary" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "✅ Scenario 1: Desktop Full Visibility (1920x1080) - Manual verification complete" -ForegroundColor Green
Write-Host "✅ Scenario 2: Tablet Responsive Design (768x1024) - Manual verification complete" -ForegroundColor Green
Write-Host "✅ Scenario 3: Mobile Sidebar Toggle (375x667) - Manual verification complete" -ForegroundColor Green
Write-Host "✅ Scenario 4: Mobile Landscape (851x393) - Manual verification complete" -ForegroundColor Green
Write-Host "✅ Scenario 5: Functionality Deep Dive - Manual verification complete" -ForegroundColor Green

Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Cyan
Write-Host "   1. Document test results in Phase-4.4.2-Manual-Testing-Checklist.md" -ForegroundColor Gray
Write-Host "   2. Use phase4-tool-visibility-test.html for detailed checklist" -ForegroundColor Gray
Write-Host "   3. Test additional browsers (Firefox, Edge, Safari)" -ForegroundColor Gray
Write-Host "   4. Export test results from HTML tool" -ForegroundColor Gray
Write-Host "   5. Update Phase 4.4.2 status to COMPLETE when all tests pass" -ForegroundColor Gray

Write-Host ""
Write-Host "🌐 Cross-Browser Testing Recommendations:" -ForegroundColor Cyan
Write-Host "   - Firefox: firefox.exe -new-window -url $WebBaseUrl" -ForegroundColor Gray
Write-Host "   - Edge: msedge.exe --new-window --app=$WebBaseUrl" -ForegroundColor Gray
Write-Host "   - Safari (macOS): open -a Safari $WebBaseUrl" -ForegroundColor Gray

Write-Host ""
Write-Host "✅ Phase 4.4.2 Testing Script Complete" -ForegroundColor Green
Write-Host ""
Write-Host "For automated browser testing, consider:" -ForegroundColor Gray
Write-Host "   - Selenium WebDriver (cross-browser automation)" -ForegroundColor Gray
Write-Host "   - Playwright (modern cross-browser testing)" -ForegroundColor Gray
Write-Host "   - Puppeteer (Chrome/Chromium automation)" -ForegroundColor Gray
Write-Host ""
