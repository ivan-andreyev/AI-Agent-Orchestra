# Автоматизированная система ревью планов
# Версия: 2.0 (Расширенная автоматизация)
# Назначение: Полуавтоматическое и автоматическое исправление нарушений структуры

param(
    [string]$Path = "Docs/PLAN",
    [switch]$AutoFix = $false,
    [switch]$GenerateReport = $true,
    [switch]$Interactive = $true,
    [switch]$CommitChanges = $false
)

# Импорт основного валидатора будет вызван через &

Write-Host "🤖 АВТОМАТИЗИРОВАННАЯ СИСТЕМА РЕВЬЮ" -ForegroundColor Green
$modeText = if ($AutoFix) { "АВТОМАТИЧЕСКИЕ ИСПРАВЛЕНИЯ" } else { "ТОЛЬКО АНАЛИЗ" }
Write-Host "Режим: $modeText" -ForegroundColor Yellow
Write-Host "=" * 70

# Функция автоматического исправления размеров файлов
function Invoke-AutoFileSizefix {
    param([array]$CriticalFiles, [string]$BasePath)
    
    if ($CriticalFiles.Count -eq 0) {
        Write-Host "✅ Нет файлов для исправления размера" -ForegroundColor Green
        return @()
    }
    
    Write-Host "`n🔧 АВТОМАТИЧЕСКОЕ ИСПРАВЛЕНИЕ РАЗМЕРОВ ФАЙЛОВ:" -ForegroundColor Cyan
    
    $fixedFiles = @()
    
    foreach ($file in $CriticalFiles) {
        $filePath = $file.Path
        $lines = $file.Lines
        
        Write-Host "`n📝 Обрабатываю: $filePath ($lines строк)" -ForegroundColor Yellow
        
        if ($Interactive) {
            $response = Read-Host "Исправить автоматически? (y/n/s=skip)"
            if ($response -eq 'n' -or $response -eq 's') {
                Write-Host "⏭️  Пропускаю файл по запросу пользователя" -ForegroundColor Gray
                continue
            }
        }
        
        try {
            # Создаем каталог для декомпозиции
            $fileName = [System.IO.Path]::GetFileNameWithoutExtension($filePath)
            $directory = [System.IO.Path]::GetDirectoryName($filePath)
            $catalogDir = Join-Path $directory $fileName
            
            if (-not (Test-Path $catalogDir)) {
                New-Item -ItemType Directory -Path $catalogDir -Force | Out-Null
                Write-Host "📁 Создан каталог: $catalogDir" -ForegroundColor Green
            }
            
            # Читаем содержимое файла
            $content = Get-Content $filePath -Encoding UTF8
            
            # Автоматическая декомпозиция по заголовкам ##
            $sections = @()
            $currentSection = @{
                Title = "Введение"
                StartLine = 0
                Lines = @()
            }
            
            for ($i = 0; $i -lt $content.Length; $i++) {
                $line = $content[$i]
                
                if ($line -match '^## (.+)$') {
                    # Завершаем предыдущую секцию
                    if ($currentSection.Lines.Count -gt 0) {
                        $currentSection.EndLine = $i - 1
                        $sections += $currentSection
                    }
                    
                    # Начинаем новую секцию
                    $currentSection = @{
                        Title = $matches[1]
                        StartLine = $i
                        Lines = @()
                    }
                }
                
                $currentSection.Lines += $line
            }
            
            # Добавляем последнюю секцию
            if ($currentSection.Lines.Count -gt 0) {
                $currentSection.EndLine = $content.Length - 1
                $sections += $currentSection
            }
            
            # Создаем файлы для секций
            $createdFiles = @()
            $coordinationContent = @()
            $coordinationContent += "# $fileName"
            $coordinationContent += ""
            $coordinationContent += "**Родительский план**: [$(Split-Path $directory -Leaf).md](../$(Split-Path $directory -Leaf).md)"
            $coordinationContent += ""
            $coordinationContent += "## Обзор"
            $coordinationContent += ""
            $coordinationContent += "Этот раздел был автоматически декомпозирован из файла $lines строк для улучшения навигации."
            $coordinationContent += ""
            $coordinationContent += "## Структура раздела"
            $coordinationContent += ""
            
            for ($sectionIndex = 0; $sectionIndex -lt $sections.Count; $sectionIndex++) {
                $section = $sections[$sectionIndex]
                
                if ($section.Lines.Count -lt 10) {
                    continue # Пропускаем слишком маленькие секции
                }
                
                $sectionFileName = "$fileName-$($sectionIndex + 1)-$($section.Title -replace '[^\w\s-]', '' -replace '\s+', '-').md"
                $sectionFilePath = Join-Path $catalogDir $sectionFileName
                
                # Создаем содержимое секции
                $sectionContent = @()
                $sectionContent += "# $($section.Title)"
                $sectionContent += ""
                $sectionContent += "**Родительский план**: [$(Split-Path $filePath -Leaf)](../$(Split-Path $filePath -Leaf))"
                $sectionContent += ""
                $sectionContent += $section.Lines[1..($section.Lines.Count - 1)] # Пропускаем заголовок ##
                $sectionContent += ""
                $sectionContent += "---"
                $sectionContent += "**Статус**: ✅ Автоматически извлечено из исходного файла"
                
                # Записываем файл секции
                $sectionContent | Out-File -FilePath $sectionFilePath -Encoding UTF8
                $createdFiles += $sectionFilePath
                
                # Добавляем ссылку в координационный файл
                $relativePath = "./$fileName/$sectionFileName"
                $coordinationContent += "### ✅ [$($sectionIndex + 1). $($section.Title)]($relativePath)"
                $coordinationContent += "- $($section.Lines.Count) строк"
                $coordinationContent += ""
            }
            
            $coordinationContent += "---"
            $coordinationContent += "**Результат автоматической декомпозиции**:"
            $coordinationContent += "- **Было**: 1 файл, $lines строк"
            $coordinationContent += "- **Стало**: $($createdFiles.Count) файлов, оптимальный размер"
            $coordinationContent += "- **Дата**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
            
            # Создаем координационный файл
            $coordinationContent | Out-File -FilePath $filePath -Encoding UTF8
            
            Write-Host "✅ Файл декомпозирован:" -ForegroundColor Green
            Write-Host "   📄 Координационный файл: $(Split-Path $filePath -Leaf) ($($coordinationContent.Count) строк)" -ForegroundColor Green
            Write-Host "   📁 Создано подфайлов: $($createdFiles.Count)" -ForegroundColor Green
            
            $fixedFiles += @{
                OriginalFile = $filePath
                OriginalLines = $lines
                CoordinationFile = $filePath
                CreatedFiles = $createdFiles
                NewLines = $coordinationContent.Count
            }
            
        }
        catch {
            Write-Host "❌ Ошибка при обработке $filePath`: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    return $fixedFiles
}

# Функция автоматической каталогизации паттернов
function Invoke-AutoCatalogization {
    param([array]$CatalogPatterns, [string]$BasePath)
    
    if ($CatalogPatterns.Count -eq 0) {
        Write-Host "✅ Нет паттернов для каталогизации" -ForegroundColor Green
        return @()
    }
    
    Write-Host "`n🔧 АВТОМАТИЧЕСКАЯ КАТАЛОГИЗАЦИЯ ПАТТЕРНОВ:" -ForegroundColor Cyan
    
    $processedPatterns = @()
    
    foreach ($pattern in $CatalogPatterns) {
        $patternName = $pattern.Pattern
        $files = $pattern.Files
        
        Write-Host "`n📋 Паттерн: $patternName → $($files.Count) файлов" -ForegroundColor Yellow
        
        if ($Interactive) {
            Write-Host "Файлы для каталогизации:"
            $files | ForEach-Object { Write-Host "   - $($_)" -ForegroundColor Gray }
            
            $response = Read-Host "Каталогизировать автоматически? (y/n/s=skip)"
            if ($response -eq 'n' -or $response -eq 's') {
                Write-Host "⏭️  Пропускаю паттерн по запросу пользователя" -ForegroundColor Gray
                continue
            }
        }
        
        try {
            # Находим общий каталог
            $firstFile = $files[0]
            $commonDir = Split-Path $firstFile -Parent
            $catalogDirName = "$patternName-Group"
            $catalogDirPath = Join-Path $commonDir $catalogDirName
            
            # Создаем каталог для группы
            if (-not (Test-Path $catalogDirPath)) {
                New-Item -ItemType Directory -Path $catalogDirPath -Force | Out-Null
                Write-Host "📁 Создан каталог: $catalogDirPath" -ForegroundColor Green
            }
            
            # Перемещаем файлы в каталог
            $movedFiles = @()
            foreach ($file in $files) {
                $fileName = Split-Path $file -Leaf
                $newPath = Join-Path $catalogDirPath $fileName
                
                Move-Item -Path $file -Destination $newPath -Force
                $movedFiles += $newPath
                Write-Host "📝 Перемещен: $fileName → $catalogDirName/" -ForegroundColor Green
            }
            
            # Создаем координационный файл для группы
            $coordFileName = "$patternName-Group.md"
            $coordFilePath = Join-Path $commonDir $coordFileName
            
            $coordContent = @()
            $coordContent += "# $patternName Group"
            $coordContent += ""
            $coordContent += "Автоматически созданная группа файлов с общим паттерном `$patternName`."
            $coordContent += ""
            $coordContent += "## Файлы в группе"
            $coordContent += ""
            
            foreach ($movedFile in $movedFiles) {
                $fileName = Split-Path $movedFile -Leaf
                $relativePath = "./$catalogDirName/$fileName"
                $coordContent += "- [$fileName]($relativePath)"
            }
            
            $coordContent += ""
            $coordContent += "---"
            $coordContent += "**Автоматически каталогизировано**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
            
            $coordContent | Out-File -FilePath $coordFilePath -Encoding UTF8
            
            Write-Host "✅ Каталогизация завершена: $($movedFiles.Count) файлов в $catalogDirName/" -ForegroundColor Green
            
            $processedPatterns += @{
                Pattern = $patternName
                CatalogDirectory = $catalogDirPath
                CoordinationFile = $coordFilePath
                MovedFiles = $movedFiles
            }
            
        }
        catch {
            Write-Host "❌ Ошибка при каталогизации паттерна $patternName`: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    return $processedPatterns
}

# Функция генерации детального отчета
function New-ReviewReport {
    param(
        [hashtable]$ValidationResults,
        [array]$FixedFiles,
        [array]$ProcessedPatterns,
        [string]$OutputPath
    )
    
    Write-Host "`n📊 ГЕНЕРАЦИЯ ОТЧЕТА РЕВЬЮ..." -ForegroundColor Cyan
    
    $report = @()
    $report += "# Автоматизированный отчет ревью структуры"
    $report += ""
    $report += "**Дата**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $report += "**Инструмент**: AutomatedReviewSystem.ps1 v2.0"
    $report += ""
    
    # Сводка результатов
    $totalIssues = 0
    $totalIssues += $ValidationResults.FileSizes.Critical.Count
    $totalIssues += $ValidationResults.FileSizes.Warning.Count
    $totalIssues += $ValidationResults.CatalogPatterns.Count
    $totalIssues += $ValidationResults.CoordFiles.Count
    $totalIssues += $ValidationResults.BrokenLinks.Count
    
    $report += "## 📊 Сводка"
    $report += ""
    $report += "| Тип нарушения | Обнаружено | Исправлено |"
    $report += "|---------------|------------|------------|"
    $report += "| Файлы >400 строк | $($ValidationResults.FileSizes.Critical.Count) | $($FixedFiles.Count) |"
    $report += "| Файлы >250 строк | $($ValidationResults.FileSizes.Warning.Count) | - |"
    $report += "| Паттерны каталогизации | $($ValidationResults.CatalogPatterns.Count) | $($ProcessedPatterns.Count) |"
    $report += "| Пустые каталоги | $($ValidationResults.CoordFiles.Count) | - |"
    $report += "| Битые ссылки | $($ValidationResults.BrokenLinks.Count) | - |"
    $report += "| **ИТОГО** | **$totalIssues** | **$($FixedFiles.Count + $ProcessedPatterns.Count)** |"
    $report += ""
    
    # Детали исправлений файлов
    if ($FixedFiles.Count -gt 0) {
        $report += "## 🔧 Автоматические исправления размеров файлов"
        $report += ""
        foreach ($fix in $FixedFiles) {
            $report += "### ✅ $(Split-Path $fix.OriginalFile -Leaf)"
            $report += "- **Было**: $($fix.OriginalLines) строк (КРИТИЧЕСКОЕ)"
            $report += "- **Стало**: Координационный файл $($fix.NewLines) строк + $($fix.CreatedFiles.Count) подфайлов"
            $report += "- **Созданные файлы**:"
            foreach ($createdFile in $fix.CreatedFiles) {
                $report += "  - $(Split-Path $createdFile -Leaf)"
            }
            $report += ""
        }
    }
    
    # Детали каталогизации
    if ($ProcessedPatterns.Count -gt 0) {
        $report += "## 📁 Автоматическая каталогизация"
        $report += ""
        foreach ($pattern in $ProcessedPatterns) {
            $report += "### ✅ Паттерн: $($pattern.Pattern)"
            $report += "- **Каталог**: $(Split-Path $pattern.CatalogDirectory -Leaf)/"
            $report += "- **Файлов перемещено**: $($pattern.MovedFiles.Count)"
            $report += "- **Координационный файл**: $(Split-Path $pattern.CoordinationFile -Leaf)"
            $report += ""
        }
    }
    
    # Рекомендации
    $report += "## 💡 Рекомендации"
    $report += ""
    if ($ValidationResults.FileSizes.Warning.Count -gt 0) {
        $report += "- 📝 **Файлы >250 строк**: Рассмотрите возможность декомпозиции $($ValidationResults.FileSizes.Warning.Count) файлов"
    }
    if ($ValidationResults.BrokenLinks.Count -gt 0) {
        $report += "- 🔗 **Битые ссылки**: Требуется мануальное исправление $($ValidationResults.BrokenLinks.Count) ссылок"
    }
    if ($ValidationResults.CoordFiles.Count -gt 0) {
        $report += "- 📂 **Пустые каталоги**: Удалите или заполните $($ValidationResults.CoordFiles.Count) каталогов"
    }
    
    $report += ""
    $report += "---"
    $report += "**Следующий шаг**: Запустите повторную валидацию для проверки результатов"
    
    # Сохраняем отчет
    $reportPath = Join-Path $OutputPath "Review-Report-$(Get-Date -Format 'yyyy-MM-dd-HH-mm').md"
    $report | Out-File -FilePath $reportPath -Encoding UTF8
    
    Write-Host "📄 Отчет сохранен: $reportPath" -ForegroundColor Green
    return $reportPath
}

# Основная логика
try {
    # Шаг 1: Базовая валидация
    Write-Host "🔍 Этап 1: Базовая валидация структуры..." -ForegroundColor Cyan
    $validatorPath = Join-Path $PSScriptRoot "PlanStructureValidator.ps1"
    $validationResults = & $validatorPath -Path $Path
    
    if (-not $validationResults) {
        Write-Host "❌ Ошибка валидации. Проверьте базовый валидатор." -ForegroundColor Red
        exit 1
    }
    
    # Шаг 2: Автоматические исправления (если включены)
    $fixedFiles = @()
    $processedPatterns = @()
    
    if ($AutoFix) {
        Write-Host "`n🤖 Этап 2: Автоматические исправления..." -ForegroundColor Cyan
        
        # Исправление критических файлов
        $fixedFiles = Invoke-AutoFileSizefix -CriticalFiles $validationResults.FileSizes.Critical -BasePath $Path
        
        # Каталогизация паттернов (только простые случаи)
        $simplePatterns = $validationResults.CatalogPatterns | Where-Object { $_.Count -ge 3 -and $_.Count -le 10 }
        $processedPatterns = Invoke-AutoCatalogization -CatalogPatterns $simplePatterns -BasePath $Path
    }
    
    # Шаг 3: Генерация отчета
    if ($GenerateReport) {
        Write-Host "`n📊 Этап 3: Генерация отчета..." -ForegroundColor Cyan
        $reportPath = New-ReviewReport -ValidationResults $validationResults -FixedFiles $fixedFiles -ProcessedPatterns $processedPatterns -OutputPath "Tools"
    }
    
    # Шаг 4: Коммит изменений (если включен)
    if ($CommitChanges -and ($fixedFiles.Count -gt 0 -or $processedPatterns.Count -gt 0)) {
        Write-Host "`n📝 Этап 4: Коммит изменений..." -ForegroundColor Cyan
        
        $commitMessage = "Автоматическое исправление структуры планов`n`n"
        $commitMessage += "- Исправлено файлов: $($fixedFiles.Count)`n"
        $commitMessage += "- Каталогизировано паттернов: $($processedPatterns.Count)`n"
        $commitMessage += "- Отчет: $(Split-Path $reportPath -Leaf)"
        
        git add .
        git commit -m $commitMessage
        
        Write-Host "✅ Изменения зафиксированы в git" -ForegroundColor Green
    }
    
    # Итоги
    Write-Host "`n" + "=" * 70
    Write-Host "🎉 АВТОМАТИЗИРОВАННОЕ РЕВЬЮ ЗАВЕРШЕНО!" -ForegroundColor Green
    Write-Host "📊 Исправлено автоматически: $($fixedFiles.Count + $processedPatterns.Count) нарушений" -ForegroundColor Yellow
    
    if ($GenerateReport) {
        Write-Host "📄 Подробный отчет: $reportPath" -ForegroundColor Yellow
    }
    
    Write-Host "`n💡 Рекомендация: Запустите PlanStructureValidator.ps1 для проверки результатов" -ForegroundColor Cyan
    
}
catch {
    Write-Host "❌ КРИТИЧЕСКАЯ ОШИБКА: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}