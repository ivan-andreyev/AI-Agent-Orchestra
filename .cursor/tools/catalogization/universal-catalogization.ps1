# GALACTIC IDLERS - UNIVERSAL DEEP CATALOGIZATION
# Каталогизирует все оставшиеся файлы Level 4+ в стандарт XX-XX-XX-XX-0Y

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

Write-Safety "=== GALACTIC IDLERS - UNIVERSAL DEEP CATALOGIZATION ==="
Write-Info "Completing catalogization of ALL remaining Level 4+ files"

Write-Info "`n=== PHASE 1: SCANNING DEEP LEVEL FILES ==="

# Find all files with deep level patterns that need catalogization
$deepFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object {
    # Match files with pattern XX-XX-XX-Y where Y >= 2 (Level 4+)  
    ($_.Name -match "^\d{2}-\d{2}-\d{2}-[2-9]+-") -or
    # Match files with pattern XX-XX-XX-XX-Y where Y >= 2 (Level 5+)
    ($_.Name -match "^\d{2}-\d{2}-\d{2}-\d{2}-[2-9]+-") -or
    # Match files with any deeper patterns
    ($_.Name -match "^\d{2}-\d{2}-\d{2}-\d+-[2-9]+-")
} | Where-Object { 
    $_.FullName -notmatch "\\reviews\\" 
}

Write-Info "Found $($deepFiles.Count) deep level files to process"

if ($deepFiles.Count -eq 0) {
    Write-Safe "✅ No deep level files found - all files are fully catalogized!"
    exit 0
}

Write-Info "`n=== PHASE 2: ANALYZING DEEP CATALOGIZATION ==="

$renameOperations = @()

foreach ($file in $deepFiles) {
    $oldName = $file.Name
    $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
    
    $newName = $oldName
    
    # Universal deep level transformation
    # Pattern: XX-XX-XX-Y-suffix -> XX-XX-XX-0Y-suffix (Level 4)
    if ($oldName -match "^(\d{2}-\d{2}-\d{2})-([2-9])-(.+)$") {
        $prefix = $matches[1]      # XX-XX-XX
        $levelNum = $matches[2]    # Y
        $suffix = $matches[3]      # rest of filename
        
        $newName = "$prefix-$($levelNum.PadLeft(2,'0'))-$suffix"
    }
    # Pattern: XX-XX-XX-XX-Y-suffix -> XX-XX-XX-XX-0Y-suffix (Level 5)  
    elseif ($oldName -match "^(\d{2}-\d{2}-\d{2}-\d{2})-([2-9])-(.+)$") {
        $prefix = $matches[1]      # XX-XX-XX-XX
        $levelNum = $matches[2]    # Y  
        $suffix = $matches[3]      # rest of filename
        
        $newName = "$prefix-$($levelNum.PadLeft(2,'0'))-$suffix"
    }
    # Pattern: XX-XX-XX-X-Y-suffix -> XX-XX-XX-0X-0Y-suffix (complex level)
    elseif ($oldName -match "^(\d{2}-\d{2}-\d{2})-(\d)-([2-9])-(.+)$") {
        $basePrefix = $matches[1]  # XX-XX-XX
        $midLevel = $matches[2]    # X
        $levelNum = $matches[3]    # Y
        $suffix = $matches[4]      # rest of filename
        
        $newName = "$basePrefix-$($midLevel.PadLeft(2,'0'))-$($levelNum.PadLeft(2,'0'))-$suffix"
    }
    
    if ($newName -ne $oldName) {
        $newPath = Join-Path (Split-Path $file.FullName) $newName
        
        $renameOperations += @{
            OldPath = $file.FullName
            NewPath = $newPath
            OldName = $oldName
            NewName = $newName
            RelativePath = $relativePath
            NewRelativePath = $newPath.Substring($PlanRoot.Length + 1)
        }
    }
}

Write-Info "Deep catalogization operations planned: $($renameOperations.Count)"

if ($Preview -and $renameOperations.Count -gt 0) {
    Write-Info "`n=== PREVIEW: ALL DEEP CATALOGIZATION RENAMES ==="
    
    $renameOperations | ForEach-Object {
        Write-Host "`n📁 PATH: $($_.RelativePath)" -ForegroundColor White
        Write-Host "   ❌ OLD: $($_.OldName)" -ForegroundColor Red
        Write-Host "   ✅ NEW: $($_.NewName)" -ForegroundColor Green
    }
    
    Write-Host "`nTotal deep catalogization operations: $($renameOperations.Count)" -ForegroundColor Yellow
}

if ($Execute -and $renameOperations.Count -gt 0) {
    Write-Danger "`n=== PHASE 3: EXECUTING DEEP CATALOGIZATION ==="
    
    $confirm = Read-Host "Type 'EXECUTE DEEP CATALOGIZATION' to complete ALL deep catalogization ($($renameOperations.Count) renames)"
    
    if ($confirm -eq "EXECUTE DEEP CATALOGIZATION") {
        Write-Info "`n--- EXECUTING DEEP CATALOGIZATION ---"
        
        $successCount = 0
        $errorCount = 0
        
        foreach ($op in $renameOperations) {
            try {
                # Check if target doesn't already exist
                if (Test-Path $op.NewPath) {
                    Write-Danger "❌ SKIP: Target already exists - $($op.NewName)"
                    $errorCount++
                    continue
                }
                
                # Perform the rename
                Rename-Item -Path $op.OldPath -NewName $op.NewName
                Write-Safe "✅ $($op.RelativePath) → $($op.NewName)"
                $successCount++
                
            } catch {
                Write-Danger "❌ FAILED: $($op.OldName) - $($_.Exception.Message)"
                $errorCount++
            }
        }
        
        Write-Safe "`n🎉 DEEP CATALOGIZATION COMPLETE!"
        Write-Host "✅ Successfully renamed: $successCount files" -ForegroundColor Green
        if ($errorCount -gt 0) {
            Write-Host "❌ Failed renames: $errorCount files" -ForegroundColor Red
        }
        
    } else {
        Write-Info "❌ Operation cancelled"
    }
} elseif ($renameOperations.Count -eq 0) {
    Write-Safe "`n✅ NO DEEP LEVEL FILES NEED RENAMING - Fully catalogized!"
}

Write-Info "`n=== UNIVERSAL DEEP CATALOGIZATION SUMMARY ==="
Write-Host "Deep level files found: $($deepFiles.Count)" -ForegroundColor Cyan
Write-Host "Rename operations planned: $($renameOperations.Count)" -ForegroundColor Green

Write-Info "`n=== UNIVERSAL DEEP CATALOGIZATION COMPLETE ==="