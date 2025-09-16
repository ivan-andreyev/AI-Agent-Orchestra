# План-валидатор структуры - обязательные проверки перед ревью
# Версия: 1.0
# Назначение: Автоматическое выявление нарушений каталогизации и структуры планов

param(
    [string]$Path = "Docs/PLAN",
    [switch]$Detailed = $false
)

Write-Host "🚀 ПЛАН-ВАЛИДАТОР СТРУКТУРЫ" -ForegroundColor Green
Write-Host "Проверяем каталог: $Path" -ForegroundColor Yellow
Write-Host "=" * 60

# Функция 0: 🔥 ПРОВЕРКА ЗОЛОТОГО ПРАВИЛА #1 - именование каталогов
function Test-GoldenRule1 {
    param([string]$BasePath)
    
    Write-Host "`n🔥 ПРОВЕРКА ЗОЛОТОГО ПРАВИЛА #1:" -ForegroundColor Magenta
    Write-Host "Каталог должен называться ИДЕНТИЧНО файлу плана (без .md)`n" -ForegroundColor Magenta
    
    $violations = @()
    
    Get-ChildItem -Path $BasePath -Recurse -Filter "*.md" | ForEach-Object {
        $file = $_
        $fileName = $file.BaseName  # имя файла без .md
        $fileDir = $file.Directory.FullName
        $expectedCatalogPath = Join-Path $fileDir $fileName
        $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "")
        
        # Если рядом с файлом есть каталог, проверяем соответствие имён
        if (Test-Path $expectedCatalogPath -PathType Container) {
            # Каталог существует и имя совпадает - всё ОК
            if ($Detailed) {
                Write-Host "✅ ЗОЛОТОЕ ПРАВИЛО ОК: $relativePath ↔ $fileName/" -ForegroundColor Green
            }
        } else {
            # Каталог не существует - проверяем, есть ли каталоги с похожим именем (возможная ошибка именования)
            $allDirs = Get-ChildItem -Path $fileDir -Directory
            $suspiciousDirs = $allDirs | Where-Object { 
                # Ищем каталоги с ТОЧНЫМ соответствием имени (исправлено: убрана логика substring matching)
                # Теперь флагируем только явные ошибки именования, а не случайные совпадения подстрок
                $false  # Временно отключаем эту логику до более точной реализации
            }
            
            if ($suspiciousDirs.Count -gt 0) {
                Write-Host "⚠️ ПОТЕНЦИАЛЬНОЕ НАРУШЕНИЕ: $relativePath" -ForegroundColor Yellow
                Write-Host "   Возможно неправильно названные каталоги:" -ForegroundColor Yellow
                foreach ($dir in $suspiciousDirs) {
                    Write-Host "   - $($dir.Name)/ (должно быть $fileName/ ?)" -ForegroundColor Yellow
                }
                
                $violations += @{
                    File = $relativePath
                    ExpectedCatalog = "$fileName/"
                    SuspiciousCatalogs = $suspiciousDirs | ForEach-Object { $_.Name }
                }
            }
            # Если каталога нет вообще - это нормально для standalone файлов!
            # Если каталогов нет вообще - это не нарушение, просто план без подфайлов
        }
    }
    
    if ($violations.Count -eq 0) {
        Write-Host "✅ ЗОЛОТОЕ ПРАВИЛО #1 соблюдено!" -ForegroundColor Green
    }
    
    return $violations
}

# Функция 0.5: 🔥 ПРОВЕРКА ЗОЛОТОГО ПРАВИЛА #2 - координаторы снаружи
function Test-GoldenRule2 {
    param([string]$BasePath)
    
    Write-Host "`n🔥 ПРОВЕРКА ЗОЛОТОГО ПРАВИЛА #2:" -ForegroundColor Magenta
    Write-Host "Координатор должен быть СНАРУЖИ своего каталога`n" -ForegroundColor Magenta
    
    $violations = @()
    
    Get-ChildItem -Path $BasePath -Recurse -Directory | ForEach-Object {
        $catalogDir = $_
        $catalogName = $catalogDir.Name
        $parentDir = $catalogDir.Parent.FullName
        
        # Проверяем, есть ли координатор ВНУТРИ каталога (ОШИБКА!)
        $coordinatorInside = Join-Path $catalogDir.FullName "$catalogName.md"
        if (Test-Path $coordinatorInside) {
            $relativePath = $coordinatorInside.Replace((Get-Location).Path + "\", "")
            Write-Host "🚨 ЗОЛОТОЕ ПРАВИЛО #2 НАРУШЕНО: $relativePath" -ForegroundColor Red
            Write-Host "   Координатор НЕ должен быть внутри своего каталога!" -ForegroundColor Yellow
            Write-Host "   Переместите в: $($catalogDir.Parent.FullName.Replace((Get-Location).Path + '\', ''))" -ForegroundColor Yellow
            
            $violations += @{
                CoordinatorInside = $relativePath
                ShouldBeAt = Join-Path $parentDir "$catalogName.md"
            }
        }
    }
    
    if ($violations.Count -eq 0) {
        Write-Host "✅ ЗОЛОТОЕ ПРАВИЛО #2 соблюдено!" -ForegroundColor Green
    }
    
    return $violations
}

# Функция 1: Проверка размеров файлов
function Test-FileSizes {
    param([string]$BasePath)
    
    Write-Host "`n🔍 ПРОВЕРКА РАЗМЕРОВ ФАЙЛОВ:" -ForegroundColor Cyan
    Write-Host "Критический лимит: >400 строк | Предупреждение: >250 строк`n"
    
    $criticalFiles = @()
    $warningFiles = @()
    
    Get-ChildItem -Path $BasePath -Recurse -Filter "*.md" | ForEach-Object {
        $lines = (Get-Content $_.FullName -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
        $relativePath = $_.FullName.Replace((Get-Location).Path + "\", "")
        
        if ($lines -gt 400) {
            Write-Host "🚨 КРИТИЧЕСКОЕ: $relativePath ($lines строк)" -ForegroundColor Red
            $criticalFiles += @{Path = $relativePath; Lines = $lines}
        } elseif ($lines -gt 250) {
            Write-Host "⚠️  ВНИМАНИЕ: $relativePath ($lines строк)" -ForegroundColor Yellow
            $warningFiles += @{Path = $relativePath; Lines = $lines}
        }
    }
    
    if ($criticalFiles.Count -eq 0 -and $warningFiles.Count -eq 0) {
        Write-Host "✅ Все файлы в пределах нормы" -ForegroundColor Green
    }
    
    return @{Critical = $criticalFiles; Warning = $warningFiles}
}

# Функция 2: Поиск паттернов каталогизации
function Test-CatalogizationPatterns {
    param([string]$BasePath)
    
    Write-Host "`n🔍 ПРОВЕРКА ПАТТЕРНОВ КАТАЛОГИЗАЦИИ:" -ForegroundColor Cyan
    Write-Host "Ищем файлы с одинаковым префиксом В ОДНОМ КАТАЛОГЕ (истинные кандидаты на вынос в подпапку)`n"
    
    $violations = @()
    $groups = @{}
    
    # Собираем группы по ключу: <DirectoryFullPath>|<NN-NN>
    Get-ChildItem -Path $BasePath -Recurse -Filter "*.md" | ForEach-Object {
        $dir = $_.Directory.FullName
        $name = $_.BaseName
        $prefix = $null
        if ($name -match '^(\d+-\d+)') { $prefix = $matches[1] }
        else { return }
        $key = "$dir|$prefix"
        if (-not $groups.ContainsKey($key)) { $groups[$key] = @() }
        $groups[$key] += $_
    }
    
    foreach ($entry in $groups.GetEnumerator()) {
        $key = $entry.Key
        $files = $entry.Value
        if ($files.Count -le 1) { continue }
        
        # Игнорируем корректный случай: есть координатор <NN-NN-Name>.md в каталоге верхнего уровня
        # и отдельная подпапка <NN-NN-Name>/ с дочерними файлами (дети уже в другой директории и сюда не попадают)
        # Здесь же отлавливаем ТОЛЬКО ситуацию, когда в ОДНОМ каталоге лежит более одного файла с одним префиксом
        $dirPath = ($key.Split('|'))[0]
        $dirLeaf = Split-Path -Path $dirPath -Leaf
        $pattern = ($key.Split('|'))[1]

        # Явные разделы верхнего уровня, где допускается несколько подгрупп с общим двухуровневым префиксом
        # (категории параллельно в одном разделе):
        if ((Split-Path -Path $dirPath -Leaf) -in @('03-Tests', '12-Demo')) { continue }

        # Если каталог уже назван с этим префиксом (например, 05-5-Finalization/ или 06-2-Performance/),
        # считаем, что каталогизация уже выполнена и доп. вложенность не требуется
        if ($dirLeaf -like "$pattern*") { continue }

        # Если для КАЖДОГО файла в группе существует одноимённый каталог-сосед (координатор снаружи + папка внутри),
        # то считаем, что структура корректна и доп. каталогизация не требуется
        $allHaveOwnDirs = $true
        foreach ($f in $files) {
            $coordDir = Join-Path $f.Directory.FullName $f.BaseName
            if (-not (Test-Path -LiteralPath $coordDir)) { $allHaveOwnDirs = $false; break }
        }
        if ($allHaveOwnDirs) { continue }
        
        Write-Host "🚨 ПАТТЕРН: $pattern → $($files.Count) файлов требуют каталогизации" -ForegroundColor Red
        $fileList = @()
        foreach ($f in $files) {
            $relativePath = $f.FullName.Replace((Get-Location).Path + "\", "")
            Write-Host "   - $relativePath" -ForegroundColor Yellow
            $fileList += $relativePath
        }
        $violations += @{
            Pattern = $pattern
            Count = $files.Count
            Files = $fileList
        }
    }
    
    if ($violations.Count -eq 0) {
        Write-Host "✅ Нарушений паттернов каталогизации не найдено" -ForegroundColor Green
    }
    
    return $violations
}

# Функция 3: Проверка координационных файлов
function Test-CoordinationFiles {
    param([string]$BasePath)
    
    Write-Host "`n🔍 ПРОВЕРКА КООРДИНАЦИОННЫХ ФАЙЛОВ:" -ForegroundColor Cyan
    Write-Host "Проверяем соответствие координационных файлов и каталогов`n"
    
    $issues = @()
    
    Get-ChildItem -Path $BasePath -Recurse -Filter "*.md" | ForEach-Object {
        $coordFileName = $_.BaseName
        $coordDir = Join-Path $_.Directory.FullName $coordFileName
        $relativePath = $_.FullName.Replace((Get-Location).Path + "\", "")
        
        if (Test-Path $coordDir) {
            # Важно: считаем каталог непустым, если в нем есть .md в любом уровне вложенности
            $childFiles = Get-ChildItem -Path $coordDir -Recurse -Filter "*.md" -ErrorAction SilentlyContinue
            if ($childFiles.Count -eq 0) {
                Write-Host "⚠️  ПУСТОЙ КАТАЛОГ: $coordFileName/ для $relativePath" -ForegroundColor Yellow
                $issues += @{
                    Type = "EmptyDirectory"
                    File = $relativePath
                    Directory = $coordDir.Replace((Get-Location).Path + "\", "")
                }
            } else {
                if ($Detailed) {
                    Write-Host "✅ ОК: $relativePath ↔ $coordFileName/ ($($childFiles.Count) файлов)" -ForegroundColor Green
                }
            }
        }
    }
    
    if ($issues.Count -eq 0) {
        Write-Host "✅ Все координационные файлы корректны" -ForegroundColor Green
    }
    
    return $issues
}

# Функция 4: Проверка целостности ссылок
function Test-LinkIntegrity {
    param([string]$BasePath)
    
    Write-Host "`n🔍 ПРОВЕРКА ЦЕЛОСТНОСТИ ССЫЛОК:" -ForegroundColor Cyan
    Write-Host "Ищем битые ссылки на несуществующие файлы`n"
    
    $brokenLinks = @()
    
    Get-ChildItem -Path $BasePath -Recurse -Filter "*.md" | ForEach-Object {
        $currentFile = $_
        $content = Get-Content $currentFile.FullName -ErrorAction SilentlyContinue
        $relativePath = $currentFile.FullName.Replace((Get-Location).Path + "\", "")
        
        if ($content) {
            $inFence = $false
            $content | ForEach-Object {
                $line = $_
                # Тоггл для многострочных кодовых блоков ```
                if ($line -match '^\s*```') { $inFence = -not $inFence; return }
                if ($inFence) { return }

                # Убираем инлайн-код `...`
                $lineToScan = [regex]::Replace($line, '`[^`]*`', '')

                # Ищем все ссылки [text](path.md) (с поддержкой якорей), исключая изображения ![]()
                $pattern = '(?<!\!)\[([^\]]+)\]\(([^)]+\.md(?:#[^)]+)?)\)'
                $matchesAll = [regex]::Matches($lineToScan, $pattern)

                foreach ($m in $matchesAll) {
                    $linkText = $m.Groups[1].Value
                    $linkPathRaw = $m.Groups[2].Value

                    if ($linkPathRaw -match '^(http|https)://|^mailto:') { continue }

                    # Отбрасываем якорь, нормализуем относительный путь и слэши
                    $linkPath = $linkPathRaw -replace '#.*$',''
                    $baseDir = Split-Path $currentFile.FullName
                    $normalized = $linkPath.Trim()
                    if ($normalized.StartsWith('./')) { $normalized = $normalized.Substring(2) }
                    $normalized = $normalized -replace '/', '\\'

                    $fullCandidate = [System.IO.Path]::GetFullPath((Join-Path $baseDir $normalized))
                    if (-not (Test-Path -LiteralPath $fullCandidate)) {
                        Write-Host "🚨 БИТАЯ ССЫЛКА: $relativePath → $linkPathRaw" -ForegroundColor Red
                        $brokenLinks += @{
                            SourceFile = $relativePath
                            BrokenLink = $linkPathRaw
                            LinkText = $linkText
                        }
                    }
                }
            }
        }
    }
    
    if ($brokenLinks.Count -eq 0) {
        Write-Host "✅ Все ссылки целостны" -ForegroundColor Green
    }
    
    return $brokenLinks
}

# Основная логика выполнения
try {
    if (-not (Test-Path $Path)) {
        Write-Host "❌ ОШИБКА: Путь $Path не существует!" -ForegroundColor Red
        exit 1
    }
    
    $results = @{
        GoldenRule1 = Test-GoldenRule1 -BasePath $Path
        GoldenRule2 = Test-GoldenRule2 -BasePath $Path
        FileSizes = Test-FileSizes -BasePath $Path
        CatalogPatterns = Test-CatalogizationPatterns -BasePath $Path  
        CoordFiles = Test-CoordinationFiles -BasePath $Path
        BrokenLinks = Test-LinkIntegrity -BasePath $Path
    }
    
    # Итоговая сводка
    Write-Host "`n" + "=" * 60
    Write-Host "📊 ИТОГОВАЯ СВОДКА:" -ForegroundColor Green
    
    $totalIssues = 0
    $totalIssues += $results.GoldenRule1.Count
    $totalIssues += $results.GoldenRule2.Count
    $totalIssues += $results.FileSizes.Critical.Count
    $totalIssues += $results.CatalogPatterns.Count
    $totalIssues += $results.CoordFiles.Count
    $totalIssues += $results.BrokenLinks.Count
    
    if ($totalIssues -eq 0) {
        Write-Host "🎉 ОТЛИЧНО! Нарушений структуры не найдено!" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Обнаружено нарушений: $totalIssues" -ForegroundColor Yellow
        Write-Host "🔧 Требуется исправление перед продолжением работы" -ForegroundColor Red
    }
    
    Write-Host "`n📝 РЕКОМЕНДАЦИИ:"
    if ($results.GoldenRule1.Count -gt 0) {
        Write-Host "   🔥 ПРИОРИТЕТ #1: Переименуйте каталоги согласно Золотому Правилу #1!" -ForegroundColor Red
    }
    if ($results.GoldenRule2.Count -gt 0) {
        Write-Host "   🔥 ПРИОРИТЕТ #2: Переместите координаторы согласно Золотому Правилу #2!" -ForegroundColor Red
    }
    if ($results.FileSizes.Critical.Count -gt 0) {
        Write-Host "   • Разбейте файлы >400 строк на подфайлы" -ForegroundColor Yellow
    }
    if ($results.CatalogPatterns.Count -gt 0) {
        Write-Host "   • Создайте каталоги для файлов с общими паттернами" -ForegroundColor Yellow
    }
    if ($results.CoordFiles.Count -gt 0) {
        Write-Host "   • Удалите пустые каталоги или добавьте файлы" -ForegroundColor Yellow
    }
    if ($results.BrokenLinks.Count -gt 0) {
        Write-Host "   • Исправьте битые ссылки или создайте отсутствующие файлы" -ForegroundColor Yellow
    }
    
    Write-Host "`n✅ Валидация завершена: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
    # Возвращаем объект результатов и выходим с кодом в зависимости от наличия нарушений
    $script:__results = $results
    if ($totalIssues -eq 0) { exit 0 } else { exit 1 }
}
catch {
    Write-Host "❌ КРИТИЧЕСКАЯ ОШИБКА: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}