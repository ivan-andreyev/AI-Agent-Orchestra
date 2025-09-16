# FIX DIRECTORY CATALOGIZATION - Исправляет дублирующие номера
param(
    [switch]$Preview = $true,
    [switch]$Execute = $false
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path

function Write-Safety { param($msg) Write-Host $msg -ForegroundColor Magenta }
function Write-Danger { param($msg) Write-Host $msg -ForegroundColor Red -BackgroundColor Yellow }
function Write-Safe { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }

Write-Safety "=== FIX DIRECTORY CATALOGIZATION ==="
Write-Info "Исправляем дублирующие номера директорий и файлов"

Write-Info "`n=== АНАЛИЗ ПРОБЛЕМЫ ==="

# Найдем все директории с неправильными номерами
$badDirs = Get-ChildItem -Path $PlanRoot -Recurse -Directory | Where-Object {
    $_.Name -match "^\d{2}-\d-" -or $_.Name -match "^\d{1}-\d-"
} | Sort-Object FullName

Write-Info "Найдено $($badDirs.Count) директорий с неправильными номерами:"
foreach ($dir in $badDirs) {
    $relativePath = $dir.FullName.Substring($PlanRoot.Length + 1)
    Write-Host "   📁 $relativePath" -ForegroundColor Yellow
}

Write-Info "`n=== ПЛАН ИСПРАВЛЕНИЯ ==="

# Создаем план переименования директорий
$renameOperations = @()

# Правила исправления для директорий Models
$dirMappings = @{
    "01-1-Core-Models" = "01-01-Core-Models"
    "01-1-Game-State" = "01-02-Game-State"
    "01-2-Progression" = "01-03-Progression" 
    "01-2-Progression-Models" = "01-04-Progression-Models"
    "01-3-API" = "01-05-API"
    "01-3-API-Models" = "01-06-API-Models"
    "01-4-CQRS-Models" = "01-07-CQRS-Models"
    "01-5-Unity-Bridge" = "01-08-Unity-Bridge"
}

foreach ($dir in $badDirs) {
    $oldName = $dir.Name
    $newName = $null
    
    # Проверяем точные соответствия
    if ($dirMappings.ContainsKey($oldName)) {
        $newName = $dirMappings[$oldName]
    }
    # Общий паттерн для остальных
    elseif ($oldName -match "^(\d{2})-(\d)-(.+)$") {
        $part1 = $matches[1]
        $part2 = $matches[2].PadLeft(2,'0')
        $suffix = $matches[3]
        $newName = "$part1-$part2-$suffix"
    }
    
    if ($newName -and $newName -ne $oldName) {
        $newPath = Join-Path (Split-Path $dir.FullName) $newName
        
        $renameOperations += @{
            OldPath = $dir.FullName
            NewPath = $newPath
            OldName = $oldName
            NewName = $newName
            RelativePath = $dir.FullName.Substring($PlanRoot.Length + 1)
            NewRelativePath = $newPath.Substring($PlanRoot.Length + 1)
        }
    }
}

Write-Info "Запланировано $($renameOperations.Count) переименований директорий"

if ($Preview -and $renameOperations.Count -gt 0) {
    Write-Info "`n=== PREVIEW: ПЕРЕИМЕНОВАНИЯ ДИРЕКТОРИЙ ==="
    
    foreach ($op in $renameOperations) {
        Write-Host "`n📁 $($op.RelativePath)" -ForegroundColor White
        Write-Host "   ❌ OLD: $($op.OldName)" -ForegroundColor Red
        Write-Host "   ✅ NEW: $($op.NewName)" -ForegroundColor Green
    }
}

# Теперь нужно также исправить дублирующие файлы
Write-Info "`n=== АНАЛИЗ ДУБЛИРУЮЩИХ ФАЙЛОВ ==="

$modelsDir = Join-Path $PlanRoot "01-Models"
$duplicateFiles = @()

# Найдем файлы с дублирующими номерами
$rootFiles = Get-ChildItem -Path $modelsDir -Filter "*.md" | Where-Object {
    $_.Name -match "^01-0\d-"
}

foreach ($file in $rootFiles) {
    if ($file.Name -eq "01-01-Game-State.md") {
        # Этот файл дублирует директорию, нужно переименовать
        $duplicateFiles += @{
            OldPath = $file.FullName
            NewPath = Join-Path $modelsDir "01-02-Game-State-Overview.md"
            OldName = $file.Name
            NewName = "01-02-Game-State-Overview.md"
            Reason = "Дублирует директорию 01-02-Game-State"
        }
    }
    elseif ($file.Name -eq "01-02-Progression.md") {
        # Этот файл дублирует директорию, нужно переименовать
        $duplicateFiles += @{
            OldPath = $file.FullName
            NewPath = Join-Path $modelsDir "01-03-Progression-Overview.md"
            OldName = $file.Name
            NewName = "01-03-Progression-Overview.md"
            Reason = "Дублирует директорию 01-03-Progression"
        }
    }
}

if ($duplicateFiles.Count -gt 0) {
    Write-Info "Найдено $($duplicateFiles.Count) дублирующих файлов:"
    foreach ($dup in $duplicateFiles) {
        Write-Host "   📄 $($dup.OldName) → $($dup.NewName)" -ForegroundColor Yellow
        Write-Host "      Причина: $($dup.Reason)" -ForegroundColor Gray
    }
}

if ($Execute -and ($renameOperations.Count -gt 0 -or $duplicateFiles.Count -gt 0)) {
    Write-Danger "`n=== ВЫПОЛНЕНИЕ ИСПРАВЛЕНИЙ ==="
    
    $confirm = Read-Host "Type 'FIX CATALOGIZATION' to proceed with $($renameOperations.Count + $duplicateFiles.Count) renames"
    
    if ($confirm -eq "FIX CATALOGIZATION") {
        Write-Info "`n--- ПЕРЕИМЕНОВАНИЕ ДИРЕКТОРИЙ ---"
        
        $successCount = 0
        $errorCount = 0
        
        # Сначала переименуем директории
        foreach ($op in $renameOperations) {
            try {
                if (Test-Path $op.NewPath) {
                    Write-Danger "❌ SKIP: Target already exists - $($op.NewName)"
                    $errorCount++
                    continue
                }
                
                Rename-Item -Path $op.OldPath -NewName $op.NewName
                Write-Safe "✅ DIR: $($op.OldName) → $($op.NewName)"
                $successCount++
                
            } catch {
                Write-Danger "❌ FAILED: $($op.OldName) - $($_.Exception.Message)"
                $errorCount++
            }
        }
        
        Write-Info "`n--- ПЕРЕИМЕНОВАНИЕ ДУБЛИРУЮЩИХ ФАЙЛОВ ---"
        
        # Затем переименуем дублирующие файлы
        foreach ($dup in $duplicateFiles) {
            try {
                if (Test-Path $dup.NewPath) {
                    Write-Danger "❌ SKIP: Target already exists - $($dup.NewName)"
                    $errorCount++
                    continue
                }
                
                Rename-Item -Path $dup.OldPath -NewName $dup.NewName
                Write-Safe "✅ FILE: $($dup.OldName) → $($dup.NewName)"
                $successCount++
                
            } catch {
                Write-Danger "❌ FAILED: $($dup.OldName) - $($_.Exception.Message)"
                $errorCount++
            }
        }
        
        Write-Safe "`n🎉 ИСПРАВЛЕНИЕ КАТАЛОГИЗАЦИИ ЗАВЕРШЕНО!"
        Write-Host "✅ Successfully renamed: $successCount items" -ForegroundColor Green
        if ($errorCount -gt 0) {
            Write-Host "❌ Failed renames: $errorCount items" -ForegroundColor Red
        }
        
    } else {
        Write-Info "❌ Operation cancelled"
    }
} elseif ($renameOperations.Count -eq 0 -and $duplicateFiles.Count -eq 0) {
    Write-Safe "`n✅ NO DUPLICATES FOUND - Catalogization is correct!"
}

Write-Info "`n=== FIX DIRECTORY CATALOGIZATION SUMMARY ==="
Write-Host "Bad directories found: $($badDirs.Count)" -ForegroundColor Cyan
Write-Host "Directory renames planned: $($renameOperations.Count)" -ForegroundColor Green
Write-Host "Duplicate file fixes planned: $($duplicateFiles.Count)" -ForegroundColor Green