# FINAL DIRECTORY CLEANUP - Исправляет все оставшиеся паттерны
param(
    [switch]$Preview = $true,
    [switch]$Execute = $false
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path

function Write-Safety { param($msg) Write-Host $msg -ForegroundColor Magenta }
function Write-Danger { param($msg) Write-Host $msg -ForegroundColor Red -BackgroundColor Yellow }
function Write-Safe { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }

Write-Safety "=== FINAL DIRECTORY CLEANUP ==="
Write-Info "Исправляем ВСЕ оставшиеся паттерны с одиночными цифрами"

Write-Info "`n=== PHASE 1: ПОИСК ОСТАВШИХСЯ ПРОБЛЕМ ==="

# Найдем ВСЕ директории с одиночными цифрами в любом месте названия
$remainingBadDirs = Get-ChildItem -Path $PlanRoot -Recurse -Directory | Where-Object {
    # Ищем паттерны где есть одиночная цифра между тире (но не в составе 2-значного числа)
    $_.Name -match "-\d-" -and $_.Name -notmatch "-\d{2}-\d{2}-"
} | Sort-Object FullName

Write-Info "Найдено $($remainingBadDirs.Count) директорий с оставшимися проблемами"

if ($remainingBadDirs.Count -eq 0) {
    Write-Safe "✅ Все директории правильно каталогизированы!"
    exit 0
}

# Группируем по типам проблем
$problems = @{
    "XX-X-Y" = @()
    "XX-XX-X" = @()
    "XX-X-XX" = @()
    "XX-XX-XX-X" = @()
    "Other" = @()
}

foreach ($dir in $remainingBadDirs) {
    $name = $dir.Name
    if ($name -match "^\d{2}-\d-\d-") {
        $problems["XX-X-Y"] += $dir
    }
    elseif ($name -match "^\d{2}-\d{2}-\d-") {
        $problems["XX-XX-X"] += $dir
    }
    elseif ($name -match "^\d{2}-\d-\d{2}-") {
        $problems["XX-X-XX"] += $dir
    }
    elseif ($name -match "^\d{2}-\d{2}-\d{2}-\d-") {
        $problems["XX-XX-XX-X"] += $dir
    }
    else {
        $problems["Other"] += $dir
    }
}

Write-Info "`nТипы проблем:"
foreach ($type in $problems.Keys) {
    $count = $problems[$type].Count
    if ($count -gt 0) {
        Write-Host "  $type`: $count директорий" -ForegroundColor Yellow
    }
}

Write-Info "`n=== PHASE 2: ПЛАНИРОВАНИЕ ИСПРАВЛЕНИЙ ==="

$finalRenameOperations = @()

foreach ($dir in $remainingBadDirs) {
    $oldName = $dir.Name
    $newName = $oldName
    
    # Универсальная замена всех одиночных цифр на двузначные
    $newName = $newName -replace '-(\d)-', '-0$1-'
    
    if ($newName -ne $oldName) {
        $newPath = Join-Path (Split-Path $dir.FullName) $newName
        
        $finalRenameOperations += @{
            OldPath = $dir.FullName
            NewPath = $newPath
            OldName = $oldName
            NewName = $newName
            RelativePath = $dir.FullName.Substring($PlanRoot.Length + 1)
            NewRelativePath = $newPath.Substring($PlanRoot.Length + 1)
        }
    }
}

Write-Info "Запланировано $($finalRenameOperations.Count) финальных переименований"

if ($Preview -and $finalRenameOperations.Count -gt 0) {
    Write-Info "`n=== PREVIEW: ФИНАЛЬНЫЕ ПЕРЕИМЕНОВАНИЯ ==="
    
    foreach ($op in $finalRenameOperations) {
        Write-Host "`n📁 $($op.RelativePath)" -ForegroundColor White
        Write-Host "   ❌ OLD: $($op.OldName)" -ForegroundColor Red
        Write-Host "   ✅ NEW: $($op.NewName)" -ForegroundColor Green
    }
}

if ($Execute -and $finalRenameOperations.Count -gt 0) {
    Write-Danger "`n=== ВЫПОЛНЕНИЕ ФИНАЛЬНОЙ ОЧИСТКИ ==="
    
    $confirm = Read-Host "Type 'EXECUTE FINAL DIRECTORY CLEANUP' to proceed with $($finalRenameOperations.Count) final renames"
    
    if ($confirm -eq "EXECUTE FINAL DIRECTORY CLEANUP") {
        Write-Info "`n--- ВЫПОЛНЕНИЕ ФИНАЛЬНЫХ ПЕРЕИМЕНОВАНИЙ ---"
        
        $successCount = 0
        $errorCount = 0
        
        # Сортируем по глубине (сначала самые глубокие)
        $sortedOps = $finalRenameOperations | Sort-Object { ($_.RelativePath -split '\\').Count } -Descending
        
        foreach ($op in $sortedOps) {
            try {
                if (Test-Path $op.NewPath) {
                    Write-Danger "❌ SKIP: Target exists - $($op.NewName)"
                    $errorCount++
                    continue
                }
                
                Rename-Item -Path $op.OldPath -NewName $op.NewName
                Write-Safe "✅ FINAL: $($op.OldName) → $($op.NewName)"
                $successCount++
                
            } catch {
                Write-Danger "❌ FAILED: $($op.OldName) - $($_.Exception.Message)"
                $errorCount++
            }
        }
        
        Write-Safe "`n🎉 ФИНАЛЬНАЯ ОЧИСТКА ДИРЕКТОРИЙ ЗАВЕРШЕНА!"
        Write-Host "✅ Successfully renamed: $successCount directories" -ForegroundColor Green
        
        if ($errorCount -gt 0) {
            Write-Host "❌ Failed renames: $errorCount directories" -ForegroundColor Red
        }
        
        # Проверяем результат
        Write-Info "`n--- ПРОВЕРКА РЕЗУЛЬТАТА ---"
        $remainingAfterFix = Get-ChildItem -Path $PlanRoot -Recurse -Directory | Where-Object {
            $_.Name -match "-\d-" -and $_.Name -notmatch "-\d{2}-"
        }
        
        if ($remainingAfterFix.Count -eq 0) {
            Write-Safe "✅ ИДЕАЛЬНО! Все директории теперь имеют правильную каталогизацию!"
        } else {
            Write-Danger "⚠️  Осталось $($remainingAfterFix.Count) директорий с проблемами"
            $remainingAfterFix | Select-Object -First 5 | ForEach-Object {
                $relativePath = $_.FullName.Substring($PlanRoot.Length + 1)
                Write-Host "   📁 $relativePath" -ForegroundColor Yellow
            }
        }
        
    } else {
        Write-Info "❌ Operation cancelled"
    }
}

Write-Info "`n=== FINAL DIRECTORY CLEANUP SUMMARY ==="
Write-Host "Remaining bad directories: $($remainingBadDirs.Count)" -ForegroundColor Cyan
Write-Host "Final rename operations: $($finalRenameOperations.Count)" -ForegroundColor Green