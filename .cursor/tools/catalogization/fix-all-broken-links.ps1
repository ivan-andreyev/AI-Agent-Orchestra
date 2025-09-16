# GALACTIC IDLERS - UNIVERSAL BROKEN LINKS FIXER
# Исправляет ВСЕ битые ссылки после переименований

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

Write-Safety "=== GALACTIC IDLERS - UNIVERSAL BROKEN LINKS FIXER ==="
Write-Info "Scanning and fixing ALL broken links in documentation"

Write-Info "`n=== PHASE 1: BUILDING FILE INDEX ==="

# Build index of all existing files for fast lookup
$fileIndex = @{}
$allFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
    $_.FullName -notmatch "\\reviews\\" 
}

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
    $fileName = $file.Name
    
    # Index by filename for quick lookup
    if (-not $fileIndex.ContainsKey($fileName)) {
        $fileIndex[$fileName] = @()
    }
    $fileIndex[$fileName] += $relativePath
}

Write-Info "✅ Indexed $($allFiles.Count) files"

Write-Info "`n=== PHASE 2: SCANNING BROKEN LINKS ==="

$brokenLinks = @()
$linkFixes = @()

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
    
    # Read file content
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    
    $lines = $content -split "`n"
    $lineNumber = 0
    
    foreach ($line in $lines) {
        $lineNumber++
        
        # Find all markdown links in this line
        $links = [regex]::Matches($line, '\[([^\]]*)\]\(([^)]+)\)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        
        foreach ($link in $links) {
            $linkText = $link.Groups[1].Value
            $linkPath = $link.Groups[2].Value
            
            # Skip external links and anchors
            if ($linkPath -match "^https?://") { continue }
            if ($linkPath -match "^#") { continue }
            
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
                if (-not (Test-Path $targetPath)) {
                    # Try to find the correct path
                    $targetFileName = Split-Path $targetPath -Leaf
                    
                    $correctPath = $null
                    if ($fileIndex.ContainsKey($targetFileName)) {
                        # Found potential matches
                        $matches = $fileIndex[$targetFileName]
                        
                        if ($matches.Count -eq 1) {
                            # Single match - use it
                            $correctPath = $matches[0]
                        } elseif ($matches.Count -gt 1) {
                            # Multiple matches - try to find the best one
                            # Prefer matches that have similar directory structure
                            $sourceDir = Split-Path $relativePath
                            $bestMatch = $matches | Sort-Object {
                                $matchDir = Split-Path $_ -Parent
                                if ($matchDir -eq $sourceDir) { return 0 }  # Same directory
                                if ($matchDir.StartsWith($sourceDir)) { return 1 }  # Subdirectory
                                if ($sourceDir.StartsWith($matchDir)) { return 2 }  # Parent directory
                                return 3  # Different branch
                            } | Select-Object -First 1
                            $correctPath = $bestMatch
                        }
                    }
                    
                    if ($correctPath) {
                        # Calculate new relative link path
                        $sourceDir = Split-Path $relativePath
                        if ($sourceDir) {
                            $newLinkPath = [System.IO.Path]::GetRelativePath($sourceDir, $correctPath) -replace "\\", "/"
                            if (-not $newLinkPath.StartsWith("..")) {
                                $newLinkPath = "./" + $newLinkPath
                            }
                        } else {
                            $newLinkPath = "./" + ($correctPath -replace "\\", "/")
                        }
                        
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
                            TargetFound = $correctPath
                        }
                    } else {
                        $brokenLinks += @{
                            SourceFile = $relativePath
                            LineNumber = $lineNumber
                            LinkText = $linkText
                            LinkPath = $linkPath
                            TargetFileName = $targetFileName
                        }
                    }
                }
            } catch {
                # Skip problematic links
            }
        }
    }
}

Write-Info "`n=== PHASE 3: ANALYSIS RESULTS ==="
Write-Safe "✅ Link fixes available: $($linkFixes.Count)"
Write-Danger "❌ Links that can't be fixed: $($brokenLinks.Count)"

if ($Preview) {
    Write-Info "`n=== PREVIEW: FIRST 20 LINK FIXES ==="
    
    $linkFixes | Select-Object -First 20 | ForEach-Object {
        Write-Host "`n📄 SOURCE: $($_.SourceRelativePath):$($_.LineNumber)" -ForegroundColor White
        Write-Host "🎯 TARGET: $($_.TargetFound)" -ForegroundColor Cyan
        Write-Host "❌ OLD LINK: $($_.FullOldMatch)" -ForegroundColor Red
        Write-Host "✅ NEW LINK: $($_.FullNewMatch)" -ForegroundColor Green
    }
    
    if ($linkFixes.Count -gt 20) {
        Write-Host "`n... and $($linkFixes.Count - 20) more fixes available" -ForegroundColor Yellow
    }
}

if ($Execute) {
    Write-Danger "`n=== PHASE 4: EXECUTING LINK FIXES (DANGEROUS!) ==="
    
    $confirm = Read-Host "Type 'FIX ALL BROKEN LINKS' to confirm mass link fixing"
    
    if ($confirm -eq "FIX ALL BROKEN LINKS") {
        Write-Info "`n--- FIXING LINKS ---"
        
        # Group fixes by source file for efficiency
        $fixesByFile = $linkFixes | Group-Object SourceFile
        $filesUpdated = 0
        $totalFixesApplied = 0
        
        foreach ($fileGroup in $fixesByFile) {
            $sourceFile = $fileGroup.Name
            $fileFixes = $fileGroup.Group
            
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
                    Set-Content $sourceFile -Value $content -Encoding UTF8
                    $filesUpdated++
                    Write-Safe "✅ Updated $($fileFixes.Count) links in: $($fix.SourceRelativePath)"
                }
            } catch {
                Write-Danger "❌ Failed to update links in $($fix.SourceRelativePath): $($_.Exception.Message)"
            }
        }
        
        Write-Safe "`n🎉 LINK FIXING COMPLETE!"
        Write-Host "✅ Applied $totalFixesApplied link fixes across $filesUpdated files" -ForegroundColor Green
        Write-Info "📋 Universal link fixing completed successfully!"
        
    } else {
        Write-Info "❌ Operation cancelled"
    }
}

Write-Info "`n=== LINK FIXING SUMMARY ==="
Write-Host "Fixable links: $($linkFixes.Count)" -ForegroundColor Green
Write-Host "Unfixable links: $($brokenLinks.Count)" -ForegroundColor Red

if ($brokenLinks.Count -gt 0) {
    Write-Info "`n=== UNFIXABLE LINKS (First 10) ==="
    $brokenLinks | Select-Object -First 10 | ForEach-Object {
        Write-Host "❌ $($_.SourceFile):$($_.LineNumber) → $($_.TargetFileName)" -ForegroundColor Red
    }
}

$healthPercentage = if (($linkFixes.Count + $brokenLinks.Count) -gt 0) { 
    [math]::Round(($linkFixes.Count / ($linkFixes.Count + $brokenLinks.Count)) * 100, 2) 
} else { 
    100 
}

Write-Host "`nLink Fix Success Rate: $healthPercentage%" -ForegroundColor $(if ($healthPercentage -ge 95) { 'Green' } elseif ($healthPercentage -ge 80) { 'Yellow' } else { 'Red' })

Write-Info "`n=== UNIVERSAL LINK FIXER COMPLETE ==="