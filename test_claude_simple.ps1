$ErrorActionPreference = "Continue"

Write-Host "=== Testing Claude CLI ===" -ForegroundColor Green

# Test 1: Check if claude is accessible
Write-Host "`n1. Testing claude --help..." -ForegroundColor Cyan
try {
    & claude --help 2>&1 | Select-Object -First 5
    Write-Host "✓ Claude CLI is accessible" -ForegroundColor Green
} catch {
    Write-Host "✗ Error: $_" -ForegroundColor Red
    exit 1
}

# Test 2: Create test directory
$testDir = "C:\Temp\claude_e2e_manual_test"
if (Test-Path $testDir) {
    Remove-Item $testDir -Recurse -Force
}
New-Item -ItemType Directory -Path $testDir | Out-Null
Write-Host "`n2. Created test directory: $testDir" -ForegroundColor Cyan

# Test 3: Execute simple file creation command
Write-Host "`n3. Executing Claude command..." -ForegroundColor Cyan
Set-Location $testDir

$command = "Create a file called hello.txt with exactly this content: Test123"
Write-Host "Command: $command" -ForegroundColor Yellow

Write-Host "Full command line:" -ForegroundColor Yellow
$fullCmd = "claude --print --output-format text --dangerously-skip-permissions --add-dir `"$testDir`" `"$command`""
Write-Host $fullCmd -ForegroundColor Gray

try {
    # Use Start-Process to properly handle arguments
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "C:\Users\mrred\AppData\Roaming\npm\claude.cmd"
    $psi.Arguments = "--print --output-format text --dangerously-skip-permissions --add-dir `"$testDir`" `"$command`""
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.WorkingDirectory = $testDir

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    $process.Start() | Out-Null

    $output = $process.StandardOutput.ReadToEnd()
    $errorOutput = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    Write-Host "`nClaude Output:" -ForegroundColor Cyan
    Write-Host $output -ForegroundColor Gray
    if ($errorOutput) {
        Write-Host "`nClaude Error:" -ForegroundColor Red
        Write-Host $errorOutput -ForegroundColor Gray
    }
    Write-Host "`nExit Code: $($process.ExitCode)" -ForegroundColor Cyan
} catch {
    Write-Host "✗ Execution error: $_" -ForegroundColor Red
}

# Test 4: Check if file was created
Write-Host "`n4. Checking if file was created..." -ForegroundColor Cyan
Start-Sleep -Seconds 2

if (Test-Path "$testDir\hello.txt") {
    Write-Host "✓ SUCCESS: File created!" -ForegroundColor Green
    Write-Host "Content:" -ForegroundColor Cyan
    Get-Content "$testDir\hello.txt" -Raw
} else {
    Write-Host "✗ FAILED: File not created" -ForegroundColor Red
    Write-Host "Files in directory:" -ForegroundColor Yellow
    Get-ChildItem $testDir
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Green