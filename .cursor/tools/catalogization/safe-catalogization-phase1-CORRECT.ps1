# GALACTIC IDLERS - SAFE CATALOGIZATION REPAIR - PHASE 1 (CORRECT LOGIC)
# ONLY add hierarchy prefixes, do NOT change existing file numbers!

param(
    [switch]$GenerateMapping = $false,
    [switch]$ValidateMapping = $false,
    [switch]$Preview = $true
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path
$MappingFile = "rename-mapping-fixed.json"

# Safety Colors
function Write-Safety { param($msg) Write-Host $msg -ForegroundColor Magenta }
function Write-Danger { param($msg) Write-Host $msg -ForegroundColor Red -BackgroundColor Yellow }
function Write-Safe { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }

Write-Safety "=== GALACTIC IDLERS - SAFE CATALOGIZATION PHASE 1 (CORRECT LOGIC) ==="
Write-Info "RULE: Only add hierarchy prefixes, keep existing file numbers unchanged!"

# Generate comprehensive mapping with CORRECT logic
if ($GenerateMapping -or $Preview) {
    Write-Info "`n=== PHASE 1A: FILE ANALYSIS WITH CORRECTED LOGIC ==="
    
    $AllFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
        $_.FullName -notmatch "\\reviews\\" 
    }
    
    Write-Info "Analyzing $($AllFiles.Count) files..."
    
    $RenameOperations = @()
    $CorrectlyNamedCount = 0
    
    foreach ($file in $AllFiles) {
        # Calculate relative path
        $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
        $pathParts = $relativePath -split "\\"
        $fileName = $pathParts[-1]
        
        # CORRECTED LOGIC: Root files are NEVER renamed!
        if ($pathParts.Count -eq 1) {
            # Root file - keep as-is, no changes!
            $CorrectlyNamedCount++
            continue
        }
        
        # For subdirectory files: calculate hierarchy prefix
        $directoryParts = $pathParts[0..($pathParts.Count-2)]
        
        # Extract hierarchy numbers - FINAL WORKING LOGIC
        $directoryNumbers = @()
        
        # Build full path for number extraction
        $fullPathForExtraction = $relativePath -replace "\.md$", ""
        
        # Use regex to find the SPECIFIC pattern we need
        # Pattern: 00-Project-Structure\00-3-IdleGame-Specific-Structure\00-3-1-Architectural
        # We want: 00, 3, 1 (not 00, 00, 00)
        
        # Split by backslash and extract meaningful numbers from each level
        $pathLevels = $fullPathForExtraction -split "\\"
        
        foreach ($level in $pathLevels) {
            # For directories like "00-3-IdleGame-Specific-Structure"
            # We want the LAST number if multiple exist, otherwise the first
            $numbers = [regex]::Matches($level, '\d+')
            if ($numbers.Count -eq 1) {
                # Single number: use it (e.g., "00-Project-Structure" -> 00)
                $directoryNumbers += ([int]$numbers[0].Value).ToString("00")
            } elseif ($numbers.Count -gt 1) {
                # Multiple numbers: use the LAST one (e.g., "00-3-1-Architectural" -> 1)
                $directoryNumbers += ([int]$numbers[$numbers.Count - 1].Value).ToString("00")
            }
        }
        
        # Calculate expected hierarchy prefix
        $hierarchyPrefix = ""
        if ($directoryNumbers.Count -gt 0) {
            $hierarchyPrefix = $directoryNumbers -join "-"
        }
        
        # Extract current file info
        $currentPrefix = ""
        $fileNumber = ""
        $descriptivePart = $fileName
        
        if ($fileName -match "^((\d+(?:-\d+)*)-)?(\d+)-(.+)$") {
            # File has: [prefix-]number-description
            $currentPrefix = $matches[2]  # Could be empty for simple "01-file.md"
            $fileNumber = ([int]$matches[3]).ToString("00")  # 02-digit padding!
            $descriptivePart = $matches[4]
        } elseif ($fileName -match "^(\d+)-(.+)$") {
            # Simple format: number-description
            $fileNumber = ([int]$matches[1]).ToString("00")  # 02-digit padding!
            $descriptivePart = $matches[2]
        } else {
            # No standard numbering - skip
            $CorrectlyNamedCount++
            continue
        }
        
        # Generate correct filename: hierarchy-prefix + description (file number already in hierarchy)
        $correctFileName = if ($hierarchyPrefix -eq "") {
            # No hierarchy needed, keep original format
            "$fileNumber-$descriptivePart"
        } else {
            # Hierarchy prefix already contains the file number, just add description
            "$hierarchyPrefix-$descriptivePart"
        }
        
        # Check if rename is needed
        $needsRename = ($fileName -ne $correctFileName)
        
        if ($needsRename) {
            # Calculate new path
            $dirPath = Join-Path $PlanRoot ($directoryParts -join "\")
            $newPath = Join-Path $dirPath $correctFileName
            
            $RenameOperations += @{
                OriginalPath = $file.FullName
                NewPath = $newPath
                OriginalName = $fileName
                NewName = $correctFileName
                HierarchyPrefix = $hierarchyPrefix
                FileNumber = $fileNumber
                DescriptivePart = $descriptivePart
                Depth = $directoryParts.Count
                Type = "Add Hierarchy Prefix"
            }
        } else {
            $CorrectlyNamedCount++
        }
    }
    
    Write-Safe "✅ Correctly named files: $CorrectlyNamedCount"
    Write-Danger "❌ Files needing hierarchy prefix: $($RenameOperations.Count)"
    
    # Step 2: Check for conflicts
    Write-Info "`n=== PHASE 1B: CONFLICT DETECTION ==="
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
        Write-Safe "✅ No target path conflicts detected"
    } else {
        Write-Danger "🚨 Found $conflicts target path conflicts!"
    }
    
    # Build final mapping
    Write-Info "`n=== PHASE 1C: FINAL RENAME MAPPING ==="
    
    $RenameMapping = @{
        Statistics = @{
            TotalFiles = $AllFiles.Count
            CorrectlyNamed = $CorrectlyNamedCount
            NeedingRename = $RenameOperations.Count
            ConflictCount = $conflicts
            RootFilesUntouched = ($AllFiles | Where-Object { ($_.FullName -split "\\").Count -eq ($PlanRoot -split "\\").Count + 1 }).Count
            GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        }
        RenameOperations = $RenameOperations
    }
    
    Write-Safe "✅ Hierarchy prefix operations prepared: $($RenameMapping.RenameOperations.Count)"
    Write-Info "📊 Root files kept unchanged: $($RenameMapping.Statistics.RootFilesUntouched)"
    
    # Save mapping
    if ($GenerateMapping) {
        $mappingJson = $RenameMapping | ConvertTo-Json -Depth 10
        Set-Content $MappingFile -Value $mappingJson
        Write-Safe "✅ Mapping saved to: $MappingFile"
    }
    
    # Preview examples
    Write-Info "`n=== RENAME PREVIEW (Top 15 Examples) ===" 
    Write-Info "RULE: Only add hierarchy prefixes, keep file numbers unchanged"
    Write-Host ""
    
    $RenameMapping.RenameOperations[0..14] | ForEach-Object {
        $relativeCurrent = $_.OriginalPath -replace [regex]::Escape($PlanRoot + "\"), ""
        $relativeNew = $_.NewPath -replace [regex]::Escape($PlanRoot + "\"), ""
        Write-Host "❌ OLD: $relativeCurrent" -ForegroundColor Red
        Write-Host "✅ NEW: $relativeNew" -ForegroundColor Green
        Write-Host "   📋 Hierarchy: '$($_.HierarchyPrefix)', FileNum: '$($_.FileNumber)', Depth: $($_.Depth)" -ForegroundColor Yellow
        Write-Host ""
    }
    
    # Show statistics
    Write-Info "`n=== STATISTICS BY DEPTH ===" 
    $depthStats = $RenameMapping.RenameOperations | Group-Object Depth | Sort-Object Name
    $depthStats | ForEach-Object {
        Write-Host "Depth $($_.Name): $($_.Count) files need hierarchy prefix" -ForegroundColor Cyan
    }
}

# Validation
if ($ValidateMapping) {
    Write-Info "`n=== MAPPING VALIDATION ==="
    
    if (-not (Test-Path $MappingFile)) {
        Write-Danger "❌ Mapping file not found: $MappingFile"
        exit 1
    }
    
    $mapping = Get-Content $MappingFile | ConvertFrom-Json
    Write-Info "📊 Loaded mapping with $($mapping.RenameOperations.Count) operations"
    
    # Validate source files exist
    $missingFiles = 0
    $mapping.RenameOperations | ForEach-Object {
        if (-not (Test-Path $_.OriginalPath)) {
            Write-Danger "❌ Missing source file: $($_.OriginalPath)"
            $missingFiles++
        }
    }
    
    # Check uniqueness
    $targetPaths = @{}
    $duplicateTargets = 0
    $mapping.RenameOperations | ForEach-Object {
        if ($targetPaths.ContainsKey($_.NewPath)) {
            Write-Danger "❌ Duplicate target path: $($_.NewPath)"
            $duplicateTargets++
        }
        $targetPaths[$_.NewPath] = $true
    }
    
    if ($missingFiles -eq 0 -and $duplicateTargets -eq 0) {
        Write-Safe "✅ Mapping validation passed!"
    } else {
        Write-Danger "❌ Validation failed: $missingFiles missing, $duplicateTargets duplicates"
        exit 1
    }
}

Write-Info "`n=== PHASE 1 COMPLETE ==="
Write-Safety "📋 CORRECTED LOGIC:"
Write-Safety "  ✅ Root files (08-Production-QA.md) - UNCHANGED"  
Write-Safety "  ✅ Subdirs - Add hierarchy prefix only"
Write-Safety "  ✅ Keep existing file numbers intact"
Write-Safety "  📝 Example: file.md → 01-02-03-file.md (hierarchy + original)"