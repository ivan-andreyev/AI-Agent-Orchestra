# DEBUG VERSION - QUICK LINK FIXER TEST
$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path

# Find first few link fixes
$allFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Select-Object -First 3

$linkFixes = @()

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    
    $links = [regex]::Matches($content, '\[([^\]]*)\]\(([^)]+)\)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    
    foreach ($link in $links) {
        $linkPath = $link.Groups[2].Value
        if ($linkPath.Contains("00-3-1-")) {
            $newLinkPath = $linkPath -replace "00-3-1-", "00-00-01-"
            $linkFixes += @{
                SourceFile = $file.FullName
                FullOldMatch = $link.Groups[0].Value
                FullNewMatch = $link.Groups[0].Value -replace [regex]::Escape($linkPath), $newLinkPath
            }
            break  # Just one fix per file for testing
        }
    }
}

Write-Host "Found $($linkFixes.Count) test fixes"

# Test grouping
$fixesByFile = $linkFixes | Group-Object SourceFile
Write-Host "Groups: $($fixesByFile.Count)"

foreach ($fileGroup in $fixesByFile) {
    $sourceFile = $fileGroup.Name
    Write-Host "Group: '$sourceFile' (Length: $($sourceFile.Length))"
    
    if ($sourceFile -and $sourceFile -ne "") {
        Write-Host "✅ Would process file: $sourceFile"
        
        # Test actual file operation
        try {
            $content = Get-Content $sourceFile -Raw -Encoding UTF8
            $originalContent = $content
            
            foreach ($fix in $fileGroup.Group) {
                $content = $content -replace [regex]::Escape($fix.FullOldMatch), $fix.FullNewMatch
            }
            
            if ($content -ne $originalContent) {
                Set-Content -Path $sourceFile -Value $content -Encoding UTF8 -NoNewline
                Write-Host "✅ Successfully updated file"
            } else {
                Write-Host "❌ No changes made"
            }
        } catch {
            Write-Host "❌ Error: $($_.Exception.Message)"
        }
    } else {
        Write-Host "❌ Empty source file path"
    }
}