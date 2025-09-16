# GALACTIC IDLERS - INTELLIGENT LINK FIXER
# Использует знание о переименованиях для исправления битых ссылок

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

Write-Safety "=== GALACTIC IDLERS - INTELLIGENT LINK FIXER ==="
Write-Info "Using pattern knowledge to fix broken links after catalogization"

Write-Info "`n=== PHASE 1: BUILDING INTELLIGENT MAPPING ==="

# Build mapping of old patterns to new patterns
$patternMappings = @{
    # Level 2 patterns - key insight: files got renamed from XX-Y-Z to XX-XX-0Y format
    "00-3-1-" = "00-00-01-"
    "00-3-2-" = "00-00-02-"
    "00-3-3-" = "00-00-03-"
    "00-3-4-" = "00-00-04-"
    "01-1-1-" = "01-01-01-"
    "01-1-2-" = "01-01-02-"
    "01-1-3-" = "01-01-03-"
    "01-1-4-" = "01-01-04-"
    "01-2-1-" = "01-01-01-"
    "01-2-2-" = "01-01-02-"
    "01-2-3-" = "01-01-03-"
    "01-2-4-" = "01-01-04-"
    "01-3-1-" = "01-01-01-"
    "01-3-2-" = "01-01-02-"
    "01-3-3-" = "01-01-03-"
    "01-4-1-" = "01-01-01-"
    "01-4-2-" = "01-01-02-"
    "01-4-3-" = "01-01-03-"
    "01-4-4-" = "01-01-04-"
    "01-5-1-" = "01-01-01-"
    "01-5-2-" = "01-01-02-"
    "01-5-3-" = "01-01-03-"
    "01-5-4-" = "01-01-04-"
    "01-5-5-" = "01-01-05-"
    "02-1-1-" = "02-02-01-"
    "02-1-2-" = "02-02-02-"
    "02-1-3-" = "02-02-03-"
    "02-1-4-" = "02-02-04-"
    "02-1-5-" = "02-02-05-"
    "02-2-1-" = "02-02-01-"
    "02-2-2-" = "02-02-02-"
    "02-2-3-" = "02-02-03-"
    "02-2-4-" = "02-02-04-"
    "02-2-5-" = "02-02-05-"
    "02-3-1-" = "02-02-01-"
    "02-3-2-" = "02-02-02-"
    "02-3-3-" = "02-02-03-"
    "02-3-4-" = "02-02-04-"
    "02-3-5-" = "02-02-05-"
    "02-7-1-" = "02-02-01-"
    "02-7-2-" = "02-02-02-"
    "02-7-3-" = "02-02-03-"
    "03-1-1-" = "03-03-01-"
    "03-1-2-" = "03-03-02-"
    "03-1-3-" = "03-03-03-"
    "03-1-4-" = "03-03-04-"
    "03-2-1-" = "03-03-01-"
    "03-2-2-" = "03-03-02-"
    "03-2-3-" = "03-03-03-"
    "03-5-1-" = "03-03-01-"
    "03-5-2-" = "03-03-02-"
    "03-5-3-" = "03-03-03-"
    "03-5-4-" = "03-03-04-"
    "03-5-5-" = "03-03-05-"
    "04-1-1-" = "04-04-01-"
    "04-1-2-" = "04-04-02-"
    "04-1-3-" = "04-04-03-"
    "04-1-4-" = "04-04-04-"
    "04-1-5-" = "04-04-05-"
    "04-2-1-" = "04-04-01-"
    "04-2-2-" = "04-04-02-"
    "04-3-1-" = "04-04-01-"
    "04-3-2-" = "04-04-02-"
    "04-3-3-" = "04-04-03-"
    "04-4-0-" = "04-04-00-"
    "04-4-1-" = "04-04-01-"
    "04-4-2-" = "04-04-02-"
    "04-4-3-" = "04-04-03-"
    "04-5-1-" = "04-04-01-"
    "04-5-2-" = "04-04-02-"
    "04-5-3-" = "04-04-03-"
    "04-5-4-" = "04-04-04-"
    "04-5-5-" = "04-04-05-"
    "04-6-1-" = "04-04-01-"
    "04-6-2-" = "04-04-02-"
    "04-6-3-" = "04-04-03-"
    "04-6-4-" = "04-04-04-"
    "04-6-5-" = "04-04-05-"
    "04-6-6-" = "04-04-06-"
    "04-6-7-" = "04-04-07-"
    "04-6-8-" = "04-04-08-"
    "04-6-9-" = "04-04-09-"
    "04-6-10-" = "04-04-10-"
    "05-2-1-" = "05-05-01-"
    "05-2-2-" = "05-05-02-"
    "05-2-3-" = "05-05-03-"
    "05-2-4-" = "05-05-04-"
    "05-2-5-" = "05-05-05-"
    "05-2-6-" = "05-05-06-"
    "05-2-7-" = "05-05-07-"
    "05-2-8-" = "05-05-08-"
    "05-3-1-" = "05-05-01-"
    "05-3-2-" = "05-05-02-"
    "05-3-3-" = "05-05-03-"
    "05-3-4-" = "05-05-04-"
    "05-3-5-" = "05-05-05-"
    "05-3-6-" = "05-05-06-"
    "05-3-7-" = "05-05-07-"
    "05-3-8-" = "05-05-08-"
    "05-4-1-" = "05-05-01-"
    "05-4-2-" = "05-05-02-"
    "05-4-4-" = "05-05-04-"
    "05-5-1-" = "05-05-01-"
    "05-5-2-" = "05-05-02-"
    "05-5-3-" = "05-05-03-"
    "05-5-4-" = "05-05-04-"
    "05-5-5-" = "05-05-05-"
    "06-1-1-" = "06-06-01-"
    "06-1-2-" = "06-06-02-"
    "06-1-3-" = "06-06-03-"
    "06-1-4-" = "06-06-04-"
    "06-1-5-" = "06-06-05-"
    "06-2-0-" = "06-06-00-"
    "06-2-1-" = "06-06-01-"
    "06-2-2-" = "06-06-02-"
    "06-2-3-" = "06-06-03-"
    "06-2-4-" = "06-06-04-"
    "06-2-5-" = "06-06-05-"
    "06-3-1-" = "06-06-01-"
    "06-3-2-" = "06-06-02-"
    "06-3-3-" = "06-06-03-"
}

# Build index of actual existing files
$fileIndex = @{}
$allFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
    $_.FullName -notmatch "\\reviews\\" 
}

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
    $fileName = $file.Name
    $fileIndex[$fileName] = $relativePath
}

Write-Info "✅ Loaded $($patternMappings.Count) pattern mappings"
Write-Info "✅ Indexed $($fileIndex.Count) existing files"

Write-Info "`n=== PHASE 2: INTELLIGENT LINK ANALYSIS ==="

function Fix-LinkPath {
    param($linkPath)
    
    # Extract filename from path
    $fileName = Split-Path $linkPath -Leaf
    
    # Try pattern-based fixing
    foreach ($oldPattern in $patternMappings.Keys) {
        if ($fileName.StartsWith($oldPattern)) {
            $newPattern = $patternMappings[$oldPattern]
            $newFileName = $fileName -replace [regex]::Escape($oldPattern), $newPattern
            
            if ($fileIndex.ContainsKey($newFileName)) {
                return $fileIndex[$newFileName]
            }
        }
    }
    
    # Try direct filename lookup
    if ($fileIndex.ContainsKey($fileName)) {
        return $fileIndex[$fileName]
    }
    
    return $null
}

$linkFixes = @()
$unfixableLinks = @()

foreach ($file in $allFiles) {
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
            if ($linkPath -match "/$") { continue }  # Skip directory links
            
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
                    # Try intelligent fixing
                    $fixedTarget = Fix-LinkPath $linkPath
                    
                    if ($fixedTarget) {
                        # Calculate new relative link path
                        $sourceDir = Split-Path $relativePath
                        if ($sourceDir) {
                            $newLinkPath = [System.IO.Path]::GetRelativePath($sourceDir, $fixedTarget) -replace "\\", "/"
                            if (-not $newLinkPath.StartsWith("..")) {
                                $newLinkPath = "./" + $newLinkPath
                            }
                        } else {
                            $newLinkPath = "./" + ($fixedTarget -replace "\\", "/")
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
                            FixedTarget = $fixedTarget
                        }
                    } else {
                        $unfixableLinks += @{
                            SourceFile = $relativePath
                            LineNumber = $lineNumber
                            LinkPath = $linkPath
                            LinkText = $linkText
                        }
                    }
                }
            } catch {
                # Skip problematic links
            }
        }
    }
}

Write-Info "`n=== PHASE 3: INTELLIGENT ANALYSIS RESULTS ==="
Write-Safe "✅ Intelligent link fixes available: $($linkFixes.Count)"
Write-Danger "❌ Links that can't be fixed intelligently: $($unfixableLinks.Count)"

if ($Preview) {
    Write-Info "`n=== PREVIEW: FIRST 15 INTELLIGENT FIXES ==="
    
    $linkFixes | Select-Object -First 15 | ForEach-Object {
        Write-Host "`n📄 SOURCE: $($_.SourceRelativePath):$($_.LineNumber)" -ForegroundColor White
        Write-Host "🎯 TARGET: $($_.FixedTarget)" -ForegroundColor Cyan
        Write-Host "❌ OLD LINK: $($_.FullOldMatch)" -ForegroundColor Red
        Write-Host "✅ NEW LINK: $($_.FullNewMatch)" -ForegroundColor Green
    }
    
    if ($linkFixes.Count -gt 15) {
        Write-Host "`n... and $($linkFixes.Count - 15) more intelligent fixes available" -ForegroundColor Yellow
    }
}

if ($Execute) {
    Write-Danger "`n=== PHASE 4: EXECUTING INTELLIGENT LINK FIXES ==="
    
    $confirm = Read-Host "Type 'APPLY INTELLIGENT FIXES' to confirm"
    
    if ($confirm -eq "APPLY INTELLIGENT FIXES") {
        Write-Info "`n--- APPLYING INTELLIGENT FIXES ---"
        
        # Group fixes by source file
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
        
        Write-Safe "`n🎉 INTELLIGENT LINK FIXING COMPLETE!"
        Write-Host "✅ Applied $totalFixesApplied intelligent fixes across $filesUpdated files" -ForegroundColor Green
        
    } else {
        Write-Info "❌ Operation cancelled"
    }
}

Write-Info "`n=== INTELLIGENT LINK FIXER SUMMARY ==="
Write-Host "Intelligent fixes available: $($linkFixes.Count)" -ForegroundColor Green
Write-Host "Unfixable links remaining: $($unfixableLinks.Count)" -ForegroundColor $(if ($unfixableLinks.Count -eq 0) { 'Green' } else { 'Yellow' })

$successRate = if (($linkFixes.Count + $unfixableLinks.Count) -gt 0) { 
    [math]::Round(($linkFixes.Count / ($linkFixes.Count + $unfixableLinks.Count)) * 100, 2) 
} else { 
    100 
}

Write-Host "Intelligent Fix Success Rate: $successRate%" -ForegroundColor $(if ($successRate -ge 95) { 'Green' } elseif ($successRate -ge 80) { 'Yellow' } else { 'Red' })

Write-Info "`n=== INTELLIGENT LINK FIXER COMPLETE ==="