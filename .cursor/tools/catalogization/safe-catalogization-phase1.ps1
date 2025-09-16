# GALACTIC IDLERS - SAFE CATALOGIZATION REPAIR - PHASE 1
# Production-Grade Rename Mapping with Maximum Safety

param(
    [switch]$GenerateMapping = $false,
    [switch]$ValidateMapping = $false,
    [switch]$Preview = $true
)

$PlanRoot = "Docs\PLAN\Galactic-Idlers-Plan"
$MappingFile = "rename-mapping.json"
$BackupBranch = "catalogization-backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

# Safety Colors
function Write-Safety { param($msg) Write-Host $msg -ForegroundColor Magenta }
function Write-Danger { param($msg) Write-Host $msg -ForegroundColor Red -BackgroundColor Yellow }
function Write-Safe { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }

Write-Safety "=== GALACTIC IDLERS - SAFE CATALOGIZATION PHASE 1 ==="
Write-Info "Mode: $(if($GenerateMapping) {'GENERATE MAPPING'} elseif($ValidateMapping) {'VALIDATE MAPPING'} else {'PREVIEW ONLY'})"

# Step 1: Build comprehensive file analysis
function Get-FileAnalysis {
    param($FilePath)
    
    $relativePath = $FilePath -replace [regex]::Escape($PlanRoot + "\"), ""
    $pathParts = $relativePath -split "\\"
    $fileName = $pathParts[-1]
    $directories = $pathParts[0..($pathParts.Count-2)]
    
    # Calculate directory depth and expected prefix
    $expectedPrefix = @()
    foreach ($dir in $directories) {
        if ($dir -match "^(\d+)") {
            $expectedPrefix += $matches[1]
        }
    }
    $expectedPrefixString = $expectedPrefix -join "-"
    
    # Extract current prefix from filename
    $currentPrefix = ""
    if ($fileName -match "^(\d+(?:-\d+)*)-") {
        $currentPrefix = $matches[1]
    }
    
    # Extract descriptive part
    $descriptivePart = $fileName
    if ($fileName -match "^(?:\d+(?:-\d+)*-)?(.+)$") {
        $descriptivePart = $matches[1]
    }
    
    # Determine correct filename
    $correctFileName = if ($expectedPrefixString -eq "") { 
        $descriptivePart 
    } else { 
        "$expectedPrefixString-$descriptivePart" 
    }
    
    return @{
        OriginalPath = $FilePath
        RelativePath = $relativePath
        FileName = $fileName
        Directory = (Split-Path $FilePath -Parent)
        DirectoryParts = $directories
        CurrentPrefix = $currentPrefix
        ExpectedPrefix = $expectedPrefixString
        DescriptivePart = $descriptivePart
        CorrectFileName = $correctFileName
        NewPath = Join-Path (Split-Path $FilePath -Parent) $correctFileName
        NeedsRename = ($fileName -ne $correctFileName)
        Depth = $directories.Count
    }
}

# Step 2: Detect duplicate conflicts
function Find-DuplicateConflicts {
    param($FileAnalyses)
    
    $conflicts = @()
    $directoryGroups = $FileAnalyses | Group-Object Directory
    
    foreach ($dirGroup in $directoryGroups) {
        # Group by expected prefix
        $prefixGroups = $dirGroup.Group | Group-Object ExpectedPrefix
        
        foreach ($prefixGroup in $prefixGroups) {
            if ($prefixGroup.Count -gt 1) {
                $conflicts += @{
                    Directory = $dirGroup.Name -replace [regex]::Escape($PlanRoot + "\"), ""
                    Prefix = $prefixGroup.Name
                    Count = $prefixGroup.Count
                    Files = $prefixGroup.Group | ForEach-Object { $_.FileName }
                    AnalysisObjects = $prefixGroup.Group
                }
            }
        }
    }
    
    return $conflicts
}

# Step 3: Smart conflict resolution
function Resolve-Conflicts {
    param($Conflicts)
    
    $resolutions = @()
    
    foreach ($conflict in $Conflicts) {
        Write-Danger "🚨 CONFLICT in $($conflict.Directory): prefix '$($conflict.Prefix)' used by $($conflict.Count) files"
        
        $sortedFiles = $conflict.AnalysisObjects | Sort-Object FileName
        for ($i = 0; $i -lt $sortedFiles.Count; $i++) {
            $file = $sortedFiles[$i]
            $suffix = if ($i -eq 0) { "" } else { "-" + ($i + 1) }
            
            $newPrefix = if ($file.ExpectedPrefix -eq "") { 
                ($i + 1).ToString().PadLeft(2, '0')
            } else { 
                $file.ExpectedPrefix + $suffix 
            }
            
            $resolvedFileName = "$newPrefix-$($file.DescriptivePart)"
            $resolvedPath = Join-Path $file.Directory $resolvedFileName
            
            $resolutions += @{
                OriginalPath = $file.OriginalPath
                ResolvedPath = $resolvedPath
                OriginalName = $file.FileName
                ResolvedName = $resolvedFileName
                ConflictType = "Duplicate Prefix"
                Resolution = "Added suffix '$suffix'"
            }
            
            Write-Info "  ✅ $($file.FileName) → $resolvedFileName"
        }
    }
    
    return $resolutions
}

# Step 4: Generate comprehensive mapping
if ($GenerateMapping -or $Preview) {
    Write-Info "`n=== PHASE 1A: COMPREHENSIVE FILE ANALYSIS ==="
    
    $AllFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
        $_.FullName -notmatch "\\reviews\\" 
    }
    
    Write-Info "Analyzing $($AllFiles.Count) files..."
    
    $FileAnalyses = @()
    foreach ($file in $AllFiles) {
        $analysis = Get-FileAnalysis $file.FullName
        $FileAnalyses += $analysis
    }
    
    # Separate files that need renaming
    $FilesNeedingRename = $FileAnalyses | Where-Object { $_.NeedsRename }
    $FilesCorrectlyNamed = $FileAnalyses | Where-Object { -not $_.NeedsRename }
    
    Write-Safe "✅ Correctly named files: $($FilesCorrectlyNamed.Count)"
    Write-Danger "❌ Files needing rename: $($FilesNeedingRename.Count)"
    
    Write-Info "`n=== PHASE 1B: CONFLICT DETECTION ==="
    
    $Conflicts = Find-DuplicateConflicts $FilesNeedingRename
    Write-Danger "🚨 Found $($Conflicts.Count) naming conflicts requiring resolution"
    
    if ($Conflicts.Count -gt 0) {
        Write-Info "`n=== PHASE 1C: SMART CONFLICT RESOLUTION ==="
        $ConflictResolutions = Resolve-Conflicts $Conflicts
        Write-Safe "✅ Generated $($ConflictResolutions.Count) conflict resolutions"
    } else {
        $ConflictResolutions = @()
    }
    
    # Build final rename mapping
    Write-Info "`n=== PHASE 1D: FINAL RENAME MAPPING ==="
    
    $RenameMapping = @{
        Statistics = @{
            TotalFiles = $AllFiles.Count
            CorrectlyNamed = $FilesCorrectlyNamed.Count
            NeedingRename = $FilesNeedingRename.Count
            ConflictCount = $Conflicts.Count
            ResolutionCount = $ConflictResolutions.Count
            GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        }
        ConflictResolutions = $ConflictResolutions
        StandardRenames = @()
    }
    
    # Add non-conflicted renames
    foreach ($file in $FilesNeedingRename) {
        $hasConflict = $ConflictResolutions | Where-Object { $_.OriginalPath -eq $file.OriginalPath }
        if (-not $hasConflict) {
            $RenameMapping.StandardRenames += @{
                OriginalPath = $file.OriginalPath
                NewPath = $file.NewPath
                OriginalName = $file.FileName
                NewName = $file.CorrectFileName
                Type = "Standard Rename"
            }
        }
    }
    
    Write-Safe "✅ Standard renames: $($RenameMapping.StandardRenames.Count)"
    Write-Safe "✅ Conflict resolutions: $($RenameMapping.ConflictResolutions.Count)"
    Write-Safe "✅ Total rename operations: $(($RenameMapping.StandardRenames.Count) + ($RenameMapping.ConflictResolutions.Count))"
    
    # Save mapping for next phase
    if ($GenerateMapping) {
        $mappingJson = $RenameMapping | ConvertTo-Json -Depth 10
        Set-Content $MappingFile -Value $mappingJson
        Write-Safe "✅ Mapping saved to: $MappingFile"
    }
    
    # Preview sample operations
    Write-Info "`n=== RENAME PREVIEW (Top 10) ==="
    $allRenames = $RenameMapping.StandardRenames + $RenameMapping.ConflictResolutions
    $allRenames[0..9] | ForEach-Object {
        $relativeCurrent = $_.OriginalPath -replace [regex]::Escape($PlanRoot + "\"), ""
        $relativeNew = $_.NewPath -replace [regex]::Escape($PlanRoot + "\"), ""
        Write-Host "❌ $relativeCurrent" -ForegroundColor Red
        Write-Host "✅ $relativeNew" -ForegroundColor Green
        if ($_.Resolution) {
            Write-Host "   📋 $($_.Resolution)" -ForegroundColor Yellow
        }
        Write-Host ""
    }
}

# Step 5: Validate existing mapping
if ($ValidateMapping) {
    Write-Info "`n=== MAPPING VALIDATION ==="
    
    if (-not (Test-Path $MappingFile)) {
        Write-Danger "❌ Mapping file not found: $MappingFile"
        Write-Info "Run with -GenerateMapping first"
        exit 1
    }
    
    $mapping = Get-Content $MappingFile | ConvertFrom-Json
    Write-Info "📊 Loaded mapping with $($mapping.StandardRenames.Count + $mapping.ConflictResolutions.Count) operations"
    
    # Validate all source files exist
    $missingFiles = 0
    $mapping.StandardRenames + $mapping.ConflictResolutions | ForEach-Object {
        if (-not (Test-Path $_.OriginalPath)) {
            Write-Danger "❌ Missing source file: $($_.OriginalPath)"
            $missingFiles++
        }
    }
    
    # Check for target path conflicts
    $targetPaths = @{}
    $conflicts = 0
    $mapping.StandardRenames + $mapping.ConflictResolutions | ForEach-Object {
        $targetPath = if ($_.NewPath) { $_.NewPath } else { $_.ResolvedPath }
        if ($targetPaths.ContainsKey($targetPath)) {
            Write-Danger "❌ Target path conflict: $targetPath"
            $conflicts++
        }
        $targetPaths[$targetPath] = $true
    }
    
    if ($missingFiles -eq 0 -and $conflicts -eq 0) {
        Write-Safe "✅ Mapping validation passed! Ready for Phase 2."
    } else {
        Write-Danger "❌ Mapping validation failed: $missingFiles missing files, $conflicts conflicts"
        exit 1
    }
}

Write-Info "`n=== PHASE 1 COMPLETE ==="
Write-Safety "📋 Next Steps:"
Write-Safety "  1. Review rename preview above"
Write-Safety "  2. Run with -GenerateMapping to save mapping file"
Write-Safety "  3. Run with -ValidateMapping to verify mapping"
Write-Safety "  4. Proceed to Phase 2 (Link Analysis & Remapping)"