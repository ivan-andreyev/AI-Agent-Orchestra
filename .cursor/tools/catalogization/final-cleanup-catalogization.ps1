# GALACTIC IDLERS - FINAL CLEANUP CATALOGIZATION  
# Исправляет ВСЕ оставшиеся паттерны каталогизации

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

Write-Safety "=== GALACTIC IDLERS - FINAL CLEANUP CATALOGIZATION ==="
Write-Info "Fixing ALL remaining non-standard catalogization patterns"

Write-Info "`n=== PHASE 1: SCANNING NON-STANDARD PATTERNS ==="

# Find all files with non-standard patterns
$problemFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object {
    # Files with old XX-X-X patterns that should be XX-XX-XX
    ($_.Name -match "^\d{2}-\d-\d-") -or
    # Files with XX-XX-X patterns that should be XX-XX-XX
    ($_.Name -match "^\d{2}-\d{2}-\d-") -or  
    # Files with mixed patterns like XX-X-XX
    ($_.Name -match "^\d{2}-\d-\d{2}-") -or
    # Files with very deep single digits (XX-XX-XX-XX-X-)
    ($_.Name -match "\d{2}-\d{2}-\d{2}-\d{2}-\d-") -or
    # Files with ultra deep single digits (XX-XX-XX-XX-XX-X-)  
    ($_.Name -match "\d{2}-\d{2}-\d{2}-\d{2}-\d{2}-\d-")
} | Where-Object { 
    $_.FullName -notmatch "\\reviews\\" 
}

Write-Info "Found $($problemFiles.Count) files with non-standard patterns"

if ($problemFiles.Count -eq 0) {
    Write-Safe "✅ All files are properly catalogized!"
    exit 0
}

Write-Info "`n=== PHASE 2: ANALYZING CLEANUP OPERATIONS ==="

$renameOperations = @()

foreach ($file in $problemFiles) {
    $oldName = $file.Name
    $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
    
    $newName = $oldName
    
    # Universal cleanup patterns - pad all single digits to 02-digit format
    
    # Pattern 1: XX-X-X-Y-... -> XX-0X-0X-0Y-...
    if ($oldName -match "^(\d{2})-(\d)-(\d)-(\d+)-(.+)$") {
        $part1 = $matches[1]  # XX
        $part2 = $matches[2].PadLeft(2,'0')  # X -> 0X
        $part3 = $matches[3].PadLeft(2,'0')  # X -> 0X
        $part4 = $matches[4].PadLeft(2,'0')  # Y -> 0Y
        $suffix = $matches[5]  # rest
        
        $newName = "$part1-$part2-$part3-$part4-$suffix"
    }
    # Pattern 2: XX-X-X-... -> XX-0X-0X-...  
    elseif ($oldName -match "^(\d{2})-(\d)-(\d)-(.+)$") {
        $part1 = $matches[1]  # XX
        $part2 = $matches[2].PadLeft(2,'0')  # X -> 0X
        $part3 = $matches[3].PadLeft(2,'0')  # X -> 0X
        $suffix = $matches[4]  # rest
        
        $newName = "$part1-$part2-$part3-$suffix"
    }
    # Pattern 3: XX-XX-X-... -> XX-XX-0X-...
    elseif ($oldName -match "^(\d{2})-(\d{2})-(\d)-(.+)$") {
        $part1 = $matches[1]  # XX
        $part2 = $matches[2]  # XX
        $part3 = $matches[3].PadLeft(2,'0')  # X -> 0X
        $suffix = $matches[4]  # rest
        
        $newName = "$part1-$part2-$part3-$suffix"
    }
    # Pattern 4: XX-X-XX-... -> XX-0X-XX-...
    elseif ($oldName -match "^(\d{2})-(\d)-(\d{2})-(.+)$") {
        $part1 = $matches[1]  # XX
        $part2 = $matches[2].PadLeft(2,'0')  # X -> 0X
        $part3 = $matches[3]  # XX
        $suffix = $matches[4]  # rest
        
        $newName = "$part1-$part2-$part3-$suffix"
    }
    # Pattern 5: XX-XX-XX-XX-X-... -> XX-XX-XX-XX-0X-...
    elseif ($oldName -match "^(\d{2}-\d{2}-\d{2}-\d{2})-(\d)-(.+)$") {
        $prefix = $matches[1]  # XX-XX-XX-XX
        $singleDigit = $matches[2].PadLeft(2,'0')  # X -> 0X  
        $suffix = $matches[3]  # rest
        
        $newName = "$prefix-$singleDigit-$suffix"
    }
    # Pattern 6: XX-XX-XX-XX-XX-X-... -> XX-XX-XX-XX-XX-0X-...
    elseif ($oldName -match "^(\d{2}-\d{2}-\d{2}-\d{2}-\d{2})-(\d)-(.+)$") {
        $prefix = $matches[1]  # XX-XX-XX-XX-XX
        $singleDigit = $matches[2].PadLeft(2,'0')  # X -> 0X  
        $suffix = $matches[3]  # rest
        
        $newName = "$prefix-$singleDigit-$suffix"
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

Write-Info "Final cleanup operations planned: $($renameOperations.Count)"

if ($Preview -and $renameOperations.Count -gt 0) {
    Write-Info "`n=== PREVIEW: FIRST 15 CLEANUP OPERATIONS ==="
    
    $renameOperations | Select-Object -First 15 | ForEach-Object {
        Write-Host "`n📁 PATH: $($_.RelativePath)" -ForegroundColor White
        Write-Host "   ❌ OLD: $($_.OldName)" -ForegroundColor Red
        Write-Host "   ✅ NEW: $($_.NewName)" -ForegroundColor Green
    }
    
    if ($renameOperations.Count -gt 15) {
        Write-Host "`n... and $($renameOperations.Count - 15) more cleanup operations" -ForegroundColor Yellow
    }
}

if ($Execute -and $renameOperations.Count -gt 0) {
    Write-Danger "`n=== PHASE 3: EXECUTING FINAL CLEANUP ==="
    
    $confirm = Read-Host "Type 'EXECUTE FINAL CLEANUP' to complete ALL catalogization ($($renameOperations.Count) renames)"
    
    if ($confirm -eq "EXECUTE FINAL CLEANUP") {
        Write-Info "`n--- EXECUTING FINAL CLEANUP ---"
        
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
        
        Write-Safe "`n🎉 FINAL CLEANUP COMPLETE!"
        Write-Host "✅ Successfully renamed: $successCount files" -ForegroundColor Green
        if ($errorCount -gt 0) {
            Write-Host "❌ Failed renames: $errorCount files" -ForegroundColor Red
        }
        
    } else {
        Write-Info "❌ Operation cancelled"
    }
} elseif ($renameOperations.Count -eq 0) {
    Write-Safe "`n✅ NO FILES NEED CLEANUP - Perfect catalogization!"
}

Write-Info "`n=== FINAL CLEANUP SUMMARY ==="
Write-Host "Problem files found: $($problemFiles.Count)" -ForegroundColor Cyan
Write-Host "Cleanup operations planned: $($renameOperations.Count)" -ForegroundColor Green

Write-Info "`n=== FINAL CLEANUP COMPLETE ==="