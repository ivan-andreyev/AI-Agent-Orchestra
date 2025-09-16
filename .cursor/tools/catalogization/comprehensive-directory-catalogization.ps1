# COMPREHENSIVE DIRECTORY CATALOGIZATION + LINK FIXING
param(
    [switch]$Preview = $true,
    [switch]$Execute = $false
)

$PlanRoot = (Resolve-Path "Docs\PLAN\Galactic-Idlers-Plan").Path

function Write-Safety { param($msg) Write-Host $msg -ForegroundColor Magenta }
function Write-Danger { param($msg) Write-Host $msg -ForegroundColor Red -BackgroundColor Yellow }
function Write-Safe { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }

Write-Safety "=== COMPREHENSIVE DIRECTORY CATALOGIZATION + LINK FIXING ==="
Write-Info "Полная каталогизация директорий и исправление всех ссылок"

Write-Info "`n=== PHASE 1: АНАЛИЗ ДИРЕКТОРИЙ ==="

# Найдем все директории с неправильными номерами
$badDirs = Get-ChildItem -Path $PlanRoot -Recurse -Directory | Where-Object {
    $_.Name -match "^\d{2}-\d-" -or $_.Name -match "^\d{1}-\d-"
} | Sort-Object FullName

Write-Info "Найдено $($badDirs.Count) директорий для каталогизации"

# Создаем план переименования директорий
$dirRenameOperations = @()

# Правила исправления для основных директорий Models  
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
    
    # Проверяем точные соответствия для главных директорий
    if ($dirMappings.ContainsKey($oldName)) {
        $newName = $dirMappings[$oldName]
    }
    # Общий паттерн для остальных - простое padding
    elseif ($oldName -match "^(\d{2})-(\d)-(.+)$") {
        $part1 = $matches[1]
        $part2 = $matches[2].PadLeft(2,'0')
        $suffix = $matches[3]
        $newName = "$part1-$part2-$suffix"
    }
    # Глубокие паттерны
    elseif ($oldName -match "^(\d{2})-(\d)-(\d)-(.+)$") {
        $part1 = $matches[1]
        $part2 = $matches[2].PadLeft(2,'0')
        $part3 = $matches[3].PadLeft(2,'0')
        $suffix = $matches[4]
        $newName = "$part1-$part2-$part3-$suffix"
    }
    
    if ($newName -and $newName -ne $oldName) {
        $newPath = Join-Path (Split-Path $dir.FullName) $newName
        
        $dirRenameOperations += @{
            OldPath = $dir.FullName
            NewPath = $newPath
            OldName = $oldName
            NewName = $newName
            RelativePath = $dir.FullName.Substring($PlanRoot.Length + 1)
            NewRelativePath = $newPath.Substring($PlanRoot.Length + 1)
        }
    }
}

Write-Info "Запланировано $($dirRenameOperations.Count) переименований директорий"

Write-Info "`n=== PHASE 2: АНАЛИЗ ДУБЛИРУЮЩИХ ФАЙЛОВ ==="

$modelsDir = Join-Path $PlanRoot "01-Models"
$duplicateFiles = @()

# Найдем файлы с дублирующими номерами
$rootFiles = Get-ChildItem -Path $modelsDir -Filter "*.md" | Where-Object {
    $_.Name -match "^01-0\d-"
}

foreach ($file in $rootFiles) {
    if ($file.Name -eq "01-01-Game-State.md") {
        $duplicateFiles += @{
            OldPath = $file.FullName
            NewPath = Join-Path $modelsDir "01-02-Game-State-Overview.md"
            OldName = $file.Name
            NewName = "01-02-Game-State-Overview.md"
            Reason = "Дублирует директорию 01-02-Game-State"
        }
    }
    elseif ($file.Name -eq "01-02-Progression.md") {
        $duplicateFiles += @{
            OldPath = $file.FullName
            NewPath = Join-Path $modelsDir "01-03-Progression-Overview.md"
            OldName = $file.Name
            NewName = "01-03-Progression-Overview.md"
            Reason = "Дублирует директорию 01-03-Progression"
        }
    }
}

Write-Info "Найдено $($duplicateFiles.Count) дублирующих файлов для исправления"

Write-Info "`n=== PHASE 3: ПОДГОТОВКА ИСПРАВЛЕНИЯ ССЫЛОК ==="

# Собираем все изменения путей для создания карты замен ссылок
$linkReplacements = @{}

# Добавляем замены для директорий
foreach ($op in $dirRenameOperations) {
    $oldRelative = $op.RelativePath.Replace('\', '/')
    $newRelative = $op.NewRelativePath.Replace('\', '/')
    $linkReplacements[$oldRelative] = $newRelative
    
    # Добавляем также сокращенные варианты (только название директории)
    $oldDirName = Split-Path $oldRelative -Leaf
    $newDirName = Split-Path $newRelative -Leaf
    $linkReplacements[$oldDirName] = $newDirName
}

# Добавляем замены для файлов
foreach ($dup in $duplicateFiles) {
    $oldFileName = $dup.OldName
    $newFileName = $dup.NewName
    $linkReplacements[$oldFileName] = $newFileName
}

Write-Info "Подготовлено $($linkReplacements.Count) замен для ссылок"

if ($Preview) {
    Write-Info "`n=== PREVIEW: ПЕРЕИМЕНОВАНИЯ ДИРЕКТОРИЙ (первые 20) ==="
    
    $dirRenameOperations | Select-Object -First 20 | ForEach-Object {
        Write-Host "`n📁 $($_.RelativePath)" -ForegroundColor White
        Write-Host "   ❌ OLD: $($_.OldName)" -ForegroundColor Red
        Write-Host "   ✅ NEW: $($_.NewName)" -ForegroundColor Green
    }
    
    if ($dirRenameOperations.Count -gt 20) {
        Write-Host "`n... и ещё $($dirRenameOperations.Count - 20) переименований директорий" -ForegroundColor Yellow
    }
    
    if ($duplicateFiles.Count -gt 0) {
        Write-Info "`n=== PREVIEW: ДУБЛИРУЮЩИЕ ФАЙЛЫ ==="
        foreach ($dup in $duplicateFiles) {
            Write-Host "`n📄 $($dup.OldName) → $($dup.NewName)" -ForegroundColor Yellow
            Write-Host "   Причина: $($dup.Reason)" -ForegroundColor Gray
        }
    }
    
    Write-Info "`n=== PREVIEW: ПРИМЕРЫ ЗАМЕН ССЫЛОК ==="
    $linkReplacements.GetEnumerator() | Select-Object -First 10 | ForEach-Object {
        Write-Host "   🔗 $($_.Key) → $($_.Value)" -ForegroundColor Cyan
    }
    
    if ($linkReplacements.Count -gt 10) {
        Write-Host "`n... и ещё $($linkReplacements.Count - 10) замен ссылок" -ForegroundColor Yellow
    }
}

if ($Execute) {
    Write-Danger "`n=== ВЫПОЛНЕНИЕ КОМПЛЕКСНОЙ КАТАЛОГИЗАЦИИ ==="
    
    $confirm = Read-Host "Type 'EXECUTE COMPREHENSIVE CATALOGIZATION' to proceed with $($dirRenameOperations.Count + $duplicateFiles.Count) renames + link fixes"
    
    if ($confirm -eq "EXECUTE COMPREHENSIVE CATALOGIZATION") {
        $successCount = 0
        $errorCount = 0
        
        Write-Info "`n--- STEP 1: ПЕРЕИМЕНОВАНИЕ ДИРЕКТОРИЙ ---"
        
        # Сортируем по глубине (сначала самые глубокие, чтобы не было конфликтов)
        $sortedDirOps = $dirRenameOperations | Sort-Object { ($_.RelativePath -split '\\').Count } -Descending
        
        foreach ($op in $sortedDirOps) {
            try {
                if (Test-Path $op.NewPath) {
                    Write-Danger "❌ SKIP: Target exists - $($op.NewName)"
                    $errorCount++
                    continue
                }
                
                Rename-Item -Path $op.OldPath -NewName $op.NewName
                Write-Safe "✅ DIR: $($op.OldName) → $($op.NewName)"
                $successCount++
                
            } catch {
                Write-Danger "❌ FAILED DIR: $($op.OldName) - $($_.Exception.Message)"
                $errorCount++
            }
        }
        
        Write-Info "`n--- STEP 2: ПЕРЕИМЕНОВАНИЕ ДУБЛИРУЮЩИХ ФАЙЛОВ ---"
        
        foreach ($dup in $duplicateFiles) {
            try {
                if (Test-Path $dup.NewPath) {
                    Write-Danger "❌ SKIP: Target exists - $($dup.NewName)"
                    $errorCount++
                    continue
                }
                
                Rename-Item -Path $dup.OldPath -NewName $dup.NewName
                Write-Safe "✅ FILE: $($dup.OldName) → $($dup.NewName)"
                $successCount++
                
            } catch {
                Write-Danger "❌ FAILED FILE: $($dup.OldName) - $($_.Exception.Message)"
                $errorCount++
            }
        }
        
        Write-Info "`n--- STEP 3: ИСПРАВЛЕНИЕ ССЫЛОК В ФАЙЛАХ ---"
        
        # Находим все markdown файлы для обновления ссылок
        $allMarkdownFiles = Get-ChildItem -Path $PlanRoot -Recurse -Filter "*.md" | Where-Object { 
            $_.FullName -notmatch "\\reviews\\" 
        }
        
        Write-Info "Обновляем ссылки в $($allMarkdownFiles.Count) файлах..."
        
        $linkFixesCount = 0
        $filesWithLinksFixed = 0
        
        foreach ($file in $allMarkdownFiles) {
            try {
                $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
                if (-not $content) { continue }
                
                $originalContent = $content
                $fileChanges = 0
                
                foreach ($oldLink in $linkReplacements.Keys) {
                    $newLink = $linkReplacements[$oldLink]
                    $beforeCount = ([regex]::Matches($content, [regex]::Escape($oldLink))).Count
                    
                    if ($beforeCount -gt 0) {
                        $content = $content -replace [regex]::Escape($oldLink), $newLink
                        $fileChanges += $beforeCount
                        $linkFixesCount += $beforeCount
                    }
                }
                
                if ($content -ne $originalContent) {
                    Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
                    $filesWithLinksFixed++
                    
                    $relativePath = $file.FullName.Substring($PlanRoot.Length + 1)
                    Write-Safe "✅ LINKS: $relativePath ($fileChanges fixes)"
                }
                
            } catch {
                Write-Danger "❌ FAILED LINKS: $($file.Name) - $($_.Exception.Message)"
                $errorCount++
            }
        }
        
        Write-Safe "`n🎉 КОМПЛЕКСНАЯ КАТАЛОГИЗАЦИЯ ЗАВЕРШЕНА!"
        Write-Host "✅ Directory renames: $($dirRenameOperations.Count)" -ForegroundColor Green
        Write-Host "✅ File renames: $($duplicateFiles.Count)" -ForegroundColor Green
        Write-Host "✅ Files with links fixed: $filesWithLinksFixed" -ForegroundColor Green
        Write-Host "✅ Total link fixes: $linkFixesCount" -ForegroundColor Green
        
        if ($errorCount -gt 0) {
            Write-Host "❌ Errors encountered: $errorCount" -ForegroundColor Red
        }
        
    } else {
        Write-Info "❌ Operation cancelled"
    }
}

Write-Info "`n=== COMPREHENSIVE CATALOGIZATION SUMMARY ==="
Write-Host "Directories to rename: $($dirRenameOperations.Count)" -ForegroundColor Cyan
Write-Host "Duplicate files to fix: $($duplicateFiles.Count)" -ForegroundColor Cyan
Write-Host "Link replacements prepared: $($linkReplacements.Count)" -ForegroundColor Cyan
Write-Host "Total operations: $($dirRenameOperations.Count + $duplicateFiles.Count + $linkReplacements.Count)" -ForegroundColor Green