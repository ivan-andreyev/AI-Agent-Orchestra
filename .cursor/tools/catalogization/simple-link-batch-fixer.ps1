# SIMPLE BATCH LINK FIXER - Direct pattern replacements
param(
    [switch]$Execute = $false
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path

# Define exact string replacements
$replacements = @{
    "00-3-1-Architectural-Principles-Structure.md" = "00-00-01-Architectural-Principles-Structure.md"
    "00-3-2-Implementation-Tasks-Requirements.md" = "00-00-02-Implementation-Tasks-Requirements.md" 
    "00-3-3-Idle-Game-Architecture-Patterns.md" = "00-00-03-Idle-Game-Architecture-Patterns.md"
    "00-3-4-Implementation-Result.md" = "00-00-04-Implementation-Result.md"
    "01-1-1-GameState-Entity.md" = "01-01-01-GameState-Entity.md"
    "01-1-2-Resource-Model.md" = "01-01-02-Resource-Model.md"
    "01-1-3-Player-Model.md" = "01-01-03-Player-Model.md"
    "01-1-4-Validation-Implementation.md" = "01-01-04-Validation-Implementation.md"
    "01-1-1-Game-State-Models.md" = "01-01-01-Game-State-Models.md"
    "01-1-2-API-Models.md" = "01-01-02-API-Models.md"
    "01-2-1-Progression-Models.md" = "01-01-01-Progression-Models.md"
    "01-2-2-Database-Models.md" = "01-01-02-Database-Models.md"
    "01-2-1-Upgrade-Model.md" = "01-01-01-Upgrade-Model.md"
    "01-2-2-Generator-Model.md" = "01-01-02-Generator-Model.md"
    "01-2-3-Achievement-Model.md" = "01-01-03-Achievement-Model.md"
    "01-2-4-Validation-Framework.md" = "01-01-04-Validation-Framework.md"
    "02-1-1-Core-Implementation.md" = "02-02-01-Core-Implementation.md"
    "02-1-2-Service-Registration.md" = "02-02-02-Service-Registration.md"
    "02-1-3-Dependency-Injection-Framework.md" = "02-02-03-Dependency-Injection-Framework.md"
    "02-2-1-UpgradeService-Interface.md" = "02-02-01-UpgradeService-Interface.md"
    "02-2-2-GeneratorService-Interface.md" = "02-02-02-GeneratorService-Interface.md"
    "03-2-2-Progression" = "03-03-02-Progression"
    "03-2-3-Integration" = "03-03-03-Integration"
}

# Find all markdown files
$allFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
    $_.FullName -notmatch "\\reviews\\" 
}

$totalReplacements = 0
$filesModified = 0

Write-Host "=== SIMPLE BATCH LINK FIXER ===" -ForegroundColor Magenta
Write-Host "Processing $($allFiles.Count) files..." -ForegroundColor Cyan

foreach ($file in $allFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    
    $originalContent = $content
    $fileReplacements = 0
    
    foreach ($oldPattern in $replacements.Keys) {
        $newPattern = $replacements[$oldPattern]
        $beforeCount = ([regex]::Matches($content, [regex]::Escape($oldPattern))).Count
        if ($beforeCount -gt 0) {
            $content = $content -replace [regex]::Escape($oldPattern), $newPattern
            $fileReplacements += $beforeCount
            $totalReplacements += $beforeCount
        }
    }
    
    if ($content -ne $originalContent) {
        $filesModified++
        $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
        Write-Host "✅ $relativePath ($fileReplacements replacements)" -ForegroundColor Green
        
        if ($Execute) {
            Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
        }
    }
}

Write-Host "`n=== BATCH FIX RESULTS ===" -ForegroundColor Magenta
Write-Host "Files to modify: $filesModified" -ForegroundColor Green
Write-Host "Total replacements: $totalReplacements" -ForegroundColor Green

if (-not $Execute) {
    Write-Host "`nRun with -Execute to apply changes" -ForegroundColor Yellow
} else {
    Write-Host "`n✅ All fixes applied successfully!" -ForegroundColor Green
}