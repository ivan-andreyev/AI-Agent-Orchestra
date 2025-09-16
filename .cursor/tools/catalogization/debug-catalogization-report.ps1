# GALACTIC IDLERS - COMPREHENSIVE DEBUG REPORT GENERATOR
# Создает полный отчёт для ручного контроля переименований и ссылок

param(
    [switch]$GenerateReport = $true
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path
$ReportFile = "DEBUG-CATALOGIZATION-REPORT.txt"

Write-Host "=== GENERATING COMPREHENSIVE DEBUG REPORT ===" -ForegroundColor Magenta
Write-Host "Report will be saved to: $ReportFile" -ForegroundColor Cyan

if ($GenerateReport) {
    # Initialize report
    $report = @()
    $report += "=" * 80
    $report += "GALACTIC IDLERS - COMPREHENSIVE CATALOGIZATION DEBUG REPORT"
    $report += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $report += "=" * 80
    $report += ""

    # SECTION 1: GENERATE FRESH MAPPING
    Write-Host "Phase 1: Generating fresh mapping..." -ForegroundColor Yellow
    $result = & ".\safe-catalogization-phase1-CORRECT.ps1" -GenerateMapping 2>&1
    
    # SECTION 2: LOAD AND ANALYZE MAPPING
    Write-Host "Phase 2: Analyzing mapping file..." -ForegroundColor Yellow
    
    if (Test-Path "rename-mapping-fixed.json") {
        $mapping = Get-Content "rename-mapping-fixed.json" | ConvertFrom-Json
        
        $report += "🔍 MAPPING ANALYSIS"
        $report += "-" * 50
        $report += "Total Files: $($mapping.Statistics.TotalFiles)"
        $report += "Correctly Named: $($mapping.Statistics.CorrectlyNamed)"
        $report += "Need Rename: $($mapping.Statistics.NeedingRename)"
        $report += "Conflicts: $($mapping.Statistics.ConflictCount)"
        $report += "Generated At: $($mapping.Statistics.GeneratedAt)"
        $report += ""
        
        # SECTION 3: DETAILED FILE RENAME ANALYSIS BY CATEGORIES
        $report += "📁 DETAILED RENAME ANALYSIS BY FILE CATEGORIES"
        $report += "=" * 80
        
        # Group files by depth for analysis
        $byDepth = $mapping.RenameOperations | Group-Object Depth | Sort-Object Name
        
        foreach ($depthGroup in $byDepth) {
            $depth = $depthGroup.Name
            $files = $depthGroup.Group
            
            $report += ""
            $report += "📊 DEPTH $depth ANALYSIS ($($files.Count) files)"
            $report += "-" * 60
            
            # Show samples from each depth
            $samples = $files | Select-Object -First 10
            foreach ($file in $samples) {
                $oldRel = $file.OriginalPath -replace [regex]::Escape($PlanRoot + "\"), ""
                $newRel = $file.NewPath -replace [regex]::Escape($PlanRoot + "\"), ""
                
                $report += "❌ OLD: $oldRel"
                $report += "✅ NEW: $newRel"
                $report += "   📋 Prefix: '$($file.ExpectedPrefix)' | Position: $($file.FilePosition) | Depth: $($file.Depth)"
                $report += ""
            }
            
            if ($files.Count -gt 10) {
                $report += "... and $($files.Count - 10) more files at this depth"
                $report += ""
            }
        }
        
        # SECTION 4: SPECIFIC PROBLEMATIC CASES ANALYSIS
        $report += ""
        $report += "🚨 SPECIFIC CASE ANALYSIS"
        $report += "=" * 80
        
        # Root files analysis
        $rootFiles = $mapping.RenameOperations | Where-Object { $_.Depth -eq 0 }
        $report += ""
        $report += "🏠 ROOT LEVEL FILES ($($rootFiles.Count) files):"
        $report += "-" * 40
        foreach ($file in $rootFiles) {
            $report += "❌ $($file.OriginalName) → ✅ $($file.NewName)"
        }
        
        # Deep nested files
        $deepFiles = $mapping.RenameOperations | Where-Object { $_.Depth -ge 6 } | Select-Object -First 15
        if ($deepFiles.Count -gt 0) {
            $report += ""
            $report += "🔽 DEEPLY NESTED FILES (Depth 6+, showing first 15):"
            $report += "-" * 50
            foreach ($file in $deepFiles) {
                $oldRel = $file.OriginalPath -replace [regex]::Escape($PlanRoot + "\"), ""
                $newRel = $file.NewPath -replace [regex]::Escape($PlanRoot + "\"), ""
                $report += "❌ $oldRel"
                $report += "✅ $newRel"
                $report += ""
            }
        }
        
        # Files with complex prefixes
        $complexFiles = $mapping.RenameOperations | Where-Object { $_.ExpectedPrefix -match "^(\d{2}-){3,}" } | Select-Object -First 10
        if ($complexFiles.Count -gt 0) {
            $report += ""
            $report += "🔢 COMPLEX PREFIX FILES (4+ levels, showing first 10):"
            $report += "-" * 50
            foreach ($file in $complexFiles) {
                $report += "❌ $($file.OriginalName)"
                $report += "✅ $($file.NewName)"
                $report += "   🎯 Complex Prefix: '$($file.ExpectedPrefix)'"
                $report += ""
            }
        }
    }
    
    # SECTION 5: LINK ANALYSIS
    Write-Host "Phase 3: Analyzing links with fresh mapping..." -ForegroundColor Yellow
    
    # Generate link analysis using Phase 2
    $linkResult = & ".\safe-catalogization-phase2.ps1" -PreviewUpdates 2>&1 | Out-String
    
    $report += ""
    $report += "🔗 LINK UPDATE ANALYSIS"
    $report += "=" * 80
    
    if (Test-Path "link-update-plan.json") {
        $linkPlan = Get-Content "link-update-plan.json" | ConvertFrom-Json
        
        $report += "📊 LINK STATISTICS:"
        $report += "Total Updates Needed: $($linkPlan.TotalUpdates)"
        $report += "Cannot Update: $($linkPlan.CannotUpdate)"
        $report += "Generated At: $($linkPlan.GeneratedAt)"
        $report += ""
        
        # Show different types of link updates
        $updatesByReason = $linkPlan.Updates | Group-Object UpdateReason
        
        foreach ($reasonGroup in $updatesByReason) {
            $report += ""
            $report += "📋 $($reasonGroup.Name.ToUpper()) ($($reasonGroup.Count) links):"
            $report += "-" * 50
            
            $samples = $reasonGroup.Group | Select-Object -First 8
            foreach ($update in $samples) {
                $sourceRel = $update.SourceFile -replace [regex]::Escape($PlanRoot + "\"), ""
                $report += "📁 File: $sourceRel (Line: $($update.LineNumber))"
                $report += "❌ OLD: $($update.OldFullMatch)"
                $report += "✅ NEW: $($update.NewFullMatch)"
                $report += ""
            }
            
            if ($reasonGroup.Count -gt 8) {
                $report += "... and $($reasonGroup.Count - 8) more similar link updates"
                $report += ""
            }
        }
    }
    
    # SECTION 6: EDGE CASES AND POTENTIAL ISSUES
    $report += ""
    $report += "⚠️  POTENTIAL EDGE CASES TO MANUALLY VERIFY"
    $report += "=" * 80
    
    if (Test-Path "rename-mapping-fixed.json") {
        # Files with very long names
        $longNameFiles = $mapping.RenameOperations | Where-Object { $_.NewName.Length -gt 100 } | Select-Object -First 5
        if ($longNameFiles.Count -gt 0) {
            $report += ""
            $report += "📏 VERY LONG NEW NAMES (>100 chars, showing first 5):"
            $report += "-" * 40
            foreach ($file in $longNameFiles) {
                $report += "Length: $($file.NewName.Length) chars"
                $report += "✅ $($file.NewName)"
                $report += ""
            }
        }
        
        # Files with special characters
        $specialFiles = $mapping.RenameOperations | Where-Object { $_.NewName -match "[^a-zA-Z0-9\-\._]" } | Select-Object -First 5
        if ($specialFiles.Count -gt 0) {
            $report += ""
            $report += "🔤 FILES WITH SPECIAL CHARACTERS (showing first 5):"
            $report += "-" * 40
            foreach ($file in $specialFiles) {
                $report += "❌ $($file.OriginalName)"
                $report += "✅ $($file.NewName)"
                $report += ""
            }
        }
        
        # Files where prefix changed significantly
        $bigPrefixChanges = $mapping.RenameOperations | Where-Object { 
            $_.ExpectedPrefix.Length -gt ($_.OriginalName -replace "-.*", "").Length + 10 
        } | Select-Object -First 8
        if ($bigPrefixChanges.Count -gt 0) {
            $report += ""
            $report += "🔄 SIGNIFICANT PREFIX CHANGES (showing first 8):"
            $report += "-" * 40
            foreach ($file in $bigPrefixChanges) {
                $oldPrefix = if ($file.OriginalName -match "^(\d+(?:-\d+)*)-") { $matches[1] } else { "NONE" }
                $report += "❌ OLD PREFIX: '$oldPrefix' | NEW PREFIX: '$($file.ExpectedPrefix)'"
                $report += "   File: $($file.OriginalName) → $($file.NewName)"
                $report += ""
            }
        }
    }
    
    # SECTION 7: FINAL VALIDATION CHECKLIST
    $report += ""
    $report += "✅ MANUAL VERIFICATION CHECKLIST"
    $report += "=" * 80
    $report += "□ Root files (Depth 0) have correct 01-13 sequential numbering"
    $report += "□ Deep nested files maintain proper hierarchy prefixes"
    $report += "□ All prefixes use 02-digit padding (01-02-03, not 1-2-3)"
    $report += "□ File positions within directories are sequential (01, 02, 03...)"
    $report += "□ Link updates preserve the correct link text"
    $report += "□ Relative paths in links are calculated correctly (./file.md, ../dir/file.md)"
    $report += "□ No filename length exceeds filesystem limits"
    $report += "□ Special characters in filenames are handled properly"
    $report += "□ Complex nested directories maintain correct path relationships"
    $report += "□ Both source and target renamed cases are handled"
    $report += ""
    $report += "=" * 80
    $report += "END OF DEBUG REPORT"
    $report += "=" * 80
    
    # Write report to file
    $report | Out-File -FilePath $ReportFile -Encoding UTF8
    
    Write-Host "✅ Report generated successfully!" -ForegroundColor Green
    Write-Host "📄 Check file: $ReportFile" -ForegroundColor Cyan
    Write-Host "📊 Report contains detailed analysis of all $($mapping.Statistics.TotalFiles) files" -ForegroundColor Yellow
    
    # Display summary to console
    Write-Host "`n📋 QUICK SUMMARY:" -ForegroundColor Magenta
    Write-Host "• Total files to rename: $($mapping.Statistics.NeedingRename)" -ForegroundColor Yellow
    Write-Host "• Total link updates: $(if (Test-Path 'link-update-plan.json') { (Get-Content 'link-update-plan.json' | ConvertFrom-Json).TotalUpdates } else { 'N/A' })" -ForegroundColor Yellow
    Write-Host "• Root files: $(($mapping.RenameOperations | Where-Object { $_.Depth -eq 0 }).Count)" -ForegroundColor Yellow
    Write-Host "• Deep files (6+ levels): $(($mapping.RenameOperations | Where-Object { $_.Depth -ge 6 }).Count)" -ForegroundColor Yellow
    Write-Host "`n👀 MANUAL REVIEW RECOMMENDED for edge cases marked in report!" -ForegroundColor Red
}

Write-Host "`n✅ Debug report generation complete!" -ForegroundColor Green