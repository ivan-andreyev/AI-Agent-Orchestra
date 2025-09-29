$testDir = "C:\Temp\test_claude_e2e_manual"
if (Test-Path $testDir) {
    Remove-Item $testDir -Recurse -Force
}
New-Item -ItemType Directory -Path $testDir | Out-Null

Write-Host "Testing Claude CLI execution..."
Write-Host "Working directory: $testDir"

$command = "Create a file called hello.txt with content: Test123"
Write-Host "Command: $command"

cd $testDir
claude --print --output-format text --add-dir $testDir $command

Write-Host "`nChecking if file was created..."
if (Test-Path "$testDir\hello.txt") {
    Write-Host "SUCCESS: File created!"
    Get-Content "$testDir\hello.txt"
} else {
    Write-Host "FAILED: File not created"
    Get-ChildItem $testDir
}