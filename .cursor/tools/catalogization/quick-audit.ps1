# Quick Catalogization Audit for Galactic Idlers Plan

$PlanRoot = "Docs\PLAN\Galactic-Idlers-Plan"

Write-Host "=== QUICK CATALOGIZATION AUDIT ===" -ForegroundColor Cyan

# Find all MD files
$AllFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
    $_.FullName -notmatch "\\reviews\\" 
}

Write-Host "Total .md files found: $($AllFiles.Count)" -ForegroundColor Yellow

# Analyze naming patterns
$NamingProblems = @()
$CorrectNaming = @()

foreach ($file in $AllFiles) {
    $relativePath = $file.FullName -replace [regex]::Escape($PlanRoot + "\"), ""
    $pathParts = $relativePath -split "\\"
    $fileName = $pathParts[-1]
    $directories = $pathParts[0..($pathParts.Count-2)]
    
    # Calculate expected depth
    $depth = $directories.Count
    
    # Extract numbering from directories
    $expectedPrefix = @()
    foreach ($dir in $directories) {
        if ($dir -match "^(\d+)") {
            $expectedPrefix += $matches[1]
        }
    }
    
    $expectedPrefixString = $expectedPrefix -join "-"
    
    # Check if filename starts with expected prefix
    if ($expectedPrefixString -ne "" -and -not $fileName.StartsWith($expectedPrefixString + "-")) {
        $NamingProblems += [PSCustomObject]@{
            File = $relativePath
            CurrentPrefix = ($fileName -split "-")[0]
            ExpectedPrefix = $expectedPrefixString
            Depth = $depth
            Problem = "Prefix mismatch"
        }
    } elseif ($expectedPrefixString -eq "" -and $fileName -match "^(\d+)") {
        $NamingProblems += [PSCustomObject]@{
            File = $relativePath
            CurrentPrefix = $matches[1]
            ExpectedPrefix = ""
            Depth = $depth
            Problem = "Unexpected numbering at root"
        }
    } else {
        $CorrectNaming += $relativePath
    }
}

Write-Host "`n=== RESULTS ===" -ForegroundColor Green
Write-Host "✅ Correctly named files: $($CorrectNaming.Count)" -ForegroundColor Green
Write-Host "❌ Incorrectly named files: $($NamingProblems.Count)" -ForegroundColor Red

if ($NamingProblems.Count -gt 0) {
    Write-Host "`n=== TOP 20 NAMING PROBLEMS ===" -ForegroundColor Yellow
    $NamingProblems | Select-Object -First 20 | ForEach-Object {
        Write-Host "❌ $($_.File)" -ForegroundColor Red
        Write-Host "   Current: '$($_.CurrentPrefix)' → Expected: '$($_.ExpectedPrefix)' (Depth: $($_.Depth))" -ForegroundColor Gray
        Write-Host ""
    }
}

# Check for duplicate prefixes in same directory  
Write-Host "=== CHECKING FOR DUPLICATE PREFIXES ===" -ForegroundColor Cyan
$DuplicateProblems = @()

$AllFiles | Group-Object DirectoryName | ForEach-Object {
    $dirFiles = $_.Group
    $prefixes = @{}
    
    foreach ($file in $dirFiles) {
        if ($file.Name -match "^(\d+(?:-\d+)*)-") {
            $prefix = $matches[1]
            if ($prefixes.ContainsKey($prefix)) {
                $prefixes[$prefix] += $file.Name
            } else {
                $prefixes[$prefix] = @($file.Name)
            }
        }
    }
    
    foreach ($prefix in $prefixes.Keys) {
        if ($prefixes[$prefix].Count -gt 1) {
            $DuplicateProblems += [PSCustomObject]@{
                Directory = $_.Name -replace [regex]::Escape($PlanRoot + "\"), ""
                Prefix = $prefix
                Files = $prefixes[$prefix]
            }
        }
    }
}

if ($DuplicateProblems.Count -gt 0) {
    Write-Host "❌ Found $($DuplicateProblems.Count) directories with duplicate prefixes:" -ForegroundColor Red
    $DuplicateProblems | ForEach-Object {
        Write-Host "📁 $($_.Directory)" -ForegroundColor Yellow
        Write-Host "   Duplicate prefix '$($_.Prefix)':" -ForegroundColor Red
        $_.Files | ForEach-Object { Write-Host "     - $_" -ForegroundColor Gray }
        Write-Host ""
    }
} else {
    Write-Host "✅ No duplicate prefixes found" -ForegroundColor Green
}

# Quick link check
Write-Host "=== QUICK LINK ANALYSIS ===" -ForegroundColor Cyan
$TotalLinks = 0
$BrokenLinks = 0

foreach ($file in $AllFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($content) {
        $links = [regex]::Matches($content, '\[([^\]]*)\]\(([^)]+\.md[^)]*)\)')
        $TotalLinks += $links.Count
        
        foreach ($link in $links) {
            $linkPath = $link.Groups[2].Value
            if ($linkPath.StartsWith("./") -or $linkPath.StartsWith("../")) {
                $resolvedPath = Join-Path (Split-Path $file.FullName -Parent) $linkPath
                if (-not (Test-Path $resolvedPath -ErrorAction SilentlyContinue)) {
                    $BrokenLinks++
                }
            }
        }
    }
}

Write-Host "🔗 Total internal links: $TotalLinks" -ForegroundColor Yellow
Write-Host "❌ Broken links: $BrokenLinks" -ForegroundColor Red

Write-Host "`n=== SUMMARY ===" -ForegroundColor Magenta
Write-Host "📊 Total files analyzed: $($AllFiles.Count)"
Write-Host "✅ Correct naming: $($CorrectNaming.Count) ($([math]::Round(($CorrectNaming.Count / $AllFiles.Count) * 100, 1))%)"
Write-Host "❌ Naming problems: $($NamingProblems.Count) ($([math]::Round(($NamingProblems.Count / $AllFiles.Count) * 100, 1))%)"
Write-Host "🔗 Link integrity: $(if($BrokenLinks -eq 0) {'✅ Perfect'} else {"❌ $BrokenLinks broken"})"