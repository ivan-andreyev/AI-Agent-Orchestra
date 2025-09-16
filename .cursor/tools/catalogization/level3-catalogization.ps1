# GALACTIC IDLERS - LEVEL 3 CATALOGIZATION
# Переименовывает файлы из XX-X-X-Y в XX-XX-XX-0Y формат

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

Write-Safety "=== GALACTIC IDLERS - LEVEL 3 CATALOGIZATION ==="
Write-Info "Converting XX-X-X-Y patterns to XX-XX-XX-0Y format"

Write-Info "`n=== PHASE 1: SCANNING LEVEL 3+ FILES ==="

# Find all Level 3+ files (files with pattern XX-X-X-Y where Y >= 2)
$level3Files = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object {
    $_.Name -match "^\d{2}-\d-\d-[2-9]-" -or 
    $_.Name -match "^\d{2}-\d{2}-\d-[2-9]-" -or
    $_.Name -match "^\d{2}-\d{2}-\d{2}-[2-9]-"
} | Where-Object { 
    $_.FullName -notmatch "\\reviews\\" 
}

Write-Info "Found $($level3Files.Count) Level 3+ files to process"

if ($level3Files.Count -eq 0) {
    Write-Safe "✅ No Level 3+ files found - all files are already properly catalogized!"
    exit 0
}

Write-Info "`n=== PHASE 2: ANALYZING RENAME OPERATIONS ==="

$renameOperations = @()

foreach ($file in $level3Files) {
    $oldName = $file.Name
    $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
    
    # Apply Level 3 transformation patterns
    $newName = $oldName
    
    # Pattern: XX-X-X-Y -> XX-XX-XX-0Y
    if ($oldName -match "^(\d{2})-(\d)-(\d)-([2-9])-(.+)$") {
        $part1 = $matches[1]  # XX
        $part2 = $matches[2]  # X -> XX (pad with zero)
        $part3 = $matches[3]  # X -> XX (pad with zero)
        $part4 = $matches[4]  # Y -> 0Y (pad with zero)
        $suffix = $matches[5]  # rest of filename
        
        $newName = "$part1-$($part1)-$($part1)-$($part4.PadLeft(2,'0'))-$suffix"
    }
    # Pattern: XX-XX-X-Y -> XX-XX-XX-0Y  
    elseif ($oldName -match "^(\d{2})-(\d{2})-(\d)-([2-9])-(.+)$") {
        $part1 = $matches[1]  # XX
        $part2 = $matches[2]  # XX
        $part3 = $matches[3]  # X -> XX (pad with zero)
        $part4 = $matches[4]  # Y -> 0Y (pad with zero)
        $suffix = $matches[5]  # rest of filename
        
        $newName = "$part1-$part2-$($part1)-$($part4.PadLeft(2,'0'))-$suffix"
    }
    # Pattern: XX-XX-XX-Y -> XX-XX-XX-0Y
    elseif ($oldName -match "^(\d{2})-(\d{2})-(\d{2})-([2-9])-(.+)$") {
        $part1 = $matches[1]  # XX
        $part2 = $matches[2]  # XX
        $part3 = $matches[3]  # XX
        $part4 = $matches[4]  # Y -> 0Y (pad with zero)
        $suffix = $matches[5]  # rest of filename
        
        $newName = "$part1-$part2-$part3-$($part4.PadLeft(2,'0'))-$suffix"
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

Write-Info "Level 3 rename operations planned: $($renameOperations.Count)"

if ($Preview -and $renameOperations.Count -gt 0) {
    Write-Info "`n=== PREVIEW: FIRST 15 LEVEL 3 RENAMES ==="
    
    $renameOperations | Select-Object -First 15 | ForEach-Object {
        Write-Host "`n📁 PATH: $($_.RelativePath)" -ForegroundColor White
        Write-Host "   ❌ OLD: $($_.OldName)" -ForegroundColor Red
        Write-Host "   ✅ NEW: $($_.NewName)" -ForegroundColor Green
    }
    
    if ($renameOperations.Count -gt 15) {
        Write-Host "`n... and $($renameOperations.Count - 15) more rename operations" -ForegroundColor Yellow
    }
}

if ($Execute -and $renameOperations.Count -gt 0) {
    Write-Danger "`n=== PHASE 3: EXECUTING LEVEL 3 RENAMES ==="
    
    $confirm = Read-Host "Type 'EXECUTE LEVEL 3 CATALOGIZATION' to proceed with $($renameOperations.Count) renames"
    
    if ($confirm -eq "EXECUTE LEVEL 3 CATALOGIZATION") {
        Write-Info "`n--- EXECUTING LEVEL 3 RENAMES ---"
        
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
        
        Write-Safe "`n🎉 LEVEL 3 CATALOGIZATION COMPLETE!"
        Write-Host "✅ Successfully renamed: $successCount files" -ForegroundColor Green
        if ($errorCount -gt 0) {
            Write-Host "❌ Failed renames: $errorCount files" -ForegroundColor Red
        }
        
    } else {
        Write-Info "❌ Operation cancelled"
    }
} elseif ($renameOperations.Count -eq 0) {
    Write-Safe "`n✅ NO LEVEL 3+ FILES NEED RENAMING - Already properly catalogized!"
}

Write-Info "`n=== LEVEL 3 CATALOGIZATION SUMMARY ==="
Write-Host "Files found for Level 3 catalogization: $($level3Files.Count)" -ForegroundColor Cyan
Write-Host "Rename operations planned: $($renameOperations.Count)" -ForegroundColor Green

Write-Info "`n=== LEVEL 3 CATALOGIZATION COMPLETE ==="