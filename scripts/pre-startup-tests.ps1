# Pre-Startup Tests Script
# Runs startup/smoke tests before launching Orchestra frontend and backend
#
# Purpose: Prevent "снова нихуя не взлетает" situations by catching build/runtime issues early
#
# Usage:
#   ./scripts/pre-startup-tests.ps1
#
# Exit codes:
#   0 - All tests passed
#   1 - Tests failed or error occurred

param(
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "================================" -ForegroundColor Cyan
Write-Host " Orchestra Pre-Startup Tests" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Running startup tests to verify system integrity..." -ForegroundColor Yellow
Write-Host ""

# Step 1: Build Orchestra.Tests if needed
Write-Host "[1/3] Building test project..." -ForegroundColor Cyan
try {
    Push-Location $projectRoot
    $buildOutput = dotnet build "src/Orchestra.Tests/Orchestra.Tests.csproj" --verbosity quiet 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Build failed!" -ForegroundColor Red
        Write-Host $buildOutput
        exit 1
    }

    Write-Host "✓ Test project built successfully" -ForegroundColor Green
} catch {
    Write-Host "✗ Build error: $_" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}

# Step 2: Run startup tests
Write-Host ""
Write-Host "[2/3] Running startup tests..." -ForegroundColor Cyan

try {
    Push-Location $projectRoot

    $testFilter = "Category=Startup"
    $verbosityLevel = if ($Verbose) { "normal" } else { "minimal" }

    $testOutput = dotnet test "src/Orchestra.Tests/Orchestra.Tests.csproj" `
        --filter $testFilter `
        --no-build `
        --verbosity $verbosityLevel `
        --logger "console;verbosity=$verbosityLevel" `
        2>&1

    $testExitCode = $LASTEXITCODE

    if ($Verbose) {
        Write-Host $testOutput
    }

    if ($testExitCode -ne 0) {
        Write-Host ""
        Write-Host "✗ STARTUP TESTS FAILED!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Test output:" -ForegroundColor Yellow
        Write-Host $testOutput
        Write-Host ""
        Write-Host "Cannot proceed with startup - fix failing tests first!" -ForegroundColor Red
        Write-Host ""
        exit 1
    }

    # Extract test results summary
    $testSummary = $testOutput | Select-String -Pattern "(Passed|Failed|Total tests|Test Run Successful)" | Out-String

    Write-Host "✓ All startup tests passed" -ForegroundColor Green
    if ($testSummary) {
        Write-Host ""
        Write-Host "Test Summary:" -ForegroundColor Cyan
        Write-Host $testSummary
    }

} catch {
    Write-Host "✗ Test execution error: $_" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}

# Step 3: Verification complete
Write-Host ""
Write-Host "[3/3] Verification complete" -ForegroundColor Cyan
Write-Host ""
Write-Host "================================" -ForegroundColor Green
Write-Host " ✓ ALL STARTUP TESTS PASSED" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""
Write-Host "System is ready for launch!" -ForegroundColor Green
Write-Host ""

exit 0
