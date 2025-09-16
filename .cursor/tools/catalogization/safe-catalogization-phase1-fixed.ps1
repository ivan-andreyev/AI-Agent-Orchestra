# GALACTIC IDLERS - SAFE CATALOGIZATION REPAIR - PHASE 1 (FIXED)
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

Write-Safety "=== GALACTIC IDLERS - SAFE CATALOGIZATION PHASE 1 (FIXED) ==="
Write-Info "Target Pattern: 01-01-01-filename.md (02-digit padding)"

# CORRECTED: Proper file analysis with directory-based prefix calculation
function Get-FileAnalysisFixed {
    param($FilePath)
    
    # Calculate relative path correctly
    $relativePath = $FilePath.Substring($PlanRoot.Length + 1)
    $pathParts = $relativePath -split "\\"
    $fileName = $pathParts[-1]
    $directoryParts = $pathParts[0..($pathParts.Count-2)]
    
    # Extract directory numbers with 02-digit padding
    $directoryNumbers = @()
    foreach ($dirPart in $directoryParts) {
        if ($dirPart -match "^(\d+)") {
            $number = [int]$matches[1]
            $directoryNumbers += $number.ToString("00")  # 02-digit padding
        }
    }
    
    # Calculate expected prefix based on directory structure
    $expectedPrefix = ""
    if ($directoryNumbers.Count -gt 0) {
        $expectedPrefix = $directoryNumbers -join "-"
    }
    
    # Extract current prefix and descriptive part from filename
    $currentPrefix = ""
    $descriptivePart = $fileName
    if ($fileName -match "^(\d+(?:-\d+)*)-(.+)$") {
        $currentPrefix = $matches[1]
        $descriptivePart = $matches[2]
    } elseif ($fileName -match "^(\d+)(.+)$") {
        $currentPrefix = $matches[1]
        $descriptivePart = $matches[2] -replace "^-", ""
    }
    
    # Determine correct filename
    $correctFileName = if ($expectedPrefix -eq "") { 
        $descriptivePart  # Root level files don't need prefix
    } else { 
        "$expectedPrefix-XX-$descriptivePart"  # XX will be replaced with file position
    }
    
    return @{
        OriginalPath = $FilePath
        RelativePath = $relativePath
        FileName = $fileName
        Directory = if ($directoryParts.Count -gt 0) { $directoryParts -join "\" } else { "ROOT" }
        DirectoryParts = $directoryParts
        DirectoryNumbers = $directoryNumbers
        CurrentPrefix = $currentPrefix
        ExpectedPrefix = $expectedPrefix
        DescriptivePart = $descriptivePart
        CorrectFileNameTemplate = $correctFileName
        Depth = $directoryParts.Count
        NeedsRename = $true  # Will be calculated later
    }
}

# Group files by directory and assign sequential file numbers
function Assign-FilePositions {
    param($FileAnalyses)
    
    $processedFiles = @()
    $directoryGroups = $FileAnalyses | Group-Object Directory
    
    foreach ($dirGroup in $directoryGroups) {
        # Debug output for directory grouping
        if ($dirGroup.Count -gt 100) {
            Write-Info "❌ PROBLEM: Directory '$($dirGroup.Name)' has $($dirGroup.Count) files - too many!"
            Write-Info "   First 5 files in this group:"
            $dirGroup.Group[0..4] | ForEach-Object { Write-Info "   - $($_.FileName) (RelativePath: '$($_.RelativePath)') (Directory: '$($_.Directory)')" }
        } else {
            Write-Info "✅ Directory '$($dirGroup.Name -replace [regex]::Escape($PlanRoot), '')' has $($dirGroup.Count) files"
        }
        
        # Sort files alphabetically within directory for consistent numbering
        $sortedFiles = $dirGroup.Group | Sort-Object DescriptivePart
        
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
            
            $newPath = Join-Path $file.Directory $correctFileName
            
            # Check if rename is needed
            $needsRename = ($file.FileName -ne $correctFileName)
            
            $processedFiles += @{
                OriginalPath = $file.OriginalPath
                NewPath = $newPath
                OriginalName = $file.FileName
                NewName = $correctFileName
                Directory = $file.Directory
                RelativePath = $file.RelativePath
                ExpectedPrefix = $file.ExpectedPrefix
                FilePosition = $filePosition
                DescriptivePart = $file.DescriptivePart
                NeedsRename = $needsRename
                Depth = $file.Depth
            }
        }
    }
    
    return $processedFiles
}

# Generate comprehensive mapping with conflict detection
if ($GenerateMapping -or $Preview) {
    Write-Info "`n=== PHASE 1A: COMPREHENSIVE FILE ANALYSIS WITH FIXED LOGIC ==="
    
    $AllFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
        $_.FullName -notmatch "\\reviews\\" 
    }
    
    Write-Info "Analyzing $($AllFiles.Count) files..."
    
    # Step 1: Basic file analysis
    $FileAnalyses = @()
    foreach ($file in $AllFiles) {
        $analysis = Get-FileAnalysisFixed $file.FullName
        $FileAnalyses += $analysis
    }
    
    # Step 2: Assign proper file positions and generate final mapping
    Write-Info "`n=== PHASE 1B: ASSIGNING SEQUENTIAL FILE POSITIONS ==="
    $ProcessedFiles = Assign-FilePositions $FileAnalyses
    
    # Separate files that need renaming
    $FilesNeedingRename = $ProcessedFiles | Where-Object { $_.NeedsRename }
    $FilesCorrectlyNamed = $ProcessedFiles | Where-Object { -not $_.NeedsRename }
    
    Write-Safe "✅ Correctly named files: $($FilesCorrectlyNamed.Count)"
    Write-Danger "❌ Files needing rename: $($FilesNeedingRename.Count)"
    
    # Step 3: Check for potential conflicts (should be minimal with new logic)
    Write-Info "`n=== PHASE 1C: CONFLICT DETECTION ==="
    $targetPaths = @{}
    $conflicts = @()
    
    foreach ($file in $ProcessedFiles) {
        if ($targetPaths.ContainsKey($file.NewPath)) {
            $conflicts += "Conflict: $($file.NewPath)"
        }
        $targetPaths[$file.NewPath] = $true
    }
    
    if ($conflicts.Count -gt 0) {
        Write-Danger "🚨 Found $($conflicts.Count) target path conflicts!"
        $conflicts | ForEach-Object { Write-Danger $_ }
    } else {
        Write-Safe "✅ No target path conflicts detected"
    }
    
    # Build final rename mapping
    Write-Info "`n=== PHASE 1D: FINAL RENAME MAPPING ==="
    
    $RenameMapping = @{
        Statistics = @{
            TotalFiles = $AllFiles.Count
            CorrectlyNamed = $FilesCorrectlyNamed.Count
            NeedingRename = $FilesNeedingRename.Count
            ConflictCount = $conflicts.Count
            PaddingDigits = 2
            GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        }
        RenameOperations = $FilesNeedingRename | ForEach-Object {
            @{
                OriginalPath = $_.OriginalPath
                NewPath = $_.NewPath
                OriginalName = $_.OriginalName
                NewName = $_.NewName
                ExpectedPrefix = $_.ExpectedPrefix
                FilePosition = $_.FilePosition
                DescriptivePart = $_.DescriptivePart
                Depth = $_.Depth
                Type = "Standard Rename with 02-Padding"
            }
        }
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
    
    # Show statistics by depth
    Write-Info "`n=== STATISTICS BY DIRECTORY DEPTH ==="
    $depthStats = $ProcessedFiles | Group-Object Depth | Sort-Object Name
    $depthStats | ForEach-Object {
        $needRename = ($_.Group | Where-Object { $_.NeedsRename }).Count
        Write-Host "Depth $($_.Name): $($_.Count) files, $needRename need rename" -ForegroundColor Cyan
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
    
    # Validate 02-digit padding
    $paddingViolations = 0
    $mapping.RenameOperations | ForEach-Object {
        if ($_.ExpectedPrefix -and $_.ExpectedPrefix -match "\b\d\b") {
            Write-Danger "❌ Single-digit number found in prefix: $($_.NewName)"
            $paddingViolations++
        }
    }
    
    if ($missingFiles -eq 0 -and $duplicateTargets -eq 0 -and $paddingViolations -eq 0) {
        Write-Safe "✅ Mapping validation passed! Ready for Phase 2 (Link Remapping)."
    } else {
        Write-Danger "❌ Mapping validation failed:"
        Write-Danger "   - Missing files: $missingFiles"
        Write-Danger "   - Duplicate targets: $duplicateTargets" 
        Write-Danger "   - Padding violations: $paddingViolations"
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