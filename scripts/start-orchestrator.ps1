# Скрипт для запуска AI Agent Orchestra MVP

param(
    [string]$Mode = "dev",
    [int]$Port = 5000
)

Write-Host "=== Starting AI Agent Orchestra MVP ===" -ForegroundColor Green
Write-Host "Mode: $Mode" -ForegroundColor Yellow
Write-Host "Port: $Port" -ForegroundColor Yellow
Write-Host ""

# Run pre-startup tests first
Write-Host "Running pre-startup tests..." -ForegroundColor Cyan
$preStartupTestsScript = Join-Path $PSScriptRoot "pre-startup-tests.ps1"
if (Test-Path $preStartupTestsScript) {
    try {
        & $preStartupTestsScript
        if ($LASTEXITCODE -ne 0) {
            Write-Host ""
            Write-Host "Pre-startup tests failed! Aborting startup." -ForegroundColor Red
            Write-Host "Fix the failing tests before starting Orchestra." -ForegroundColor Yellow
            exit 1
        }
    } catch {
        Write-Host ""
        Write-Host "Pre-startup tests error: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Aborting startup." -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "Warning: pre-startup-tests.ps1 not found at: $preStartupTestsScript" -ForegroundColor Yellow
    Write-Host "Skipping pre-startup tests..." -ForegroundColor Yellow
}
Write-Host ""

# Переходим в директорию API
$apiPath = Join-Path $PSScriptRoot "..\src\Orchestra.API"
if (-not (Test-Path $apiPath)) {
    Write-Host "API directory not found: $apiPath" -ForegroundColor Red
    exit 1
}

Set-Location $apiPath
Write-Host "Working directory: $(Get-Location)" -ForegroundColor Cyan

try {
    if ($Mode -eq "dev") {
        Write-Host "`nStarting in development mode..." -ForegroundColor Green
        $env:ASPNETCORE_URLS = "http://localhost:$Port"
        & dotnet run
    }
    elseif ($Mode -eq "build") {
        Write-Host "`nBuilding and running..." -ForegroundColor Green
        & dotnet build
        if ($LASTEXITCODE -eq 0) {
            $env:ASPNETCORE_URLS = "http://localhost:$Port"
            & dotnet run
        }
        else {
            Write-Host "Build failed" -ForegroundColor Red
            exit 1
        }
    }
    else {
        Write-Host "Unknown mode: $Mode. Use 'dev' or 'build'" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "Error starting orchestrator: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}