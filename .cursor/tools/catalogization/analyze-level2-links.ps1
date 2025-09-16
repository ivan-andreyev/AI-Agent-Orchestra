# GALACTIC IDLERS - LEVEL 2 LINK VALIDATION ANALYSIS
# Проверяет, что все ссылки на Level 2 файлы корректно обновились

param(
    [switch]$DetailedAnalysis = $true
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path

# Safety Colors
function Write-Safety { param($msg) Write-Host $msg -ForegroundColor Magenta }
function Write-Danger { param($msg) Write-Host $msg -ForegroundColor Red -BackgroundColor Yellow }
function Write-Safe { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }

Write-Safety "=== GALACTIC IDLERS - LEVEL 2 LINK VALIDATION ANALYSIS ==="
Write-Info "Checking that all links to Level 2 files are working after rename"

Write-Info "`n=== SCANNING ALL FILES FOR BROKEN LINKS ===="

# Find all markdown files
$allFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
    $_.FullName -notmatch "\\reviews\\" 
}

$brokenLinks = @()
$workingLinks = @()
$totalLinks = 0

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
    
    # Read file content
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    
    # Find all markdown links
    $links = [regex]::Matches($content, '\[([^\]]*)\]\(([^)]+)\)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    
    foreach ($link in $links) {
        $linkText = $link.Groups[1].Value
        $linkPath = $link.Groups[2].Value
        
        # Skip external links and anchors
        if ($linkPath -match "^https?://") { continue }
        if ($linkPath -match "^#") { continue }
        
        $totalLinks++
        
        # Resolve relative path to target
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
            
            # Normalize path
            $targetPath = [System.IO.Path]::GetFullPath($targetPath)
            
            # Check if target exists
            if (Test-Path $targetPath) {
                $workingLinks += @{
                    SourceFile = $relativePath
                    LinkText = $linkText
                    LinkPath = $linkPath
                    TargetExists = $true
                }
            } else {
                $brokenLinks += @{
                    SourceFile = $relativePath
                    LinkText = $linkText
                    LinkPath = $linkPath
                    ResolvedTarget = $targetPath.Substring($PlanRoot.Length + 1)
                    TargetExists = $false
                }
            }
        } catch {
            # Count as broken link
            $brokenLinks += @{
                SourceFile = $relativePath
                LinkText = $linkText
                LinkPath = $linkPath
                ResolvedTarget = "RESOLUTION_ERROR"
                TargetExists = $false
            }
        }
    }
}

Write-Info "`n=== LINK VALIDATION RESULTS ==="

Write-Info "Total markdown links found: $totalLinks"
Write-Safe "✅ Working links: $($workingLinks.Count)"
Write-Danger "❌ Broken links: $($brokenLinks.Count)"

if ($brokenLinks.Count -gt 0) {
    Write-Danger "`n=== BROKEN LINKS DETAILS ==="
    
    # Group broken links by source file
    $brokenBySource = $brokenLinks | Group-Object SourceFile | Sort-Object Name
    
    foreach ($sourceGroup in $brokenBySource) {
        $sourceFile = $sourceGroup.Name
        $sourceBrokenLinks = $sourceGroup.Group
        
        Write-Host "`n📄 SOURCE: $sourceFile ($($sourceBrokenLinks.Count) broken links)" -ForegroundColor Red
        
        foreach ($brokenLink in $sourceBrokenLinks) {
            Write-Host "   ❌ [$($brokenLink.LinkText)]($($brokenLink.LinkPath))" -ForegroundColor Red
            Write-Host "      Target: $($brokenLink.ResolvedTarget)" -ForegroundColor Yellow
        }
    }
    
    Write-Info "`n=== POSSIBLE CAUSES ==="
    Write-Info "1. Link path didn't get updated during Level 2 rename"
    Write-Info "2. Target file has non-standard naming (skipped during rename)"
    Write-Info "3. Link pointing to file that was moved/deleted"
    Write-Info "4. Relative path calculation error"
    
    # Analyze patterns in broken links
    Write-Info "`n=== PATTERN ANALYSIS ==="
    $level2Links = $brokenLinks | Where-Object { $_.ResolvedTarget -match "\\.*\\.*\\" }
    if ($level2Links.Count -gt 0) {
        Write-Host "Broken links to Level 2+ files: $($level2Links.Count)" -ForegroundColor Red
        $level2Links | Select-Object -First 5 | ForEach-Object {
            Write-Host "   • $($_.SourceFile) → $($_.ResolvedTarget)" -ForegroundColor Yellow
        }
    }
    
} else {
    Write-Safe "`n🎉 ALL LINKS ARE WORKING CORRECTLY!"
    Write-Safe "✅ Level 2 rename + link fix operation was 100% successful"
    Write-Safe "✅ No broken links found in the entire documentation"
}

Write-Info "`n=== LINK HEALTH SUMMARY ==="
$healthPercentage = if ($totalLinks -gt 0) { [math]::Round(($workingLinks.Count / $totalLinks) * 100, 2) } else { 100 }
Write-Host "Link Health: $healthPercentage% ($($workingLinks.Count)/$totalLinks working)" -ForegroundColor $(if ($healthPercentage -eq 100) { 'Green' } else { 'Yellow' })

if ($healthPercentage -eq 100) {
    Write-Safe "`n✅ PERFECT LINK HEALTH - Ready for Level 3 processing!"
} elseif ($healthPercentage -ge 95) {
    Write-Info "`n⚠️ Good link health, but some issues need attention"
} else {
    Write-Danger "`n❌ Poor link health - investigate before proceeding"
}

Write-Info "`n=== VALIDATION COMPLETE ==="