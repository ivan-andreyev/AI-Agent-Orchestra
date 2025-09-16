# GALACTIC IDLERS - SAFE CATALOGIZATION REPAIR - PHASE 1 (WORKING VERSION)
# Production-Grade Rename Mapping with Proper 02-Digit Padding Logic

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

Write-Safety "=== GALACTIC IDLERS - SAFE CATALOGIZATION PHASE 1 (WORKING VERSION) ==="
Write-Info "Target Pattern: 01-01-01-filename.md (02-digit padding)"

# Generate comprehensive mapping with FIXED directory logic
if ($GenerateMapping -or $Preview) {
    Write-Info "`n=== PHASE 1A: COMPREHENSIVE FILE ANALYSIS WITH WORKING LOGIC ==="
    
    $AllFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
        $_.FullName -notmatch "\\reviews\\" 
    }
    
    Write-Info "Analyzing $($AllFiles.Count) files..."
    
    # Step 1: Create file analysis with proper directory grouping
    $FilesByDirectory = @{}
    
    foreach ($file in $AllFiles) {
        # Calculate relative path properly
        $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
        $pathParts = $relativePath -split "\\"
        $fileName = $pathParts[-1]
        $directoryParts = $pathParts[0..($pathParts.Count-2)]
        
        # Calculate directory key for grouping
        $directoryKey = if ($directoryParts.Count -gt 0) { 
            $directoryParts -join "\" 
        } else { 
            "ROOT" 
        }
        
        # Extract directory numbers with 02-digit padding
        $directoryNumbers = @()
        foreach ($dirPart in $directoryParts) {
            if ($dirPart -match "^(\d+)") {
                $number = [int]$matches[1]
                $directoryNumbers += $number.ToString("00")  # 02-digit padding
            }
        }
        
        # Calculate expected prefix based on directory structure
        $expectedPrefix = if ($directoryNumbers.Count -gt 0) {
            $directoryNumbers -join "-"
        } else {
            ""
        }
        
        # Extract descriptive part from filename
        $descriptivePart = $fileName
        if ($fileName -match "^(?:\d+(?:-\d+)*-)?(.+)$") {
            $descriptivePart = $matches[1]
        }
        
        # Store file info grouped by directory
        if (-not $FilesByDirectory.ContainsKey($directoryKey)) {
            $FilesByDirectory[$directoryKey] = @()
        }
        
        $FilesByDirectory[$directoryKey] += @{
            OriginalPath = $file.FullName
            RelativePath = $relativePath
            FileName = $fileName
            DirectoryKey = $directoryKey
            DirectoryParts = $directoryParts
            DirectoryNumbers = $directoryNumbers
            ExpectedPrefix = $expectedPrefix
            DescriptivePart = $descriptivePart
            Depth = $directoryParts.Count
        }
    }
    
    Write-Info "`n=== PHASE 1B: ASSIGNING SEQUENTIAL FILE POSITIONS BY DIRECTORY ==="
    
    $RenameOperations = @()
    $CorrectlyNamedCount = 0
    
    foreach ($directoryKey in $FilesByDirectory.Keys) {
        $filesInDir = $FilesByDirectory[$directoryKey]
        Write-Info "✅ Directory '$directoryKey' has $($filesInDir.Count) files"
        
        # Sort files alphabetically within directory for consistent numbering
        $sortedFiles = $filesInDir | Sort-Object DescriptivePart
        
        for ($i = 0; $i -lt $sortedFiles.Count; $i++) {
            $file = $sortedFiles[$i]
            $filePosition = ($i + 1).ToString("00")  # 02-digit padding: 01, 02, 03...
            
            # Generate final correct filename
            $correctFileName = if ($file.ExpectedPrefix -eq "") {
                # Root level files still need position prefix with 02-padding
                "$filePosition-$($file.DescriptivePart)"
            } else {
                "$($file.ExpectedPrefix)-$filePosition-$($file.DescriptivePart)"
            }
            
            # Calculate new path using directory key reconstruction
            if ($file.DirectoryParts.Count -gt 0) {
                $dirPath = Join-Path $PlanRoot ($file.DirectoryParts -join "\")
                $newPath = Join-Path $dirPath $correctFileName
            } else {
                $newPath = Join-Path $PlanRoot $correctFileName
            }
            
            # Check if rename is needed
            $needsRename = ($file.FileName -ne $correctFileName)
            
            if ($needsRename) {
                $RenameOperations += @{
                    OriginalPath = $file.OriginalPath
                    NewPath = $newPath
                    OriginalName = $file.FileName
                    NewName = $correctFileName
                    ExpectedPrefix = $file.ExpectedPrefix
                    FilePosition = $filePosition
                    DescriptivePart = $file.DescriptivePart
                    Depth = $file.Depth
                    Type = "Standard Rename with 02-Padding"
                }
            } else {
                $CorrectlyNamedCount++
            }
        }
    }
    
    Write-Safe "✅ Correctly named files: $CorrectlyNamedCount"
    Write-Danger "❌ Files needing rename: $($RenameOperations.Count)"
    
    # Step 3: Check for potential conflicts
    Write-Info "`n=== PHASE 1C: CONFLICT DETECTION ==="
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
    
    # Build final rename mapping
    Write-Info "`n=== PHASE 1D: FINAL RENAME MAPPING ==="
    
    $RenameMapping = @{
        Statistics = @{
            TotalFiles = $AllFiles.Count
            CorrectlyNamed = $CorrectlyNamedCount
            NeedingRename = $RenameOperations.Count
            ConflictCount = $conflicts
            PaddingDigits = 2
            GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        }
        RenameOperations = $RenameOperations
    }
    
    Write-Safe "✅ Rename operations prepared: $($RenameMapping.RenameOperations.Count)"
    
    # Save mapping for next phase
    if ($GenerateMapping) {
        $mappingJson = $RenameMapping | ConvertTo-Json -Depth 10
        Set-Content $MappingFile -Value $mappingJson
        Write-Safe "✅ Mapping saved to: $MappingFile"
    }
    
    # Preview sample operations
    Write-Info "`n=== RENAME PREVIEW (Top 15 Examples) ==="
    Write-Info "Pattern: directory-numbers + file-position + descriptive-name"
    Write-Info "Example: 01-Models/01-1-Core-Models/file.md → 01-01-01-file.md"
    Write-Host ""
    
    $RenameMapping.RenameOperations[0..14] | ForEach-Object {
        $relativeCurrent = $_.OriginalPath -replace [regex]::Escape($PlanRoot + "\"), ""
        $relativeNew = $_.NewPath -replace [regex]::Escape($PlanRoot + "\"), ""
        Write-Host "❌ $relativeCurrent" -ForegroundColor Red
        Write-Host "✅ $relativeNew" -ForegroundColor Green
        Write-Host "   📋 Prefix: $($_.ExpectedPrefix), Position: $($_.FilePosition), Depth: $($_.Depth)" -ForegroundColor Yellow
        Write-Host ""
    }
}

# Validate existing mapping
if ($ValidateMapping) {
    Write-Info "`n=== MAPPING VALIDATION ==="
    
    if (-not (Test-Path $MappingFile)) {
        Write-Danger "❌ Mapping file not found: $MappingFile"
        Write-Info "Run with -GenerateMapping first"
        exit 1
    }
    
    $mapping = Get-Content $MappingFile | ConvertFrom-Json
    Write-Info "📊 Loaded mapping with $($mapping.RenameOperations.Count) operations"
    
    # Validate all source files exist
    $missingFiles = 0
    $mapping.RenameOperations | ForEach-Object {
        if (-not (Test-Path $_.OriginalPath)) {
            Write-Danger "❌ Missing source file: $($_.OriginalPath)"
            $missingFiles++
        }
    }
    
    # Check target path uniqueness
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
        Write-Safe "✅ Mapping validation passed! Ready for Phase 2 (Link Remapping)."
    } else {
        Write-Danger "❌ Mapping validation failed:"
        Write-Danger "   - Missing files: $missingFiles"
        Write-Danger "   - Duplicate targets: $duplicateTargets" 
        exit 1
    }
}

Write-Info "`n=== PHASE 1 COMPLETE ==="
Write-Safety "📋 Next Steps:"
Write-Safety "  1. Review rename preview above"
Write-Safety "  2. Run with -GenerateMapping to save mapping file"
Write-Safety "  3. Run with -ValidateMapping to verify mapping"
Write-Safety "  4. Proceed to Phase 2 (Link Analysis & Remapping)"
Write-Safety "  5. Target Pattern: XX-XX-XX-filename.md (02-digit padding)"