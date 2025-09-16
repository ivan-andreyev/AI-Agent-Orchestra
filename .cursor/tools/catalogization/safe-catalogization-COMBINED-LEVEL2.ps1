# GALACTIC IDLERS - COMBINED LEVEL 2 RENAME + LINK FIX
# Переименовывает Level 2 файлы и исправляет все ссылки на них

param(
    [switch]$Preview = $true,
    [switch]$GenerateMapping = $false,
    [switch]$Execute = $false
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path
$MappingFile = "combined-level2-mapping.json"

# Safety Colors
function Write-Safety { param($msg) Write-Host $msg -ForegroundColor Magenta }
function Write-Danger { param($msg) Write-Host $msg -ForegroundColor Red -BackgroundColor Yellow }
function Write-Safe { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }

Write-Safety "=== GALACTIC IDLERS - COMBINED LEVEL 2 RENAME + LINK FIX ==="
Write-Info "This will rename Level 2 files AND fix all broken links"

# STAGE 1: GENERATE RENAME MAPPING
if ($Preview -or $GenerateMapping) {
    Write-Info "`n=== STAGE 1: GENERATING LEVEL 2 RENAME MAPPING ==="
    
    $AllFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
        $_.FullName -notmatch "\\reviews\\" 
    }
    
    # Filter Level 2 files only (files 2 directories deep)
    $Level2Files = @()
    foreach ($file in $AllFiles) {
        $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
        $pathParts = $relativePath -split "\\"
        $fileDepth = $pathParts.Count - 1
        
        if ($fileDepth -eq 2) {
            $Level2Files += $file
        }
    }
    
    Write-Info "Found $($Level2Files.Count) Level 2 files to process"
    
    # Process Level 2 files for renaming
    $RenameOperations = @()
    $CorrectlyNamedCount = 0
    
    foreach ($file in $Level2Files) {
        $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
        $pathParts = $relativePath -split "\\"
        $fileName = $pathParts[-1]
        $parentDirectory = $pathParts[1]  # Level 1 directory
        $grandparentDirectory = $pathParts[0]  # Level 0 directory
        
        # Extract grandparent number for first prefix (00-)
        $grandparentPrefix = "00"
        if ($grandparentDirectory -match "^(\d+)") {
            $grandparentPrefix = ([int]$matches[1]).ToString("00")
        }
        
        # Extract parent number for second prefix (-02-)
        $parentPrefix = "00"
        if ($parentDirectory -match "^(\d+)") {
            $parentPrefix = ([int]$matches[1]).ToString("00")
        }
        
        # Extract file info with corrected logic
        if ($fileName -match "^(\d+)(?:-(\d+))*-([^-]+(?:-.+)*)$") {
            # If there are multiple numbers, take the last one before description
            $allNumbers = [regex]::Matches($fileName, '^(\d+)(?:-(\d+))*')
            if ($allNumbers[0].Groups[2].Success) {
                # Has second number, use it as file number
                $fileNumber = ([int]$allNumbers[0].Groups[2].Value).ToString("00")
            } else {
                # Only one number, use it as file number  
                $fileNumber = ([int]$allNumbers[0].Groups[1].Value).ToString("00")
            }
            
            $description = $matches[3]
            $correctName = "$grandparentPrefix-$parentPrefix-$fileNumber-$description"
            
            if ($fileName -ne $correctName) {
                $dirPath = Join-Path $PlanRoot $grandparentDirectory
                $dirPath = Join-Path $dirPath $parentDirectory
                $newPath = Join-Path $dirPath $correctName
                
                $RenameOperations += @{
                    OriginalPath = $file.FullName
                    NewPath = $newPath
                    OriginalName = $fileName
                    NewName = $correctName
                    RelativeOldPath = $relativePath
                    RelativeNewPath = (Join-Path (Join-Path $grandparentDirectory $parentDirectory) $correctName)
                    GrandparentPrefix = $grandparentPrefix
                    ParentPrefix = $parentPrefix
                    FileNumber = $fileNumber
                    Type = "Level 2 Combined Rename"
                }
            } else {
                $CorrectlyNamedCount++
            }
        } else {
            Write-Info "⚠️ Skipping non-standard file: $fileName"
            $CorrectlyNamedCount++
        }
    }
    
    Write-Safe "✅ Level 2 files correctly named: $CorrectlyNamedCount"
    Write-Danger "❌ Level 2 files needing rename: $($RenameOperations.Count)"
}

# STAGE 2: ANALYZE LINK IMPACT
if ($Preview -or $GenerateMapping) {
    Write-Info "`n=== STAGE 2: ANALYZING LINK IMPACT ==="
    
    # Build lookup table: old relative path -> new relative path
    $renameLookup = @{}
    foreach ($rename in $RenameOperations) {
        $renameLookup[$rename.RelativeOldPath] = $rename.RelativeNewPath
    }
    
    # Find all files that link to Level 2 files
    $LinkUpdates = @()
    
    foreach ($file in $AllFiles) {
        $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
        
        # Read file content
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }
        
        $lineNumber = 0
        $lines = $content -split "`n"
        
        foreach ($line in $lines) {
            $lineNumber++
            
            # Find all markdown links in this line
            $links = [regex]::Matches($line, '\[([^\]]*)\]\(([^)]+)\)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            
            foreach ($link in $links) {
                $linkText = $link.Groups[1].Value
                $linkPath = $link.Groups[2].Value
                
                # Skip external links and anchors
                if ($linkPath -match "^https?://" -or $linkPath -match "^#") { continue }
                
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
                    
                    # Normalize and make relative to PlanRoot
                    $targetPath = [System.IO.Path]::GetFullPath($targetPath)
                    
                    if ($targetPath.StartsWith($PlanRoot)) {
                        $targetRelative = $targetPath.Substring($PlanRoot.Length + 1)
                        
                        # Check if this target will be renamed
                        if ($renameLookup.ContainsKey($targetRelative)) {
                            $newTargetRelative = $renameLookup[$targetRelative]
                            
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
                            
                            $LinkUpdates += @{
                                SourceFile = $file.FullName
                                SourceRelativePath = $relativePath
                                LineNumber = $lineNumber
                                OldLine = $line
                                TargetOldPath = $targetRelative
                                TargetNewPath = $newTargetRelative
                                OldLinkPath = $linkPath
                                NewLinkPath = $newLinkPath
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
    }
    
    Write-Safe "✅ Link updates needed: $($LinkUpdates.Count)"
}

# STAGE 3: BUILD COMBINED OPERATION PLAN
if ($Preview -or $GenerateMapping) {
    Write-Info "`n=== STAGE 3: BUILDING COMBINED OPERATION PLAN ==="
    
    $CombinedPlan = @{
        Statistics = @{
            Level2FilesToRename = $RenameOperations.Count
            Level2FilesCorrect = $CorrectlyNamedCount
            LinksToUpdate = $LinkUpdates.Count
            FilesWithLinksToUpdate = (($LinkUpdates | Select-Object SourceFile -Unique).Count)
            GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        }
        RenameOperations = $RenameOperations
        LinkUpdates = $LinkUpdates
    }
    
    if ($GenerateMapping) {
        $planJson = $CombinedPlan | ConvertTo-Json -Depth 10
        Set-Content $MappingFile -Value $planJson
        Write-Safe "✅ Combined plan saved to: $MappingFile"
    }
}

# STAGE 4: PREVIEW
if ($Preview) {
    Write-Info "`n=== STAGE 4: DETAILED PREVIEW ==="
    
    Write-Info "`n--- FILE RENAME PREVIEW (First 10) ---"
    $RenameOperations | Select-Object -First 10 | ForEach-Object {
        Write-Host "📁 Directory: $($_.GrandparentPrefix)-$($_.ParentPrefix)" -ForegroundColor Cyan
        Write-Host "❌ OLD FILE: $($_.OriginalName)" -ForegroundColor Red
        Write-Host "✅ NEW FILE: $($_.NewName)" -ForegroundColor Green
        Write-Host "   📋 GP: $($_.GrandparentPrefix), P: $($_.ParentPrefix), File: $($_.FileNumber)" -ForegroundColor Yellow
        Write-Host ""
    }
    
    if ($RenameOperations.Count -gt 10) {
        Write-Host "... and $($RenameOperations.Count - 10) more file renames" -ForegroundColor Yellow
    }
    
    Write-Info "`n--- LINK UPDATE PREVIEW (First 15) ---"
    $LinkUpdates | Select-Object -First 15 | ForEach-Object {
        Write-Host "📄 SOURCE: $($_.SourceRelativePath):$($_.LineNumber)" -ForegroundColor White
        Write-Host "🎯 TARGET: $($_.TargetOldPath) → $($_.TargetNewPath)" -ForegroundColor Cyan  
        Write-Host "❌ OLD LINK: $($_.FullOldMatch)" -ForegroundColor Red
        Write-Host "✅ NEW LINK: $($_.FullNewMatch)" -ForegroundColor Green
        Write-Host ""
    }
    
    if ($LinkUpdates.Count -gt 15) {
        Write-Host "... and $($LinkUpdates.Count - 15) more link updates" -ForegroundColor Yellow
    }
    
    Write-Info "`n--- IMPACT ANALYSIS ---"
    Write-Host "Files to rename: $($CombinedPlan.Statistics.Level2FilesToRename)" -ForegroundColor Yellow
    Write-Host "Links to update: $($CombinedPlan.Statistics.LinksToUpdate)" -ForegroundColor Yellow
    Write-Host "Files containing links to update: $($CombinedPlan.Statistics.FilesWithLinksToUpdate)" -ForegroundColor Yellow
    
    # Group by source file level for impact analysis
    $linksBySourceLevel = @{}
    foreach ($linkUpdate in $LinkUpdates) {
        $sourcePathParts = $linkUpdate.SourceRelativePath -split "\\"
        $sourceLevel = $sourcePathParts.Count - 1
        
        if (-not $linksBySourceLevel.ContainsKey($sourceLevel)) {
            $linksBySourceLevel[$sourceLevel] = 0
        }
        $linksBySourceLevel[$sourceLevel]++
    }
    
    Write-Host "`nLink updates by source file level:" -ForegroundColor Cyan
    foreach ($level in ($linksBySourceLevel.Keys | Sort-Object)) {
        Write-Host "   Level $level files: $($linksBySourceLevel[$level]) link updates" -ForegroundColor White
    }
}

# STAGE 5: EXECUTE
if ($Execute) {
    Write-Danger "`n=== STAGE 5: EXECUTING COMBINED OPERATION (DANGEROUS!) ==="
    
    if (-not (Test-Path $MappingFile)) {
        Write-Danger "❌ No mapping file found: $MappingFile"
        Write-Info "Run with -GenerateMapping first"
        exit 1
    }
    
    $plan = Get-Content $MappingFile | ConvertFrom-Json
    
    Write-Danger "About to execute COMBINED OPERATION:"
    Write-Host "• Rename $($plan.Statistics.Level2FilesToRename) Level 2 files" -ForegroundColor Yellow
    Write-Host "• Update $($plan.Statistics.LinksToUpdate) links across $($plan.Statistics.FilesWithLinksToUpdate) files" -ForegroundColor Yellow
    Write-Host ""
    Write-Danger "THIS IS IRREVERSIBLE! Make sure you have git backup!"
    
    $confirm = Read-Host "Type 'EXECUTE COMBINED LEVEL 2' to confirm"
    
    if ($confirm -eq "EXECUTE COMBINED LEVEL 2") {
        Write-Info "`n--- STEP 1: UPDATING LINKS ---"
        $linkUpdateCount = 0
        $filesUpdated = @{}
        
        # Group link updates by source file for efficiency
        $updatesByFile = $plan.LinkUpdates | Group-Object SourceFile
        
        foreach ($fileGroup in $updatesByFile) {
            $sourceFile = $fileGroup.Name
            $fileUpdates = $fileGroup.Group
            
            try {
                # Read file content
                $content = Get-Content $sourceFile -Raw -Encoding UTF8
                $originalContent = $content
                
                # Apply all link updates for this file
                foreach ($update in $fileUpdates) {
                    $content = $content -replace [regex]::Escape($update.FullOldMatch), $update.FullNewMatch
                    $linkUpdateCount++
                }
                
                # Write updated content back
                if ($content -ne $originalContent) {
                    Set-Content $sourceFile -Value $content -Encoding UTF8
                    $filesUpdated[$sourceFile] = $fileUpdates.Count
                    Write-Safe "✅ Updated $($fileUpdates.Count) links in: $($update.SourceRelativePath)"
                }
            } catch {
                Write-Danger "❌ Failed to update links in $($update.SourceRelativePath): $($_.Exception.Message)"
            }
        }
        
        Write-Info "`n--- STEP 2: RENAMING FILES ---"
        $renameCount = 0
        foreach ($rename in $plan.RenameOperations) {
            try {
                Move-Item -Path $rename.OriginalPath -Destination $rename.NewPath
                Write-Safe "✅ Renamed: $($rename.OriginalName) → $($rename.NewName)"
                $renameCount++
            } catch {
                Write-Danger "❌ Failed to rename $($rename.OriginalName): $($_.Exception.Message)"
            }
        }
        
        Write-Safe "`n🎉 COMBINED OPERATION COMPLETE!"
        Write-Host "✅ Updated $linkUpdateCount links in $($filesUpdated.Count) files" -ForegroundColor Green
        Write-Host "✅ Renamed $renameCount Level 2 files" -ForegroundColor Green
        Write-Info "📋 Level 2 catalogization with link fixes complete!"
        
    } else {
        Write-Info "❌ Operation cancelled"
    }
}

Write-Info "`n=== COMBINED LEVEL 2 PROCESSING COMPLETE ==="
if ($Preview) {
    Write-Safety "📋 Next Steps:"
    Write-Safety "  1. Review the preview above carefully"
    Write-Safety "  2. Run with -GenerateMapping to save the plan"
    Write-Safety "  3. Create git backup: git add . && git commit -m 'Before Level 2 combined operation'"
    Write-Safety "  4. Run with -Execute to perform the combined operation"
    Write-Safety "  5. Verify results before proceeding to Level 3"
}