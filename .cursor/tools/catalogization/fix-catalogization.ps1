# Galactic Idlers Plan - Catalogization Naming Fix Script
# Phase 1: Analysis and Preview ("before" and "after")
# Phase 2: Safe Application with Link Correction

param(
    [switch]$Preview = $true,
    [switch]$Apply = $false,
    [switch]$ValidateOnly = $false
)

$PlanRoot = "Docs\PLAN\Galactic-Idlers-Plan"
$BackupSuffix = "_backup_" + (Get-Date -Format "yyyyMMdd_HHmmss")

# Color output functions
function Write-Success { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Warning { param($msg) Write-Host $msg -ForegroundColor Yellow }
function Write-Error { param($msg) Write-Host $msg -ForegroundColor Red }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }

Write-Info "=== GALACTIC IDLERS CATALOGIZATION FIX ==="
Write-Info "Mode: $(if($Apply) {'APPLY CHANGES'} else {'PREVIEW ONLY'})"

# Phase 1: Analyze current structure and build fix plan
function Get-FileDepthFromPath {
    param($FilePath)
    $relativePath = $FilePath -replace [regex]::Escape($PlanRoot), ""
    return ($relativePath -split "\\").Count - 2  # -2 for root and filename
}

function Get-ExpectedPrefix {
    param($FilePath)
    $depth = Get-FileDepthFromPath $FilePath
    $pathParts = ($FilePath -replace [regex]::Escape($PlanRoot), "") -split "\\" | Where-Object { $_ -ne "" }
    $pathParts = $pathParts[0..($pathParts.Count-2)]  # Remove filename
    
    $prefix = ""
    for ($i = 0; $i -lt $pathParts.Count; $i++) {
        if ($pathParts[$i] -match "^(\d+)") {
            if ($prefix -eq "") {
                $prefix = $matches[1]
            } else {
                $prefix += "-" + $matches[1]
            }
        }
    }
    return $prefix
}

function Get-CorrectFileName {
    param($FilePath)
    $fileName = Split-Path $FilePath -Leaf
    $expectedPrefix = Get-ExpectedPrefix $FilePath
    
    # Extract the descriptive part (after the numbering)
    if ($fileName -match "^\d+.*?-(.+)$") {
        $descriptivePart = $matches[1]
        return "$expectedPrefix-$descriptivePart"
    } elseif ($fileName -match "^(.+)\.md$") {
        # Handle files without proper numbering
        $baseName = $matches[1]
        if ($baseName -notmatch "^\d+") {
            return "$expectedPrefix-$baseName.md"
        }
    }
    
    return $fileName  # Return original if can't parse
}

# Find all problematic files
Write-Info "`n=== ANALYZING FILE STRUCTURE ==="
$AllMdFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
    $_.FullName -notmatch "\\reviews\\" 
}

$ProblematicFiles = @()
$LinkDatabase = @{}

foreach ($file in $AllMdFiles) {
    $currentName = $file.Name
    $expectedName = Get-CorrectFileName $file.FullName
    
    if ($currentName -ne $expectedName) {
        $ProblematicFiles += @{
            CurrentPath = $file.FullName
            CurrentName = $currentName
            ExpectedName = $expectedName
            Directory = $file.Directory.FullName
            NewPath = Join-Path $file.Directory.FullName $expectedName
        }
    }
    
    # Build link database by scanning file content
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($content) {
        $links = [regex]::Matches($content, '\[([^\]]*)\]\(([^)]+\.md[^)]*)\)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        foreach ($link in $links) {
            $linkText = $link.Groups[1].Value
            $linkPath = $link.Groups[2].Value
            
            if (-not $LinkDatabase.ContainsKey($file.FullName)) {
                $LinkDatabase[$file.FullName] = @()
            }
            
            $LinkDatabase[$file.FullName] += @{
                LinkText = $linkText
                LinkPath = $linkPath
                FullMatch = $link.Value
            }
        }
    }
}

# Display analysis results
Write-Info "`n=== ANALYSIS RESULTS ==="
Write-Warning "Found $($ProblematicFiles.Count) files with incorrect naming"
Write-Warning "Found $($LinkDatabase.Keys.Count) files with internal links"
Write-Warning "Total internal links: $(($LinkDatabase.Values | Measure-Object -Sum Count).Sum)"

if ($ProblematicFiles.Count -eq 0) {
    Write-Success "✅ All files have correct naming! No changes needed."
    exit 0
}

Write-Info "`n=== TOP 10 NAMING VIOLATIONS ==="
$ProblematicFiles[0..9] | ForEach-Object {
    $relativeCurrent = $_.CurrentPath -replace [regex]::Escape($PlanRoot), ""
    Write-Host "❌ $relativeCurrent" -ForegroundColor Red
    Write-Host "✅ $($_.Directory -replace [regex]::Escape($PlanRoot), '')\$($_.ExpectedName)" -ForegroundColor Green
    Write-Host ""
}

# Phase 2: Preview link corrections
Write-Info "`n=== LINK CORRECTION PREVIEW ==="
$LinkCorrections = @()

foreach ($problematicFile in $ProblematicFiles) {
    $oldRelativePath = $problematicFile.CurrentPath -replace [regex]::Escape($PlanRoot + "\"), ""
    $newRelativePath = ($problematicFile.NewPath -replace [regex]::Escape($PlanRoot + "\"), "")
    
    # Find all files that reference this file
    foreach ($fileWithLinks in $LinkDatabase.Keys) {
        $links = $LinkDatabase[$fileWithLinks]
        foreach ($link in $links) {
            $linkPath = $link.LinkPath
            
            # Check if this link points to our problematic file
            $resolvedPath = ""
            if ($linkPath.StartsWith("./") -or $linkPath.StartsWith("../") -or -not $linkPath.StartsWith("/")) {
                # Relative path - resolve it
                $sourceDir = Split-Path $fileWithLinks -Parent
                $resolvedPath = Join-Path $sourceDir $linkPath | Resolve-Path -ErrorAction SilentlyContinue
                
                if ($resolvedPath -and ($resolvedPath.Path -eq $problematicFile.CurrentPath)) {
                    # Calculate new relative path
                    $newRelativeLink = [System.IO.Path]::GetRelativePath($sourceDir, $problematicFile.NewPath) -replace "\\", "/"
                    
                    $LinkCorrections += @{
                        SourceFile = $fileWithLinks
                        OldLink = $link.FullMatch
                        NewLink = $link.FullMatch -replace [regex]::Escape($link.LinkPath), $newRelativeLink
                        OldPath = $link.LinkPath
                        NewPath = $newRelativeLink
                    }
                }
            }
        }
    }
}

Write-Info "Found $($LinkCorrections.Count) links that need correction"

if ($LinkCorrections.Count -gt 0) {
    Write-Info "`n=== TOP 5 LINK CORRECTIONS PREVIEW ==="
    $LinkCorrections[0..4] | ForEach-Object {
        $sourceFile = $_.SourceFile -replace [regex]::Escape($PlanRoot), ""
        Write-Host "📁 $sourceFile" -ForegroundColor Cyan
        Write-Host "❌ $($_.OldLink)" -ForegroundColor Red  
        Write-Host "✅ $($_.NewLink)" -ForegroundColor Green
        Write-Host ""
    }
}

# Phase 3: Apply changes (if requested)
if ($Apply) {
    Write-Warning "`n=== APPLYING CHANGES ==="
    Write-Warning "Creating backup branch in git..."
    
    # Create git backup branch
    $branchName = "catalogization-fix-$((Get-Date -Format 'yyyyMMdd-HHmmss'))"
    git checkout -b $branchName
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create git backup branch. Aborting."
        exit 1
    }
    
    Write-Success "✅ Created backup branch: $branchName"
    
    # Step 1: Apply link corrections
    Write-Info "`nStep 1/2: Updating $($LinkCorrections.Count) internal links..."
    foreach ($correction in $LinkCorrections) {
        $content = Get-Content $correction.SourceFile -Raw
        $newContent = $content -replace [regex]::Escape($correction.OldLink), $correction.NewLink
        Set-Content $correction.SourceFile -Value $newContent -NoNewline
    }
    Write-Success "✅ Updated all internal links"
    
    # Step 2: Rename files  
    Write-Info "`nStep 2/2: Renaming $($ProblematicFiles.Count) files..."
    foreach ($file in $ProblematicFiles) {
        Move-Item $file.CurrentPath $file.NewPath
        Write-Success "✅ Renamed: $($file.CurrentName) → $($file.ExpectedName)"
    }
    
    # Step 3: Validate
    Write-Info "`n=== VALIDATION ==="
    $brokenLinks = 0
    foreach ($file in (Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md")) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if ($content) {
            $links = [regex]::Matches($content, '\[([^\]]*)\]\(([^)]+\.md[^)]*)\)')
            foreach ($link in $links) {
                $linkPath = $link.Groups[2].Value
                if ($linkPath.StartsWith("./") -or $linkPath.StartsWith("../")) {
                    $resolvedPath = Join-Path (Split-Path $file.FullName -Parent) $linkPath
                    if (-not (Test-Path $resolvedPath)) {
                        $brokenLinks++
                        Write-Error "❌ Broken link in $($file.Name): $linkPath"
                    }
                }
            }
        }
    }
    
    if ($brokenLinks -eq 0) {
        Write-Success "`n🎉 SUCCESS! All $($ProblematicFiles.Count) files renamed and $($LinkCorrections.Count) links updated without breaks!"
        Write-Info "Backup branch: $branchName"
    } else {
        Write-Error "`n❌ VALIDATION FAILED: $brokenLinks broken links found"
        Write-Warning "Rolling back to main branch..."
        git checkout main
        git branch -D $branchName
        exit 1
    }
    
} elseif ($ValidateOnly) {
    Write-Info "`n=== VALIDATION ONLY MODE ==="
    $brokenLinks = 0
    foreach ($file in (Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md")) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if ($content) {
            $links = [regex]::Matches($content, '\[([^\]]*)\]\(([^)]+\.md[^)]*)\)')
            foreach ($link in $links) {
                $linkPath = $link.Groups[2].Value
                if ($linkPath.StartsWith("./") -or $linkPath.StartsWith("../")) {
                    $resolvedPath = Join-Path (Split-Path $file.FullName -Parent) $linkPath
                    if (-not (Test-Path $resolvedPath)) {
                        $brokenLinks++
                        Write-Error "❌ Broken link in $($file.Name): $linkPath"
                    }
                }
            }
        }
    }
    Write-Info "Current broken links: $brokenLinks"
} else {
    Write-Info "`n=== PREVIEW MODE COMPLETE ==="
    Write-Info "To apply changes, run: .\fix-catalogization.ps1 -Apply"
    Write-Info "To validate current links: .\fix-catalogization.ps1 -ValidateOnly"
}

Write-Info "`n=== SUMMARY ==="
Write-Host "📊 Files to rename: $($ProblematicFiles.Count)" -ForegroundColor Yellow
Write-Host "🔗 Links to update: $($LinkCorrections.Count)" -ForegroundColor Yellow
Write-Host "📁 Total .md files: $($AllMdFiles.Count)" -ForegroundColor Cyan