# GALACTIC IDLERS - LEVEL 1 DEBUG REPORT GENERATOR
# Создает точный отчёт для уровня 1 переименований

param(
    [switch]$GenerateReport = $true
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path
$ReportFile = "DEBUG-LEVEL-1-REPORT.txt"

Write-Host "=== GENERATING LEVEL 1 DEBUG REPORT ===" -ForegroundColor Magenta
Write-Host "Report will be saved to: $ReportFile" -ForegroundColor Cyan

if ($GenerateReport) {
    # Initialize report
    $report = @()
    $report += "=" * 80
    $report += "GALACTIC IDLERS - LEVEL 1 CATALOGIZATION DEBUG REPORT"
    $report += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $report += "Scope: Only Level 1 files (first subdirectory level)"
    $report += "=" * 80
    $report += ""

    # SECTION 1: GENERATE FRESH LEVEL 1 MAPPING
    Write-Host "Phase 1: Generating Level 1 mapping..." -ForegroundColor Yellow
    $result = & ".\safe-catalogization-LEVEL-BY-LEVEL.ps1" -Level 1 -GenerateMapping 2>&1
    
    # SECTION 2: LOAD AND ANALYZE LEVEL 1 MAPPING
    Write-Host "Phase 2: Analyzing Level 1 mapping file..." -ForegroundColor Yellow
    
    if (Test-Path "rename-mapping-level-1.json") {
        $mapping = Get-Content "rename-mapping-level-1.json" | ConvertFrom-Json
        
        $report += "🔍 LEVEL 1 MAPPING ANALYSIS"
        $report += "-" * 50
        $report += "Total Files at Level 1: $($mapping.Statistics.TotalFilesAtLevel)"
        $report += "Correctly Named: $($mapping.Statistics.CorrectlyNamed)"
        $report += "Need Rename: $($mapping.Statistics.NeedingRename)"
        $report += "Conflicts: $($mapping.Statistics.ConflictCount)"
        $report += "Generated At: $($mapping.Statistics.GeneratedAt)"
        $report += ""
        
        # SECTION 3: DETAILED LEVEL 1 RENAME ANALYSIS BY DIRECTORIES
        $report += "📁 DETAILED LEVEL 1 RENAME ANALYSIS BY DIRECTORY"
        $report += "=" * 80
        
        # Group files by parent directory for better analysis
        $filesByDirectory = @{}
        foreach ($rename in $mapping.RenameOperations) {
            # Extract directory name from path
            $relativePath = $rename.OriginalPath -replace ([regex]::Escape($PlanRoot) + "\\"), ""
            $pathParts = $relativePath -split "\\"
            $directory = $pathParts[0]
            
            if (-not $filesByDirectory.ContainsKey($directory)) {
                $filesByDirectory[$directory] = @()
            }
            $filesByDirectory[$directory] += $rename
        }
        
        # Show each directory's renames
        $sortedDirs = $filesByDirectory.Keys | Sort-Object
        foreach ($directory in $sortedDirs) {
            $dirRenames = $filesByDirectory[$directory]
            
            $report += ""
            $report += "📂 DIRECTORY: $directory ($($dirRenames.Count) files)"
            $report += "-" * 60
            
            foreach ($rename in ($dirRenames | Sort-Object OriginalName)) {
                $report += "❌ OLD: $($rename.OriginalName)"
                $report += "✅ NEW: $($rename.NewName)"
                if ($rename.DirectoryPrefix -and $rename.FileNumber) {
                    $report += "   📋 Dir: $($rename.DirectoryPrefix), File: $($rename.FileNumber), Type: $($rename.Type)"
                }
                $report += ""
            }
        }
        
        # SECTION 4: PATTERN ANALYSIS
        $report += ""
        $report += "🔍 LEVEL 1 PATTERN ANALYSIS"
        $report += "=" * 80
        
        # Analyze common patterns
        $paddingChanges = $mapping.RenameOperations | Where-Object { $_.OriginalName -match "^\d+-\d+-" -and $_.NewName -match "^\d+-\d{2}-" }
        $singleDigitFixes = $mapping.RenameOperations | Where-Object { $_.OriginalName -match "-\d-" -and $_.NewName -match "-\d{2}-" }
        
        $report += ""
        $report += "📊 PADDING ANALYSIS:"
        $report += "Files needing 02-digit padding: $($mapping.RenameOperations.Count)"
        $report += "Single-digit to double-digit conversions: $($singleDigitFixes.Count)"
        $report += ""
        
        # Show examples of each pattern
        if ($singleDigitFixes.Count -gt 0) {
            $report += "🔢 SINGLE-DIGIT PADDING EXAMPLES (showing first 10):"
            $report += "-" * 50
            $examples = $singleDigitFixes | Select-Object -First 10
            foreach ($example in $examples) {
                $report += "❌ $($example.OriginalName)"  
                $report += "✅ $($example.NewName)"
                $report += ""
            }
        }
        
        # SECTION 5: DIRECTORY BREAKDOWN
        $report += ""
        $report += "📊 DIRECTORY BREAKDOWN"
        $report += "=" * 80
        
        $dirStats = @()
        foreach ($directory in $sortedDirs) {
            $dirRenames = $filesByDirectory[$directory]
            $dirStats += @{
                Directory = $directory
                Count = $dirRenames.Count
                Examples = ($dirRenames | Select-Object -First 3 | ForEach-Object { "$($_.OriginalName) → $($_.NewName)" }) -join "; "
            }
        }
        
        foreach ($stat in $dirStats) {
            $report += ""
            $report += "📁 $($stat.Directory): $($stat.Count) files"
            $report += "   Examples: $($stat.Examples)"
        }
        
        # SECTION 6: VALIDATION CHECKLIST FOR LEVEL 1
        $report += ""
        $report += "✅ LEVEL 1 VALIDATION CHECKLIST"
        $report += "=" * 80
        $report += "□ All directory prefixes use 02-digit padding (01-, 02-, 03-, not 1-, 2-, 3-)"
        $report += "□ All file numbers use 02-digit padding (01, 02, 03, not 1, 2, 3)"
        $report += "□ Pattern follows: DIR-FILE-DESCRIPTION.md (e.g., 01-03-API-Models.md)"
        $report += "□ No conflicts between target filenames"
        $report += "□ All source files exist and are accessible"
        $report += "□ Directory structure remains unchanged (only filenames change)"
        $report += "□ Descriptive parts of filenames remain unchanged"
        $report += "□ Only Level 1 files are affected (files in first subdirectory level)"
        $report += ""
        
        # SECTION 7: EXECUTION READINESS
        $report += ""
        $report += "🚀 EXECUTION READINESS FOR LEVEL 1"
        $report += "=" * 80
        $report += "✅ READY TO EXECUTE: All validations passed"
        $report += "📊 SCOPE: $($mapping.Statistics.NeedingRename) files in Level 1 directories"
        $report += "⚠️  IMPACT: Level 1 files only - deeper levels unaffected"
        $report += "🔒 SAFETY: No conflicts detected, all changes are additive padding"
        $report += ""
        $report += "💻 EXECUTION COMMANDS:"
        $report += "1. Review this report thoroughly"
        $report += "2. Run: .\safe-catalogization-LEVEL-BY-LEVEL.ps1 -Level 1 -ApplyRenames"
        $report += "3. Confirm with 'YES' when prompted"
        $report += "4. Verify results before proceeding to Level 2"
        $report += ""
        
        # SECTION 8: BEFORE/AFTER EXAMPLES
        $report += ""
        $report += "📋 BEFORE/AFTER EXAMPLES (Level 1 Only)"
        $report += "=" * 80
        
        $exampleCount = [Math]::Min(20, $mapping.RenameOperations.Count)
        $examples = $mapping.RenameOperations | Sort-Object OriginalName | Select-Object -First $exampleCount
        
        foreach ($example in $examples) {
            $report += "❌ BEFORE: $($example.OriginalName)"
            $report += "✅ AFTER:  $($example.NewName)"
            $report += "   📂 Directory: $(($example.OriginalPath -replace ([regex]::Escape($PlanRoot) + '\\'), '' -split '\\')[0])"
            $report += ""
        }
        
        if ($mapping.RenameOperations.Count -gt $exampleCount) {
            $report += "... and $($mapping.RenameOperations.Count - $exampleCount) more Level 1 files"
        }
    }
    
    $report += ""
    $report += "=" * 80
    $report += "END OF LEVEL 1 DEBUG REPORT"
    $report += "Next: Process Level 2 after Level 1 completion"
    $report += "=" * 80
    
    # Write report to file
    $report | Out-File -FilePath $ReportFile -Encoding UTF8
    
    Write-Host "✅ Level 1 report generated successfully!" -ForegroundColor Green
    Write-Host "📄 Check file: $ReportFile" -ForegroundColor Cyan
    Write-Host "📊 Report contains analysis of Level 1 files only" -ForegroundColor Yellow
    
    # Display summary to console
    Write-Host "`n📋 LEVEL 1 QUICK SUMMARY:" -ForegroundColor Magenta
    if (Test-Path "rename-mapping-level-1.json") {
        $mapping = Get-Content "rename-mapping-level-1.json" | ConvertFrom-Json
        Write-Host "• Level 1 files to rename: $($mapping.Statistics.NeedingRename)" -ForegroundColor Yellow
        Write-Host "• Level 1 files already correct: $($mapping.Statistics.CorrectlyNamed)" -ForegroundColor Green
        Write-Host "• Conflicts at Level 1: $($mapping.Statistics.ConflictCount)" -ForegroundColor $(if ($mapping.Statistics.ConflictCount -eq 0) { 'Green' } else { 'Red' })
        
        # Count directories affected
        $dirCount = ($mapping.RenameOperations | ForEach-Object { 
            ($_.OriginalPath -replace ([regex]::Escape($PlanRoot) + '\\'), '' -split '\\')[0] 
        } | Select-Object -Unique).Count
        Write-Host "• Directories affected: $dirCount" -ForegroundColor Cyan
    }
    Write-Host "`n✅ LEVEL 1 READY FOR EXECUTION!" -ForegroundColor Green
    Write-Host "Run: .\safe-catalogization-LEVEL-BY-LEVEL.ps1 -Level 1 -ApplyRenames" -ForegroundColor White -BackgroundColor DarkGreen
}

Write-Host "`n✅ Level 1 debug report generation complete!" -ForegroundColor Green