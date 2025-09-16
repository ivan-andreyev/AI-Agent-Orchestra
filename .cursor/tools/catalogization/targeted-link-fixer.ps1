# GALACTIC IDLERS - TARGETED PATTERN-BASED LINK FIXER
# Исправляет ссылки на основе точных паттернов переименований

param(
    [switch]$Preview = $true,
    [switch]$Execute = $false
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path

# Safety Colors
function Write-Safety { param($msg) Write-Host $msg -ForegroundColor Magenta }
function Write-Danger { param($msg) Write-Host $msg -ForegroundColor Red -BackgroundColor Yellow }
function Write-Safe { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }

Write-Safety "=== GALACTIC IDLERS - TARGETED PATTERN-BASED LINK FIXER ==="
Write-Info "Fixing links using exact filename patterns from Level 2 catalogization"

Write-Info "`n=== PHASE 1: BUILDING PATTERN REPLACEMENTS ==="

# Define exact pattern replacements based on our Level 2 catalogization
$filenameReplacements = @{
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
    "01-3-1-Save-Load-API-Models.md" = "01-01-01-Save-Load-API-Models.md"
    "01-3-2-Game-Operation-API-Models.md" = "01-01-02-Game-Operation-API-Models.md"
    "01-3-3-Utilities-Coordinator.md" = "01-01-03-Utilities-Coordinator.md"
    "01-3-3-Utilities.md" = "01-01-03-Utilities.md"
    "01-3-API-Interaction-Models.md" = "01-01-03-API-Interaction-Models.md"
    "01-3-Supporting.md" = "01-01-03-Supporting.md"
    "01-3-API.md" = "01-01-03-API.md"
    "01-3-SyncGame-Models.md" = "01-01-03-SyncGame-Models.md"
    "01-4-1-Base-Command-Query-Event-Models.md" = "01-01-01-Base-Command-Query-Event-Models.md"
    "01-4-2-Game-Specific-Commands-Queries.md" = "01-01-02-Game-Specific-Commands-Queries.md"
    "01-4-3-Result-Models-Validation.md" = "01-01-03-Result-Models-Validation.md"
    "01-4-4-Performance-Optimization.md" = "01-01-04-Performance-Optimization.md"
    "01-4-Core-CQRS-Architecture.md" = "01-01-04-Core-CQRS-Architecture.md"
    "01-4-Service-Migration-Strategy.md" = "01-01-04-Service-Migration-Strategy.md"
    "01-5-1-Unity-Serializable-Models.md" = "01-01-01-Unity-Serializable-Models.md"
    "01-5-Bridge.md" = "01-01-05-Bridge.md"
    "01-5-Unity-Bridge-Models.md" = "01-01-05-Unity-Bridge-Models.md"
    "02-1-1-Core-Implementation.md" = "02-02-01-Core-Implementation.md"
    "02-1-1-IGameFramework-Universal-Interface.md" = "02-02-01-IGameFramework-Universal-Interface.md"
    "02-1-2-Command-Pipeline-Architecture.md" = "02-02-02-Command-Pipeline-Architecture.md"
    "02-1-2-Service-Registration.md" = "02-02-02-Service-Registration.md"
    "02-1-3-Dependency-Injection-Framework.md" = "02-02-03-Dependency-Injection-Framework.md"
    "02-1-3-Pipeline-Behaviors.md" = "02-02-03-Pipeline-Behaviors.md"
    "02-1-4-Performance-Monitoring-Core.md" = "02-02-04-Performance-Monitoring-Core.md"
    "02-1-4-Testing-Framework.md" = "02-02-04-Testing-Framework.md"
    "02-1-5-Enterprise-Security-Framework.md" = "02-02-05-Enterprise-Security-Framework.md"
    "02-1-5-Validation-Scripts.md" = "02-02-05-Validation-Scripts.md"
    "02-1-Core-Framework-Decomposition-Coordinator.md" = "02-02-01-Core-Framework-Decomposition-Coordinator.md"
    "02-1-Core-Framework.md" = "02-02-01-Core-Framework.md"
    "02-1-IGameFramework-Universal.md" = "02-02-01-IGameFramework-Universal.md"
    "02-1-Resource-Management-Interfaces.md" = "02-02-01-Resource-Management-Interfaces.md"
}

# Extend with more patterns automatically
$additionalPatterns = @{}
for ($i = 1; $i -le 9; $i++) {
    for ($j = 1; $j -le 9; $j++) {
        for ($k = 1; $k -le 9; $k++) {
            $oldPattern = "$($i.ToString('00'))-$j-$k-"
            $newPattern = "$($i.ToString('00'))-$($i.ToString('00'))-$($k.ToString('00'))-"
            $additionalPatterns[$oldPattern] = $newPattern
        }
    }
}

Write-Info "✅ Loaded $($filenameReplacements.Count) exact filename mappings"
Write-Info "✅ Generated $($additionalPatterns.Count) additional pattern mappings"

Write-Info "`n=== PHASE 2: SCANNING AND FIXING LINKS ==="

$allFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
    $_.FullName -notmatch "\\reviews\\" 
}

$linkFixes = @()
$processedFiles = 0

foreach ($file in $allFiles) {
    $processedFiles++
    if ($processedFiles % 100 -eq 0) {
        Write-Info "Processed $processedFiles/$($allFiles.Count) files..."
    }
    
    $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
    
    # Read file content
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    
    $lines = $content -split "`n"
    $lineNumber = 0
    
    foreach ($line in $lines) {
        $lineNumber++
        
        # Find all markdown links
        $links = [regex]::Matches($line, '\[([^\]]*)\]\(([^)]+)\)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        
        foreach ($link in $links) {
            $linkText = $link.Groups[1].Value
            $linkPath = $link.Groups[2].Value
            
            # Skip external links and anchors
            if ($linkPath -match "^https?://") { continue }
            if ($linkPath -match "^#") { continue }
            if ($linkPath.EndsWith("/")) { continue }  # Skip directory links
            
            $needsFix = $false
            $newLinkPath = $linkPath
            
            # Try exact filename replacement
            foreach ($oldFilename in $filenameReplacements.Keys) {
                if ($linkPath.Contains($oldFilename)) {
                    $newFilename = $filenameReplacements[$oldFilename]
                    $newLinkPath = $linkPath -replace [regex]::Escape($oldFilename), $newFilename
                    $needsFix = $true
                    break
                }
            }
            
            # Try pattern-based replacement if not fixed yet
            if (-not $needsFix) {
                foreach ($oldPattern in $additionalPatterns.Keys) {
                    if ($linkPath.Contains($oldPattern)) {
                        $newPattern = $additionalPatterns[$oldPattern]
                        $newLinkPath = $linkPath -replace [regex]::Escape($oldPattern), $newPattern
                        $needsFix = $true
                        break
                    }
                }
            }
            
            if ($needsFix) {
                $linkFixes += @{
                    SourceFile = $file.FullName
                    SourceRelativePath = $relativePath
                    LineNumber = $lineNumber
                    OldLine = $line
                    OldLinkPath = $linkPath
                    NewLinkPath = $newLinkPath
                    LinkText = $linkText
                    FullOldMatch = $link.Groups[0].Value
                    FullNewMatch = "[$linkText]($newLinkPath)"
                }
            }
        }
    }
}

Write-Info "`n=== PHASE 3: TARGETED ANALYSIS RESULTS ==="
Write-Safe "✅ Targeted pattern-based fixes available: $($linkFixes.Count)"

if ($Preview -and $linkFixes.Count -gt 0) {
    Write-Info "`n=== PREVIEW: FIRST 15 TARGETED FIXES ==="
    
    $linkFixes | Select-Object -First 15 | ForEach-Object {
        Write-Host "`n📄 SOURCE: $($_.SourceRelativePath):$($_.LineNumber)" -ForegroundColor White
        Write-Host "❌ OLD LINK: $($_.FullOldMatch)" -ForegroundColor Red
        Write-Host "✅ NEW LINK: $($_.FullNewMatch)" -ForegroundColor Green
    }
    
    if ($linkFixes.Count -gt 15) {
        Write-Host "`n... and $($linkFixes.Count - 15) more targeted fixes available" -ForegroundColor Yellow
    }
}

if ($Execute -and $linkFixes.Count -gt 0) {
    Write-Danger "`n=== PHASE 4: EXECUTING TARGETED LINK FIXES ==="
    
    $confirm = Read-Host "Type 'APPLY TARGETED FIXES' to fix $($linkFixes.Count) links"
    
    if ($confirm -eq "APPLY TARGETED FIXES") {
        Write-Info "`n--- APPLYING TARGETED FIXES ---"
        
        # Group fixes by source file
        $fixesByFile = $linkFixes | Group-Object SourceFile
        $filesUpdated = 0
        $totalFixesApplied = 0
        
        Write-Info "Processing $($fixesByFile.Count) files with grouped fixes..."
        
        foreach ($fileGroup in $fixesByFile) {
            $sourceFile = $fileGroup.Name
            Write-Info "Working on file: '$sourceFile' (length: $($sourceFile.Length))"
            $fileFixes = $fileGroup.Group
            
            if (-not $sourceFile -or $sourceFile -eq "") {
                Write-Danger "❌ Skipping empty source file path"
                continue
            }
            
            try {
                # Read file content
                $content = Get-Content $sourceFile -Raw -Encoding UTF8
                $originalContent = $content
                
                # Apply all fixes for this file
                foreach ($fix in $fileFixes) {
                    $content = $content -replace [regex]::Escape($fix.FullOldMatch), $fix.FullNewMatch
                    $totalFixesApplied++
                }
                
                # Write updated content back
                if ($content -ne $originalContent) {
                    Set-Content -Path $sourceFile -Value $content -Encoding UTF8 -NoNewline
                    $filesUpdated++
                    $fileRelPath = $fileFixes[0].SourceRelativePath
                    Write-Safe "✅ Updated $($fileFixes.Count) links in: $fileRelPath"
                }
            } catch {
                Write-Danger "❌ Failed to update links in $($fileFixes[0].SourceRelativePath): $($_.Exception.Message)"
            }
        }
        
        Write-Safe "`n🎉 TARGETED LINK FIXING COMPLETE!"
        Write-Host "✅ Applied $totalFixesApplied targeted fixes across $filesUpdated files" -ForegroundColor Green
        
    } else {
        Write-Info "❌ Operation cancelled"
    }
} elseif ($linkFixes.Count -eq 0) {
    Write-Safe "`n✅ NO BROKEN LINKS FOUND - All links are already correct!"
}

Write-Info "`n=== TARGETED LINK FIXER SUMMARY ==="
Write-Host "Pattern-based fixes applied: $($linkFixes.Count)" -ForegroundColor Green

Write-Info "`n=== TARGETED LINK FIXER COMPLETE ==="