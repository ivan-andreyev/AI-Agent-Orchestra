# GALACTIC IDLERS - SAFE CATALOGIZATION BY LEVELS
# Поэтапное переименование: сначала уровень 0, потом 1, потом 2, и т.д.

param(
    [int]$Level = 0,
    [switch]$GenerateMapping = $false,
    [switch]$Preview = $true,
    [switch]$ApplyRenames = $false
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path
$MappingFile = "rename-mapping-level-$Level.json"

# Safety Colors
function Write-Safety { param($msg) Write-Host $msg -ForegroundColor Magenta }
function Write-Danger { param($msg) Write-Host $msg -ForegroundColor Red -BackgroundColor Yellow }
function Write-Safe { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }

Write-Safety "=== GALACTIC IDLERS - LEVEL-BY-LEVEL CATALOGIZATION ==="
Write-Info "Processing Level $Level files only"

if ($GenerateMapping -or $Preview) {
    Write-Info "`n=== ANALYZING LEVEL $Level FILES ==="
    
    $AllFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
        $_.FullName -notmatch "\\reviews\\" 
    }
    
    # Filter files by level
    $LevelFiles = @()
    foreach ($file in $AllFiles) {
        $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
        $pathParts = $relativePath -split "\\"
        $fileDepth = $pathParts.Count - 1  # Subtract 1 for the filename itself
        
        if ($fileDepth -eq $Level) {
            $LevelFiles += $file
        }
    }
    
    Write-Info "Found $($LevelFiles.Count) files at Level $Level"
    
    if ($LevelFiles.Count -eq 0) {
        Write-Safe "✅ No files at Level $Level to process"
        exit 0
    }
    
    # Process files at this level
    $RenameOperations = @()
    $CorrectlyNamedCount = 0
    
    foreach ($file in $LevelFiles) {
        $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
        $pathParts = $relativePath -split "\\"
        $fileName = $pathParts[-1]
        
        Write-Info "Processing: $fileName (Level $Level)"
        
        # For Level 0 (root files): should already be correct (01-Models.md, 02-Framework.md, etc.)
        if ($Level -eq 0) {
            # Root files should already have correct numbering, just check
            if ($fileName -match "^(\d+)-(.+)$") {
                $number = [int]$matches[1]
                $description = $matches[2]
                $correctName = $number.ToString("00") + "-" + $description
                
                if ($fileName -ne $correctName) {
                    $newPath = Join-Path $PlanRoot $correctName
                    $RenameOperations += @{
                        OriginalPath = $file.FullName
                        NewPath = $newPath
                        OriginalName = $fileName
                        NewName = $correctName
                        Level = $Level
                        Type = "Root Level 02-Padding"
                    }
                } else {
                    $CorrectlyNamedCount++
                }
            } else {
                Write-Info "⚠️ Skipping non-standard root file: $fileName"
                $CorrectlyNamedCount++
            }
        }
        
        # For Level 1: files in first subdirectory (01-Models\file.md)
        elseif ($Level -eq 1) {
            $directoryName = $pathParts[0]  # Like "01-Models"
            
            # Extract directory number for prefix
            if ($directoryName -match "^(\d+)") {
                $dirNumber = ([int]$matches[1]).ToString("00")
                
                # Extract file info - need to find the LAST number before description
                # Pattern: 12-4-Framework-Production-Demo.md -> want "4" as file number
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
                    $correctName = "$dirNumber-$fileNumber-$description"
                    
                    if ($fileName -ne $correctName) {
                        $dirPath = Join-Path $PlanRoot $directoryName
                        $newPath = Join-Path $dirPath $correctName
                        
                        $RenameOperations += @{
                            OriginalPath = $file.FullName
                            NewPath = $newPath
                            OriginalName = $fileName
                            NewName = $correctName
                            Level = $Level
                            DirectoryPrefix = $dirNumber
                            FileNumber = $fileNumber
                            Type = "Level 1 Hierarchy"
                        }
                    } else {
                        $CorrectlyNamedCount++
                    }
                } else {
                    Write-Info "⚠️ Skipping non-standard file: $fileName"
                    $CorrectlyNamedCount++
                }
            } else {
                Write-Info "⚠️ Directory without standard numbering: $directoryName"
                $CorrectlyNamedCount++
            }
        }
        
        # For Level 2+: more complex hierarchy
        else {
            # Extract numbers from each directory level + file level
            $hierarchyNumbers = @()
            
            # Process each directory in the path
            for ($i = 0; $i -lt $pathParts.Count - 1; $i++) {
                $dirPart = $pathParts[$i]
                
                # Find all numbers in directory name and take the last one
                $numbers = [regex]::Matches($dirPart, '\d+')
                if ($numbers.Count -gt 0) {
                    $lastNumber = [int]$numbers[$numbers.Count - 1].Value
                    $hierarchyNumbers += $lastNumber.ToString("00")
                }
            }
            
            # Extract file number from filename (the last number before description)
            # Pattern: 00-3-1-Architectural-Principles-Structure.md
            # We want the LAST number from the numbers sequence, which is "1"
            $fileNumbers = [regex]::Matches($fileName, '\d+')
            if ($fileNumbers.Count -gt 0) {
                # Take the last number as the file sequence number
                $fileNumber = [int]$fileNumbers[$fileNumbers.Count - 1].Value
                $hierarchyNumbers += $fileNumber.ToString("00")
            }
            
            # Extract description (everything after the last number-dash pattern)
            $description = $fileName
            if ($fileName -match "^\d+(?:-\d+)*-(.+)$") {
                $description = $matches[1]
            }
            
            # Build correct name
            $hierarchyPrefix = $hierarchyNumbers -join "-"
            $correctName = "$hierarchyPrefix-$description"
            
            if ($fileName -ne $correctName) {
                $dirPath = Join-Path $PlanRoot (($pathParts[0..($pathParts.Count-2)]) -join "\")
                $newPath = Join-Path $dirPath $correctName
                
                $RenameOperations += @{
                    OriginalPath = $file.FullName
                    NewPath = $newPath
                    OriginalName = $fileName
                    NewName = $correctName
                    Level = $Level
                    HierarchyPrefix = $hierarchyPrefix
                    Type = "Level $Level Hierarchy"
                }
            } else {
                $CorrectlyNamedCount++
            }
        }
    }
    
    Write-Safe "✅ Correctly named at Level $Level`: $CorrectlyNamedCount"
    Write-Danger "❌ Need rename at Level $Level`: $($RenameOperations.Count)"
    
    # Check for conflicts
    $targetPaths = @{}
    $conflicts = 0
    foreach ($rename in $RenameOperations) {
        if ($targetPaths.ContainsKey($rename.NewPath)) {
            Write-Danger "🚨 Conflict: $($rename.NewPath)"
            $conflicts++
        }
        $targetPaths[$rename.NewPath] = $true
    }
    
    if ($conflicts -eq 0) {
        Write-Safe "✅ No conflicts at Level $Level"
    } else {
        Write-Danger "🚨 Found $conflicts conflicts at Level $Level!"
    }
    
    # Build mapping
    $LevelMapping = @{
        Statistics = @{
            Level = $Level
            TotalFilesAtLevel = $LevelFiles.Count
            CorrectlyNamed = $CorrectlyNamedCount
            NeedingRename = $RenameOperations.Count
            ConflictCount = $conflicts
            GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        }
        RenameOperations = $RenameOperations
    }
    
    # Save mapping
    if ($GenerateMapping) {
        $mappingJson = $LevelMapping | ConvertTo-Json -Depth 10
        Set-Content $MappingFile -Value $mappingJson
        Write-Safe "✅ Level $Level mapping saved to: $MappingFile"
    }
    
    # Preview operations
    if ($RenameOperations.Count -gt 0) {
        Write-Info "`n=== LEVEL $Level RENAME PREVIEW ==="
        $RenameOperations | ForEach-Object {
            Write-Host "❌ OLD: $($_.OriginalName)" -ForegroundColor Red
            Write-Host "✅ NEW: $($_.NewName)" -ForegroundColor Green
            if ($_.DirectoryPrefix) {
                Write-Host "   📋 Dir: $($_.DirectoryPrefix), File: $($_.FileNumber)" -ForegroundColor Yellow
            } elseif ($_.HierarchyPrefix) {
                Write-Host "   📋 Hierarchy: $($_.HierarchyPrefix)" -ForegroundColor Yellow
            }
            Write-Host ""
        }
    }
}

# Apply renames if requested
if ($ApplyRenames) {
    Write-Danger "`n=== APPLYING LEVEL $Level RENAMES (DANGEROUS!) ==="
    
    if (-not (Test-Path $MappingFile)) {
        Write-Danger "❌ No mapping file found: $MappingFile"
        Write-Info "Run with -GenerateMapping first"
        exit 1
    }
    
    $mapping = Get-Content $MappingFile | ConvertFrom-Json
    
    Write-Danger "About to rename $($mapping.RenameOperations.Count) files at Level $Level"
    $confirm = Read-Host "Type 'YES' to confirm"
    
    if ($confirm -eq "YES") {
        $renamed = 0
        foreach ($rename in $mapping.RenameOperations) {
            try {
                Move-Item -Path $rename.OriginalPath -Destination $rename.NewPath
                Write-Safe "✅ Renamed: $($rename.OriginalName) → $($rename.NewName)"
                $renamed++
            } catch {
                Write-Danger "❌ Failed to rename $($rename.OriginalName): $($_.Exception.Message)"
            }
        }
        Write-Safe "✅ Successfully renamed $renamed files at Level $Level"
    } else {
        Write-Info "Rename cancelled"
    }
}

Write-Info "`n=== LEVEL $Level PROCESSING COMPLETE ==="
Write-Safety "📋 Next Steps:"
Write-Safety "  1. Review preview above"
Write-Safety "  2. Run with -GenerateMapping to save mapping"
Write-Safety "  3. Run with -ApplyRenames to execute renames (DANGEROUS!)"
Write-Safety "  4. Then process Level $($Level + 1)"