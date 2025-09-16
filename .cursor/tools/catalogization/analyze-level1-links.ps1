# GALACTIC IDLERS - LEVEL 1 LINK IMPACT ANALYSIS
# Показывает ВСЕ ссылки, которые сломаются после переименования Level 1

param(
    [switch]$DetailedAnalysis = $true
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path
$Level1MappingFile = "rename-mapping-level-1.json"

# Safety Colors
function Write-Safety { param($msg) Write-Host $msg -ForegroundColor Magenta }
function Write-Danger { param($msg) Write-Host $msg -ForegroundColor Red -BackgroundColor Yellow }
function Write-Safe { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }

Write-Safety "=== GALACTIC IDLERS - LEVEL 1 LINK IMPACT ANALYSIS ==="
Write-Info "Analyzing what links will break when Level 1 files are renamed"

if (-not (Test-Path $Level1MappingFile)) {
    Write-Danger "❌ Level 1 mapping file not found: $Level1MappingFile"
    Write-Info "Run: .\safe-catalogization-LEVEL-BY-LEVEL.ps1 -Level 1 -GenerateMapping first"
    exit 1
}

# Load Level 1 rename mapping
$level1Mapping = Get-Content $Level1MappingFile | ConvertFrom-Json
Write-Info "✅ Loaded $($level1Mapping.RenameOperations.Count) Level 1 rename operations"

# Build lookup table: old filename -> new filename for Level 1 files
$level1Renames = @{}
foreach ($rename in $level1Mapping.RenameOperations) {
    $relativePath = $rename.OriginalPath -replace ([regex]::Escape($PlanRoot) + "\\"), ""
    $newRelativePath = $rename.NewPath -replace ([regex]::Escape($PlanRoot) + "\\"), ""
    $level1Renames[$relativePath] = $newRelativePath
}

Write-Info "`n=== SCANNING ALL FILES FOR LINKS TO LEVEL 1 FILES ==="

# Find all markdown files
$allFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
    $_.FullName -notmatch "\\reviews\\" 
}

$linkUpdatesNeeded = @()
$filesByLevel = @{}

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
    $pathParts = $relativePath -split "\\"
    $fileLevel = $pathParts.Count - 1
    
    # Group files by level for statistics
    if (-not $filesByLevel.ContainsKey($fileLevel)) {
        $filesByLevel[$fileLevel] = @()
    }
    $filesByLevel[$fileLevel] += $file
    
    # Read file content
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    
    # Find all markdown links
    $links = [regex]::Matches($content, '\[([^\]]*)\]\(([^)]+)\)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    
    foreach ($link in $links) {
        $linkText = $link.Groups[1].Value
        $linkPath = $link.Groups[2].Value
        
        # Skip external links
        if ($linkPath -match "^https?://") { continue }
        if ($linkPath -match "^#") { continue }  # Skip anchors
        
        # Resolve relative path to absolute
        try {
            $fileDir = Split-Path $file.FullName
            $targetPath = ""
            
            if ($linkPath.StartsWith("./")) {
                $targetPath = Join-Path $fileDir ($linkPath.Substring(2))
            } elseif ($linkPath.StartsWith("../")) {
                $targetPath = Join-Path $fileDir $linkPath
            } else {
                $targetPath = Join-Path $fileDir $linkPath
            }
            
            # Normalize path and make relative to PlanRoot
            $targetPath = [System.IO.Path]::GetFullPath($targetPath)
            
            if ($targetPath.StartsWith($PlanRoot)) {
                $targetRelative = $targetPath.Substring($PlanRoot.Length + 1)
                
                # Check if this link points to a Level 1 file that will be renamed
                if ($level1Renames.ContainsKey($targetRelative)) {
                    $newTargetRelative = $level1Renames[$targetRelative]
                    
                    # Calculate new relative link path
                    $sourceDir = Split-Path $relativePath
                    if ($sourceDir) {
                        $newLinkPath = [System.IO.Path]::GetRelativePath($sourceDir, $newTargetRelative) -replace "\\", "/"
                        if (-not $newLinkPath.StartsWith("..")) {
                            $newLinkPath = "./" + $newLinkPath
                        }
                    } else {
                        $newLinkPath = "./" + ($newTargetRelative -replace "\\", "/")
                    }
                    
                    $linkUpdatesNeeded += @{
                        SourceFile = $relativePath
                        SourceLevel = $fileLevel
                        TargetFile = $targetRelative
                        NewTargetFile = $newTargetRelative
                        OldLink = $linkPath
                        NewLink = $newLinkPath
                        LinkText = $linkText
                        FullOldMatch = $link.Groups[0].Value
                        FullNewMatch = "[$linkText]($newLinkPath)"
                    }
                }
            }
        } catch {
            # Skip problematic links
        }
    }
}

Write-Info "`n=== ANALYSIS RESULTS ==="

Write-Safe "✅ Files by level:"
foreach ($level in ($filesByLevel.Keys | Sort-Object)) {
    Write-Host "   Level $level`: $($filesByLevel[$level].Count) files" -ForegroundColor Cyan
}

Write-Danger "`n❌ Links that will break after Level 1 rename: $($linkUpdatesNeeded.Count)"

if ($linkUpdatesNeeded.Count -gt 0) {
    Write-Info "`n=== AFFECTED FILES BY LEVEL ==="
    
    $affectedByLevel = $linkUpdatesNeeded | Group-Object SourceLevel | Sort-Object Name
    foreach ($levelGroup in $affectedByLevel) {
        $level = $levelGroup.Name
        $count = $levelGroup.Count
        Write-Host "   Level $level files with broken links: $count" -ForegroundColor Yellow
    }
    
    Write-Info "`n=== DETAILED LINK UPDATE PREVIEW (First 20 examples) ==="
    
    $examples = $linkUpdatesNeeded | Sort-Object SourceLevel, SourceFile | Select-Object -First 20
    
    foreach ($update in $examples) {
        Write-Host "`n📁 SOURCE: $($update.SourceFile) (Level $($update.SourceLevel))" -ForegroundColor White
        Write-Host "   🎯 TARGET: $($update.TargetFile) → $($update.NewTargetFile)" -ForegroundColor Cyan
        Write-Host "   ❌ OLD LINK: $($update.FullOldMatch)" -ForegroundColor Red
        Write-Host "   ✅ NEW LINK: $($update.FullNewMatch)" -ForegroundColor Green
    }
    
    if ($linkUpdatesNeeded.Count -gt 20) {
        Write-Host "`n... and $($linkUpdatesNeeded.Count - 20) more link updates needed" -ForegroundColor Yellow
    }
    
    Write-Info "`n=== IMPACT BY TARGET LEVEL 1 FILE ==="
    
    $byTarget = $linkUpdatesNeeded | Group-Object TargetFile | Sort-Object Name
    Write-Host "Top 10 most referenced Level 1 files:" -ForegroundColor Cyan
    
    $byTarget | Sort-Object Count -Descending | Select-Object -First 10 | ForEach-Object {
        Write-Host "   📄 $($_.Name): $($_.Count) incoming links" -ForegroundColor Yellow
    }
    
    Write-Info "`n=== SAFETY VALIDATION ==="
    
    # Check if all new paths are unique
    $newPaths = $linkUpdatesNeeded | ForEach-Object { $_.NewLink } | Sort-Object -Unique
    if ($newPaths.Count -eq $linkUpdatesNeeded.Count) {
        Write-Safe "✅ All new link paths are unique - no conflicts"
    } else {
        Write-Danger "❌ Some new link paths may conflict!"
    }
    
    # Check if source files will also be renamed
    $sourcesAlsoRenamed = $linkUpdatesNeeded | Where-Object { $level1Renames.ContainsKey($_.SourceFile) }
    if ($sourcesAlsoRenamed.Count -gt 0) {
        Write-Danger "⚠️  WARNING: $($sourcesAlsoRenamed.Count) source files will ALSO be renamed!"
        Write-Info "   This means both source and target are changing - extra complexity!"
        
        Write-Host "`nDouble-rename examples:" -ForegroundColor Yellow
        $sourcesAlsoRenamed | Select-Object -First 5 | ForEach-Object {
            Write-Host "   📄 $($_.SourceFile) → $($level1Renames[$_.SourceFile])" -ForegroundColor Orange
            Write-Host "      Links to: $($_.TargetFile) → $($_.NewTargetFile)" -ForegroundColor Orange
        }
    } else {
        Write-Safe "✅ No source files are being renamed - simpler updates"
    }
}

Write-Info "`n=== SUMMARY ==="
Write-Host "Level 1 files to rename: $($level1Mapping.RenameOperations.Count)" -ForegroundColor Cyan
Write-Host "Links that will break: $($linkUpdatesNeeded.Count)" -ForegroundColor Yellow
Write-Host "Files affected by broken links: $(($linkUpdatesNeeded | Select-Object SourceFile -Unique).Count)" -ForegroundColor Yellow

if ($linkUpdatesNeeded.Count -eq 0) {
    Write-Safe "`n✅ NO LINKS WILL BREAK - Level 1 rename is safe!"
} else {
    Write-Danger "`n❌ LINKS WILL BREAK - Need link update strategy!"
    Write-Info "Options:"
    Write-Info "1. Rename Level 1 files + fix all broken links immediately"  
    Write-Info "2. Rename all levels first, then fix all links at the end"
    Write-Info "3. Create backup before any changes"
}

Write-Info "`n=== NEXT STEPS ==="
Write-Info "1. Review the link update preview above"
Write-Info "2. Decide on link update strategy"  
Write-Info "3. Consider creating git backup before proceeding"
Write-Info "4. Run Level 1 rename + link fixes together"