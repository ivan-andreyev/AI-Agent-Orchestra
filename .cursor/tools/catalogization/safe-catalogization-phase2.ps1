# GALACTIC IDLERS - SAFE CATALOGIZATION REPAIR - PHASE 2
# Link Analysis and Safe Preview Before Real Application

param(
    [switch]$AnalyzeLinks = $false,
    [switch]$PreviewUpdates = $false,
    [switch]$ApplyUpdates = $false,
    [switch]$ValidateAfter = $false
)

$PlanRoot = "Docs\PLAN\Galactic-Idlers-Plan"
$MappingFile = "rename-mapping-fixed.json"
$LinkAnalysisFile = "link-analysis.json"
$BackupBranch = "catalogization-phase2-backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

# Safety Colors
function Write-Safety { param($msg) Write-Host $msg -ForegroundColor Magenta }
function Write-Danger { param($msg) Write-Host $msg -ForegroundColor Red -BackgroundColor Yellow }
function Write-Safe { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }
function Write-Preview { param($msg) Write-Host $msg -ForegroundColor Yellow }

Write-Safety "=== GALACTIC IDLERS - SAFE CATALOGIZATION PHASE 2 ==="
Write-Info "Stage: $(if($AnalyzeLinks) {'LINK ANALYSIS'} elseif($PreviewUpdates) {'SAFE PREVIEW'} elseif($ApplyUpdates) {'APPLY UPDATES'} else {'VALIDATION ONLY'})"

# Check dependencies
if (-not (Test-Path $MappingFile)) {
    Write-Danger "❌ Mapping file not found: $MappingFile"
    Write-Info "Run Phase 1 with -GenerateMapping first"
    exit 1
}

# Load rename mapping from Phase 1
$RenameMapping = Get-Content $MappingFile | ConvertFrom-Json
Write-Info "📊 Loaded $($RenameMapping.RenameOperations.Count) rename operations from Phase 1"

# Build path lookup tables for fast resolution
$OldToNewPath = @{}
$NewToOldPath = @{}
foreach ($rename in $RenameMapping.RenameOperations) {
    $OldToNewPath[$rename.OriginalPath] = $rename.NewPath
    $NewToOldPath[$rename.NewPath] = $rename.OriginalPath
}

# STAGE 1: Comprehensive Link Analysis
if ($AnalyzeLinks -or $PreviewUpdates) {
    Write-Info "`n=== STAGE 1: COMPREHENSIVE LINK ANALYSIS ==="
    
    $AllFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
        $_.FullName -notmatch "\\reviews\\" 
    }
    
    Write-Info "Analyzing links in $($AllFiles.Count) files..."
    
    $LinkDatabase = @()
    $TotalLinksFound = 0
    
    foreach ($file in $AllFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }
        
        # Find all markdown links: [text](path.md)
        $links = [regex]::Matches($content, '\[([^\]]*)\]\(([^)]+\.md[^)]*)\)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        
        foreach ($link in $links) {
            $linkText = $link.Groups[1].Value
            $linkPath = $link.Groups[2].Value.Trim()
            $fullMatch = $link.Value
            
            # Skip external links (http/https)
            if ($linkPath -match '^https?://') { continue }
            
            # Resolve relative path to absolute path
            $sourceDir = Split-Path $file.FullName -Parent
            $resolvedPath = $null
            
            try {
                if ($linkPath.StartsWith('./') -or $linkPath.StartsWith('../') -or -not $linkPath.StartsWith('/')) {
                    $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $sourceDir $linkPath))
                } else {
                    $resolvedPath = Join-Path $PlanRoot $linkPath.TrimStart('/')
                }
            }
            catch {
                Write-Preview "⚠️  Could not resolve path: $linkPath in file $($file.Name)"
                continue
            }
            
            $LinkDatabase += @{
                SourceFile = $file.FullName
                SourceRelative = $file.FullName -replace [regex]::Escape($PlanRoot + "\"), ""
                LinkText = $linkText
                OriginalLinkPath = $linkPath
                ResolvedTargetPath = $resolvedPath
                FullMatch = $fullMatch
                TargetExists = (Test-Path $resolvedPath -ErrorAction SilentlyContinue)
                LineNumber = ($content.Substring(0, $link.Index) -split "`n").Count
            }
            
            $TotalLinksFound++
        }
    }
    
    Write-Safe "✅ Found $TotalLinksFound internal markdown links in $($AllFiles.Count) files"
    Write-Info "📊 Links pointing to existing files: $(($LinkDatabase | Where-Object { $_.TargetExists }).Count)"
    Write-Preview "⚠️  Broken links found: $(($LinkDatabase | Where-Object { -not $_.TargetExists }).Count)"
    
    # Save link analysis
    $LinkAnalysis = @{
        TotalFiles = $AllFiles.Count
        TotalLinks = $TotalLinksFound
        ExistingLinks = ($LinkDatabase | Where-Object { $_.TargetExists }).Count
        BrokenLinks = ($LinkDatabase | Where-Object { -not $_.TargetExists }).Count
        AnalyzedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        Links = $LinkDatabase
    }
    
    $analysisJson = $LinkAnalysis | ConvertTo-Json -Depth 10
    Set-Content $LinkAnalysisFile -Value $analysisJson
    Write-Safe "✅ Link analysis saved to: $LinkAnalysisFile"
}

# STAGE 2: Safe Preview of Link Updates  
if ($PreviewUpdates) {
    Write-Info "`n=== STAGE 2: SAFE PREVIEW OF LINK UPDATES ==="
    
    # Load link analysis
    if (-not (Test-Path $LinkAnalysisFile)) {
        Write-Danger "❌ Link analysis file not found. Run with -AnalyzeLinks first."
        exit 1
    }
    
    $LinkAnalysis = Get-Content $LinkAnalysisFile | ConvertFrom-Json
    Write-Info "📊 Loaded analysis of $($LinkAnalysis.TotalLinks) links"
    
    $LinkUpdates = @()
    $UpdatesNeeded = 0
    $CannotUpdate = 0
    
    foreach ($link in $LinkAnalysis.Links) {
        $targetNeedsRename = $OldToNewPath.ContainsKey($link.ResolvedTargetPath)
        $sourceNeedsRename = $OldToNewPath.ContainsKey($link.SourceFile)
        
        if (-not $targetNeedsRename -and -not $sourceNeedsRename) {
            # No update needed - neither source nor target are being renamed
            continue
        }
        
        # Calculate new paths
        $newSourcePath = if ($sourceNeedsRename) { $OldToNewPath[$link.SourceFile] } else { $link.SourceFile }
        $newTargetPath = if ($targetNeedsRename) { $OldToNewPath[$link.ResolvedTargetPath] } else { $link.ResolvedTargetPath }
        
        # Calculate new relative path from new source to new target
        try {
            $newSourceDir = Split-Path $newSourcePath -Parent
            # Manual relative path calculation for PowerShell compatibility
            $sourceParts = $newSourceDir.TrimEnd('\') -split '\\'
            $targetParts = $newTargetPath.TrimEnd('\') -split '\\'
            
            # Find common base path
            $commonLength = 0
            $minLength = [Math]::Min($sourceParts.Length, $targetParts.Length)
            for ($i = 0; $i -lt $minLength; $i++) {
                if ($sourceParts[$i] -eq $targetParts[$i]) {
                    $commonLength++
                } else {
                    break
                }
            }
            
            # Calculate relative path
            $upLevels = $sourceParts.Length - $commonLength
            $downParts = $targetParts[$commonLength..($targetParts.Length-1)]
            
            if ($upLevels -eq 0 -and $downParts.Count -eq 0) {
                # Same directory
                $newRelativePath = "./" + (Split-Path $newTargetPath -Leaf)
            } elseif ($upLevels -eq 0) {
                # Target is deeper
                $newRelativePath = "./" + ($downParts -join '/')
            } else {
                # Need to go up
                $upPath = ('../' * $upLevels).TrimEnd('/')
                if ($downParts.Count -gt 0) {
                    $newRelativePath = "$upPath/" + ($downParts -join '/')
                } else {
                    $newRelativePath = $upPath
                }
            }
            
            $newRelativePath = $newRelativePath -replace '\\', '/'
            
            # Ensure proper ./ prefix for same-directory links
            if (-not $newRelativePath.StartsWith('../') -and -not $newRelativePath.StartsWith('./')) {
                $newRelativePath = "./$newRelativePath"
            }
            
            $newFullMatch = $link.FullMatch -replace [regex]::Escape($link.OriginalLinkPath), $newRelativePath
            
            $LinkUpdates += @{
                SourceFile = $link.SourceFile
                NewSourceFile = $newSourcePath
                SourceRelative = $link.SourceRelative
                LineNumber = $link.LineNumber
                LinkText = $link.LinkText
                OldLinkPath = $link.OriginalLinkPath
                NewLinkPath = $newRelativePath
                OldFullMatch = $link.FullMatch
                NewFullMatch = $newFullMatch
                TargetExists = $link.TargetExists
                UpdateReason = if ($sourceNeedsRename -and $targetNeedsRename) { "Both source and target renamed" } 
                             elseif ($sourceNeedsRename) { "Source file renamed" }
                             else { "Target file renamed" }
            }
            
            $UpdatesNeeded++
        }
        catch {
            Write-Preview "⚠️  Cannot calculate relative path for link in $($link.SourceRelative):$($link.LineNumber)"
            $CannotUpdate++
        }
    }
    
    Write-Safe "✅ Link updates calculated: $UpdatesNeeded updates needed"
    Write-Preview "⚠️  Cannot update: $CannotUpdate links"
    
    # Show preview of updates
    Write-Info "`n=== LINK UPDATE PREVIEW (Top 15 examples) ==="
    $LinkUpdates[0..14] | ForEach-Object {
        Write-Host "📁 $($_.SourceRelative):$($_.LineNumber)" -ForegroundColor Cyan
        Write-Host "❌ OLD: $($_.OldFullMatch)" -ForegroundColor Red
        Write-Host "✅ NEW: $($_.NewFullMatch)" -ForegroundColor Green
        Write-Host "   📋 Reason: $($_.UpdateReason)" -ForegroundColor Yellow
        Write-Host ""
    }
    
    # Show statistics by update reason
    Write-Info "`n=== UPDATE STATISTICS ==="
    $updateReasons = $LinkUpdates | Group-Object UpdateReason
    $updateReasons | ForEach-Object {
        Write-Host "$($_.Name): $($_.Count) links" -ForegroundColor Cyan
    }
    
    # Save update plan
    $UpdatePlan = @{
        TotalUpdates = $UpdatesNeeded
        CannotUpdate = $CannotUpdate
        GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        Updates = $LinkUpdates
    }
    
    $updatePlanJson = $UpdatePlan | ConvertTo-Json -Depth 10
    Set-Content "link-update-plan.json" -Value $updatePlanJson
    Write-Safe "✅ Update plan saved to: link-update-plan.json"
}

# STAGE 3: Apply Updates (DANGEROUS - only after manual verification)
if ($ApplyUpdates) {
    Write-Danger "`n🚨 DANGER: APPLYING REAL CHANGES! 🚨"
    Write-Info "This will modify files and rename them permanently!"
    
    # Load update plan
    if (-not (Test-Path "link-update-plan.json")) {
        Write-Danger "❌ Update plan not found. Run with -PreviewUpdates first."
        exit 1
    }
    
    Write-Info "Creating git backup branch: $BackupBranch"
    git checkout -b $BackupBranch
    if ($LASTEXITCODE -ne 0) {
        Write-Danger "❌ Failed to create backup branch. Aborting."
        exit 1
    }
    
    $UpdatePlan = Get-Content "link-update-plan.json" | ConvertFrom-Json
    Write-Info "📊 Applying $($UpdatePlan.TotalUpdates) link updates..."
    
    # Group updates by source file for efficient processing
    $UpdatesByFile = $UpdatePlan.Updates | Group-Object SourceFile
    
    foreach ($fileGroup in $UpdatesByFile) {
        $filePath = $fileGroup.Name
        Write-Info "📝 Updating $($fileGroup.Count) links in: $($filePath -replace [regex]::Escape($PlanRoot + '\'), '')"
        
        $content = Get-Content $filePath -Raw
        
        # Apply all updates for this file
        foreach ($update in $fileGroup.Group) {
            $content = $content -replace [regex]::Escape($update.OldFullMatch), $update.NewFullMatch
        }
        
        Set-Content $filePath -Value $content -NoNewline
    }
    
    Write-Safe "✅ All link updates applied"
    
    # Apply file renames
    Write-Info "`n📁 Applying $($RenameMapping.RenameOperations.Count) file renames..."
    
    foreach ($rename in $RenameMapping.RenameOperations) {
        Move-Item $rename.OriginalPath $rename.NewPath
        Write-Info "✅ $($rename.OriginalName) → $($rename.NewName)"
    }
    
    Write-Safe "✅ All file renames completed"
}

# STAGE 4: Validation
if ($ValidateAfter -or $ApplyUpdates) {
    Write-Info "`n=== VALIDATION AFTER UPDATES ==="
    
    $AllFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
        $_.FullName -notmatch "\\reviews\\" 
    }
    
    $BrokenLinksAfter = 0
    foreach ($file in $AllFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }
        
        $links = [regex]::Matches($content, '\[([^\]]*)\]\(([^)]+\.md[^)]*)\)')
        foreach ($link in $links) {
            $linkPath = $link.Groups[2].Value.Trim()
            
            if ($linkPath -match '^https?://') { continue }
            
            try {
                $sourceDir = Split-Path $file.FullName -Parent
                $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $sourceDir $linkPath))
                
                if (-not (Test-Path $resolvedPath)) {
                    $BrokenLinksAfter++
                    Write-Preview "❌ Broken link: $linkPath in $($file.Name)"
                }
            }
            catch {
                $BrokenLinksAfter++
            }
        }
    }
    
    if ($BrokenLinksAfter -eq 0) {
        Write-Safe "🎉 VALIDATION SUCCESS: No broken links found!"
    } else {
        Write-Danger "❌ VALIDATION FAILED: $BrokenLinksAfter broken links found"
    }
}

Write-Info "`n=== PHASE 2 STAGE COMPLETE ==="
Write-Safety "📋 Available Commands:"
Write-Safety "  -AnalyzeLinks     : Scan all files and analyze current links"
Write-Safety "  -PreviewUpdates   : Safe preview of all planned updates"  
Write-Safety "  -ApplyUpdates     : Apply real changes (DANGEROUS!)"
Write-Safety "  -ValidateAfter    : Validate links after updates"